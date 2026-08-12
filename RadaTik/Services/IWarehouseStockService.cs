using RadaTik.Models.Business;

namespace RadaTik.Services;

public interface IWarehouseStockService
{
    decimal ComputeOnHand(IEnumerable<WarehouseMovement> movements);

    decimal ComputeOnHand(IEnumerable<WarehouseMovement> movements, DateTime? asOfDate);

    Task<Dictionary<int, decimal>> GetOnHandByItemIdAsync(
        int companyNetworkId,
        CancellationToken ct = default);

    Task<Dictionary<int, decimal>> GetOnHandByItemIdAsync(
        int companyNetworkId,
        IReadOnlyCollection<int>? itemIds,
        CancellationToken ct = default);
}
