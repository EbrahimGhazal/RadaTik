using Xunit;

namespace RadaTik.Tests.Views;

/// <summary>
/// صفحة التفاصيل كانت تحقن النوع الملموس PermissionService بينما DI يسجّل IPermissionService فقط،
/// فيسقط العرض برسالة Error العامة عند فتح /networkManager/Clients/Details/{id}.
/// </summary>
public sealed class ClientDetailsViewDiTests
{
    [Fact]
    public void CompanyAdminDetailsView_DoesNotInjectConcretePermissionService()
    {
        string viewPath = FindView("Areas", "CompanyAdmin", "Views", "Clients", "Details.cshtml");
        string text = File.ReadAllText(viewPath);

        Assert.DoesNotContain("@inject RadaTik.Services.PermissionService", text);
        Assert.Contains("ViewBag.CanEditClient", text);
        Assert.Contains("_SubscriberFaultDiagnosis", text);
        Assert.DoesNotContain("model.Password", text);
        Assert.DoesNotContain("MikroTikInfo.Password", text);
        Assert.Contains("CreateMaintenanceFromDiagnosis", File.ReadAllText(FindView("Views", "Shared", "_SubscriberFaultDiagnosis.cshtml")));
        Assert.Contains("_SubscriberFaultLedQuestions", File.ReadAllText(FindView("Views", "Shared", "_SubscriberFaultDiagnosis.cshtml")));
        Assert.Contains("_ClientNationalIdCard", text);
        Assert.Contains("NationalIdFrontPath", text);
    }

    [Fact]
    public void CompanyEmployeeDetailsView_PlacesModelDirectiveFirst()
    {
        string viewPath = FindView("Areas", "CompanyEmployee", "Views", "Clients", "Details.cshtml");
        string text = File.ReadAllText(viewPath).TrimStart();

        Assert.StartsWith("@model RadaTik.Models.Client", text);
        Assert.Contains("_SubscriberFaultDiagnosis", text);
        Assert.DoesNotContain("@inject RadaTik.Services.PermissionService", text);
        Assert.DoesNotContain("model.Password", text);
        Assert.DoesNotContain("كلمة المرور", text);
        Assert.Contains("_ClientNationalIdCard", text);
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

        throw new FileNotFoundException("لم يتم العثور على عرض تفاصيل العميل: " + Path.Combine(relativeParts));
    }
}
