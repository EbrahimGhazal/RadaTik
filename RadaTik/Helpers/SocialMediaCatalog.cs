using System.Text.RegularExpressions;
using RadaTik.Models;

namespace RadaTik.Helpers;

public sealed record SocialMediaPlatformInfo(
    SocialMediaPlatform Platform,
    string ArabicName,
    string IconClass,
    string BrandColor);

public static class SocialMediaCatalog
{
    private static readonly Regex PhoneChars = new(@"[^\d+]", RegexOptions.Compiled);

    public static IReadOnlyList<SocialMediaPlatformInfo> All { get; } =
    [
        new(SocialMediaPlatform.Facebook, "فيسبوك", "fab fa-facebook", "#1877F2"),
        new(SocialMediaPlatform.Instagram, "إنستغرام", "fab fa-instagram", "#E1306C"),
        new(SocialMediaPlatform.Twitter, "إكس", "fab fa-x-twitter", "#111111"),
        new(SocialMediaPlatform.YouTube, "يوتيوب", "fab fa-youtube", "#FF0000"),
        new(SocialMediaPlatform.TikTok, "تيك توك", "fab fa-tiktok", "#25F4EE"),
        new(SocialMediaPlatform.WhatsApp, "واتساب", "fab fa-whatsapp", "#25D366"),
        new(SocialMediaPlatform.Telegram, "تيليغرام", "fab fa-telegram", "#229ED9"),
        new(SocialMediaPlatform.LinkedIn, "لينكدإن", "fab fa-linkedin", "#0A66C2"),
        new(SocialMediaPlatform.Snapchat, "سناب شات", "fab fa-snapchat", "#FFFC00"),
        new(SocialMediaPlatform.Website, "موقع إلكتروني", "fas fa-globe", "#2563EB"),
        new(SocialMediaPlatform.Other, "أخرى", "fas fa-share-nodes", "#64748B"),
    ];

    public static SocialMediaPlatformInfo Info(SocialMediaPlatform platform) =>
        All.FirstOrDefault(x => x.Platform == platform) ?? All[^1];

    public static string DefaultDisplayName(SocialMediaPlatform platform) => Info(platform).ArabicName;

    public static bool TryNormalizeHttpUrl(string? raw, out string url, out string? error)
    {
        url = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "الرابط مطلوب.";
            return false;
        }

        string trimmed = raw.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "الرابط يجب أن يبدأ بـ http أو https.";
            return false;
        }

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            error = "لا يمكن استخدام عنوان محلي.";
            return false;
        }

        url = uri.ToString();
        return true;
    }

    public static string NormalizeSocialUrl(SocialMediaPlatform platform, string? raw)
    {
        string value = (raw ?? string.Empty).Trim();
        if (platform == SocialMediaPlatform.WhatsApp && LooksLikePhone(value))
        {
            string digits = new string(value.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(digits) ? value : "https://wa.me/" + digits;
        }

        if (platform == SocialMediaPlatform.Telegram && LooksLikeHandle(value))
        {
            return "https://t.me/" + value.TrimStart('@');
        }

        return value;
    }

    public static bool TryNormalizePhone(string? raw, out string phone, out string? error)
    {
        phone = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "رقم الهاتف مطلوب.";
            return false;
        }

        string compact = PhoneChars.Replace(raw.Trim(), string.Empty);
        int digits = compact.Count(char.IsDigit);
        if (digits < 8 || digits > 15)
        {
            error = "أدخل رقماً صالحاً بين 8 و 15 خانة.";
            return false;
        }

        phone = compact;
        return true;
    }

    public static string TelHref(string phone) => "tel:" + phone;

    private static bool LooksLikePhone(string value) =>
        value.Any(char.IsDigit) && !value.Contains("://", StringComparison.Ordinal) && !value.Contains('.', StringComparison.Ordinal);

    private static bool LooksLikeHandle(string value) =>
        value.StartsWith('@') || (!value.Contains("://", StringComparison.Ordinal) && !value.Contains('.', StringComparison.Ordinal));
}
