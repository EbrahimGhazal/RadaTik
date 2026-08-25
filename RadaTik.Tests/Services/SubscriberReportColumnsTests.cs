using RadaTik.Models;
using RadaTik.Services.Reports;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class SubscriberReportColumnsTests
{
    [Fact]
    public void Build_WritesSequentialNumbersIndependentOfClientId()
    {
        List<Client> clients =
        [
            new() { Id = 40, Name = "بسام", SID = "2" },
            new() { Id = 7, Name = "أحمد", SID = "1" }
        ];

        (string[] headers, List<IReadOnlyList<string>> rows) = SubscriberReportColumns.Build(clients, ["name"]);

        Assert.Equal("تسلسل", headers[0]);
        Assert.Equal("الاسم الثلاثي", headers[1]);
        Assert.Equal("1", rows[0][0]);
        Assert.Equal("بسام", rows[0][1]);
        Assert.Equal("2", rows[1][0]);
        Assert.Equal("أحمد", rows[1][1]);
        Assert.DoesNotContain("40", rows.SelectMany(r => r));
        Assert.DoesNotContain("7", rows.SelectMany(r => r));
    }

    [Fact]
    public void Build_IncludesOnlySelectedColumns()
    {
        Client client = new()
        {
            Id = 9,
            Name = "ليلى",
            SID = "111",
            UserName = "laila",
            PhoneNumber = "099",
            ProfileName = "ذهبي",
            ResidenceAddress = "دمشق"
        };

        (string[] headers, List<IReadOnlyList<string>> rows) = SubscriberReportColumns.Build([client], ["name", "phone"]);

        Assert.Equal(["تسلسل", "الاسم الثلاثي", "الجوال"], headers);
        Assert.Equal(["1", "ليلى", "099"], rows[0]);
        Assert.DoesNotContain("111", rows[0]);
        Assert.DoesNotContain("laila", rows[0]);
    }

    [Fact]
    public void ResolveSelected_FallsBackToDefaultsWhenEmpty()
    {
        IReadOnlyList<string> selected = SubscriberReportColumns.ResolveSelected([]);
        Assert.Equal(SubscriberReportColumns.DefaultKeys, selected);
    }
}
