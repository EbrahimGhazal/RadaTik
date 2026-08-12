using RadaTik.Models;

namespace RadaTik.Helpers;

public static class SubscriberInstallationPaymentMethodLabels
{
    public static string Get(SubscriberInstallationPaymentMethod method) => method switch
    {
        SubscriberInstallationPaymentMethod.Cash => "نقدي (دفتر إيراد)",
        SubscriberInstallationPaymentMethod.Wallet => "محفظة التطبيق",
        _ => method.ToString()
    };
}
