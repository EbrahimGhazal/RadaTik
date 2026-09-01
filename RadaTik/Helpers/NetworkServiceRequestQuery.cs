using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Helpers;

public static class NetworkServiceRequestQuery
{
    public static IQueryable<NetworkServiceRequest> WhereVisibleToSystemAdmin(
        this IQueryable<NetworkServiceRequest> query)
    {
        return query.Where(r =>
            (r.Notes == null || !r.Notes.StartsWith("EMP_REQ:")) &&
            !(r.FeatureKey == FeatureKeys.Sectors && r.Notes != null && r.Notes.Contains("SECTOR_CREATE_PENDING:")));
    }
}
