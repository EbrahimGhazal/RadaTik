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
        Assert.Contains("مشترك مميز (VIP)", text);
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
