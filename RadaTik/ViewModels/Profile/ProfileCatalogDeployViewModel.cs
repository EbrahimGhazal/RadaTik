namespace RadaTik.ViewModels.Profile;

public sealed class ProfileCatalogDeployViewModel
{
    public int CatalogId { get; init; }
    public string CatalogName { get; init; } = string.Empty;
    public List<ServerDeployOption> Servers { get; set; } = new();
}

public sealed class CompanyCatalogSummaryItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int DeployedCount { get; init; }
}

public sealed class ServerDeployOption
{
    public int ServerId { get; init; }
    public string ServerName { get; init; } = string.Empty;
    public string? Host { get; init; }
    public bool AlreadyDeployed { get; init; }
    public bool IsSelected { get; set; }
}
