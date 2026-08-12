using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.SystemAdminPricing;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class SystemAdminPricingReadinessServiceTests
{
    [Fact]
    public void TotalRequiredPricingChecks_IsSixteen()
    {
        Assert.Equal(16, SystemAdminPricingReadinessService.TotalRequiredPricingChecks);
    }

    [Fact]
    public async Task EvaluateAsync_WhenNothingConfigured_ReturnsZeroConfiguredAndSixteenMissing()
    {
        var snapshot = new ServiceCatalogSnapshot();
        var provider = new FakeSnapshotProvider(snapshot);
        var service = new SystemAdminPricingReadinessService(provider);

        SystemAdminPricingReadiness result = await service.EvaluateAsync();

        Assert.False(result.IsComplete);
        Assert.Equal(16, result.TotalRequired);
        Assert.Equal(0, result.ConfiguredCount);
        Assert.Equal(16, result.MissingItems.Count);
        Assert.Equal(0, result.ProgressPercent);
    }

    [Fact]
    public async Task EvaluateAsync_WhenFullyConfigured_ReturnsComplete()
    {
        var snapshot = BuildFullyConfiguredSnapshot();
        var provider = new FakeSnapshotProvider(snapshot);
        var service = new SystemAdminPricingReadinessService(provider);

        SystemAdminPricingReadiness result = await service.EvaluateAsync();

        Assert.True(result.IsComplete);
        Assert.Equal(16, result.TotalRequired);
        Assert.Equal(16, result.ConfiguredCount);
        Assert.Empty(result.MissingItems);
        Assert.Equal(100, result.ProgressPercent);
    }

    private static ServiceCatalogSnapshot BuildFullyConfiguredSnapshot()
    {
        RecurringServiceSnapshot configured = new()
        {
            HasInitialPricing = true,
            HasRenewalPricing = true
        };

        return new ServiceCatalogSnapshot
        {
            NetworkPricing = configured,
            ServerPricing = configured,
            SectorPricing = configured,
            ReceiverPricing = configured,
            ClientPricing = configured,
            UserPricing = configured,
            SpeedProfilePricing = configured,
            ReportPricing = new ReportServiceSnapshot { HasInitialPricing = true },
            HasProfilePriceTax = true
        };
    }

    private sealed class FakeSnapshotProvider(ServiceCatalogSnapshot snapshot) : IServiceCatalogSnapshotProvider
    {
        public Task<ServiceCatalogSnapshot> BuildAsync(CancellationToken ct = default) =>
            Task.FromResult(snapshot);
    }
}
