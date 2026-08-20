using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Security;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.Clients;

public sealed class ClientListQueryService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IPermissionService permissionService,
    IClientRenewalGuardService renewalGuardService,
    IClientPendingApprovalQueryService pendingApprovalQuery,
    IMikroTikPppoeUserService mikroTikPppoe,
    IMemoryCache memoryCache,
    ILogger<ClientListQueryService> logger)
    : ApplicationServiceBase(context), IClientListQueryService
{
    private static readonly TimeSpan ConnectedCacheDuration = TimeSpan.FromSeconds(45);

    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IPermissionService _permissionService = permissionService;
    private readonly IClientRenewalGuardService _renewalGuard = renewalGuardService;
    private readonly IClientPendingApprovalQueryService _pendingApproval = pendingApprovalQuery;
    private readonly IMikroTikPppoeUserService _mikroTik = mikroTikPppoe;
    private readonly IMemoryCache _cache = memoryCache;
    private readonly ILogger<ClientListQueryService> _logger = logger;

    public async Task<ClientIndexPageModel> BuildIndexPageAsync(
        ApplicationUser user,
        ClaimsPrincipal principal,
        IReadOnlyList<string> userRoles,
        int? selectedNetworkId,
        CancellationToken ct = default)
    {
        bool isEmployee = userRoles.Contains(RoleNames.CompanyEmployee) ||
                          userRoles.Contains(RoleNames.EmployeeLegacy);
        bool isClientOnly = userRoles.Contains(RoleNames.Client) && !isEmployee &&
                            !userRoles.Contains(RoleNames.NetworkAdministrator);

        IQueryable<Client> query = Db.Clients
            .Include(c => c.Receiver)
                .ThenInclude(r => r!.Sector)
            .Include(c => c.MikroTikServer)
            .Include(c => c.Profile);

        List<int> companyNetworkIds = [];
        if (isClientOnly)
        {
            if (user.ClientId == null)
            {
                query = query.Where(c => false);
            }
            else
            {
                query = query.Where(c => c.Id == user.ClientId);
            }
        }
        else
        {
            if (!await _permissionService.HasPermissionAsync(principal, "Clients.View"))
            {
                return new ClientIndexPageModel { Access = ClientListAccessOutcome.Forbidden };
            }

            if (!selectedNetworkId.HasValue)
            {
                return new ClientIndexPageModel { Access = ClientListAccessOutcome.RequiresNetworkSelection };
            }

            companyNetworkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsForSelectedAsync(
                Db,
                selectedNetworkId.Value,
                ct);
            query = query.Where(c => c.NetworkId.HasValue && companyNetworkIds.Contains(c.NetworkId.Value));
        }

        List<Client> clients = await query.ToListAsync(ct);
        List<int> clientIds = clients.Select(c => c.Id).ToList();
        Dictionary<int, string> dbAccountMap = await Db.Users
            .Where(u => u.ClientId.HasValue && clientIds.Contains(u.ClientId.Value))
            .Select(u => new { ClientId = u.ClientId!.Value, u.UserName })
            .ToDictionaryAsync(x => x.ClientId, x => x.UserName ?? string.Empty, ct);

        HashSet<int> pendingIds = selectedNetworkId.HasValue
            ? await _pendingApproval.GetPendingClientIdsAsync(selectedNetworkId.Value, ct)
            : [];

        int? connectionScopeId = selectedNetworkId;
        if (companyNetworkIds.Count > 0)
        {
            connectionScopeId = companyNetworkIds[0];
        }

        HashSet<int> connectedIds = [];
        bool connectionsReady = false;
        if (connectionScopeId.HasValue &&
            _cache.TryGetValue(ConnectedCacheKey(connectionScopeId.Value), out HashSet<int>? cached) &&
            cached != null)
        {
            connectedIds = cached;
            connectionsReady = true;
        }

        return new ClientIndexPageModel
        {
            Access = ClientListAccessOutcome.Ok,
            Clients = clients,
            DbAccountMap = dbAccountMap,
            PendingClientIds = pendingIds,
            ConnectedClientIds = connectedIds,
            ConnectionsReady = connectionsReady,
            AvailableNetworks = await NetworkHelper.GetAvailableNetworksAsync(Db, user, _userManager),
            CurrentNetworkId = selectedNetworkId,
            CopyTargetServers = selectedNetworkId.HasValue
                ? await Db.MikroTikServers.AsNoTracking()
                    .Where(s =>
                        s.IsActive
                        && s.NetworkId.HasValue
                        && companyNetworkIds.Contains(s.NetworkId.Value))
                    .OrderBy(s => s.Name)
                    .Select(s => new ClientCopyTargetServerItem
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Host = s.Host
                    })
                    .ToListAsync(ct)
                : []
        };
    }

    public async Task<HashSet<int>> GetLiveConnectedClientIdsAsync(
        int networkId,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        List<int> companyNetworkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsForSelectedAsync(
            Db,
            networkId,
            ct);
        int cacheScopeId = companyNetworkIds.Count > 0 ? companyNetworkIds[0] : networkId;
        string cacheKey = ConnectedCacheKey(cacheScopeId);
        if (!forceRefresh &&
            _cache.TryGetValue(cacheKey, out HashSet<int>? cached) &&
            cached != null)
        {
            return cached;
        }

        List<Client> clients = await Db.Clients
            .AsNoTracking()
            .Where(c =>
                c.NetworkId.HasValue
                && companyNetworkIds.Contains(c.NetworkId.Value)
                && c.UserName != null)
            .Select(c => new Client
            {
                Id = c.Id,
                UserName = c.UserName,
                MikroTikServerId = c.MikroTikServerId,
                IsActive = c.IsActive
            })
            .ToListAsync(ct);

        HashSet<int> serverIds = await Db.MikroTikServers
            .AsNoTracking()
            .Where(s => s.NetworkId.HasValue && companyNetworkIds.Contains(s.NetworkId.Value))
            .Select(s => s.Id)
            .ToHashSetAsync(ct);
        foreach (int assignedServerId in clients
            .Where(c => c.MikroTikServerId.HasValue)
            .Select(c => c.MikroTikServerId!.Value))
        {
            serverIds.Add(assignedServerId);
        }

        HashSet<int> connectedIds = await ResolveConnectedClientIdsAsync(clients, serverIds, ct);
        _cache.Set(cacheKey, connectedIds, ConnectedCacheDuration);
        return connectedIds;
    }

    private static string ConnectedCacheKey(int companyScopeId) => $"clients.connected.company.{companyScopeId}";

    /// <summary>
    /// يحدد المتصلين فعلياً عبر جلسات /ppp/active على كل سيرفرات MikroTik للشركة.
    /// </summary>
    private async Task<HashSet<int>> ResolveConnectedClientIdsAsync(
        IReadOnlyList<Client> clients,
        IReadOnlyCollection<int> serverIds,
        CancellationToken ct)
    {
        Dictionary<int, IReadOnlyCollection<string>> activeNamesByServer = [];

        foreach (int serverId in serverIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                List<Client> activeSessions = await _mikroTik.GetActivePPPoEUsers(serverId);
                HashSet<string> activeUserNames = activeSessions
                    .Select(a => ClientLiveConnectionMatcher.NormalizeUserName(a.UserName))
                    .Where(name => name.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                activeNamesByServer[serverId] = activeUserNames;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "تعذر جلب الجلسات النشطة من سيرفر MikroTik {ServerId} — سيُعرض المشتركون كغير متصلين لهذا السيرفر",
                    serverId);
            }
        }

        return ClientLiveConnectionMatcher.Match(clients, activeNamesByServer);
    }

    public async Task<ClientDetailsPageModel> BuildDetailsPageAsync(
        int clientId,
        ApplicationUser user,
        ClaimsPrincipal principal,
        IReadOnlyList<string> userRoles,
        bool canLoadMikroTikInfo,
        CancellationToken ct = default)
    {
        bool isEmployee = userRoles.Contains(RoleNames.CompanyEmployee) ||
                          userRoles.Contains(RoleNames.EmployeeLegacy);
        bool isClientOnly = userRoles.Contains(RoleNames.Client) && !isEmployee &&
                            !userRoles.Contains(RoleNames.NetworkAdministrator);

        if (isClientOnly && (user.ClientId == null || user.ClientId != clientId))
        {
            return new ClientDetailsPageModel { Access = ClientListAccessOutcome.Forbidden };
        }

        if (!isClientOnly && !await _permissionService.HasPermissionAsync(principal, "Clients.View"))
        {
            return new ClientDetailsPageModel { Access = ClientListAccessOutcome.Forbidden };
        }

        Client? client = await Db.Clients
            .Include(c => c.Receiver)
            .Include(c => c.MikroTikServer)
            .Include(c => c.Profile)
            .FirstOrDefaultAsync(m => m.Id == clientId, ct);

        if (client == null)
        {
            return new ClientDetailsPageModel { Access = ClientListAccessOutcome.NotFound };
        }

        bool isPending = await _pendingApproval.IsPendingClientApprovalAsync(client, ct);
        string? renewalBlocked = null;
        RenewalBlockResult renewalGuard = await _renewalGuard.CheckBlockingInvoicesAsync(client.Id, ct);
        if (!renewalGuard.CanRenew)
        {
            renewalBlocked =
                $"لا يمكن تنفيذ التجديد حالياً قبل تسديد جميع فواتير الصيانة المستحقة (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).";
        }

        Client? mikrotikInfo = null;
        string? mikrotikError = null;
        bool isClientView = false;

        if (canLoadMikroTikInfo && client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
        {
            try
            {
                mikrotikInfo = await _mikroTik.GetPPPoEUserInfo(client.UserName, client.MikroTikServerId.Value);
            }
            catch (Exception ex)
            {
                mikrotikError = MikroTikErrorFormatter.Format("تعذر جلب بيانات MikroTik", ex.Message);
            }
        }
        else if (!canLoadMikroTikInfo)
        {
            isClientView = true;
        }

        List<ClientTopUpTransaction> recentTopUps = await Db.ClientTopUpTransactions
            .Where(t => t.ClientId == client.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .Include(t => t.CreatedByUser)
            .ToListAsync(ct);

        return new ClientDetailsPageModel
        {
            Access = ClientListAccessOutcome.Ok,
            Client = client,
            IsPendingClientApproval = isPending,
            RenewalBlockedMessage = renewalBlocked,
            MikroTikInfo = mikrotikInfo,
            MikroTikError = mikrotikError,
            IsClientView = isClientView,
            IsClientOnly = isClientOnly,
            RecentTopUps = recentTopUps
        };
    }
}
