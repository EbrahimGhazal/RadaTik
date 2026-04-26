using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services.PricingPolicies;

namespace RadTik.Services;

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

        var pricing = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == FeatureKeys.CollectionCommission &&
                p.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (pricing == null || pricing.AmountSYP <= 0m)
        {
            return new CollectionCommissionChargeResult { Success = true, SkippedNoPricing = true };
        }

        var pricingComputation = _pricingResolver.Resolve(pricing, paymentAmountSyp);
        if (!pricingComputation.IsSupported)
        {
            return new CollectionCommissionChargeResult
            {
                Success = false,
                ErrorMessage = "نوع تسعير عمولة التحصيل غير مدعوم حالياً."
            };
        }

        var percent = pricingComputation.PercentValue;
        var fee = pricingComputation.FeeAmountSyp;
        if (fee <= 0m)
        {
            return new CollectionCommissionChargeResult { Success = true, SkippedNoPricing = true };
        }

        var payment = await _db.PaymentTransactions
            .Include(t => t.Client)
            .FirstOrDefaultAsync(t => t.Id == paymentTransactionId, ct);

        if (payment?.Client == null || !payment.Client.NetworkId.HasValue)
        {
            return new CollectionCommissionChargeResult
            {
                Success = false,
                ErrorMessage = "تعذر ربط عملية الدفع بالشبكة لاحتساب العمولة."
            };
        }

        var net = await _db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == payment.Client.NetworkId.Value, ct);
        if (net == null)
        {
            return new CollectionCommissionChargeResult { Success = false, ErrorMessage = "الشبكة غير موجودة." };
        }

        var companyNetworkId = net.ParentNetworkId ?? net.Id;

        var company = await _db.Networks.FirstOrDefaultAsync(
            n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct);
        if (company == null)
        {
            return new CollectionCommissionChargeResult { Success = false, ErrorMessage = "تعذر تحديد حساب الشركة." };
        }

        if (company.Balance < fee)
        {
            return new CollectionCommissionChargeResult
            {
                Success = false,
                ErrorMessage = $"رصيد محفظة الشركة غير كافٍ لعمولة التحصيل. المطلوب: {fee:N0} ل.س.ج والرصيد: {company.Balance:N0} ل.س.ج."
            };
        }

        var actorId = payment.ReceivedByUserId;
        if (string.IsNullOrWhiteSpace(actorId))
        {
            actorId = await ResolveActorFallbackAsync(companyNetworkId, ct);
        }

        var previousBalance = company.Balance;
        company.Balance -= fee;

        _db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
        {
            NetworkId = companyNetworkId,
            Type = NetworkWalletTransactionType.CollectionCommission,
            SignedAmount = -fee,
            PreviousBalance = previousBalance,
            NewBalance = company.Balance,
            RelatedPaymentTransactionId = paymentTransactionId,
            CreatedByUserId = actorId,
            CreatedAt = DateTime.Now,
            Notes = $"عمولة تحصيل {percent:N2}% من مبلغ {paymentAmountSyp:N2} ل.س.ج (عملية دفع #{paymentTransactionId})"
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Collection commission charged: payment #{PaymentId}, company {CompanyId}, fee {Fee}",
            paymentTransactionId, companyNetworkId, fee);

        return new CollectionCommissionChargeResult { Success = true, FeeChargedSyp = fee };
    }

    private async Task<string> ResolveActorFallbackAsync(int companyNetworkId, CancellationToken ct)
    {
        var company = await _db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        if (company != null && !string.IsNullOrWhiteSpace(company.ManagerUserId))
        {
            return company.ManagerUserId!;
        }

        var sysAdminId = await (from u in _db.Users
            join ur in _db.UserRoles on u.Id equals ur.UserId
            join r in _db.Roles on ur.RoleId equals r.Id
            where r.Name == RoleNames.SystemAdministrator
            select u.Id).FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(sysAdminId)
            ? await _db.Users.Select(u => u.Id).FirstAsync(ct)
            : sysAdminId!;
    }
}
