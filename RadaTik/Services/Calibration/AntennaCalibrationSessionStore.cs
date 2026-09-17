using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.SectorRadio;

namespace RadaTik.Services.Calibration;

public interface IAntennaCalibrationSessionStore
{
    AntennaCalibrationSession Create(AntennaCalibrationSession session);
    AntennaCalibrationSession? Get(string code);
    IReadOnlyList<AntennaCalibrationSession> ListHot(TimeSpan maxIdle);
    IReadOnlyList<AntennaCalibrationSessionListItem> ListRecent(int networkId, int take = 8);
    bool TryUpdatePose(string code, string role, PhoneBoresightPose pose, string connectionId, double? accuracyDegrees);
    void SetConnected(string code, string role, string connectionId, bool connected);
    void BindConnection(string connectionId, string code, string? role);
    void UnbindConnection(string connectionId);
    bool TryResetPeak(string code);
    bool TryApplyRadio(string code, SectorRadioStationsResult radio, DateTime utcNow);
    Task HydrateAsync(CancellationToken ct = default);
    Task PersistActivityAsync(CancellationToken ct = default);
    AntennaCalibrationSnapshot ToSnapshot(AntennaCalibrationSession session);
}

public sealed class AntennaCalibrationSessionStore : IAntennaCalibrationSessionStore
{
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(4);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentDictionary<string, AntennaCalibrationSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (string Code, string? Role)> _connections = new(StringComparer.Ordinal);
    private readonly IServiceScopeFactory? _scopes;
    private readonly ILogger<AntennaCalibrationSessionStore> _logger;
    private int _hydrated;

    public AntennaCalibrationSessionStore()
        : this(null, Microsoft.Extensions.Logging.Abstractions.NullLogger<AntennaCalibrationSessionStore>.Instance)
    {
    }

    public AntennaCalibrationSessionStore(IServiceScopeFactory? scopes, ILogger<AntennaCalibrationSessionStore> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public AntennaCalibrationSession Create(AntennaCalibrationSession session)
    {
        PruneMemory();
        _sessions[session.Code] = session;
        Persist(session, insert: true);
        return session;
    }

    public AntennaCalibrationSession? Get(string code)
    {
        EnsureHydrated();
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        string key = code.Trim();
        if (_sessions.TryGetValue(key, out AntennaCalibrationSession? session))
        {
            if (DateTime.UtcNow > session.ExpiresAtUtc || DateTime.UtcNow - session.LastActivityUtc > Ttl)
            {
                _sessions.TryRemove(session.Code, out _);
                return null;
            }

            return session;
        }

        return LoadFromDatabase(key);
    }

    public IReadOnlyList<AntennaCalibrationSession> ListHot(TimeSpan maxIdle)
    {
        EnsureHydrated();
        DateTime cutoff = DateTime.UtcNow - maxIdle;
        return _sessions.Values
            .Where(s => s.LastActivityUtc >= cutoff && DateTime.UtcNow <= s.ExpiresAtUtc)
            .ToList();
    }

    public IReadOnlyList<AntennaCalibrationSessionListItem> ListRecent(int networkId, int take = 8)
    {
        EnsureHydrated();
        return _sessions.Values
            .Where(s => s.NetworkId == networkId && DateTime.UtcNow <= s.ExpiresAtUtc)
            .OrderByDescending(s => s.LastActivityUtc)
            .Take(take)
            .Select(s => new AntennaCalibrationSessionListItem
            {
                Code = s.Code,
                SectorName = s.SectorName,
                ReceiverName = s.ReceiverName,
                Workflow = AntennaCalibrationWorkflow.Normalize(s.Workflow),
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToList();
    }

    public bool TryUpdatePose(string code, string role, PhoneBoresightPose pose, string connectionId, double? accuracyDegrees)
    {
        AntennaCalibrationSession? session = Get(code);
        if (session == null)
        {
            return false;
        }

        AntennaCalibrationEndpointPose endpoint = RoleEndpoint(session, role);
        endpoint.Connected = true;
        endpoint.ConnectionId = connectionId;
        endpoint.PoseValid = pose.AzimuthValid;
        endpoint.AzimuthDegrees = pose.AzimuthDegrees;
        endpoint.ElevationDegrees = pose.ElevationDegrees;
        endpoint.AccuracyDegrees = accuracyDegrees;
        endpoint.UpdatedAtUtc = DateTime.UtcNow;
        session.LastActivityUtc = DateTime.UtcNow;
        _connections[connectionId] = (session.Code, NormalizeRole(role));
        return true;
    }

    public void SetConnected(string code, string role, string connectionId, bool connected)
    {
        AntennaCalibrationSession? session = Get(code);
        if (session == null)
        {
            return;
        }

        AntennaCalibrationEndpointPose endpoint = RoleEndpoint(session, role);
        if (!connected && endpoint.ConnectionId != connectionId)
        {
            return;
        }

        endpoint.Connected = connected;
        if (connected)
        {
            endpoint.ConnectionId = connectionId;
            _connections[connectionId] = (session.Code, NormalizeRole(role));
        }

        session.LastActivityUtc = DateTime.UtcNow;
    }

    public void BindConnection(string connectionId, string code, string? role)
    {
        AntennaCalibrationSession? session = Get(code);
        if (session == null)
        {
            return;
        }

        session.LastActivityUtc = DateTime.UtcNow;
        _connections[connectionId] = (session.Code, role);
    }

    public void UnbindConnection(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out (string Code, string? Role) bound))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(bound.Role))
        {
            return;
        }

        SetConnected(bound.Code, bound.Role, connectionId, false);
    }

