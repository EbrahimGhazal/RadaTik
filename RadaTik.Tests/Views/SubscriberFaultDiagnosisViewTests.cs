using Xunit;

namespace RadaTik.Tests.Views;

public sealed class SubscriberFaultDiagnosisViewTests
{
    [Fact]
    public void SharedDiagnosisPartial_IncludesLedAndCreateTicket()
    {
        string text = File.ReadAllText(FindView("Views", "Shared", "_SubscriberFaultDiagnosis.cshtml"));
        Assert.Contains("_SubscriberFaultLedQuestions", text);
        Assert.Contains("CreateMaintenanceFromDiagnosis", text);
        Assert.Contains("routerPowerOn", text);
        Assert.Contains("canCreateMaintenance", text);
    }

    [Fact]
    public void ClientPortalCreateMaintenance_IncludesLedQuestions()
    {
        string text = File.ReadAllText(FindView("Areas", "ClientPortal", "Views", "ClientPortal", "CreateMaintenanceRequest.cshtml"));
        Assert.Contains("_SubscriberFaultLedQuestions", text);
        Assert.Contains("subscriber-fault-led-wrap", text);
    }

    [Fact]
    public void MaintenanceDetailsViews_ShowDiagnosisReport()
    {
        Assert.Contains(
            "_MaintenanceRequestDetailsBody",
            File.ReadAllText(FindView("Areas", "CompanyAdmin", "Views", "RequestsManagement", "MaintenanceRequestDetails.cshtml")));
        Assert.Contains(
            "_MaintenanceRequestDetailsBody",
            File.ReadAllText(FindView("Areas", "CompanyEmployee", "Views", "RequestsManagement", "MaintenanceRequestDetails.cshtml")));
        Assert.Contains(
            "_SubscriberFaultDiagnosisReport",
            File.ReadAllText(FindView("Views", "Shared", "_MaintenanceRequestDetailsBody.cshtml")));
        Assert.Contains(
            "_SubscriberFaultDiagnosisReport",
            File.ReadAllText(FindView("Areas", "ClientPortal", "Views", "ClientPortal", "MaintenanceRequestDetails.cshtml")));
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

        throw new FileNotFoundException("لم يتم العثور على العرض: " + Path.Combine(relativeParts));
    }
}
