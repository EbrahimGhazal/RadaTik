namespace RadaTik.Services.MikroTik;

public sealed record PppActiveSessionQueryResult(
    int ServerId,
    IReadOnlyList<string> Names,
    bool Succeeded);
