namespace RadaTik.Helpers;

/// <summary>رسائل أخطاء MikroTik مفهومة للمستخدم.</summary>
public static class MikroTikErrorFormatter
{
    public static string Format(string prefix, Exception? ex)
    {
        string? raw = Flatten(ex);
        return Format(prefix, raw);
    }

    public static string Format(string prefix, string? rawMessage)
    {
        string message = (rawMessage ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return prefix;
        }

        if (IsEmptyResponse(message))
        {
            return $"{prefix}: المستخدم غير موجود على المايكروتك أو الجهاز أرجع رداً فارغاً.";
        }

        if (IsAuthFailure(message))
        {
            return
                $"{prefix}: فشل تسجيل الدخول إلى MikroTik — تحقق من اسم المستخدم وكلمة المرور " +
                "وصلاحيات API للمستخدم في إعدادات الخادم.";
        }

        if (IsConnectionFailure(message))
        {
            return
                $"{prefix}: تعذر الاتصال بخادم MikroTik لأن الاتصال انقطع. " +
                "تحقق من صحة Host/Port وتفعيل API أو API-SSL والسماح بالاتصال عبر الجدار الناري.";
        }

        if (IsTimeout(message))
        {
            return
                $"{prefix}: انتهت مهلة الاتصال بخادم MikroTik — تحقق من الشبكة بين RadaTik والجهاز.";
        }

        return $"{prefix}: {message}";
    }

    public static bool IsAuthFailure(Exception? ex) => IsAuthFailure(Flatten(ex));

    public static bool IsEmptyResponse(string? message)
    {
        string text = message ?? string.Empty;
        return text.Contains("!empty", StringComparison.OrdinalIgnoreCase)
            || text.Contains("no such item", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAuthFailure(string? message)
    {
        string text = message ?? string.Empty;
        if (IsEmptyResponse(text))
        {
            return false;
        }

        return text.Contains("invalid user name or password", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cannot log in", StringComparison.OrdinalIgnoreCase)
            || (text.Contains("password", StringComparison.OrdinalIgnoreCase)
                && text.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            || text.Contains("login failure", StringComparison.OrdinalIgnoreCase)
            || text.Contains("authentication failed", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not logged in", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsConnectionFailure(string? message)
    {
        string text = message ?? string.Empty;
        return text.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
            || text.Contains("transport connection", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase)
            || text.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
            || text.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cannot connect", StringComparison.OrdinalIgnoreCase)
            || text.Contains("No connection", StringComparison.OrdinalIgnoreCase)
            || text.Contains("socket", StringComparison.OrdinalIgnoreCase)
            || text.Contains("فشل الاتصال", StringComparison.OrdinalIgnoreCase)
            || text.Contains("تعذر الاتصال", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTimeout(string? message)
    {
        string text = message ?? string.Empty;
        return text.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || text.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || text.Contains("انتهت مهلة", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnreachable(Exception? ex) => IsUnreachable(Flatten(ex));

    public static bool IsUnreachable(string? message) =>
        IsConnectionFailure(message) || IsTimeout(message);

    private static string Flatten(Exception? ex)
    {
        if (ex == null)
        {
            return string.Empty;
        }

        return string.Join(' ', Enumerate(ex).Select(e => e.Message)).Trim();
    }

    private static IEnumerable<Exception> Enumerate(Exception ex)
    {
        Exception? current = ex;
        while (current != null)
        {
            yield return current;
            current = current.InnerException;
        }
    }
}
