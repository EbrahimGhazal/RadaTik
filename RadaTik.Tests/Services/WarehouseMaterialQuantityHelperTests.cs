using RadaTik.Models.Business;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public class WarehouseMaterialQuantityHelperTests
{
  [Theory]
  [InlineData(MaterialPackageUnit.Piece, 5, 1, 5)]
  [InlineData(MaterialPackageUnit.Carton, 2, 12, 24)]
  [InlineData(MaterialPackageUnit.Box, 3, 0, 0)]
  public void ComputeBaseQuantity_ConvertsPackages(MaterialPackageUnit unit, decimal qty, int perPackage, decimal expected)
  {
    decimal result = WarehouseMaterialQuantityHelper.ComputeBaseQuantity(unit, qty, perPackage);
    Assert.Equal(expected, result);
  }

  [Fact]
  public void BuildItemMatchKey_IsCaseInsensitive()
  {
    string a = WarehouseMaterialQuantityHelper.BuildItemMatchKey("Router", "X1");
    string b = WarehouseMaterialQuantityHelper.BuildItemMatchKey(" router ", "x1");
    Assert.Equal(a, b);
  }

  [Fact]
  public void TryComputePurchaseTotal_SumsLineTotals()
  {
    MaterialInvoiceLineInput[] lines =
    [
      new() { PackageQuantity = 2m, UnitPrice = 10m },
      new() { PackageQuantity = 3m, UnitPrice = 5.5m }
    ];

    bool ok = WarehouseMaterialQuantityHelper.TryComputePurchaseTotal(lines, out decimal total);

    Assert.True(ok);
    Assert.Equal(36.5m, total);
  }

  [Fact]
  public void TryComputePurchaseTotal_RejectsEmptyOrInvalidLines()
  {
    Assert.False(WarehouseMaterialQuantityHelper.TryComputePurchaseTotal(null, out _));
    Assert.False(WarehouseMaterialQuantityHelper.TryComputePurchaseTotal([], out _));
    Assert.False(WarehouseMaterialQuantityHelper.TryComputePurchaseTotal(
      [new MaterialInvoiceLineInput { PackageQuantity = 0m, UnitPrice = 10m }], out _));
  }
}
