using Xunit;

namespace RadaTik.Tests.Views;

public sealed class PublicSiteStatsViewTests
{
    [Fact]
    public void HomePage_ShowsVisitorCountClearly()
    {
        string view = Read("RadaTik", "Areas", "RadaTik", "Views", "Public", "Index.cshtml");
        Assert.Contains("عدد زوار الموقع", view);
        Assert.Contains("SiteVisitors", view);
        Assert.Contains("rt-stat--visitors", view);
    }

    [Fact]
    public void PublicLayout_ShowsVisitorCountInFooter()
    {
        string layout = Read("RadaTik", "Areas", "RadaTik", "Views", "Shared", "_PublicLayout.cshtml");
        Assert.Contains("عدد زوار الموقع", layout);
        Assert.Contains("rt-visitor-count", layout);
    }

    [Fact]
    public void LoginView_LocksNativeAppToAssignedRole()
    {
        string view = Read("RadaTik", "Views", "Account", "Login.cshtml");
        Assert.Contains("asp-for=\"AppRole\"", view);
        Assert.Contains("الدخول مسموح لهذا الدور فقط", view);
        string controller = Read("RadaTik", "Controllers", "AccountController.cs");
        Assert.Contains("NativeAppContext.IsRoleAllowed", controller);
        Assert.Contains("DeniedMessage", controller);
        Assert.Contains("persistSession", controller);
        Assert.Contains("سيبقى حسابك مسجلاً على هذا التطبيق", view);
        Assert.Contains("NativeAppContext.Company", view);
        Assert.Contains("إنشاء حساب مدير شركة", view);
        Assert.Contains("إنشاء حساب نقطة تحصيل", view);
    }

    private static string Read(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }
}
