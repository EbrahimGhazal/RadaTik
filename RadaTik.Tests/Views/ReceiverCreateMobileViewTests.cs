using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ReceiverCreateMobileViewTests
{
    [Fact]
    public void CompanyAdminCreate_HasMobileMapLayout()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "Receiver", "Create.cshtml"));
        Assert.Contains("receiver-create-page", text);
        Assert.Contains("receiver-map-wrap", text);
        Assert.Contains("receiver-map-toolbar", text);
        Assert.Contains("btnUseMyLocation", text);
        Assert.Contains("invalidateSize", text);
        Assert.Contains("--receiver-map-height", text);
        Assert.DoesNotContain("height: 250px", text);
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
