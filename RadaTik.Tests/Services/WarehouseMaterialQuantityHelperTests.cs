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
}
