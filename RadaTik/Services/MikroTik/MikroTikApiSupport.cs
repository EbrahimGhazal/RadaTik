using tik4net;

namespace RadaTik.Services.MikroTik;

/// <summary>دوال مساعدة مشتركة لقراءة استجابات MikroTik API.</summary>
public static class MikroTikApiSupport
{
    public static string GetSafeValue(ITikReSentence row, string key) =>
        row.Words.ContainsKey(key) ? row.Words[key] : string.Empty;

    public static string GenerateDefaultPassword() => Guid.NewGuid().ToString()[..8];

    /// <summary>
    /// RouterOS returns <c>!empty</c> when a filtered print matches nothing.
    /// tik4net throws instead of returning an empty list.
    /// </summary>
    public static bool IsEmptyResponse(Exception? ex)
    {
        Exception? current = ex;
        while (current != null)
        {
            if (IsEmptyResponse(current.Message))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    public static bool IsEmptyResponse(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("!empty", StringComparison.OrdinalIgnoreCase)
               || message.Contains("no such item", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// جلب سجل واحد بالاسم دون تنزيل القائمة كاملة من الجهاز.
    /// عدم وجود السجل يُرجع null ولا يُعتبر خطأ.
    /// </summary>
    public static ITikReSentence? FindByName(ITikConnection connection, string printPath, string name)
    {
        if (connection is null || string.IsNullOrWhiteSpace(printPath) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        ITikCommand cmd = connection.CreateCommand(printPath);
        cmd.AddParameter("?name", name);
        try
        {
            return cmd.ExecuteList().FirstOrDefault(row =>
                string.Equals(GetSafeValue(row, "name"), name, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (IsEmptyResponse(ex))
        {
            return null;
        }
    }

    public static bool IsAlreadyExistsMessage(Exception ex)
    {
        string text = ex.Message + " " + (ex.InnerException?.Message ?? string.Empty);
        return text.Contains("موجود مسبقاً", StringComparison.OrdinalIgnoreCase)
               || text.Contains("already exists", StringComparison.OrdinalIgnoreCase);
    }
}
