using Xunit;

namespace RadaTik.Tests.Views;

public sealed class CompanyClientPresenceViewTests
{
    [Fact]
    public void ClientSidebar_ShowsSocialFooterAndComplaintsWhenPresent()
    {
        string sidebar = Read("RadaTik", "Areas", "ClientPortal", "Views", "Shared", "_Sidebar.cshtml");
        Assert.Contains("صفحاتنا", sidebar);
        Assert.Contains("HasSocialLinks", sidebar);
        Assert.Contains("HasComplaintContacts", sidebar);
        Assert.Contains("asp-route-action=\"Complaints\"", sidebar);
        Assert.Contains("nav-section-complaints", sidebar);
        Assert.Contains("ماذا تريد؟", sidebar);
        Assert.DoesNotContain("طلبات الخدمة", sidebar);
        Assert.DoesNotContain("nav-sublist", sidebar);

        int complaints = sidebar.IndexOf("asp-route-action=\"Complaints\"", StringComparison.Ordinal);
        int social = sidebar.IndexOf("صفحاتنا", StringComparison.Ordinal);
        Assert.True(complaints > 0);
        Assert.True(social > complaints);
    }

    [Fact]
    public void ClientPortalSocial_HasLightThemeContrast()
    {
        string css = Read("RadaTik", "wwwroot", "css", "company-client-presence.css");
        Assert.Contains("[data-theme=\"light\"] .sidebar .client-portal-social__title", css);
        Assert.Contains("[data-theme=\"light\"] .sidebar .client-portal-social__link", css);
        Assert.Contains("color: #334155", css);
    }

    [Fact]
    public void CompanyAdmin_HasSocialAndComplaintsTabs()
    {
        string page = Read("RadaTik", "Areas", "CompanyAdmin", "Views", "ClientPresence", "Index.cshtml");
        Assert.Contains("السوشال ميديا", page);
        Assert.Contains("الشكاوى", page);
        Assert.Contains("AddSocial", page);
        Assert.Contains("AddComplaint", page);
        Assert.Contains("إظهار للمشترك", page);
        Assert.Contains("networkManager-client-presence", page);
    }

    [Fact]
    public void ClientComplaintsPage_UsesCompanyPhoneNumbers()
    {
        string page = Read("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "Complaints.cshtml");
        Assert.Contains("VisibleComplaintContacts", page);
        Assert.Contains("TelHref", page);
        Assert.Contains("CompanyName", page);
    }

    [Fact]
    public void CompanyAdminSidebar_LinksToClientPresence()
    {
        string nav = Read("RadaTik", "Navigation", "CompanySidebarCatalog.cs");
        Assert.Contains("networkManager-client-presence", nav);
        Assert.Contains("صفحات التواصل", nav);
        Assert.Contains("ClientPresence", nav);
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

        throw new FileNotFoundException("لم يتم العثور على الملف: " + Path.Combine(relativeParts));
    }
}
