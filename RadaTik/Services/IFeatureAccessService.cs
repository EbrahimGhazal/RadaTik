using System.Security.Claims;

namespace RadaTik.Services
{
    /// <summary>
    /// Checks whether a given company/network has an enabled feature (paid module).
    /// </summary>
    /// <summary>
    /// تحقق اشتراك الشركة. مدير النظام يمرّ دائماً (معاينة/إدارة جميع الوحدات).
    /// </summary>
    public interface IFeatureAccessService
    {
        Task<bool> HasFeatureAsync(ClaimsPrincipal principal, HttpContext httpContext, string featureKey);
    }
}

