using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Dtos.Traffic;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Controllers;

[ApiController]
[Route("api/manager/traffic")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class ManagerTrafficApiController : ControllerBase
{
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
        var networkIds = await ResolveAccessibleNetworkIdsAsync(cancellationToken);
        if (networkIds.Count == 0)
        {
            return Ok(Array.Empty<ManagerMikroTikServerOptionDto>());
        }

        var servers = await _context.MikroTikServers.AsNoTracking()
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
        var networkIds = await ResolveAccessibleNetworkIdsAsync(cancellationToken);
        if (networkIds.Count == 0)
        {
            return Forbid();
        }

        var server = await _context.MikroTikServers.AsNoTracking()
            .Where(s => s.Id == serverId && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .Select(s => new { s.Id, s.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (server == null)
        {
            return NotFound();
        }

        var utcNow = DateTime.UtcNow;
        var periods = new (string Key, TimeSpan Span)[]
        {
            ("day", TimeSpan.FromDays(1)),
            ("week", TimeSpan.FromDays(7)),
            ("month", TimeSpan.FromDays(30)),
        };
        var oldestFrom = utcNow - periods.Max(p => p.Span);

        var rows = await _context.MikroTikServerTrafficSamples.AsNoTracking()
            .Where(s => s.MikroTikServerId == server.Id && s.CapturedAtUtc >= oldestFrom && s.CapturedAtUtc <= utcNow)
            .Select(s => new TrafficSamplePoint
            {
                CapturedAtUtc = s.CapturedAtUtc,
                RxBps = s.RxBps,
                TxBps = s.TxBps,
            })
            .ToListAsync(cancellationToken);

        var result = new TrafficStatisticsOverviewDto
        {
            ServerId = server.Id,
            ServerName = server.Name,
            GeneratedAtUtcIso = utcNow.ToString("o"),
            Periods = periods.Select(period =>
            {
                var from = utcNow - period.Span;
                var subset = rows.Where(r => r.CapturedAtUtc >= from).ToList();
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
        var networkIds = await ResolveAccessibleNetworkIdsAsync(cancellationToken);
        if (networkIds.Count == 0)
        {
            return Forbid();
        }

        var server = await _context.MikroTikServers.AsNoTracking()
            .Where(s => s.Id == serverId && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .Select(s => new { s.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (server == null)
        {
            return NotFound();
        }

        var normalized = NormalizePeriod(period);
        var utcNow = DateTime.UtcNow;
        var from = normalized switch
        {
            "week" => utcNow.AddDays(-7),
            "month" => utcNow.AddDays(-30),
            _ => utcNow.AddDays(-1),
        };

        var rows = await _context.MikroTikServerTrafficSamples.AsNoTracking()
            .Where(s => s.MikroTikServerId == serverId && s.CapturedAtUtc >= from && s.CapturedAtUtc <= utcNow)
            .Select(s => new TrafficSamplePoint
            {
                CapturedAtUtc = s.CapturedAtUtc,
                RxBps = s.RxBps,
                TxBps = s.TxBps,
            })
            .ToListAsync(cancellationToken);

        var points = rows
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
        var rxWarnMbps = _configuration.GetValue("Traffic:KpiThresholds:PeakRxWarnMbps", 150d);
        var rxCriticalMbps = _configuration.GetValue("Traffic:KpiThresholds:PeakRxCriticalMbps", 300d);
        var txWarnMbps = _configuration.GetValue("Traffic:KpiThresholds:PeakTxWarnMbps", 100d);
        var txCriticalMbps = _configuration.GetValue("Traffic:KpiThresholds:PeakTxCriticalMbps", 200d);
        var loadWarn = _configuration.GetValue("Traffic:KpiThresholds:LoadIndexWarnPercent", 70);
        var loadCritical = _configuration.GetValue("Traffic:KpiThresholds:LoadIndexCriticalPercent", 85);

        var response = new TrafficKpiThresholdsDto
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

        var rx = rows.Select(r => (double)r.RxBps).ToList();
        var tx = rows.Select(r => (double)r.TxBps).ToList();

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
        var user = await _userManager.GetUserAsync(User);
        if (user?.NetworkId is not int mainNetId)
        {
            return new List<int>();
        }

        var childIds = await _context.Networks.AsNoTracking()
            .Where(n => n.ParentNetworkId == mainNetId)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        var networkIds = new List<int> { mainNetId };
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
