using System.Globalization;

namespace RadaTik.Helpers;

/// <summary>تحويل وعرض الليرة السورية الجديدة (ل.س.ج) والقديمة في الواجهة.</summary>
public static class SyrianCurrencyHelper
{
    /// <summary>1 ليرة جديدة = 100 ليرة قديمة (وفق تسوية النظام في RadaTik).</summary>
    public const int OldLiraPerNewLira = 100;

    public static decimal NewToOld(decimal newAmount) => newAmount * OldLiraPerNewLira;

    /// <summary>تنسيق بفواصل آلاف (مثال: 5,000,000.00).</summary>
    public static string FormatNew(decimal amount, int decimals = 2) =>
        amount.ToString($"N{decimals}", CultureInfo.InvariantCulture);

    public static string FormatNew(decimal? amount, int decimals = 2) =>
        amount.HasValue ? FormatNew(amount.Value, decimals) : "—";

    public static string FormatOld(decimal newAmount, int decimals = 0) =>
        FormatNew(NewToOld(newAmount), decimals);

    public static string FormatOld(decimal? newAmount, int decimals = 0) =>
        newAmount.HasValue ? FormatOld(newAmount.Value, decimals) : "—";

    /// <summary>تنسيق رقم عادي بفواصل آلاف — للأعداد غير المالية (عدد عملاء، منافذ، …).</summary>
    public static string FormatNumber(decimal amount, int decimals = 0) =>
        amount.ToString($"N{decimals}", CultureInfo.InvariantCulture);

    public static string FormatNumber(decimal? amount, int decimals = 0) =>
        amount.HasValue ? FormatNumber(amount.Value, decimals) : "—";

    public static string FormatNumber(int amount, int decimals = 0) =>
        FormatNumber((decimal)amount, decimals);

    public static string FormatNumber(long amount, int decimals = 0) =>
        FormatNumber((decimal)amount, decimals);
}