    public bool TryResetPeak(string code)
    {
        AntennaCalibrationSession? session = Get(code);
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
        AntennaCalibrationSession? session = Get(code);
        if (session == null)
        {
            return false;
        }

        AntennaCalibrationRadioState state = session.Radio;
        string previous = SnapshotKey(state);
        if (!radio.Success)
        {
            state.Available = false;
            state.Stale = state.UpdatedAtUtc is DateTime last && utcNow - last > TimeSpan.FromSeconds(20);
            state.Status = string.IsNullOrWhiteSpace(radio.StatusMessage)
                ? "تعذر قراءة جدول التسجيل من المرسل."
                : radio.StatusMessage;
            state.NearPeakSinceUtc = null;
            return previous != SnapshotKey(state);
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
            state.TxRateMbps = null;
            state.RxRateMbps = null;
            state.NearPeakSinceUtc = null;
            state.Status = string.IsNullOrWhiteSpace(session.ReceiverIp) && string.IsNullOrWhiteSpace(session.ReceiverMac)
                ? "حدد IP أو MAC للمستقبل لربط الإشارة، أو اترك محطة واحدة على الواجهة."
                : "لم تُطابق محطة للمستقبل بعد. تحقق من IP/MAC أو أن الجهاز مرتبط بالمرسل.";
            return previous != SnapshotKey(state);
        }

        RadioStationSignal station = match.Station;
        state.Available = true;
        state.MatchReason = match.Reason;
        state.MacAddress = station.MacAddress;
        state.LastIp = station.LastIp;
        state.SignalDbm = station.SignalDbm;
        state.SnrDb = station.SnrDb;
        state.CcqPercent = station.CcqPercent;
        state.TxRateMbps = station.TxRateMbps;
        state.RxRateMbps = station.RxRateMbps;
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
        return previous != SnapshotKey(state);
    }

    public async Task HydrateAsync(CancellationToken ct = default)
    {
        if (_scopes == null)
        {
            Interlocked.Exchange(ref _hydrated, 1);
            return;
        }

        using IServiceScope scope = _scopes.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTime now = DateTime.UtcNow;
        List<AntennaCalibrationSessionRecord> rows = await db.AntennaCalibrationSessions.AsNoTracking()
            .Where(r => r.ExpiresAtUtc > now)
            .ToListAsync(ct);
        foreach (AntennaCalibrationSessionRecord row in rows)
        {
            AntennaCalibrationSession? session = FromRecord(row);
            if (session != null)
            {
                _sessions.TryAdd(session.Code, session);
            }
        }

        Interlocked.Exchange(ref _hydrated, 1);
    }

