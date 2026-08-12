namespace RadaTik.ViewModels.Onboarding;

public sealed class OnboardingChecklistItem
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string ActionUrl { get; init; }
    public required string ActionLabel { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsRequired { get; init; }
}

public sealed class OnboardingChecklistViewModel
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public required IReadOnlyList<OnboardingChecklistItem> Items { get; init; }
    public bool IsDismissed { get; init; }
    public required string DismissUrl { get; init; }
    public int CompletedRequired { get; init; }
    public int TotalRequired { get; init; }
    public int ProgressPercent { get; init; }

    public bool ShouldShow =>
        !IsDismissed && Items.Any(i => i.IsRequired && !i.IsCompleted);
}
