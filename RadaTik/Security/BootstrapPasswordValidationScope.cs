namespace RadaTik.Security;

/// <summary>
/// يسمح مؤقتاً بكلمة المرور الافتراضية لمدير النظام عند البذر الأولي فقط (قبل أول تسجيل دخول).
/// </summary>
public static class BootstrapPasswordValidationScope
{
    private static readonly AsyncLocal<int> Depth = new();

    public static bool IsActive => Depth.Value > 0;

    public static IDisposable Enter()
    {
        Depth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
            if (Depth.Value > 0)
            {
                Depth.Value--;
            }
        }
    }
}
