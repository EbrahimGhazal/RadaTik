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
            return ClientOperationOutcome.Fail(
                MikroTikErrorFormatter.Format("حدث خطأ أثناء حذف العميل من المايكروتك", ex));
        }
    }
}
