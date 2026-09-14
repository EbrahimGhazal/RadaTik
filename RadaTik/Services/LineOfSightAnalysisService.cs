using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RadaTik.ViewModels;

namespace RadaTik.Services;

public interface ILineOfSightAnalysisService
{
    Task<LineOfSightResult> AnalyzeAsync(LineOfSightAnalysisInput input, CancellationToken ct = default);

    /// <summary>ارتفاع سطح البحر (م) عند نقطة واحدة عبر Open-Elevation، أو null عند الفشل.</summary>
    Task<double?> LookupElevationAtAsync(double latitude, double longitude, CancellationToken ct = default);
}

/// <summary>
/// تحليل تقريبي لخط الرؤية: تضاريس، انحناء الأرض، منطقة فريسنل، ومبانٍ من OSM بتقاطع المضلعات.
/// </summary>
public sealed class LineOfSightAnalysisService : ILineOfSightAnalysisService
{
    private const double DefaultSectorAntennaAgl = 12;
    private const double DefaultReceiverAntennaAgl = 6;
    private const int MaxBuildingsForElevation = 80;
    private const int MaxBuildingsReturned = 40;
    private const int OverpassAroundMeters = 90;

    private static readonly string[] OverpassEndpoints =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.private.coffee/api/interpreter"
    ];

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<LineOfSightAnalysisService> _logger;

    public LineOfSightAnalysisService(IHttpClientFactory httpFactory, ILogger<LineOfSightAnalysisService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<double?> LookupElevationAtAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        if (!IsValidLatLng(latitude, longitude))
        {
            return null;
        }

        try
        {
            double[] arr = await FetchElevationsBatchAsync([(latitude, longitude)], ct);
            return arr.Length > 0 ? arr[0] : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LookupElevationAt failed for {Lat},{Lon}", latitude, longitude);
            return null;
        }
    }

    public async Task<LineOfSightResult> AnalyzeAsync(LineOfSightAnalysisInput input, CancellationToken ct = default)
    {
        if (input.SampleCount < 8 || input.SampleCount > 200)
        {
            return new LineOfSightResult { Success = false, ErrorMessage = "عدد العينات يجب أن يكون بين 8 و 200." };
        }

        double lat1 = input.SectorLat;
        double lon1 = input.SectorLon;
        double lat2 = input.ReceiverLat;
        double lon2 = input.ReceiverLon;

        if (!IsValidLatLng(lat1, lon1) || !IsValidLatLng(lat2, lon2))
        {
            return new LineOfSightResult { Success = false, ErrorMessage = "إحداثيات غير صالحة." };
        }

        double dist = LineOfSightMath.HaversineMeters(lat1, lon1, lat2, lon2);
        double frequencyMhz = LineOfSightMath.NormalizeFrequencyMhz(input.FrequencyMhz);
        bool frequencyIsDefault = input.FrequencyMhz < 400;

        if (dist < 5)
        {
            return new LineOfSightResult
            {
                Success = true,
                DistanceMeters = dist,
                PathClear = true,
                TerrainClear = true,
                FresnelClear = true,
                MinTerrainMarginMeters = 999,
                MinFresnelMarginMeters = 999,
                TerrainNote = "المسافة شبه معدومة.",
                FrequencyMhzUsed = frequencyMhz,
                FrequencyIsDefault = frequencyIsDefault,
                EarthCurvatureApplied = true,
                BuildingsDataAvailable = false
            };
        }

        int n = input.SampleCount;
        List<(double Lat, double Lon, double T)> samples = new List<(double Lat, double Lon, double T)>();
        for (int i = 0; i <= n; i++)
        {
            double t = i / (double)n;
            double lat = lat1 + t * (lat2 - lat1);
            double lon = lon1 + t * (lon2 - lon1);
            samples.Add((lat, lon, t));
        }

        double[] elevations;
        try
        {
            elevations = await FetchElevationsBatchAsync(samples.Select(s => (s.Lat, s.Lon)).ToList(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open-Elevation batch failed");
            return new LineOfSightResult
            {
                Success = false,
                ErrorMessage = "تعذر جلب بيانات التضاريس من الخدمة الخارجية."
            };
        }

        if (elevations.Length != samples.Count)
        {
            return new LineOfSightResult { Success = false, ErrorMessage = "عدد نقاط الارتفاع لا يطابق المسار." };
        }

        double sectorAgl = input.SectorAntennaAglMeters > 0 ? input.SectorAntennaAglMeters : DefaultSectorAntennaAgl;
        double recvAgl = input.ReceiverAntennaAglMeters > 0 ? input.ReceiverAntennaAglMeters : DefaultReceiverAntennaAgl;

        double elev0 = input.SectorTerrainElevationMeters ?? elevations[0];
        double elev1 = input.ReceiverTerrainElevationMeters ?? elevations[^1];

        double hStart = elev0 + sectorAgl;
        double hEnd = elev1 + recvAgl;

        List<LosProfilePoint> profile = new List<LosProfilePoint>();
        double minMargin = double.MaxValue;
        double minFresnelMargin = double.MaxValue;
        bool terrainBlocked = false;
        bool fresnelTerrainBlocked = false;

        for (int i = 0; i < samples.Count; i++)
        {
            double t = samples[i].T;
            double dAlong = dist * t;
            double terr = elevations[i];
            double bulge = LineOfSightMath.EarthBulgeMeters(dAlong, dist);
            double effective = terr + bulge;
            double line = hStart + t * (hEnd - hStart);
            double r1 = LineOfSightMath.FirstFresnelRadiusMeters(frequencyMhz, dAlong, dist);
            double fresnelNeed = LineOfSightMath.FresnelClearanceFraction * r1;
            double margin = line - effective;
            double fresnelMargin = margin - fresnelNeed;
            if (margin < minMargin)
            {
                minMargin = margin;
            }

            if (fresnelMargin < minFresnelMargin)
            {
                minFresnelMargin = fresnelMargin;
            }

            if (margin < LineOfSightMath.MinTerrainClearanceM)
            {
                terrainBlocked = true;
            }

            if (fresnelMargin < 0)
            {
                fresnelTerrainBlocked = true;
            }

            profile.Add(new LosProfilePoint
            {
                DistanceFromStartMeters = Math.Round(dAlong, 1),
                TerrainElevationMslMeters = Math.Round(terr, 2),
                EarthBulgeMeters = Math.Round(bulge, 2),
                EffectiveTerrainMslMeters = Math.Round(effective, 2),
                LineHeightMslMeters = Math.Round(line, 2),
                FresnelRadiusMeters = Math.Round(r1, 2),
                FresnelFloorMslMeters = Math.Round(line - fresnelNeed, 2),
                MarginMeters = Math.Round(margin, 2),
                FresnelMarginMeters = Math.Round(fresnelMargin, 2)
            });
        }

        if (minMargin == double.MaxValue)
        {
            minMargin = 0;
        }

        if (minFresnelMargin == double.MaxValue)
        {
            minFresnelMargin = 0;
        }

        List<BuildingObstructionInfo> buildingList = new List<BuildingObstructionInfo>();
        int buildingsConsidered = 0;
        bool buildingsOk = false;
        try
        {
            (buildingList, buildingsConsidered) = await AnalyzeBuildingsAsync(
                lat1, lon1, lat2, lon2, hStart, hEnd, dist, frequencyMhz, ct);
            buildingsOk = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Overpass / buildings analysis failed");
        }

        bool blockedByBuilding = buildingList.Any(b => b.LikelyBlocksLos);
        bool blockedByBuildingFresnel = buildingList.Any(b => b.LikelyBlocksFresnel);
        bool fresnelClear = !fresnelTerrainBlocked && !blockedByBuildingFresnel;
        bool pathClear = !terrainBlocked && fresnelClear;

        string note;
        if (!buildingsOk)
        {
            note = terrainBlocked || fresnelTerrainBlocked
                ? "التضاريس أو منطقة فريسنل قد تحجب المسار. لم تُحمّل بيانات المبانٍ."
                : "خط الرؤية فوق التضاريس ضمن النموذج التقريبي، لكن بيانات المبانٍ غير متاحة.";
        }
        else if (blockedByBuilding)
        {
            note = "يوجد مبنى يعترض خط الرؤية الهندسي وفق مضلع OSM وارتفاع تقديري.";
        }
        else if (blockedByBuildingFresnel || fresnelTerrainBlocked)
        {
            note = "خط الرؤية الهندسي قد يكون مكشوفاً، لكن 60٪ من منطقة فريسنل الأولى غير خالية (تضاريس أو مبنى).";
        }
        else if (terrainBlocked)
        {
            note = "التضاريس قد تحجب خط الرؤية (الهامش الأدنى أقل من المطلوب).";
        }
        else
        {
            note = "خط الرؤية ومنطقة فريسنل ضمن النموذج التقريبي فوق التضاريس والمبانٍ المفحوصة.";
        }

        int measured = buildingList.Count(b => b.HeightSource == BuildingHeightSource.OsmHeight);
        int estimated = buildingList.Count(b => b.HeightSource != BuildingHeightSource.OsmHeight);

        return new LineOfSightResult
        {
            Success = true,
            DistanceMeters = Math.Round(dist, 1),
            PathClear = pathClear,
            TerrainClear = !terrainBlocked,
            FresnelClear = fresnelClear,
            MinTerrainMarginMeters = Math.Round(minMargin, 2),
            MinFresnelMarginMeters = Math.Round(minFresnelMargin, 2),
            TerrainNote = note,
            FrequencyMhzUsed = Math.Round(frequencyMhz, 1),
            FrequencyIsDefault = frequencyIsDefault,
            EarthCurvatureApplied = true,
            BuildingsDataAvailable = buildingsOk,
            BuildingsConsidered = buildingsConsidered,
            BuildingsMeasuredHeightCount = measured,
            BuildingsEstimatedHeightCount = estimated,
            BuildingObstructions = buildingList,
            Profile = profile
        };
    }

    private async Task<(List<BuildingObstructionInfo> Buildings, int Considered)> AnalyzeBuildingsAsync(
        double lat1, double lon1, double lat2, double lon2,
        double hStart, double hEnd,
        double dist,
        double frequencyMhz,
        CancellationToken ct)
    {
        List<OsmBuildingWay> ways = await FetchOsmBuildingsAlongPathAsync(lat1, lon1, lat2, lon2, ct);
        if (ways.Count == 0)
        {
            return ([], 0);
        }

        List<(OsmBuildingWay Way, PathPolygonRelation Rel, BuildingHeightEstimate Height)> candidates = [];
        foreach (OsmBuildingWay way in ways)
        {
            PathPolygonRelation rel = LineOfSightMath.RelatePolygonToPath(lat1, lon1, lat2, lon2, way.Ring);
            if (rel.T is < 0.02 or > 0.98 || double.IsInfinity(rel.DistanceMeters))
            {
                continue;
            }

            double corridor = LineOfSightMath.CorridorMetersAt(frequencyMhz, dist * rel.T, dist);
            if (!rel.IntersectsPath && rel.DistanceMeters > corridor)
            {
                continue;
            }

            BuildingHeightEstimate height = LineOfSightMath.EstimateBuildingHeight(way.Tags);
            candidates.Add((way, rel, height));
        }

        if (candidates.Count == 0)
        {
            return ([], 0);
        }

        candidates.Sort((a, b) => a.Rel.DistanceMeters.CompareTo(b.Rel.DistanceMeters));
        List<(OsmBuildingWay Way, PathPolygonRelation Rel, BuildingHeightEstimate Height)> top =
            candidates.Take(MaxBuildingsForElevation).ToList();

        List<(double lat, double lon)> elevPoints = top.Select(c => (c.Rel.ClosestLat, c.Rel.ClosestLon)).ToList();
        double[] bElev = await FetchElevationsBatchAsync(elevPoints, ct);

        List<BuildingObstructionInfo> scored = new List<BuildingObstructionInfo>();
        for (int i = 0; i < top.Count; i++)
        {
            (OsmBuildingWay way, PathPolygonRelation rel, BuildingHeightEstimate height) = top[i];
            double g = bElev[i];
            double roof = g + height.HeightMeters;
            double line = hStart + rel.T * (hEnd - hStart);
            double dAlong = dist * rel.T;
            double r1 = LineOfSightMath.FirstFresnelRadiusMeters(frequencyMhz, dAlong, dist);
            double fresnelFloor = line - (LineOfSightMath.FresnelClearanceFraction * r1);
            bool blocksLos = roof > line + LineOfSightMath.GeometricBlockSlackM;
            bool blocksFresnel = roof > fresnelFloor + LineOfSightMath.GeometricBlockSlackM;
            way.Tags.TryGetValue("building", out string? buildingType);

            scored.Add(new BuildingObstructionInfo
            {
                Lat = Math.Round(rel.ClosestLat, 6),
                Lon = Math.Round(rel.ClosestLon, 6),
                EstimatedBuildingHeightMeters = Math.Round(height.HeightMeters, 2),
                HeightSource = height.Source,
                HeightConfidence = height.Confidence,
                BuildingType = string.IsNullOrWhiteSpace(buildingType) ? null : buildingType,
                GroundElevationMslMeters = Math.Round(g, 2),
                RoofMslMeters = Math.Round(roof, 2),
                PathFraction = Math.Round(rel.T, 4),
                CrossTrackMeters = Math.Round(rel.DistanceMeters, 1),
                IntersectsPath = rel.IntersectsPath,
                LineHeightAtPointMslMeters = Math.Round(line, 2),
                FresnelRadiusMeters = Math.Round(r1, 2),
                LikelyBlocksLos = blocksLos,
                LikelyBlocksFresnel = blocksFresnel
            });
        }

        scored.Sort((a, b) =>
        {
            int blockA = a.LikelyBlocksLos ? 0 : a.LikelyBlocksFresnel ? 1 : 2;
            int blockB = b.LikelyBlocksLos ? 0 : b.LikelyBlocksFresnel ? 1 : 2;
            int cmp = blockA.CompareTo(blockB);
            if (cmp != 0)
            {
                return cmp;
            }

            double spareA = a.LineHeightAtPointMslMeters - a.RoofMslMeters;
            double spareB = b.LineHeightAtPointMslMeters - b.RoofMslMeters;
            return spareA.CompareTo(spareB);
        });

        List<BuildingObstructionInfo> returned = new List<BuildingObstructionInfo>();
        foreach (BuildingObstructionInfo b in scored)
        {
            bool nearMiss = !b.LikelyBlocksFresnel &&
                            (b.RoofMslMeters + 3) >= (b.LineHeightAtPointMslMeters - LineOfSightMath.FresnelClearanceFraction * b.FresnelRadiusMeters);
            if (b.LikelyBlocksLos || b.LikelyBlocksFresnel || nearMiss)
            {
                returned.Add(b);
            }

            if (returned.Count >= MaxBuildingsReturned)
            {
                break;
            }
        }

        List<BuildingObstructionInfo> result = returned.Count > 0 ? returned : scored.Take(8).ToList();
        return (result, candidates.Count);
    }

    private async Task<List<OsmBuildingWay>> FetchOsmBuildingsAlongPathAsync(
        double lat1, double lon1, double lat2, double lon2, CancellationToken ct)
    {
        StringBuilder aroundPts = new StringBuilder();
        const int polySamples = 10;
        for (int i = 0; i <= polySamples; i++)
        {
            double t = i / (double)polySamples;
            double lat = lat1 + t * (lat2 - lat1);
            double lon = lon1 + t * (lon2 - lon1);
            aroundPts.Append(CultureInfo.InvariantCulture, $",{lat},{lon}");
        }

        string query = $"""
            [out:json][timeout:25];
            way["building"](around:{OverpassAroundMeters}{aroundPts});
            out geom tags;
            """;

        HttpClient client = _httpFactory.CreateClient("Overpass");
        foreach (string endpoint in OverpassEndpoints)
        {
            try
            {
                using HttpResponseMessage response = await client.PostAsync(
                    endpoint,
                    new StringContent("data=" + Uri.EscapeDataString(query), Encoding.UTF8, "application/x-www-form-urlencoded"),
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Overpass {Endpoint} returned {Status}", endpoint, (int)response.StatusCode);
                    continue;
                }

                await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
                using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("elements", out JsonElement elements))
                {
                    continue;
                }

                List<OsmBuildingWay> ways = ParseOsmBuildingWays(elements);
                _logger.LogDebug("Overpass {Endpoint} returned {Count} building ways", endpoint, ways.Count);
                return ways;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Overpass endpoint {Endpoint} failed", endpoint);
            }
        }

        throw new InvalidOperationException("All Overpass endpoints failed.");
    }

    private static List<OsmBuildingWay> ParseOsmBuildingWays(JsonElement elements)
    {
        List<OsmBuildingWay> ways = [];
        foreach (JsonElement el in elements.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!el.TryGetProperty("geometry", out JsonElement geom) || geom.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            List<(double Lat, double Lon)> ring = [];
            foreach (JsonElement pt in geom.EnumerateArray())
            {
                if (pt.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!pt.TryGetProperty("lat", out JsonElement latProp) || !pt.TryGetProperty("lon", out JsonElement lonProp))
                {
                    continue;
                }

                ring.Add((latProp.GetDouble(), lonProp.GetDouble()));
            }

            if (ring.Count < 2)
            {
                continue;
            }

            Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (el.TryGetProperty("tags", out JsonElement tagsEl) && tagsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in tagsEl.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String)
                    {
                        tags[p.Name] = p.Value.GetString() ?? "";
                    }
                    else if (p.Value.ValueKind is JsonValueKind.Number)
                    {
                        tags[p.Name] = p.Value.ToString();
                    }
                }
            }

            ways.Add(new OsmBuildingWay { Tags = tags, Ring = ring });
        }

        return ways;
    }

    private async Task<double[]> FetchElevationsBatchAsync(IReadOnlyList<(double Lat, double Lon)> points, CancellationToken ct)
    {
        HttpClient client = _httpFactory.CreateClient("OpenElevation");
        OpenElevationRequestDto payload = new OpenElevationRequestDto
        {
            locations = points.Select(p => new OpenElevationLocationDto { latitude = p.Lat, longitude = p.Lon }).ToList()
        };

        using HttpResponseMessage resp = await client.PostAsJsonAsync("https://api.open-elevation.com/api/v1/lookup", payload, ct);
        resp.EnsureSuccessStatusCode();
        await using Stream stream = await resp.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("results", out JsonElement results))
        {
            throw new InvalidOperationException("Invalid elevation response");
        }

        double[] list = new double[points.Count];
        int i = 0;
        foreach (JsonElement r in results.EnumerateArray())
        {
            if (i >= list.Length)
            {
                break;
            }

            list[i++] = r.GetProperty("elevation").GetDouble();
        }

        if (i != list.Length)
        {
            throw new InvalidOperationException("Elevation count mismatch");
        }

        return list;
    }

    private sealed class OsmBuildingWay
    {
        public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(double Lat, double Lon)> Ring { get; init; } = [];
    }

    private sealed class OpenElevationLocationDto
    {
        public double latitude { get; init; }
        public double longitude { get; init; }
    }

    private sealed class OpenElevationRequestDto
    {
        public List<OpenElevationLocationDto> locations { get; init; } = null!;
    }

    private static bool IsValidLatLng(double lat, double lon) =>
        lat is >= -90 and <= 90 && lon is >= -180 and <= 180;
}
