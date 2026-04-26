using RadTik.Models;

namespace RadTik.Helpers;

public static class NetworkWalletTransactionDisplayHelper
{
    public static string TypeLabel(NetworkWalletTransactionType type) => type switch
    {
        NetworkWalletTransactionType.TopUp => "تغذية رصيد",
        NetworkWalletTransactionType.ServiceCharge => "حسم خدمة",
        NetworkWalletTransactionType.Refund => "استرجاع",
        NetworkWalletTransactionType.Adjustment => "تعديل",
        NetworkWalletTransactionType.CollectionCommission => "عمولة تحصيل",
        NetworkWalletTransactionType.MaintenanceRevenue => "إيراد صيانة",
        _ => type.ToString()
    };

    public static string TypeClass(NetworkWalletTransactionType type) => type switch
    {
        NetworkWalletTransactionType.TopUp => "is-success",
        NetworkWalletTransactionType.Refund => "is-success",
        NetworkWalletTransactionType.ServiceCharge => "is-danger",
        NetworkWalletTransactionType.Adjustment => "is-warning",
        NetworkWalletTransactionType.CollectionCommission => "is-danger",
        NetworkWalletTransactionType.MaintenanceRevenue => "is-success",
        _ => "is-neutral"
    };

    public static string TypeDetails(NetworkWalletTransaction tx)
    {
        var notes = tx.Notes?.Trim();
        return tx.Type switch
        {
            NetworkWalletTransactionType.TopUp => "تغذية رصيد المحفظة",
            NetworkWalletTransactionType.Refund => "استرجاع مبلغ إلى المحفظة",
            NetworkWalletTransactionType.CollectionCommission => "حسم عمولة التحصيل من عملية دفع",
            NetworkWalletTransactionType.MaintenanceRevenue => "إضافة صافي إيراد فاتورة صيانة",
            NetworkWalletTransactionType.ServiceCharge => $"تم حسم رسوم {ExtractServiceName(notes)}",
            NetworkWalletTransactionType.Adjustment => string.IsNullOrWhiteSpace(notes)
                ? "تعديل يدوي على الرصيد"
                : $"تعديل رصيد: {notes}",
            _ => string.IsNullOrWhiteSpace(notes) ? "—" : notes
        };
    }

    private static string ExtractServiceName(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return "غير محدد";
        }

        if (notes.Contains("مرسل", StringComparison.OrdinalIgnoreCase) ||
            notes.Contains("Sector", StringComparison.OrdinalIgnoreCase))
        {
            return "خدمة المرسلات";
        }

        if (notes.Contains("تقرير", StringComparison.OrdinalIgnoreCase))
        {
            return "خدمة التقارير";
        }

        if (notes.Contains("FeatureKey", StringComparison.OrdinalIgnoreCase) ||
            notes.Contains("خصم عنصر جديد", StringComparison.OrdinalIgnoreCase))
        {
            return "خدمة اشتراك/ميزة";
        }

        return "خدمة نظامية";
    }
}
