namespace RadaTik.Models.Business;

/// <summary>تصنيفات جاهزة بالعربية — لتبسيط الإدخال دون مصطلحات محاسبية.</summary>
public static class MoneyDiaryCategories
{
    public sealed record CategoryOption(string Key, string Label, MoneyDiaryEntryType EntryType);

    public static readonly IReadOnlyList<CategoryOption> All = new List<CategoryOption>
  {
    new("income_subscriptions", "إيراد اشتراكات / تحصيل", MoneyDiaryEntryType.Income),
    new("income_maintenance", "إيراد صيانة (صافي الشركة)", MoneyDiaryEntryType.Income),
    new("income_installation", "إيراد تركيب / تجهيز مشترك", MoneyDiaryEntryType.Income),
    new("income_equipment_sale", "بيع معدات", MoneyDiaryEntryType.Income),
    new("income_other", "إيراد آخر", MoneyDiaryEntryType.Income),

    new("expense_purchase", "شراء معدات أو مواد", MoneyDiaryEntryType.Expense),
    new("expense_maintenance", "مصروف صيانة", MoneyDiaryEntryType.Expense),
    new("expense_transport", "نقل / مواصلات", MoneyDiaryEntryType.Expense),
    new("expense_salary", "رواتب وأجور", MoneyDiaryEntryType.Expense),
    new("expense_rent_utilities", "إيجار / كهرباء / اتصالات", MoneyDiaryEntryType.Expense),
    new("expense_other", "مصروف آخر", MoneyDiaryEntryType.Expense)
  };

    public static string GetLabel(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "غير محدد";
        }

        return All.FirstOrDefault(c => c.Key == key)?.Label ?? key;
    }

    public static IEnumerable<CategoryOption> ForType(MoneyDiaryEntryType type) =>
      All.Where(c => c.EntryType == type);
}
