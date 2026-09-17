using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Hubs;
using RadaTik.Models;
using RadaTik.Services.Calibration;
using RadaTik.Services.SectorRadio;

namespace RadaTik.Services.Calibration;

/// <summary>يقرأ إشارة MikroTik لكل جلسة معايرة نشطة ويبث القمة عبر SignalR.</summary>
public sealed class AntennaCalibrationRadioWorker : BackgroundService
{
    private static readonly TimeSpan NormalTick = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan ProTick = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan HotIdle = TimeSpan.FromMinutes(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAntennaCalibrationSessionStore _sessions;
    private readonly IHubContext<AntennaCalibrationHub> _hub;
    private readonly ILogger<AntennaCalibrationRadioWorker> _logger;
    private int _persistTicks;

    public AntennaCalibrationRadioWorker(
        IServiceScopeFactory scopeFactory,
        IAntennaCalibrationSessionStore sessions,
        IHubContext<AntennaCalibrationHub> hub,
        ILogger<AntennaCalibrationRadioWorker> logger)
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
                IReadOnlyList<AntennaCalibrationSession> hot = _sessions.ListHot(HotIdle);
                bool anyPro = hot.Any(s => AntennaCalibrationWorkflow.IsPro(s.Workflow));
                await Task.Delay(anyPro ? ProTick : NormalTick, stoppingToken);
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
        IReadOnlyList<AntennaCalibrationSession> hot = _sessions.ListHot(HotIdle);
        if (hot.Count == 0)
        {
            return;
        }

        foreach (IGrouping<int, AntennaCalibrationSession> group in hot.GroupBy(s => s.SectorId))
        {
            SectorRadioStationsResult? radio = null;
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
            foreach (AntennaCalibrationSession session in group)
            {
                bool changed = _sessions.TryApplyRadio(session.Code, radio, now);
                // المسار الاحترافي يُحدَّث باستمرار لالتقاط قفل القمة بعد ثبات 3 ثوانٍ.
                if (!changed && !AntennaCalibrationWorkflow.IsPro(session.Workflow))
                {
                    continue;
                }

                AntennaCalibrationSession? fresh = _sessions.Get(session.Code);
                if (fresh == null)
                {
                    continue;
                }

                await _hub.Clients.Group(AntennaCalibrationHub.GroupName(fresh.Code))
                    .SendAsync(AntennaCalibrationHub.SessionUpdatedMethod, _sessions.ToSnapshot(fresh), ct);
            }
        }
    }
}
