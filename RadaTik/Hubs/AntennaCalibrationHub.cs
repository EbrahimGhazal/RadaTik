using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.Calibration;

namespace RadaTik.Hubs;

[Authorize(Roles = RoleNames.NetworkAdministrator + "," + RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy + "," + RoleNames.SystemAdministrator)]
public sealed class AntennaCalibrationHub(IAntennaCalibrationSessionStore sessions) : Hub
{
    public const string SessionUpdatedMethod = "sessionUpdated";

    public static string GroupName(string code) => "calib:" + code.Trim().ToUpperInvariant();

    public async Task JoinMonitor(string code)
    {
        AntennaCalibrationSession session = RequireSession(code);
        sessions.BindConnection(Context.ConnectionId, session.Code, null);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(session.Code));
        await Clients.Caller.SendAsync(SessionUpdatedMethod, sessions.ToSnapshot(session));
    }

    public async Task JoinField(string code, string role)
    {
        AntennaCalibrationSession session = RequireSession(code);
        string normalized = NormalizeRole(role);
        sessions.BindConnection(Context.ConnectionId, session.Code, normalized);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(session.Code));
        sessions.SetConnected(session.Code, normalized, Context.ConnectionId, true);
        await Clients.Group(GroupName(session.Code)).SendAsync(SessionUpdatedMethod, sessions.ToSnapshot(session));
    }

    public async Task PublishOrientation(
        string code,
        string role,
        double alpha,
        double beta,
        double gamma,
        bool @absolute,
        double? accuracy)
    {
        AntennaCalibrationSession session = RequireSession(code);
        string normalized = NormalizeRole(role);
        PhoneBoresightPose pose = PhoneBoresightMath.FromDeviceOrientation(alpha, beta, gamma);
        PhoneBoresightPose dishBack = PhoneBoresightMath.ToAntennaBoresightFromDishBack(pose);
        double az = PhoneBoresightMath.ToMagneticAzimuth(
            dishBack.AzimuthDegrees,
            session.Alignment.MagneticDeclinationDegrees,
            fromTrueNorth: @absolute);
        PhoneBoresightPose magnetic = dishBack with { AzimuthDegrees = az };
        if (!sessions.TryUpdatePose(session.Code, normalized, magnetic, Context.ConnectionId, accuracy))
        {
            throw new HubException("تعذر تحديث وضع الهوائي.");
        }

        AntennaCalibrationSession fresh = sessions.Get(session.Code) ?? session;
        await Clients.Group(GroupName(session.Code)).SendAsync(SessionUpdatedMethod, sessions.ToSnapshot(fresh));
    }

    public async Task ResetPeak(string code)
    {
        AntennaCalibrationSession session = RequireSession(code);
        if (!sessions.TryResetPeak(session.Code))
        {
            throw new HubException("تعذر تصفير القمة.");
        }

        AntennaCalibrationSession fresh = sessions.Get(session.Code) ?? session;
        await Clients.Group(GroupName(session.Code)).SendAsync(SessionUpdatedMethod, sessions.ToSnapshot(fresh));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        sessions.UnbindConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private AntennaCalibrationSession RequireSession(string code)
    {
        AntennaCalibrationSession? session = sessions.Get(code ?? string.Empty);
        if (session == null)
        {
            throw new HubException("انتهت الجلسة أو الرمز غير صحيح.");
        }

        return session;
    }

    private static string NormalizeRole(string? role) =>
        string.Equals(role, "tx", StringComparison.OrdinalIgnoreCase) ? "tx" : "rx";
}
