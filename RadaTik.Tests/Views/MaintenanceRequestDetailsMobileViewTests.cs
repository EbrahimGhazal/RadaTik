using Xunit;

namespace RadaTik.Tests.Views;

public sealed class MaintenanceRequestDetailsMobileViewTests
{
    [Fact]
    public void EmployeeMaintenanceRequestDetails_UsesReadableVisitCards()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyEmployee", "Views", "RequestsManagement", "MaintenanceRequestDetails.cshtml"));
        Assert.Contains("radtk-page-maint-details", view);
        Assert.Contains("maint-request-details-page", view);
        Assert.Contains("_MaintenanceRequestDetailsBody", view);
        Assert.Contains("maintenance-request-details-cards.css", view);
        Assert.Contains("maintenance-request-details.js", view);
        Assert.Contains("employee-requestsManagement", view);
        Assert.Contains("max-width: 1199.98px", File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "maintenance-request-details-cards.css")));
    }

    [Fact]
    public void SharedDetailsBody_ShowsVisitAddressMapAndActions()
    {
        string body = File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_MaintenanceRequestDetailsBody.cshtml"));
        Assert.Contains("maint-facts", body);
        Assert.Contains("عنوان الزيارة", body);
        Assert.Contains("ReceiverVisitAddressFormatter", body);
        Assert.Contains("maintVisitMap", body);
        Assert.Contains("افتح في الخرائط", body);
        Assert.Contains("الموظف المسند", body);
        Assert.Contains("_SubscriberFaultDiagnosisReport", body);
        Assert.Contains("selectedMaintenanceTypes", body);
        Assert.Contains("liveGrossEstimate", body);
        Assert.Contains("js-once-submit", body);
        Assert.DoesNotContain("details-grid", body);

        string controller = File.ReadAllText(FindFile("RadaTik", "Controllers", "RequestsManagementController.cs"));
        Assert.Contains("ThenInclude(r => r!.Sector)", controller);
        Assert.Contains("ThenInclude(c => c!.Receiver)", controller);
    }

    [Fact]
    public void MaintenanceRequestDetailsCardsCss_ClampsCardToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "maintenance-request-details-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("#maintVisitMap", css);
        Assert.Contains(".maint-totals__row--gross", css);
    }

    [Fact]
    public void CompanyAdminMaintenanceRequestDetails_ReusesSharedBody()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "RequestsManagement", "MaintenanceRequestDetails.cshtml"));
        Assert.Contains("_MaintenanceRequestDetailsBody", view);
        Assert.Contains("ShowInvoiceLedgerLinks", view);
        Assert.Contains("networkManager-requestsManagement", view);
        Assert.Contains("maintenance-request-details-cards.css", view);
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

        throw new FileNotFoundException("لم يتم العثور على ملف تفاصيل طلب الصيانة: " + Path.Combine(relativeParts));
    }
}
