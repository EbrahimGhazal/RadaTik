using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.NewSubscriberWizard;

public sealed class NewSubscriberWizardOrchestrator
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMikroTikPppoeUserService _mikroTikService;
    private readonly ISubscriberInstallationInvoiceService _invoiceService;
    private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
    private readonly ILogger<NewSubscriberWizardOrchestrator> _logger;

    public NewSubscriberWizardOrchestrator(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMikroTikPppoeUserService mikroTikService,
        ISubscriberInstallationInvoiceService invoiceService,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ILogger<NewSubscriberWizardOrchestrator> logger)
    {
        _context = context;
        _userManager = userManager;
        _mikroTikService = mikroTikService;
        _invoiceService = invoiceService;
        _usageChargeService = usageChargeService;
        _logger = logger;
    }

    public sealed class CreateSubscriberResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public int? ClientId { get; init; }
        public int? InvoiceId { get; init; }
        public bool RequiresManagerApproval { get; init; }
    }

    public async Task<CreateSubscriberResult> CreateSubscriberAsync(
        Client client,
        ApplicationUser actor,
        int networkId,
        NewSubscriberWizardPath path,
        string? dbUserName,
        string? dbPassword,
        CancellationToken cancellationToken = default)
    {
        Profile? profile = await _context.Profiles
            .FirstOrDefaultAsync(p => p.Id == client.ProfileId && p.NetworkId == networkId, cancellationToken);
        if (profile == null)
        {
            return new CreateSubscriberResult { Success = false, ErrorMessage = "البروفايل المحدد غير موجود في هذه الشبكة." };
        }

        if (path != NewSubscriberWizardPath.TowerDirect && !client.ReceiverId.HasValue)
        {
            return new CreateSubscriberResult { Success = false, ErrorMessage = "يجب تحديد اللاقط لهذا المسار." };
        }

        if (path == NewSubscriberWizardPath.SharedSelectReceiver && client.ReceiverId.HasValue)
        {
            bool hasPeers = await _context.Clients
                .AsNoTracking()
                .AnyAsync(c => c.ReceiverId == client.ReceiverId && c.NetworkId == networkId, cancellationToken);
            if (!hasPeers)
            {
                return new CreateSubscriberResult
                {
                    Success = false,
                    ErrorMessage = "اللاقط المحدد ليس مشتركاً بعد. استخدم مسار اللاقط الخاص أو اختر لاقطاً آخر."
                };
            }
        }

        if (path is NewSubscriberWizardPath.PrivateNewReceiver or NewSubscriberWizardPath.ExistingReceiverFromList
            && client.ReceiverId.HasValue)
        {
            bool isShared = await _context.Clients
                .AsNoTracking()
                .AnyAsync(c => c.ReceiverId == client.ReceiverId && c.NetworkId == networkId, cancellationToken);
            if (isShared && path == NewSubscriberWizardPath.PrivateNewReceiver)
            {
                return new CreateSubscriberResult
                {
                    Success = false,
                    ErrorMessage = "هذا اللاقط مستخدم من مشترك آخر. استخدم مسار اللاقط المشترك."
                };
            }
        }

        IList<string> roles = await _userManager.GetRolesAsync(actor);
        bool isEmployee = (roles.Contains(RoleNames.CompanyEmployee) || roles.Contains(RoleNames.EmployeeLegacy))
                          && !roles.Contains(RoleNames.NetworkAdministrator);

        client.ProfileName = profile.Name;
        client.NetworkId = networkId;
        client.CreatedDate = DateTime.Now;
        client.LastUpdated = DateTime.Now;

        if (isEmployee)
        {
            return await CreatePendingSubscriberAsync(client, actor, networkId, path, dbUserName, dbPassword, cancellationToken);
        }

        return await CreateActiveSubscriberAsync(client, actor, networkId, path, dbUserName, dbPassword, cancellationToken);
    }

    private async Task<CreateSubscriberResult> CreatePendingSubscriberAsync(
        Client client,
        ApplicationUser actor,
        int networkId,
        NewSubscriberWizardPath path,
        string? dbUserName,
        string? dbPassword,
        CancellationToken cancellationToken)
    {
        string? duplicateMessage = await ValidateCompanyScopedDuplicateSubscriberAsync(client, networkId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(duplicateMessage))
        {
            return new CreateSubscriberResult { Success = false, ErrorMessage = duplicateMessage };
        }

        ApplicationUser? existing = await _userManager.FindByNameAsync(client.UserName!);
        if (existing != null)
        {
            return new CreateSubscriberResult { Success = false, ErrorMessage = "اسم المستخدم موجود مسبقاً." };
        }

        client.IsActive = false;
        client.ConnectionStatus = "معلق بانتظار موافقة مدير الشركة";
        client.AccountExpirationDate ??= DateTime.Now.AddMonths(1);
        client.ServiceStartDate ??= DateTime.Now.Date;
        client.LastRenewalDate = DateTime.Now.Date;

        _context.Clients.Add(client);
        await _context.SaveChangesAsync(cancellationToken);

        ClientApprovalPayload payload = BuildApprovalPayload(client, dbUserName, dbPassword);
        string? requestNotes = EmployeeApprovalRequestHelper.BuildClientCreate(client.Id, payload);
        if (string.IsNullOrWhiteSpace(requestNotes))
        {
            _context.Clients.Remove(client);
            await _context.SaveChangesAsync(cancellationToken);
            return new CreateSubscriberResult { Success = false, ErrorMessage = "تعذر إنشاء طلب الموافقة." };
        }

        decimal expectedCharge = await ResolveExpectedClientCreateChargeAsync(networkId);
        await CreateEmployeeApprovalRequestAsync(networkId, actor.Id, requestNotes, expectedCharge, cancellationToken);

        int invoiceId = await _invoiceService.CreateDraftInitialSetupInvoiceAsync(client, path, actor.Id, cancellationToken);

        return new CreateSubscriberResult
        {
            Success = true,
            ClientId = client.Id,
            InvoiceId = invoiceId,
            RequiresManagerApproval = true
        };
    }

    private async Task<CreateSubscriberResult> CreateActiveSubscriberAsync(
        Client client,
        ApplicationUser actor,
        int networkId,
        NewSubscriberWizardPath path,
        string? dbUserName,
        string? dbPassword,
        CancellationToken cancellationToken)
    {
        string? duplicateMessage = await ValidateCompanyScopedDuplicateSubscriberAsync(client, networkId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(duplicateMessage))
        {
            return new CreateSubscriberResult { Success = false, ErrorMessage = duplicateMessage };
        }

        ApplicationUser? existing = await _userManager.FindByNameAsync(client.UserName!);
        if (existing != null)
        {
            return new CreateSubscriberResult { Success = false, ErrorMessage = "اسم المستخدم موجود مسبقاً." };
        }

        if (client.ReceiverId.HasValue && client.ReceiverId.Value <= 0)
        {
            client.ReceiverId = null;
        }

        if (path == NewSubscriberWizardPath.TowerDirect)
        {
            client.ReceiverId = null;
        }

        bool mikroTikSuccess = false;

        try
        {
            Network? selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId, cancellationToken);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId;

            UsageImportChargeEstimate chargeEstimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSubscriber,
                1);
            if (chargeEstimate.RequiredAmountSyp > 0m && chargeEstimate.WalletBalance < chargeEstimate.RequiredAmountSyp)
            {
                return new CreateSubscriberResult
                {
                    Success = false,
                    ErrorMessage =
                        $"رصيد محفظة الشركة غير كافٍ. المطلوب {chargeEstimate.RequiredAmountSyp:N2} ل.س.ج والرصيد {chargeEstimate.WalletBalance:N2} ل.س.ج."
                };
            }

            if (client.MikroTikServerId.HasValue)
            {
                await _mikroTikService.AddPPPoEUser(client);
                mikroTikSuccess = true;
            }

            await using (IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
            {
                client.ConnectionStatus = client.IsActive ? "مفعل" : "معطل";
                client.AccountExpirationDate ??= DateTime.Now.AddMonths(1);
                client.ServiceStartDate ??= DateTime.Now.Date;
                client.LastRenewalDate = DateTime.Now.Date;

                _context.Clients.Add(client);
                await _context.SaveChangesAsync(cancellationToken);

                int invoiceId = await _invoiceService.CreateDraftInitialSetupInvoiceAsync(client, path, actor.Id, cancellationToken);

                string? normalizedDbUserName = string.IsNullOrWhiteSpace(dbUserName) ? client.UserName : dbUserName.Trim();
                string? normalizedDbPassword = string.IsNullOrWhiteSpace(dbPassword) ? client.Password : dbPassword.Trim();
                string userEmail = !string.IsNullOrWhiteSpace(normalizedDbUserName) && normalizedDbUserName.Contains("@")
                    ? normalizedDbUserName
                    : $"{normalizedDbUserName}@radatik.local";

                ApplicationUser newUser = new ApplicationUser
                {
                    UserName = normalizedDbUserName,
                    Email = userEmail,
                    FullName = client.Name,
                    PhoneNumber = client.PhoneNumber,
                    CreatedDate = DateTime.Now,
                    IsActive = client.IsActive,
                    ClientId = client.Id,
                    NetworkId = networkId,
                    MustChangePassword = true
                };

                IdentityResult createResult = await _userManager.CreateAsync(newUser, normalizedDbPassword!);
                if (!createResult.Succeeded)
                {
                    throw new Exception(string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }

                await _userManager.AddToRoleAsync(newUser, "Client");
                await transaction.CommitAsync(cancellationToken);

                await _usageChargeService.ChargeUsageIncreaseAsync(companyNetworkId, actor.Id, PricingChargeUnit.PerSubscriber);

                return new CreateSubscriberResult
                {
                    Success = true,
                    ClientId = client.Id,
                    InvoiceId = invoiceId
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wizard subscriber create failed for {UserName}", client.UserName);

            if (mikroTikSuccess && !string.IsNullOrWhiteSpace(client.UserName) && client.MikroTikServerId.HasValue)
            {
                try
                {
                    await _mikroTikService.DeletePPPoEUser(client.UserName, client.MikroTikServerId.Value);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Wizard MikroTik cleanup failed");
                }
            }

            return new CreateSubscriberResult { Success = false, ErrorMessage = ex.Message };
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

    private static ClientApprovalPayload BuildApprovalPayload(Client client, string? dbUserName, string? dbPassword) =>
        new ClientApprovalPayload
        {
            Name = client.Name,
            SID = client.SID,
            UserName = client.UserName,
            Password = client.Password,
            ProfileId = client.ProfileId,
            ProfileName = client.ProfileName,
            PhoneNumber = client.PhoneNumber,
            ResidenceAddress = client.ResidenceAddress,
            Latitude = client.Latitude,
            Longitude = client.Longitude,
            PowerSource = client.PowerSource,
            Building = client.Building,
            Floor = client.Floor,
            ReceiverId = client.ReceiverId,
            MikroTikServerId = client.MikroTikServerId,
            ServiceStartDate = client.ServiceStartDate,
            AccountExpirationDate = client.AccountExpirationDate,
            DbUserName = dbUserName,
            DbPassword = dbPassword
        };

    private async Task<decimal> ResolveExpectedClientCreateChargeAsync(int networkId)
    {
        Network? network = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId);
        int companyNetworkId = network?.ParentNetworkId ?? networkId;
        UsageImportChargeEstimate estimate = await _usageChargeService.EstimateImportChargeAsync(
            companyNetworkId,
            PricingChargeUnit.PerSubscriber,
            1);
        return estimate.RequiredAmountSyp;
    }

    private async Task CreateEmployeeApprovalRequestAsync(
        int networkId,
        string employeeUserId,
        string requestNotes,
        decimal expectedCharge,
        CancellationToken cancellationToken)
    {
        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId, cancellationToken);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId;

        NetworkServiceRequest request = new NetworkServiceRequest
        {
            NetworkId = companyNetworkId,
            FeatureKey = FeatureKeys.Clients,
            BillingPeriod = PricingBillingPeriod.OneTime,
            AmountSYP = Math.Max(0m, WalletMath.CeilSyp(expectedCharge)),
            AmountUSD = 0m,
            Currency = PricingCurrency.SYP_New,
            Status = NetworkServiceRequestStatus.Pending,
            RequestedByUserId = employeeUserId,
            RequestedAt = DateTime.Now,
            Notes = requestNotes
        };
        _context.NetworkServiceRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
