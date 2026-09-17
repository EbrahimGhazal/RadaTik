using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class PhoneBoresightMathTests
{
    [Fact]
    public void FlatScreenUp_PointsSkyward_AzimuthInvalid()
    {
        PhoneBoresightPose pose = PhoneBoresightMath.FromDeviceOrientation(0, 0, 0);
        Assert.False(pose.AzimuthValid);
        Assert.InRange(pose.ElevationDegrees, 80, 90);
    }

    [Fact]
    public void UprightNaturalAlpha0_ScreenNormalIsEastAndLevel()
    {
        PhoneBoresightPose pose = PhoneBoresightMath.FromDeviceOrientation(0, 90, 0);
        Assert.True(pose.AzimuthValid);
        Assert.InRange(pose.AzimuthDegrees, 80, 100);
        Assert.InRange(pose.ElevationDegrees, -8, 8);
    }

    [Fact]
    public void MutualAzimuth_OppositeHeadings_AreFacing()
    {
        Assert.True(PhoneBoresightMath.MutualAzimuthAligned(20, 200));
        Assert.False(PhoneBoresightMath.MutualAzimuthAligned(20, 40));
    }

    [Fact]
    public void ToMagneticAzimuth_SubtractsEastDeclinationFromTrueNorth()
    {
        double mag = PhoneBoresightMath.ToMagneticAzimuth(90, 5, fromTrueNorth: true);
        Assert.InRange(mag, 84.5, 85.5);
    }

    [Fact]
    public void DishBackPlacement_FlipsAzimuth180AndNegatesElevation()
    {
        PhoneBoresightPose screen = new(90, 10, true);
        PhoneBoresightPose beam = PhoneBoresightMath.ToAntennaBoresightFromDishBack(screen);
        Assert.InRange(beam.AzimuthDegrees, 269.5, 270.5);
        Assert.InRange(beam.ElevationDegrees, -10.5, -9.5);
        Assert.True(beam.AzimuthValid);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(8.0, false)]
    [InlineData(25.0, true)]
    [InlineData(-1.0, true)]
    public void CompassUnstable_UsesWebkitAccuracyThreshold(double? accuracy, bool expected)
    {
        Assert.Equal(expected, PhoneBoresightMath.CompassUnstable(accuracy));
    }
}
