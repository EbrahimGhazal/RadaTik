namespace RadaTik.ViewModels;

/// <summary>عرض رصيد المحفظة في الهيدر حسب الدور.</summary>
public sealed class HeaderWalletBalanceViewModel
{
    public decimal BalanceSyp { get; set; }
    public decimal? BalanceUsd { get; set; }
    public bool ShowDualCurrency { get; set; }
    public string? WalletUrl { get; set; }
    /// <summary>تسمية الرصيد في الهيدر (مثلاً: مستحقات الراتب).</summary>
    public string? BalanceLabel { get; set; }
    /// <summary>عرض كل عملة في سطر منفصل (الشريط الجانبي).</summary>
    public bool StackCurrencies { get; set; }
}
