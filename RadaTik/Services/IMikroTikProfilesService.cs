using RadaTik.Dtos.MikroTik;

using RadaTik.Models;

using RadaTik.Services.MikroTik;



namespace RadaTik.Services;



public interface IMikroTikProfilesService : IMikroTikProfileSyncService

{

    Task<List<MikroTikProfileInfo>> GetProfilesFromMikroTik(int serverId);

    Task<List<string>> GetProfileNamesFromMikroTik(int serverId);

    Task<MikroTikProfileInfo> GetProfileFromMikroTik(int serverId, string profileIdOrName);

    Task<string> AddProfileToMikroTik(int serverId, Profile profile);

    Task<bool> UpdateProfileInMikroTik(int serverId, Profile profile, string? oldName = null);

    Task<bool> DeleteProfileFromMikroTik(int serverId, string profileName);

    Task<bool> CheckProfileExistsInMikroTik(int serverId, string profileName);

    Task<ImportProfilesPreviewResult> BuildProfilesImportPreviewAsync(int serverId, int networkId);

}

