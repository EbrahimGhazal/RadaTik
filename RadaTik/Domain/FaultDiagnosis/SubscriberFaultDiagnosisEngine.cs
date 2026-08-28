using RadaTik.Models;

namespace RadaTik.Domain.FaultDiagnosis;

/// <summary>
/// يعزل سبب العطل من نطاق الانقطاع ثم من سلسلة الفحص.
/// لا يعتمد على نموذج لغوي: القرار قواعد صريحة قابلة للتدقيق.
/// </summary>
public static class SubscriberFaultDiagnosisEngine
{
    public static SubscriberFaultDiagnosisResult Diagnose(SubscriberFaultFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        List<SubscriberFaultEvidence> evidence = BuildEvidence(facts);

        if (!facts.IsAccountActive)
        {
            return Result(
                SubscriberFaultComponent.Account,
                SubscriberFaultConfidence.High,
                "الحساب معطّل في النظام. هذا ليس عطلاً في المرسل أو اللاقط.",
                "فعّل الحساب أو راجع طلب الاعتماد إن كان بانتظار الموافقة.",
                suggested: null,
                evidence);
        }

        if (IsExpired(facts))
        {
            string summary = facts.HasPppSession
                ? "الاشتراك منتهٍ لكن جلسة PPPoE ما زالت قائمة. جدّد الاشتراك أو أوقف الجلسة يدوياً."
                : "الاشتراك منتهٍ ولا توجد جلسة PPPoE. هذا سبب محاسبي وليس عطلاً في الشبكة.";
            return Result(
                SubscriberFaultComponent.Account,
                SubscriberFaultConfidence.High,
                summary,
                "جدّد اشتراك المشترك ثم أعد الفحص.",
                suggested: null,
                evidence);
        }

        if (facts.HasPppSession)
        {
            return Result(
                SubscriberFaultComponent.Router,
                SubscriberFaultConfidence.Medium,
                "جلسة PPPoE قائمة: المسار حتى السيرفر سليم. إن كان المشترك بلا إنترنت فالمشكلة بعد المصادقة (راوتر أو أجهزته).",
                "اطلب من المشترك فحص الراوتر ومصابيح WAN/الإنترنت وإعادة تشغيله.",
                MaintenanceType.RouterInternetLedOff,
                evidence);
        }

        if (!facts.HasMikroTikServer)
        {
            return Result(
                SubscriberFaultComponent.Unknown,
                SubscriberFaultConfidence.Low,
                "المشترك غير مربوط بخادم MikroTik، لذلك لا يمكن فحص الجلسات أو سلسلة الوصول.",
                "اربط المشترك بسيرفر ثم أعد التشخيص.",
                MaintenanceType.TechnicianVisit,
                evidence);
        }

        if (!facts.ServerApiReachable)
        {
            return Result(
                SubscriberFaultComponent.Server,
                SubscriberFaultConfidence.High,
                "تعذر الاتصال بواجهة MikroTik. العطل في السيرفر أو مسار الإدارة إليه.",
                "تحقق من تشغيل السيرفر والمنفذ 8728 وكلمة المرور والجدار الناري.",
                MaintenanceType.TechnicianVisit,
                evidence);
        }

        bool serverWide = facts.ServerClientCount >= 2 && facts.ServerConnectedCount == 0;
        if (serverWide)
        {
            return Result(
                SubscriberFaultComponent.Server,
                SubscriberFaultConfidence.High,
                $"كل مشتركي هذا السيرفر غير متصلين ({facts.ServerConnectedCount} من {facts.ServerClientCount}). النطاق برج كامل.",
                "افحص السيرفر والكهرباء والأبتريم على البرج.",
                MaintenanceType.TechnicianVisit,
                evidence);
        }

        bool sectorWide = facts.SectorClientCount >= 2 && facts.SectorConnectedCount == 0;
        if (sectorWide)
        {
            string radioNote = facts.SectorRadioDegraded
                ? " قياسات الراديو متدهورة (ضجيج/SNR/CCQ)."
                : string.Empty;
            string pingNote = facts.SectorPingOk == false
                ? " المرسل لا يرد على Ping."
                : facts.SectorPingOk == true
                    ? " المرسل يرد على Ping: العطل أقرب للراديو من كبل الإدارة."
                    : string.Empty;
            return Result(
                SubscriberFaultComponent.Sector,
                SubscriberFaultConfidence.High,
                $"كل مشتركي هذا المرسل غير متصلين ({facts.SectorConnectedCount} من {facts.SectorClientCount}) وبقية السيرفر ليست متوقفة بالكامل.{pingNote}{radioNote}",
                "افحص كهرباء المرسل والواجهة اللاسلكية والضجيج.",
                MaintenanceType.TechnicianVisit,
                evidence);
        }

        bool receiverWide = facts.ReceiverClientCount >= 2 && facts.ReceiverConnectedCount == 0;
        if (receiverWide)
        {
            if (facts.ReceiverPingOk == true)
            {
                return Result(
                    SubscriberFaultComponent.CableOrSwitch,
                    SubscriberFaultConfidence.High,
                    $"اللاقط يرد على Ping لكن كل مشتركي اللاقط غير متصلين ({facts.ReceiverConnectedCount} من {facts.ReceiverClientCount}). العطل بعد اللاقط: كبل أو سويتش.",
                    "افحص الكبل والسويتش خلف اللاقط ومغذّي POE إن وُجد بعد اللاقط.",
                    MaintenanceType.CableIssue,
                    evidence);
            }

            return Result(
                SubscriberFaultComponent.Receiver,
                SubscriberFaultConfidence.High,
                $"كل مشتركي هذا اللاقط غير متصلين ({facts.ReceiverConnectedCount} من {facts.ReceiverClientCount}) وبقية المرسل ليست متوقفة بالكامل.",
                facts.ReceiverPingOk == false
                    ? "اللاقط لا يرد على Ping: افحص اللاقط وPOE والكبل حتى اللاقط."
                    : "افحص اللاقط وPOE والكبل حتى اللاقط.",
                facts.ReceiverPingOk == false ? MaintenanceType.PoeChange : MaintenanceType.ReceiverReplacement,
                evidence);
        }

        if (facts.ReceiverPingOk == false)
        {
            SubscriberFaultConfidence confidence = facts.ReceiverClientCount <= 1
                ? SubscriberFaultConfidence.Medium
                : SubscriberFaultConfidence.High;
            return Result(
                SubscriberFaultComponent.Receiver,
                confidence,
                "اللاقط لا يرد على Ping وهذا المشترك بلا جلسة PPPoE.",
                "افحص اللاقط وPOE واتجاه التسديد.",
                MaintenanceType.PoeChange,
                evidence);
        }

        if (facts.ReceiverPingOk == true)
        {
            return Result(
                SubscriberFaultComponent.CableOrSwitch,
                SubscriberFaultConfidence.Medium,
                "اللاقط يصل من السيرفر والمشترك بلا جلسة PPPoE. العطل في آخر الميل: كبل أو سويتش أو راوتر.",
                "اطلب فحص كبل المشترك والراوتر ومصابيح WAN.",
                MaintenanceType.CableIssue,
                evidence);
        }

        if (facts.SectorPingOk == false)
        {
            return Result(
                SubscriberFaultComponent.Sector,
                SubscriberFaultConfidence.Medium,
                "المرسل لا يرد على Ping وهذا المشترك بلا جلسة. إن كان الوحيد على المرسل فالعطل في المرسل حتى يثبت العكس.",
                "افحص المرسل وكهرباءه.",
                MaintenanceType.TechnicianVisit,
                evidence);
        }

        if (facts.ReceiverClientCount <= 1 && facts.SectorClientCount >= 2 && facts.SectorConnectedCount > 0)
        {
            return Result(
                SubscriberFaultComponent.LastMile,
                SubscriberFaultConfidence.Low,
                "المشترك وحيد على اللاقط وبقية المرسل متصلة. السبب فردي: لاقط هذا المشترك أو كبله أو راوتره.",
                "زيارة فنية لمنزل المشترك أو موقع اللاقط.",
                MaintenanceType.TechnicianVisit,
                evidence);
        }

        if (facts.ReceiverClientCount >= 2 && facts.ReceiverConnectedCount > 0)
        {
            return Result(
                SubscriberFaultComponent.CableOrSwitch,
                SubscriberFaultConfidence.Medium,
                $"جيرانه على نفس اللاقط متصلون ({facts.ReceiverConnectedCount} من {facts.ReceiverClientCount}) وهو غير متصل. العطل فردي بعد اللاقط.",
                "افحص كبل هذا المشترك وراوتره.",
                MaintenanceType.CableIssue,
                evidence);
        }

        return Result(
            SubscriberFaultComponent.Unknown,
            SubscriberFaultConfidence.Low,
            "لا توجد جلسة PPPoE ولا مؤشرات نطاق كافية لعزل العنصر. أعد الفحص بعد التأكد من ربط اللاقط والسيرفر.",
            "زيارة فنية مع فحص السلسلة يدوياً.",
            MaintenanceType.TechnicianVisit,
            evidence);
    }

