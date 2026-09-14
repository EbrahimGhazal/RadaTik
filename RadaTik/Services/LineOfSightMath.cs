namespace RadaTik.Services;

/// <summary>
/// حسابات خط الرؤية: فريسنل، انحناء الأرض، تقاطع المضلعات، وتقدير ارتفاع المبنى.
/// </summary>
public static class LineOfSightMath
{
    public const double SpeedOfLightMps = 299_792_458;
    public const double DefaultFrequencyMhz = 5800;
    public const double FresnelClearanceFraction = 0.60;
    public const double EarthRadiusMeters = 6_371_000;
    public const double KFactor = 4.0 / 3.0;
    public const double MetersPerDegreeLat = 111_320;
    public const double MinTerrainClearanceM = 2;
    public const double GeometricBlockSlackM = 0.5;
    public const double DefaultGuessBuildingHeightM = 12;
    public const double MetersPerBuildingLevel = 3.2;
    public const double DefaultForestHeightM = 16;
    public const double DefaultTreeHeightM = 12;
    public const double DefaultTreeRowHeightM = 10;
    public const double DefaultOrchardHeightM = 7;
    public const double DefaultScrubHeightM = 3.5;

    public static double NormalizeFrequencyMhz(double frequencyMhz) =>
        frequencyMhz is >= 400 and <= 90_000 ? frequencyMhz : DefaultFrequencyMhz;

    public static double WavelengthMeters(double frequencyMhz)
    {
        double fHz = NormalizeFrequencyMhz(frequencyMhz) * 1_000_000;
        return SpeedOfLightMps / fHz;
    }

    /// <summary>نصف قطر منطقة فريسنل الأولى عند مسافة d من المرسل.</summary>
    public static double FirstFresnelRadiusMeters(double frequencyMhz, double distanceAlongMeters, double totalDistanceMeters)
    {
        if (totalDistanceMeters < 1 || distanceAlongMeters <= 0 || distanceAlongMeters >= totalDistanceMeters)
        {
            return 0;
        }

        double lambda = WavelengthMeters(frequencyMhz);
        double d1 = distanceAlongMeters;
        double d2 = totalDistanceMeters - distanceAlongMeters;
        return Math.Sqrt(lambda * d1 * d2 / totalDistanceMeters);
    }

    /// <summary>انتفاخ الأرض الفعلي (عامل k = 4/3) بالمتر عند موضع على المسار.</summary>
    public static double EarthBulgeMeters(double distanceAlongMeters, double totalDistanceMeters)
    {
        if (totalDistanceMeters < 1 || distanceAlongMeters <= 0 || distanceAlongMeters >= totalDistanceMeters)
        {
            return 0;
        }

        double d1 = distanceAlongMeters;
        double d2 = totalDistanceMeters - distanceAlongMeters;
        return d1 * d2 / (2 * KFactor * EarthRadiusMeters);
    }

    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = EarthRadiusMeters;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    public static (double X, double Y) ToLocalMeters(double lat1, double lon1, double lat, double lon)
    {
        double x = (lon - lon1) * Math.Cos(lat1 * Math.PI / 180) * MetersPerDegreeLat;
        double y = (lat - lat1) * MetersPerDegreeLat;
        return (x, y);
    }

    public static (double Lat, double Lon) FromLocalMeters(double lat1, double lon1, double x, double y)
    {
        double lat = lat1 + y / MetersPerDegreeLat;
        double lon = lon1 + x / (Math.Cos(lat1 * Math.PI / 180) * MetersPerDegreeLat);
        return (lat, lon);
    }

    /// <summary>إسقاط نقطة على المسار: t على [0,1] والمسافة العمودية بالمتر.</summary>
    public static (double t, double crossM) ProjectOntoPath(
        double lat1, double lon1, double lat2, double lon2,
        double plat, double plon)
    {
        (double px, double py) = ToLocalMeters(lat1, lon1, plat, plon);
        (double bx, double by) = ToLocalMeters(lat1, lon1, lat2, lon2);
        (double t, double dist) = PointToSegment(px, py, 0, 0, bx, by);
        return (t, dist);
    }

