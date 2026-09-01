using Xunit;

namespace RadaTik.Tests.Views;

public sealed class NewSubscriberWizardSubscriberViewTests
{
    [Fact]
    public void SubscriberStep_IncludesOccupationWorkplaceAndVip()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "NewSubscriberWizard", "Subscriber.cshtml"));
        Assert.Contains("asp-for=\"Occupation\"", text);
        Assert.Contains("asp-for=\"Workplace\"", text);
        Assert.Contains("asp-for=\"IsVip\"", text);
        Assert.Contains("asp-for=\"VipNote\"", text);
        Assert.Contains("VipDiscountPercent", text);
        Assert.Contains("مجاني دائم", text);
        Assert.Contains("مشترك مميز (VIP)", text);
        Assert.Contains("VipDiscountPercent", text);
        Assert.Contains("مجاني دائم", text);
        Assert.Contains("asp-route=\"@ViewData[\"WizardRoute\"]\"", text);
        Assert.Contains("asp-route-action=\"Subscriber\"", text);
    }

    [Fact]
    public void StartStep_PostsToNamedWizardRoute()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "NewSubscriberWizard", "Start.cshtml"));
        Assert.Contains("asp-route=\"@ViewData[\"WizardRoute\"]\"", text);
        Assert.Contains("asp-route-action=\"Start\"", text);
        Assert.DoesNotContain("asp-action=\"Start\"", text);
    }

    [Fact]
    public void SharedReceiverStep_ReloadsReceiversAfterSenderChange()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "NewSubscriberWizard", "SharedReceiver.cshtml"));
        Assert.Contains("data-server-id", view);
        Assert.Contains("data-sector-id", view);
        Assert.Contains("sectorMatcher", view);
        Assert.Contains("receiverMatcher", view);
        Assert.Contains("no-select2", view);
        Assert.Contains("لا يوجد لاقط على هذا المرسل", view);
        Assert.Contains("بانتظار التفعيل", view);
        Assert.DoesNotContain("GetSectorsByServer", view);
        Assert.DoesNotContain("$('#sectorSelect').html", view);

        string controller = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Controllers", "NewSubscriberWizardController.cs"));
        Assert.Contains("GetSectorsByServer(int? serverId)", controller);
        Assert.Contains("SharedReceiverQuery", controller);
        Assert.Contains("SharedSectorQuery", controller);
        Assert.Contains("s.MikroTikServer != null && s.MikroTikServer.NetworkId == networkId", controller);
        Assert.Contains("WizardSectorLookup", controller);
        Assert.Contains("اللاقط المحدد غير متاح ضمن السيرفر/المرسل الحالي", controller);
        Assert.DoesNotContain("لا يوجد مشترك آخر عليه", controller);
    }

    [Fact]
    public void WizardController_UsesNamedRouteRedirects()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Controllers", "NewSubscriberWizardController.cs"));
        Assert.Contains("employee-new-subscriber-wizard", text);
        Assert.Contains("WizardRedirect(nameof(Subscriber))", text);
        Assert.DoesNotContain("return RedirectToAction(nameof(Start));", text);
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
