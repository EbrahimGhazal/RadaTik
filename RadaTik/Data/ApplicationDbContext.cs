using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Data
{
    public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Network> Networks { get; set; }
        public DbSet<Sector> Sectors { get; set; }
        public DbSet<Receiver> Receivers { get; set; }
        public DbSet<SectorRadioMetricSample> SectorRadioMetricSamples { get; set; }
        public DbSet<SectorRadioEvent> SectorRadioEvents { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<MikroTikServer> MikroTikServers { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<CompanyProfileCatalog> CompanyProfileCatalogs { get; set; }
        public DbSet<ProfilePriceHistory> ProfilePriceHistories { get; set; }
        public DbSet<JoinRequest> JoinRequests { get; set; }
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<SpeedChangeRequest> SpeedChangeRequests { get; set; }
        public DbSet<CollectionPointAccount> CollectionPointAccounts { get; set; }
        public DbSet<CollectionPointRenewalRequest> CollectionPointRenewalRequests { get; set; }
        public DbSet<CollectionPointTopUpRequest> CollectionPointTopUpRequests { get; set; }
        public DbSet<ClientWalletTopUpRequest> ClientWalletTopUpRequests { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<ClientTopUpTransaction> ClientTopUpTransactions { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<NetworkFeature> NetworkFeatures { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SystemPricingItem> SystemPricingItems { get; set; }
        public DbSet<FeaturePricing> FeaturePricings { get; set; }
        public DbSet<NetworkServiceRequest> NetworkServiceRequests { get; set; }
        public DbSet<NetworkServiceSubscription> NetworkServiceSubscriptions { get; set; }
        public DbSet<NetworkWalletTransaction> NetworkWalletTransactions { get; set; }
        public DbSet<NetworkTopUpRequest> NetworkTopUpRequests { get; set; }
        public DbSet<ServiceUnitChargeLedger> ServiceUnitChargeLedgers { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<SystemService> SystemServices { get; set; }
        public DbSet<FeaturePublicInfo> FeaturePublicInfos { get; set; }
        public DbSet<CustomServiceItem> CustomServiceItems { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<CashBox> CashBoxes { get; set; }
        public DbSet<CashBoxWithdrawal> CashBoxWithdrawals { get; set; }
        public DbSet<CashBoxDeposit> CashBoxDeposits { get; set; }
        public DbSet<CashBoxCurrencyExchange> CashBoxCurrencyExchanges { get; set; }
        public DbSet<NetworkReportTemplate> NetworkReportTemplates { get; set; }
        public DbSet<CompanyDocumentAppearance> CompanyDocumentAppearances { get; set; }
        public DbSet<MaintenanceInvoice> MaintenanceInvoices { get; set; }
        public DbSet<NetworkMaintenancePrice> NetworkMaintenancePrices { get; set; }
        public DbSet<MikroTikServerTrafficSample> MikroTikServerTrafficSamples { get; set; }
        public DbSet<ClientTrafficTestSession> ClientTrafficTestSessions { get; set; }
        public DbSet<SubscriberInstallationInvoice> SubscriberInstallationInvoices { get; set; }
        public DbSet<SubscriberInstallationInvoiceItem> SubscriberInstallationInvoiceItems { get; set; }
        public DbSet<SubscriberInstallationMaterialPrice> SubscriberInstallationMaterialPrices { get; set; }
        public DbSet<SubscriberInstallationMaterialWarehouseLink> SubscriberInstallationMaterialWarehouseLinks { get; set; }
        public DbSet<SubscriberInstallationInvoicePayment> SubscriberInstallationInvoicePayments { get; set; }
        public DbSet<WarehouseItem> WarehouseItems { get; set; }
        public DbSet<WarehouseMovement> WarehouseMovements { get; set; }
        public DbSet<MaterialPurchaseInvoice> MaterialPurchaseInvoices { get; set; }
        public DbSet<MaterialPurchaseInvoiceLine> MaterialPurchaseInvoiceLines { get; set; }
        public DbSet<MaterialSalesInvoice> MaterialSalesInvoices { get; set; }
        public DbSet<MaterialSalesInvoiceLine> MaterialSalesInvoiceLines { get; set; }
        public DbSet<WarehouseStocktake> WarehouseStocktakes { get; set; }
        public DbSet<WarehouseStocktakeLine> WarehouseStocktakeLines { get; set; }
        public DbSet<MoneyDiaryEntry> MoneyDiaryEntries { get; set; }
        public DbSet<PayrollEmployee> PayrollEmployees { get; set; }
        public DbSet<PayrollPayment> PayrollPayments { get; set; }
        public DbSet<PayrollTransaction> PayrollTransactions { get; set; }
        public DbSet<PayrollSalaryRevision> PayrollSalaryRevisions { get; set; }
        public DbSet<PayrollWithdrawalRequest> PayrollWithdrawalRequests { get; set; }
        public DbSet<PayrollMonthAccrualRun> PayrollMonthAccrualRuns { get; set; }
        public DbSet<EmployeeWalletTopUpRequest> EmployeeWalletTopUpRequests { get; set; }
        public DbSet<EmployeeWalletTransaction> EmployeeWalletTransactions { get; set; }
        public DbSet<SystemAdminWallet> SystemAdminWallets { get; set; }
        public DbSet<WalletLedgerUnifiedEntry> WalletLedgerUnifiedEntries { get; set; }
        public DbSet<ErpCustomer> ErpCustomers { get; set; }
        public DbSet<ErpSupplier> ErpSuppliers { get; set; }
        public DbSet<CompanyEmployeeTask> CompanyEmployeeTasks { get; set; }
        public DbSet<EmployeeRewardPenalty> EmployeeRewardPenalties { get; set; }
        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
        public DbSet<NetworkClientRenewalReminderSettings> NetworkClientRenewalReminderSettings { get; set; }
        public DbSet<ClientRenewalReminderSendLog> ClientRenewalReminderSendLogs { get; set; }
        public DbSet<SubscriberFaultDiagnosisRun> SubscriberFaultDiagnosisRuns { get; set; }
        public DbSet<PublicSiteCounter> PublicSiteCounters { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
            ConfigureNetworkTenantQueryFilters(modelBuilder);
        }
    }
}
