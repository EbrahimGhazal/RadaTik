using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Domain.Warehouse;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class WarehouseStockService(ApplicationDbContext context)
    : ApplicationServiceBase(context), IWarehouseStockService
{
    public decimal ComputeOnHand(IEnumerable<WarehouseMovement> movements) =>
        WarehouseStockCalculator.ComputeOnHand(movements);

    public decimal ComputeOnHand(IEnumerable<WarehouseMovement> movements, DateTime? asOfDate) =>
        WarehouseStockCalculator.ComputeOnHand(movements, asOfDate);

    public Task<Dictionary<int, decimal>> GetOnHandByItemIdAsync(
        int companyNetworkId,
        CancellationToken ct = default) =>
        GetOnHandByItemIdAsync(companyNetworkId, itemIds: null, ct);

    public async Task<Dictionary<int, decimal>> GetOnHandByItemIdAsync(
        int companyNetworkId,
        IReadOnlyCollection<int>? itemIds,
        CancellationToken ct = default)
    {
        IQueryable<WarehouseMovement> query = Db.WarehouseMovements
            .AsNoTracking()
            .Where(m => m.CompanyNetworkId == companyNetworkId);

        if (itemIds is { Count: > 0 })
        {
            query = query.Where(m => itemIds.Contains(m.WarehouseItemId));
        }

        List<WarehouseMovement> movements = await query.ToListAsync(ct);

        return movements
            .GroupBy(m => m.WarehouseItemId)
            .ToDictionary(g => g.Key, g => ComputeOnHand(g));
    }
}
