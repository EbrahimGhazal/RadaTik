using tik4net;

namespace RadaTik.Services.MikroTik;

/// <summary>دوال مساعدة مشتركة لقراءة استجابات MikroTik API.</summary>
public static class MikroTikApiSupport
{
    public static string GetSafeValue(ITikReSentence row, string key) =>
        row.Words.ContainsKey(key) ? row.Words[key] : string.Empty;

    public static string GenerateDefaultPassword() => Guid.NewGuid().ToString()[..8];

    /// <summary>
    /// جلب سجل واحد بالاسم دون تنزيل القائمة كاملة من الجهاز.
    /// </summary>
    public static ITikReSentence? FindByName(ITikConnection connection, string printPath, string name)
    {
        if (connection is null || string.IsNullOrWhiteSpace(printPath) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        ITikCommand cmd = connection.CreateCommand(printPath);
        cmd.AddParameter("?name", name);
        return cmd.ExecuteList().FirstOrDefault(row =>
            string.Equals(GetSafeValue(row, "name"), name, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAlreadyExistsMessage(Exception ex)
    {
        string text = ex.Message + " " + (ex.InnerException?.Message ?? string.Empty);
        return text.Contains("موجود مسبقاً", StringComparison.OrdinalIgnoreCase)
               || text.Contains("already exists", StringComparison.OrdinalIgnoreCase);
    }
}
