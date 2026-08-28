using RadaTik.Models;

namespace RadaTik.Domain.FaultDiagnosis;

/// <summary>
/// يعزل سبب العطل من نطاق الانقطاع ثم من سلسلة الفحص وأسئلة LED والسجل المؤكد.
/// </summary>
public static class SubscriberFaultDiagnosisEngine
{
    public static SubscriberFaultDiagnosisResult Diagnose(SubscriberFaultFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        List<SubscriberFaultEvidence> evidence = BuildEvidence(facts);
        SubscriberFaultDiagnosisResult core = DiagnoseCore(facts, evidence);
        return RefineWithLedAndHistory(facts, core);
    }

    private static SubscriberFaultDiagnosisResult DiagnoseCore(
        SubscriberFaultFacts facts,
        List<SubscriberFaultEvidence> evidence)
    {
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

    private static SubscriberFaultDiagnosisResult RefineWithLedAndHistory(
        SubscriberFaultFacts facts,
        SubscriberFaultDiagnosisResult core)
    {
        if (IsInfrastructureCause(core.Cause))
        {
            return core;
        }

        SubscriberFaultDiagnosisResult? fromLed = ApplyLed(facts, core);
        if (fromLed != null)
        {
            return fromLed;
        }

        return ApplyHistory(facts, core);
    }

    private static bool IsInfrastructureCause(SubscriberFaultComponent cause) =>
        cause is SubscriberFaultComponent.Account
            or SubscriberFaultComponent.Server
            or SubscriberFaultComponent.Sector
            or SubscriberFaultComponent.Receiver;

    private static SubscriberFaultDiagnosisResult? ApplyLed(
        SubscriberFaultFacts facts,
        SubscriberFaultDiagnosisResult core)
    {
        SubscriberFaultLedAnswers led = facts.Led;
        if (!led.HasAny)
        {
            return null;
        }

        IReadOnlyList<SubscriberFaultEvidence> evidence = core.Evidence;

        if (facts.HasPppSession)
        {
            if (led.RouterPowerOn == false)
            {
                return Result(
                    SubscriberFaultComponent.Router,
                    SubscriberFaultConfidence.High,
                    "جلسة PPPoE كانت قائمة لكن الراوتر لا يعمل الآن. أعد الفحص بعد تشغيل الراوتر.",
                    "شغّل الراوتر أو استبدله.",
                    MaintenanceType.RouterNotWorking,
                    evidence);
            }

            if (led.RouterPowerOn == true && led.WanLedOn == true && led.InternetLedOn == false)
            {
                return Result(
                    SubscriberFaultComponent.Router,
                    SubscriberFaultConfidence.High,
                    "المسار حتى السيرفر سليم وليد الإنترنت مطفأ. المشكلة في الراوتر أو أجهزته.",
                    "راجع إعدادات الراوتر ومصابيحه.",
                    MaintenanceType.RouterInternetLedOff,
                    evidence);
            }

            return null;
        }

        if (led.NeighborsOnSwitchDown == true)
        {
            return Result(
                SubscriberFaultComponent.Switch,
                SubscriberFaultConfidence.High,
                "عدة أجهزة خلف نفس السويتش متوقفة واللاقط/المسار العلوي ليسا السبب الأرجح. العطل في السويتش.",
                "افحص السويتش ومغذّيه واستبدله إن لزم.",
                MaintenanceType.SwitchReplacement,
                evidence);
        }

        if (led.RouterPowerOn == false)
        {
            return Result(
                SubscriberFaultComponent.Router,
                SubscriberFaultConfidence.High,
                "الراوتر لا يعمل (بدون طاقة أو لا يقلع). هذا عطل في راوتر المشترك.",
                "اطلب إعادة التشغيل أو تغيير الراوتر.",
                MaintenanceType.RouterNotWorking,
                evidence);
        }

        if (led.RouterPowerOn == true && led.WanLedOn == false)
        {
            return Result(
                SubscriberFaultComponent.Cable,
                SubscriberFaultConfidence.High,
                "الراوتر يعمل وليد WAN مطفأ: لا تصل إشارة من الكبل/السويتش إلى منفذ WAN.",
                "افحص كبل WAN والموصلات حتى السويتش أو اللاقط.",
                MaintenanceType.CableIssue,
                evidence);
        }

        if (led.RouterPowerOn == true && led.WanLedOn == true && led.InternetLedOn == false)
        {
            return Result(
                SubscriberFaultComponent.Router,
                SubscriberFaultConfidence.High,
                "الراوتر يعمل وليد WAN مضاء لكن ليد الإنترنت مطفأ. الإعدادات أو المصادقة على الراوتر.",
                "راجع إعدادات PPPoE/WAN على الراوتر أو غيّرها.",
                MaintenanceType.RouterInternetLedOff,
                evidence);
        }

        if (led.RouterPowerOn == true && led.WanLedOn == true && led.InternetLedOn == true && !facts.HasPppSession)
        {
            return Result(
                SubscriberFaultComponent.Router,
                SubscriberFaultConfidence.Medium,
                "مصابيح الراوتر تبدو سليمة لكن لا توجد جلسة PPPoE. غالباً إعدادات الراوتر أو كلمة السر.",
                "راجع اسم المستخدم وكلمة سر PPPoE على الراوتر.",
                MaintenanceType.RouterSettingsChange,
                evidence);
        }

        return null;
    }

    private static SubscriberFaultDiagnosisResult ApplyHistory(
        SubscriberFaultFacts facts,
        SubscriberFaultDiagnosisResult core)
    {
        if (core.Cause is not (SubscriberFaultComponent.CableOrSwitch or SubscriberFaultComponent.LastMile))
        {
            return core;
        }

        SubscriberFaultLastMileStats? stats = facts.LastMileHistory;
        if (stats == null || stats.SampleCount < 5 || stats.TotalLastMile < 5)
        {
            return core;
        }

        int cable = stats.CableCount;
        int sw = stats.SwitchCount;
        int router = stats.RouterCount;
        int receiver = stats.ReceiverCount;
        int max = Math.Max(Math.Max(cable, sw), Math.Max(router, receiver));
        if (max * 2 < stats.TotalLastMile)
        {
            return core;
        }

        if (max == sw)
        {
            return Result(
                SubscriberFaultComponent.Switch,
                SubscriberFaultConfidence.Medium,
                core.Summary + " السجل المؤكد يرجّح السويتش في حالات آخر الميل المشابهة.",
                "افحص السويتش أولاً بناءً على نتائج الزيارات السابقة.",
                MaintenanceType.SwitchReplacement,
                core.Evidence);
        }

        if (max == cable)
        {
            return Result(
                SubscriberFaultComponent.Cable,
                SubscriberFaultConfidence.Medium,
                core.Summary + " السجل المؤكد يرجّح الكبل في حالات آخر الميل المشابهة.",
                "افحص الكبل والموصلات أولاً بناءً على نتائج الزيارات السابقة.",
                MaintenanceType.CableIssue,
                core.Evidence);
        }

        if (max == router)
        {
            return Result(
                SubscriberFaultComponent.Router,
                SubscriberFaultConfidence.Medium,
                core.Summary + " السجل المؤكد يرجّح الراوتر في حالات آخر الميل المشابهة.",
                "افحص الراوتر أولاً بناءً على نتائج الزيارات السابقة.",
                MaintenanceType.RouterReplacement,
                core.Evidence);
        }

        return Result(
            SubscriberFaultComponent.Receiver,
            SubscriberFaultConfidence.Medium,
            core.Summary + " السجل المؤكد يرجّح اللاقط في حالات آخر الميل المشابهة.",
            "افحص اللاقط وPOE أولاً بناءً على نتائج الزيارات السابقة.",
            MaintenanceType.PoeChange,
            core.Evidence);
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

        evidence.Add(HopItem("sector-ping", "Ping المرسل", facts.SectorIp, facts.SectorPingOk, facts.SectorPingMessage));
        evidence.Add(HopItem("receiver-ping", "Ping اللاقط", facts.ReceiverIp, facts.ReceiverPingOk, facts.ReceiverPingMessage));
        evidence.Add(HopItem("client-ping", "Ping عنوان المشترك", facts.ClientIp, facts.ClientPingOk, facts.ClientPingMessage));

        if (facts.SectorNoiseFloorDbm.HasValue || facts.SectorSnrDb.HasValue || facts.SectorCcqPercent.HasValue)
        {
            string radio =
                $"Noise {Format(facts.SectorNoiseFloorDbm)} dBm · SNR {Format(facts.SectorSnrDb)} dB · CCQ {Format(facts.SectorCcqPercent)}%";
            evidence.Add(Item("radio", "راديو المرسل", radio, facts.SectorRadioDegraded));
        }

        SubscriberFaultLedAnswers led = facts.Led;
        if (led.HasAny)
        {
            evidence.Add(Item("led-power", "الراوتر يعمل", Tri(led.RouterPowerOn), led.RouterPowerOn == false));
            evidence.Add(Item("led-wan", "ليد WAN", Tri(led.WanLedOn), led.WanLedOn == false));
            evidence.Add(Item("led-internet", "ليد الإنترنت", Tri(led.InternetLedOn), led.InternetLedOn == false));
            evidence.Add(Item("led-switch", "أجهزة أخرى على السويتش متوقفة", Tri(led.NeighborsOnSwitchDown), led.NeighborsOnSwitchDown == true));
        }

        return evidence;
    }

    private static SubscriberFaultEvidence HopItem(string code, string label, string? ip, bool? ok, string? message)
    {
        string address = string.IsNullOrWhiteSpace(ip) ? "بدون عنوان" : ip.Trim();
        string status = ok switch
        {
            true => "يرد",
            false => "لا يرد",
            _ => "لم يُفحص"
        };
        string value = string.IsNullOrWhiteSpace(message) ? $"{address} — {status}" : $"{address} — {status} ({message})";
        return Item(code, label, value, ok == false);
    }

    private static string Tri(bool? value) => value switch
    {
        true => "نعم",
        false => "لا",
        _ => "غير محدد"
    };

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
        SubscriberFaultComponent.Cable => "الكبل",
        SubscriberFaultComponent.Switch => "السويتش",
        SubscriberFaultComponent.Router => "الراوتر",
        SubscriberFaultComponent.LastMile => "اللاقط أو الكبل أو الراوتر",
        _ => "غير محدد"
    };
}