    public static PathPolygonRelation RelatePolygonToPath(
        double lat1, double lon1, double lat2, double lon2,
        IReadOnlyList<(double Lat, double Lon)> ring)
    {
        if (ring.Count == 1)
        {
            (double tPt, double distPt) = ProjectOntoPath(lat1, lon1, lat2, lon2, ring[0].Lat, ring[0].Lon);
            return new PathPolygonRelation(tPt, distPt, ring[0].Lat, ring[0].Lon, distPt < 0.35);
        }

        if (ring.Count < 2)
        {
            return new PathPolygonRelation(0.5, double.PositiveInfinity, lat1, lon1, false);
        }

        List<(double X, double Y)> xy = new List<(double X, double Y)>(ring.Count);
        foreach ((double lat, double lon) in ring)
        {
            xy.Add(ToLocalMeters(lat1, lon1, lat, lon));
        }

        if (xy.Count >= 3)
        {
            (double fx, double fy) = xy[0];
            (double lx, double ly) = xy[^1];
            if (Math.Abs(fx - lx) > 0.4 || Math.Abs(fy - ly) > 0.4)
            {
                xy.Add(xy[0]);
            }
        }

        (double bx, double by) = ToLocalMeters(lat1, lon1, lat2, lon2);
        double bestDist = double.PositiveInfinity;
        double bestT = 0.5;
        double bestX = bx * 0.5;
        double bestY = by * 0.5;

        void Consider(double t, double dist, double x, double y)
        {
            if (dist < bestDist)
            {
                bestDist = dist;
                bestT = t;
                bestX = x;
                bestY = y;
            }
        }

        for (int i = 0; i < xy.Count - 1; i++)
        {
            (double cx, double cy) = xy[i];
            (double dx, double dy) = xy[i + 1];
            if (TrySegmentIntersection(0, 0, bx, by, cx, cy, dx, dy, out double tHit, out double uHit))
            {
                double ix = cx + uHit * (dx - cx);
                double iy = cy + uHit * (dy - cy);
                (double hitLat, double hitLon) = FromLocalMeters(lat1, lon1, ix, iy);
                return new PathPolygonRelation(Math.Clamp(tHit, 0, 1), 0, hitLat, hitLon, true);
            }

            (double tA, double distA, double ax, double ay) = ClosestPointsOnSegments(0, 0, bx, by, cx, cy, dx, dy);
            Consider(tA, distA, ax, ay);
        }

        for (int i = 0; i < xy.Count; i++)
        {
            (double px, double py) = xy[i];
            (double t, double dist) = PointToSegment(px, py, 0, 0, bx, by);
            Consider(t, dist, px, py);
        }

        if (xy.Count >= 4)
        {
            for (int s = 1; s <= 24; s++)
            {
                double t = s / 25.0;
                double sx = t * bx;
                double sy = t * by;
                if (PointInRing(sx, sy, xy))
                {
                    (double insideLat, double insideLon) = FromLocalMeters(lat1, lon1, sx, sy);
                    return new PathPolygonRelation(t, 0, insideLat, insideLon, true);
                }
            }
        }

        if (double.IsPositiveInfinity(bestDist))
        {
            return new PathPolygonRelation(0.5, double.PositiveInfinity, lat1, lon1, false);
        }

        (double clat, double clon) = FromLocalMeters(lat1, lon1, bestX, bestY);
        return new PathPolygonRelation(Math.Clamp(bestT, 0, 1), bestDist, clat, clon, bestDist < 0.35);
    }

    public static BuildingHeightEstimate EstimateBuildingHeight(IReadOnlyDictionary<string, string> tags)
    {
        if (TryGetTag(tags, "height", out string? heightRaw) ||
            TryGetTag(tags, "building:height", out heightRaw))
        {
            if (TryParseMeters(heightRaw, out double height))
            {
                return new BuildingHeightEstimate(
                    Math.Clamp(height, 2, 400),
                    BuildingHeightSource.OsmHeight,
                    BuildingHeightConfidence.High);
            }
        }

        if (TryGetTag(tags, "building:levels", out string? levelsRaw) &&
            TryParseLeadingNumber(levelsRaw, out double levels) &&
            levels > 0)
        {
            return new BuildingHeightEstimate(
                Math.Clamp(levels * MetersPerBuildingLevel, 3, 200),
                BuildingHeightSource.Levels,
                BuildingHeightConfidence.Medium);
        }

        string type = "yes";
        if (TryGetTag(tags, "building", out string? buildingType) && !string.IsNullOrWhiteSpace(buildingType))
        {
            type = buildingType.Trim();
        }

        return new BuildingHeightEstimate(
            GuessHeightFromBuildingType(type),
            BuildingHeightSource.BuildingType,
            BuildingHeightConfidence.Low);
    }

