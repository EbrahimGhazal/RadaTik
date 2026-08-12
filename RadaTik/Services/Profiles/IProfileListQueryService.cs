namespace RadaTik.Services.Profiles;

public interface IProfileListQueryService
{
    Task<ProfileIndexPageModel?> BuildIndexPageAsync(int networkId, int? serverId, CancellationToken ct = default);
}
