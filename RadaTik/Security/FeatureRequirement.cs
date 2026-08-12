using Microsoft.AspNetCore.Authorization;

namespace RadaTik.Security;

/// <summary>
/// Authorization requirement that ensures the current company/network has an enabled feature.
/// Used via policy name: "Feature:&lt;FeatureKey&gt;".
/// </summary>
public sealed class FeatureRequirement : IAuthorizationRequirement
{
    public FeatureRequirement(string featureKey)
    {
        FeatureKey = featureKey ?? string.Empty;
    }

    public string FeatureKey { get; }
}

