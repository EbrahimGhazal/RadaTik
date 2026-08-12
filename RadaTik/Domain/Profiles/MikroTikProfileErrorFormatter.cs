namespace RadaTik.Domain.Profiles;

/// <summary>تنسيق رسائل أخطاء اتصال MikroTik لعرضها للمستخدم.</summary>
public static class MikroTikProfileErrorFormatter
{
    public static string Format(string prefix, Exception ex) =>
        Sanitize(ex.Message, prefix);

    public static string Sanitize(string? rawMessage, string prefix)
    {
        string message = (rawMessage ?? string.Empty).Trim();
        if (message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("transport connection", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("connection reset", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("socket", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"{prefix}: تعذر الاتصال بخادم MikroTik لأن الاتصال انقطع. " +
                "تحقق من صحة Host/Port وتفعيل API أو API-SSL والسماح بالاتصال عبر الجدار الناري.";
        }

        return string.IsNullOrWhiteSpace(message) ? prefix : $"{prefix}: {message}";
    }
}
