namespace RadaTik.ViewModels;

/// <summary>شريحة رصيد واحدة في الهيدر أو الشريط الجانبي.</summary>
public sealed class HeaderBalanceChipViewModel
{
    public string Label { get; set; } = string.Empty;
    public string IconClass { get; set; } = "fas fa-wallet";
    public decimal AmountSyp { get; set; }
    public decimal? AmountUsd { get; set; }
    public bool ShowUsd { get; set; }
    public string? Url { get; set; }
    public string Tone { get; set; } = "wallet";
    public bool Stacked { get; set; }
}

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
    /// <summary>مدير الشركة: المحفظة والصندوق معاً، كل منهما بالليرة والدولار.</summary>
    public IReadOnlyList<HeaderBalanceChipViewModel> Chips { get; set; } = [];
}

