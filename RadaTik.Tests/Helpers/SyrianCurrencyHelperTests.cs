using RadaTik.Helpers;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class SyrianCurrencyHelperTests
{
    [Fact]
    public void NewToOld_UsesHundredRatio()
    {
        Assert.Equal(500_000_000m, SyrianCurrencyHelper.NewToOld(5_000_000m));
    }

    [Fact]
    public void FormatNew_UsesThousandSeparators()
    {
        Assert.Equal("5,000,000.00", SyrianCurrencyHelper.FormatNew(5_000_000m));
    }

    [Fact]
    public void FormatNumber_FormatsIntegerWithCommas()
    {
        Assert.Equal("12,500", SyrianCurrencyHelper.FormatNumber(12_500));
    }
}
