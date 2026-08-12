using RadaTik.Models;

namespace RadaTik.Domain.Maintenance;

/// <summary>تصنيف أنواع الصيانة القابلة للتسعير (طرق الحل) — مصدر واحد للمنطق واستعلامات EF.</summary>
public static class MaintenanceSolutionTypes
{
    public static readonly MaintenanceType[] Values =
    [
        MaintenanceType.CableReplacement,
        MaintenanceType.ReceiverReplacement,
        MaintenanceType.PoeChange,
        MaintenanceType.Rg45ConnectorReplacement,
        MaintenanceType.RouterSettingsChange,
        MaintenanceType.RouterReplacement,
        MaintenanceType.SwitchReplacement
    ];

    public static bool Contains(MaintenanceType type) => Values.Contains(type);
}
