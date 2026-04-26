using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace RadTik.Security;

/// <summary>
/// Dynamic policy provider for feature-based policies.
/// Usage: [Authorize(Policy = "Feature:&lt;FeatureKey&gt;")]
/// Example: [Authorize(Policy = "Feature:MikroTikServers")]
/// </summary>
public sealed class FeaturePolicyProvider : DefaultAuthorizationPolicyProvider
{
    public const string PolicyPrefix = "Feature:";

    public FeaturePolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!string.IsNullOrWhiteSpace(policyName) &&
            policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var featureKey = policyName.Substring(PolicyPrefix.Length).Trim();

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new FeatureRequirement(featureKey))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return base.GetPolicyAsync(policyName);
    }
}

