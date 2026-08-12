using RadaTik.Models;

namespace RadaTik.Services.Clients;

public interface IClientContractService
{
    string DefaultTemplateBody { get; }

    Task<ClientContractMeta> GetMetaAsync(int networkId, CancellationToken ct = default);

    Task<string> GetTemplateBodyAsync(int networkId, CancellationToken ct = default);

    ClientContractPrintViewData BuildPrintView(Client client, ClientContractMeta meta, string templateBody, DateTime contractDate);

    ClientContractTemplateSettingsViewData BuildTemplateSettingsView(
        Network network,
        ClientContractMeta meta,
        string templateBody);

    IReadOnlyList<string> ValidateTemplateVariables(string? templateBody);

    Task SaveSettingsAsync(int networkId, ClientContractMeta meta, string templateBody, CancellationToken ct = default);

    Task ResetTemplateToDefaultAsync(int networkId, CancellationToken ct = default);

    Task<ClientMembershipContractPageResult> BuildMembershipContractPageAsync(
        int clientId,
        int? restrictToNetworkId,
        CancellationToken ct = default);

    Task<ClientContractTemplateSettingsViewData> BuildSettingsPageAsync(int networkId, CancellationToken ct = default);

    ClientContractSettingsSaveResult ValidateSettingsSave(
        Network network,
        ClientContractSettingsSaveCommand command);

    Task SaveSettingsAsync(int networkId, ClientContractSettingsSaveCommand command, CancellationToken ct = default);
}
