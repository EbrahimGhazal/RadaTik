using System.Globalization;
using System.Text.RegularExpressions;
using tik4net;

namespace RadaTik.Services.MikroTik;

/// <summary>دوال مساعدة مشتركة لقراءة استجابات MikroTik API.</summary>
public static class MikroTikApiSupport
{
    public static string GetSafeValue(ITikReSentence row, string key)
    {
        if (row?.Words is null || string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (row.Words.ContainsKey(key))
        {
            string? direct = row.Words[key];
            if (!string.IsNullOrEmpty(direct))
            {
                return direct;
            }
        }

        foreach (KeyValuePair<string, string> pair in row.Words)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pair.Key.TrimStart('.'), key.TrimStart('.'), StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value ?? string.Empty;
            }
        }

        return string.Empty;
    }

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
        rows.FirstOrDefault(row => ProfileIdentityMatch(name, GetSafeValue(row, "name"), GetSafeValue(row, "rate-limit")));

    public static bool ProfileIdentityMatch(string wantedName, string actualName, string? rateLimit = null)
    {
        if (NamesMatch(wantedName, actualName))
        {
            return true;
        }

        decimal? wantedMbps = ParseSpeedMbps(wantedName);
        if (wantedMbps is null)
        {
            return false;
        }

        decimal? fromName = ParseSpeedMbps(actualName);
        if (fromName is not null && SpeedsEqual(wantedMbps.Value, fromName.Value))
        {
            return true;
        }

        decimal? fromRate = ParseSpeedMbps(rateLimit);
        return fromRate is not null && SpeedsEqual(wantedMbps.Value, fromRate.Value);
    }

    public static decimal? ParseSpeedMbps(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Match match = Regex.Match(
            text,
            @"(\d+(?:\.\d+)?)\s*(gbps|mbps|kbps|[gmk])\b",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
        {
            return null;
        }

        string unit = match.Groups[2].Value.ToLowerInvariant();
        return unit switch
        {
            "g" or "gbps" => value * 1000m,
            "k" or "kbps" => value / 1000m,
            _ => value
        };
    }

    public static IReadOnlyList<ITikReSentence> PrintPppProfiles(ITikConnection connection)
    {
        IReadOnlyList<ITikReSentence> rows = PrintList(connection, "/ppp/profile/print");
        return rows.Count > 0 ? rows : PrintList(connection, "/ppp/profile");
    }

    /// <summary>
    /// بروفايلات PPP قليلة؛ نقرأ القائمة كاملة ونطابق بالاسم أو السرعة.
    /// </summary>
    public static ITikReSentence? FindProfileByName(ITikConnection connection, string name) =>
        FindInPrint(PrintPppProfiles(connection), name);

    public static string? ActualName(ITikReSentence? row)
    {
        if (row is null)
        {
            return null;
        }

        string value = GetSafeValue(row, "name").Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool SpeedsEqual(decimal left, decimal right) => Math.Abs(left - right) < 0.05m;

    private static string CompactName(string value) =>
        string.Concat(value.Where(c => !char.IsWhiteSpace(c)));

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

    public static bool IsAlreadyExistsMessage(Exception ex)
    {
        string text = ex.Message + " " + (ex.InnerException?.Message ?? string.Empty);
        return text.Contains("موجود مسبقاً", StringComparison.OrdinalIgnoreCase)
               || text.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || text.Contains("already have such", StringComparison.OrdinalIgnoreCase)
               || text.Contains("failure: already", StringComparison.OrdinalIgnoreCase);
    }
}
