using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ProfileIndexMobileViewTests
{
    [Fact]
    public void CompanyAdminProfileIndex_RemovesOldCurrencyAndKeepsMobileActions()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "Profile", "Index.cshtml"));
        Assert.Contains("radtk-page-profile-index", view);
        Assert.Contains("profile-index-cards.css", view);
        Assert.Contains("ل.س.ج", view);
        Assert.Contains("action-btn__text", view);
        Assert.Contains("profile-select-hit", view);
        Assert.Contains("initProfilesTableIfDesktop", view);
        Assert.Contains("max-width: 1199.98px", view);
        Assert.Contains("responsive: false", view);
        Assert.DoesNotContain("price-old-hint", view);
        Assert.DoesNotContain("ل.س جديد", view);
        Assert.DoesNotContain("قديم:", view);
    }

    [Fact]
    public void ProfileIndexCardsCss_ShowsActionsAndClampsCardToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "profile-index-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow: visible !important", css);
        Assert.Contains("td.radtk-col-actions", css);
        Assert.Contains("visibility: visible !important", css);
        Assert.Contains("grid-template-columns: minmax(0, 1fr) 2.5rem", css);
        Assert.Contains(".action-btn__text", css);
        Assert.Contains("price-old-hint", css);
        Assert.Contains("display: none !important", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف صفحة البروفايلات: " + Path.Combine(relativeParts));
    }
}
