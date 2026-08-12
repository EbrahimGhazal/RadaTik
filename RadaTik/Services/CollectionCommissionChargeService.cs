using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.PricingPolicies;

namespace RadaTik.Services;

public sealed class CollectionCommissionChargeService : ICollectionCommissionChargeService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CollectionCommissionChargeService> _logger;
    private readonly ICollectionCommissionPricingResolver _pricingResolver;

    public CollectionCommissionChargeService(
        ApplicationDbContext db,
        ILogger<CollectionCommissionChargeService> logger,
        ICollectionCommissionPricingResolver pricingResolver)
    {
        _db = db;
        _logger = logger;
        _pricingResolver = pricingResolver;
    }

    public async Task<CollectionCommissionChargeResult> ChargeAfterPaymentRecordedAsync(
        int paymentTransactionId,
        decimal paymentAmountSyp,
        CancellationToken ct = default)
    {
        if (paymentAmountSyp <= 0m)
        {
            return new CollectionCommissionChargeResult { Success = true, SkippedNoPricing = true };
        }

        PaymentTransaction? payment = await _db.PaymentTransactions
            .Include(t => t.Client)
            .FirstOrDefaultAsync(t => t.Id == paymentTransactionId, ct);

        if (payment?.Client == null || !payment.Client.NetworkId.HasValue)
        {
            return new CollectionCommissionChargeResult
            {
                Success = false,
                ErrorMessage = "تعذر ربط عملية الدفع بالشبكة لتسوية محفظة الشركة."
            };
        }

        Network? net = await _db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == payment.Client.NetworkId.Value, ct);
        if (net == null)
        {
            return new CollectionCommissionChargeResult { Success = false, ErrorMessage = "الشبكة غير موجودة." };
        }

        int companyNetworkId = net.ParentNetworkId ?? net.Id;

        Network? company = await _db.Networks.FirstOrDefaultAsync(
            n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct);
        if (company == null)
        {
            return new CollectionCommissionChargeResult { Success = false, ErrorMessage = "تعذر تحديد حساب الشركة (الشبكة الأم)." };
        }

        FeaturePricing? pricing = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == FeatureKeys.CollectionCommission &&
                p.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        PricingCurrency walletCurrency = CurrencyHelper.IsSyrian(payment.PaymentCurrency)
            ? PricingCurrency.SYP_New
            : payment.AccountCurrency;

        decimal gross = walletCurrency == PricingCurrency.SYP_New
            ? payment.CollectionAmountSyp > 0m ? payment.CollectionAmountSyp : paymentAmountSyp
            : payment.AccountAmount;

        decimal fee = 0m;
        decimal percentForNote = 0m;

        if (pricing != null && pricing.AmountSYP > 0m)
        {
            CollectionCommissionPricingComputation computation = _pricingResolver.Resolve(pricing, gross);
            if (!computation.IsSupported)
            {
                return new CollectionCommissionChargeResult
                {
                    Success = false,
                    ErrorMessage = "نوع تسعير عمولة التحصيل غير مدعوم حالياً."
                };
            }

            percentForNote = computation.PercentValue;
            fee = computation.FeeAmountSyp;
            if (fee <= 0m)
            {
                fee = 0m;
            }
        }

        if (fee > gross)
        {
            return new CollectionCommissionChargeResult
            {
                Success = false,
                ErrorMessage =
                    $"عمولة التحصيل المحسوبة ({CurrencyHelper.FormatAmount(fee, walletCurrency)}) تتجاوز مبلغ الدفع ({CurrencyHelper.FormatAmount(gross, walletCurrency)}). راجع تسعير خدمة عمولة التحصيل."
            };
        }

        string actorId = payment.ReceivedByUserId;
        if (string.IsNullOrWhiteSpace(actorId))
        {
            actorId = await ResolveActorFallbackAsync(companyNetworkId, ct);
        }

        string currLabel = CurrencyHelper.GetSymbol(walletCurrency);
        decimal balanceBeforeGross = CompanyWalletHelper.GetBalance(company, walletCurrency);
        CompanyWalletHelper.ApplyDelta(company, walletCurrency, gross);
        decimal balanceAfterGross = CompanyWalletHelper.GetBalance(company, walletCurrency);

        _db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
        {
            NetworkId = companyNetworkId,
            Type = NetworkWalletTransactionType.SubscriptionCollectedRevenue,
            Currency = walletCurrency,
            SignedAmount = gross,
            PreviousBalance = balanceBeforeGross,
            NewBalance = balanceAfterGross,
            RelatedPaymentTransactionId = paymentTransactionId,
            CreatedByUserId = actorId,
            CreatedAt = DateTime.Now,
            Notes = fee > 0m
                ? $"إيراد تحصيل إجمالي {gross:N2} {currLabel} (سيتم خصم عمولة {fee:N2} {currLabel}) — عملية دفع #{paymentTransactionId}"
                : $"إيراد تحصيل {gross:N2} {currLabel} — عملية دفع #{paymentTransactionId}"
        });

        if (fee > 0m)
        {
            decimal balanceBeforeFee = CompanyWalletHelper.GetBalance(company, walletCurrency);
            CompanyWalletHelper.ApplyDelta(company, walletCurrency, -fee);
            decimal balanceAfterFee = CompanyWalletHelper.GetBalance(company, walletCurrency);

            _db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
            {
                NetworkId = companyNetworkId,
                Type = NetworkWalletTransactionType.CollectionCommission,
                Currency = walletCurrency,
                SignedAmount = -fee,
                PreviousBalance = balanceBeforeFee,
                NewBalance = balanceAfterFee,
                RelatedPaymentTransactionId = paymentTransactionId,
                CreatedByUserId = actorId,
                CreatedAt = DateTime.Now,
                Notes =
                    $"عمولة تحصيل {percentForNote:N2}% من {gross:N2} {currLabel} (عملية دفع #{paymentTransactionId})"
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Company wallet settled for payment #{PaymentId}, company {CompanyId}, gross {Gross}, fee {Fee}, net {Net}",
            paymentTransactionId, companyNetworkId, gross, fee, gross - fee);

        return new CollectionCommissionChargeResult
        {
            Success = true,
            FeeChargedSyp = fee,
            SkippedNoPricing = pricing == null || pricing.AmountSYP <= 0m || fee <= 0m
        };
    }

    private async Task<string> ResolveActorFallbackAsync(int companyNetworkId, CancellationToken ct)
    {
        Network? companyRow = await _db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        if (companyRow != null && !string.IsNullOrWhiteSpace(companyRow.ManagerUserId))
        {
            return companyRow.ManagerUserId!;
        }

        string? sysAdminId = await (from u in _db.Users
                                    join ur in _db.UserRoles on u.Id equals ur.UserId
                                    join r in _db.Roles on ur.RoleId equals r.Id
                                    where r.Name == RoleNames.SystemAdministrator
                                    select u.Id).FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(sysAdminId)
            ? await _db.Users.Select(u => u.Id).FirstAsync(ct)
            : sysAdminId!;
    }
}
