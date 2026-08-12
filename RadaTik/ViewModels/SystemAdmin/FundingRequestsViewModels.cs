using RadaTik.Models;

namespace RadaTik.ViewModels.SystemAdmin;

public enum FundingRequestsTab
{
    Companies = 1,
    CollectionPoints = 2
}

public sealed class FundingRequestsIndexViewModel
{
    public FundingRequestsTab ActiveTab { get; set; } = FundingRequestsTab.Companies;

    public NetworkTopUpRequestStatus? CompanyStatus { get; set; }
    public CollectionPointTopUpStatus? CollectionPointStatus { get; set; }

    public int PendingCompaniesCount { get; set; }
    public int PendingCollectionPointsCount { get; set; }

    public List<NetworkTopUpRequest> CompanyItems { get; set; } = [];
    public List<CollectionPointTopUpRequest> CollectionPointItems { get; set; } = [];
}

