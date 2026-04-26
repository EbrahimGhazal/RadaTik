using System.Security.Claims;

namespace RadTik.Services
{
    /// <summary>
    /// Checks whether a given company/network has an enabled feature (paid module).
    /// </summary>
    public interface IFeatureAccessService
    {
        Task<bool> HasFeatureAsync(ClaimsPrincipal principal, HttpContext httpContext, string featureKey);
    }
}

