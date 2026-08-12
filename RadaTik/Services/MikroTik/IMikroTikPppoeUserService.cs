using RadaTik.Models;
using RadaTik.ViewModels.MikroTikServers;

namespace RadaTik.Services.MikroTik;

/// <summary>عمليات مستخدمي PPPoE على أجهزة MikroTik (بدون الاستيراد الجماعي).</summary>
public interface IMikroTikPppoeUserService
{
    Task<bool> UpdateUserFromAllUsers(EditMikroTikUserViewModel model);

    Task<bool> UpdateMikroTikUserProfile(EditMikroTikUserViewModel model);

    Task<bool> CheckProfileExistsInServer(int serverId, string profileName);

    Task<List<EditMikroTikUserViewModel>> GetAllUsersWithDetails(int serverId);

    Task<bool> CheckUserExists(string username, int serverId);

    Task<bool> AddPPPoEUser(Client client);

    Task<bool> UpdatePPPoEUser(Client client);

    Task<bool> UpdatePPPoEUserWithOriginalUsername(Client client, string originalUsername);

    Task<bool> DeletePPPoEUser(string username, int serverId);

    Task<Client?> GetPPPoEUserInfo(string username, int serverId);

    Task<List<Client>> GetActivePPPoEUsers(int serverId);

    Task<List<Client>> GetAllPPPoEUsers(int serverId);

    Task<bool> DisconnectActiveUser(int serverId, string username);

    Task<bool> DisablePPPoEUser(int serverId, string username);

    Task<bool> EnablePPPoEUser(int serverId, string username);

    Task<bool> FreezeAccount(int serverId, string username);

    Task<bool> UnfreezeAccount(int serverId, string username);

    Task<bool> TestConnection(int serverId);

    Task<bool> RenewPPPoESubscription(string username, int serverId, DateTime? newExpirationDate);

    Task<bool> RenewSubscriptionTo8thNextMonth(string username, int serverId);

    Task<ExpiredAccountsResult> CheckAndDisableExpiredAccounts();
}
