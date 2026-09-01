using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ClientPortalRenewSubscriptionMobileViewTests
{
    [Fact]
    public void RenewSubscription_FitsViewportOnCompactScreens()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "RenewSubscription.cshtml"));
        Assert.Contains("client-portal-renew-page", view);
        Assert.Contains("client-portal-renew-cards.css", view);
        Assert.Contains("renew-wallet-hero", view);
        Assert.Contains("radtk-data-table--cards", view);
        Assert.Contains("data-label=\"الاشتراك\"", view);
        Assert.Contains("data-label=\"المبلغ المستحق\"", view);
        Assert.Contains("data-label=\"الإجراء\"", view);
        Assert.Contains("SelfRenewSubscription", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
    }

    [Fact]
    public void RenewCardsCss_ClampsToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "client-portal-renew-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("content: attr(data-label)", css);
        Assert.Contains("visibility: visible !important", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف تجديد الاشتراك: " + Path.Combine(relativeParts));
    }
}
