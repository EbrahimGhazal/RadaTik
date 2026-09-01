using Xunit;

namespace RadaTik.Tests.Views;

public sealed class EmployeeServiceApprovalsMobileViewTests
{
    [Fact]
    public void CompanyAdminEmployeeServiceApprovals_UsesReadableApprovalCards()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "EmployeeServiceApprovals", "Index.cshtml"));
        Assert.Contains("radtk-page-emp-approvals", view);
        Assert.Contains("employee-approvals-page", view);
        Assert.Contains("emp-approval-card", view);
        Assert.Contains("emp-approvals-empty", view);
        Assert.Contains("employee-service-approvals-cards.css", view);
        Assert.Contains("approval-request-", view);
        Assert.Contains("asp-action=\"Approve\"", view);
        Assert.Contains("asp-action=\"Reject\"", view);
        Assert.DoesNotContain("radtk-data-table--cards", view);
    }

    [Fact]
    public void EmployeeServiceApprovalsCardsCss_ClampsCardToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "employee-service-approvals-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("emp-approval-card--focused", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف موافقات الموظفين: " + Path.Combine(relativeParts));
    }
}
