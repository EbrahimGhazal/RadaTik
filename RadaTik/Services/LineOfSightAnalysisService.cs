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
/// تحليل تقريبي لخط الرؤية بين نقطتين: عينات تضاريس (Open-Elevation) ومبانٍ من OSM (Overpass) عند توفرها.
/// </summary>
public sealed class LineOfSightAnalysisService : ILineOfSightAnalysisService
{
    private const double DefaultSectorAntennaAgl = 12;
    private const double DefaultReceiverAntennaAgl = 6;
    private const double MinTerrainClearanceM = 2;
    private const double MaxCrossTrackBuildingM = 45;
    private const double DefaultGuessBuildingHeightM = 9;

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

        double dist = HaversineMeters(lat1, lon1, lat2, lon2);
        if (dist < 5)
        {
            return new LineOfSightResult
            {
                Success = true,
                DistanceMeters = dist,
                TerrainClear = true,
                MinTerrainMarginMeters = 999,
                TerrainNote = "المسافة شبه معدومة.",
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
        bool terrainBlocked = false;

        for (int i = 0; i < samples.Count; i++)
        {
            double t = samples[i].T;
            double terr = elevations[i];
            double line = hStart + t * (hEnd - hStart);
            double margin = line - terr;
            if (margin < minMargin)
            {
                minMargin = margin;
            }

            if (margin < MinTerrainClearanceM)
            {
                terrainBlocked = true;
            }

            profile.Add(new LosProfilePoint
            {
                DistanceFromStartMeters = dist * t,
                TerrainElevationMslMeters = Math.Round(terr, 2),
                LineHeightMslMeters = Math.Round(line, 2),
                MarginMeters = Math.Round(margin, 2)
            });
        }

        if (minMargin == double.MaxValue)
        {
            minMargin = 0;
        }

        List<BuildingObstructionInfo> buildingList = new List<BuildingObstructionInfo>();
        bool buildingsOk = false;
        try
        {
            buildingList = await AnalyzeBuildingsAsync(
                lat1, lon1, lat2, lon2, hStart, hEnd, ct);
            buildingsOk = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Overpass / buildings analysis failed");
        }

        bool blockedByBuilding = buildingList.Any(b => b.LikelyBlocksLos);
        string note = terrainBlocked
            ? "التضاريس قد تحجب خط الرؤية (الهامش الأدنى أقل من المطلوب)."
            : (blockedByBuilding
                ? "يوجد مبنى قد يعيق الإشارة وفق البيانات التقريبية (OSM)."
                : "خط الرؤية فوق التضاريس ضمن النموذج التقريبي.");

        return new LineOfSightResult
        {
            Success = true,
            DistanceMeters = Math.Round(dist, 1),
            TerrainClear = !terrainBlocked && !blockedByBuilding,
            MinTerrainMarginMeters = Math.Round(minMargin, 2),
            TerrainNote = note,
            BuildingsDataAvailable = buildingsOk,
            BuildingsConsidered = buildingList.Count,
            BuildingObstructions = buildingList,
            Profile = profile
        };
    }

    private async Task<List<BuildingObstructionInfo>> AnalyzeBuildingsAsync(
        double lat1, double lon1, double lat2, double lon2,
        double hStart, double hEnd,
        CancellationToken ct)
    {
        double south = Math.Min(lat1, lat2) - 0.015;
        double north = Math.Max(lat1, lat2) + 0.015;
        double west = Math.Min(lon1, lon2) - 0.015;
        double east = Math.Max(lon1, lon2) + 0.015;

        string query = $"""
            [out:json][timeout:25];
            (
              way["building"]({south.ToString(CultureInfo.InvariantCulture)},{west.ToString(CultureInfo.InvariantCulture)},{north.ToString(CultureInfo.InvariantCulture)},{east.ToString(CultureInfo.InvariantCulture)});
            );
            out center tags;
            """;

        HttpClient client = _httpFactory.CreateClient("Overpass");
        using HttpResponseMessage response = await client.PostAsync(
            "https://overpass-api.de/api/interpreter",
            new StringContent("data=" + Uri.EscapeDataString(query), Encoding.UTF8, "application/x-www-form-urlencoded"),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("elements", out JsonElement elements))
        {
            return [];
        }

        List<(double lat, double lon, double bldgH, double t, double xtrack)> candidates = new List<(double lat, double lon, double bldgH, double t, double xtrack)>();
        foreach (JsonElement el in elements.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!el.TryGetProperty("center", out JsonElement center))
            {
                continue;
            }

            if (!center.TryGetProperty("lat", out JsonElement latProp) || !center.TryGetProperty("lon", out JsonElement lonProp))
            {
                continue;
            }

            double clat = latProp.GetDouble();
            double clon = lonProp.GetDouble();

            if (!el.TryGetProperty("tags", out JsonElement tags))
            {
                continue;
            }

            double bHeight = ParseBuildingHeightMeters(tags);
            (double t, double xtrack) = ProjectOntoPath(lat1, lon1, lat2, lon2, clat, clon);
            if (t is < 0.02 or > 0.98)
            {
                continue;
            }

            if (xtrack > MaxCrossTrackBuildingM)
            {
                continue;
            }

            candidates.Add((clat, clon, bHeight, t, xtrack));
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        candidates.Sort((a, b) => a.xtrack.CompareTo(b.xtrack));
        List<(double lat, double lon, double bldgH, double t, double xtrack)> top = candidates.Take(60).ToList();

        List<(double lat, double lon)> elevPoints = top.Select(c => (c.lat, c.lon)).ToList();
        double[] bElev;
        try
        {
            bElev = await FetchElevationsBatchAsync(elevPoints, ct);
        }
        catch
        {
            return [];
        }

        List<BuildingObstructionInfo> result = new List<BuildingObstructionInfo>();
        for (int i = 0; i < top.Count; i++)
        {
            (double lat, double lon, double bldgH, double t, double xtrack) c = top[i];
            double g = bElev[i];
            double roof = g + c.bldgH;
            double line = hStart + c.t * (hEnd - hStart);
            bool blocks = roof > line + 0.5;
            result.Add(new BuildingObstructionInfo
            {
                Lat = Math.Round(c.lat, 6),
                Lon = Math.Round(c.lon, 6),
                EstimatedBuildingHeightMeters = Math.Round(c.bldgH, 2),
                GroundElevationMslMeters = Math.Round(g, 2),
                RoofMslMeters = Math.Round(roof, 2),
                PathFraction = Math.Round(c.t, 4),
                CrossTrackMeters = Math.Round(c.xtrack, 1),
                LineHeightAtPointMslMeters = Math.Round(line, 2),
                LikelyBlocksLos = blocks
            });
        }

        return result;
    }

    private static double ParseBuildingHeightMeters(JsonElement tags)
    {
        if (tags.TryGetProperty("height", out JsonElement h))
        {
            string? s = h.GetString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                s = s.Replace("m", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    return Math.Clamp(v, 2, 400);
                }
            }
        }

        if (tags.TryGetProperty("building:levels", out JsonElement lv))
        {
            string? ls = lv.GetString();
            if (double.TryParse(ls, NumberStyles.Float, CultureInfo.InvariantCulture, out double levels))
            {
                return Math.Clamp(levels * 3.2, 3, 200);
            }
        }

        return DefaultGuessBuildingHeightM;
    }

    /// <summary>إسقاط نقطة على المسار: t على [0,1] والمسافة العمودية بالمتر (تقريب مسطح محلي حول المرسل).</summary>
    private static (double t, double crossM) ProjectOntoPath(
        double lat1, double lon1, double lat2, double lon2,
        double plat, double plon)
    {
        double ToX(double lat, double lon) => (lon - lon1) * Math.Cos(lat1 * Math.PI / 180) * 111320;
        double ToY(double lat, double lon) => (lat - lat1) * 111320;

        double bx = ToX(lat2, lon2);
        double by = ToY(lat2, lon2);
        double px = ToX(plat, plon);
        double py = ToY(plat, plon);

        double abx = bx;
        double aby = by;
        double ab2 = abx * abx + aby * aby;
        if (ab2 < 1e-6)
        {
            return (0, Math.Sqrt(px * px + py * py));
        }

        double t = (px * abx + py * aby) / ab2;
        t = Math.Clamp(t, 0, 1);
        double projx = t * abx;
        double projy = t * aby;
        double dx = px - projx;
        double dy = py - projy;
        double cross = Math.Sqrt(dx * dx + dy * dy);
        return (t, cross);
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

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
