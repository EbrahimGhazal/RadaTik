using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ClientPortalAvailablePlansMobileViewTests
{
    [Fact]
    public void AvailablePlans_UsesCompactPlanCards()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "AvailablePlans.cshtml"));
        Assert.Contains("client-portal-plans-page", view);
        Assert.Contains("client-portal-plans-cards.css", view);
        Assert.Contains("plan-card__badges", view);
        Assert.Contains("plan-card__intro", view);
        Assert.Contains("ل.س.ج", view);
        Assert.Contains("open-change-plan-modal", view);
        Assert.Contains("CreateSpeedChangeRequest", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
        Assert.DoesNotContain("ل.س.جديدة", view);
        Assert.DoesNotContain("plan-ribbon", view);
    }

    [Fact]
    public void PlansCardsCss_ClampsToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "client-portal-plans-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("plan-pill--featured", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف الباقات المتاحة: " + Path.Combine(relativeParts));
    }
}
