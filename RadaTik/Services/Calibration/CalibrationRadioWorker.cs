using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Hubs;
using RadaTik.Models;
using RadaTik.Services.SectorRadio;

namespace RadaTik.Services.Calibration;

public sealed class CalibrationRadioWorker : BackgroundService
{
    private static readonly TimeSpan NormalTick = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan FastTick = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan HotIdle = TimeSpan.FromMinutes(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICalibrationSessionStore _sessions;
    private readonly IHubContext<CalibrationHub> _hub;
    private readonly ILogger<CalibrationRadioWorker> _logger;
    private int _persistTicks;

    public CalibrationRadioWorker(
        IServiceScopeFactory scopeFactory,
        ICalibrationSessionStore sessions,
        IHubContext<CalibrationHub> hub,
        ILogger<CalibrationRadioWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _sessions = sessions;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _sessions.HydrateAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Calibration hydrate on start skipped.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<CalibrationSession> hot = _sessions.ListHot(HotIdle);
                bool anyFast = hot.Any(s =>
                    CalibrationOptionValues.NormalizeAim(s.Scenario.AimMode) == CalibrationOptionValues.AimPro
                    || CalibrationOptionValues.NormalizeSuccess(s.Scenario.SuccessMode) == CalibrationOptionValues.SuccessHold);
                await Task.Delay(anyFast ? FastTick : NormalTick, stoppingToken);
                await PollHotSessionsAsync(stoppingToken);
                if (Interlocked.Increment(ref _persistTicks) % 12 == 0)
                {
                    await _sessions.PersistActivityAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Calibration radio worker tick failed.");
            }
        }
    }

    private async Task PollHotSessionsAsync(CancellationToken ct)
    {
        IReadOnlyList<CalibrationSession> hot = _sessions.ListHot(HotIdle)
            .Where(s => s.Scenario.NeedsRadio)
            .ToList();
        if (hot.Count == 0)
        {
            return;
        }

        foreach (IGrouping<int, CalibrationSession> group in hot.GroupBy(s => s.SectorId))
        {
            SectorRadioStationsResult? radio;
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                ISectorRadioAdapter adapter = scope.ServiceProvider.GetRequiredService<ISectorRadioAdapter>();
                Sector? sector = await db.Sectors.AsNoTracking()
                    .Include(s => s.MikroTikServer)
                    .FirstOrDefaultAsync(s => s.Id == group.Key, ct);
                if (sector?.MikroTikServer == null || !sector.MikroTikServer.IsActive)
                {
                    radio = new SectorRadioStationsResult
                    {
                        Success = false,
                        StatusMessage = "خادم MikroTik غير متاح لهذا القطاع."
                    };
                }
                else
                {
                    radio = await adapter.ReadStationsAsync(sector, sector.MikroTikServer, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Calibration radio read failed for sector {SectorId}", group.Key);
                radio = new SectorRadioStationsResult
                {
                    Success = false,
                    StatusMessage = "تعذر الاتصال براديو المرسل."
                };
            }

            DateTime now = DateTime.UtcNow;
            foreach (CalibrationSession session in group)
            {
                bool changed = _sessions.TryApplyRadio(session.Code, radio, now);
                bool needsHoldPulse = CalibrationOptionValues.NormalizeSuccess(session.Scenario.SuccessMode)
                    == CalibrationOptionValues.SuccessHold;
                if (!changed && !needsHoldPulse)
                {
                    continue;
                }

                CalibrationSession? fresh = _sessions.Get(session.Code);
                if (fresh == null)
                {
                    continue;
                }

                await _hub.Clients.Group(CalibrationHub.GroupName(fresh.Code))
                    .SendAsync(CalibrationHub.SessionUpdatedMethod, _sessions.ToSnapshot(fresh), ct);
            }
        }
    }
}
