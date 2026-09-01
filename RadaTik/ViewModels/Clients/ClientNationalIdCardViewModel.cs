namespace RadaTik.ViewModels.Clients;

public sealed class ClientNationalIdCardViewModel
{
    public int ClientId { get; init; }

    public string? FrontPath { get; init; }

    public string? BackPath { get; init; }

    public bool CanUpload { get; init; }
}
