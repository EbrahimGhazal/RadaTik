using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>
/// إعدادات تذكير المشتركين بتجديد الاشتراك قبل انتهاء الصلاحية — لكل شبكة شركة (الشبكة الرئيسية).
/// </summary>
public class NetworkClientRenewalReminderSettings
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int NetworkId { get; set; }

    [ForeignKey(nameof(NetworkId))]
    public virtual Network Network { get; set; } = null!;

    [Display(Name = "تفعيل التذكيرات")]
    public bool IsEnabled { get; set; }

    [Display(Name = "تذكير قبل 5 أيام")]
    public bool RemindDaysBefore5 { get; set; } = true;

    [Display(Name = "تذكير قبل 4 أيام")]
    public bool RemindDaysBefore4 { get; set; } = true;

    [Display(Name = "تذكير قبل 3 أيام")]
    public bool RemindDaysBefore3 { get; set; } = true;

    /// <summary>نص الرسالة؛ متغيرات: {Name} {Profile} {Amount} {Days} {ExpiryDate}</summary>
    [Display(Name = "نص الرسالة")]
    [StringLength(4000)]
    public string MessageTemplate { get; set; } =
        "مرحباً {Name}، تذكير: اشتراكك ({Profile}) ينتهي بعد {Days} أيام بتاريخ {ExpiryDate}. المبلغ: {Amount} ل.س. يرجى تجديد الاشتراك.";

    [Display(Name = "إرسال واتساب")]
    public bool SendWhatsApp { get; set; }

    /// <summary>رقم واتساب الشركة (للعرض والتأكيد اليدوي).</summary>
    [Display(Name = "رقم واتساب")]
    [StringLength(32)]
    public string? WhatsAppDisplayNumber { get; set; }

    public DateTime? WhatsAppVerifiedAt { get; set; }

    /// <summary>عنوان POST لبوابة إرسال واتساب (JSON: phone, message) إن وُجد.</summary>
    [StringLength(1000)]
    public string? WhatsAppApiUrl { get; set; }

    /// <summary>قيمة ترويسة Authorization اختيارية (مثلاً Bearer token كاملاً).</summary>
    [StringLength(500)]
    public string? WhatsAppApiAuthorizationHeader { get; set; }

    /// <summary>
    /// قالب جسم الطلب JSON. إن وُجد: يُستبدل {phone} برقم المشترك، و{message} بقيمة JSON مُرمّزة للنص (مثال: {"to":"{phone}","text":{message}}).
    /// إن كان فارغاً يُستخدم الافتراضي {"phone":"...","message":"..."}.
    /// </summary>
    [Display(Name = "قالب جسم الطلب (JSON اختياري)")]
    [StringLength(4000)]
    public string? WhatsAppApiBodyTemplate { get; set; }

    [Display(Name = "إرسال تلغرام")]
    public bool SendTelegram { get; set; }

    [StringLength(256)]
    public string? TelegramBotToken { get; set; }

    public DateTime? TelegramVerifiedAt { get; set; }

    /// <summary>معرّف محادثة لاختبار البوت (اختياري).</summary>
    [StringLength(64)]
    public string? TelegramTestChatId { get; set; }

    [StringLength(32)]
    public string? WhatsAppTestPhone { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
