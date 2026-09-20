using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RadaTik.Services;
using RadaTik.Services.Calibration;

namespace RadaTik.Hubs;

[AllowAnonymous]
public sealed class CalibrationHub(ICalibrationSessionStore sessions) : Hub
{
    public const string SessionUpdatedMethod = "sessionUpdated";

    public static string GroupName(string code) => "cal:" + code.Trim().ToUpperInvariant();

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        sessions.ClearConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task JoinMonitor(string code)
    {
        CalibrationSession session = RequireSession(code);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(session.Code));
        await Clients.Caller.SendAsync(SessionUpdatedMethod, sessions.ToSnapshot(session));
    }

    public async Task JoinField(string code, string role)
    {
        CalibrationSession session = RequireSession(code);
        string normalized = NormalizeRole(role);
        if (!session.Scenario.IsDual && normalized == "tx")
        {
            throw new HubException("هذا السيناريو لفني واحد عند المستقبل.");
        }

        sessions.SetConnected(session.Code, normalized, Context.ConnectionId, true);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(session.Code));
        await Clients.Group(GroupName(session.Code))
            .SendAsync(SessionUpdatedMethod, sessions.ToSnapshot(session));
    }

    public async Task PublishOrientation(
        string code,
        string role,
        double alpha,
        double beta,
        double gamma,
        bool absolute,
        double? accuracyDegrees)
    {
        CalibrationSession session = RequireSession(code);
        PhoneBoresightPose pose = PhoneBoresightMath.FromDeviceOrientation(alpha, beta, gamma);
        PhoneBoresightPose dishBack = PhoneBoresightMath.ToAntennaBoresightFromDishBack(pose);
        double az = PhoneBoresightMath.ToMagneticAzimuth(
            dishBack.AzimuthDegrees,
            session.Alignment.MagneticDeclinationDegrees,
            fromTrueNorth: absolute);
        PhoneBoresightPose magnetic = dishBack with { AzimuthDegrees = az };
        if (!sessions.TryUpdatePose(session.Code, NormalizeRole(role), magnetic, Context.ConnectionId, accuracyDegrees))
        {
            throw new HubException("تعذر تحديث الاتجاه.");
        }

        CalibrationSession fresh = sessions.Get(session.Code) ?? session;
        await Clients.Group(GroupName(fresh.Code))
            .SendAsync(SessionUpdatedMethod, sessions.ToSnapshot(fresh));
    }

    public async Task ResetPeak(string code)
    {
        CalibrationSession session = RequireSession(code);
        if (!sessions.TryResetPeak(session.Code))
        {
            throw new HubException("تعذر تصفير القمة.");
        }

        CalibrationSession fresh = sessions.Get(session.Code) ?? session;
        await Clients.Group(GroupName(fresh.Code))
            .SendAsync(SessionUpdatedMethod, sessions.ToSnapshot(fresh));
    }

    private CalibrationSession RequireSession(string code)
    {
        CalibrationSession? session = sessions.Get(code ?? string.Empty);
        if (session == null)
        {
            throw new HubException("انتهت الجلسة أو الرمز غير صالح.");
        }

        return session;
    }

    private static string NormalizeRole(string? role) =>
        string.Equals(role, "tx", StringComparison.OrdinalIgnoreCase) ? "tx" : "rx";
}
