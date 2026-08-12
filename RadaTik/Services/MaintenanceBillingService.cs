using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Services;

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
        decimal? transportFeeOverride = null,
        CancellationToken ct = default);

    Task<MaintenanceInvoicePaymentResult> PayInvoiceFromClientWalletAsync(
        int invoiceId,
        string paidByUserId,
        CancellationToken ct = default);
}

public sealed class MaintenanceBillingService : IMaintenanceBillingService
{
    private sealed record NetworkIdParentRow(int Id, int? ParentNetworkId);

    private readonly ApplicationDbContext _db;
    private readonly IRequestNotificationService _notifications;
    private readonly ILogger<MaintenanceBillingService> _logger;

    public MaintenanceBillingService(
        ApplicationDbContext db,
        IRequestNotificationService notifications,
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
        decimal? transportFeeOverride = null,
        CancellationToken ct = default)
    {
        MaintenanceRequest? request = await _db.MaintenanceRequests
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request?.Client == null)
        {
            return new MaintenanceInvoiceIssueResult { ErrorMessage = "طلب الصيانة أو بيانات العميل غير موجودة." };
        }

        int? existingId = await _db.MaintenanceInvoices
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

        int? networkId = request.Client.NetworkId;
        if (!networkId.HasValue)
        {
            return new MaintenanceInvoiceIssueResult { ErrorMessage = "العميل غير مرتبط بشبكة." };
        }

        int? companyNetworkId = await ResolveCompanyNetworkIdAsync(networkId.Value, ct);
        if (!companyNetworkId.HasValue)
        {
            return new MaintenanceInvoiceIssueResult { ErrorMessage = "تعذر تحديد شبكة الشركة." };
        }

        List<MaintenanceType> selectedTypes = (selectedMaintenanceTypes ?? [])
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

