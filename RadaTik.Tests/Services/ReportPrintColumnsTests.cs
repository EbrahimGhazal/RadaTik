using RadaTik.Models;
using RadaTik.Services.Reports;
using RadaTik.ViewModels.CompanyAdmin;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ReportPrintColumnsTests
{
    [Fact]
    public void Selectable_DiffersByReportKind()
    {
        HashSet<string> subscriberKeys = ReportPrintColumns.Selectable(CompanyReportKind.Subscribers)
            .Select(c => c.Key)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> sectorKeys = ReportPrintColumns.Selectable(CompanyReportKind.Sectors)
            .Select(c => c.Key)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> serverKeys = ReportPrintColumns.Selectable(CompanyReportKind.Servers)
            .Select(c => c.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("sid", subscriberKeys);
        Assert.DoesNotContain("sid", sectorKeys);
        Assert.Contains("coords", sectorKeys);
        Assert.DoesNotContain("coords", subscriberKeys);
        Assert.DoesNotContain("pass", serverKeys);
        Assert.DoesNotContain("Pass", serverKeys);
    }

    [Fact]
    public void BuildSectors_WritesSequenceAndSelectedColumnsOnly()
    {
        List<Sector> sectors =
        [
            new()
            {
                Id = 88,
                Name = "قطاع أ",
                IPAddress = "10.0.0.1",
                Latitude = 33.5,
                Longitude = 36.3,
                ElevationMeters = 700,
                Direction = 180,
                CoverageAngle = 90,
                CoverageRange = 12.5,
                Network = new Network { Name = "شبكة" }
            }
        ];

        (string[] headers, List<IReadOnlyList<string>> rows) =
            ReportPrintColumns.BuildSectors(sectors, ["name", "ip"]);

        Assert.Equal(["تسلسل", "اسم المرسل", "عنوان IP"], headers);
        Assert.Equal(["1", "قطاع أ", "10.0.0.1"], rows[0]);
        Assert.DoesNotContain("88", rows.SelectMany(r => r));
        Assert.DoesNotContain("شبكة", rows[0]);
    }

    [Fact]
    public void BuildReceivers_IgnoresUnknownKeysAndUsesDefaultsWhenEmpty()
    {
        IReadOnlyList<string> selected = ReportPrintColumns.ResolveSelected(
            CompanyReportKind.Receivers,
            ["name", "unknown", "status"]);

        Assert.Equal(["name", "status"], selected);

        IReadOnlyList<string> defaults = ReportPrintColumns.ResolveSelected(CompanyReportKind.Receivers, []);
        Assert.Equal(ReportPrintColumns.DefaultKeys(CompanyReportKind.Receivers), defaults);
        Assert.Contains("sector", defaults);
        Assert.DoesNotContain("sid", defaults);
    }

    [Fact]
    public void BuildServers_NeverIncludesPassword()
    {
        MikroTikServer server = new()
        {
            Id = 5,
            Name = "سرفر",
            Host = "1.2.3.4",
            Port = 8728,
            User = "admin",
            Pass = "secret-password",
            Network = new Network { Name = "نت" },
            IsActive = true
        };

        (string[] headers, List<IReadOnlyList<string>> rows) =
            ReportPrintColumns.BuildServers([server], ReportPrintColumns.Selectable(CompanyReportKind.Servers).Select(c => c.Key));

        Assert.DoesNotContain("كلمة المرور", headers);
        Assert.DoesNotContain("secret-password", rows.SelectMany(r => r));
        Assert.Equal("1", rows[0][0]);
        Assert.DoesNotContain("5", rows.SelectMany(r => r));
    }
}
