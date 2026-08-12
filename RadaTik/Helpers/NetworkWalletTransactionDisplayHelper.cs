using System.Text.RegularExpressions;

using RadaTik.Models;
using RadaTik.Security;



namespace RadaTik.Helpers;



/// <summary>نصوص عربية واضحة لعرض حركات محفظة الشركة في الواجهة.</summary>

public static class NetworkWalletTransactionDisplayHelper

{

    private static readonly Regex RequestIdRegex = new(@"طلب\s*#(\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PaymentIdRegex = new(@"عملية\s*دفع\s*#(\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex InvoiceIdRegex = new(@"فاتورة\s*#?(\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);



    public static string TypeLabel(NetworkWalletTransactionType type) => type switch

    {

        NetworkWalletTransactionType.TopUp => "تغذية رصيد",

        NetworkWalletTransactionType.ServiceCharge => "حسم خدمة",

        NetworkWalletTransactionType.Refund => "استرجاع",

        NetworkWalletTransactionType.Adjustment => "تعديل يدوي",

        NetworkWalletTransactionType.CollectionCommission => "عمولة تحصيل",

        NetworkWalletTransactionType.MaintenanceRevenue => "إيراد صيانة",

        NetworkWalletTransactionType.SubscriptionCollectedRevenue => "إيراد تحصيل",

        NetworkWalletTransactionType.MaterialPurchasePayment => "دفع مواد",

        NetworkWalletTransactionType.MaterialPurchaseRefund => "استرداد شراء مواد",

        NetworkWalletTransactionType.MaterialSaleReceipt => "تحصيل بيع مواد",

        NetworkWalletTransactionType.MaterialSaleRefund => "عكس تحصيل مواد",

        NetworkWalletTransactionType.WalletFundedFromCashBox => "تغذية من الصندوق",

        _ => type.ToString()

    };



    public static string TypeClass(NetworkWalletTransactionType type) => type switch

    {

        NetworkWalletTransactionType.TopUp => "is-success",

        NetworkWalletTransactionType.WalletFundedFromCashBox => "is-success",

        NetworkWalletTransactionType.Refund => "is-success",

        NetworkWalletTransactionType.MaterialPurchaseRefund => "is-success",

        NetworkWalletTransactionType.MaterialSaleReceipt => "is-success",

        NetworkWalletTransactionType.ServiceCharge => "is-danger",

        NetworkWalletTransactionType.CollectionCommission => "is-danger",

        NetworkWalletTransactionType.MaterialPurchasePayment => "is-danger",

        NetworkWalletTransactionType.MaterialSaleRefund => "is-danger",

        NetworkWalletTransactionType.Adjustment => "is-warning",

        NetworkWalletTransactionType.MaintenanceRevenue => "is-success",

        NetworkWalletTransactionType.SubscriptionCollectedRevenue => "is-success",

        _ => "is-neutral"

    };



    /// <summary>سبب الحركة — جملة واحدة توضّح لماذا تغيّر الرصيد.</summary>

    public static string TypeDetails(NetworkWalletTransaction tx)

    {

        string? notes = tx.Notes?.Trim();

        return tx.Type switch

        {

            NetworkWalletTransactionType.TopUp =>

                "إضافة مبلغ إلى رصيد المحفظة بعد اعتماد طلب التغذية",



            NetworkWalletTransactionType.Refund =>

                "إرجاع مبلغ سبق خصمه من المحفظة",



            NetworkWalletTransactionType.CollectionCommission =>

                "خصم عمولة المنصة من مبلغ دُفِع للشركة (اشتراك أو تحصيل)",



            NetworkWalletTransactionType.MaintenanceRevenue =>

                "إضافة صافي مبلغ فاتورة صيانة إلى المحفظة بعد التحصيل",



            NetworkWalletTransactionType.SubscriptionCollectedRevenue =>

                "إيداع المبلغ الذي دفعه المشترك أو نقطة التحصيل (قبل خصم عمولة المنصة)",



            NetworkWalletTransactionType.ServiceCharge =>

                DescribeServiceChargeReason(tx),



            NetworkWalletTransactionType.Adjustment =>

                "تعديل يدوي على رصيد المحفظة (زيادة أو نقصان)",



            NetworkWalletTransactionType.MaterialPurchasePayment =>

                "دفع فاتورة شراء مواد من رصيد المحفظة",



            NetworkWalletTransactionType.MaterialPurchaseRefund =>

                "إرجاع مبلغ دُفع سابقاً لفاتورة شراء مواد (إلغاء أو تعديل)",



            NetworkWalletTransactionType.MaterialSaleReceipt =>

                "إيداع مبلغ تحصيل فاتورة بيع مواد إلى المحفظة",



            NetworkWalletTransactionType.MaterialSaleRefund =>

                "عكس تحصيل فاتورة بيع مواد (إلغاء أو تعديل)",



            NetworkWalletTransactionType.WalletFundedFromCashBox =>

                "تحويل تنظيمي: نقل مبلغ نقدي من الصندوق إلى رصيد المحفظة (ل.س.ج) — لا يُستبدل التحصيل أو طلب تعبئة المنصة",



            _ => "حركة على المحفظة"

        };

    }



    /// <summary>مرجع وملاحظة — أرقام الطلبات والفواتير وتفاصيل إضافية للمتابعة.</summary>

    public static string ReferenceAndNotes(NetworkWalletTransaction tx)

    {

        List<string> parts = [];



        string? fromIds = BuildReferenceFromLinkedIds(tx);

        if (!string.IsNullOrWhiteSpace(fromIds))

        {

            parts.Add(fromIds);

        }



        string? fromNotes = BuildReferenceFromNotes(tx);

        if (!string.IsNullOrWhiteSpace(fromNotes) && !parts.Contains(fromNotes, StringComparer.Ordinal))

        {

            parts.Add(fromNotes);

        }



        if (parts.Count == 0)

        {

            return EmptyReferencePlaceholder;

        }



        return string.Join(" — ", parts);

    }



    private const string EmptyReferencePlaceholder = "—";



    private static string? BuildReferenceFromLinkedIds(NetworkWalletTransaction tx)

    {

        if (tx.NetworkTopUpRequestId is int topUpId)

        {

            return $"طلب تغذية رقم {topUpId}";

        }



        if (tx.RelatedPaymentTransactionId is int paymentId)

        {

            return $"عملية دفع رقم {paymentId}";

        }



        if (tx.MaterialPurchaseInvoiceId is int purchaseId)

        {

            return $"فاتورة شراء مواد رقم {purchaseId}";

        }



        if (tx.MaterialSalesInvoiceId is int salesId)

        {

            return $"فاتورة بيع مواد رقم {salesId}";

        }



        if (tx.NetworkServiceRequestId is int serviceReqId)

        {

            return $"طلب خدمة رقم {serviceReqId}";

        }



        if (tx.NetworkServiceSubscriptionId is int subId)

        {

            return $"اشتراك خدمة رقم {subId}";

        }



        return null;

    }



    private static string? BuildReferenceFromNotes(NetworkWalletTransaction tx)

    {

        string? notes = tx.Notes?.Trim();

        if (string.IsNullOrWhiteSpace(notes))

        {

            return null;

        }



        if (HasPrimaryReferenceLink(tx) && tx.Type is not (

            NetworkWalletTransactionType.ServiceCharge or

            NetworkWalletTransactionType.Adjustment or

            NetworkWalletTransactionType.SubscriptionCollectedRevenue or

            NetworkWalletTransactionType.CollectionCommission))

        {

            return null;

        }



        return tx.Type switch

        {

            NetworkWalletTransactionType.Adjustment => notes,



            NetworkWalletTransactionType.ServiceCharge => FormatServiceChargeReference(notes, tx),



            NetworkWalletTransactionType.TopUp => FormatTopUpReference(notes, tx.NetworkTopUpRequestId),



            NetworkWalletTransactionType.SubscriptionCollectedRevenue or

            NetworkWalletTransactionType.CollectionCommission =>

                SimplifyCollectionNote(notes),



            NetworkWalletTransactionType.MaintenanceRevenue =>

                ExtractInvoiceReference(notes) ?? notes,



            NetworkWalletTransactionType.Refund => notes,



            _ => notes

        };

    }



    private static bool HasPrimaryReferenceLink(NetworkWalletTransaction tx) =>

        tx.NetworkTopUpRequestId.HasValue ||

        tx.RelatedPaymentTransactionId.HasValue ||

        tx.MaterialPurchaseInvoiceId.HasValue ||

        tx.MaterialSalesInvoiceId.HasValue;



    private static string DescribeServiceChargeReason(NetworkWalletTransaction tx)

    {

        string? notes = tx.Notes?.Trim();

        if (string.IsNullOrWhiteSpace(notes))

        {

            return "خصم رسوم خدمة من رصيد المحفظة";

        }



        if (notes.Contains("شبكة", StringComparison.OrdinalIgnoreCase) ||

            notes.Contains("Networks", StringComparison.OrdinalIgnoreCase))

        {

            string? networkName = ExtractAfterColon(notes);

            return string.IsNullOrWhiteSpace(networkName)

                ? "خصم رسوم إنشاء شبكة فرعية"

                : $"خصم رسوم إنشاء شبكة فرعية: {networkName}";

        }



        if (notes.Contains("سيرفر", StringComparison.OrdinalIgnoreCase) ||

            notes.Contains("MikroTik", StringComparison.OrdinalIgnoreCase))

        {

            string? serverName = ExtractAfterColon(notes);

            return string.IsNullOrWhiteSpace(serverName)

                ? "خصم رسوم إضافة خادم MikroTik"

                : $"خصم رسوم إضافة خادم MikroTik: {serverName}";

        }



        if (notes.Contains("مرسل", StringComparison.OrdinalIgnoreCase) ||

            notes.Contains("Sector", StringComparison.OrdinalIgnoreCase))

        {

            return "خصم رسوم خدمة المرسلات";

        }



        if (notes.Contains("تقرير", StringComparison.OrdinalIgnoreCase))

        {

            return "خصم رسوم توليد تقرير";

        }



        if (notes.Contains("خصم عنصر", StringComparison.OrdinalIgnoreCase) ||

            notes.Contains("FeatureKey", StringComparison.OrdinalIgnoreCase))

        {

            string itemLabel = GetFriendlyFeatureNameFromChargeNotes(notes) ?? "ميزة غير محددة";

            if (tx.NetworkServiceSubscriptionId is int subId)

            {

                return $"خصم رسوم اشتراك أو ميزة: {itemLabel} (اشتراك خدمة رقم {subId})";

            }



            return $"خصم رسوم ميزة في النظام: {itemLabel}";

        }



        if (notes.Contains("تجريبي", StringComparison.OrdinalIgnoreCase) ||
            notes.Contains("بدون اشتراك", StringComparison.OrdinalIgnoreCase))
        {
            return "خصم خدمة (سجل قديم)";
        }



        string headline = notes.Split('|', 2, StringSplitOptions.TrimEntries)[0];

        return $"خصم رسوم: {headline}";

    }



    private static string? GetFriendlyFeatureNameFromChargeNotes(string notes)

    {

        string? technicalPath = ExtractTechnicalChargePath(notes);

        if (string.IsNullOrWhiteSpace(technicalPath))

        {

            return null;

        }



        string[] parts = technicalPath.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)

        {

            return null;

        }



        string featureKey = parts[0];

        FeatureCatalog.FeatureDefinition? feature = FeatureCatalog.All.FirstOrDefault(f =>

            string.Equals(f.Key, featureKey, StringComparison.OrdinalIgnoreCase));

        return feature?.DisplayName ?? featureKey;

    }



