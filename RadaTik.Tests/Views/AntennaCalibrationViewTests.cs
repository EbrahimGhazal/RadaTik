using Xunit;

namespace RadaTik.Tests.Views;

public sealed class AntennaCalibrationViewTests
{
    [Fact]
    public void AdminSidebar_HasCalibrationLink()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "Shared", "_SidebarNavSections.cshtml"));
        Assert.Contains("networkManager-calibration", text);
        Assert.Contains("معايرة", text);
    }

    [Fact]
    public void JoinView_HasRolePlacementFiguresAndCompassEnable()
    {
        string join = File.ReadAllText(FindFile("RadaTik", "Views", "AntennaCalibration", "Join.cshtml"));
        string index = File.ReadAllText(FindFile("RadaTik", "Views", "AntennaCalibration", "Index.cshtml"));
        Assert.Contains("_CalibratePhonePlacementTx", join);
        Assert.Contains("_CalibratePhonePlacementRx", join);
        Assert.Contains("btnEnableFieldCompass", join);
        Assert.Contains("antenna-cal-split", index);
        Assert.Contains("calStartMap", index);
        Assert.Contains("antenna-cal-qr-img", index);
        Assert.Contains("no-select2", index);
        Assert.Contains("dir=\"ltr\"", index);
        Assert.Contains("select2:select", index);
        Assert.Contains("/calibrate/Join", File.ReadAllText(FindFile("RadaTik", "Controllers", "AntennaCalibrationController.cs")));
        Assert.Contains("_CalibrateFieldLayout", File.ReadAllText(FindFile("RadaTik", "Views", "AntennaCalibration", "JoinMissing.cshtml")));
        Assert.Contains("btnResetPeak", index);
        Assert.Contains("PublishOrientation", join);
        Assert.Contains("liveRf", join);
        Assert.Contains("metalWarn", join);
        Assert.Contains("ظهر الموبايل على القطاع", File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_CalibratePhonePlacementTx.cshtml")));
        Assert.Contains("ظهر الموبايل على اللاقط", File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_CalibratePhonePlacementRx.cshtml")));
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
