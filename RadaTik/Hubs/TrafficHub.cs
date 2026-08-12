using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.Traffic;

namespace RadaTik.Hubs;

/// <summary>
/// Real-time interface traffic from MikroTik. Groups: Company_{networkId}_{serverId}.
/// SPA role <c>company_manager</c> maps to <see cref="RoleNames.NetworkAdministrator"/>.
/// العميل (<see cref="RoleNames.Client"/>) يُسمح له فقط ببث خادم MikroTik المرتبط بملفه.
/// </summary>
[Authorize(Roles = RoleNames.NetworkAdministrator + "," + RoleNames.Client)]
public sealed class TrafficHub : Hub
{
    public const string TrafficUpdateMethod = "trafficUpdate";
    public const string TrafficErrorMethod = "trafficError";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITrafficMonitoringCoordinator _coordinator;
    private readonly ILogger<TrafficHub> _logger;

    public TrafficHub(
        IServiceScopeFactory scopeFactory,
        ITrafficMonitoringCoordinator coordinator,
        ILogger<TrafficHub> logger)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
        _logger = logger;
    }

    public static string GroupName(int networkId, int serverId) => $"Company_{networkId}_{serverId}";

    /// <summary>
    /// Join streaming for a MikroTik server that belongs to the caller's current network.
    /// </summary>
    public async Task JoinTraffic(int networkId, int mikrotikServerId)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        HttpContext? http = Context.GetHttpContext();
        if (http == null)
        {
            throw new HubException("Missing HTTP context.");
        }

        ClaimsPrincipal principal = Context.User ?? new ClaimsPrincipal();
        ApplicationUser? user = await userManager.GetUserAsync(principal);
        if (user == null)
        {
            throw new HubException("User not found.");
        }

        if (!await NetworkHelper.IsNetworkAccessibleAsync(http, db, user, networkId))
        {
            throw new HubException("Network is not accessible for this account.");
        }

        if (http.User.IsInRole(RoleNames.Client))
        {
            if (user.ClientId is not int clientEntityId)
            {
                throw new HubException("الحساب غير مرتبط بملف عميل.");
            }

            Client? clientRow = await db.Clients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clientEntityId);

            if (clientRow == null || !clientRow.IsActive)
            {
                throw new HubException("ملف العميل غير متاح أو غير مفعّل.");
            }

            if (clientRow.MikroTikServerId != mikrotikServerId)
            {
                throw new HubException("غير مصرح بمراقبة هذا الخادم.");
            }
        }

        MikroTikServer? server = await db.MikroTikServers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == mikrotikServerId && s.NetworkId == networkId);

        if (server == null || !server.IsActive)
        {
            throw new HubException("MikroTik server not found, inactive, or not in this network.");
        }

        string group = GroupName(networkId, mikrotikServerId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _coordinator.RegisterClient(Context.ConnectionId, (networkId, mikrotikServerId));

        _logger.LogInformation(
            "Traffic join: user={UserId} connection={ConnectionId} group={Group}",
            user.Id,
            Context.ConnectionId,
            group);
    }

    public async Task LeaveTraffic(int networkId, int mikrotikServerId)
    {
        string group = GroupName(networkId, mikrotikServerId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        _coordinator.UnregisterClient(Context.ConnectionId, (networkId, mikrotikServerId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _coordinator.UnregisterAllForConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
