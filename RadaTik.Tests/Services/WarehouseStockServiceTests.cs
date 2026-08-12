using RadaTik.Domain.Warehouse;
using RadaTik.Models.Business;
using Xunit;

namespace RadaTik.Tests.Services;

public class WarehouseStockServiceTests
{
  [Fact]
  public void ComputeOnHand_SumsInOutAndAdjustment()
  {
    List<WarehouseMovement> movements =
    [
      new() { MovementType = WarehouseMovementType.In, Quantity = 10m },
      new() { MovementType = WarehouseMovementType.Out, Quantity = 3m },
      new() { MovementType = WarehouseMovementType.Adjustment, Quantity = -1m }
    ];

    decimal onHand = WarehouseStockCalculator.ComputeOnHand(movements);
    Assert.Equal(6m, onHand);
  }
}
