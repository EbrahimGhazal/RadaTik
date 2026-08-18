using RadaTik.Helpers;
using Xunit;

namespace RadaTik.Tests.Helpers;

public class MikroTikErrorFormatterTests
{
    [Fact]
    public void Format_AuthFailure_ReturnsArabicCredentialHint()
    {
        var inner = new InvalidOperationException("invalid user name or password (6)");
        var outer = new InvalidOperationException("فشل الاتصال بالخادم id-37.hostddns.us بعد 3 محاولات", inner);

        string message = MikroTikErrorFormatter.Format("تعذر الاتصال بالسيرفر", outer);

        Assert.Contains("فشل تسجيل الدخول", message);
        Assert.Contains("اسم المستخدم وكلمة المرور", message);
    }

    [Fact]
    public void IsUnreachable_DetectsArabicConnectionFailure()
    {
        var ex = new InvalidOperationException("فشل الاتصال بالخادم 10.0.0.1 بعد 3 محاولات");
        Assert.True(MikroTikErrorFormatter.IsUnreachable(ex));
    }

    [Fact]
    public void IsAuthFailure_DetectsTikTrapMessage()
    {
        Assert.True(MikroTikErrorFormatter.IsAuthFailure("invalid user name or password (6)"));
    }
}
