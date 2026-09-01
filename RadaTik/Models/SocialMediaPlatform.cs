using System.ComponentModel.DataAnnotations;

namespace RadaTik.Models;

public enum SocialMediaPlatform
{
    [Display(Name = "فيسبوك")]
    Facebook = 0,

    [Display(Name = "إنستغرام")]
    Instagram = 1,

    [Display(Name = "إكس")]
    Twitter = 2,

    [Display(Name = "يوتيوب")]
    YouTube = 3,

    [Display(Name = "تيك توك")]
    TikTok = 4,

    [Display(Name = "واتساب")]
    WhatsApp = 5,

    [Display(Name = "تيليغرام")]
    Telegram = 6,

    [Display(Name = "لينكدإن")]
    LinkedIn = 7,

    [Display(Name = "سناب شات")]
    Snapchat = 8,

    [Display(Name = "موقع إلكتروني")]
    Website = 9,

    [Display(Name = "أخرى")]
    Other = 10
}
