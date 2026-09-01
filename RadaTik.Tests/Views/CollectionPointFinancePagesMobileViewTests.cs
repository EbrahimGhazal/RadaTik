using Xunit;

namespace RadaTik.Tests.Views;

public sealed class CollectionPointFinancePagesMobileViewTests
{
    [Fact]
    public void WalletTopUp_UsesFinanceCardsAndNav()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CollectionPoint", "Views", "Wallet", "TopUp.cshtml"));
        Assert.Contains("collection-point-finance-page", view);
        Assert.Contains("_CollectionPointFinanceNav", view);
        Assert.Contains("collection-point-finance-cards.css", view);
        Assert.Contains("cp-finance-field", view);
        Assert.Contains("ل.س.ج", view);
    }

    [Fact]
    public void CashBox_FitsViewportOnCompactScreens()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CollectionPoint", "Views", "CashBox", "Index.cshtml"));
        Assert.Contains("collection-point-finance-page", view);
        Assert.Contains("_CollectionPointFinanceNav", view);
        Assert.Contains("data-label=\"التاريخ\"", view);
        Assert.Contains("data-label=\"المبلغ\"", view);
        Assert.Contains("radtk-data-table--cards", view);
        Assert.Contains("cp-finance-balance", view);
    }

    [Fact]
    public void ClientTopUpRequests_UsesLabeledActionCards()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CollectionPoint", "Views", "Wallet", "ClientTopUpRequests.cshtml"));
        Assert.Contains("collection-point-finance-page", view);
        Assert.Contains("طلبات تغذية رصيد للمشتركين", view);
        Assert.Contains("data-label=\"المشترك\"", view);
        Assert.Contains("cp-row-actions", view);
        Assert.Contains("موافقة", view);
    }

    [Fact]
    public void Receipts_ConvertsTablesToCards()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CollectionPoint", "Views", "Receipts", "Index.cshtml"));
        Assert.Contains("collection-point-finance-page", view);
        Assert.Contains("_CollectionPointFinanceNav", view);
        Assert.Contains("data-label=\"طريقة الدفع\"", view);
        Assert.Contains("data-label=\"المبلغ (ل.س.ج)\"", view);
        Assert.Contains("radtk-data-table--cards", view);
    }

    [Fact]
    public void CollectionPointFinanceCardsCss_ClampsToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "collection-point-finance-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("cp-finance-nav", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف مالية نقطة التحصيل: " + Path.Combine(relativeParts));
    }
}
