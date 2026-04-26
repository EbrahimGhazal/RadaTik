using RadTik.Dtos.MikroTik;
using RadTik.Models;

namespace RadTik.Services;

public interface IMikroTikProfilesService
{
    Task<List<MikroTikProfileInfo>> GetProfilesFromMikroTik(int serverId);
    Task<List<string>> GetProfileNamesFromMikroTik(int serverId);
    Task<MikroTikProfileInfo> GetProfileFromMikroTik(int serverId, string profileIdOrName);
    Task<string> AddProfileToMikroTik(int serverId, Profile profile);
    Task<bool> UpdateProfileInMikroTik(int serverId, Profile profile, string? oldName = null);
    Task<bool> DeleteProfileFromMikroTik(int serverId, string profileName);
    Task<bool> CheckProfileExistsInMikroTik(int serverId, string profileName);
    Task<ImportProfilesPreviewResult> BuildProfilesImportPreviewAsync(int serverId, int networkId);
    Task<SyncResult> SyncFromMikroTikToDatabase(int serverId, bool importAsInactive = false, int? networkId = null, decimal defaultPrice = 100);
    Task<SyncResult> SyncFromDatabaseToMikroTik(int serverId, int? networkId = null);
    Task<SyncResult> TwoWaySync(int serverId, int? networkId = null, decimal defaultImportPrice = 100);
}
