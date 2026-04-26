using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using RadTik.Services;

namespace RadTik.Security;

/// <summary>
/// Handles <see cref="FeatureRequirement"/> by checking current network's enabled features.
/// </summary>
public sealed class FeatureAuthorizationHandler : AuthorizationHandler<FeatureRequirement>
{
    private readonly IFeatureAccessService _featureAccess;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FeatureAuthorizationHandler(IFeatureAccessService featureAccess, IHttpContextAccessor httpContextAccessor)
    {
        _featureAccess = featureAccess;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var httpContext = context.Resource switch
        {
            HttpContext hc => hc,
            AuthorizationFilterContext afc => afc.HttpContext,
            _ => _httpContextAccessor.HttpContext
        };

        if (httpContext == null)
        {
            return;
        }

        var ok = await _featureAccess.HasFeatureAsync(context.User, httpContext, requirement.FeatureKey);
        if (ok)
        {
            context.Succeed(requirement);
        }
    }
}

