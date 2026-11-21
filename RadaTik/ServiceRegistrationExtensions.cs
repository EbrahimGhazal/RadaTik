using Microsoft.AspNetCore.Authorization;
using RadaTik.Data;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.MikroTikSync;
using RadaTik.Services.MaintenancePricing;
using RadaTik.Services.PricingPolicies;
using RadaTik.Services.CollectionPoint;
using RadaTik.Services.NewSubscriberWizard;
using RadaTik.Services.PricingPreview;
using RadaTik.Services.SectorRadio;
using RadaTik.Services.SystemAdminPricing;
using RadaTik.Helpers;
using RadaTik.Services.Traffic;
using RadaTik.Services.PublicStats;
using RadaTik.Services.Calibration;
using RadaTik.Services.Warehouse;

namespace RadaTik;

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
        services.AddScoped<ICreatePricingPreviewService, CreatePricingPreviewService>();
        services.AddScoped<IPricingPreviewUnitsCounterStrategy, NetworksUnitsCounterStrategy>();
        services.AddScoped<IPricingPreviewUnitsCounterStrategy, ClientsUnitsCounterStrategy>();
        services.AddScoped<IPricingPreviewUnitsCounterStrategy, ReceiversUnitsCounterStrategy>();
        services.AddScoped<IPricingPreviewUnitsCounterStrategy, SectorsUnitsCounterStrategy>();
        services.AddScoped<IPricingPreviewUnitsCounterStrategy, ProfilesUnitsCounterStrategy>();
        services.AddScoped<IPricingPreviewUnitsCounterStrategy, MikroTikServersUnitsCounterStrategy>();
        services.AddScoped<IPricingPreviewUnitsCounterStrategy, EmployeesUnitsCounterStrategy>();
        services.AddScoped<ICollectionCommissionChargeService, CollectionCommissionChargeService>();
        services.AddScoped<IMaintenancePricingService, MaintenancePricingService>();
        services.AddScoped<IMaintenancePricingScopeStrategy, MainMaintenancePricingScopeStrategy>();
        services.AddScoped<IMaintenancePricingScopeStrategy, CurrentMaintenancePricingScopeStrategy>();
        services.AddScoped<IMaintenanceBillingService, MaintenanceBillingService>();
        services.AddScoped<ISubscriberInstallationInvoiceService, SubscriberInstallationInvoiceService>();
        services.AddScoped<ICompanyBusinessSummaryService, CompanyBusinessSummaryService>();
        services.AddScoped<Services.Dashboard.IDashboardLiveConnectionInsightsService, Services.Dashboard.DashboardLiveConnectionInsightsService>();
        services.AddScoped<IErpSummaryService, ErpSummaryService>();
        services.AddScoped<IErpReportService, ErpReportService>();
        services.AddScoped<IErpNotificationService, ErpNotificationService>();
        services.AddScoped<IErpDossierService, ErpDossierService>();
        services.AddScoped<ISubscriberActivityService, SubscriberActivityService>();
        services.AddScoped<IMaintenanceEmployeeTaskService, MaintenanceEmployeeTaskService>();
        services.AddScoped<EmployeeRewardPenaltyService>();
        services.AddScoped<AccountingPeriodCloseService>();
        services.AddScoped<CompanyAccountingService>();
        services.AddScoped<ZakatPeriodPostingService>();
        services.AddScoped<Services.CompanySettings.CompanySettingsService>();
        services.AddScoped<FinancialPostingService>();
        services.AddScoped<FinancialStatementService>();
        services.AddScoped<IErpCustomerLinkService, ErpCustomerLinkService>();
        services.AddScoped<IWarehouseMaterialInvoiceService, WarehouseMaterialInvoiceService>();
        services.AddScoped<MaterialInvoiceWalletService>();
        services.AddScoped<MaterialInvoiceAccountingService>();
        services.AddScoped<CompanyWalletCashTransferService>();
        services.AddScoped<CashBoxExchangeService>();
        services.AddScoped<FinancialReconciliationService>();
        services.AddScoped<ICollectionPaymentService, CollectionPaymentService>();
        services.AddScoped<IOnboardingChecklistService, OnboardingChecklistService>();
        services.AddScoped<IWarehouseStockService, WarehouseStockService>();
        services.AddScoped<IWarehouseItemImageService, WarehouseItemImageService>();
        services.AddScoped<WarehouseItemCardLoader>();
        services.AddScoped<ICollectionPointReceivePaymentService, CollectionPointReceivePaymentService>();
        services.AddScoped<ICollectionPointRenewalOrchestrator, CollectionPointRenewalOrchestrator>();
        services.AddScoped<ICollectionPointTopUpOrchestrator, CollectionPointTopUpOrchestrator>();
        services.AddScoped<ISystemAdminPricingReadinessService, SystemAdminPricingReadinessService>();
        services.AddScoped<PrivateSubscriberSetupOrchestrator>();
        services.AddScoped<CompanyProfileCatalogService>();
        services.AddScoped<ICompanyProfileCatalogService>(sp => sp.GetRequiredService<CompanyProfileCatalogService>());
        services.AddScoped<Services.Profiles.IProfileListQueryService, Services.Profiles.ProfileListQueryService>();
        services.AddScoped<Services.Profiles.IProfileImportPreviewService, Services.Profiles.ProfileImportPreviewService>();
        services.AddScoped<Services.Profiles.IProfileCompanyWalletService, Services.Profiles.ProfileCompanyWalletService>();
        services.AddScoped<Services.Profiles.IProfileBulkPricingService, Services.Profiles.ProfileBulkPricingService>();
        services.AddScoped<Services.Profiles.IProfileFormViewDataService, Services.Profiles.ProfileFormViewDataService>();
        services.AddScoped<Services.Profiles.IProfileMikroTikSyncOrchestrator, Services.Profiles.ProfileMikroTikSyncOrchestrator>();
        services.AddScoped<Services.Clients.IClientApplicationServices, Services.Clients.ClientApplicationServices>();
        services.AddScoped<NewSubscriberWizardOrchestrator>();
        services.AddScoped<SubscriberInstallationWarehouseLinkService>();
        services.AddScoped<CompanyPayrollService>();
        services.AddScoped<PayrollWithdrawalRequestService>();
        services.AddScoped<PayrollMonthEndAccrualService>();
        services.AddScoped<EmployeeWalletTopUpCommissionService>();
        services.AddScoped<EmployeeWalletFundingService>();
        services.AddScoped<EmployeeWalletTopUpService>();
        services.AddScoped<ClientOperationsHubService>();
        services.AddScoped<CompanyHrIntegrationService>();
        services.AddScoped<ICompanyMoneyDiaryService, CompanyMoneyDiaryService>();
        services.AddScoped<ICompanyFinancialHelper, CompanyFinancialService>();
        services.AddScoped<Services.Clients.IClientMikroTikLifecycleService, Services.Clients.ClientMikroTikLifecycleService>();
        services.AddScoped<Services.Clients.ISpeedChangeRequestExecutionService, Services.Clients.SpeedChangeRequestExecutionService>();
        services.AddScoped<Services.Clients.ITemporarySpeedScheduleService, Services.Clients.TemporarySpeedScheduleService>();
        services.AddScoped<Services.Clients.IRecurringSpeedScheduleService, Services.Clients.RecurringSpeedScheduleService>();
        services.AddScoped<Services.Clients.IClientProvisioningService, Services.Clients.ClientProvisioningService>();
        services.AddSingleton<Services.Clients.IClientLiveConnectionStore, Services.Clients.ClientLiveConnectionStore>();
        services.AddScoped<Services.Clients.ClientLiveConnectionRefreshService>();
        services.AddScoped<Services.Clients.IClientLiveConnectionRefreshService>(sp =>
            sp.GetRequiredService<Services.Clients.ClientLiveConnectionRefreshService>());
        services.AddScoped<Services.Clients.IClientListQueryService, Services.Clients.ClientListQueryService>();
        services.AddScoped<Services.Clients.IClientPendingApprovalQueryService, Services.Clients.ClientPendingApprovalQueryService>();
        services.AddScoped<Services.Clients.IClientFormViewDataService, Services.Clients.ClientFormViewDataService>();
        services.AddScoped<Services.Clients.IClientContractService, Services.Clients.ClientContractService>();
        services.AddScoped<Services.Clients.IClientImportOrchestrator, Services.Clients.ClientImportOrchestrator>();
        services.AddScoped<Services.Clients.IClientWalletTopUpService, Services.Clients.ClientWalletTopUpService>();
        services.AddScoped<Services.Clients.IClientSelfRenewalService, Services.Clients.ClientSelfRenewalService>();
        services.AddScoped<Services.Clients.IClientExpirationQueryService, Services.Clients.ClientExpirationQueryService>();
        services.AddScoped<Services.Clients.IClientFormLookupService, Services.Clients.ClientFormLookupService>();
        services.AddScoped<Services.Clients.IClientInfoFileImportService, Services.Clients.ClientInfoFileImportService>();
        services.AddScoped<Services.Clients.IClientVipPolicyService, Services.Clients.ClientVipPolicyService>();
        services.AddScoped<Services.Clients.ISubscriberFaultDiagnosisService, Services.Clients.SubscriberFaultDiagnosisService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceAssistantService, Services.CustomerService.CustomerServiceAssistantService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceAiEmployeeService, Services.CustomerService.CustomerServiceAiEmployeeService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceAiTeamService, Services.CustomerService.CustomerServiceAiTeamService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceTransparencyService, Services.CustomerService.CustomerServiceTransparencyService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceOpsService, Services.CustomerService.CustomerServiceOpsService>();
        services.AddScoped<Services.CustomerService.ISocialPostComposerService, Services.CustomerService.SocialPostComposerService>();
        services.AddScoped<Services.CustomerService.ISocialPostCopyWriter, Services.CustomerService.SocialPostCopyWriter>();
        services.AddScoped<Services.CustomerService.ICustomerServiceSocialPostService, Services.CustomerService.CustomerServiceSocialPostService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceAiTechnicalReportService, Services.CustomerService.CustomerServiceAiTechnicalReportService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceAiStudyReportService, Services.CustomerService.CustomerServiceAiStudyReportService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceMikroTikScriptService, Services.CustomerService.CustomerServiceMikroTikScriptService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceAiWorkforceService, Services.CustomerService.CustomerServiceAiWorkforceService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceAiReportService, Services.CustomerService.CustomerServiceAiReportService>();
        services.AddScoped<Services.CustomerService.ICustomerServiceAiDeskService, Services.CustomerService.CustomerServiceAiDeskService>();
        services.AddScoped<RenewalReminderOutboundService>();
        services.AddOptions<Services.CustomerService.CustomerServiceAiOptions>()
            .BindConfiguration(Services.CustomerService.CustomerServiceAiOptions.SectionName);
        services.AddHttpClient("CustomerServiceAi", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(90);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RadaTik-CustomerServiceAi/1.0");
        });
        services.AddScoped<Services.Clients.IClientNationalIdImageService, Services.Clients.ClientNationalIdImageService>();
        services.AddScoped<Services.Sectors.ISectorExcelImportService, Services.Sectors.SectorExcelImportService>();
        services.AddScoped<Services.Receivers.IReceiverExcelImportService, Services.Receivers.ReceiverExcelImportService>();
        services.AddScoped<Services.Documents.ICompanyDocumentAppearanceService, Services.Documents.CompanyDocumentAppearanceService>();
        services.AddScoped<Services.Company.ICompanyClientPresenceService, Services.Company.CompanyClientPresenceService>();
        services.AddScoped<Services.Clients.IClientPortalSelfRenewOrchestrator, Services.Clients.ClientPortalSelfRenewOrchestrator>();
        services.AddScoped<Services.Profiles.IProfileImportPricingService, Services.Profiles.ProfileImportPricingService>();
        services.AddScoped<Services.Approvals.IEmployeeServiceApprovalRequestService, Services.Approvals.EmployeeServiceApprovalRequestService>();
        services.AddScoped<IClientRenewalGuardService, ClientRenewalGuardService>();
        services.AddScoped<NetworkSubscriptionRenewalProcessor>();
        services.AddScoped<IWalletTopUpSubscriptionResumeService, WalletTopUpSubscriptionResumeService>();
        services.AddScoped<ICompanyWalletOnboardingFundingService, CompanyWalletOnboardingFundingService>();
        return services;
    }

    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services)
    {
        // Elevation: Open-Meteo (primary) + OpenTopoData (fallback). Do not use api.open-elevation.com
        // — its public TLS certificate has repeatedly expired/failed and is outside our control.
        services.AddHttpClient("OpenMeteoElevation", client =>
        {
            client.BaseAddress = new Uri("https://api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RadatikElevation/1.0");
        });
        services.AddHttpClient("OpenTopoData", client =>
        {
            client.BaseAddress = new Uri("https://api.opentopodata.org/");
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RadatikElevation/1.0");
        });
        services.AddHttpClient("Overpass", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(90);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RadatikLOS/1.0");
        });
        services.AddScoped<ILineOfSightAnalysisService, LineOfSightAnalysisService>();
        services.AddSingleton<ICalibrationSessionStore, CalibrationSessionStore>();
        services.AddScoped<ICalibrationScenarioService, CalibrationScenarioService>();
        services.AddMikroTikServices();
        services.AddScoped<IRequestNotificationService, RequestNotificationService>();
        services.AddScoped<Services.Auth.ILoginIdentityResolver, Services.Auth.LoginIdentityResolver>();
        services.AddSingleton<Services.Auth.IPasswordResetGuard, Services.Auth.PasswordResetGuard>();
        services.AddScoped<Services.Auth.IClientPortalCredentialAligner, Services.Auth.ClientPortalCredentialAligner>();
        services.AddScoped<ClientWalletTopUpApprovalService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddSingleton<ICurrencyHelper, CurrencyHelperAdapter>();
        services.AddSingleton<IBillingPeriodDateCalculator, BillingPeriodDateCalculatorAdapter>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentNetworkScope, CurrentNetworkScope>();
        services.AddScoped<INetworkScopeResolver, NetworkScopeResolver>();
        services.AddScoped<IPublicStatsService, PublicStatsService>();
        services.AddScoped<IPublicLandingStatsService, PublicLandingStatsService>();
        services.AddScoped<IFeatureAccessService, FeatureAccessService>();
        services.AddScoped<RadaTik.Navigation.ICompanySidebarNavBuilder, RadaTik.Navigation.CompanySidebarNavBuilder>();
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

    public static IServiceCollection RegisterHostedServices(this IServiceCollection services, bool includeHostedServices = true)
    {
        if (!includeHostedServices)
        {
            return services;
        }

        services.AddHostedService<ExpiredAccountsBackgroundService>();
        services.AddHostedService<Services.Clients.TemporarySpeedRevertWorker>();
        services.AddHostedService<Services.Clients.RecurringSpeedWorker>();
        services.AddHostedService<MikroTikSyncBackgroundService>();
        services.AddHostedService<NetworkSubscriptionsBackgroundService>();
        services.AddHostedService<NetworkSubscriptionBillingBackgroundService>();
        services.AddHostedService<SubscriptionExpiryNotificationsBackgroundService>();
        services.AddHostedService<PayrollMonthEndBackgroundService>();
        services.AddHostedService<SectorRadioMetricsWorkerService>();
        services.AddHostedService<SectorRadioMetricsSchedulerService>();
        services.AddHostedService<Services.Clients.ClientLiveConnectionBackgroundService>();
        services.AddHostedService<ClientRenewalRemindersBackgroundService>();
        services.AddHostedService<Services.CustomerService.CustomerServiceProactiveBackgroundService>();
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
        services.AddSingleton<ITrafficInterfaceCatalogCache, TrafficInterfaceCatalogCache>();
        services.AddSingleton<ITrafficMonitoringCoordinator, TrafficMonitoringCoordinator>();
        services.AddSingleton<ITrafficServerCircuitBreaker, TrafficServerCircuitBreaker>();
        services.AddOptions<TrafficMonitoringOptions>()
            .BindConfiguration(TrafficMonitoringOptions.SectionName);
        services.AddOptions<TrafficAlertOptions>()
            .BindConfiguration(TrafficAlertOptions.SectionName)
            .Validate(
                o => o.AlertCheckIntervalMinutes is >= 1 and <= 30
                     && o.AlertSustainMinutes is >= 2 and <= 60,
                "Traffic alert timing options are out of range.")
            .ValidateOnStart();
        services.AddSingleton<TrafficAlertThresholdPolicy>();
        services.AddScoped<ITrafficAlertSettingsService, TrafficAlertSettingsService>();
        services.AddScoped<ITrafficInterfaceCatalogStore, TrafficInterfaceCatalogStore>();
        services.AddScoped<MikroTikTrafficSnapshotReader>();
        services.AddScoped<TrafficHistoryAggregateService>();
        services.AddHostedService<TrafficBroadcastWorker>();
        services.AddHostedService<TrafficStatisticsSamplerWorker>();
        services.AddHostedService<TrafficRetentionPurgeWorker>();
        services.AddHostedService<TrafficThresholdAlertWorker>();
        services.AddHostedService<CalibrationRadioWorker>();
        return services;
    }
}
