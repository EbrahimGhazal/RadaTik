using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.SystemAdminPricing;

namespace RadaTik.Helpers;

/// <summary>
/// نصوص افتراضية وملخصات لتوثيق الخدمات في كتالوج مدير النظام.
/// </summary>
public static class ServiceCatalogDocumentationHelper
{
    public static string BuildDefaultPricingPolicyHtml(string displayName)
    {
        return $"""
                <p><strong>سياسة التسعير — {displayName}</strong></p>
                <ul>
                    <li>يُخصم مبلغ الاشتراك من محفظة الشركة عند إرسال طلب التفعيل (إن وُجد سعر).</li>
                    <li>بعد موافقة مدير النظام يُفعَّل الاشتراك وتظهر روابط الخدمة في لوحة مدير الشركة.</li>
                    <li>الإضافات ضمن الخدمة (مشترك، خادم، …) تُحسب حسب وحدة التسعير المعرّفة أدناه.</li>
                </ul>
                """;
    }

    public static string BuildDefaultRenewalPolicyHtml(string displayName)
    {
        return $"""
                <p><strong>سياسة التجديد — {displayName}</strong></p>
                <ul>
                    <li>يُجدَّد الاشتراك تلقائياً من محفظة الشركة عند الاستحقاق وفق دورة الفوترة المعتمدة.</li>
                    <li>عند انخفاض الرصيد قد يُعلَّق الاشتراك حتى تعبئة المحفظة.</li>
                    <li>راجع قسم «أسعار الخدمات» في هذا الكتالوج لتحديد سعر التجديد وعدد الوحدات المجانية.</li>
                </ul>
                """;
    }

    public static string BuildPricingPlansSummaryHtml(IReadOnlyList<FeaturePricing> activePricings)
    {
        if (activePricings.Count == 0)
        {
            return "<p class=\"text-muted mb-0\">لم تُعرّف أسعار نشطة لهذه الخدمة بعد.</p>";
        }

        IEnumerable<string> lines = activePricings
            .OrderBy(p => p.BillingPeriod)
            .ThenBy(p => p.ChargeUnit)
            .Select(p =>
            {
                string period = PricingDisplay.BillingPeriodLabel(p.BillingPeriod);
                string unit = PricingDisplay.ChargeUnitSubjectLabel(p.ChargeUnit);
                return $"<li>{period} — {p.AmountSYP:N0} ل.س.ج لكل {unit}</li>";
            });

        return $"<ul class=\"mb-0\">{string.Join("", lines)}</ul>";
    }

    public static string BuildSuggestedRenewalFromRecurring(RecurringServiceSnapshot snapshot)
    {
        if (!snapshot.HasRenewalPricing)
        {
            return "<p class=\"text-muted mb-0\">لم يُضبط سعر تجديد دوري لهذه الخدمة في قسم الأسعار.</p>";
        }

        string period = PricingDisplay.BillingPeriodLabel(snapshot.RenewalBillingPeriod);
        string freeRenewal = snapshot.FreeRenewalUnits > 0
            ? $"أول {snapshot.FreeRenewalUnits} وحدة مجانية في كل دورة تجديد."
            : "لا توجد وحدات مجانية في التجديد.";

        return $"""
                <p>يُجدَّد اشتراك <strong>{snapshot.ServiceName}</strong> كل <strong>{period}</strong>.</p>
                <p>سعر التجديد: <strong>{snapshot.RenewalPricePerUnitSyp:N0} ل.س.ج</strong> لكل وحدة مستخدمة (حسب نوع الوحدة في التسعير).</p>
                <p>{freeRenewal}</p>
                <p class="text-muted small mb-0">يُخصم التجديد تلقائياً من محفظة الشركة ما لم يكن الرصيد كافياً.</p>
                """;
    }

    public static RecurringServiceSnapshot? TryMapRecurringSnapshot(string featureKey, ServiceCatalogSnapshot snapshot)
    {
        return featureKey switch
        {
            FeatureKeys.Networks => snapshot.NetworkPricing,
            FeatureKeys.MikroTikServers => snapshot.ServerPricing,
            FeatureKeys.Sectors => snapshot.SectorPricing,
            FeatureKeys.Receivers => snapshot.ReceiverPricing,
            FeatureKeys.Clients => snapshot.ClientPricing,
            FeatureKeys.Users => snapshot.UserPricing,
            FeatureKeys.Profiles => snapshot.SpeedProfilePricing,
            _ => null
        };
    }
}
