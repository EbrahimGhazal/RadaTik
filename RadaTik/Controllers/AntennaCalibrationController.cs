using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.Calibration;
using RadaTik.ViewModels;
using RadaTik.ViewModels.Calibration;

namespace RadaTik.Controllers;

[Authorize(Roles = "SystemAdministrator,NetworkAdministrator,CompanyEmployee,Employee")]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Receivers)]
public class AntennaCalibrationController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILineOfSightAnalysisService _lineOfSight;
    private readonly IAntennaCalibrationSessionStore _sessions;

    public AntennaCalibrationController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILineOfSightAnalysisService lineOfSight,
        IAntennaCalibrationSessionStore sessions)
    {
        _context = context;
        _userManager = userManager;
        _lineOfSight = lineOfSight;
        _sessions = sessions;
    }

    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> Index(string? code, CancellationToken ct)
    {
        AntennaCalibrationIndexViewModel vm = await BuildIndexModelAsync(code, ct);
        return View("~/Views/AntennaCalibration/Index.cshtml", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> Start(
        [FromForm] int sectorId,
        [FromForm] int? receiverId,
        [FromForm] double? receiverLat,
        [FromForm] double? receiverLng,
        [FromForm] double? receiverAgl,
        [FromForm] string? receiverName,
        [FromForm] string? receiverIp,
        [FromForm] string? receiverMac,
        [FromForm] string? workflow,
        CancellationToken ct)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            AntennaCalibrationSession session = await CreateSessionAsync(
                networkId.Value,
                sectorId,
                receiverId,
                receiverLat,
                receiverLng,
                receiverAgl,
                receiverName,
                receiverIp,
                receiverMac,
                workflow,
                ct);
            return RedirectToAction(nameof(Index), new { code = session.Code });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [RequirePermission("Receivers.View")]
    public IActionResult Join(string code, string role = "rx")
    {
        AntennaCalibrationSession? session = _sessions.Get(code ?? string.Empty);
        if (session == null)
        {
            return View("~/Views/AntennaCalibration/JoinMissing.cshtml");
        }

        string normalized = string.Equals(role, "tx", StringComparison.OrdinalIgnoreCase) ? "tx" : "rx";
        AntennaCalibrationSnapshot snapshot = _sessions.ToSnapshot(session);
        AntennaCalibrationJoinViewModel vm = new()
        {
            Code = session.Code,
            Role = normalized,
            IsTransmitter = normalized == "tx",
            Title = normalized == "tx" ? "معايرة المرسل" : "معايرة المستقبل",
            EndpointName = normalized == "tx" ? session.SectorName : session.ReceiverName,
            OtherName = normalized == "tx" ? session.ReceiverName : session.SectorName,
            Workflow = AntennaCalibrationWorkflow.Normalize(session.Workflow),
            WorkflowLabel = AntennaCalibrationWorkflow.DisplayName(session.Workflow),
            Snapshot = snapshot
        };
        return View("~/Views/AntennaCalibration/Join.cshtml", vm);
    }

    [HttpGet]
    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> ReceiversForSector(int sectorId, CancellationToken ct)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return Json(Array.Empty<object>());
        }

        var rows = await _context.Receivers.AsNoTracking()
            .Where(r => r.SectorId == sectorId && r.NetworkId == networkId.Value && r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                id = r.Id,
                name = r.Name ?? ("#" + r.Id),
                lat = r.Latitude,
                lng = r.Longitude,
                ip = r.IPAddress,
                agl = r.AntennaHeightAglMeters
            })
            .ToListAsync(ct);
        return Json(rows);
    }

    private async Task<AntennaCalibrationIndexViewModel> BuildIndexModelAsync(string? code, CancellationToken ct)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        AntennaCalibrationIndexViewModel vm = new();
        if (!networkId.HasValue)
        {
            vm.ErrorMessage = "يرجى تحديد شبكة أولاً.";
            return vm;
        }

        vm.Sectors = await _context.Sectors.AsNoTracking()
            .Where(s => s.NetworkId == networkId.Value && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new AntennaCalibrationSectorOption(
                s.Id,
                s.Name ?? ("قطاع #" + s.Id),
                s.Latitude,
                s.Longitude))
            .ToListAsync(ct);
        vm.RecentSessions = _sessions.ListRecent(networkId.Value).ToList();

        if (!string.IsNullOrWhiteSpace(code))
        {
            AntennaCalibrationSession? session = _sessions.Get(code);
            if (session != null && session.NetworkId == networkId.Value)
            {
                vm.Session = _sessions.ToSnapshot(session);
                vm.TransmitterJoinUrl = AbsoluteJoinUrl(session.Code, "tx");
                vm.ReceiverJoinUrl = AbsoluteJoinUrl(session.Code, "rx");
                vm.TransmitterQrDataUri = CalibrationQrCode.PngDataUri(vm.TransmitterJoinUrl);
                vm.ReceiverQrDataUri = CalibrationQrCode.PngDataUri(vm.ReceiverJoinUrl);
            }
            else
            {
                vm.ErrorMessage = "انتهت الجلسة أو الرمز غير تابع لهذه الشبكة.";
            }
        }

        if (TempData["Error"] is string err)
        {
            vm.ErrorMessage = err;
        }

        return vm;
    }

    private string AbsoluteJoinUrl(string code, string role)
    {
        return $"{Request.Scheme}://{Request.Host}/calibrate/Join?code={Uri.EscapeDataString(code)}&role={Uri.EscapeDataString(role)}";
    }

    private async Task<AntennaCalibrationSession> CreateSessionAsync(
        int networkId,
        int sectorId,
        int? receiverId,
        double? receiverLat,
        double? receiverLng,
        double? receiverAgl,
        string? receiverName,
        string? receiverIp,
        string? receiverMac,
        string? workflow,
        CancellationToken ct)
    {
        Sector? sector = await _context.Sectors.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sectorId && s.NetworkId == networkId, ct);
        if (sector == null)
        {
            throw new InvalidOperationException("القطاع غير موجود في الشبكة الحالية.");
        }

        Receiver? receiver = null;
        if (receiverId is > 0)
        {
            receiver = await _context.Receivers.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == receiverId.Value && r.SectorId == sectorId && r.NetworkId == networkId, ct);
            if (receiver == null)
            {
                throw new InvalidOperationException("المستقبل غير موجود في هذا القطاع.");
            }
        }

        double lat = receiver?.Latitude ?? receiverLat ?? double.NaN;
        double lng = receiver?.Longitude ?? receiverLng ?? double.NaN;
        if (lat is < -90 or > 90 || lng is < -180 or > 180 || double.IsNaN(lat) || double.IsNaN(lng))
        {
            throw new InvalidOperationException("حدد مستقبلاً محفوظاً أو انقر نقطة على الخريطة.");
        }

        double sectorAgl = sector.AntennaHeightAglMeters is > 0 ? sector.AntennaHeightAglMeters.Value : 12;
        double rxAgl = receiver?.AntennaHeightAglMeters is > 0
            ? receiver.AntennaHeightAglMeters.Value
            : (receiverAgl is > 0 ? receiverAgl.Value : 6);
        double? sectorTerrain = sector.ElevationMeters
            ?? await _lineOfSight.LookupElevationAtAsync(sector.Latitude, sector.Longitude, ct);
        double? receiverTerrain = receiver?.ElevationMeters
            ?? await _lineOfSight.LookupElevationAtAsync(lat, lng, ct);
        if (sectorTerrain == null || receiverTerrain == null)
        {
            throw new InvalidOperationException("تعذر تحديد ارتفاع الأرض للطرفين.");
        }

        AntennaAlignmentResult alignment = LineOfSightMath.ComputeAlignment(
            sector.Latitude,
            sector.Longitude,
            sectorTerrain.Value + sectorAgl,
            sector.Direction,
            sector.CoverageAngle,
            lat,
            lng,
            receiverTerrain.Value + rxAgl);

        if (alignment.DistanceMeters < 5)
        {
            throw new InvalidOperationException("المسافة بين الطرفين شبه معدومة.");
        }

        (double frequencyMhz, string frequencySource) = await ResolveLosFrequencyAsync(sector.Id, ct);
        AntennaCalibrationPathSnapshot path;
        try
        {
            LineOfSightResult los = await _lineOfSight.AnalyzeAsync(new LineOfSightAnalysisInput
            {
                SectorLat = sector.Latitude,
                SectorLon = sector.Longitude,
                SectorTerrainElevationMeters = sectorTerrain,
                SectorAntennaAglMeters = sectorAgl,
                ReceiverLat = lat,
                ReceiverLon = lng,
                ReceiverTerrainElevationMeters = receiverTerrain,
                ReceiverAntennaAglMeters = rxAgl,
                FrequencyMhz = frequencyMhz,
                FrequencySource = frequencySource
            }, ct);
            path = AntennaCalibrationPathSnapshot.From(los);
        }
        catch (Exception)
        {
            path = AntennaCalibrationPathSnapshot.Unavailable();
        }

        string name = receiver?.Name
            ?? (string.IsNullOrWhiteSpace(receiverName) ? "نقطة ميدانية" : receiverName.Trim());
        AntennaCalibrationSession session = new()
        {
            Code = AntennaCalibrationSessionStore.NewCode(),
            NetworkId = networkId,
            SectorId = sector.Id,
            ReceiverId = receiver?.Id,
            SectorName = sector.Name ?? "المرسل",
            ReceiverName = name,
            ReceiverLatitude = lat,
            ReceiverLongitude = lng,
            ReceiverIp = FirstNonEmpty(receiverIp, receiver?.IPAddress),
            ReceiverMac = string.IsNullOrWhiteSpace(receiverMac) ? null : receiverMac.Trim(),
            Alignment = alignment,
            SectorAntennaMsl = sectorTerrain.Value + sectorAgl,
            ReceiverAntennaMsl = receiverTerrain.Value + rxAgl,
            Path = path,
            Workflow = AntennaCalibrationWorkflow.Normalize(workflow)
        };
        return _sessions.Create(session);
    }

    private async Task<(double Mhz, string Source)> ResolveLosFrequencyAsync(int sectorId, CancellationToken ct)
    {
        int sampleFrequencyMhz = await _context.SectorRadioMetricSamples.AsNoTracking()
            .Where(s => s.SectorId == sectorId && s.FrequencyMhz != null && s.FrequencyMhz > 100)
            .OrderByDescending(s => s.CapturedAt)
            .Select(s => s.FrequencyMhz!.Value)
            .FirstOrDefaultAsync(ct);
        if (sampleFrequencyMhz > 100)
        {
            return (sampleFrequencyMhz, "sector");
        }

        return (0, "default");
    }

    private static string? FirstNonEmpty(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a))
        {
            return a.Trim();
        }

        return string.IsNullOrWhiteSpace(b) ? null : b.Trim();
    }
}
