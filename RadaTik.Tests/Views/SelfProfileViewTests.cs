using Xunit;

namespace RadaTik.Tests.Views;

public sealed class SelfProfileViewTests
{
    [Fact]
    public void EmployeeProfile_AllowsPersonalFieldsAndSystemPasswordOnly()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyEmployee", "Views", "Account", "Profile.cshtml"));
        Assert.Contains("asp-for=\"FullName\"", text);
        Assert.Contains("asp-for=\"Email\"", text);
        Assert.Contains("asp-for=\"PhoneNumber\"", text);
        Assert.Contains("كلمة مرور النظام", text);
        Assert.Contains("لا يمكن تغيير بيانات MikroTik", text);
        Assert.Contains("value=\"@Model.UserName\" disabled", text);
        Assert.Contains("asp-area=\"CompanyEmployee\"", text);
        Assert.Contains("asp-controller=\"Account\"", text);
        Assert.Contains("asp-action=\"UpdateProfile\"", text);
        Assert.Contains("asp-action=\"ChangePassword\"", text);
        Assert.DoesNotContain("asp-for=\"UserName\"", text);
        Assert.DoesNotContain("/networkManager/Account/", text);
    }

    [Fact]
    public void EmployeeClientEdit_LocksMikroTikUsernameAndPassword()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyEmployee", "Views", "Clients", "Edit.cshtml"));
        Assert.Contains("ممنوع تغيير اسم المستخدم على المايكروتك", text);
        Assert.Contains("ممنوع تغيير كلمة مرور السيرفر", text);
        Assert.DoesNotContain("placeholder=\"اسم المستخدم على MikroTik\"", text);
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
