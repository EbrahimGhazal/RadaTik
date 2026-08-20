using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Helpers;

public static class PricingChargeHelper
{
    public static async Task<List<int>> GetCompanyScopeNetworkIdsAsync(ApplicationDbContext db, int companyNetworkId)
    {
        // الشركة الرئيسية + الشبكات الفرعية
        return await db.Networks
            .AsNoTracking()
            .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
            .Select(n => n.Id)
            .ToListAsync();
    }

    /// <summary>
    /// نطاق الشركة للشبكة المحددة: الشبكة الرئيسية وجميع الشبكات الفرعية التابعة لها.
    /// </summary>
    public static async Task<List<int>> GetCompanyScopeNetworkIdsForSelectedAsync(
        ApplicationDbContext db,
        int selectedNetworkId,
        CancellationToken ct = default)
    {
        var selected = await db.Networks
            .AsNoTracking()
            .Where(n => n.Id == selectedNetworkId)
            .Select(n => new { n.Id, n.ParentNetworkId })
            .FirstOrDefaultAsync(ct);
        if (selected == null)
        {
            return [selectedNetworkId];
        }

        int companyNetworkId = selected.ParentNetworkId ?? selected.Id;
        List<int> ids = await GetCompanyScopeNetworkIdsAsync(db, companyNetworkId);
        if (ids.Count == 0)
        {
            return [companyNetworkId];
        }

        ids.Remove(companyNetworkId);
        ids.Insert(0, companyNetworkId);
        return ids;
    }

    public static async Task<int> GetMultiplierAsync(
        ApplicationDbContext db,
        IReadOnlyList<int> networkIds,
        PricingChargeUnit unit,
        DateTime? windowStart = null,
        DateTime? windowEnd = null)
    {
        switch (unit)
        {
            case PricingChargeUnit.Flat:
                return 1;

            case PricingChargeUnit.PerUser:
            {
                var usersCount = await db.Users
                    .AsNoTracking()
                    .CountAsync(u => u.IsActive && u.NetworkId.HasValue && networkIds.Contains(u.NetworkId.Value));
                return Math.Max(1, usersCount);
            }

            case PricingChargeUnit.PerNetwork:
            {
                var networksCount = await db.Networks
                    .AsNoTracking()
                    .CountAsync(n => networkIds.Contains(n.Id));

                // القاعدة التجارية: أول شبكة مجانية دائماً.
                return Math.Max(0, networksCount - 1);
            }

            case PricingChargeUnit.PerSubscriber:
                return await db.Clients.AsNoTracking()
                    .CountAsync(c => c.IsActive && c.NetworkId.HasValue && networkIds.Contains(c.NetworkId.Value));

            case PricingChargeUnit.PerSector:
                return await db.Sectors.AsNoTracking()
                    .CountAsync(s => s.IsActive && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value));

            case PricingChargeUnit.PerReceiver:
                return await db.Receivers.AsNoTracking()
                    .CountAsync(r => r.IsActive && r.NetworkId.HasValue && networkIds.Contains(r.NetworkId.Value));

            case PricingChargeUnit.PerServer:
                return await db.MikroTikServers.AsNoTracking()
                    .CountAsync(s => s.IsActive && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value));

            case PricingChargeUnit.PerCollectionPoint:
                return await db.CollectionPointAccounts.AsNoTracking()
                    .CountAsync(a => a.NetworkId.HasValue && networkIds.Contains(a.NetworkId.Value));

            case PricingChargeUnit.PerSpeedProfile:
                return await db.Profiles.AsNoTracking()
                    .CountAsync(p => p.IsActive && p.NetworkId.HasValue && networkIds.Contains(p.NetworkId.Value));

            case PricingChargeUnit.PerRequest:
            {
                if (!windowStart.HasValue || !windowEnd.HasValue)
                {
                    return 0;
                }

                var start = windowStart.Value;
                var end = windowEnd.Value;

                // طلبات صيانة
                var maintenanceCount = await db.MaintenanceRequests
                    .AsNoTracking()
                    .CountAsync(r =>
                        r.RequestDate >= start && r.RequestDate < end &&
                        r.Client != null &&
                        r.Client.NetworkId.HasValue &&
                        networkIds.Contains(r.Client.NetworkId.Value));

                // طلبات تغيير سرعة
                var speedChangeCount = await db.SpeedChangeRequests
                    .AsNoTracking()
                    .CountAsync(r =>
                        r.RequestDate >= start && r.RequestDate < end &&
                        r.Client != null &&
                        r.Client.NetworkId.HasValue &&
                        networkIds.Contains(r.Client.NetworkId.Value));

                return maintenanceCount + speedChangeCount;
            }

            case PricingChargeUnit.PercentOfCollectedAmount:
                return 0;

            case PricingChargeUnit.PerReport:
                // يتم خصم تقارير التصدير عند الحدث نفسه عبر TryChargeReportExportAsync.
                return 0;

            default:
                return 1;
        }
    }
}

