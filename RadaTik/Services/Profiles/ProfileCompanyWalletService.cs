using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.PricingPolicies;

namespace RadaTik.Services.Profiles;

public sealed class ProfileCompanyWalletService(
    ApplicationDbContext context,
    IProfileImportPricingService profileImportPricing)
    : ApplicationServiceBase(context), IProfileCompanyWalletService
{
    public async Task<decimal> ResolveSystemProfileVatPercentageAsync(CancellationToken ct = default)
    {
        FeaturePricing? taxRow = await Db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == FeatureKeys.ProfilePriceTax &&
                p.ChargeUnit == PricingChargeUnit.Flat &&
                p.BillingPeriod == PricingBillingPeriod.OneTime)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (taxRow == null)
        {
            return 15m;
        }

        decimal tax = taxRow.AmountSYP;
        if (tax < 0m)
        {
            return 0m;
        }

        if (tax > 100m)
        {
            return 100m;
        }

        return tax;
    }

    public async Task<decimal> ChargeCompanyForProfileUnitsAsync(
        int companyNetworkId,
        string actorUserId,
        int unitsCount,
        string note,
        CancellationToken ct = default)
    {
        if (unitsCount <= 0)
        {
            return 0m;
        }

        ProfileImportChargeEstimate charge =
            await profileImportPricing.CalculateProfileChargeAsync(companyNetworkId, unitsCount, ct);
        if (charge.TotalCharge <= 0m)
        {
            return 0m;
        }

        Network? company = await Db.Networks
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct);
        if (company == null)
        {
            return 0m;
        }

        if (company.Balance < charge.TotalCharge)
        {
            throw new InvalidOperationException(
                $"Insufficient company balance. Required={charge.TotalCharge}, Balance={company.Balance}");
        }

        decimal previousBalance = company.Balance;
        company.Balance -= charge.TotalCharge;

        Db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
        {
            NetworkId = companyNetworkId,
            Type = NetworkWalletTransactionType.ServiceCharge,
            SignedAmount = -charge.TotalCharge,
            PreviousBalance = previousBalance,
            NewBalance = company.Balance,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.Now,
            Notes = $"{note} | العدد: {unitsCount} | سعر الوحدة: {charge.UnitPrice:N2} ل.س.ج | الإجمالي: {charge.TotalCharge:N2} ل.س.ج"
        });

        await Db.SaveChangesAsync(ct);
        return charge.TotalCharge;
    }
}