        List<NetworkMaintenancePrice> fixedPriceCandidates = await _db.NetworkMaintenancePrices
            .AsNoTracking()
            .Where(p =>
                p.NetworkId == companyNetworkId.Value &&
                selectedTypes.Contains(p.MaintenanceType))
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);

        Dictionary<MaintenanceType, NetworkMaintenancePrice> fixedPricesByType = fixedPriceCandidates
            .GroupBy(p => p.MaintenanceType)
            .ToDictionary(g => g.Key, g => g.First());

        List<MaintenanceType> missingTypes = selectedTypes
            .Where(t => !fixedPricesByType.ContainsKey(t))
            .ToList();
        if (missingTypes.Count > 0)
        {
            return new MaintenanceInvoiceIssueResult
            {
                ErrorMessage = "بعض العناصر المختارة لا تملك سعراً ثابتاً. راجع تسعير الصيانة لدى مدير الشركة."
            };
        }

        List<NetworkMaintenancePrice> selectedFixedPrices = selectedTypes
            .Select(t => fixedPricesByType[t])
            .ToList();

        SystemPricingSnapshot? transport = await ResolveSystemPricingAsync(FeatureKeys.MaintenanceTransportFee, ct);
        if (transport == null)
        {
            return new MaintenanceInvoiceIssueResult
            {
                ErrorMessage = "تعذر تحميل أجور النقل من إعدادات النظام."
            };
        }

        SystemPricingSnapshot? commission = await ResolveSystemPricingAsync(FeatureKeys.MaintenanceCommission, ct);
        if (commission == null)
        {
            return new MaintenanceInvoiceIssueResult
            {
                ErrorMessage = "تعذر تحميل عمولة الصيانة من إعدادات النظام."
            };
        }

        decimal serviceBasePrice = selectedFixedPrices.Sum(p => p.AmountSYP);
        decimal effectiveTransportFee = transportFeeOverride.HasValue
            ? WalletMath.CeilSyp(Math.Max(0m, transportFeeOverride.Value))
            : WalletMath.CeilSyp(transport.Value);
        decimal subtotalBeforeCommission = serviceBasePrice + effectiveTransportFee;
        decimal commissionAmount = commission.Mode == MaintenanceCommissionMode.Percent
            ? WalletMath.CeilSyp(subtotalBeforeCommission * (commission.Value / 100m))
            : WalletMath.CeilSyp(commission.Value);
        if (commissionAmount < 0m)
        {
            commissionAmount = 0m;
        }

        // العميل يدفع (الخدمة + النقل + العمولة)، بينما صافي الشركة يبقى دون العمولة.
        decimal gross = Math.Max(0m, subtotalBeforeCommission + commissionAmount);
        decimal net = Math.Max(0m, gross - commissionAmount);

        MaintenanceInvoice invoice = new MaintenanceInvoice
        {
            MaintenanceRequestId = requestId,
            ClientId = request.ClientId,
            NetworkId = companyNetworkId.Value,
            IssuedByUserId = issuedByUserId,
            FaultExplanation = faultExplanation.Trim(),
            FixExplanation = BuildFixExplanationWithSelectedItems(fixExplanation, selectedFixedPrices),
            ServiceBasePrice = serviceBasePrice,
            TransportFee = effectiveTransportFee,
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
        MaintenanceInvoice? invoice = await _db.MaintenanceInvoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice?.Client == null)
        {
            return new MaintenanceInvoicePaymentResult { ErrorMessage = "الفاتورة غير موجودة." };
        }

        Client client = invoice.Client;

        if (invoice.Status != MaintenanceInvoiceStatus.Pending)
        {
            return new MaintenanceInvoicePaymentResult { ErrorMessage = "لا يمكن تسديد الفاتورة لأن حالتها ليست بانتظار السداد." };
        }

        Network? company = await _db.Networks.FirstOrDefaultAsync(n => n.Id == invoice.NetworkId, ct);
        if (company == null)
        {
            return new MaintenanceInvoicePaymentResult { ErrorMessage = "شبكة الشركة غير موجودة." };
        }

        if (client.Balance < invoice.GrossAmount)
        {
            return new MaintenanceInvoicePaymentResult
            {
                InsufficientBalance = true,
                RequiredAmount = invoice.GrossAmount
            };
        }

        await using IDbContextTransaction tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            decimal previousClientBalance = client.Balance;
            client.Balance -= invoice.GrossAmount;
            client.LastUpdated = DateTime.Now;

            decimal previousCompanyBalance = company.Balance;
            company.Balance += invoice.NetAmountToCompany;

            PaymentTransaction payment = new PaymentTransaction
            {
                ClientId = invoice.ClientId,
                NetworkId = invoice.NetworkId,
                PaymentDate = DateTime.Now,
                ReceivedByUserId = paidByUserId,
                OperationType = "MaintenanceInvoicePayment",
                ReferenceNumber = $"MIN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40],
                Notes = $"تسديد فاتورة صيانة #{invoice.Id}",
                PreviousClientBalance = previousClientBalance,
                NewClientBalance = client.Balance,
                PreviousPointBalance = 0m,
                NewPointBalance = 0m
            };
            PaymentTransactionHelper.ApplySingleCurrencySyp(
                payment,
                invoice.GrossAmount,
                client.AccountCurrency);
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
            invoice.NewClientBalance = client.Balance;
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
        NetworkIdParentRow? net = await _db.Networks
            .AsNoTracking()
            .Where(n => n.Id == networkId)
            .Select(n => new NetworkIdParentRow(n.Id, n.ParentNetworkId))
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
        FeaturePricing? pricing = await _db.FeaturePricings
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
        string baseText = (fixExplanation ?? string.Empty).Trim();
        if (selectedFixedPrices.Count == 0)
        {
            return baseText;
        }

        string itemsText = string.Join("، ", selectedFixedPrices.Select(p =>
            $"{MaintenanceCatalog.GetDisplayName(p.MaintenanceType)} ({p.AmountSYP:N0} ل.س)"));
        string full = $"{baseText}\n\nالعناصر المنفذة: {itemsText}";
        return full.Length <= 1000 ? full : full[..1000];
    }
}
