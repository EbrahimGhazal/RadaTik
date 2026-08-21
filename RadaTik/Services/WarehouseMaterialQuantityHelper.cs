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

    /// <summary>إجمالي فاتورة الشراء = مجموع (العدد × سعر وحدة الشراء). False إذا لم يمكن الاحتساب.</summary>
    public static bool TryComputePurchaseTotal(IReadOnlyList<MaterialInvoiceLineInput>? lines, out decimal total)
    {
        total = 0m;
        if (lines == null || lines.Count == 0)
        {
            return false;
        }

        foreach (MaterialInvoiceLineInput input in lines)
        {
            if (input.PackageQuantity <= 0m || input.UnitPrice < 0m)
            {
                total = 0m;
                return false;
            }

            decimal lineTotal = input.PackageQuantity * input.UnitPrice;
            if (lineTotal <= 0m)
            {
                total = 0m;
                return false;
            }

            total += lineTotal;
        }

        return total > 0m;
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
