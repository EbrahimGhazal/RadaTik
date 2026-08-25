using System.Globalization;
using global::RadaTik.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace RadaTik.Areas.CompanyAdmin.ViewModels;

public sealed class MaintenancePricingRowViewModel
{
    public MaintenanceType Type { get; init; }
    public string SolutionName { get; init; } = string.Empty;
    public decimal AmountSyp { get; init; }
    public bool IsActive { get; init; }
}

public sealed class MaintenancePricingPageViewModel
{
    public int NetworkId { get; init; }
    public string NetworkScope { get; init; } = "main";
    public string EffectiveNetworkName { get; init; } = string.Empty;
    public bool CanUseCurrentNetworkScope { get; init; }
    public List<MaintenancePricingRowViewModel> Rows { get; init; } = new();
}

public sealed class MaintenancePricingBulkSaveRowInput
{
    public MaintenanceType Type { get; set; }
    public decimal AmountSyp { get; set; }
    public bool IsActive { get; set; }
}

public sealed class MaintenancePricingBulkSaveInput
{
    public string NetworkScope { get; set; } = "main";
    public List<MaintenancePricingBulkSaveRowInput> Rows { get; set; } = new();

    public static List<MaintenancePricingBulkSaveRowInput> BindRows(IFormCollection form)
    {
        List<MaintenancePricingBulkSaveRowInput> rows = [];
        for (int i = 0; i < 200; i++)
        {
            string typeKey = $"Rows[{i}].Type";
            if (!form.ContainsKey(typeKey))
            {
                break;
            }

            if (!TryParseType(form[typeKey], out MaintenanceType type))
            {
                continue;
            }

            rows.Add(new MaintenancePricingBulkSaveRowInput
            {
                Type = type,
                AmountSyp = ParseAmount(form[$"Rows[{i}].AmountSyp"]),
                IsActive = IsChecked(form[$"Rows[{i}].IsActive"])
            });
        }

        return rows;
    }

    private static bool TryParseType(StringValues posted, out MaintenanceType type)
    {
        foreach (string? value in posted)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            string raw = value.Trim();
            if (Enum.TryParse(raw, ignoreCase: true, out type) && Enum.IsDefined(type))
            {
                return true;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric)
                && Enum.IsDefined(typeof(MaintenanceType), numeric))
            {
                type = (MaintenanceType)numeric;
                return true;
            }
        }

        type = default;
        return false;
    }

    private static decimal ParseAmount(StringValues posted)
    {
        foreach (string? value in posted)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            string raw = value.Trim().Replace(",", "", StringComparison.Ordinal);
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal invariant))
            {
                return invariant;
            }

            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal local))
            {
                return local;
            }
        }

        return 0m;
    }

    private static bool IsChecked(StringValues posted)
    {
        foreach (string? value in posted)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