    private static string? ExtractTechnicalChargePath(string notes)

    {

        int colon = notes.IndexOf(':', StringComparison.Ordinal);

        if (colon < 0 || colon >= notes.Length - 1)

        {

            return null;

        }



        string tail = notes[(colon + 1)..].Trim();

        int userRef = tail.IndexOf("U:", StringComparison.OrdinalIgnoreCase);

        if (userRef > 0)

        {

            tail = tail[..userRef].Trim().TrimEnd('/');

        }



        return string.IsNullOrWhiteSpace(tail) ? null : tail;

    }



    private static string? FormatServiceChargeReference(string notes, NetworkWalletTransaction tx)

    {

        if (notes.Contains("خصم عنصر", StringComparison.OrdinalIgnoreCase))

        {

            if (tx.NetworkServiceSubscriptionId.HasValue)

            {

                return null;

            }



            return GetFriendlyFeatureNameFromChargeNotes(notes);

        }



        if (notes.Contains('|'))

        {

            string[] segments = notes.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length > 1)

            {

                return string.Join(" — ", segments.Skip(1));

            }

        }



        if (notes.Contains("إنشاء شبكة", StringComparison.OrdinalIgnoreCase))

        {

            string? name = ExtractAfterColon(notes);

            return string.IsNullOrWhiteSpace(name) ? notes : $"الشبكة: {name}";

        }