    private static bool IsExpired(SubscriberFaultFacts facts) =>
        facts.AccountExpirationDate.HasValue && facts.AccountExpirationDate.Value < facts.Now;

    private static List<SubscriberFaultEvidence> BuildEvidence(SubscriberFaultFacts facts)
    {
        List<SubscriberFaultEvidence> evidence =
        [
            Item("account", "الحساب", facts.IsAccountActive ? "مفعّل" : "معطّل", !facts.IsAccountActive),
            Item(
                "expiration",
                "صلاحية الاشتراك",
                facts.AccountExpirationDate.HasValue
                    ? facts.AccountExpirationDate.Value.ToString("yyyy/MM/dd")
                    : "غير محدد",
                IsExpired(facts)),
            Item("ppp", "جلسة PPPoE", facts.HasPppSession ? "متصلة" : "غير متصلة", !facts.HasPppSession && facts.IsAccountActive),
            Item(
                "server-api",
                "واجهة السيرفر",
                !facts.HasMikroTikServer ? "غير مربوط" : facts.ServerApiReachable ? "ترد" : "لا ترد",
                facts.HasMikroTikServer && !facts.ServerApiReachable),
            Item(
                "server-scope",
                "نطاق السيرفر",
                $"{facts.ServerConnectedCount} متصل من {facts.ServerClientCount}",
                facts.ServerClientCount >= 2 && facts.ServerConnectedCount == 0),
            Item(
                "sector-scope",
                "نطاق المرسل",
                facts.SectorClientCount > 0
                    ? $"{facts.SectorConnectedCount} متصل من {facts.SectorClientCount}"
                    : "غير مربوط بمرسل",
                facts.SectorClientCount >= 2 && facts.SectorConnectedCount == 0),
            Item(
                "receiver-scope",
                "نطاق اللاقط",
                facts.ReceiverClientCount > 0
                    ? $"{facts.ReceiverConnectedCount} متصل من {facts.ReceiverClientCount}"
                    : "غير مربوط بلاقط",
                facts.ReceiverClientCount >= 2 && facts.ReceiverConnectedCount == 0)
        ];

        if (facts.SectorPingOk.HasValue)
        {
            evidence.Add(Item("sector-ping", "Ping المرسل", facts.SectorPingOk.Value ? "يرد" : "لا يرد", !facts.SectorPingOk.Value));
        }

        if (facts.ReceiverPingOk.HasValue)
        {
            evidence.Add(Item("receiver-ping", "Ping اللاقط", facts.ReceiverPingOk.Value ? "يرد" : "لا يرد", !facts.ReceiverPingOk.Value));
        }

        if (facts.ClientPingOk.HasValue)
        {
            evidence.Add(Item("client-ping", "Ping عنوان المشترك", facts.ClientPingOk.Value ? "يرد" : "لا يرد", !facts.ClientPingOk.Value));
        }

        if (facts.SectorNoiseFloorDbm.HasValue || facts.SectorSnrDb.HasValue || facts.SectorCcqPercent.HasValue)
        {
            string radio =
                $"Noise {Format(facts.SectorNoiseFloorDbm)} dBm · SNR {Format(facts.SectorSnrDb)} dB · CCQ {Format(facts.SectorCcqPercent)}%";
            evidence.Add(Item("radio", "راديو المرسل", radio, facts.SectorRadioDegraded));
        }

        return evidence;
    }

