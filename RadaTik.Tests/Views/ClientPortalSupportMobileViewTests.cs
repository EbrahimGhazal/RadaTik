using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ClientPortalSupportMobileViewTests
{
    [Fact]
    public void MaintenanceInvoices_UsesSupportCardsAndOnceSubmit()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "MaintenanceInvoices.cshtml"));
        Assert.Contains("client-portal-invoices-page", view);
        Assert.Contains("client-portal-support-cards.css", view);
        Assert.Contains("support-card-list", view);
        Assert.Contains("js-once-submit", view);
        Assert.Contains("PayMaintenanceInvoice", view);
        Assert.Contains("form-once-submit.js", view);
        Assert.DoesNotContain("<thead", view);
    }

    [Fact]
    public void Notifications_UsesReadableCardsInsteadOfTable()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "Notifications.cshtml"));
        Assert.Contains("client-portal-notify-page", view);
        Assert.Contains("notify-card", view);
        Assert.Contains("support-filter", view);
        Assert.Contains("OpenNotification", view);
        Assert.DoesNotContain("<table", view);
    }

    [Fact]
    public void MaintenanceRequestDetails_ShowsVisitAddressWithoutDuplicateAssignee()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "MaintenanceRequestDetails.cshtml"));
        Assert.Contains("client-portal-ticket-page", view);
        Assert.Contains("ticket-facts", view);
        Assert.Contains("العنوان التفصيلي", view);
        Assert.Contains("هاتف التواصل", view);
        Assert.Contains("_SubscriberFaultDiagnosisReport", view);
        Assert.Contains("الموظف المسند", view);
        Assert.DoesNotContain("الموظف المكلّف", view);
    }

    [Fact]
    public void CreateMaintenanceRequest_PrefillsReceiverAddressAndBlocksDoubleSubmit()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "ClientPortal", "Views", "ClientPortal", "CreateMaintenanceRequest.cshtml"));
        Assert.Contains("js-once-submit", view);
        Assert.Contains("form-once-submit.js", view);
        Assert.Contains("visitAddress", view);
        Assert.Contains("data-receiver-lat", view);
        Assert.Contains("تم تعبئة العنوان تلقائياً من موقع اللاقط", view);
        Assert.Contains("جاري إرسال الطلب", view);
        Assert.Contains("_SubscriberFaultLedQuestions", view);
        Assert.Contains("nominatim.openstreetmap.org/reverse", view);

        string controller = File.ReadAllText(FindFile("RadaTik", "Controllers", "ClientPortalController.cs"));
        Assert.Contains("ReceiverVisitAddressFormatter.FromClient", controller);
        Assert.Contains("ThenInclude(r => r!.Sector)", controller);
        Assert.Contains("duplicateClick", controller);
        Assert.Contains("TimeSpan.FromSeconds(6)", controller);
        Assert.Contains("طلب مشابه قبل لحظات", controller);
    }

    [Fact]
    public void SupportCardsCss_ClampsToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "client-portal-support-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("js-once-submit", File.ReadAllText(FindFile("RadaTik", "wwwroot", "js", "form-once-submit.js")));
        Assert.Contains("dataset.submitting", File.ReadAllText(FindFile("RadaTik", "wwwroot", "js", "form-once-submit.js")));
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
