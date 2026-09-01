using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class NetworkServiceRequestQueryTests
{
    [Fact]
    public void WhereVisibleToSystemAdmin_HidesEmployeeApprovalsAndKeepsCompanyServiceRequests()
    {
        IQueryable<NetworkServiceRequest> items = new[]
        {
            new NetworkServiceRequest { Id = 1, FeatureKey = FeatureKeys.Reports, Notes = null },
            new NetworkServiceRequest { Id = 2, FeatureKey = FeatureKeys.Clients, Notes = "EMP_REQ:CLIENT_CREATE:10" },
            new NetworkServiceRequest { Id = 3, FeatureKey = FeatureKeys.Sectors, Notes = "SECTOR_CREATE_PENDING:9;Network:2" },
            new NetworkServiceRequest { Id = 4, FeatureKey = FeatureKeys.Reports, Notes = "اشتراك شهري" }
        }.AsQueryable();

        int[] visibleIds = items.WhereVisibleToSystemAdmin().Select(x => x.Id).ToArray();

        Assert.Equal(new[] { 1, 4 }, visibleIds);
    }
}