    public async Task PersistActivityAsync(CancellationToken ct = default)
    {
        if (_scopes == null)
        {
            return;
        }

        EnsureHydrated();
        DateTime now = DateTime.UtcNow;
        List<AntennaCalibrationSession> live = _sessions.Values.Where(s => s.ExpiresAtUtc > now).ToList();
        if (live.Count == 0)
        {
            return;
        }

        using IServiceScope scope = _scopes.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (AntennaCalibrationSession session in live)
        {
            AntennaCalibrationSessionRecord? row = await db.AntennaCalibrationSessions
                .FirstOrDefaultAsync(r => r.Code == session.Code, ct);
            if (row == null)
            {
                db.AntennaCalibrationSessions.Add(ToRecord(session));
                continue;
            }

            row.LastActivityUtc = session.LastActivityUtc;
            row.ExpiresAtUtc = session.ExpiresAtUtc;
        }

        await db.SaveChangesAsync(ct);

        List<AntennaCalibrationSessionRecord> expired = await db.AntennaCalibrationSessions
            .Where(r => r.ExpiresAtUtc < now.AddHours(-1))
            .Take(50)
            .ToListAsync(ct);
        if (expired.Count > 0)
        {
            db.AntennaCalibrationSessions.RemoveRange(expired);
            await db.SaveChangesAsync(ct);
        }
    }

    public AntennaCalibrationSnapshot ToSnapshot(AntennaCalibrationSession session)
    {
        AntennaAlignmentResult a = session.Alignment;
        AntennaCalibrationLiveSnapshot tx = BuildLive(session.Transmitter, a.TransmitterMagneticAzimuthDegrees, a.TransmitterElevationDegrees);
        AntennaCalibrationLiveSnapshot rx = BuildLive(session.Receiver, a.ReceiverMagneticAzimuthDegrees, a.ReceiverElevationDegrees);
        bool mutual = session.Transmitter.PoseValid
            && session.Receiver.PoseValid
            && session.Transmitter.AzimuthDegrees.HasValue
            && session.Receiver.AzimuthDegrees.HasValue
            && PhoneBoresightMath.MutualAzimuthAligned(session.Transmitter.AzimuthDegrees.Value, session.Receiver.AzimuthDegrees.Value);
        bool geometryLocked = tx.HorizontalAligned && tx.VerticalAligned && rx.HorizontalAligned && rx.VerticalAligned;
        DateTime now = session.Radio.UpdatedAtUtc ?? DateTime.UtcNow;
        AntennaCalibrationRadioSnapshot radio = session.Radio.ToSnapshot(now);
        if (radio.UpdatedAtUtc is DateTime updated && now - updated > TimeSpan.FromSeconds(20))
        {
            radio = new AntennaCalibrationRadioSnapshot
            {
                Available = radio.Available,
                Stale = true,
                Status = radio.Status,
                MatchReason = radio.MatchReason,
                SignalDbm = radio.SignalDbm,
                PeakSignalDbm = radio.PeakSignalDbm,
                SnrDb = radio.SnrDb,
                PeakSnrDb = radio.PeakSnrDb,
                CcqPercent = radio.CcqPercent,
                TxRateMbps = radio.TxRateMbps,
                RxRateMbps = radio.RxRateMbps,
                NoiseFloorDbm = radio.NoiseFloorDbm,
                FrequencyMhz = radio.FrequencyMhz,
                MacAddress = radio.MacAddress,
                LastIp = radio.LastIp,
                UpdatedAtUtc = radio.UpdatedAtUtc,
                NearPeak = radio.NearPeak,
                PeakLocked = radio.PeakLocked
            };
        }

        string workflow = AntennaCalibrationWorkflow.Normalize(session.Workflow);
        return new AntennaCalibrationSnapshot
        {
            Code = session.Code,
            SectorName = session.SectorName,
            ReceiverName = session.ReceiverName,
            SavedReceiver = session.ReceiverId.HasValue,
            Workflow = workflow,
            WorkflowLabel = AntennaCalibrationWorkflow.DisplayName(workflow),
            DistanceMeters = a.DistanceMeters,
            MagneticDeclinationDegrees = a.MagneticDeclinationDegrees,
            ExpectedSignalHint = BuildExpectedSignalHint(a.DistanceMeters),
            TransmitterTarget = new AntennaCalibrationTargetSnapshot
            {
                AzimuthTrue = a.TransmitterAzimuthDegrees,
                AzimuthMagnetic = a.TransmitterMagneticAzimuthDegrees,
                Elevation = a.TransmitterElevationDegrees,
                Cardinal = a.TransmitterCardinal
            },
            ReceiverTarget = new AntennaCalibrationTargetSnapshot
            {
                AzimuthTrue = a.ReceiverAzimuthDegrees,
                AzimuthMagnetic = a.ReceiverMagneticAzimuthDegrees,
                Elevation = a.ReceiverElevationDegrees,
                Cardinal = a.ReceiverCardinal
            },
            TransmitterLive = tx,
            ReceiverLive = rx,
            Path = session.Path,
            Radio = radio,
            MutualFacing = mutual,
            GeometryLocked = geometryLocked,
            Advice = AntennaCalibrationAdvice.Build(session, tx, rx, geometryLocked, radio)
        };
    }

