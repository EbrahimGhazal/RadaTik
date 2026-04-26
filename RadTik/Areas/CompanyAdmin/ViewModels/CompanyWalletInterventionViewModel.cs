namespace RadTik.Areas.CompanyAdmin.ViewModels;

/// <summary>
/// حالة تتطلب تدخل مدير الشركة لتغذية الرصيد (تجديد معلّق لعدم كفاية الرصيد).
/// </summary>
public sealed class CompanyWalletInterventionViewModel
{
    public bool ShowModal { get; init; }
    public int SuspendedRenewalCount { get; init; }
    public string TopUpUrl { get; init; } = "";
}
