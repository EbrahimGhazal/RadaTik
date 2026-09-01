using Xunit;

namespace RadaTik.Tests.Views;

public sealed class CollectionPointSearchClientsViewTests
{
    [Fact]
    public void DashboardSearchCallsCollectionPointSearchClientsAction()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CollectionPoint", "Views", "CollectionPoint", "Index.cshtml"));
        Assert.Contains("Url.Action(\"SearchClients\", \"CollectionPoint\"", view);
        Assert.Contains("area = \"CollectionPoint\"", view);
        Assert.Contains("Array.isArray(items)", view);
        Assert.Contains("Accept': 'application/json'", view);
        Assert.Contains("collect-client-card", view);
        Assert.Contains("searchResults", view);
        Assert.Contains("collection-point-search-cards.css", view);
        Assert.DoesNotContain("searchResultsBody", view);
    }

    [Fact]
    public void CollectionPointSearchCardsCss_UsesSubscriberCards()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "collection-point-search-cards.css"));
        Assert.Contains(".collect-client-card", css);
        Assert.Contains(".collect-client-card__due-amount", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف لوحة نقطة التحصيل: " + Path.Combine(relativeParts));
    }
}
