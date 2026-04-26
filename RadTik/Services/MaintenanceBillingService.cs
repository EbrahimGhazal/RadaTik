using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Services;

public sealed class MaintenanceInvoiceIssueResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? InvoiceId { get; init; }
}

public sealed class MaintenanceInvoicePaymentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public bool InsufficientBalance { get; init; }
    public decimal RequiredAmount { get; init; }
}

public interface IMaintenanceBillingService
{
    Task<MaintenanceInvoiceIssueResult> IssueInvoiceForCompletedRequestAsync(
        int requestId,
        string issuedByUserId,
        string faultExplanation,
        string fixExplanation,
        IReadOnlyCollection<MaintenanceType>? selectedMaintenanceTypes = null,
        CancellationToken ct = default);

    Task<MaintenanceInvoicePaymentResult> PayInvoiceFromClientWalletAsync(
        int invoiceId,
        string paidByUserId,
        CancellationToken ct = default);
}

public sealed class MaintenanceBillingService : IMaintenanceBillingService
{
    private readonly ApplicationDbContext _db;
    private readonly RequestNotificationService _notifications;
    private readonly ILogger<MaintenanceBillingService> _logger;

    public MaintenanceBillingService(
        ApplicationDbContext db,
        RequestNotificationService notifications,
        ILogger<MaintenanceBillingService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<MaintenanceInvoiceIssueResult> IssueInvoiceForCompletedRequestAsync(
        int requestId,
        string issuedByUserId,
        string faultExplanation,
        string fixExplanation,
        IReadOnlyCollection<MaintenanceType>? selectedMaintenanceTypes = null,
        CancellationToken ct = default)
    {
        var request = await _db.MaintenanceRequests
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request?.Client == null)
        {
            return new MaintenanceInvoiceIssueResult { ErrorMessage = "طلب الصيانة أو بيانات العميل غير موجودة." };
        }

        var existingId = await _db.MaintenanceInvoices
            .AsNoTracking()
            .Where(i => i.MaintenanceRequestId == requestId)
            .Select(i => (int?)i.Id)
            .FirstOrDefaultAsync(ct);
        if (existingId.HasValue)
        {
            return new MaintenanceInvoiceIssueResult
            {
                Success = true,
                InvoiceId = existingId.Value
            };
        }

        var networkId = request.Client.NetworkId;
        if (!networkId.HasValue)
        {
            return new MaintenanceInvoiceIssueResult { ErrorMessage = "العميل غير مرتبط بشبكة." };
        }

        var companyNetworkId = await ResolveCompanyNetworkIdAsync(networkId.Value, ct);
        if (!companyNetworkId.HasValue)
        {
            return new MaintenanceInvoiceIssueResult { ErrorMessage = "تعذر تحديد شبكة الشركة." };
        }

        var selectedTypes = (selectedMaintenanceTypes ?? [])
            .Where(MaintenanceCatalog.IsSolutionType)
            .Distinct()
            .ToList();
        if (selectedTypes.Count == 0 && MaintenanceCatalog.IsSolutionType(request.Type))
        {
            selectedTypes.Add(request.Type);
        }
        if (selectedTypes.Count == 0)
        {
            return new MaintenanceInvoiceIssueResult
            {
                ErrorMessage = "يرجى اختيار طريقة حل واحدة على الأقل لإصدار الفاتورة."
            };
        }

        var fixedPriceCandidates = await _db.NetworkMaintenancePrices
            .AsNoTracking()
            .Where(p =>
                p.NetworkId == companyNetworkId.Value &&
                selectedTypes.Contains(p.MaintenanceType))
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);

        var fixedPricesByType = fixedPriceCandidates
            .GroupBy(p => p.MaintenanceType)
            .ToDictionary(g => g.Key, g => g.First());