    private static string Format(int? value) => value.HasValue ? value.Value.ToString() : "—";

    private static SubscriberFaultEvidence Item(string code, string label, string value, bool isAlert) =>
        new()
        {
            Code = code,
            Label = label,
            Value = value,
            IsAlert = isAlert
        };

    private static SubscriberFaultDiagnosisResult Result(
        SubscriberFaultComponent cause,
        SubscriberFaultConfidence confidence,
        string summary,
        string action,
        MaintenanceType? suggested,
        IReadOnlyList<SubscriberFaultEvidence> evidence) =>
        new()
        {
            Cause = cause,
            Confidence = confidence,
            CauseLabel = LabelOf(cause),
            ConfidenceLabel = confidence switch
            {
                SubscriberFaultConfidence.High => "عالية",
                SubscriberFaultConfidence.Medium => "متوسطة",
                _ => "منخفضة"
            },
            Summary = summary,
            SuggestedAction = action,
            SuggestedMaintenanceType = suggested,
            Evidence = evidence
        };

    public static string LabelOf(SubscriberFaultComponent cause) => cause switch
    {
        SubscriberFaultComponent.Account => "الحساب",
        SubscriberFaultComponent.Server => "السيرفر",
        SubscriberFaultComponent.Sector => "المرسل",
        SubscriberFaultComponent.Receiver => "اللاقط",
        SubscriberFaultComponent.CableOrSwitch => "الكبل أو السويتش",
        SubscriberFaultComponent.Router => "الراوتر",
        SubscriberFaultComponent.LastMile => "اللاقط أو الكبل أو الراوتر",
        _ => "غير محدد"
    };
}
