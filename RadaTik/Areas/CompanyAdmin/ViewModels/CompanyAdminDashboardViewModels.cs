namespace RadaTik.Areas.CompanyAdmin.ViewModels;

public sealed record CompanyAdminDashboardRecentClient(int Id, string? Name, string? UserName, bool IsActive, DateTime CreatedDate);

public sealed record CompanyAdminDashboardProfileStat(string? Name, int ClientCount);
