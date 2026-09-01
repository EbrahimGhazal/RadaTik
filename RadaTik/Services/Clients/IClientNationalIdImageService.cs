using Microsoft.AspNetCore.Http;
using RadaTik.Domain.Common;

namespace RadaTik.Services.Clients;

public interface IClientNationalIdImageService
{
    Task<ServiceResult<string>> SaveAsync(int clientId, IFormFile file, CancellationToken ct = default);

    void DeleteOwned(string? publicPath, int clientId);

    bool IsOwnedPath(string? publicPath, int clientId);
}
