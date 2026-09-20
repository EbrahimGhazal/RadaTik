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
    private readonly ICalibrationSessionStore _sessions;
    private readonly ICalibrationScenarioService _scenarios;

    public AntennaCalibrationController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILineOfSightAnalysisService lineOfSight,
        ICalibrationSessionStore sessions,
        ICalibrationScenarioService scenarios)
    {
        _context = context;
        _userManager = userManager;
        _lineOfSight = lineOfSight;
        _sessions = sessions;
        _scenarios = scenarios;
    }

    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> Index(string? code, int? scenarioId, int? sectorId, int? receiverId, CancellationToken ct)
    {
        CalibrationIndexViewModel vm = await BuildIndexModelAsync(code, scenarioId, sectorId, receiverId, ct);
        return View("~/Views/AntennaCalibration/Index.cshtml", vm);
    }

    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> Scenarios(CancellationToken ct)
    {
        int networkId = await RequireNetworkIdAsync();
        IReadOnlyList<CalibrationScenario> list = await _scenarios.ListAsync(networkId, ct);
        return View("~/Views/AntennaCalibration/Scenarios.cshtml", list);
    }

    [HttpGet]
    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> EditScenario(int? id, CancellationToken ct)
    {
        int networkId = await RequireNetworkIdAsync();
        await _scenarios.EnsureDefaultsAsync(networkId, ct);
        CalibrationScenarioEditViewModel vm;
        if (id is > 0)
        {
            CalibrationScenario? row = await _scenarios.GetAsync(networkId, id.Value, ct)
                ?? throw new InvalidOperationException("السيناريو غير موجود.");
            vm = ToEditVm(row);
        }
        else
        {
            vm = new CalibrationScenarioEditViewModel();
        }

        return View("~/Views/AntennaCalibration/EditScenario.cshtml", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> SaveScenario(CalibrationScenarioEditViewModel model, CancellationToken ct)
    {
        int networkId = await RequireNetworkIdAsync();
        CalibrationScenario entity = new()
        {
            Id = model.Id,
            NetworkId = networkId,
            Name = model.Name,
            Description = model.Description,
            IsDefault = model.IsDefault,
            IsActive = true,
            CrewMode = model.CrewMode,
            AimMode = model.AimMode,
            AuthMode = model.AuthMode,
            DisplayMode = model.DisplayMode,
            SuccessMode = model.SuccessMode,
            MinSignalDbm = model.MinSignalDbm,
            PeakHoldSeconds = model.PeakHoldSeconds,
            RequireIpOrMac = model.RequireIpOrMac,
            ShowSnrCcq = model.ShowSnrCcq,
            ShowLosHint = model.ShowLosHint,
            ShowGeometryTargets = model.ShowGeometryTargets,
            SortOrder = model.SortOrder
        };

        if (model.Id > 0)
        {
            await _scenarios.UpdateAsync(entity, ct);
        }
        else
        {
            await _scenarios.CreateAsync(entity, ct);
        }

        return RedirectToAction(nameof(Scenarios));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> DeleteScenario(int id, CancellationToken ct)
    {
        int networkId = await RequireNetworkIdAsync();
        await _scenarios.DeleteAsync(networkId, id, ct);
        return RedirectToAction(nameof(Scenarios));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> SetDefaultScenario(int id, CancellationToken ct)
    {
        int networkId = await RequireNetworkIdAsync();
        await _scenarios.SetDefaultAsync(networkId, id, ct);
        return RedirectToAction(nameof(Scenarios));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> Start(
        [FromForm] int sectorId,
        [FromForm] int scenarioId,
        [FromForm] int? receiverId,
        [FromForm] double? receiverLat,
        [FromForm] double? receiverLng,
        [FromForm] double? receiverAgl,
        [FromForm] string? receiverName,
        [FromForm] string? receiverIp,
        [FromForm] string? receiverMac,
        CancellationToken ct)
    {
        try
        {
            int networkId = await RequireNetworkIdAsync();
            CalibrationSession session = await CreateSessionAsync(
                networkId, sectorId, scenarioId, receiverId, receiverLat, receiverLng,
                receiverAgl, receiverName, receiverIp, receiverMac, ct);
            return RedirectToAction(nameof(Index), new { code = session.Code });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [AllowAnonymous]
    public IActionResult Join(string code, string role = "rx")
    {
        CalibrationSession? session = _sessions.Get(code ?? string.Empty);
        if (session == null)
        {
            return View("~/Views/AntennaCalibration/JoinMissing.cshtml");
        }

        if (CalibrationOptionValues.NormalizeAuth(session.Scenario.AuthMode) == CalibrationOptionValues.AuthLogin
            && !(User.Identity?.IsAuthenticated ?? false))
        {
            return Challenge();
        }

        string normalized = string.Equals(role, "tx", StringComparison.OrdinalIgnoreCase) ? "tx" : "rx";
        if (!session.Scenario.IsDual && normalized == "tx")
        {
            normalized = "rx";
        }

        CalibrationSnapshot snapshot = _sessions.ToSnapshot(session);
        CalibrationJoinViewModel vm = new()
        {
            Code = session.Code,
            Role = normalized,
            IsTransmitter = normalized == "tx",
            Title = normalized == "tx" ? "معايرة المرسل" : "معايرة المستقبل",
            EndpointName = normalized == "tx" ? session.SectorName : session.ReceiverName,
            OtherName = normalized == "tx" ? session.ReceiverName : session.SectorName,
            Scenario = session.Scenario,
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

    private async Task<CalibrationIndexViewModel> BuildIndexModelAsync(
        string? code, int? scenarioId, int? sectorId, int? receiverId, CancellationToken ct)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        CalibrationIndexViewModel vm = new()
        {
            PrefillScenarioId = scenarioId,
            PrefillSectorId = sectorId,
            PrefillReceiverId = receiverId
        };
        if (!networkId.HasValue)
        {
            vm.ErrorMessage = "يرجى تحديد شبكة أولاً.";
            return vm;
        }

        vm.Sectors = await _context.Sectors.AsNoTracking()
            .Where(s => s.NetworkId == networkId.Value && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new CalibrationSectorOption(
                s.Id,
                s.Name ?? ("قطاع #" + s.Id),
                s.Latitude,
                s.Longitude))
            .ToListAsync(ct);
        vm.Scenarios = (await _scenarios.ListAsync(networkId.Value, ct)).ToList();
        vm.RecentSessions = _sessions.ListRecent(networkId.Value).ToList();

        if (!string.IsNullOrWhiteSpace(code))
        {
            CalibrationSession? session = _sessions.Get(code);
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

    private string AbsoluteJoinUrl(string code, string role) =>
        $"{Request.Scheme}://{Request.Host}/calibrate/Join?code={Uri.EscapeDataString(code)}&role={Uri.EscapeDataString(role)}";

    private async Task<int> RequireNetworkIdAsync()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            throw new InvalidOperationException("يرجى تحديد شبكة أولاً.");
        }

        return networkId.Value;
    }

    private async Task<CalibrationSession> CreateSessionAsync(
        int networkId,
        int sectorId,
        int scenarioId,
        int? receiverId,
        double? receiverLat,
        double? receiverLng,
        double? receiverAgl,
        string? receiverName,
        string? receiverIp,
        string? receiverMac,
        CancellationToken ct)
    {
        CalibrationScenario scenarioEntity = await _scenarios.GetAsync(networkId, scenarioId, ct)
            ?? throw new InvalidOperationException("اختر سيناريو معايرة.");
        CalibrationScenarioSnapshot scenario = CalibrationScenarioSnapshot.FromEntity(scenarioEntity);

        Sector? sector = await _context.Sectors.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sectorId && s.NetworkId == networkId, ct)
            ?? throw new InvalidOperationException("القطاع غير موجود في الشبكة الحالية.");

        Receiver? receiver = null;
        if (receiverId is > 0)
        {
            receiver = await _context.Receivers.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == receiverId.Value && r.SectorId == sectorId && r.NetworkId == networkId, ct)
                ?? throw new InvalidOperationException("المستقبل غير موجود في هذا القطاع.");
        }

        double lat = receiver?.Latitude ?? receiverLat ?? double.NaN;
        double lng = receiver?.Longitude ?? receiverLng ?? double.NaN;
        if (lat is < -90 or > 90 || lng is < -180 or > 180 || double.IsNaN(lat) || double.IsNaN(lng))
        {
            throw new InvalidOperationException("حدد مستقبلاً محفوظاً أو انقر نقطة على الخريطة.");
        }

        string? ip = FirstNonEmpty(receiverIp, receiver?.IPAddress);
        string? mac = string.IsNullOrWhiteSpace(receiverMac) ? null : receiverMac.Trim();
        if (scenario.RequireIpOrMac && scenario.NeedsRadio && string.IsNullOrWhiteSpace(ip) && string.IsNullOrWhiteSpace(mac))
        {
            throw new InvalidOperationException("هذا السيناريو يتطلب IP أو MAC للمستقبل لربط الإشارة.");
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
            sector.Latitude, sector.Longitude, sectorTerrain.Value + sectorAgl,
            sector.Direction, sector.CoverageAngle,
            lat, lng, receiverTerrain.Value + rxAgl);

        if (alignment.DistanceMeters < 5)
        {
            throw new InvalidOperationException("المسافة بين الطرفين شبه معدومة.");
        }

        string pathSummary = "تعذر تحليل خط الرؤية. المعايرة الهندسية ما زالت صالحة.";
        bool pathClear = true;
        if (scenario.ShowLosHint)
        {
            try
            {
                (double frequencyMhz, string frequencySource) = await ResolveLosFrequencyAsync(sector.Id, ct);
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
                if (los.Success)
                {
                    pathClear = los.PathClear;
                    pathSummary = los.PathClear
                        ? "المسار مفتوح تقريباً. اضبط حتى أعلى إشارة ثم اربط."
                        : (los.TerrainNote ?? "عائق محتمل على المسار. راقب الإشارة بعد المحاذاة.");
                    if (!los.TerrainClear)
                    {
                        pathSummary = "التضاريس تقطع الخط. توجيه الهوائي لن يفتح المسار.";
                    }
                    else if (!los.FresnelClear)
                    {
                        pathSummary = "منطقة فريسنل غير خالية. قد تضعف الإشارة حتى مع محاذاة صحيحة.";
                    }
                }
                else if (!string.IsNullOrWhiteSpace(los.ErrorMessage))
                {
                    pathSummary = los.ErrorMessage;
                }
            }
            catch
            {
                // keep default path summary
            }
        }

        string name = receiver?.Name
            ?? (string.IsNullOrWhiteSpace(receiverName) ? "نقطة ميدانية" : receiverName.Trim());
        CalibrationSession session = new()
        {
            Code = CalibrationSessionStore.NewCode(),
            NetworkId = networkId,
            SectorId = sector.Id,
            ReceiverId = receiver?.Id,
            SectorName = sector.Name ?? "المرسل",
            ReceiverName = name,
            ReceiverLatitude = lat,
            ReceiverLongitude = lng,
            ReceiverIp = ip,
            ReceiverMac = mac,
            Alignment = alignment,
            SectorAntennaMsl = sectorTerrain.Value + sectorAgl,
            ReceiverAntennaMsl = receiverTerrain.Value + rxAgl,
            PathSummary = pathSummary,
            PathClear = pathClear,
            Scenario = scenario
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
        return sampleFrequencyMhz > 100 ? (sampleFrequencyMhz, "sector") : (0, "default");
    }

    private static CalibrationScenarioEditViewModel ToEditVm(CalibrationScenario row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Description = row.Description,
        IsDefault = row.IsDefault,
        CrewMode = row.CrewMode,
        AimMode = row.AimMode,
        AuthMode = row.AuthMode,
        DisplayMode = row.DisplayMode,
        SuccessMode = row.SuccessMode,
        MinSignalDbm = row.MinSignalDbm,
        PeakHoldSeconds = row.PeakHoldSeconds,
        RequireIpOrMac = row.RequireIpOrMac,
        ShowSnrCcq = row.ShowSnrCcq,
        ShowLosHint = row.ShowLosHint,
        ShowGeometryTargets = row.ShowGeometryTargets,
        SortOrder = row.SortOrder
    };

    private static string? FirstNonEmpty(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a))
        {
            return a.Trim();
        }

        return string.IsNullOrWhiteSpace(b) ? null : b.Trim();
    }
}
