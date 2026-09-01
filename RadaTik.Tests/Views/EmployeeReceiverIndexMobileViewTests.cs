using Xunit;

namespace RadaTik.Tests.Views;

public sealed class EmployeeReceiverIndexMobileViewTests
{
    [Fact]
    public void CompanyEmployeeReceiverIndex_FitsViewportOnCompactScreens()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyEmployee", "Views", "Receiver", "Index.cshtml"));
        Assert.Contains("radtk-page-emp-receiver-index", view);
        Assert.Contains("employee-receiver-page", view);
        Assert.Contains("radtk-data-table--cards", view);
        Assert.Contains("employee-receiver-cards.css", view);
        Assert.Contains("data-label=\"المستقبل\"", view);
        Assert.Contains("data-label=\"IP\"", view);
        Assert.Contains("radtk-col-actions", view);
        Assert.Contains("employee-receiver-actions__label", view);
        Assert.Contains("id=\"employeeReceiversTable\"", view);
    }

    [Fact]
    public void EmployeeReceiverCardsCss_ClampsCardToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "employee-receiver-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("#employeeReceiversTable tbody tr", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("visibility: visible !important", css);
        Assert.Contains("#receiversMap", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف مستقبلات الموظف: " + Path.Combine(relativeParts));
    }
}
