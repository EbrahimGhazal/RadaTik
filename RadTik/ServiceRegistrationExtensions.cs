using Microsoft.AspNetCore.Authorization;
using RadTik.Security;
using RadTik.Services;
using RadTik.Services.MikroTikSync;
using RadTik.Services.PricingPolicies;
using RadTik.Services.SectorRadio;
using RadTik.Services.SystemAdminPricing;
using RadTik.Services.Traffic;

namespace RadTik;

internal static class ServiceRegistrationExtensions
{
    public static IServiceCollection RegisterPricingServices(this IServiceCollection services)
    {
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IServicePricingPolicyCatalog, ServicePricingPolicyCatalog>();
        services.AddScoped<IPricingScenarioStrategy, FixedAmountPricingScenarioStrategy>();
        services.AddScoped<IPricingScenarioStrategy, PercentagePricingScenarioStrategy>();
        services.AddScoped<IFeaturePublicContentComposer, FeaturePublicContentComposer>();
        services.AddScoped<ICollectionCommissionPricingStrategy, PercentageCollectionCommissionPricingStrategy>();
        services.AddScoped<ICollectionCommissionPricingResolver, CollectionCommissionPricingResolver>();
        services.AddScoped<IRecurringServicePricingHandler, NetworkRecurringPricingHandler>();
        services.AddScoped<IRecurringServicePricingHandler, ServerRecurringPricingHandler>();
        services.AddScoped<IRecurringServicePricingHandler, SectorRecurringPricingHandler>();
        services.AddScoped<IRecurringServicePricingHandler, ReceiverRecurringPricingHandler>();
        services.AddScoped<IRecurringServicePricingHandler, ClientRecurringPricingHandler>();
        services.AddScoped<IRecurringServicePricingHandler, UserRecurringPricingHandler>();
        services.AddScoped<IRecurringServicePricingHandler, SpeedProfileRecurringPricingHandler>();
        services.AddScoped<IRecurringServicePricingHandlerResolver, RecurringServicePricingHandlerResolver>();
        services.AddScoped<IReportPricingHandler, ReportPricingHandler>();
        services.AddScoped<IProfileTaxPricingHandler, ProfileTaxPricingHandler>();
        services.AddScoped<IMaintenanceCommissionPricingHandler, MaintenanceCommissionPricingHandler>();
        services.AddScoped<IStandaloneServicePricingHandlerResolver, StandaloneServicePricingHandlerResolver>();
        services.AddScoped<IServiceCatalogSnapshotProvider, ServiceCatalogSnapshotProvider>();
        services.AddScoped<ISenderCreationWorkflowStrategy, ImmediateSenderCreationWorkflowStrategy>();
        services.AddScoped<ISenderCreationWorkflowStrategy, ApprovalGatedSenderCreationWorkflowStrategy>();
        services.AddScoped<ISenderPricingOrchestrator, SenderPricingOrchestrator>();
        services.AddScoped<IUsageBasedSubscriptionChargeService, UsageBasedSubscriptionChargeService>();
        services.AddScoped<ICollectionCommissionChargeService, CollectionCommissionChargeService>();
        services.AddScoped<IMaintenanceBillingService, MaintenanceBillingService>();
        services.AddScoped<IClientRenewalGuardService, ClientRenewalGuardService>();
        services.AddScoped<NetworkSubscriptionRenewalProcessor>();
        services.AddScoped<IWalletTopUpSubscriptionResumeService, WalletTopUpSubscriptionResumeService>();
        return services;
    }

    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services)
    {
        services.AddHttpClient("OpenElevation", client =>
        {
            client.BaseAddress = new Uri("https://api.open-elevation.com/");
            client.Timeout = TimeSpan.FromSeconds(90);
        });
        services.AddHttpClient("Overpass", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(90);
        });
        services.AddScoped<ILineOfSightAnalysisService, LineOfSightAnalysisService>();
        services.AddMikroTikServices();
        services.AddScoped<RequestNotificationService>();
        services.AddScoped<PermissionService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IFeatureAccessService, FeatureAccessService>();
        services.AddSingleton<IAuthorizationPolicyProvider, FeaturePolicyProvider>();
        services.AddScoped<IAuthorizationHandler, FeatureAuthorizationHandler>();
        return services;
    }

    public static IServiceCollection RegisterSectorRadioServices(this IServiceCollection services)
    {
        services.AddSingleton<ISectorRadioMetricsQueue, SectorRadioMetricsQueue>();
        services.AddScoped<ISectorRadioAdapter, MikroTikSectorRadioAdapter>();
        services.AddScoped<SectorRadioMetricsCollector>();
        return services;
    }

    public static IServiceCollection RegisterHostedServices(this IServiceCollection services)
    {
        services.AddHostedService<ExpiredAccountsBackgroundService>();
        services.AddHostedService<MikroTikSyncBackgroundService>();
        services.AddHostedService<NetworkSubscriptionsBackgroundService>();
        services.AddHostedService<NetworkSubscriptionBillingBackgroundService>();
        services.AddHostedService<SubscriptionExpiryNotificationsBackgroundService>();
        services.AddHostedService<SectorRadioMetricsWorkerService>();
        services.AddHostedService<SectorRadioMetricsSchedulerService>();
        return services;
    }

    public static IServiceCollection RegisterTrafficMonitoringServices(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddSignalR(options =>
        {
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
            options.HandshakeTimeout = TimeSpan.FromSeconds(30);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.MaximumReceiveMessageSize = 1024 * 1024;
            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors = true;
            }
        });
        services.AddSingleton<TrafficRateTracker>();
        services.AddSingleton<ITrafficMonitoringCoordinator, TrafficMonitoringCoordinator>();
        services.AddScoped<MikroTikTrafficSnapshotReader>();
        services.AddHostedService<TrafficBroadcastWorker>();
        services.AddHostedService<TrafficStatisticsSamplerWorker>();
        return services;
    }
}