        var missingTypes = selectedTypes
            .Where(t => !fixedPricesByType.ContainsKey(t))
            .ToList();
        if (missingTypes.Count > 0)
        {
            return new MaintenanceInvoiceIssueResult
            {
                ErrorMessage = "بعض العناصر المختارة لا تملك سعراً ثابتاً. راجع تسعير الصيانة لدى مدير الشركة."
            };
        }

        var selectedFixedPrices = selectedTypes
            .Select(t => fixedPricesByType[t])
            .ToList();

        var transport = await ResolveSystemPricingAsync(FeatureKeys.MaintenanceTransportFee, ct);
        if (transport == null)
        {
            return new MaintenanceInvoiceIssueResult
            {
                ErrorMessage = "تعذر تحميل أجور النقل من إعدادات النظام."
            };
        }

        var commission = await ResolveSystemPricingAsync(FeatureKeys.MaintenanceCommission, ct);
        if (commission == null)
        {
            return new MaintenanceInvoiceIssueResult
            {
                ErrorMessage = "تعذر تحميل عمولة الصيانة من إعدادات النظام."
            };
        }

        var serviceBasePrice = selectedFixedPrices.Sum(p => p.AmountSYP);
        var subtotalBeforeCommission = serviceBasePrice + transport.Value;
        var commissionAmount = commission.Mode == MaintenanceCommissionMode.Percent
            ? WalletMath.CeilSyp(subtotalBeforeCommission * (commission.Value / 100m))
            : WalletMath.CeilSyp(commission.Value);
        if (commissionAmount < 0m)
        {
            commissionAmount = 0m;
        }

        // العميل يدفع (الخدمة + النقل + العمولة)، بينما صافي الشركة يبقى دون العمولة.
        var gross = Math.Max(0m, subtotalBeforeCommission + commissionAmount);
        var net = Math.Max(0m, gross - commissionAmount);

        var invoice = new MaintenanceInvoice
        {
            MaintenanceRequestId = requestId,
            ClientId = request.ClientId,
            NetworkId = companyNetworkId.Value,
            IssuedByUserId = issuedByUserId,
            FaultExplanation = faultExplanation.Trim(),
            FixExplanation = BuildFixExplanationWithSelectedItems(fixExplanation, selectedFixedPrices),
            ServiceBasePrice = serviceBasePrice,
            TransportFee = transport.Value,
            GrossAmount = gross,
            CommissionMode = commission.Mode,
            CommissionValue = commission.Value,
            CommissionAmount = commissionAmount,
            NetAmountToCompany = net,
            Status = MaintenanceInvoiceStatus.Pending,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.MaintenanceInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyMaintenanceInvoiceIssuedAsync(invoice, request.Client.UserName, request.Client.Name);

        return new MaintenanceInvoiceIssueResult
        {
            Success = true,
            InvoiceId = invoice.Id
        };
    }

    public async Task<MaintenanceInvoicePaymentResult> PayInvoiceFromClientWalletAsync(
        int invoiceId,
        string paidByUserId,
        CancellationToken ct = default)
    {
        var invoice = await _db.MaintenanceInvoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice?.Client == null)
        {
            return new MaintenanceInvoicePaymentResult { ErrorMessage = "الفاتورة غير موجودة." };
        }

        if (invoice.Status != MaintenanceInvoiceStatus.Pending)
        {
            return new MaintenanceInvoicePaymentResult { ErrorMessage = "لا يمكن تسديد الفاتورة لأن حالتها ليست بانتظار السداد." };
        }

        var company = await _db.Networks.FirstOrDefaultAsync(n => n.Id == invoice.NetworkId, ct);
        if (company == null)
        {
            return new MaintenanceInvoicePaymentResult { ErrorMessage = "شبكة الشركة غير موجودة." };
        }

