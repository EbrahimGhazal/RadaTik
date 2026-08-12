using tik4net;

namespace RadaTik.Services.MikroTik;

/// <summary>دوال مساعدة مشتركة لقراءة استجابات MikroTik API.</summary>
public static class MikroTikApiSupport
{
    public static string GetSafeValue(ITikReSentence row, string key) =>
        row.Words.ContainsKey(key) ? row.Words[key] : string.Empty;

    public static string GenerateDefaultPassword() => Guid.NewGuid().ToString()[..8];
}
