using System.Globalization;

namespace RadaTik.Services;

public static class RenewalReminderMessageFormatter
{
    /// <summary>
    /// متغيرات مدعومة: {Name} {Profile} {Amount} {Days} {ExpiryDate}
    /// </summary>
    public static string Format(
        string template,
        string? subscriberName,
        string? profileName,
        decimal amountWithVat,
        int daysBeforeExpiry,
        DateTime expirationDate)
    {
        var name = subscriberName?.Trim() ?? "";
        var profile = profileName?.Trim() ?? "";
        var amountStr = amountWithVat.ToString("N0", CultureInfo.InvariantCulture);
        var daysStr = daysBeforeExpiry.ToString(CultureInfo.InvariantCulture);
        var expiryStr = expirationDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

        return template
            .Replace("{Name}", name, StringComparison.Ordinal)
            .Replace("{Profile}", profile, StringComparison.Ordinal)
            .Replace("{Amount}", amountStr, StringComparison.Ordinal)
            .Replace("{Days}", daysStr, StringComparison.Ordinal)
            .Replace("{ExpiryDate}", expiryStr, StringComparison.Ordinal);
    }
}
