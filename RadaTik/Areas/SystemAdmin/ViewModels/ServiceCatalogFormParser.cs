using System.Globalization;
using Microsoft.Extensions.Primitives;
using global::RadaTik.Models;

namespace RadaTik.Areas.SystemAdmin.ViewModels;

public static class ServiceCatalogFormParser
{
    public static ServiceCatalogSaveViewModel Parse(IFormCollection form)
    {
        return new ServiceCatalogSaveViewModel
        {
            Network = ParseRecurring(form, "Network"),
            Server = ParseRecurring(form, "Server"),
            Sector = ParseRecurring(form, "Sector"),
            Receiver = ParseRecurring(form, "Receiver"),
            Client = ParseRecurring(form, "Client"),
            User = ParseRecurring(form, "User"),
            SpeedProfile = ParseRecurring(form, "SpeedProfile"),
            ReportInitialPriceSyp = ParseDecimal(form, "ReportInitialPriceSyp"),
            MaintenanceCommissionMode = ParseEnum(form, "MaintenanceCommissionMode", MaintenanceCommissionMode.Fixed),
            MaintenanceCommissionValue = ParseDecimal(form, "MaintenanceCommissionValue"),
            ProfileTaxPercentage = ParseDecimal(form, "ProfileTaxPercentage")
        };
    }

    private static RecurringPricingFormSection ParseRecurring(IFormCollection form, string prefix)
    {
        return new RecurringPricingFormSection
        {
            InitialPriceSyp = ParseDecimal(form, $"{prefix}.InitialPriceSyp"),
            RenewalBillingPeriod = ParseEnum(form, $"{prefix}.RenewalBillingPeriod", PricingBillingPeriod.Monthly),
            RenewalPricePerUnitSyp = ParseDecimal(form, $"{prefix}.RenewalPricePerUnitSyp"),
            FreeInitialUnits = ParseInt(form, $"{prefix}.FreeInitialUnits"),
            FreeRenewalUnits = ParseInt(form, $"{prefix}.FreeRenewalUnits")
        };
    }

    private static decimal ParseDecimal(IFormCollection form, string key)
    {
        if (!TryGetValue(form, key, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return 0m;
        }

        raw = raw.Trim().Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : 0m;
    }

    private static int ParseInt(IFormCollection form, string key)
    {
        if (!TryGetValue(form, key, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;
    }

    private static TEnum ParseEnum<TEnum>(IFormCollection form, string key, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (!TryGetValue(form, key, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        raw = raw.Trim();
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric) &&
            Enum.IsDefined(typeof(TEnum), numeric))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
        }

        return Enum.TryParse(raw, ignoreCase: true, out TEnum parsed) ? parsed : fallback;
    }

    private static bool TryGetValue(IFormCollection form, string key, out string? value)
    {
        if (form.TryGetValue(key, out StringValues values) && values.Count > 0)
        {
            value = values[0];
            return true;
        }

        value = null;
        return false;
    }
}
