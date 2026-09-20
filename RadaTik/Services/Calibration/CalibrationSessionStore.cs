using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.SectorRadio;
using RadaTik.ViewModels;

namespace RadaTik.Services.Calibration;

public interface ICalibrationSessionStore
{
    CalibrationSession Create(CalibrationSession session);
    CalibrationSession? Get(string code);
    IReadOnlyList<CalibrationSession> ListHot(TimeSpan maxIdle);
    IReadOnlyList<CalibrationSessionListItem> ListRecent(int networkId, int take = 8);
    bool TryUpdatePose(string code, string role, PhoneBoresightPose pose, string connectionId, double? accuracyDegrees);
    void SetConnected(string code, string role, string connectionId, bool connected);
    void ClearConnection(string connectionId);
    bool TryResetPeak(string code);
    bool TryApplyRadio(string code, SectorRadioStationsResult radio, DateTime utcNow);
    Task HydrateAsync(CancellationToken ct = default);
    Task PersistActivityAsync(CancellationToken ct = default);
    CalibrationSnapshot ToSnapshot(CalibrationSession session);
}

public sealed class CalibrationSessionStore : ICalibrationSessionStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentDictionary<string, CalibrationSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceScopeFactory? _scopes;
    private readonly ILogger<CalibrationSessionStore> _logger;
    private int _hydrated;

    public CalibrationSessionStore()
        : this(null, Microsoft.Extensions.Logging.Abstractions.NullLogger<CalibrationSessionStore>.Instance)
    {
    }

    public CalibrationSessionStore(IServiceScopeFactory? scopes, ILogger<CalibrationSessionStore> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public CalibrationSession Create(CalibrationSession session)
    {
        _sessions[session.Code] = session;
        Persist(session, insert: true);
        return session;
    }

    public CalibrationSession? Get(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        string key = code.Trim();
        if (_sessions.TryGetValue(key, out CalibrationSession? session))
        {
            if (session.ExpiresAtUtc < DateTime.UtcNow)
            {
                _sessions.TryRemove(key, out _);
                return null;
            }

            return session;
        }

        return LoadFromDatabase(key);
    }

    public IReadOnlyList<CalibrationSession> ListHot(TimeSpan maxIdle)
    {
        DateTime cutoff = DateTime.UtcNow - maxIdle;
        return _sessions.Values
            .Where(s => s.ExpiresAtUtc > DateTime.UtcNow && s.LastActivityUtc >= cutoff)
            .ToList();
    }

    public IReadOnlyList<CalibrationSessionListItem> ListRecent(int networkId, int take = 8)
    {
        return _sessions.Values
            .Where(s => s.NetworkId == networkId && s.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(s => s.LastActivityUtc)
            .Take(Math.Clamp(take, 1, 20))
            .Select(s => new CalibrationSessionListItem
            {
                Code = s.Code,
                SectorName = s.SectorName,
                ReceiverName = s.ReceiverName,
                ScenarioName = s.Scenario.Name,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToList();
    }

    public bool TryUpdatePose(string code, string role, PhoneBoresightPose pose, string connectionId, double? accuracyDegrees)
    {
        CalibrationSession? session = Get(code);
        if (session == null)
        {
            return false;
        }

        CalibrationEndpointPose endpoint = RoleEndpoint(session, role);
        endpoint.Connected = true;
        endpoint.ConnectionId = connectionId;
        endpoint.PoseValid = pose.AzimuthValid;
        endpoint.AzimuthDegrees = pose.AzimuthDegrees;
        endpoint.ElevationDegrees = pose.ElevationDegrees;
        endpoint.AccuracyDegrees = accuracyDegrees;
        endpoint.UpdatedAtUtc = DateTime.UtcNow;
        session.LastActivityUtc = DateTime.UtcNow;
        return true;
    }

    public void SetConnected(string code, string role, string connectionId, bool connected)
    {
        CalibrationSession? session = Get(code);
        if (session == null)
        {
            return;
        }

        CalibrationEndpointPose endpoint = RoleEndpoint(session, role);
        if (connected)
        {
            endpoint.Connected = true;
            endpoint.ConnectionId = connectionId;
            session.LastActivityUtc = DateTime.UtcNow;
            return;
        }

        if (endpoint.ConnectionId == connectionId)
        {
            endpoint.Connected = false;
            endpoint.ConnectionId = null;
        }
    }

    public void ClearConnection(string connectionId)
    {
        foreach (CalibrationSession session in _sessions.Values)
        {
            if (session.Transmitter.ConnectionId == connectionId)
            {
                session.Transmitter.Connected = false;
                session.Transmitter.ConnectionId = null;
            }

            if (session.Receiver.ConnectionId == connectionId)
            {
                session.Receiver.Connected = false;
                session.Receiver.ConnectionId = null;
            }
        }
    }

    public bool TryResetPeak(string code)
    {
        CalibrationSession? session = Get(code);
        if (session == null)
        {
            return false;
        }

        session.Radio.PeakSignalDbm = session.Radio.SignalDbm;
        session.Radio.PeakSnrDb = session.Radio.SnrDb;
        session.Radio.NearPeakSinceUtc = null;
        session.LastActivityUtc = DateTime.UtcNow;
        return true;
    }

    public bool TryApplyRadio(string code, SectorRadioStationsResult radio, DateTime utcNow)
    {
        CalibrationSession? session = Get(code);
        if (session == null)
        {
            return false;
        }

        CalibrationRadioState state = session.Radio;
        string previous = SnapshotKey(state, session.Scenario);
        if (!radio.Success)
        {
            state.Available = false;
            state.Stale = state.UpdatedAtUtc is DateTime last && utcNow - last > TimeSpan.FromSeconds(20);
            state.Status = string.IsNullOrWhiteSpace(radio.StatusMessage)
                ? "تعذر قراءة جدول التسجيل من المرسل."
                : radio.StatusMessage;
            state.NearPeakSinceUtc = null;
            return previous != SnapshotKey(state, session.Scenario);
        }

        RadioStationMatch? match = RadioStationMatcher.Pick(
            radio.Stations,
            session.ReceiverIp,
            session.ReceiverMac,
            radio.InterfaceName);

        state.FrequencyMhz = radio.FrequencyMhz;
        state.NoiseFloorDbm = radio.NoiseFloorDbm;
        state.UpdatedAtUtc = utcNow;
        state.Stale = false;
        if (match == null)
        {
            state.Available = false;
            state.MatchReason = null;
            state.MacAddress = null;
            state.LastIp = null;
            state.SignalDbm = null;
            state.SnrDb = null;
            state.CcqPercent = null;
            state.NearPeakSinceUtc = null;
            state.Status = string.IsNullOrWhiteSpace(session.ReceiverIp) && string.IsNullOrWhiteSpace(session.ReceiverMac)
                ? "حدد IP أو MAC للمستقبل لربط الإشارة، أو اترك محطة واحدة على الواجهة."
                : "لم تُطابق محطة للمستقبل بعد. تحقق من IP/MAC أو أن الجهاز مرتبط بالمرسل.";
            return previous != SnapshotKey(state, session.Scenario);
        }

        RadioStationSignal station = match.Station;
        state.Available = true;
        state.MatchReason = match.Reason;
        state.MacAddress = station.MacAddress;
        state.LastIp = station.LastIp;
        state.SignalDbm = station.SignalDbm;
        state.SnrDb = station.SnrDb;
        state.CcqPercent = station.CcqPercent;
        if (station.SignalDbm is int signal && (state.PeakSignalDbm is null || signal > state.PeakSignalDbm))
        {
            state.PeakSignalDbm = signal;
        }

        if (station.SnrDb is int snr && (state.PeakSnrDb is null || snr > state.PeakSnrDb))
        {
            state.PeakSnrDb = snr;
        }

        bool nearPeak = state.SignalDbm is int live
            && state.PeakSignalDbm is int peak
            && live >= peak - 2;
        if (nearPeak)
        {
            state.NearPeakSinceUtc ??= utcNow;
        }
        else
        {
            state.NearPeakSinceUtc = null;
        }

        state.Status = "إشارة حية من جدول تسجيل المرسل.";
        session.LastActivityUtc = utcNow;
        return previous != SnapshotKey(state, session.Scenario);
    }

    public async Task HydrateAsync(CancellationToken ct = default)
    {
        if (_scopes == null)
        {
            Interlocked.Exchange(ref _hydrated, 1);
            return;
        }

        if (Interlocked.Exchange(ref _hydrated, 1) == 1)
        {
            return;
        }

        using IServiceScope scope = _scopes.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTime now = DateTime.UtcNow;
        List<CalibrationSessionRecord> rows = await db.CalibrationSessions.AsNoTracking()
            .Where(r => r.ExpiresAtUtc > now)
            .ToListAsync(ct);
        foreach (CalibrationSessionRecord row in rows)
        {
            CalibrationSession? session = FromRecord(row);
            if (session != null)
            {
                _sessions[session.Code] = session;
            }
        }

        _logger.LogInformation("Hydrated {Count} calibration sessions.", rows.Count);
    }

    public async Task PersistActivityAsync(CancellationToken ct = default)
    {
        if (_scopes == null)
        {
            return;
        }

        PruneMemory();
        using IServiceScope scope = _scopes.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTime now = DateTime.UtcNow;
        List<CalibrationSession> live = _sessions.Values.Where(s => s.ExpiresAtUtc > now).ToList();
        foreach (CalibrationSession session in live)
        {
            CalibrationSessionRecord? row = await db.CalibrationSessions
                .FirstOrDefaultAsync(r => r.Code == session.Code, ct);
            if (row == null)
            {
                db.CalibrationSessions.Add(ToRecord(session));
            }
            else
            {
                CopyToRecord(session, row);
            }
        }

        List<CalibrationSessionRecord> expired = await db.CalibrationSessions
            .Where(r => r.ExpiresAtUtc < now)
            .ToListAsync(ct);
        if (expired.Count > 0)
        {
            db.CalibrationSessions.RemoveRange(expired);
        }

        await db.SaveChangesAsync(ct);
    }

    public CalibrationSnapshot ToSnapshot(CalibrationSession session)
    {
        AntennaAlignmentResult a = session.Alignment;
        CalibrationLiveSnapshot tx = BuildLive(session.Transmitter, a.TransmitterMagneticAzimuthDegrees, a.TransmitterElevationDegrees);
        CalibrationLiveSnapshot rx = BuildLive(session.Receiver, a.ReceiverMagneticAzimuthDegrees, a.ReceiverElevationDegrees);
        bool mutual = session.Transmitter.AzimuthDegrees is double taz
            && session.Receiver.AzimuthDegrees is double raz
            && PhoneBoresightMath.MutualAzimuthAligned(taz, raz);
        bool geometryLocked = tx.HorizontalAligned && tx.VerticalAligned && rx.HorizontalAligned && rx.VerticalAligned;
        if (CalibrationOptionValues.NormalizeCrew(session.Scenario.CrewMode) == CalibrationOptionValues.CrewSolo)
        {
            geometryLocked = rx.HorizontalAligned && rx.VerticalAligned;
        }

        CalibrationRadioSnapshot radio = session.Radio.ToSnapshot(session.Scenario);
        if (!session.Scenario.NeedsRadio)
        {
            radio = new CalibrationRadioSnapshot
            {
                Available = false,
                Stale = false,
                Status = "هذا السيناريو هندسي فقط — بدون إشارة حية.",
                NearPeak = false,
                PeakLocked = false,
                MinReached = false,
                SuccessMet = geometryLocked
            };
        }

        return new CalibrationSnapshot
        {
            Code = session.Code,
            SectorName = session.SectorName,
            ReceiverName = session.ReceiverName,
            SavedReceiver = session.ReceiverId is > 0,
            Scenario = session.Scenario,
            ScenarioLabel = session.Scenario.Name,
            DistanceMeters = a.DistanceMeters,
            MagneticDeclinationDegrees = a.MagneticDeclinationDegrees,
            ExpectedSignalHint = BuildExpectedSignalHint(a.DistanceMeters),
            TransmitterTarget = new CalibrationTargetSnapshot
            {
                AzimuthTrue = a.TransmitterAzimuthDegrees,
                AzimuthMagnetic = a.TransmitterMagneticAzimuthDegrees,
                Elevation = a.TransmitterElevationDegrees,
                Cardinal = a.TransmitterCardinal
            },
            ReceiverTarget = new CalibrationTargetSnapshot
            {
                AzimuthTrue = a.ReceiverAzimuthDegrees,
                AzimuthMagnetic = a.ReceiverMagneticAzimuthDegrees,
                Elevation = a.ReceiverElevationDegrees,
                Cardinal = a.ReceiverCardinal
            },
            TransmitterLive = tx,
            ReceiverLive = rx,
            PathSummary = session.PathSummary,
            PathClear = session.PathClear,
            Radio = radio,
            MutualFacing = mutual,
            GeometryLocked = geometryLocked,
            Advice = CalibrationAdvice.Build(session, tx, rx, geometryLocked, radio)
        };
    }

    private CalibrationSession? LoadFromDatabase(string code)
    {
        if (_scopes == null)
        {
            return null;
        }

        try
        {
            using IServiceScope scope = _scopes.CreateScope();
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            CalibrationSessionRecord? row = db.CalibrationSessions.AsNoTracking()
                .FirstOrDefault(r => r.Code == code);
            CalibrationSession? session = row == null ? null : FromRecord(row);
            if (session != null)
            {
                _sessions[session.Code] = session;
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed loading calibration session {Code}", code);
            return null;
        }
    }

    private void Persist(CalibrationSession session, bool insert)
    {
        if (_scopes == null)
        {
            return;
        }

        try
        {
            using IServiceScope scope = _scopes.CreateScope();
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (insert)
            {
                db.CalibrationSessions.Add(ToRecord(session));
                db.SaveChanges();
                return;
            }

            CalibrationSessionRecord? row = db.CalibrationSessions.FirstOrDefault(r => r.Code == session.Code);
            if (row == null)
            {
                db.CalibrationSessions.Add(ToRecord(session));
            }
            else
            {
                CopyToRecord(session, row);
            }

            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed persisting calibration session {Code}", session.Code);
        }
    }

    private static CalibrationSessionRecord ToRecord(CalibrationSession session)
    {
        CalibrationSessionRecord row = new();
        CopyToRecord(session, row);
        return row;
    }

    private static void CopyToRecord(CalibrationSession session, CalibrationSessionRecord row)
    {
        row.Code = session.Code;
        row.NetworkId = session.NetworkId;
        row.SectorId = session.SectorId;
        row.ReceiverId = session.ReceiverId;
        row.ScenarioId = session.Scenario.ScenarioId;
        row.ScenarioName = session.Scenario.Name;
        row.ScenarioJson = session.Scenario.ToJson();
        row.SectorName = session.SectorName;
        row.ReceiverName = session.ReceiverName;
        row.ReceiverLatitude = session.ReceiverLatitude;
        row.ReceiverLongitude = session.ReceiverLongitude;
        row.ReceiverIp = session.ReceiverIp;
        row.ReceiverMac = session.ReceiverMac;
        row.SectorAntennaMsl = session.SectorAntennaMsl;
        row.ReceiverAntennaMsl = session.ReceiverAntennaMsl;
        row.AlignmentJson = JsonSerializer.Serialize(session.Alignment, JsonOptions);
        row.PathSummary = session.PathSummary;
        row.PathClear = session.PathClear;
        row.CreatedAtUtc = session.CreatedAtUtc;
        row.LastActivityUtc = session.LastActivityUtc;
        row.ExpiresAtUtc = session.ExpiresAtUtc;
    }

    private CalibrationSession? FromRecord(CalibrationSessionRecord row)
    {
        try
        {
            AntennaAlignmentResult alignment = JsonSerializer.Deserialize<AntennaAlignmentResult>(row.AlignmentJson, JsonOptions);
            if (alignment.DistanceMeters <= 0 && string.IsNullOrWhiteSpace(alignment.TransmitterCardinal))
            {
                return null;
            }

            return new CalibrationSession
            {
                Code = row.Code,
                NetworkId = row.NetworkId,
                SectorId = row.SectorId,
                ReceiverId = row.ReceiverId,
                SectorName = row.SectorName,
                ReceiverName = row.ReceiverName,
                ReceiverLatitude = row.ReceiverLatitude,
                ReceiverLongitude = row.ReceiverLongitude,
                ReceiverIp = row.ReceiverIp,
                ReceiverMac = row.ReceiverMac,
                Alignment = alignment,
                SectorAntennaMsl = row.SectorAntennaMsl,
                ReceiverAntennaMsl = row.ReceiverAntennaMsl,
                PathSummary = row.PathSummary ?? string.Empty,
                PathClear = row.PathClear,
                Scenario = CalibrationScenarioSnapshot.FromJson(row.ScenarioJson),
                CreatedAtUtc = row.CreatedAtUtc,
                LastActivityUtc = row.LastActivityUtc,
                ExpiresAtUtc = row.ExpiresAtUtc
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed deserializing calibration session {Code}", row.Code);
            return null;
        }
    }

    private static CalibrationLiveSnapshot BuildLive(
        CalibrationEndpointPose live,
        double targetAzimuthMagnetic,
        double targetElevation)
    {
        double? azDelta = live.AzimuthDegrees is double az
            ? LineOfSightMath.SignedAngleDeltaDegrees(az, targetAzimuthMagnetic)
            : null;
        double? elDelta = live.ElevationDegrees is double el
            ? el - targetElevation
            : null;
        return new CalibrationLiveSnapshot
        {
            Connected = live.Connected,
            PoseValid = live.PoseValid,
            Azimuth = live.AzimuthDegrees,
            Elevation = live.ElevationDegrees,
            AzimuthDelta = azDelta,
            ElevationDelta = elDelta,
            HorizontalAligned = azDelta is double dAz && Math.Abs(dAz) <= PhoneBoresightMath.HorizontalAlignToleranceDegrees,
            VerticalAligned = elDelta is double dEl && Math.Abs(dEl) <= PhoneBoresightMath.VerticalAlignToleranceDegrees,
            AccuracyDegrees = live.AccuracyDegrees,
            CompassUnstable = PhoneBoresightMath.CompassUnstable(live.AccuracyDegrees)
        };
    }

    private static CalibrationEndpointPose RoleEndpoint(CalibrationSession session, string role) =>
        string.Equals(role, "tx", StringComparison.OrdinalIgnoreCase) ? session.Transmitter : session.Receiver;

    private static string SnapshotKey(CalibrationRadioState state, CalibrationScenarioSnapshot scenario)
    {
        CalibrationRadioSnapshot snap = state.ToSnapshot(scenario);
        return string.Join('|',
            state.Available,
            state.Status,
            state.MatchReason,
            state.SignalDbm,
            state.PeakSignalDbm,
            state.SnrDb,
            state.PeakSnrDb,
            state.CcqPercent,
            state.MacAddress,
            state.LastIp,
            snap.NearPeak,
            snap.PeakLocked,
            snap.SuccessMet,
            state.NearPeakSinceUtc?.Ticks);
    }

    private static string? BuildExpectedSignalHint(double distanceMeters)
    {
        if (distanceMeters < 50)
        {
            return "مسافة قصيرة: القمة عادة قوية. إن بقيت ضعيفة تحقق من الاستقطاب والمسار.";
        }

        if (distanceMeters < 1500)
        {
            return $"مسافة {distanceMeters:0} م: اضبط على أعلى RSSI. ضعف كبير عن المعتاد غالباً عائق أو توجيه بعيد.";
        }

        return $"مسافة {distanceMeters:0} م: تحرّك أبطأ وراقب القمة وSNR. المسارات الطويلة أكثر حساسية للفريسنل.";
    }

    private void PruneMemory()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, CalibrationSession> pair in _sessions)
        {
            if (pair.Value.ExpiresAtUtc < now || now - pair.Value.LastActivityUtc > Ttl)
            {
                _sessions.TryRemove(pair.Key, out _);
            }
        }
    }

    public static string NewCode()
    {
        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        Span<char> chars = stackalloc char[6];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }

        return new string(chars);
    }
}

public static class CalibrationAdvice
{
    public static string Build(
        CalibrationSession session,
        CalibrationLiveSnapshot tx,
        CalibrationLiveSnapshot rx,
        bool geometryLocked,
        CalibrationRadioSnapshot radio)
    {
        if (!session.PathClear && !string.IsNullOrWhiteSpace(session.PathSummary))
        {
            return session.PathSummary;
        }

        if (radio.SuccessMet)
        {
            if (radio.PeakLocked)
            {
                return $"قفل قمة مستقر ({radio.SignalDbm} dBm). اربط البراغي الآن ثم أعد التحقق.";
            }

            if (radio.MinReached)
            {
                return $"تم تجاوز الحد الأدنى ({radio.SignalDbm} dBm). اربط البراغي.";
            }

            return $"عند القمة أو قربها ({radio.SignalDbm} dBm). اربط البراغي الآن.";
        }

        string aim = CalibrationOptionValues.NormalizeAim(session.Scenario.AimMode);
        if (aim == CalibrationOptionValues.AimGeometry)
        {
            if (geometryLocked)
            {
                return "المحاذاة الهندسية جاهزة. ثبّت ثم تحقق ميدانياً من الإشارة إن أمكن.";
            }

            return "اتبع الأسهم حتى يقترب السمت والميل من القيم المطلوبة.";
        }

        if (!session.Transmitter.Connected && !session.Receiver.Connected)
        {
            return aim switch
            {
                CalibrationOptionValues.AimPro => "افتح شاشة الموبايل، ابدأ المراقبة، امسح أفقياً ثم عمودياً حتى قفل القمة.",
                CalibrationOptionValues.AimCompass => "فعّل البوصلة للتقريب ثم اقفل على أعلى رقم إشارة.",
                _ => "افتح شاشة الموبايل، ابدأ المراقبة، حرّك حتى أعلى رقم، ثم اربط."
            };
        }

        if (radio.Available && radio.SignalDbm is int liveSignal)
        {
            string peak = radio.PeakSignalDbm is int p ? $" · قمة {p} dBm" : "";
            string snr = radio.SnrDb is int s ? $" · SNR {s} dB" : "";
            if (radio.NearPeak)
            {
                return $"قرب القمة ({liveSignal} dBm{peak}{snr}). ثبّت عند أفضل قيمة.";
            }

            if (aim == CalibrationOptionValues.AimPro)
            {
                return $"احترافي: راقب {liveSignal} dBm{peak}{snr}. امسح محوراً واحداً ببطء.";
            }

            return $"راقب الإشارة {liveSignal} dBm{peak}. حرّك ببطء حتى القمة ثم اربط.";
        }

        if (tx.CompassUnstable || rx.CompassUnstable)
        {
            return "البوصلة مشوّشة قرب المعدن. اعتمد قمة الإشارة إن توفرت.";
        }

        return "حرّك ببطء وراقب الرقم. القفل النهائي بقمة الإشارة وليس بدرجة البوصلة.";
    }
}
