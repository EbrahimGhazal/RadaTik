using RadTik.Models;

namespace RadTik.Areas.CompanyAdmin.ViewModels;

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
}
