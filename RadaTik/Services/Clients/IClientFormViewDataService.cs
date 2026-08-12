using RadaTik.Models;

namespace RadaTik.Services.Clients;

public interface IClientFormViewDataService
{
    Task<ClientCreateFormViewData> BuildCreateFormDataAsync(int networkId, Client client, CancellationToken ct = default);

    Task<ClientEditFormViewData> BuildEditFormDataAsync(int networkId, Client client, CancellationToken ct = default);
}
