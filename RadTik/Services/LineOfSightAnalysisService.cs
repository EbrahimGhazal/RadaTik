using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RadTik.ViewModels;

namespace RadTik.Services;

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
            var arr = await FetchElevationsBatchAsync([(latitude, longitude)], ct);
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

        var lat1 = input.SectorLat;
        var lon1 = input.SectorLon;
        var lat2 = input.ReceiverLat;
        var lon2 = input.ReceiverLon;

        if (!IsValidLatLng(lat1, lon1) || !IsValidLatLng(lat2, lon2))
        {
            return new LineOfSightResult { Success = false, ErrorMessage = "إحداثيات غير صالحة." };
        }

        var dist = HaversineMeters(lat1, lon1, lat2, lon2);
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

        var n = input.SampleCount;
        var samples = new List<(double Lat, double Lon, double T)>();
        for (var i = 0; i <= n; i++)
        {
            var t = i / (double)n;
            var lat = lat1 + t * (lat2 - lat1);
            var lon = lon1 + t * (lon2 - lon1);
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

        var sectorAgl = input.SectorAntennaAglMeters > 0 ? input.SectorAntennaAglMeters : DefaultSectorAntennaAgl;
        var recvAgl = input.ReceiverAntennaAglMeters > 0 ? input.ReceiverAntennaAglMeters : DefaultReceiverAntennaAgl;

        var elev0 = input.SectorTerrainElevationMeters ?? elevations[0];
        var elev1 = input.ReceiverTerrainElevationMeters ?? elevations[^1];

        var hStart = elev0 + sectorAgl;
        var hEnd = elev1 + recvAgl;

        var profile = new List<LosProfilePoint>();
        var minMargin = double.MaxValue;
        var terrainBlocked = false;

        for (var i = 0; i < samples.Count; i++)
        {
            var t = samples[i].T;
            var terr = elevations[i];
            var line = hStart + t * (hEnd - hStart);
            var margin = line - terr;
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

        var buildingList = new List<BuildingObstructionInfo>();
        var buildingsOk = false;
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

        var blockedByBuilding = buildingList.Any(b => b.LikelyBlocksLos);
        var note = terrainBlocked
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
        var south = Math.Min(lat1, lat2) - 0.015;
        var north = Math.Max(lat1, lat2) + 0.015;
        var west = Math.Min(lon1, lon2) - 0.015;
        var east = Math.Max(lon1, lon2) + 0.015;

        var query = $"""
            [out:json][timeout:25];
            (
              way["building"]({south.ToString(CultureInfo.InvariantCulture)},{west.ToString(CultureInfo.InvariantCulture)},{north.ToString(CultureInfo.InvariantCulture)},{east.ToString(CultureInfo.InvariantCulture)});
            );
            out center tags;
            """;

        var client = _httpFactory.CreateClient("Overpass");
        using var response = await client.PostAsync(
            "https://overpass-api.de/api/interpreter",
            new StringContent("data=" + Uri.EscapeDataString(query), Encoding.UTF8, "application/x-www-form-urlencoded"),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("elements", out var elements))
        {
            return [];
        }

        var candidates = new List<(double lat, double lon, double bldgH, double t, double xtrack)>();
        foreach (var el in elements.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!el.TryGetProperty("center", out var center))
            {
                continue;
            }

            if (!center.TryGetProperty("lat", out var latProp) || !center.TryGetProperty("lon", out var lonProp))
            {
                continue;
            }

            var clat = latProp.GetDouble();
            var clon = lonProp.GetDouble();

            if (!el.TryGetProperty("tags", out var tags))
            {
                continue;
            }

            var bHeight = ParseBuildingHeightMeters(tags);
            var (t, xtrack) = ProjectOntoPath(lat1, lon1, lat2, lon2, clat, clon);
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
        var top = candidates.Take(60).ToList();

        var elevPoints = top.Select(c => (c.lat, c.lon)).ToList();
        double[] bElev;
        try
        {
            bElev = await FetchElevationsBatchAsync(elevPoints, ct);
        }
        catch
        {
            return [];
        }

        var result = new List<BuildingObstructionInfo>();
        for (var i = 0; i < top.Count; i++)
        {
            var c = top[i];
            var g = bElev[i];
            var roof = g + c.bldgH;
            var line = hStart + c.t * (hEnd - hStart);
            var blocks = roof > line + 0.5;
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
        if (tags.TryGetProperty("height", out var h))
        {
            var s = h.GetString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                s = s.Replace("m", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    return Math.Clamp(v, 2, 400);
                }
            }
        }

        if (tags.TryGetProperty("building:levels", out var lv))
        {
            var ls = lv.GetString();
            if (double.TryParse(ls, NumberStyles.Float, CultureInfo.InvariantCulture, out var levels))
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

        var bx = ToX(lat2, lon2);
        var by = ToY(lat2, lon2);
        var px = ToX(plat, plon);
        var py = ToY(plat, plon);

        var abx = bx;
        var aby = by;
        var ab2 = abx * abx + aby * aby;
        if (ab2 < 1e-6)
        {
            return (0, Math.Sqrt(px * px + py * py));
        }

        var t = (px * abx + py * aby) / ab2;
        t = Math.Clamp(t, 0, 1);
        var projx = t * abx;
        var projy = t * aby;
        var dx = px - projx;
        var dy = py - projy;
        var cross = Math.Sqrt(dx * dx + dy * dy);
        return (t, cross);
    }

    private async Task<double[]> FetchElevationsBatchAsync(IReadOnlyList<(double Lat, double Lon)> points, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("OpenElevation");
        var payload = new
        {
            locations = points.Select(p => new { latitude = p.Lat, longitude = p.Lon }).ToList()
        };

        using var resp = await client.PostAsJsonAsync("https://api.open-elevation.com/api/v1/lookup", payload, ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("results", out var results))
        {
            throw new InvalidOperationException("Invalid elevation response");
        }

        var list = new double[points.Count];
        var i = 0;
        foreach (var r in results.EnumerateArray())
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

    private static bool IsValidLatLng(double lat, double lon) =>
        lat is >= -90 and <= 90 && lon is >= -180 and <= 180;

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
