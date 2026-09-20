using Xunit;

namespace RadaTik.Tests.Views;

public sealed class AntennaCalibrationViewTests
{
    [Fact]
    public void ManagerViews_IncludeScenarioWorkflow()
    {
        string index = Read("Views", "AntennaCalibration", "Index.cshtml");
        string scenarios = Read("Views", "AntennaCalibration", "Scenarios.cshtml");
        string edit = Read("Views", "AntennaCalibration", "EditScenario.cshtml");
        Assert.Contains("سيناريو المعايرة", index);
        Assert.Contains("calStartMap", index);
        Assert.Contains("JoinMonitor", index);
        Assert.Contains("سيناريوهات المعايرة", scenarios);
        Assert.Contains("SaveScenario", edit);
        Assert.Contains("CrewMode", edit);
        Assert.Contains("AimMode", edit);
    }

    [Fact]
    public void FieldJoin_IsScenarioDriven()
    {
        string join = Read("Views", "AntennaCalibration", "Join.cshtml");
        string js = File.ReadAllText(Find("wwwroot", "js", "antenna-calibration-field.js"));
        Assert.Contains("_CalibrateFieldLayout", join);
        Assert.Contains("JoinField", js);
        Assert.Contains("PublishOrientation", js);
        Assert.Contains("cfSignal", join);
        Assert.DoesNotContain("اختر الوضع", join);
    }

    [Fact]
    public void Sidebars_LinkToCalibration()
    {
        string admin = File.ReadAllText(Find("Areas", "CompanyAdmin", "Views", "Shared", "_SidebarNavSections.cshtml"));
        string emp = File.ReadAllText(Find("Areas", "CompanyEmployee", "Views", "Shared", "_Sidebar.cshtml"));
        Assert.Contains("networkManager-calibration", admin);
        Assert.Contains("معايرة الهوائي", admin);
        Assert.Contains("employee-calibration", emp);
        Assert.Contains("معايرة الهوائي", emp);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Find(parts));

    private static string Find(params string[] parts)
    {
        string[] roots =
        [
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
            Directory.GetCurrentDirectory()
        ];
        foreach (string root in roots)
        {
            string candidate = Path.Combine(new[] { root, "RadaTik" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }
}
