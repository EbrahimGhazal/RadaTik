namespace RadaTik.ViewModels;

public sealed record ReceiverCreateSectorOption(
    int Id,
    string? Name,
    int MikroTikServerId,
    string? NetworkMask,
    string? IPAddress);

public sealed record ReceiverCreateServerOption(int Id, string? Name);