    public static string? ClassifyOsmObstacle(IReadOnlyDictionary<string, string> tags)
    {
        if (TryGetTag(tags, "building", out string? building) &&
            !string.IsNullOrWhiteSpace(building) &&
            !building.Equals("no", StringComparison.OrdinalIgnoreCase) &&
            !building.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return LosObstacleKind.Building;
        }

        if (TryGuessVegetationHeight(tags, out _))
        {
            return LosObstacleKind.Vegetation;
        }

        return null;
    }

    public static BuildingHeightEstimate EstimateVegetationHeight(IReadOnlyDictionary<string, string> tags)
    {
        if (TryGetTag(tags, "height", out string? heightRaw) && TryParseMeters(heightRaw, out double height))
        {
            return new BuildingHeightEstimate(
                Math.Clamp(height, 1, 80),
                BuildingHeightSource.OsmHeight,
                BuildingHeightConfidence.High);
        }

        if (TryGuessVegetationHeight(tags, out double guessed))
        {
            return new BuildingHeightEstimate(
                guessed,
                BuildingHeightSource.VegetationType,
                BuildingHeightConfidence.Low);
        }

        return new BuildingHeightEstimate(
            DefaultTreeHeightM,
            BuildingHeightSource.VegetationType,
            BuildingHeightConfidence.Low);
    }

    public static bool TryGuessVegetationHeight(IReadOnlyDictionary<string, string> tags, out double heightMeters)
    {
        heightMeters = 0;
        if (TryGetTag(tags, "natural", out string? natural) && !string.IsNullOrWhiteSpace(natural))
        {
            heightMeters = natural.Trim().ToLowerInvariant() switch
            {
                "wood" => DefaultForestHeightM,
                "tree" => DefaultTreeHeightM,
                "tree_row" => DefaultTreeRowHeightM,
                "scrub" => DefaultScrubHeightM,
                _ => 0
            };
            if (heightMeters > 0)
            {
                return true;
            }
        }

        if (TryGetTag(tags, "landuse", out string? landuse) && !string.IsNullOrWhiteSpace(landuse))
        {
            heightMeters = landuse.Trim().ToLowerInvariant() switch
            {
                "forest" => DefaultForestHeightM,
                "orchard" => DefaultOrchardHeightM,
                _ => 0
            };
            if (heightMeters > 0)
            {
                return true;
            }
        }

        return false;
    }

    public static double GuessHeightFromBuildingType(string buildingType)
    {
        string key = (buildingType ?? "yes").Trim().ToLowerInvariant();
        return key switch
        {
            "house" or "detached" or "semidetached_house" or "terrace" or "bungalow"
                or "static_caravan" or "houseboat" => 7,
            "apartments" => 18,
            "residential" => 12,
            "garage" or "garages" or "shed" or "hut" or "cabin" or "kiosk"
                or "service" or "carport" or "ruins" => 3.5,
            "greenhouse" => 4,
            "industrial" or "warehouse" or "hangar" => 10,
            "retail" or "commercial" or "shop" or "supermarket" => 10,
            "office" => 16,
            "hotel" => 20,
            "hospital" or "school" or "university" or "public" or "civic" => 12,
            "church" or "cathedral" or "mosque" or "synagogue" => 14,
            "construction" => 9,
            "farm" or "farm_auxiliary" or "barn" => 6,
            _ => DefaultGuessBuildingHeightM
        };
    }

