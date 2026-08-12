using RadaTik.Models;

namespace RadaTik.Helpers;

public static class SubscriberInstallationInvoiceStatusLabels
{
    public static string Get(SubscriberInstallationInvoiceStatus status) => status switch
    {
        SubscriberInstallationInvoiceStatus.Draft => "مسودة",
        SubscriberInstallationInvoiceStatus.PendingWalletPayment => "بانتظار الدفع",
        SubscriberInstallationInvoiceStatus.Finalized => "مُثبت — بانتظار التحصيل",
        SubscriberInstallationInvoiceStatus.PartiallyPaid => "مدفوع جزئياً",
        SubscriberInstallationInvoiceStatus.Paid => "مسددة",
        SubscriberInstallationInvoiceStatus.Cancelled => "ملغاة",
        _ => status.ToString()
    };
}
