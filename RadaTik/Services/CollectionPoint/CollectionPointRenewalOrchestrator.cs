using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Domain.CollectionPoint;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;

namespace RadaTik.Services.CollectionPoint;

public sealed class CollectionPointRenewalOrchestrator(
    ApplicationDbContext context,
    ICollectionPaymentService collectionPaymentService,
    ICollectionCommissionChargeService collectionCommissionChargeService,
    IClientRenewalGuardService clientRenewalGuardService,
    ICurrencyHelper currencyHelper,
    ICompanyFinancialHelper companyFinancial,
    IClientVipPolicyService vipPolicy)
    : ApplicationServiceBase(context), ICollectionPointRenewalOrchestrator
{
    private readonly ICollectionPaymentService _collectionPayment = collectionPaymentService;
    private readonly ICollectionCommissionChargeService _commission = collectionCommissionChargeService;
    private readonly IClientRenewalGuardService _renewalGuard = clientRenewalGuardService;
    private readonly ICurrencyHelper _currency = currencyHelper;
    private readonly ICompanyFinancialHelper _companyFinancial = companyFinancial;

    public Task<CollectionPointOperationOutcome> PayBillAsync(PayBillCommand command, CancellationToken ct = default) =>
        ExecuteRenewalAsync(
            command.ClientId,
            command.UserId,
            networkId: null,
            notes: null,
            useClientNetworkForFinancial: true,
            redirectAction: "Index",
            redirectRouteValues: null,
            successPrefix: "تم تسديد",
            ct);

    public Task<CollectionPointOperationOutcome> PayAndRenewAsync(PayAndRenewCommand command, CancellationToken ct = default) =>
        ExecuteRenewalAsync(
            command.ClientId,
            command.UserId,
            command.NetworkId,
            command.Notes,
            useClientNetworkForFinancial: false,
            redirectAction: "ClientDetails",
            redirectRouteValues: new { id = command.ClientId },
            successPrefix: "تم التجديد المباشر بنجاح",
            ct,
            months => command.Notes ?? (months > 1 ? $"تجديد مباشر عن {months} أشهر متأخرة" : "تجديد مباشر"));

    private async Task<CollectionPointOperationOutcome> ExecuteRenewalAsync(
        int clientId,
        string userId,
        int? networkId,
        string? notes,
        bool useClientNetworkForFinancial,
        string redirectAction,
        object? redirectRouteValues,
        string successPrefix,
        CancellationToken ct,
        Func<int, string>? notesFactory = null)
    {
        Client? client = await Db.Clients
            .Include(c => c.Profile)
            .Include(c => c.Network)
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);

        if (client == null)
        {
            return CollectionPointOperationOutcome.Fail("لم يتم العثور على المشترك.", redirectAction, redirectRouteValues);
        }

        if (networkId.HasValue && client.NetworkId != networkId.Value)
        {
            return CollectionPointOperationOutcome.NotFoundClient();
        }

        CollectionPointAccount? account = await Db.CollectionPointAccounts
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (account == null)
        {
            return CollectionPointOperationOutcome.Fail(
                "تعذر العثور على حساب نقطة التحصيل. يرجى التواصل مع مدير النظام.",
                redirectAction,
                redirectRouteValues);
        }

        RenewalPricing? pricing = await TryBuildRenewalPricingAsync(client, useClientNetworkForFinancial, ct);
        if (pricing == null)
        {
            return CollectionPointOperationOutcome.Fail(
                "لا يوجد سعر محدد للباقة.",
                redirectAction,
                redirectRouteValues);
        }

        RenewalBlockResult renewalGuard = await _renewalGuard.CheckBlockingInvoicesAsync(client.Id, ct);
        if (!renewalGuard.CanRenew)
        {
            return CollectionPointOperationOutcome.Fail(
                $"لا يمكن تنفيذ التجديد حالياً قبل تسديد جميع فواتير الصيانة المستحقة على المشترك (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).",
                redirectAction,
                redirectRouteValues);
        }

        if (!pricing.Quote.Success)
        {
            return CollectionPointOperationOutcome.Fail(
                pricing.Quote.ErrorMessage ?? "تعذر احتساب التجديد.",
                redirectAction,
                redirectRouteValues);
        }

        if (account.Balance < pricing.Quote.PointChargeSyp)
        {
            decimal totalBase = pricing.BasePricePerMonth * pricing.PendingMonths;
            decimal totalVat = pricing.VatPerMonth * pricing.PendingMonths;
            string dueLabel = _currency.FormatAmount(pricing.Quote.AmountDueAccount, client.AccountCurrency);
            return CollectionPointOperationOutcome.Fail(
                $"رصيد نقطة التحصيل غير كافٍ. المستحق {dueLabel} (خصم من النقطة: {SyrianCurrencyHelper.FormatNew(pricing.Quote.PointChargeSyp)} ل.س.ج — الأساسي: {totalBase:N0} + ضريبة {pricing.VatPercentage:N2}%: {totalVat:N0}) والرصيد: {SyrianCurrencyHelper.FormatNew(account.Balance)} ل.س.ج",
                redirectAction,
                redirectRouteValues);
        }

        await using IDbContextTransaction tx = await Db.Database.BeginTransactionAsync(ct);
        try
        {
            decimal prevPointBalance = account.Balance;
            account.Balance -= pricing.Quote.PointChargeSyp;
            account.UpdatedAt = DateTime.Now;

            string referenceNumber = BuildReferenceNumber("REN");
            string paymentNotes = notesFactory?.Invoke(pricing.PendingMonths)
                ?? notes
                ?? (pricing.PendingMonths > 1
                    ? $"تسديد متأخر {pricing.PendingMonths} أشهر وتجديد مباشر"
                    : "تسديد فاتورة وتجديد مباشر");

            PaymentTransaction payment = new()
            {
                ClientId = client.Id,
                NetworkId = client.NetworkId,
                PaymentDate = DateTime.Now,
                ReceivedByUserId = userId,
                OperationType = "Renewal",
                ReferenceNumber = referenceNumber,
                Notes = paymentNotes,
                PreviousClientBalance = client.Balance,
                NewClientBalance = client.Balance,
                PreviousPointBalance = prevPointBalance,
                NewPointBalance = account.Balance
            };
            _collectionPayment.FillRenewalPaymentTransaction(payment, pricing.Quote);
            Db.PaymentTransactions.Add(payment);

            DateTime baseDate = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value > DateTime.Now
                ? client.AccountExpirationDate.Value
                : client.AccountExpirationDate ?? DateTime.Now;
            client.AccountExpirationDate = baseDate.AddMonths(pricing.PendingMonths);
            client.LastRenewalDate = DateTime.Now;
            client.LastUpdated = DateTime.Now;

            await Db.SaveChangesAsync(ct);

            CollectionCommissionChargeResult commission = await _commission.ChargeAfterPaymentRecordedAsync(
                payment.Id,
                payment.CollectionAmountSyp,
                ct);
            if (!commission.Success)
            {
                await tx.RollbackAsync(ct);
                return CollectionPointOperationOutcome.Fail(
                    commission.ErrorMessage ?? "تعذر إتمام عمولة التحصيل (محفظة الشركة).",
                    redirectAction,
                    redirectRouteValues);
            }

            await tx.CommitAsync(ct);

            decimal successBase = pricing.BasePricePerMonth * pricing.PendingMonths;
            decimal successVat = pricing.VatPerMonth * pricing.PendingMonths;
            string paidLabel = _currency.RequiresExchangeAtCollection(client.AccountCurrency)
                ? $"{_currency.FormatAmount(pricing.Quote.AmountDueAccount, client.AccountCurrency)} (خصم {SyrianCurrencyHelper.FormatNew(pricing.Quote.PointChargeSyp)} ل.س.ج من النقطة)"
                : $"{SyrianCurrencyHelper.FormatNew(pricing.Quote.PointChargeSyp)} ل.س.ج";

            string successMessage = successPrefix.Contains("المباشر", StringComparison.Ordinal)
                ? $"{successPrefix} ({pricing.PendingMonths} شهر/أشهر) بمبلغ {paidLabel} (الأساسي: {successBase:N0} + الضريبة {pricing.VatPercentage:N2}%: {successVat:N0}). المرجع: {referenceNumber}"
                : $"{successPrefix} {pricing.PendingMonths} شهر/أشهر بمبلغ {paidLabel} (الأساسي: {successBase:N0} + الضريبة {pricing.VatPercentage:N2}%: {successVat:N0}) وتجديد اشتراك {client.UserName} مباشرة حتى {client.AccountExpirationDate:yyyy/MM/dd}. المرجع: {referenceNumber}";

            return CollectionPointOperationOutcome.Success(successMessage, redirectAction, redirectRouteValues);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<RenewalPricing?> TryBuildRenewalPricingAsync(
        Client client,
        bool useClientNetworkForFinancial,
        CancellationToken ct)
    {
        (decimal basePricePerMonth, decimal vatPerMonth, decimal amountPerMonth) =
            await vipPolicy.ApplyMonthlyPriceAsync(client, ct);
        decimal vatPercentage = client.Profile?.VATPercentage ?? 0m;
        if (amountPerMonth <= 0m)
        {
            return null;
        }

        int pendingMonths = SubscriptionArrearsCalculator.CalculatePendingMonths(
            client.AccountExpirationDate,
            DateTime.Now);
        decimal amountDueAccount = amountPerMonth * pendingMonths;

        int financialNetworkId = useClientNetworkForFinancial
            ? client.NetworkId ?? client.Id
            : client.NetworkId ?? throw new InvalidOperationException("شبكة المشترك غير محددة.");

        CompanyFinancialSnapshot financial = await _companyFinancial.GetSnapshotAsync(financialNetworkId, ct);
        CollectionRenewalQuote quote = _collectionPayment.QuoteAccountCharge(
            client.AccountCurrency,
            amountDueAccount,
            financial.DefaultUsdToSypExchangeRate);

        return new RenewalPricing(
            pendingMonths,
            basePricePerMonth,
            vatPercentage,
            vatPerMonth,
            amountPerMonth,
            quote);
    }

    private static string BuildReferenceNumber(string operationType) =>
        $"{operationType}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];

    private sealed record RenewalPricing(
        int PendingMonths,
        decimal BasePricePerMonth,
        decimal VatPercentage,
        decimal VatPerMonth,
        decimal AmountPerMonth,
        CollectionRenewalQuote Quote);
}