    public static bool TryParseMeters(string? raw, out double meters)
    {
        meters = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string s = raw.Trim();
        int cut = s.IndexOf(';');
        if (cut >= 0)
        {
            s = s[..cut];
        }

        s = s.Replace("m", "", StringComparison.OrdinalIgnoreCase)
            .Replace("متر", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        return TryParseLeadingNumber(s, out meters) && meters > 0;
    }

    public static double CorridorMetersAt(double frequencyMhz, double distanceAlongMeters, double totalDistanceMeters)
    {
        double r = FirstFresnelRadiusMeters(frequencyMhz, distanceAlongMeters, totalDistanceMeters);
        return Math.Max(12, (r * FresnelClearanceFraction) + 8);
    }

    private static bool TryGetTag(IReadOnlyDictionary<string, string> tags, string key, out string? value)
    {
        foreach (KeyValuePair<string, string> pair in tags)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryParseLeadingNumber(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string s = raw.Trim();
        int i = 0;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] is '.' or ',' or '-' or '+'))
        {
            i++;
        }

        if (i == 0)
        {
            return false;
        }

        string num = s[..i].Replace(',', '.');
        return double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static (double t, double dist) PointToSegment(
        double px, double py, double ax, double ay, double bx, double by)
    {
        double abx = bx - ax;
        double aby = by - ay;
        double ab2 = abx * abx + aby * aby;
        if (ab2 < 1e-8)
        {
            double dx0 = px - ax;
            double dy0 = py - ay;
            return (0, Math.Sqrt(dx0 * dx0 + dy0 * dy0));
        }

        double t = ((px - ax) * abx + (py - ay) * aby) / ab2;
        t = Math.Clamp(t, 0, 1);
        double qx = ax + t * abx;
        double qy = ay + t * aby;
        double dx = px - qx;
        double dy = py - qy;
        return (t, Math.Sqrt(dx * dx + dy * dy));
    }

    private static (double tAb, double dist, double x, double y) ClosestPointsOnSegments(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy)
    {
        (double tC, double distC) = PointToSegment(cx, cy, ax, ay, bx, by);
        (double tD, double distD) = PointToSegment(dx, dy, ax, ay, bx, by);
        (double uA, double distA) = PointToSegment(ax, ay, cx, cy, dx, dy);
        (double uB, double distB) = PointToSegment(bx, by, cx, cy, dx, dy);

        double best = distC;
        double t = tC;
        double x = cx;
        double y = cy;

        if (distD < best)
        {
            best = distD;
            t = tD;
            x = dx;
            y = dy;
        }

        if (distA < best)
        {
            best = distA;
            t = 0;
            x = ax;
            y = ay;
        }

        if (distB < best)
        {
            best = distB;
            t = 1;
            x = bx;
            y = by;
        }

        return (t, best, x, y);
    }

    private static bool TrySegmentIntersection(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy,
        out double tAb,
        out double uCd)
    {
        tAb = 0;
        uCd = 0;
        double rx = bx - ax;
        double ry = by - ay;
        double sx = dx - cx;
        double sy = dy - cy;
        double det = rx * sy - ry * sx;
        if (Math.Abs(det) < 1e-12)
        {
            return false;
        }

        double qpx = cx - ax;
        double qpy = cy - ay;
        double t = (qpx * sy - qpy * sx) / det;
        double u = (qpx * ry - qpy * rx) / det;
        if (t is < 0 or > 1 || u is < 0 or > 1)
        {
            return false;
        }

        tAb = t;
        uCd = u;
        return true;
    }

    private static bool PointInRing(double x, double y, IReadOnlyList<(double X, double Y)> ring)
    {
        bool inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            (double xi, double yi) = ring[i];
            (double xj, double yj) = ring[j];
            if ((yi > y) == (yj > y))
            {
                continue;
            }

            double denom = yj - yi;
            if (Math.Abs(denom) < 1e-15)
            {
                continue;
            }

            if (x < (xj - xi) * (y - yi) / denom + xi)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}

public readonly record struct PathPolygonRelation(
    double T,
    double DistanceMeters,
    double ClosestLat,
    double ClosestLon,
    bool IntersectsPath);

public readonly record struct BuildingHeightEstimate(
    double HeightMeters,
    string Source,
    string Confidence);

public static class BuildingHeightSource
{
    public const string OsmHeight = "osm_height";
    public const string Levels = "levels";
    public const string BuildingType = "building_type";
    public const string VegetationType = "vegetation_type";
}

public static class LosObstacleKind
{
    public const string Building = "building";
    public const string Vegetation = "vegetation";
}

public static class BuildingHeightConfidence
{
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";
}
