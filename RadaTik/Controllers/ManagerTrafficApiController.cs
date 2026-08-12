using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Dtos.Traffic;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Controllers;

[ApiController]
[Route("api/manager/traffic")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class ManagerTrafficApiController : ControllerBase
{
    private sealed record ServerIdNameRow(int Id, string? Name);
    private sealed record ServerIdRow(int Id);

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public ManagerTrafficApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
    }

    /// <summary>Active MikroTik servers the company manager can stream traffic for.</summary>
    [HttpGet("mikrotik-servers")]
    public async Task<ActionResult<IReadOnlyList<ManagerMikroTikServerOptionDto>>> GetMikroTikServers(CancellationToken cancellationToken)
    {
        List<int> networkIds = await ResolveAccessibleNetworkIdsAsync(cancellationToken);
        if (networkIds.Count == 0)
        {
            return Ok(Array.Empty<ManagerMikroTikServerOptionDto>());
        }

        List<ManagerMikroTikServerOptionDto> servers = await _context.MikroTikServers.AsNoTracking()
            .Where(s => s.IsActive && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .OrderBy(s => s.Name)
            .Select(s => new ManagerMikroTikServerOptionDto
            {
                Id = s.Id,
                Name = s.Name,
                Host = s.Host,
                NetworkId = s.NetworkId!.Value,
            })
            .ToListAsync(cancellationToken);

        return Ok(servers);
    }

    /// <summary>
    /// Historical RX/TX statistics (min/max/avg) for day/week/month.
    /// </summary>
    [HttpGet("server-stats")]
    public async Task<ActionResult<TrafficStatisticsOverviewDto>> GetServerStatistics(
        [FromQuery] int serverId,
        CancellationToken cancellationToken)
    {
        List<int> networkIds = await ResolveAccessibleNetworkIdsAsync(cancellationToken);
        if (networkIds.Count == 0)
        {
            return Forbid();
        }

        ServerIdNameRow? server = await _context.MikroTikServers.AsNoTracking()
            .Where(s => s.Id == serverId && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .Select(s => new ServerIdNameRow(s.Id, s.Name))
            .FirstOrDefaultAsync(cancellationToken);

        if (server == null)
        {
            return NotFound();
        }

        DateTime utcNow = DateTime.UtcNow;
        (string Key, TimeSpan Span)[] periods = new (string Key, TimeSpan Span)[]
        {
            ("day", TimeSpan.FromDays(1)),
            ("week", TimeSpan.FromDays(7)),
            ("month", TimeSpan.FromDays(30)),
        };
        DateTime oldestFrom = utcNow - periods.Max(p => p.Span);

        List<TrafficSamplePoint> rows = await _context.MikroTikServerTrafficSamples.AsNoTracking()
            .Where(s => s.MikroTikServerId == server.Id && s.CapturedAtUtc >= oldestFrom && s.CapturedAtUtc <= utcNow)
            .Select(s => new TrafficSamplePoint
            {
                CapturedAtUtc = s.CapturedAtUtc,
                RxBps = s.RxBps,
                TxBps = s.TxBps,
            })
            .ToListAsync(cancellationToken);

        TrafficStatisticsOverviewDto result = new TrafficStatisticsOverviewDto
        {
            ServerId = server.Id,
            ServerName = server.Name ?? $"Server #{server.Id}",
            GeneratedAtUtcIso = utcNow.ToString("o"),
            Periods = periods.Select(period =>
            {
                DateTime from = utcNow - period.Span;
                List<TrafficSamplePoint> subset = rows.Where(r => r.CapturedAtUtc >= from).ToList();
                return BuildPeriodStats(period.Key, from, utcNow, subset);
            }).ToList(),
        };

        return Ok(result);
    }

    /// <summary>
    /// Trend line points for RX/TX averages over day/week/month.
    /// day: hourly buckets, week/month: daily buckets.
    /// </summary>
    [HttpGet("server-trend")]
    public async Task<ActionResult<TrafficTrendResponseDto>> GetServerTrend(
        [FromQuery] int serverId,
        [FromQuery] string period = "day",
        CancellationToken cancellationToken = default)
    {
        List<int> networkIds = await ResolveAccessibleNetworkIdsAsync(cancellationToken);
        if (networkIds.Count == 0)
        {
            return Forbid();
        }

        ServerIdRow? server = await _context.MikroTikServers.AsNoTracking()
            .Where(s => s.Id == serverId && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .Select(s => new ServerIdRow(s.Id))
            .FirstOrDefaultAsync(cancellationToken);

        if (server == null)
        {
            return NotFound();
        }

        string normalized = NormalizePeriod(period);
        DateTime utcNow = DateTime.UtcNow;
        DateTime from = normalized switch
        {
            "week" => utcNow.AddDays(-7),
            "month" => utcNow.AddDays(-30),
            _ => utcNow.AddDays(-1),
        };

        List<TrafficSamplePoint> rows = await _context.MikroTikServerTrafficSamples.AsNoTracking()
            .Where(s => s.MikroTikServerId == serverId && s.CapturedAtUtc >= from && s.CapturedAtUtc <= utcNow)
            .Select(s => new TrafficSamplePoint
            {
                CapturedAtUtc = s.CapturedAtUtc,
                RxBps = s.RxBps,
                TxBps = s.TxBps,
            })
            .ToListAsync(cancellationToken);

        List<TrafficTrendPointDto> points = rows
            .GroupBy(r => BucketStartUtc(r.CapturedAtUtc, normalized))
            .OrderBy(g => g.Key)
            .Select(g => new TrafficTrendPointDto
            {
                BucketUtcIso = g.Key.ToString("o"),
                RxAvgBps = g.Average(x => x.RxBps),
                TxAvgBps = g.Average(x => x.TxBps),
            })
            .ToList();

        return Ok(new TrafficTrendResponseDto
        {
            ServerId = serverId,
            PeriodKey = normalized,
            GeneratedAtUtcIso = utcNow.ToString("o"),
            Points = points,
        });
    }

    [HttpGet("kpi-thresholds")]
    public ActionResult<TrafficKpiThresholdsDto> GetKpiThresholds()
    {
        double rxWarnMbps = _configuration.GetValue("Traffic:KpiThresholds:PeakRxWarnMbps", 150d);
        double rxCriticalMbps = _configuration.GetValue("Traffic:KpiThresholds:PeakRxCriticalMbps", 300d);
        double txWarnMbps = _configuration.GetValue("Traffic:KpiThresholds:PeakTxWarnMbps", 100d);
        double txCriticalMbps = _configuration.GetValue("Traffic:KpiThresholds:PeakTxCriticalMbps", 200d);
        int loadWarn = _configuration.GetValue("Traffic:KpiThresholds:LoadIndexWarnPercent", 70);
        int loadCritical = _configuration.GetValue("Traffic:KpiThresholds:LoadIndexCriticalPercent", 85);

        TrafficKpiThresholdsDto response = new TrafficKpiThresholdsDto
        {
            PeakRxWarnBps = Math.Max(0, rxWarnMbps) * 1_000_000d,
            PeakRxCriticalBps = Math.Max(0, rxCriticalMbps) * 1_000_000d,
            PeakTxWarnBps = Math.Max(0, txWarnMbps) * 1_000_000d,
            PeakTxCriticalBps = Math.Max(0, txCriticalMbps) * 1_000_000d,
            LoadIndexWarnPercent = Math.Clamp(loadWarn, 1, 99),
            LoadIndexCriticalPercent = Math.Clamp(loadCritical, 1, 99),
        };

        if (response.LoadIndexCriticalPercent < response.LoadIndexWarnPercent)
        {
            (response.LoadIndexWarnPercent, response.LoadIndexCriticalPercent) =
                (response.LoadIndexCriticalPercent, response.LoadIndexWarnPercent);
        }

        if (response.PeakRxCriticalBps < response.PeakRxWarnBps)
        {
            (response.PeakRxWarnBps, response.PeakRxCriticalBps) = (response.PeakRxCriticalBps, response.PeakRxWarnBps);
        }

        if (response.PeakTxCriticalBps < response.PeakTxWarnBps)
        {
            (response.PeakTxWarnBps, response.PeakTxCriticalBps) = (response.PeakTxCriticalBps, response.PeakTxWarnBps);
        }

        return Ok(response);
    }

    private static TrafficPeriodStatisticsDto BuildPeriodStats(
        string periodKey,
        DateTime from,
        DateTime to,
        IReadOnlyList<TrafficSamplePoint> rows)
    {
        if (rows.Count == 0)
        {
            return new TrafficPeriodStatisticsDto
            {
                PeriodKey = periodKey,
                FromUtcIso = from.ToString("o"),
                ToUtcIso = to.ToString("o"),
                Samples = 0,
            };
        }

        List<double> rx = rows.Select(r => (double)r.RxBps).ToList();
        List<double> tx = rows.Select(r => (double)r.TxBps).ToList();

        return new TrafficPeriodStatisticsDto
        {
            PeriodKey = periodKey,
            FromUtcIso = from.ToString("o"),
            ToUtcIso = to.ToString("o"),
            Samples = rows.Count,
            RxMinBps = rx.Min(),
            RxAvgBps = rx.Average(),
            RxMaxBps = rx.Max(),
            TxMinBps = tx.Min(),
            TxAvgBps = tx.Average(),
            TxMaxBps = tx.Max(),
        };
    }

    private async Task<List<int>> ResolveAccessibleNetworkIdsAsync(CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user?.NetworkId is not int mainNetId)
        {
            return new List<int>();
        }

        List<int> childIds = await _context.Networks.AsNoTracking()
            .Where(n => n.ParentNetworkId == mainNetId)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        List<int> networkIds = new List<int> { mainNetId };
        networkIds.AddRange(childIds);
        return networkIds;
    }

    private static string NormalizePeriod(string? period)
    {
        if (string.Equals(period, "week", StringComparison.OrdinalIgnoreCase))
        {
            return "week";
        }

        if (string.Equals(period, "month", StringComparison.OrdinalIgnoreCase))
        {
            return "month";
        }

        return "day";
    }

    private static DateTime BucketStartUtc(DateTime utc, string period)
    {
        return period == "day"
            ? new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc)
            : new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TrafficSamplePoint
    {
        public DateTime CapturedAtUtc { get; init; }
        public double RxBps { get; init; }
        public double TxBps { get; init; }
    }
}
