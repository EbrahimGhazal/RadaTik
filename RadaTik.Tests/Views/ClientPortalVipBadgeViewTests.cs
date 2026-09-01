using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ClientPortalVipBadgeViewTests
{
    [Fact]
    public void HeaderDropdown_ShowsClientVipBadge()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_HeaderUserDropdown.cshtml"));
        Assert.Contains("Layout_IsClientVip", text);
        Assert.Contains("clients-vip-badge", text);
        Assert.Contains("header-vip-badge", text);
    }

    [Fact]
    public void ShellLayout_LoadsClientVipForHeader()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_Shell.cshtml"));
        Assert.Contains("CurrentClientVipLookup", text);
        Assert.Contains("Layout_IsClientVip", text);
    }

    [Fact]
    public void MyProfile_ShowsPersonalFieldsAndLocksMikroTikIdentity()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "MyProfile.cshtml"));
        Assert.Contains("asp-for=\"Email\"", text);
        Assert.Contains("كلمة مرور النظام", text);
        Assert.Contains("asp-action=\"ChangePassword\"", text);
        Assert.Contains("MikroTikUserName", text);
        Assert.Contains("ممنوع تغيير اسم المستخدم أو كلمة المرور على سيرفر MikroTik", text);
        Assert.DoesNotContain("asp-for=\"Password\"", text);
    }

    [Fact]
    public void MyProfile_ShowsReadOnlyVipBanner()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "MyProfile.cshtml"));
        Assert.Contains("Model.IsVip", text);
        Assert.Contains("clients-vip-badge", text);
        Assert.Contains("client-vip-profile-banner", text);
        Assert.DoesNotContain("asp-for=\"IsVip\"", text);
        Assert.Contains("_ClientNationalIdCard", text);
        Assert.Contains("NationalIdFrontPath", text);
        Assert.Contains("الوجه الأمامي", File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_ClientNationalIdCard.cshtml")));
        Assert.Contains("الوجه الخلفي", File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_ClientNationalIdCard.cshtml")));
    }

    [Fact]
    public void ClientDashboard_ShowsVipChip()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "Index.cshtml"));
        Assert.Contains("Model.IsVip", text);
        Assert.Contains("VIP — مشترك مميز", text);
        Assert.Contains("clients-vip-badge", text);
    }

    private static string FindFile(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException("لم يتم العثور على الملف: " + Path.Combine(relativeParts));
    }
}
