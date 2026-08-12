namespace RadaTik.Data;

public sealed class CurrentNetworkScope : ICurrentNetworkScope
{
    public bool IsFilterActive { get; private set; }

    public bool BypassAllNetworks { get; private set; } = true;

    public IReadOnlyList<int> AccessibleNetworkIds { get; private set; } = Array.Empty<int>();

    public void SetScope(bool isFilterActive, bool bypassAllNetworks, IReadOnlyList<int> accessibleNetworkIds)
    {
        IsFilterActive = isFilterActive;
        BypassAllNetworks = bypassAllNetworks;
        AccessibleNetworkIds = accessibleNetworkIds ?? Array.Empty<int>();
    }

    public void Reset() => SetScope(false, true, Array.Empty<int>());
}
