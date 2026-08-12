namespace RadaTik.ViewModels.UI;

public sealed class PageHeroViewModel
{
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public string IconClass { get; init; } = "fas fa-layer-group";
    public string Theme { get; init; } = "default";
    public IReadOnlyList<PageHeroChipViewModel> Chips { get; init; } = [];
    public IReadOnlyList<PageHeroActionViewModel> Actions { get; init; } = [];
}

public sealed class PageHeroChipViewModel
{
    public string IconClass { get; init; } = "fas fa-circle";
    public string Text { get; init; } = "";
}

public sealed class PageHeroActionViewModel
{
    public string Text { get; init; } = "";
    public string Url { get; init; } = "#";
    public string CssClass { get; init; } = "btn btn-primary";
    public string IconClass { get; init; } = "";
}

public sealed class StatCardViewModel
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
    public string? Subtext { get; init; }
    public string IconClass { get; init; } = "fas fa-chart-line";
    public string Tone { get; init; } = "primary";
    public string? Url { get; init; }
}

public sealed class UnifiedStepperViewModel
{
    public IReadOnlyList<UnifiedStepperStepViewModel> Steps { get; init; } = [];
    public int CurrentStep { get; init; } = 1;
}

public sealed class UnifiedStepperStepViewModel
{
    public int Number { get; init; }
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
}

public sealed class OperationsHubViewModel
{
    public int ExpiredSubscriptions { get; init; }
    public int ExpiringInWeek { get; init; }
    public int PendingRequests { get; init; }
    public int PendingEmployeeApprovals { get; init; }
    public int PendingRenewalRequests { get; init; }
    public int PendingClientTopUps { get; init; }
    public int UnreadNotifications { get; init; }
}

public sealed class SectionCardViewModel
{
    public string Title { get; init; } = "";
    public string IconClass { get; init; } = "fas fa-layer-group";
    public string? ExtraCssClass { get; init; }
    public string BodyCssClass { get; init; } = "py-3";
}

public sealed class MapEmbedViewModel
{
    public string MapElementId { get; init; } = "map";
    public bool ShowMapTypeControls { get; init; } = true;
    public bool ShowLegend { get; init; } = true;
    public string? LegendTitle { get; init; }
    public IReadOnlyList<MapLegendItemViewModel> LegendItems { get; init; } = [];
}

public sealed class MapLegendItemViewModel
{
    public string Color { get; init; } = "#00ff00";
    public string Label { get; init; } = "";
}

public sealed class DataTableToolbarViewModel
{
    public string? Title { get; init; }
    public string? SearchPlaceholder { get; init; }
    public string? SearchInputId { get; init; }
    public IReadOnlyList<PageHeroActionViewModel> Actions { get; init; } = [];
}
