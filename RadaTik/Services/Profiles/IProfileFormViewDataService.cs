namespace RadaTik.Services.Profiles;

public interface IProfileFormViewDataService
{
    Task<ProfileCreateFormViewData> BuildCreateFormDataAsync(int? networkId, CancellationToken ct = default);

    Task<ProfileEditFormViewData> BuildEditFormDataAsync(int? networkId, CancellationToken ct = default);
}
