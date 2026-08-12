using System.Globalization;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public static class WarehouseMaterialQuantityHelper
{
    public static string NormalizeKeyPart(string? value) =>
      string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToUpperInvariant();

    public static string BuildItemMatchKey(string name, string? modelNumber) =>
      $"{NormalizeKeyPart(name)}|{NormalizeKeyPart(modelNumber)}";

    public static int NormalizeUnitsPerPackage(MaterialPackageUnit unit, int unitsPerPackage)
    {
        if (unit == MaterialPackageUnit.Piece)
        {
            return 1;
        }

        return unitsPerPackage > 0 ? unitsPerPackage : 0;
    }

    public static decimal ComputeBaseQuantity(MaterialPackageUnit unit, decimal packageQuantity, int unitsPerPackage)
    {
        if (packageQuantity <= 0m)
        {
            return 0m;
        }

        int perPackage = NormalizeUnitsPerPackage(unit, unitsPerPackage);
        if (unit != MaterialPackageUnit.Piece && perPackage <= 0)
        {
            return 0m;
        }

        return unit == MaterialPackageUnit.Piece
          ? packageQuantity
          : packageQuantity * perPackage;
    }

    public static string DisplayPackageUnit(MaterialPackageUnit unit) => unit switch
    {
        MaterialPackageUnit.Box => "علبة",
        MaterialPackageUnit.Carton => "كرتونة",
        MaterialPackageUnit.Bundle => "ربطة",
        _ => "قطعة"
    };

    /// <summary>عرض الكميات والأعداد دون خانات عشرية زائدة (بدون N3).</summary>
    public static string FormatQuantity(decimal value)
    {
        if (value == decimal.Truncate(value))
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);
        }

        return value.ToString("0.###", CultureInfo.CurrentCulture);
    }
}
