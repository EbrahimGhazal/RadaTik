using RadaTik.Services.Approvals;
using RadaTik.Services.MikroTik;
using RadaTik.Services.PricingPreview;

namespace RadaTik.Services.Clients;

public sealed class ClientApplicationServices(
    IMikroTikPppoeUserService mikroTikPppoe,
    IMikroTikUserImportService mikroTikImport,
    IPermissionService permission,
    IUsageBasedSubscriptionChargeService usageCharge,
    ICreatePricingPreviewService pricingPreview,
    IRequestNotificationService notifications,
    IClientRenewalGuardService renewalGuard,
    ISubscriberInstallationInvoiceService subscriberInstallationInvoices,
    ClientOperationsHubService operationsHub,
    IClientMikroTikLifecycleService lifecycle,
    IClientProvisioningService provisioning,
    IClientListQueryService listQuery,
    IClientPendingApprovalQueryService pendingApprovals,
    IEmployeeServiceApprovalRequestService employeeApprovals,
    IClientFormViewDataService formViewData,
    IClientContractService contract,
    IClientImportOrchestrator import,
    IClientWalletTopUpService walletTopUp,
    IClientSelfRenewalService selfRenewal,
    IClientExpirationQueryService expiration,
    IClientFormLookupService formLookup,
    IClientInfoFileImportService infoFileImport,
    ISubscriberFaultDiagnosisService faultDiagnosis,
    IClientNationalIdImageService nationalIdImages) : IClientApplicationServices
{
    public IMikroTikPppoeUserService MikroTikPppoe { get; } = mikroTikPppoe;
    public IMikroTikUserImportService MikroTikImport { get; } = mikroTikImport;
    public IPermissionService Permission { get; } = permission;
    public IUsageBasedSubscriptionChargeService UsageCharge { get; } = usageCharge;
    public ICreatePricingPreviewService PricingPreview { get; } = pricingPreview;
    public IRequestNotificationService Notifications { get; } = notifications;
    public IClientRenewalGuardService RenewalGuard { get; } = renewalGuard;
    public ISubscriberInstallationInvoiceService SubscriberInstallationInvoices { get; } = subscriberInstallationInvoices;
    public ClientOperationsHubService OperationsHub { get; } = operationsHub;
    public IClientMikroTikLifecycleService Lifecycle { get; } = lifecycle;
    public IClientProvisioningService Provisioning { get; } = provisioning;
    public IClientListQueryService ListQuery { get; } = listQuery;
    public IClientPendingApprovalQueryService PendingApprovals { get; } = pendingApprovals;
    public IEmployeeServiceApprovalRequestService EmployeeApprovals { get; } = employeeApprovals;
    public IClientFormViewDataService FormViewData { get; } = formViewData;
    public IClientContractService Contract { get; } = contract;
    public IClientImportOrchestrator Import { get; } = import;
    public IClientWalletTopUpService WalletTopUp { get; } = walletTopUp;
    public IClientSelfRenewalService SelfRenewal { get; } = selfRenewal;
    public IClientExpirationQueryService Expiration { get; } = expiration;
    public IClientFormLookupService FormLookup { get; } = formLookup;
    public IClientInfoFileImportService InfoFileImport { get; } = infoFileImport;
    public ISubscriberFaultDiagnosisService FaultDiagnosis { get; } = faultDiagnosis;
    public IClientNationalIdImageService NationalIdImages { get; } = nationalIdImages;
}
