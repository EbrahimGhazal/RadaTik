using RadaTik.Models;
using RadaTik.ViewModels.MikroTikServers;

namespace RadaTik.Services.MikroTik;

/// <summary>
/// واجهة موحّدة لمستخدمي MikroTik: عمليات PPPoE في <see cref="MikroTikUserService"/>
/// واستيراد المستخدمين في <see cref="MikroTikUserImportService"/>.
/// </summary>
public sealed class MikroTikUsersFacade(MikroTikUserService users, MikroTikUserImportService import) : IMikroTikUsersService
{
    private readonly MikroTikUserService _users = users;
    private readonly MikroTikUserImportService _import = import;

    public Task<ImportUsersResult> ImportAllUsersToDatabase(int serverId, int networkId) =>
        _import.ImportAllUsersToDatabase(serverId, networkId);

    public Task<ImportUsersPreviewResult> BuildUsersImportPreviewAsync(int serverId, int networkId) =>
        _import.BuildUsersImportPreviewAsync(serverId, networkId);

    public Task<bool> UpdateUserFromAllUsers(EditMikroTikUserViewModel model) =>
        _users.UpdateUserFromAllUsers(model);

    public Task<bool> UpdateMikroTikUserProfile(EditMikroTikUserViewModel model) =>
        _users.UpdateMikroTikUserProfile(model);

    public Task<bool> CheckProfileExistsInServer(int serverId, string profileName) =>
        _users.CheckProfileExistsInServer(serverId, profileName);

    public Task<List<EditMikroTikUserViewModel>> GetAllUsersWithDetails(int serverId) =>
        _users.GetAllUsersWithDetails(serverId);

    public Task<bool> CheckUserExists(string username, int serverId) =>
        _users.CheckUserExists(username, serverId);

    public Task<bool> AddPPPoEUser(Client client) => _users.AddPPPoEUser(client);

    public Task<bool> UpdatePPPoEUser(Client client) => _users.UpdatePPPoEUser(client);

    public Task<bool> UpdatePPPoEUserWithOriginalUsername(Client client, string originalUsername) =>
        _users.UpdatePPPoEUserWithOriginalUsername(client, originalUsername);

    public Task<bool> DeletePPPoEUser(string username, int serverId) =>
        _users.DeletePPPoEUser(username, serverId);

    public Task<Client?> GetPPPoEUserInfo(string username, int serverId) =>
        _users.GetPPPoEUserInfo(username, serverId);

    public Task<List<Client>> GetActivePPPoEUsers(int serverId) =>
        _users.GetActivePPPoEUsers(serverId);

    public Task<List<Client>> GetAllPPPoEUsers(int serverId) =>
        _users.GetAllPPPoEUsers(serverId);

    public Task<bool> DisconnectActiveUser(int serverId, string username) =>
        _users.DisconnectActiveUser(serverId, username);

    public Task<bool> DisablePPPoEUser(int serverId, string username) =>
        _users.DisablePPPoEUser(serverId, username);

    public Task<bool> EnablePPPoEUser(int serverId, string username) =>
        _users.EnablePPPoEUser(serverId, username);

    public Task<bool> FreezeAccount(int serverId, string username) =>
        _users.FreezeAccount(serverId, username);

    public Task<bool> UnfreezeAccount(int serverId, string username) =>
        _users.UnfreezeAccount(serverId, username);

    public Task<bool> TestConnection(int serverId) => _users.TestConnection(serverId);

    public Task<bool> RenewPPPoESubscription(string username, int serverId, DateTime? newExpirationDate) =>
        _users.RenewPPPoESubscription(username, serverId, newExpirationDate);

    public Task<bool> RenewSubscriptionTo8thNextMonth(string username, int serverId) =>
        _users.RenewSubscriptionTo8thNextMonth(username, serverId);

    public Task<ExpiredAccountsResult> CheckAndDisableExpiredAccounts() =>
        _users.CheckAndDisableExpiredAccounts();
}
