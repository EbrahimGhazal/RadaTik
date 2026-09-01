using RadaTik.Services.Approvals;
using RadaTik.Services.MikroTik;
using RadaTik.Services.PricingPreview;

namespace RadaTik.Services.Clients;

/// <summary>واجهة موحّدة لخدمات مسار المشتركين (تكوين بدل حقن عشرات التبعيات في الـ controller).</summary>
public interface IClientApplicationServices
{
    IMikroTikPppoeUserService MikroTikPppoe { get; }
    IMikroTikUserImportService MikroTikImport { get; }
    IPermissionService Permission { get; }
    IUsageBasedSubscriptionChargeService UsageCharge { get; }
    ICreatePricingPreviewService PricingPreview { get; }
    IRequestNotificationService Notifications { get; }
    IClientRenewalGuardService RenewalGuard { get; }
    ISubscriberInstallationInvoiceService SubscriberInstallationInvoices { get; }
    ClientOperationsHubService OperationsHub { get; }
    IClientMikroTikLifecycleService Lifecycle { get; }
    IClientProvisioningService Provisioning { get; }
    IClientListQueryService ListQuery { get; }
    IClientPendingApprovalQueryService PendingApprovals { get; }
    IEmployeeServiceApprovalRequestService EmployeeApprovals { get; }
    IClientFormViewDataService FormViewData { get; }
    IClientContractService Contract { get; }
    IClientImportOrchestrator Import { get; }
    IClientWalletTopUpService WalletTopUp { get; }
    IClientSelfRenewalService SelfRenewal { get; }
    IClientExpirationQueryService Expiration { get; }
    IClientFormLookupService FormLookup { get; }
    IClientInfoFileImportService InfoFileImport { get; }
    ISubscriberFaultDiagnosisService FaultDiagnosis { get; }
    IClientNationalIdImageService NationalIdImages { get; }
}
