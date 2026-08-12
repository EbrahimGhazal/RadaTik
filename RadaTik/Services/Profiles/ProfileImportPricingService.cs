using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.PricingPolicies;

namespace RadaTik.Services.Profiles;

public sealed class ProfileImportPricingService(ApplicationDbContext context)
    : ApplicationServiceBase(context), IProfileImportPricingService
{
    public async Task<decimal> GetProfileImportUnitPriceAsync(CancellationToken ct = default) =>
        await Db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == FeatureKeys.Profiles &&
                p.ChargeUnit == PricingChargeUnit.PerSpeedProfile &&
                p.BillingPeriod == PricingBillingPeriod.OneTime)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.Id)
            .Select(p => p.AmountSYP)
            .FirstOrDefaultAsync(ct);

    public async Task<decimal> GetCompanyWalletBalanceAsync(int companyNetworkId, CancellationToken ct = default) =>
        await Db.Networks
            .AsNoTracking()
            .Where(n => n.Id == companyNetworkId && n.ParentNetworkId == null)
            .Select(n => n.Balance)
            .FirstOrDefaultAsync(ct);

    public async Task<ProfileImportChargeEstimate> CalculateProfileChargeAsync(
        int companyNetworkId,
        int unitsCount,
        CancellationToken ct = default)
    {
        decimal walletBalance = await GetCompanyWalletBalanceAsync(companyNetworkId, ct);
        if (unitsCount <= 0)
        {
            return new ProfileImportChargeEstimate
            {
                UnitPrice = 0m,
                TotalCharge = 0m,
                WalletBalance = walletBalance,
                HasSufficientBalance = true
            };
        }

        decimal unitPrice = WalletMath.CeilSyp(await GetProfileImportUnitPriceAsync(ct));
        decimal totalCharge = WalletMath.CeilSyp(unitPrice * unitsCount);
        return new ProfileImportChargeEstimate
        {
            UnitPrice = unitPrice,
            TotalCharge = totalCharge,
            WalletBalance = walletBalance,
            HasSufficientBalance = walletBalance >= totalCharge
        };
    }
}
