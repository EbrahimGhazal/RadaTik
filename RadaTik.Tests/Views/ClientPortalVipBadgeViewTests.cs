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
    public void MyProfile_ShowsReadOnlyVipBanner()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "MyProfile.cshtml"));
        Assert.Contains("Model.IsVip", text);
        Assert.Contains("clients-vip-badge", text);
        Assert.Contains("client-vip-profile-banner", text);
        Assert.DoesNotContain("asp-for=\"IsVip\"", text);
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