        if (invoice.Client.Balance < invoice.GrossAmount)
        {
            return new MaintenanceInvoicePaymentResult
            {
                InsufficientBalance = true,
                RequiredAmount = invoice.GrossAmount
            };
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var previousClientBalance = invoice.Client.Balance;
            invoice.Client.Balance -= invoice.GrossAmount;
            invoice.Client.LastUpdated = DateTime.Now;

            var previousCompanyBalance = company.Balance;
            company.Balance += invoice.NetAmountToCompany;

            var payment = new PaymentTransaction
            {
                ClientId = invoice.ClientId,
                NetworkId = invoice.NetworkId,
                Amount = invoice.GrossAmount,
                PaymentDate = DateTime.Now,
                ReceivedByUserId = paidByUserId,
                OperationType = "MaintenanceInvoicePayment",
                ReferenceNumber = $"MIN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40],
                Notes = $"تسديد فاتورة صيانة #{invoice.Id}",
                PreviousClientBalance = previousClientBalance,
                NewClientBalance = invoice.Client.Balance,
                PreviousPointBalance = 0m,
                NewPointBalance = 0m
            };
            _db.PaymentTransactions.Add(payment);

            _db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
            {
                NetworkId = invoice.NetworkId,
                Type = NetworkWalletTransactionType.MaintenanceRevenue,
                SignedAmount = invoice.NetAmountToCompany,
                PreviousBalance = previousCompanyBalance,
                NewBalance = company.Balance,
                RelatedPaymentTransaction = payment,
                CreatedByUserId = paidByUserId,
                CreatedAt = DateTime.Now,
                Notes = $"صافي فاتورة صيانة #{invoice.Id} (العمولة مضافة على فاتورة العميل)."
            });

            invoice.Status = MaintenanceInvoiceStatus.Paid;
            invoice.PaymentTransaction = payment;
            invoice.PaidByUserId = paidByUserId;
            invoice.PaidAt = DateTime.Now;
            invoice.PreviousClientBalance = previousClientBalance;
            invoice.NewClientBalance = invoice.Client.Balance;
            invoice.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _notifications.NotifyMaintenanceInvoicePaidAsync(invoice);

            return new MaintenanceInvoicePaymentResult { Success = true };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to pay maintenance invoice {InvoiceId}", invoiceId);
            return new MaintenanceInvoicePaymentResult
            {
                    ErrorMessage = "تعذر تسديد فاتورة الصيانة حالياً."
            };
        }
    }

    private async Task<int?> ResolveCompanyNetworkIdAsync(int networkId, CancellationToken ct)
    {
        var net = await _db.Networks
            .AsNoTracking()
            .Where(n => n.Id == networkId)
            .Select(n => new { n.Id, n.ParentNetworkId })
            .FirstOrDefaultAsync(ct);
        if (net == null)
        {
            return null;
        }

        return net.ParentNetworkId ?? net.Id;
    }

    private sealed record SystemPricingSnapshot(MaintenanceCommissionMode Mode, decimal Value);

    private async Task<SystemPricingSnapshot?> ResolveSystemPricingAsync(string featureKey, CancellationToken ct)
    {
        var pricing = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p => p.FeatureKey == featureKey && p.IsActive && p.BillingPeriod == PricingBillingPeriod.OneTime)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (pricing == null)
        {
            return null;
        }

        if (pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
        {
            return new SystemPricingSnapshot(MaintenanceCommissionMode.Percent, pricing.AmountSYP);
        }

        return new SystemPricingSnapshot(MaintenanceCommissionMode.Fixed, pricing.AmountSYP);
    }

    private static string BuildFixExplanationWithSelectedItems(
        string fixExplanation,
        IReadOnlyCollection<NetworkMaintenancePrice> selectedFixedPrices)
    {
        var baseText = (fixExplanation ?? string.Empty).Trim();
        if (selectedFixedPrices.Count == 0)
        {
            return baseText;
        }

        var itemsText = string.Join("، ", selectedFixedPrices.Select(p =>
            $"{MaintenanceCatalog.GetDisplayName(p.MaintenanceType)} ({p.AmountSYP:N0} ل.س)"));
        var full = $"{baseText}\n\nالعناصر المنفذة: {itemsText}";
        return full.Length <= 1000 ? full : full[..1000];
    }
}
