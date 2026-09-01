using Xunit;

namespace RadaTik.Tests.Views;

public sealed class FinancialQuickNavMobileViewTests
{
    [Fact]
    public void FinancialQuickNav_LinksWrapStylesheet()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "Shared", "_FinancialQuickNav.cshtml"));
        Assert.Contains("financial-quick-nav.css", view);
        Assert.Contains("الصندوق", view);
        Assert.Contains("الدفتر", view);
        Assert.Contains("جرد مالي", view);
    }

    [Fact]
    public void FinancialQuickNavCss_KeepsTabsOnARowThenWraps()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "financial-quick-nav.css"));
        Assert.Contains("flex-wrap: wrap", css);
        Assert.Contains("flex: 0 0 auto !important", css);
        Assert.Contains("white-space: nowrap", css);
        Assert.DoesNotContain("flex: 1 1 100%", css);
        Assert.DoesNotContain("flex: 1 1 calc(50%", css);
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

        throw new FileNotFoundException("لم يتم العثور على تبويبات المالية: " + Path.Combine(relativeParts));
    }
}
