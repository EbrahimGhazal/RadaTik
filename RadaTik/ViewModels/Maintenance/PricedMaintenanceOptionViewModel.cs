using RadaTik.Models;

namespace RadaTik.ViewModels.Maintenance;

public sealed class PricedMaintenanceOptionViewModel
{
    public MaintenanceType MaintenanceType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal AmountSYP { get; set; }
    public bool IsDefaultForRequestType { get; set; }
}