        if (notes.Contains("إنشاء سيرفر", StringComparison.OrdinalIgnoreCase))

        {

            string? name = ExtractAfterColon(notes);

            return string.IsNullOrWhiteSpace(name) ? notes : $"الخادم: {name}";

        }



        if (notes.Contains("مرسل", StringComparison.OrdinalIgnoreCase) ||

            notes.Contains("Sector", StringComparison.OrdinalIgnoreCase))

        {

            return notes;

        }



        if (notes.Contains("توليد تقرير", StringComparison.OrdinalIgnoreCase))

        {

            return notes;

        }



        if (LooksLikeTechnicalPricingPath(notes))

        {

            return GetFriendlyFeatureNameFromChargeNotes(notes);

        }



        return notes;

    }



    private static bool LooksLikeTechnicalPricingPath(string notes)

    {

        string? path = ExtractTechnicalChargePath(notes);

        if (string.IsNullOrWhiteSpace(path))

        {

            return false;

        }



        string[] parts = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2;

    }



    private static string FormatTopUpReference(string notes, int? topUpRequestId)

    {

        if (topUpRequestId is int id)

        {

            return $"طلب تغذية رقم {id}";

        }



        Match m = RequestIdRegex.Match(notes);

        if (m.Success)

        {

            return $"طلب تغذية رقم {m.Groups[1].Value}";

        }



        if (notes.Contains("مرجع:", StringComparison.OrdinalIgnoreCase))

        {

            return notes;

        }



        return notes;

    }



    private static string SimplifyCollectionNote(string notes)

    {

        Match payment = PaymentIdRegex.Match(notes);

        string? paymentPart = payment.Success ? $"عملية دفع رقم {payment.Groups[1].Value}" : null;



        if (notes.Contains("عمولة تحصيل", StringComparison.OrdinalIgnoreCase))

        {

            int idx = notes.IndexOf('(');

            string feePart = idx >= 0 ? notes[..idx].Trim() : notes;

            return paymentPart == null ? feePart : $"{feePart} — {paymentPart}";

        }



        if (notes.Contains("إيراد تحصيل", StringComparison.OrdinalIgnoreCase))

        {

            return paymentPart ?? notes;

        }



        return paymentPart ?? notes;

    }



    private static string? ExtractInvoiceReference(string notes)

    {

        Match m = InvoiceIdRegex.Match(notes);

        return m.Success ? $"فاتورة رقم {m.Groups[1].Value}" : null;

    }



    private static string? ExtractAfterColon(string notes)

    {

        int idx = notes.IndexOf(':');

        if (idx < 0 || idx >= notes.Length - 1)

        {

            return null;

        }



        string tail = notes[(idx + 1)..].Trim();

        int paren = tail.IndexOf('(');

        if (paren > 0)

        {

            tail = tail[..paren].Trim();

        }



        return string.IsNullOrWhiteSpace(tail) ? null : tail;

    }

}


