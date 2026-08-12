using RadaTik.Dtos.MikroTik;
using RadaTik.Models;

namespace RadaTik.Services;

public sealed class MikroTikProfilesService : IMikroTikProfilesService
{
    private readonly MikroTikService _facade;

    public MikroTikProfilesService(MikroTikService facade)
    {
        _facade = facade;
    }

    public Task<List<MikroTikProfileInfo>> GetProfilesFromMikroTik(int serverId) =>
        _facade.GetProfilesFromMikroTik(serverId);

    public Task<List<string>> GetProfileNamesFromMikroTik(int serverId) =>
        _facade.GetProfileNamesFromMikroTik(serverId);

    public Task<MikroTikProfileInfo> GetProfileFromMikroTik(int serverId, string profileIdOrName) =>
        _facade.GetProfileFromMikroTik(serverId, profileIdOrName);

    public Task<string> AddProfileToMikroTik(int serverId, Profile profile) =>
        _facade.AddProfileToMikroTik(serverId, profile);

    public Task<bool> UpdateProfileInMikroTik(int serverId, Profile profile, string? oldName = null) =>
        _facade.UpdateProfileInMikroTik(serverId, profile, oldName);

    public Task<bool> DeleteProfileFromMikroTik(int serverId, string profileName) =>
        _facade.DeleteProfileFromMikroTik(serverId, profileName);

    public Task<bool> CheckProfileExistsInMikroTik(int serverId, string profileName) =>
        _facade.CheckProfileExistsInMikroTik(serverId, profileName);

    public Task<ImportProfilesPreviewResult> BuildProfilesImportPreviewAsync(int serverId, int networkId) =>
        _facade.BuildProfilesImportPreviewAsync(serverId, networkId);

    public Task<SyncResult> SyncFromMikroTikToDatabase(int serverId, bool importAsInactive = false, int? networkId = null, decimal defaultPrice = 100) =>
        _facade.SyncFromMikroTikToDatabase(serverId, importAsInactive, networkId, defaultPrice);

    public Task<SyncResult> SyncFromDatabaseToMikroTik(int serverId, int? networkId = null) =>
        _facade.SyncFromDatabaseToMikroTik(serverId, networkId);

    public Task<SyncResult> TwoWaySync(int serverId, int? networkId = null, decimal defaultImportPrice = 100) =>
        _facade.TwoWaySync(serverId, networkId, defaultImportPrice);
}
