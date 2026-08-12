using RadaTik.Models;

namespace RadaTik.Services;

public interface IUsageBasedSubscriptionChargeService
{
    Task ChargeUsageIncreaseAsync(
        int companyNetworkId,
        string actorUserId,
        PricingChargeUnit? onlyUnit = null,
        CancellationToken ct = default);

    /// <summary>
    /// خصم من محفظة الشركة عند توليد/تصدير تقرير (حسب تسعير ReportsExport من مدير النظام).
    /// يتطلب اشتراكاً فعّالاً في خدمة التقارير (Reports).
    /// </summary>
    Task<ReportExportChargeResult> TryChargeReportExportAsync(
        int companyNetworkId,
        string actorUserId,
        string reportDescription,
        CancellationToken ct = default);

    Task<UsageImportChargeEstimate> EstimateImportChargeAsync(
        int companyNetworkId,
        PricingChargeUnit unit,
        int requestedCount,
        CancellationToken ct = default);

    Task InitializeBaselineAsync(
        int companyNetworkId,
        int subscriptionId,
        CancellationToken ct = default);

    Task<string> ResolveActorUserIdAsync(
        int companyNetworkId,
        CancellationToken ct = default);
}

public sealed class ReportExportChargeResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public decimal ChargedAmountSyp { get; init; }
}
