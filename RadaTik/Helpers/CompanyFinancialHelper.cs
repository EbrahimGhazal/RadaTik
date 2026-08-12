using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Helpers;

public sealed class CompanyFinancialSnapshot
{
    public int CompanyNetworkId { get; init; }
    public decimal? DefaultUsdToSypExchangeRate { get; init; }
}

public static class CompanyFinancialHelper
{
    public static async Task<int> ResolveCompanyNetworkIdAsync(ApplicationDbContext db, int networkId, CancellationToken ct = default)
    {
        Network? net = await db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId, ct);
        if (net == null)
        {
            return networkId;
        }

        return net.ParentNetworkId ?? net.Id;
    }

    public static async Task<CompanyFinancialSnapshot> GetSnapshotAsync(
        ApplicationDbContext db,
        int networkId,
        CancellationToken ct = default)
    {
        int companyId = await ResolveCompanyNetworkIdAsync(db, networkId, ct);
        Network? company = await db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == companyId, ct);

        return new CompanyFinancialSnapshot
        {
            CompanyNetworkId = companyId,
            DefaultUsdToSypExchangeRate = company?.DefaultUsdToSypExchangeRate
        };
    }
}
