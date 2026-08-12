using System.ComponentModel.DataAnnotations;

namespace RadaTik.ViewModels.CompanyAdmin;

public class RenewalRemindersPageViewModel
{
    public int EffectiveCompanyNetworkId { get; set; }
    public string? EffectiveCompanyNetworkName { get; set; }

    public bool IsEnabled { get; set; }

    public bool RemindDaysBefore5 { get; set; } = true;
    public bool RemindDaysBefore4 { get; set; } = true;
    public bool RemindDaysBefore3 { get; set; } = true;

    [Display(Name = "نص الرسالة")]
    [StringLength(4000)]
    public string MessageTemplate { get; set; } =
        "مرحباً {Name}، تذكير: اشتراكك ({Profile}) ينتهي بعد {Days} أيام بتاريخ {ExpiryDate}. المبلغ: {Amount} ل.س. يرجى تجديد الاشتراك.";

    public bool SendWhatsApp { get; set; }

    [Display(Name = "رقم واتساب (للعرض والتأكيد)")]
    [StringLength(32)]
    public string? WhatsAppDisplayNumber { get; set; }

    public DateTime? WhatsAppVerifiedAt { get; set; }

    [Display(Name = "عنوان واجهة إرسال واتساب (POST JSON)")]
    [StringLength(1000)]
    public string? WhatsAppApiUrl { get; set; }

    [Display(Name = "ترويسة Authorization (اختياري)")]
    [StringLength(500)]
    public string? WhatsAppApiAuthorizationHeader { get; set; }

    [Display(Name = "قالب جسم الطلب JSON (اختياري)")]
    [StringLength(4000)]
    public string? WhatsAppApiBodyTemplate { get; set; }

    [Display(Name = "رقم لاختبار واتساب (أرقام فقط)")]
    [StringLength(32)]
    public string? WhatsAppTestPhone { get; set; }

    public bool SendTelegram { get; set; }

    [Display(Name = "رمز بوت تلغرام")]
    [StringLength(256)]
    public string? TelegramBotToken { get; set; }

    public DateTime? TelegramVerifiedAt { get; set; }

    [Display(Name = "معرّف محادثة للاختبار")]
    [StringLength(64)]
    public string? TelegramTestChatId { get; set; }
}
