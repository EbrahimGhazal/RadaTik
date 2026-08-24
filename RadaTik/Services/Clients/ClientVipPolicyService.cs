using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services.Clients;

public sealed class ClientVipPolicyService(ApplicationDbContext db) : IClientVipPolicyService
{
    public async Task<CompanyVipPolicy> GetCompanyPolicyAsync(int? networkId, CancellationToken ct = default)
    {
        if (!networkId.HasValue || networkId.Value <= 0)
        {
            return CompanyVipPolicy.None;
        }

        int companyId = await CompanyFinancialHelper.ResolveCompanyNetworkIdAsync(db, networkId.Value, ct);
        Network? company = await db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == companyId, ct);
        if (company == null)
        {
            return CompanyVipPolicy.None;
        }

        return new CompanyVipPolicy(
            company.VipDiscountPercent,
            company.VipGraceDays,
            company.VipSkipAutoDisable);
    }

    public async Task<decimal> ApplyPackageDiscountAsync(decimal basePrice, Client client, CancellationToken ct = default)
    {
        CompanyVipPolicy policy = await GetCompanyPolicyAsync(client.NetworkId, ct);
        return ClientVipPricing.ApplyPackageDiscount(basePrice, client.IsVip, policy);
    }

    public async Task<(decimal BasePrice, decimal VatAmount, decimal Total)> ApplyMonthlyPriceAsync(
        Client client,
        CancellationToken ct = default)
    {
        CompanyVipPolicy policy = await GetCompanyPolicyAsync(client.NetworkId, ct);
        decimal profilePrice = client.Profile?.Price ?? 0m;
        decimal vatPercent = client.Profile?.VATPercentage ?? 0m;
        return ClientVipPricing.ApplyMonthlyPrice(profilePrice, vatPercent, client.IsVip, policy);
    }

    public async Task<bool> IsProtectedFromAutoDisableAsync(Client client, DateTime now, CancellationToken ct = default)
    {
        if (!client.IsVip)
        {
            return false;
        }

        CompanyVipPolicy policy = await GetCompanyPolicyAsync(client.NetworkId, ct);
        return ClientVipPricing.IsProtectedFromAutoDisable(
            client.IsVip,
            client.AccountExpirationDate,
            policy,
            now);
    }
}
