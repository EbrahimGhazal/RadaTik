using Microsoft.AspNetCore.Identity;
using RadTik.Data;
using RadTik.Models;
using System.Security.Claims;

namespace RadTik.Services
{
    /// <summary>
    /// Trial mode feature access:
    /// all authenticated users can pass feature entitlement checks.
    /// Role/permission checks still apply in controllers and sidebars.
    /// </summary>
    public class FeatureAccessService : IFeatureAccessService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FeatureAccessService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Task<bool> HasFeatureAsync(ClaimsPrincipal principal, HttpContext httpContext, string featureKey)
        {
            if (string.IsNullOrWhiteSpace(featureKey))
            {
                return Task.FromResult(false);
            }

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Task.FromResult(false);
            }

            var userId = _userManager.GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Task.FromResult(false);
            }

            // Per-request cache
            var cacheKey = $"Feature::{userId}::{featureKey}";
            if (httpContext.Items.TryGetValue(cacheKey, out var cachedObj) && cachedObj is bool cachedBool)
            {
                return Task.FromResult(cachedBool);
            }

            httpContext.Items[cacheKey] = true;
            return Task.FromResult(true);
        }
    }
}

