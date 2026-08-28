using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ClientsIndexColumnPickerTests
{
    [Fact]
    public void CompanyAdminClientsIndex_ReappliesDisplayAfterDataTablesVisibility()
    {
        string text = File.ReadAllText(FindView("Areas", "CompanyAdmin", "Views", "Clients", "Index.cshtml"));
        Assert.Contains("applyInlineColumnDisplay", text);
        Assert.Contains("columns.adjust().draw(false)", text);
        Assert.Contains("column-toggle", text);
    }

    [Fact]
    public void CompanyEmployeeClientsIndex_ReappliesDisplayAfterDataTablesVisibility()
    {
        string text = File.ReadAllText(FindView("Areas", "CompanyEmployee", "Views", "Clients", "Index.cshtml"));
        Assert.Contains("applyInlineColumnDisplay", text);
        Assert.Contains("columns.adjust().draw(false)", text);
        Assert.Contains("column-toggle", text);
    }

    private static string FindView(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(new[] { dir, "RadaTik" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException("لم يتم العثور على عرض قائمة المشتركين: " + Path.Combine(relativeParts));
    }
}
