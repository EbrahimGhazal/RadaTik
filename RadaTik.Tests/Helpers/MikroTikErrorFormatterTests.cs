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
    public void Format_EmptyTikResponse_IsNotTreatedAsLoginFailure()
    {
        var inner = new InvalidOperationException("Response type '!empty' not supported");
        var outer = new InvalidOperationException("خطأ في إضافة المستخدم في المايكروتك", inner);

        string message = MikroTikErrorFormatter.Format("خطأ في المزامنة مع المايكروتك", outer);

        Assert.DoesNotContain("فشل تسجيل الدخول", message);
        Assert.Contains("فارغاً", message);
    }

    [Fact]
    public void IsAuthFailure_DoesNotMatchEmptyResponse()
    {
        Assert.False(MikroTikErrorFormatter.IsAuthFailure("Response type '!empty' not supported"));
    }

    [Fact]
    public void IsAuthFailure_DetectsTikTrapMessage()
    {
        Assert.True(MikroTikErrorFormatter.IsAuthFailure("invalid user name or password (6)"));
    }
}
