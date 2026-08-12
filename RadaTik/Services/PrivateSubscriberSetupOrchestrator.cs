using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services;

/// <summary>
/// مسار دراسة: مشترك جديد + لاقط خاص — إنشاء عميل، MikroTik، فاتورة مسودة.
/// </summary>
public sealed class PrivateSubscriberSetupOrchestrator
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMikroTikPppoeUserService _mikroTikService;
    private readonly ISubscriberInstallationInvoiceService _invoiceService;
    private readonly ILogger<PrivateSubscriberSetupOrchestrator> _logger;

    public PrivateSubscriberSetupOrchestrator(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMikroTikPppoeUserService mikroTikService,
        ISubscriberInstallationInvoiceService invoiceService,
        ILogger<PrivateSubscriberSetupOrchestrator> logger)
    {
        _context = context;
        _userManager = userManager;
        _mikroTikService = mikroTikService;
        _invoiceService = invoiceService;
        _logger = logger;
    }

    public sealed class CreateResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public int? ClientId { get; init; }
        public int? InvoiceId { get; init; }
    }

    public async Task<CreateResult> CreatePrivateSubscriberAsync(
        Client client,
        ApplicationUser actor,
        int networkId,
        CancellationToken cancellationToken = default)
    {
        string? duplicateMessage = await ValidateCompanyScopedDuplicateSubscriberAsync(client, networkId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(duplicateMessage))
        {
            return new CreateResult { Success = false, ErrorMessage = duplicateMessage };
        }

        if (!client.ReceiverId.HasValue || client.ReceiverId.Value <= 0)
        {
            return new CreateResult { Success = false, ErrorMessage = "يجب تحديد المستقبل (مكان اللاقط) في مسار اللاقط الخاص." };
        }

        bool receiverIsShared = await _context.Clients
            .AsNoTracking()
            .AnyAsync(c => c.ReceiverId == client.ReceiverId && c.NetworkId == networkId, cancellationToken);
        if (receiverIsShared)
        {
            return new CreateResult
            {
                Success = false,
                ErrorMessage = "هذا المستقبل مستخدم من مشترك آخر (لاقط مشترك). استخدم مساراً آخر لاحقاً."
            };
        }

        Profile? profile = await _context.Profiles
            .FirstOrDefaultAsync(p => p.Id == client.ProfileId && p.NetworkId == networkId, cancellationToken);
        if (profile == null)
        {
            return new CreateResult { Success = false, ErrorMessage = "البروفايل المحدد غير موجود في هذه الشبكة." };
        }

        client.ProfileName = profile.Name;
        client.NetworkId = networkId;

        bool mikroTikOk = false;
        if (client.MikroTikServerId.HasValue)
        {
            try
            {
                await _mikroTikService.AddPPPoEUser(client);
                mikroTikOk = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Private setup: MikroTik failed for {UserName}", client.UserName);
                return new CreateResult { Success = false, ErrorMessage = $"فشل إنشاء الحساب على MikroTik: {ex.Message}" };
            }
        }

        try
        {
            client.CreatedDate = DateTime.Now;
            client.LastUpdated = DateTime.Now;
            client.ConnectionStatus = client.IsActive ? "مفعل" : "معطل";
            client.AccountExpirationDate ??= DateTime.Now.AddMonths(1);
            client.ServiceStartDate ??= DateTime.Now.Date;
            client.LastRenewalDate = DateTime.Now.Date;

            _context.Clients.Add(client);
            await _context.SaveChangesAsync(cancellationToken);

            int invoiceId = await _invoiceService.CreatePrivateInitialSetupInvoiceAsync(client, actor.Id, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return new CreateResult
            {
                Success = true,
                ClientId = client.Id,
                InvoiceId = invoiceId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Private setup: DB save failed");
            if (mikroTikOk)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(client.UserName) && client.MikroTikServerId.HasValue)
                    {
                        await _mikroTikService.DeletePPPoEUser(client.UserName, client.MikroTikServerId.Value);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Private setup: MikroTik cleanup failed");
                }
            }

            return new CreateResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<string?> ValidateCompanyScopedDuplicateSubscriberAsync(
        Client client,
        int networkId,
        CancellationToken cancellationToken)
    {
        string normalizedUserName = (client.UserName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return null;
        }

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(networkId, cancellationToken);
        int[] companyNetworkIds = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
            .Select(n => n.Id)
            .ToArrayAsync(cancellationToken);

        if (companyNetworkIds.Length == 0)
        {
            companyNetworkIds = [networkId];
        }

        Client? existingClient = await _context.Clients
            .AsNoTracking()
            .Where(c => c.UserName != null && c.UserName.Trim() == normalizedUserName)
            .Where(c => c.NetworkId.HasValue && companyNetworkIds.Contains(c.NetworkId.Value))
            .OrderByDescending(c => c.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingClient == null)
        {
            return null;
        }

        int? existingServerId = existingClient.MikroTikServerId;
        int? requestedServerId = client.MikroTikServerId;
        if (existingServerId.HasValue && requestedServerId.HasValue && existingServerId.Value == requestedServerId.Value)
        {
            return "الحساب موجود مسبقاً على نفس السيرفر ولا يمكن إضافته مرة أخرى.";
        }

        string existingServerName = await ResolveServerNameAsync(existingServerId, cancellationToken);
        return $"الحساب موجود مسبقاً ضمن نفس الشركة على السيرفر: {existingServerName}. لا يمكن إضافته مرة ثانية.";
    }

    private async Task<int> ResolveCompanyNetworkIdAsync(int networkId, CancellationToken cancellationToken)
    {
        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId, cancellationToken);

        return selectedNetwork?.ParentNetworkId ?? networkId;
    }

    private async Task<string> ResolveServerNameAsync(int? serverId, CancellationToken cancellationToken)
    {
        if (!serverId.HasValue)
        {
            return "غير محدد";
        }

        string? serverName = await _context.MikroTikServers
            .AsNoTracking()
            .Where(s => s.Id == serverId.Value)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(serverName)
            ? $"المعرف {serverId.Value}"
            : serverName;
    }
}
