namespace RadaTik.Services.Clients;

public interface IClientProvisioningService
{
    ClientValidationResult ValidateForCreate(Models.Client client);

    Task<ClientCreateOutcome> CreateClientAsync(ClientCreateRequest request, CancellationToken ct = default);

    Task<bool?> TryCheckUserExistsOnMikroTikAsync(string username, int serverId);

    Task<ClientEditOutcome> UpdateClientAsync(ClientEditRequest request, CancellationToken ct = default);

    Task<ClientOperationOutcome> DeleteClientAsync(int clientId, int networkId, CancellationToken ct = default);
}
