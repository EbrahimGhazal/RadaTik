namespace RadaTik.Constants;

public static class AppMessages
{
    public const string InsufficientBalance = "لايوجد رصيد كاف الرجاء تعبئة المحفظة";
    public const string PricingNotConfigured = "إعدادات التسعير غير مكتملة. يرجى مراجعة الأسعار والتجديد من مدير النظام.";

    /// <summary>تسعير خدمة الشبكات (إنشاء + تجديد) غير مضبوط في كتالوج خدمات مدير النظام.</summary>
    public const string NetworkPricingNotConfigured =
        "إعدادات تسعير الشبكات غير مكتملة. يجب على مدير النظام ضبط سعر الإنشاء (مرة واحدة) وسعر التجديد الدوري لخدمة «إدارة الشبكة» في كتالوج الخدمات ثم حفظ التغييرات.";
    public const string OperationSuccess = "تم تنفيذ العملية بنجاح";
    public const string SaveFailed = "حدث خطأ أثناء حفظ البيانات. الرجاء المحاولة مرة أخرى.";
    public const string UnexpectedError = "حدث خطأ غير متوقع. الرجاء المحاولة مرة أخرى أو الاتصال بالدعم الفني.";
    public const string SelectNetworkFirst = "يرجى تحديد شبكة أولاً";
    public const string RequestNotFound = "الطلب غير موجود";
    public const string InvalidRequest = "طلب غير صالح";
    public const string PasswordMinLength = "كلمة المرور يجب أن تكون 6 أحرف على الأقل";
    public const string MustSpecifyRejectionReason = "يجب تحديد سبب الرفض";
    public const string CurrentNetworkNotFound = "تعذر العثور على الشبكة الحالية.";
}
