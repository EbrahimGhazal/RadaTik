using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class LineOfSightMathTests
{
    [Fact]
    public void FirstFresnelRadius_AtMidpointOf1Km_5_8GHz_IsAbout3_6m()
    {
        double r = LineOfSightMath.FirstFresnelRadiusMeters(5800, 500, 1000);
        Assert.InRange(r, 3.4, 3.8);
    }

    [Fact]
    public void FirstFresnelRadius_AtEndpoints_IsZero()
    {
        Assert.Equal(0, LineOfSightMath.FirstFresnelRadiusMeters(5800, 0, 1000));
        Assert.Equal(0, LineOfSightMath.FirstFresnelRadiusMeters(5800, 1000, 1000));
    }

    [Fact]
    public void EarthBulge_AtEndpoints_IsZero_AndGrowsTowardMidspan()
    {
        Assert.Equal(0, LineOfSightMath.EarthBulgeMeters(0, 20_000));
        double mid = LineOfSightMath.EarthBulgeMeters(10_000, 20_000);
        Assert.InRange(mid, 5.0, 7.0);
    }

    [Fact]
    public void NormalizeFrequency_UsesDefaultWhenMissing()
    {
        Assert.Equal(LineOfSightMath.DefaultFrequencyMhz, LineOfSightMath.NormalizeFrequencyMhz(0));
        Assert.Equal(5200, LineOfSightMath.NormalizeFrequencyMhz(5200));
    }

    [Fact]
    public void EstimateBuildingHeight_PrefersOsmHeight()
    {
        BuildingHeightEstimate h = LineOfSightMath.EstimateBuildingHeight(new Dictionary<string, string>
        {
            ["height"] = "22 m",
            ["building:levels"] = "4",
            ["building"] = "apartments"
        });
        Assert.Equal(22, h.HeightMeters);
        Assert.Equal(BuildingHeightSource.OsmHeight, h.Source);
        Assert.Equal(BuildingHeightConfidence.High, h.Confidence);
    }

    [Fact]
    public void EstimateBuildingHeight_UsesLevelsWhenNoHeight()
    {
        BuildingHeightEstimate h = LineOfSightMath.EstimateBuildingHeight(new Dictionary<string, string>
        {
            ["building:levels"] = "5",
            ["building"] = "yes"
        });
        Assert.Equal(16, h.HeightMeters);
        Assert.Equal(BuildingHeightSource.Levels, h.Source);
        Assert.Equal(BuildingHeightConfidence.Medium, h.Confidence);
    }

    [Fact]
    public void EstimateBuildingHeight_ApartmentsGuess_IsTallerThanHouse()
    {
        BuildingHeightEstimate apt = LineOfSightMath.EstimateBuildingHeight(new Dictionary<string, string>
        {
            ["building"] = "apartments"
        });
        BuildingHeightEstimate house = LineOfSightMath.EstimateBuildingHeight(new Dictionary<string, string>
        {
            ["building"] = "house"
        });
        Assert.True(apt.HeightMeters > house.HeightMeters);
        Assert.Equal(BuildingHeightSource.BuildingType, apt.Source);
        Assert.Equal(BuildingHeightConfidence.Low, apt.Confidence);
    }

    [Fact]
    public void RelatePolygonToPath_DetectsCrossingBuilding()
    {
        double lat1 = 33.50, lon1 = 36.30, lat2 = 33.50, lon2 = 36.31;
        (double Lat, double Lon)[] ring =
        [
            (33.4997, 36.3045),
            (33.5003, 36.3045),
            (33.5003, 36.3055),
            (33.4997, 36.3055)
        ];

        PathPolygonRelation rel = LineOfSightMath.RelatePolygonToPath(lat1, lon1, lat2, lon2, ring);
        Assert.True(rel.IntersectsPath || rel.DistanceMeters < 2);
        Assert.InRange(rel.T, 0.02, 0.98);
    }

    [Fact]
    public void RelatePolygonToPath_IgnoresFarBuilding()
    {
        double lat1 = 33.50, lon1 = 36.30, lat2 = 33.50, lon2 = 36.31;
        (double Lat, double Lon)[] ring =
        [
            (33.5030, 36.3045),
            (33.5034, 36.3045),
            (33.5034, 36.3055),
            (33.5030, 36.3055)
        ];

        PathPolygonRelation rel = LineOfSightMath.RelatePolygonToPath(lat1, lon1, lat2, lon2, ring);
        Assert.False(rel.IntersectsPath);
        Assert.True(rel.DistanceMeters > 80);
    }

    [Fact]
    public void CorridorMetersAt_IsAtLeast12m()
    {
        Assert.True(LineOfSightMath.CorridorMetersAt(5800, 500, 1000) >= 12);
    }
}
