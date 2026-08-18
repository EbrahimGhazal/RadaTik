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

    public static bool NamesMatch(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        string a = left.Trim();
        string b = right.Trim();
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(CompactName(a), CompactName(b), StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ITikReSentence> PrintList(ITikConnection connection, string printPath)
    {
        if (connection is null || string.IsNullOrWhiteSpace(printPath))
        {
            return [];
        }

        ITikCommand cmd = connection.CreateCommand(printPath);
        try
        {
            return cmd.ExecuteList().ToList();
        }
        catch (Exception ex) when (IsEmptyResponse(ex))
        {
            return [];
        }
    }

    public static ITikReSentence? FindInPrint(IEnumerable<ITikReSentence> rows, string name) =>
        rows.FirstOrDefault(row => NamesMatch(GetSafeValue(row, "name"), name));

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
        cmd.AddParameter("?name", name.Trim());
        try
        {
            ITikReSentence? filtered = FindInPrint(cmd.ExecuteList(), name);
            if (filtered is not null)
            {
                return filtered;
            }
        }
        catch (Exception ex) when (IsEmptyResponse(ex))
        {
            // RouterOS returns !empty when the filtered print matches nothing.
        }

        return null;
    }

    /// <summary>
    /// بروفايلات PPP قليلة؛ نقرأ القائمة كاملة لأن استعلام ?name= يفشل أحياناً رغم وجود البروفايل.
    /// </summary>
    public static ITikReSentence? FindProfileByName(ITikConnection connection, string name) =>
        FindInPrint(PrintList(connection, "/ppp/profile/print"), name);

    public static string? ActualName(ITikReSentence? row)
    {
        if (row is null)
        {
            return null;
        }

        string value = GetSafeValue(row, "name").Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string CompactName(string value) =>
        string.Concat(value.Where(c => !char.IsWhiteSpace(c)));

    public static bool IsAlreadyExistsMessage(Exception ex)
    {
        string text = ex.Message + " " + (ex.InnerException?.Message ?? string.Empty);
        return text.Contains("موجود مسبقاً", StringComparison.OrdinalIgnoreCase)
               || text.Contains("already exists", StringComparison.OrdinalIgnoreCase);
    }
}
