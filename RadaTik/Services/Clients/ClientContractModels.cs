using RadaTik.Models;

namespace RadaTik.Services.Clients;

public enum ClientContractPageStatus
{
    Success,
    NotFound,
    RenewalBlocked
}

public sealed class ClientMembershipContractPageResult
{
    public ClientContractPageStatus Status { get; init; }
    public Client? Client { get; init; }
    public ClientContractPrintViewData? PrintView { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ClientContractSettingsSaveCommand
{
    public required string ContractTitle { get; init; }
    public string? RecordNumber { get; init; }
    public string? LicenseNumber { get; init; }
    public required string ContractBodyTemplate { get; init; }
}

public sealed class ClientContractSettingsSaveResult
{
    public bool IsValid { get; init; }
    public ClientContractTemplateSettingsViewData? InvalidView { get; init; }

    public static ClientContractSettingsSaveResult Valid() => new() { IsValid = true };

    public static ClientContractSettingsSaveResult Invalid(ClientContractTemplateSettingsViewData view) =>
        new() { IsValid = false, InvalidView = view };
}

public sealed class ClientContractMeta
{
    public string? ContractTitle { get; init; }
    public string? RecordNumber { get; init; }
    public string? LicenseNumber { get; init; }
}

public sealed record ClientContractPrintViewData
{
    public required DateTime ContractDate { get; init; }
    public required string ContractTitle { get; init; }
    public required string RecordNumber { get; init; }
    public required string LicenseNumber { get; init; }
    public required string BodyHtml { get; init; }
    public RadaTik.Services.Documents.CompanyDocumentChrome? Chrome { get; init; }
}

public sealed class ClientContractTemplateSettingsViewData
{
    public required IReadOnlyDictionary<string, string> AvailableVariables { get; init; }
    public required string VariableSyntaxHint { get; init; }
    public required string PreviewHtml { get; init; }
    public required string ContractTitle { get; init; }
    public string? RecordNumber { get; init; }
    public string? LicenseNumber { get; init; }
    public required string ContractBodyTemplate { get; init; }
    public required string DefaultContractBodyTemplate { get; init; }
}
