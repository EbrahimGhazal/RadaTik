using Microsoft.EntityFrameworkCore;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Data.Infrastructure;

/// <summary>
/// فلاتر استعلام متطابقة للكيانات التابعة التي لا تحمل NetworkId مباشرةً
/// (تزيل تحذير EF 10622 عند وجود علاقة required مع كيان عليه HasQueryFilter).
/// </summary>
internal static class DependentEntityQueryFilterConfigurator
{
    public static void Apply(ApplicationDbContext db, ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MaterialPurchaseInvoiceLine>().HasQueryFilter(line =>
            db.NetworkQueryFilterDisabled
            || (line.Invoice != null && db.NetworkFilterIds.Contains(line.Invoice.CompanyNetworkId)));

        modelBuilder.Entity<MaterialSalesInvoiceLine>().HasQueryFilter(line =>
            db.NetworkQueryFilterDisabled
            || (line.Invoice != null && db.NetworkFilterIds.Contains(line.Invoice.CompanyNetworkId)));

        modelBuilder.Entity<WarehouseStocktakeLine>().HasQueryFilter(line =>
            db.NetworkQueryFilterDisabled
            || (line.Stocktake != null && db.NetworkFilterIds.Contains(line.Stocktake.CompanyNetworkId)));

        modelBuilder.Entity<ClientTrafficTestSession>().HasQueryFilter(session =>
            db.NetworkQueryFilterDisabled
            || session.Client == null
            || session.Client.NetworkId == null
            || db.NetworkFilterIds.Contains(session.Client.NetworkId.Value));

        modelBuilder.Entity<MaintenanceRequest>().HasQueryFilter(request =>
            db.NetworkQueryFilterDisabled
            || request.Client == null
            || request.Client.NetworkId == null
            || db.NetworkFilterIds.Contains(request.Client.NetworkId.Value));

        modelBuilder.Entity<SpeedChangeRequest>().HasQueryFilter(request =>
            db.NetworkQueryFilterDisabled
            || request.Client == null
            || request.Client.NetworkId == null
            || db.NetworkFilterIds.Contains(request.Client.NetworkId.Value));

        modelBuilder.Entity<ProfilePriceHistory>().HasQueryFilter(history =>
            db.NetworkQueryFilterDisabled
            || history.Profile == null
            || history.Profile.NetworkId == null
            || db.NetworkFilterIds.Contains(history.Profile.NetworkId.Value));

        modelBuilder.Entity<SectorRadioEvent>().HasQueryFilter(entry =>
            db.NetworkQueryFilterDisabled
            || entry.Sector == null
            || entry.Sector.NetworkId == null
            || db.NetworkFilterIds.Contains(entry.Sector.NetworkId.Value));

        modelBuilder.Entity<SectorRadioMetricSample>().HasQueryFilter(sample =>
            db.NetworkQueryFilterDisabled
            || sample.Sector == null
            || sample.Sector.NetworkId == null
            || db.NetworkFilterIds.Contains(sample.Sector.NetworkId.Value));

        modelBuilder.Entity<ServiceUnitChargeLedger>().HasQueryFilter(ledger =>
            db.NetworkQueryFilterDisabled
            || (ledger.Subscription != null && db.NetworkFilterIds.Contains(ledger.Subscription.NetworkId)));

        modelBuilder.Entity<SubscriberInstallationInvoiceItem>().HasQueryFilter(item =>
            db.NetworkQueryFilterDisabled
            || (item.SubscriberInstallationInvoice != null
                && db.NetworkFilterIds.Contains(item.SubscriberInstallationInvoice.NetworkId)));

        modelBuilder.Entity<SubscriberInstallationMaterialWarehouseLink>().HasQueryFilter(link =>
            db.NetworkQueryFilterDisabled
            || (link.MaterialPrice != null && db.NetworkFilterIds.Contains(link.MaterialPrice.NetworkId)));

        modelBuilder.Entity<CashBoxCurrencyExchange>().HasQueryFilter(exchange =>
            db.NetworkQueryFilterDisabled
            || exchange.CreatedByUser == null
            || exchange.CreatedByUser.NetworkId == null
            || db.NetworkFilterIds.Contains(exchange.CreatedByUser.NetworkId.Value));

        modelBuilder.Entity<CashBoxDeposit>().HasQueryFilter(deposit =>
            db.NetworkQueryFilterDisabled
            || deposit.DepositedByUser == null
            || deposit.DepositedByUser.NetworkId == null
            || db.NetworkFilterIds.Contains(deposit.DepositedByUser.NetworkId.Value));

        modelBuilder.Entity<CashBoxWithdrawal>().HasQueryFilter(withdrawal =>
            db.NetworkQueryFilterDisabled
            || withdrawal.WithdrawnByUser == null
            || withdrawal.WithdrawnByUser.NetworkId == null
            || db.NetworkFilterIds.Contains(withdrawal.WithdrawnByUser.NetworkId.Value));

        modelBuilder.Entity<PasswordResetRequest>().HasQueryFilter(request =>
            db.NetworkQueryFilterDisabled
            || request.User == null
            || request.User.NetworkId == null
            || db.NetworkFilterIds.Contains(request.User.NetworkId.Value));

        modelBuilder.Entity<SubscriberInstallationInvoicePayment>().HasQueryFilter(payment =>
            db.NetworkQueryFilterDisabled
            || payment.ReceivedByUser == null
            || payment.ReceivedByUser.NetworkId == null
            || db.NetworkFilterIds.Contains(payment.ReceivedByUser.NetworkId.Value));

        modelBuilder.Entity<UserPermission>().HasQueryFilter(permission =>
            db.NetworkQueryFilterDisabled
            || permission.User == null
            || permission.User.NetworkId == null
            || db.NetworkFilterIds.Contains(permission.User.NetworkId.Value));

        // ChartOfAccount has a tenant filter; required JournalEntryLine FK needs a matching filter (EF 10622).
        modelBuilder.Entity<JournalEntryLine>().HasQueryFilter(line =>
            db.NetworkQueryFilterDisabled
            || (line.ChartOfAccount != null
                && db.NetworkFilterIds.Contains(line.ChartOfAccount.CompanyNetworkId)));
    }
}
