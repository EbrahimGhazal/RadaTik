using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.Approvals;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.Clients;

public sealed partial class ClientProvisioningService(
    ApplicationDbContext context,
    IMikroTikPppoeUserService mikroTikUsers,
    UserManager<ApplicationUser> userManager,
    IUsageBasedSubscriptionChargeService usageChargeService,
    IEmployeeServiceApprovalRequestService approvalRequests,
    ILogger<ClientProvisioningService> logger)
    : ApplicationServiceBase(context), IClientProvisioningService
{
    private readonly IMikroTikPppoeUserService _mikroTik = mikroTikUsers;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IUsageBasedSubscriptionChargeService _usageChargeService = usageChargeService;
    private readonly IEmployeeServiceApprovalRequestService _approvalRequests = approvalRequests;
    private readonly ILogger<ClientProvisioningService> _logger = logger;

    public async Task<bool?> TryCheckUserExistsOnMikroTikAsync(string username, int serverId)
    {
        try
        {
            return await _mikroTik.CheckUserExists(username, serverId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "خطأ أثناء التحقق من وجود المستخدم {UserName} على المايكروتك", username);
            return null;
        }
    }

    public async Task<ClientOperationOutcome> DeleteClientAsync(int clientId, int networkId, CancellationToken ct = default)
    {
        Client? client = await Db.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId && c.NetworkId == networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        try
        {
            string? mikroTikWarning = null;
            if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
            {
                try
                {
                    await _mikroTik.DeletePPPoEUser(client.UserName, client.MikroTikServerId.Value);
                }
                catch (Exception mikroTikEx) when (MikroTikErrorFormatter.IsUnreachable(mikroTikEx))
                {
                    mikroTikWarning = MikroTikErrorFormatter.Format(
                        "تم حذف المشترك من النظام، لكن تعذر حذفه من MikroTik",
                        mikroTikEx);
                    _logger.LogWarning(
                        mikroTikEx,
                        "Deleted client {ClientId}/{UserName} from database after MikroTik delete failed",
                        client.Id,
                        client.UserName);
                }
            }

            int deletedId = client.Id;
            int? clientNetworkId = client.NetworkId;
            string? userName = client.UserName;

            await RemoveClientDependentRecordsAsync(deletedId, ct);

            Db.Clients.Remove(client);
            await ClientCrossServerDuplicate.RefreshRemainingAsync(
                Db,
                clientNetworkId,
                userName,
                deletedId,
                ct);
            await Db.SaveChangesAsync(ct);
            return ClientOperationOutcome.Success(
                mikroTikWarning
                ?? "تم حذف العميل بنجاح من قاعدة البيانات والمايكروتك");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete client {ClientId}", clientId);
            return ClientOperationOutcome.Fail(
                MikroTikErrorFormatter.Format("حدث خطأ أثناء حذف العميل", ex));
        }
    }

    public async Task<BulkDeleteClientsResult> BulkDeleteSelectedAsync(
        int networkId,
        IReadOnlyList<int>? clientIds,
        CancellationToken ct = default)
    {
        int[] ids = (clientIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return BulkDeleteClientsResult.Fail("لم يتم تحديد أي مشترك.");
        }

        if (ids.Length > 300)
        {
            return BulkDeleteClientsResult.Fail("لا يمكن حذف أكثر من 300 مشترك في عملية واحدة.");
        }

        int deleted = 0;
        int failed = 0;
        int notFound = 0;
        int mikroTikWarnings = 0;
        List<string> errors = [];

        foreach (int id in ids)
        {
            ct.ThrowIfCancellationRequested();
            ClientOperationOutcome outcome = await DeleteClientAsync(id, networkId, ct);
            if (outcome.NotFound)
            {
                notFound++;
                continue;
            }

            if (!outcome.IsSuccess)
            {
                failed++;
                if (!string.IsNullOrWhiteSpace(outcome.ErrorMessage) && errors.Count < 20)
                {
                    errors.Add($"#{id}: {outcome.ErrorMessage}");
                }

                continue;
            }

            deleted++;
            if (!string.IsNullOrWhiteSpace(outcome.SuccessMessage) &&
                outcome.SuccessMessage.Contains("تعذر", StringComparison.Ordinal))
            {
                mikroTikWarnings++;
                if (errors.Count < 20)
                {
                    errors.Add($"#{id}: {outcome.SuccessMessage}");
                }
            }
        }

        string message = deleted > 0
            ? $"تم حذف {deleted} من {ids.Length} مشتركاً من قاعدة البيانات والسيرفر."
              + (mikroTikWarnings > 0 ? $" تعذر الحذف من MikroTik لـ {mikroTikWarnings}." : string.Empty)
              + (failed > 0 ? $" فشل {failed}." : string.Empty)
            : "تعذر حذف المشتركين المحددين.";

        return BulkDeleteClientsResult.Ok(
            ids.Length,
            deleted,
            failed,
            notFound,
            mikroTikWarnings,
            message,
            errors);
    }

    /// <summary>
    /// يحذف السجلات المرتبطة بـ Restrict قبل حذف المشترك (فواتير التركيب، الصيانة، الشحن...).
    /// </summary>
    private async Task RemoveClientDependentRecordsAsync(int clientId, CancellationToken ct)
    {
        List<int> installationInvoiceIds = await Db.SubscriberInstallationInvoices
            .AsNoTracking()
            .Where(i => i.ClientId == clientId)
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (installationInvoiceIds.Count > 0)
        {
            List<SubscriberInstallationInvoicePayment> payments = await Db.SubscriberInstallationInvoicePayments
                .Where(p => installationInvoiceIds.Contains(p.SubscriberInstallationInvoiceId))
                .ToListAsync(ct);
            if (payments.Count > 0)
            {
                Db.SubscriberInstallationInvoicePayments.RemoveRange(payments);
            }

            List<SubscriberInstallationInvoiceItem> items = await Db.SubscriberInstallationInvoiceItems
                .Where(i => installationInvoiceIds.Contains(i.SubscriberInstallationInvoiceId))
                .ToListAsync(ct);
            if (items.Count > 0)
            {
                Db.SubscriberInstallationInvoiceItems.RemoveRange(items);
            }

            List<SubscriberInstallationInvoice> invoices = await Db.SubscriberInstallationInvoices
                .Where(i => i.ClientId == clientId)
                .ToListAsync(ct);
            Db.SubscriberInstallationInvoices.RemoveRange(invoices);
        }

        // قبل Cascade على طلبات الصيانة: فواتير الصيانة Restrict على العميل والطلب.
        List<MaintenanceInvoice> maintenanceInvoices = await Db.MaintenanceInvoices
            .Where(m => m.ClientId == clientId)
            .ToListAsync(ct);
        if (maintenanceInvoices.Count > 0)
        {
            Db.MaintenanceInvoices.RemoveRange(maintenanceInvoices);
        }

        List<ClientWalletTopUpRequest> walletTopUps = await Db.ClientWalletTopUpRequests
            .Where(r => r.ClientId == clientId)
            .ToListAsync(ct);
        if (walletTopUps.Count > 0)
        {
            Db.ClientWalletTopUpRequests.RemoveRange(walletTopUps);
        }

        List<ClientTopUpTransaction> topUps = await Db.ClientTopUpTransactions
            .Where(t => t.ClientId == clientId)
            .ToListAsync(ct);
        if (topUps.Count > 0)
        {
            Db.ClientTopUpTransactions.RemoveRange(topUps);
        }

        List<CollectionPointRenewalRequest> renewalRequests = await Db.CollectionPointRenewalRequests
            .Where(r => r.ClientId == clientId)
            .ToListAsync(ct);
        if (renewalRequests.Count > 0)
        {
            Db.CollectionPointRenewalRequests.RemoveRange(renewalRequests);
        }
    }
}
