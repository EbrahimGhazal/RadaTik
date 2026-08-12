namespace RadaTik.Models;

/// <summary>
/// وحدة احتساب التسعير/الخصم (لكل ماذا؟).
/// مثال: لكل مستخدم في كل شهر.
/// </summary>
public enum PricingChargeUnit
{
    /// <summary>سعر ثابت للشبكة (ليس لكل عنصر).</summary>
    Flat = 0,

    /// <summary>يُحسب السعر لكل شبكة (مع إمكانية تطبيق إعفاء للشبكة الأولى عبر منطق الخدمة).</summary>
    PerNetwork = 11,

    /// <summary>يُحسب السعر لكل موظف (Identity User) ضمن الشبكة.</summary>
    PerUser = 1,

    /// <summary>يُحسب السعر لكل مشترك (Client).</summary>
    PerSubscriber = 2,

    /// <summary>يُحسب السعر لكل مرسل (Sector).</summary>
    PerSector = 3,

    /// <summary>يُحسب السعر لكل مستقبل (Receiver).</summary>
    PerReceiver = 4,

    /// <summary>يُحسب السعر لكل خادم MikroTik.</summary>
    PerServer = 5,

    /// <summary>يُحسب السعر لكل نقطة تحصيل (CollectionPointAccount).</summary>
    PerCollectionPoint = 6,

    /// <summary>يُحسب السعر لكل بروفايل/سرعة (Profile).</summary>
    PerSpeedProfile = 7,

    /// <summary>
    /// يٌحسب السعر لكل طلب (مثل طلبات الصيانة/تغيير السرعة) ضمن نافذة الفوترة.
    /// </summary>
    PerRequest = 8,

    /// <summary>
    /// نسبة مئوية من مبلغ التحصيل من العميل (يُخزّن المقدار في AmountSYP كقيمة النسبة، مثل 2.5 يعني 2.5%).
    /// </summary>
    PercentOfCollectedAmount = 9,

    /// <summary>يُحسب السعر لكل تقرير مُولَّد/مُصدَّر.</summary>
    PerReport = 10
}