    private AntennaCalibrationSession? LoadFromDatabase(string code)
    {
        if (_scopes == null)
        {
            return null;
        }

        try
        {
            using IServiceScope scope = _scopes.CreateScope();
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            AntennaCalibrationSessionRecord? row = db.AntennaCalibrationSessions.AsNoTracking()
                .FirstOrDefault(r => r.Code == code && r.ExpiresAtUtc > DateTime.UtcNow);
            AntennaCalibrationSession? session = row == null ? null : FromRecord(row);
            if (session != null)
            {
                _sessions[session.Code] = session;
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load calibration session {Code}", code);
            return null;
        }
    }

    private void Persist(AntennaCalibrationSession session, bool insert)
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
                db.AntennaCalibrationSessions.Add(ToRecord(session));
                db.SaveChanges();
                return;
            }

            AntennaCalibrationSessionRecord? row = db.AntennaCalibrationSessions.FirstOrDefault(r => r.Code == session.Code);
            if (row == null)
            {
                db.AntennaCalibrationSessions.Add(ToRecord(session));
            }
            else
            {
                row.LastActivityUtc = session.LastActivityUtc;
                row.ExpiresAtUtc = session.ExpiresAtUtc;
            }

            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Calibration session {Code} stayed in memory only.", session.Code);
        }
    }

    private void EnsureHydrated()
    {
        if (Volatile.Read(ref _hydrated) == 1)
        {
            return;
        }

        try
        {
            HydrateAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Calibration session hydrate skipped.");
            Interlocked.Exchange(ref _hydrated, 1);
        }
    }

    private static AntennaCalibrationSessionRecord ToRecord(AntennaCalibrationSession session) => new()
    {
        Code = session.Code,
        NetworkId = session.NetworkId,
        SectorId = session.SectorId,
        ReceiverId = session.ReceiverId,
        SectorName = session.SectorName,
        ReceiverName = session.ReceiverName,
        ReceiverLatitude = session.ReceiverLatitude,
        ReceiverLongitude = session.ReceiverLongitude,
        ReceiverIp = session.ReceiverIp,
        ReceiverMac = session.ReceiverMac,
        SectorAntennaMsl = session.SectorAntennaMsl,
        ReceiverAntennaMsl = session.ReceiverAntennaMsl,
        AlignmentJson = JsonSerializer.Serialize(session.Alignment, JsonOptions),
        PathJson = JsonSerializer.Serialize(session.Path, JsonOptions),
        Workflow = AntennaCalibrationWorkflow.Normalize(session.Workflow),
        CreatedAtUtc = session.CreatedAtUtc,
        LastActivityUtc = session.LastActivityUtc,
        ExpiresAtUtc = session.ExpiresAtUtc
    };

    private AntennaCalibrationSession? FromRecord(AntennaCalibrationSessionRecord row)
    {
        try
        {
            AntennaAlignmentResult alignment = JsonSerializer.Deserialize<AntennaAlignmentResult>(row.AlignmentJson, JsonOptions);
            AntennaCalibrationPathSnapshot path = string.IsNullOrWhiteSpace(row.PathJson)
                ? AntennaCalibrationPathSnapshot.Unavailable()
                : JsonSerializer.Deserialize<AntennaCalibrationPathSnapshot>(row.PathJson, JsonOptions)
                  ?? AntennaCalibrationPathSnapshot.Unavailable();
            return new AntennaCalibrationSession
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
                Path = path,
                Workflow = AntennaCalibrationWorkflow.Normalize(row.Workflow),
                CreatedAtUtc = row.CreatedAtUtc,
                LastActivityUtc = row.LastActivityUtc,
                ExpiresAtUtc = row.ExpiresAtUtc
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not restore calibration session {Code}", row.Code);
            return null;
        }
    }

    private static AntennaCalibrationLiveSnapshot BuildLive(
        AntennaCalibrationEndpointPose live,
        double targetAzimuth,
        double targetElevation)
    {
        double? azDelta = live.AzimuthDegrees is double az
            ? LineOfSightMath.SignedAngleDeltaDegrees(az, targetAzimuth)
            : null;
        double? elDelta = live.ElevationDegrees is double el ? targetElevation - el : null;
        return new AntennaCalibrationLiveSnapshot
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

    private static AntennaCalibrationEndpointPose RoleEndpoint(AntennaCalibrationSession session, string role) =>
        string.Equals(role, "tx", StringComparison.OrdinalIgnoreCase) ? session.Transmitter : session.Receiver;

    private static string NormalizeRole(string? role) =>
        string.Equals(role, "tx", StringComparison.OrdinalIgnoreCase) ? "tx" : "rx";

    private static string SnapshotKey(AntennaCalibrationRadioState state)
    {
        bool nearPeak = state.SignalDbm is int live
            && state.PeakSignalDbm is int peak
            && live >= peak - 2;
        bool peakLocked = nearPeak
            && state.NearPeakSinceUtc is DateTime since
            && (state.UpdatedAtUtc ?? DateTime.UtcNow) - since >= TimeSpan.FromSeconds(3);
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
            nearPeak,
            peakLocked,
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
        foreach (KeyValuePair<string, AntennaCalibrationSession> pair in _sessions)
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

public static class AntennaCalibrationAdvice
{
    public static string Build(
        AntennaCalibrationSession session,
        AntennaCalibrationLiveSnapshot tx,
        AntennaCalibrationLiveSnapshot rx,
        bool geometryLocked,
        AntennaCalibrationRadioSnapshot radio)
    {
        if (session.Path.Analyzed && !session.Path.PathClear && !session.Path.TerrainClear)
        {
            return session.Path.Summary;
        }

        if (radio.Available && radio.PeakLocked)
        {
            return $"قفل قمة مستقر ({radio.SignalDbm} dBm). ثبّت البراغي الآن ثم أعد التحقق بعد التثبيت.";
        }

        if (geometryLocked)
        {
            if (radio.Available && radio.SignalDbm is int signal)
            {
                string peak = radio.PeakSignalDbm is int p ? $" · أفضل قمة {p} dBm" : "";
                return $"الطرفان محاذيان هندسياً. الإشارة {signal} dBm{peak}. حرّك ببطء حول القمة ثم ثبّت.";
            }

            return "الطرفان محاذيان هندسياً. ثبّت البراغي ثم راقب قمة الإشارة.";
        }

        if (!session.Transmitter.Connected && !session.Receiver.Connected)
        {
            if (AntennaCalibrationWorkflow.IsPro(session.Workflow))
            {
                return "مسار احترافي: الصق الموبايل على ظهر الصحن، ابدأ المراقبة، ثم امسح أفقياً ثم عمودياً حتى قفل القمة.";
            }

            if (AntennaCalibrationWorkflow.IsQuick(session.Workflow))
            {
                return "مسار تقريبي: فعّل البوصلة للتقريب ثم ثبّت عند قمة RSSI.";
            }

            return "افتح شاشة الميدان، الصق الموبايل على ظهر الصحن، وفضّل وضع «إشارة فقط» لقمة RSSI. البوصلة تقريبية فقط.";
        }

        if (radio.Available && radio.SignalDbm is int liveSignal)
        {
            string peak = radio.PeakSignalDbm is int p ? $" · قمة {p} dBm" : "";
            string snr = radio.SnrDb is int s ? $" · SNR {s} dB" : "";
            if (radio.NearPeak)
            {
                return $"قرب القمة ({liveSignal} dBm{peak}{snr}). ثبّت عند أفضل قيمة ثم اربط البراغي.";
            }

            if (AntennaCalibrationWorkflow.IsPro(session.Workflow))
            {
                return $"احترافي: راقب {liveSignal} dBm{peak}{snr}. امسح محوراً واحداً ببطء حتى القمة ثم انتقل للمحور التالي.";
            }

            return $"راقب الإشارة الحية {liveSignal} dBm{peak}. حرّك ببطء جداً حتى تصل للقمة ثم ثبّت.";
        }

        if (tx.CompassUnstable || rx.CompassUnstable)
        {
            return "البوصلة مشوّشة قرب المعدن. انتقل لوضع «إشارة فقط» واصطد قمة RSSI.";
        }

        if (session.Path.Analyzed && !session.Path.FresnelClear)
        {
            return session.Path.Summary + " البوصلة تقريبية؛ اعتمد قمة الإشارة للقفل النهائي.";
        }

        return "البوصلة للتقريب فقط. الأفضل: وضع إشارة فقط وحرّك حتى أعلى RSSI ثم ثبّت.";
    }
}
