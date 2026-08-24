using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ClientEditMikroTikOptInViewTests
{
    [Fact]
    public void CompanyAdminEdit_RequiresExplicitMikroTikOptIn()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "Clients", "Edit.cshtml"));
        Assert.Contains("applyMikroTikChanges", text);
        Assert.Contains("تعديل بيانات المشترك على مايكروتك", text);
        Assert.Contains("mikroTikEditFields", text);
        Assert.DoesNotContain("التعديلات هنا تنفذ مباشرة على المايكروتك.", text);
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
