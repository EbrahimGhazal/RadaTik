using Xunit;

namespace RadaTik.Tests.Views;

public sealed class CollectionPointHistoryRouteTests
{
    [Fact]
    public void DeskIndex_LinksHistoryViaNamedCollectionPointRoute()
    {
        string view = File.ReadAllText(FindFile(
            "RadaTik", "Areas", "CollectionPoint", "Views", "CollectionPoint", "Index.cshtml"));
        Assert.Contains("collectionPoint-actions", view);
        Assert.Contains("action = \"History\"", view);
        Assert.DoesNotContain("var historyUrl = Url.Action(\"History\")", view);
    }

    [Fact]
    public void Dashboard_RedirectsLegacyHistoryUrl()
    {
        string controller = File.ReadAllText(FindFile(
            "RadaTik", "Areas", "CollectionPoint", "Controllers", "DashboardController.cs"));
        Assert.Contains("public IActionResult History()", controller);
        Assert.Contains("RedirectToRoute(\"collectionPoint-actions\"", controller);
        Assert.Contains("action = \"History\"", controller);
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

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }
}
