using RadaTik.Models;
using RadaTik.Services.SystemAdminPricing;

namespace RadaTik.Services;

public sealed class SystemAdminPricingReadiness
{
    public bool IsComplete { get; init; }
    public required IReadOnlyList<string> MissingItems { get; init; }
    public int TotalRequired { get; init; }
    public int ConfiguredCount { get; init; }
    public int ProgressPercent { get; init; }
}

public interface ISystemAdminPricingReadinessService
{
    Task<SystemAdminPricingReadiness> EvaluateAsync(CancellationToken cancellationToken = default);
}

public sealed class SystemAdminPricingReadinessService(
    IServiceCatalogSnapshotProvider snapshotProvider) : ISystemAdminPricingReadinessService
{
    /// <summary>7 خدمات دورية × (سعر إنشاء + سعر تجديد) + تقارير + ضريبة بروفايل.</summary>
    public const int TotalRequiredPricingChecks = 7 * 2 + 2;

    private readonly IServiceCatalogSnapshotProvider _snapshotProvider = snapshotProvider;

    public async Task<SystemAdminPricingReadiness> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        ServiceCatalogSnapshot snapshot = await _snapshotProvider.BuildAsync(cancellationToken);
        List<string> missing = [];

        CheckRecurring(snapshot.NetworkPricing, "الشبكات", missing);
        CheckRecurring(snapshot.ServerPricing, "خوادم MikroTik", missing);
        CheckRecurring(snapshot.SectorPricing, "القطاعات", missing);
        CheckRecurring(snapshot.ReceiverPricing, "المستقبلات", missing);
        CheckRecurring(snapshot.ClientPricing, "المشتركين", missing);
        CheckRecurring(snapshot.UserPricing, "الموظفين", missing);
        CheckRecurring(snapshot.SpeedProfilePricing, "بروفايلات السرعة", missing);

        if (!snapshot.ReportPricing.HasInitialPricing)
        {
            missing.Add("التقارير: سعر الإنشاء");
        }

        if (!snapshot.HasProfilePriceTax)
        {
            missing.Add("ضريبة سعر البروفايل");
        }

        int totalRequired = TotalRequiredPricingChecks;
        int configured = Math.Clamp(totalRequired - missing.Count, 0, totalRequired);
        int progress = totalRequired == 0
            ? 100
            : (int)Math.Round(configured * 100.0 / totalRequired);

        return new SystemAdminPricingReadiness
        {
            IsComplete = missing.Count == 0,
            MissingItems = missing,
            TotalRequired = totalRequired,
            ConfiguredCount = configured,
            ProgressPercent = progress
        };
    }

    private static void CheckRecurring(RecurringServiceSnapshot pricing, string label, List<string> missing)
    {
        if (!pricing.HasInitialPricing)
        {
            missing.Add($"{label}: سعر الإنشاء");
        }

        if (!pricing.HasRenewalPricing)
        {
            missing.Add($"{label}: سعر التجديد");
        }
    }
}
