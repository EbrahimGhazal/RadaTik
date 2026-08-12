using RadaTik.Dtos.MikroTik;

namespace RadaTik.Services.Profiles;

public interface IProfileImportPreviewService
{
    Task<ImportProfilesPreviewResult> GetPreviewWithTimeoutAsync(
        int serverId,
        int networkId,
        int timeoutMs = 5000,
        CancellationToken ct = default);

    Task<ProfileImportPreviewJsonModel> BuildImportPreviewJsonAsync(
        int serverId,
        int networkId,
        CancellationToken ct = default);
}
