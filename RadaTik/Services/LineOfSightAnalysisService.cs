using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using RadaTik.ViewModels;

namespace RadaTik.Services;

public interface ILineOfSightAnalysisService
{
    Task<LineOfSightResult> AnalyzeAsync(LineOfSightAnalysisInput input, CancellationToken ct = default);

    /// <summary>ارتفاع سطح البحر (م) عند نقطة واحدة عبر Open-Elevation، أو null عند الفشل.</summary>
    Task<double?> LookupElevationAtAsync(double latitude, double longitude, CancellationToken ct = default);
}

/// <summary>
/// تحليل تقريبي لخط الرؤية: تضاريس، فريسنل، مبانٍ وغطاء نباتي من OSM، مع تخزين مؤقت للخدمات الخارجية.
/// </summary>
public sealed class LineOfSightAnalysisService : ILineOfSightAnalysisService
{
    private const double DefaultSectorAntennaAgl = 12;
    private const double DefaultReceiverAntennaAgl = 6;
    private const int MaxObstaclesForElevation = 80;
    private const int MaxObstaclesReturned = 40;
    private const int OverpassAroundMeters = 90;

    private static readonly TimeSpan ElevationCacheTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan OsmCacheTtl = TimeSpan.FromHours(6);

    private static readonly string[] OverpassEndpoints =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.private.coffee/api/interpreter"
    ];

    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LineOfSightAnalysisService> _logger;

    public LineOfSightAnalysisService(
        IHttpClientFactory httpFactory,
        IMemoryCache cache,
        ILogger<LineOfSightAnalysisService> logger)
    {
        _httpFactory = httpFactory;
        _cache = cache;
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
        string frequencySource = string.IsNullOrWhiteSpace(input.FrequencySource) ? "default" : input.FrequencySource;
        if (input.FrequencyMhz < 400)
        {
            frequencySource = "default";
        }

        bool frequencyIsDefault = frequencySource == "default";

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
                FrequencySource = frequencySource,
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

        List<BuildingObstructionInfo> obstacleList = [];
        int buildingsConsidered = 0;
        int vegetationConsidered = 0;
        bool osmOk = false;
        try
        {
            (obstacleList, buildingsConsidered, vegetationConsidered) = await AnalyzeOsmObstaclesAsync(
                lat1, lon1, lat2, lon2, hStart, hEnd, dist, frequencyMhz, ct);
            osmOk = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Overpass / OSM obstacle analysis failed");
        }

        bool blockedByBuilding = obstacleList.Any(b => b.ObstacleKind == LosObstacleKind.Building && b.LikelyBlocksLos);
        bool blockedByVegetation = obstacleList.Any(b => b.ObstacleKind == LosObstacleKind.Vegetation && b.LikelyBlocksLos);
        bool blockedByObstacleFresnel = obstacleList.Any(b => b.LikelyBlocksFresnel);
        bool fresnelClear = !fresnelTerrainBlocked && !blockedByObstacleFresnel;
        bool pathClear = !terrainBlocked && fresnelClear;

        string note;
        if (!osmOk)
        {
            note = terrainBlocked || fresnelTerrainBlocked
                ? "التضاريس أو منطقة فريسنل قد تحجب المسار. لم تُحمّل بيانات المبانٍ والغطاء النباتي."
                : "خط الرؤية فوق التضاريس ضمن النموذج التقريبي، لكن بيانات OSM غير متاحة.";
        }
        else if (blockedByBuilding)
        {
            note = "يوجد مبنى يعترض خط الرؤية الهندسي وفق مضلع OSM وارتفاع تقديري.";
        }
        else if (blockedByVegetation)
        {
            note = "يوجد غطاء نباتي (شجر/غابة) قد يعترض خط الرؤية وفق بيانات OSM التقديرية.";
        }
        else if (blockedByObstacleFresnel || fresnelTerrainBlocked)
        {
            note = "خط الرؤية الهندسي قد يكون مكشوفاً، لكن 60٪ من منطقة فريسنل الأولى غير خالية (تضاريس أو مبنى أو شجر).";
        }
        else if (terrainBlocked)
        {
            note = "التضاريس قد تحجب خط الرؤية (الهامش الأدنى أقل من المطلوب).";
        }
        else
        {
            note = "خط الرؤية ومنطقة فريسنل ضمن النموذج التقريبي فوق التضاريس والمبانٍ والغطاء النباتي المفحوص.";
        }

        int measured = obstacleList.Count(b => b.HeightSource == BuildingHeightSource.OsmHeight);
        int estimated = obstacleList.Count(b => b.HeightSource != BuildingHeightSource.OsmHeight);

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
            FrequencySource = frequencySource,
            EarthCurvatureApplied = true,
            BuildingsDataAvailable = osmOk,
            BuildingsConsidered = buildingsConsidered,
            VegetationConsidered = vegetationConsidered,
            BuildingsMeasuredHeightCount = measured,
            BuildingsEstimatedHeightCount = estimated,
            BuildingObstructions = obstacleList,
            Profile = profile
        };
    }

    private async Task<(List<BuildingObstructionInfo> Obstacles, int BuildingsConsidered, int VegetationConsidered)> AnalyzeOsmObstaclesAsync(
        double lat1, double lon1, double lat2, double lon2,
        double hStart, double hEnd,
        double dist,
        double frequencyMhz,
        CancellationToken ct)
    {
        List<OsmFeature> features = await FetchOsmFeaturesAlongPathAsync(lat1, lon1, lat2, lon2, ct);
        if (features.Count == 0)
        {
            return ([], 0, 0);
        }

        List<(OsmFeature Feature, PathPolygonRelation Rel, BuildingHeightEstimate Height)> candidates = [];
        int buildingsConsidered = 0;
        int vegetationConsidered = 0;
        foreach (OsmFeature feature in features)
        {
            PathPolygonRelation rel = LineOfSightMath.RelatePolygonToPath(lat1, lon1, lat2, lon2, feature.Ring);
            if (rel.T is < 0.02 or > 0.98 || double.IsInfinity(rel.DistanceMeters))
            {
                continue;
            }

            double corridor = LineOfSightMath.CorridorMetersAt(frequencyMhz, dist * rel.T, dist);
            if (!rel.IntersectsPath && rel.DistanceMeters > corridor)
            {
                continue;
            }

            BuildingHeightEstimate height = feature.Kind == LosObstacleKind.Vegetation
                ? LineOfSightMath.EstimateVegetationHeight(feature.Tags)
                : LineOfSightMath.EstimateBuildingHeight(feature.Tags);
            candidates.Add((feature, rel, height));
            if (feature.Kind == LosObstacleKind.Vegetation)
            {
                vegetationConsidered++;
            }
            else
            {
                buildingsConsidered++;
            }
        }

        if (candidates.Count == 0)
        {
            return ([], 0, 0);
        }

        candidates.Sort((a, b) => a.Rel.DistanceMeters.CompareTo(b.Rel.DistanceMeters));
        List<(OsmFeature Feature, PathPolygonRelation Rel, BuildingHeightEstimate Height)> top =
            candidates.Take(MaxObstaclesForElevation).ToList();

        List<(double lat, double lon)> elevPoints = top.Select(c => (c.Rel.ClosestLat, c.Rel.ClosestLon)).ToList();
        double[] bElev = await FetchElevationsBatchAsync(elevPoints, ct);

        List<BuildingObstructionInfo> scored = [];
        for (int i = 0; i < top.Count; i++)
        {
            (OsmFeature feature, PathPolygonRelation rel, BuildingHeightEstimate height) = top[i];
            double g = bElev[i];
            double roof = g + height.HeightMeters;
            double line = hStart + rel.T * (hEnd - hStart);
            double dAlong = dist * rel.T;
            double r1 = LineOfSightMath.FirstFresnelRadiusMeters(frequencyMhz, dAlong, dist);
            double fresnelFloor = line - (LineOfSightMath.FresnelClearanceFraction * r1);
            bool blocksLos = roof > line + LineOfSightMath.GeometricBlockSlackM;
            bool blocksFresnel = roof > fresnelFloor + LineOfSightMath.GeometricBlockSlackM;
            string? typeLabel = feature.Kind == LosObstacleKind.Vegetation
                ? VegetationTypeLabel(feature.Tags)
                : (feature.Tags.TryGetValue("building", out string? buildingType) ? buildingType : null);

            scored.Add(new BuildingObstructionInfo
            {
                Lat = Math.Round(rel.ClosestLat, 6),
                Lon = Math.Round(rel.ClosestLon, 6),
                EstimatedBuildingHeightMeters = Math.Round(height.HeightMeters, 2),
                HeightSource = height.Source,
                HeightConfidence = height.Confidence,
                ObstacleKind = feature.Kind,
                BuildingType = string.IsNullOrWhiteSpace(typeLabel) ? null : typeLabel,
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

        List<BuildingObstructionInfo> returned = [];
        foreach (BuildingObstructionInfo b in scored)
        {
            bool nearMiss = !b.LikelyBlocksFresnel &&
                            (b.RoofMslMeters + 3) >= (b.LineHeightAtPointMslMeters - LineOfSightMath.FresnelClearanceFraction * b.FresnelRadiusMeters);
            if (b.LikelyBlocksLos || b.LikelyBlocksFresnel || nearMiss)
            {
                returned.Add(b);
            }

            if (returned.Count >= MaxObstaclesReturned)
            {
                break;
            }
        }

        List<BuildingObstructionInfo> result = returned.Count > 0 ? returned : scored.Take(8).ToList();
        return (result, buildingsConsidered, vegetationConsidered);
    }

    private async Task<List<OsmFeature>> FetchOsmFeaturesAlongPathAsync(
        double lat1, double lon1, double lat2, double lon2, CancellationToken ct)
    {
        string cacheKey = OsmCacheKey(lat1, lon1, lat2, lon2);
        if (_cache.TryGetValue(cacheKey, out List<OsmFeature>? cached) && cached != null)
        {
            return cached;
        }

        StringBuilder aroundPts = new StringBuilder();
        const int polySamples = 10;
        for (int i = 0; i <= polySamples; i++)
        {
            double t = i / (double)polySamples;
            double lat = lat1 + t * (lat2 - lat1);
            double lon = lon1 + t * (lon2 - lon1);
            aroundPts.Append(CultureInfo.InvariantCulture, $",{lat},{lon}");
        }

        string around = aroundPts.ToString();
        string query = $"""
            [out:json][timeout:28];
            (
              way["building"](around:{OverpassAroundMeters}{around});
              way["natural"~"^(wood|tree_row|scrub)$"](around:{OverpassAroundMeters}{around});
              way["landuse"~"^(forest|orchard)$"](around:{OverpassAroundMeters}{around});
              node["natural"="tree"](around:{OverpassAroundMeters}{around});
            );
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

                List<OsmFeature> features = ParseOsmFeatures(elements);
                _cache.Set(cacheKey, features, OsmCacheTtl);
                _logger.LogDebug("Overpass {Endpoint} returned {Count} OSM features", endpoint, features.Count);
                return features;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Overpass endpoint {Endpoint} failed", endpoint);
            }
        }

        throw new InvalidOperationException("All Overpass endpoints failed.");
    }

    private static List<OsmFeature> ParseOsmFeatures(JsonElement elements)
    {
        List<OsmFeature> features = [];
        foreach (JsonElement el in elements.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            Dictionary<string, string> tags = ReadTags(el);
            string? kind = LineOfSightMath.ClassifyOsmObstacle(tags);
            if (kind == null)
            {
                continue;
            }

            List<(double Lat, double Lon)> ring = [];
            if (el.TryGetProperty("geometry", out JsonElement geom) && geom.ValueKind == JsonValueKind.Array)
            {
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
            }
            else if (el.TryGetProperty("lat", out JsonElement nlat) && el.TryGetProperty("lon", out JsonElement nlon))
            {
                ring.Add((nlat.GetDouble(), nlon.GetDouble()));
            }

            if (ring.Count < 1)
            {
                continue;
            }

            features.Add(new OsmFeature { Tags = tags, Ring = ring, Kind = kind });
        }

        return features;
    }

    private static Dictionary<string, string> ReadTags(JsonElement el)
    {
        Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!el.TryGetProperty("tags", out JsonElement tagsEl) || tagsEl.ValueKind != JsonValueKind.Object)
        {
            return tags;
        }

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

        return tags;
    }

    private static string? VegetationTypeLabel(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.TryGetValue("natural", out string? natural) && !string.IsNullOrWhiteSpace(natural))
        {
            return natural;
        }

        if (tags.TryGetValue("landuse", out string? landuse) && !string.IsNullOrWhiteSpace(landuse))
        {
            return landuse;
        }

        return "vegetation";
    }

    private async Task<double[]> FetchElevationsBatchAsync(IReadOnlyList<(double Lat, double Lon)> points, CancellationToken ct)
    {
        double[] list = new double[points.Count];
        List<int> missing = [];
        for (int i = 0; i < points.Count; i++)
        {
            if (_cache.TryGetValue(ElevationCacheKey(points[i].Lat, points[i].Lon), out double cached))
            {
                list[i] = cached;
            }
            else
            {
                missing.Add(i);
            }
        }

        if (missing.Count == 0)
        {
            return list;
        }

        List<(double Lat, double Lon)> toFetch = missing.Select(i => points[i]).ToList();
        HttpClient client = _httpFactory.CreateClient("OpenElevation");
        OpenElevationRequestDto payload = new OpenElevationRequestDto
        {
            locations = toFetch.Select(p => new OpenElevationLocationDto { latitude = p.Lat, longitude = p.Lon }).ToList()
        };

        using HttpResponseMessage resp = await client.PostAsJsonAsync("https://api.open-elevation.com/api/v1/lookup", payload, ct);
        resp.EnsureSuccessStatusCode();
        await using Stream stream = await resp.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("results", out JsonElement results))
        {
            throw new InvalidOperationException("Invalid elevation response");
        }

        int fetched = 0;
        foreach (JsonElement r in results.EnumerateArray())
        {
            if (fetched >= missing.Count)
            {
                break;
            }

            double elev = r.GetProperty("elevation").GetDouble();
            int idx = missing[fetched];
            list[idx] = elev;
            _cache.Set(ElevationCacheKey(points[idx].Lat, points[idx].Lon), elev, ElevationCacheTtl);
            fetched++;
        }

        if (fetched != missing.Count)
        {
            throw new InvalidOperationException("Elevation count mismatch");
        }

        return list;
    }

    private static string OsmCacheKey(double lat1, double lon1, double lat2, double lon2) =>
        string.Create(CultureInfo.InvariantCulture,
            $"los:osm:{Math.Round(lat1, 4):F4}:{Math.Round(lon1, 4):F4}:{Math.Round(lat2, 4):F4}:{Math.Round(lon2, 4):F4}:{OverpassAroundMeters}");

    private static string ElevationCacheKey(double lat, double lon) =>
        string.Create(CultureInfo.InvariantCulture,
            $"los:elev:{Math.Round(lat, 5):F5}:{Math.Round(lon, 5):F5}");

    private sealed class OsmFeature
    {
        public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(double Lat, double Lon)> Ring { get; init; } = [];
        public string Kind { get; init; } = LosObstacleKind.Building;
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
