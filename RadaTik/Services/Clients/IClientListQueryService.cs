using System.Security.Claims;
using RadaTik.Models;

namespace RadaTik.Services.Clients;

public interface IClientListQueryService
{
    Task<ClientIndexPageModel> BuildIndexPageAsync(
        ApplicationUser user,
        ClaimsPrincipal principal,
        IReadOnlyList<string> userRoles,
        int? selectedNetworkId,
        CancellationToken ct = default);

    /// <summary>
    /// يجلب معرّفات المشتركين المتصلين حالياً من اللقطة المخزّنة (مع تحديث خلفي).
    /// </summary>
    Task<HashSet<int>> GetLiveConnectedClientIdsAsync(
        int networkId,
        bool forceRefresh = false,
        CancellationToken ct = default);

    Task<ClientLiveConnectionStatus> GetLiveConnectionStatusAsync(
        int networkId,
        bool forceRefresh = false,
        CancellationToken ct = default);

    Task<ClientDetailsPageModel> BuildDetailsPageAsync(
        int clientId,
        ApplicationUser user,
        ClaimsPrincipal principal,
        IReadOnlyList<string> userRoles,
        bool canLoadMikroTikInfo,
        CancellationToken ct = default);
}
