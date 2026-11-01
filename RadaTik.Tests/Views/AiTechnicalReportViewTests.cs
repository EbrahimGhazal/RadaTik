using Xunit;

namespace RadaTik.Tests.Views;

public sealed class AiTechnicalReportViewTests
{
    [Fact]
    public void ReportView_ClonesStatsJsonElementsBeforeDocumentDispose()
    {
        string view = File.ReadAllText(FindFile(
            "RadaTik", "Views", "CustomerService", "AiTechnicalReport.cshtml"));
        Assert.Contains("JsonDocument.Parse(statsJson)", view);
        Assert.Contains("e.Clone()", view);
        Assert.Contains("EnumerateArray().Select(e => e.Clone()).ToList()", view);
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
