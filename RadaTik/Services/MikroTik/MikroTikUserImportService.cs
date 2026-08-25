using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Domain.ValueObjects;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.Clients;

namespace RadaTik.Services.MikroTik;

/// <summary>استيراد مستخدمي PPPoE من MikroTik إلى قاعدة البيانات.</summary>
public sealed class MikroTikUserImportService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    MikroTikUserService userService,
    ILogger<MikroTikUserImportService> logger)
    : IMikroTikUserImportService
{
    private readonly ApplicationDbContext _context = context;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly MikroTikUserService _userService = userService;
    private readonly ILogger<MikroTikUserImportService> _logger = logger;

    public async Task<ImportUsersResult> ImportAllUsersToDatabase(int serverId, int networkId)
    {
        _logger.LogInformation(
            "بدء استيراد مستخدمي PPPoE من الخادم {ServerId} إلى الشبكة {NetworkId}",
            serverId,
            networkId);

        ImportUsersResult result = new();

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            result.Success = false;
            result.Message = "الخادم غير موجود";
            return result;
        }

        List<Client> addedClients = [];
        Dictionary<string, Profile> profilesByName = await LoadProfilesByNameAsync(serverId, networkId);
        Dictionary<string, Client> existingOnServer = await LoadClientsOnServerByUserNameAsync(serverId);
        Dictionary<string, Client> orphansByUserName = await LoadOrphanClientsByUserNameAsync(networkId);

        try
        {
            // نفس مصدر المعاينة لضمان تطابق الأعداد
            List<Client> allUsers = await _userService.GetAllPPPoEUsers(serverId);

            foreach (Client mtUser in allUsers)
            {
                string? userName = mtUser.UserName?.Trim();
                if (string.IsNullOrWhiteSpace(userName))
                {
                    result.FailedCount++;
                    result.Errors.Add("تم تجاهل سجل بدون اسم مستخدم في المايكروتك");
                    continue;
                }

                try
                {
                    if (existingOnServer.TryGetValue(userName, out Client? existingClient))
                    {
                        if (existingClient.NetworkId != networkId)
                        {
                            existingClient.NetworkId = networkId;
                            existingClient.LastUpdated = DateTime.Now;
                        }

                        Profile? existingProfile = await ResolveOrCreateProfileAsync(
                            mtUser.ProfileName,
                            serverId,
                            networkId,
                            profilesByName,
                            result);
                        if (existingProfile == null)
                        {
                            result.FailedCount++;
                            result.Errors.Add($"المستخدم {userName}: تعذر مزامنة البروفايل ({mtUser.ProfileName})");
                            continue;
                        }

                        if (ApplyMikroTikChanges(existingClient, mtUser, existingProfile))
                        {
                            result.UpdatedCount++;
                        }
                        else
                        {
                            result.ExistingCount++;
                        }
                        continue;
                    }

                    // موجود في الشبكة بلا ربط بالسيرفر → ربطه بدل إنشاء مكرر يفشل لاحقاً
                    if (orphansByUserName.TryGetValue(userName, out Client? orphan))
                    {
                        Profile? orphanProfile = await ResolveOrCreateProfileAsync(
                            mtUser.ProfileName,
                            serverId,
                            networkId,
                            profilesByName,
                            result);

                        if (orphanProfile == null)
                        {
                            result.FailedCount++;
                            result.Errors.Add($"المستخدم {userName}: تعذر ربط/إنشاء البروفايل ({mtUser.ProfileName})");
                            continue;
                        }

                        orphan.MikroTikServerId = serverId;
                        orphan.ProfileId = orphanProfile.Id;
                        orphan.ProfileName = orphanProfile.Name;
                        orphan.Password = string.IsNullOrWhiteSpace(mtUser.Password)
                            ? orphan.Password
                            : mtUser.Password;
                        orphan.Service = string.IsNullOrEmpty(mtUser.Service) ? orphan.Service ?? "pppoe" : mtUser.Service;
                        orphan.Address = mtUser.Address ?? orphan.Address;
                        orphan.ConnectionStatus = mtUser.ConnectionStatus ?? orphan.ConnectionStatus;
                        orphan.LastUpdated = DateTime.Now;

                        existingOnServer[userName] = orphan;
                        orphansByUserName.Remove(userName);
                        result.RelinkedCount++;
                        continue;
                    }

                    Profile? profile = await ResolveOrCreateProfileAsync(
                        mtUser.ProfileName,
                        serverId,
                        networkId,
                        profilesByName,
                        result);

                    if (profile == null)
                    {
                        result.FailedCount++;
                        result.Errors.Add(
                            $"المستخدم {userName}: لم يتم العثور على بروفايل مناسب ({mtUser.ProfileName}) وتعذر إنشاؤه");
                        continue;
                    }

                    List<Client> crossServerSiblings = await _context.Clients
                        .Where(c =>
                            c.UserName == userName &&
                            c.NetworkId == networkId &&
                            c.MikroTikServerId != null &&
                            c.MikroTikServerId != serverId)
                        .ToListAsync();
                    bool isCrossServerDuplicate = crossServerSiblings.Count > 0;
                    if (isCrossServerDuplicate)
                    {
                        foreach (Client sibling in crossServerSiblings)
                        {
                            sibling.IsCrossServerDuplicate = true;
                        }

                        result.DuplicateCount++;
                    }

                    string sid = ResolveSid(null);
                    ServiceResult<PhoneNumber> phoneResult = PhoneNumber.TryCreate(mtUser.PhoneNumber);
                    string phoneNumber = phoneResult.IsSuccess ? phoneResult.Value!.Value : "0";

                    Client client = new()
                    {
                        Name = string.IsNullOrWhiteSpace(mtUser.Name) ? userName : mtUser.Name,
                        SID = sid,
                        UserName = userName,
                        Password = string.IsNullOrWhiteSpace(mtUser.Password)
                            ? MikroTikApiSupport.GenerateDefaultPassword()
                            : mtUser.Password,
                        ProfileId = profile.Id,
                        ProfileName = profile.Name,
                        PhoneNumber = phoneNumber,
                        IsActive = !string.Equals(mtUser.ConnectionStatus, "معطل", StringComparison.Ordinal),
                        ReceiverId = mtUser.ReceiverId,
                        Service = string.IsNullOrEmpty(mtUser.Service) ? "pppoe" : mtUser.Service,
                        Address = mtUser.Address,
                        ConnectionStatus = string.IsNullOrWhiteSpace(mtUser.ConnectionStatus)
                            ? "مفعل"
                            : mtUser.ConnectionStatus,
                        MikroTikServerId = serverId,
                        IsCrossServerDuplicate = isCrossServerDuplicate,
                        CreatedDate = DateTime.Now,
                        LastUpdated = DateTime.Now,
                        AccountExpirationDate = mtUser.AccountExpirationDate ?? DateTime.Now.AddMonths(1),
                        NetworkId = networkId
                    };

                    _context.Clients.Add(client);
                    await _context.SaveChangesAsync();

                    existingOnServer[userName] = client;
                    addedClients.Add(client);
                    result.AddedCount++;
                }
                catch (Exception ex)
                {
                    DetachAddedEntities();
                    result.FailedCount++;
                    result.Errors.Add($"المستخدم {userName}: {ex.Message}");
                    _logger.LogError(ex, "خطأ في استيراد المستخدم {UserName}", userName);
                }
            }

            result.RemovedStaleDuplicateCount = await ClientCrossServerDuplicate.RemoveCopiesMissingFromServerAsync(
                _context,
                networkId,
                serverId,
                allUsers.Select(user => user.UserName ?? string.Empty));

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
            }

            await CreatePlatformUsersForImportedClientsAsync(addedClients, result);

            result.Success = true;
            string userMsg = result.UsersCreatedCount > 0 || result.UsersFailedCount > 0
                ? $" تم إنشاء حسابات نظام (دور عميل) لـ {result.UsersCreatedCount} مشترك."
                : "";
            if (result.UsersFailedCount > 0)
            {
                userMsg += $" فشل إنشاء حساب نظام لـ {result.UsersFailedCount} مشترك.";
            }

            result.Message =
                $"تم استيراد {result.AddedCount} مستخدم جديد" +
                (result.UpdatedCount > 0 ? $"، تم تحديث {result.UpdatedCount} مشترك من بيانات السيرفر" : "") +
                (result.DuplicateCount > 0 ? $" (منها {result.DuplicateCount} مكرر عبر السيرفرات)" : "") +
                (result.RemovedStaleDuplicateCount > 0 ? $"، أُلغي {result.RemovedStaleDuplicateCount} تكرار بعد حذفه من البرج" : "") +
                (result.RelinkedCount > 0 ? $"، تم ربط {result.RelinkedCount} مشترك كان بلا سيرفر" : "") +
                (result.ProfilesCreatedCount > 0 ? $"، أُنشئ {result.ProfilesCreatedCount} بروفايل تلقائياً" : "") +
                $"، تم تخطي {result.ExistingCount} مستخدم موجود مسبقاً، وفشل استيراد {result.FailedCount} مستخدم.{userMsg}";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"فشل استيراد المستخدمين: {ex.Message}";
            _logger.LogError(ex, "خطأ عام في استيراد المستخدمين من الخادم {ServerId}", serverId);
        }

        return result;
    }

    public async Task<ImportUsersPreviewResult> BuildUsersImportPreviewAsync(int serverId, int networkId)
    {
        ImportUsersPreviewResult preview = new();
        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            preview.HasConnectionError = true;
            preview.PreviewNote = "الخادم غير موجود.";
            return preview;
        }

        List<Client> allUsers;
        try
        {
            allUsers = await _userService.GetAllPPPoEUsers(serverId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "تعذر بناء معاينة استيراد المستخدمين للخادم {ServerId} ({Host})",
                serverId,
                server.Host);

            preview.HasConnectionError = true;
            preview.PreviewNote = MikroTikErrorFormatter.Format(
                $"تعذر الاتصال بالسيرفر {server.Name}",
                ex);
            return preview;
        }

        preview.TotalUsersOnServer = allUsers.Count;

        Dictionary<string, Client> existingUsersByName = (await _context.Clients.AsNoTracking()
                .Where(c => c.MikroTikServerId == serverId && !string.IsNullOrEmpty(c.UserName))
                .ToListAsync())
            .GroupBy(c => c.UserName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        // مشترك في الشبكة بلا سيرفر سيُربَط عند الاستيراد (لا يُحسب قابلاً للاستيراد الجديد)
        HashSet<string> orphanUserNames = (await _context.Clients.AsNoTracking()
                .Where(c =>
                    c.NetworkId == networkId &&
                    c.MikroTikServerId == null &&
                    !string.IsNullOrEmpty(c.UserName))
                .Select(c => c.UserName!)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<string> otherServerUserNames = (await _context.Clients.AsNoTracking()
                .Where(c =>
                    c.NetworkId == networkId &&
                    c.MikroTikServerId != null &&
                    c.MikroTikServerId != serverId &&
                    !string.IsNullOrEmpty(c.UserName))
                .Select(c => c.UserName!)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<string> profileNames = (await _context.Profiles.AsNoTracking()
                .Where(p => p.MikroTikServerId == serverId && p.NetworkId == networkId && !string.IsNullOrEmpty(p.Name))
                .Select(p => p.Name)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Client user in allUsers)
        {
            string? userName = user.UserName?.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                preview.InvalidUsersCount++;
                continue;
            }

            if (existingUsersByName.TryGetValue(userName, out Client? existingClient))
            {
                preview.ExistingUsersCount++;
                if (HasMikroTikChanges(existingClient, user))
                {
                    preview.UpdatableUsersCount++;
                }
                continue;
            }

            if (orphanUserNames.Contains(userName))
            {
                preview.RelinkableUsersCount++;
                continue;
            }

            string? profileName = user.ProfileName?.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                preview.MissingProfileCount++;
                // سيُرفض عند الاستيراد إن لم يوجد اسم بروفايل
                continue;
            }

            if (!profileNames.Contains(profileName))
            {
                preview.MissingProfileCount++;
                // سيُنشأ البروفايل تلقائياً عند الاستيراد → يبقى قابلاً للاستيراد
            }

            preview.ImportableUsersCount++;
            if (otherServerUserNames.Contains(userName))
            {
                preview.DuplicateUsersCount++;
            }
        }

        if (preview.MissingProfileCount > 0 && string.IsNullOrWhiteSpace(preview.PreviewNote))
        {
            preview.PreviewNote =
                $"{preview.MissingProfileCount} مشترك بروفايلهم غير موجود في قاعدة البيانات — سيُنشأ البروفايل تلقائياً عند الاستيراد.";
        }

        return preview;
    }

    private async Task<Dictionary<string, Profile>> LoadProfilesByNameAsync(int serverId, int networkId)
    {
        List<Profile> profiles = await _context.Profiles
            .Where(p => p.MikroTikServerId == serverId && p.NetworkId == networkId && !string.IsNullOrEmpty(p.Name))
            .ToListAsync();

        Dictionary<string, Profile> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (Profile profile in profiles)
        {
            map.TryAdd(profile.Name.Trim(), profile);
        }

        return map;
    }

    private async Task<Dictionary<string, Client>> LoadClientsOnServerByUserNameAsync(int serverId)
    {
        List<Client> clients = await _context.Clients
            .Where(c => c.MikroTikServerId == serverId && !string.IsNullOrEmpty(c.UserName))
            .ToListAsync();

        Dictionary<string, Client> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (Client client in clients)
        {
            map.TryAdd(client.UserName!.Trim(), client);
        }

        return map;
    }

    /// <summary>
    /// يحدّث فقط الحقول التي يمثلها MikroTik؛ لا يلمس بيانات العميل اليدوية
    /// مثل الاسم والعنوان السكني ورقم الهاتف.
    /// </summary>
    private static bool ApplyMikroTikChanges(Client client, Client mikroTikUser, Profile profile)
    {
        bool changed = false;

        changed |= SetIfDifferent(client.ProfileId, profile.Id, value => client.ProfileId = value);
        changed |= SetIfDifferent(client.ProfileName, profile.Name, value => client.ProfileName = value);

        if (!string.IsNullOrWhiteSpace(mikroTikUser.Password))
        {
            changed |= SetIfDifferent(client.Password, mikroTikUser.Password, value => client.Password = value);
        }

        if (!string.IsNullOrWhiteSpace(mikroTikUser.Service))
        {
            changed |= SetIfDifferent(client.Service, mikroTikUser.Service, value => client.Service = value);
        }

        if (mikroTikUser.Address != null)
        {
            changed |= SetIfDifferent(client.Address, mikroTikUser.Address, value => client.Address = value);
        }

        if (!string.IsNullOrWhiteSpace(mikroTikUser.ConnectionStatus))
        {
            changed |= SetIfDifferent(
                client.ConnectionStatus,
                mikroTikUser.ConnectionStatus,
                value => client.ConnectionStatus = value);
            changed |= SetIfDifferent(
                client.IsActive,
                !string.Equals(mikroTikUser.ConnectionStatus, "معطل", StringComparison.Ordinal),
                value => client.IsActive = value);
        }

        if (mikroTikUser.ReceiverId.HasValue)
        {
            changed |= SetIfDifferent(client.ReceiverId, mikroTikUser.ReceiverId, value => client.ReceiverId = value);
        }

        if (mikroTikUser.AccountExpirationDate.HasValue)
        {
            changed |= SetIfDifferent(
                client.AccountExpirationDate,
                mikroTikUser.AccountExpirationDate,
                value => client.AccountExpirationDate = value);
        }

        if (changed)
        {
            client.LastUpdated = DateTime.Now;
        }

        return changed;
    }

    private static bool HasMikroTikChanges(Client client, Client mikroTikUser)
    {
        return (!string.IsNullOrWhiteSpace(mikroTikUser.ProfileName) &&
                !string.Equals(client.ProfileName, mikroTikUser.ProfileName, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(mikroTikUser.Password) &&
                !string.Equals(client.Password, mikroTikUser.Password, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(mikroTikUser.Service) &&
                !string.Equals(client.Service, mikroTikUser.Service, StringComparison.Ordinal))
            || (mikroTikUser.Address != null &&
                !string.Equals(client.Address, mikroTikUser.Address, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(mikroTikUser.ConnectionStatus) &&
                (!string.Equals(client.ConnectionStatus, mikroTikUser.ConnectionStatus, StringComparison.Ordinal) ||
                 client.IsActive == string.Equals(mikroTikUser.ConnectionStatus, "معطل", StringComparison.Ordinal)))
            || (mikroTikUser.ReceiverId.HasValue && client.ReceiverId != mikroTikUser.ReceiverId)
            || (mikroTikUser.AccountExpirationDate.HasValue &&
                client.AccountExpirationDate != mikroTikUser.AccountExpirationDate);
    }

    private static bool SetIfDifferent<T>(T currentValue, T newValue, Action<T> setValue)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return false;
        }

        setValue(newValue);
        return true;
    }

    private async Task<Dictionary<string, Client>> LoadOrphanClientsByUserNameAsync(int networkId)
    {
        List<Client> clients = await _context.Clients
            .Where(c =>
                c.NetworkId == networkId &&
                c.MikroTikServerId == null &&
                !string.IsNullOrEmpty(c.UserName))
            .ToListAsync();

        Dictionary<string, Client> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (Client client in clients)
        {
            map.TryAdd(client.UserName!.Trim(), client);
        }

        return map;
    }

    private async Task<Profile?> ResolveOrCreateProfileAsync(
        string? profileNameRaw,
        int serverId,
        int networkId,
        Dictionary<string, Profile> profilesByName,
        ImportUsersResult result)
    {
        string? profileName = profileNameRaw?.Trim();
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return null;
        }

        if (profilesByName.TryGetValue(profileName, out Profile? existing))
        {
            return existing;
        }

        Profile stub = new()
        {
            Name = profileName,
            Description = $"أُنشئ تلقائياً أثناء استيراد المشتركين — {DateTime.Now:yyyy-MM-dd}",
            Type = ProfileType.Internet,
            BillingCycle = BillingCycle.Monthly,
            Price = 100m,
            VATPercentage = 15,
            DownloadSpeed = 1,
            DownloadSpeedUnit = SpeedUnit.Mbps,
            UploadSpeed = 1,
            UploadSpeedUnit = SpeedUnit.Mbps,
            MikroTikServerId = serverId,
            NetworkId = networkId,
            IsSyncedWithMikroTik = false,
            IsActive = true,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now
        };

        _context.Profiles.Add(stub);
        await _context.SaveChangesAsync();
        profilesByName[profileName] = stub;
        result.ProfilesCreatedCount++;
        _logger.LogInformation(
            "أُنشئ بروفايل تلقائي {ProfileName} للخادم {ServerId} أثناء استيراد المشتركين",
            profileName,
            serverId);
        return stub;
    }

    private void DetachAddedEntities()
    {
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in _context.ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in _context.ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Modified)
                     .ToList())
        {
            entry.State = EntityState.Unchanged;
        }
    }

    private async Task CreatePlatformUsersForImportedClientsAsync(List<Client> addedClients, ImportUsersResult result)
    {
        foreach (Client client in addedClients)
        {
            try
            {
                ApplicationUser? existingUser = await _userManager.FindByNameAsync(client.UserName!);
                if (existingUser != null)
                {
                    if (existingUser.ClientId != null && existingUser.ClientId != client.Id)
                    {
                        if (client.IsCrossServerDuplicate)
                        {
                            continue;
                        }

                        result.UsersFailedCount++;
                        result.Errors.Add($"المستخدم {client.UserName}: اسم مستخدم مستخدم لحساب آخر");
                        continue;
                    }

                    if (existingUser.ClientId == null)
                    {
                        existingUser.ClientId = client.Id;
                        existingUser.NetworkId = client.NetworkId;
                        existingUser.IsActive = client.IsActive;
                        existingUser.FullName = string.IsNullOrWhiteSpace(client.Name)
                            ? existingUser.FullName
                            : client.Name;
                        if (!string.IsNullOrWhiteSpace(client.PhoneNumber))
                        {
                            existingUser.PhoneNumber = client.PhoneNumber;
                        }

                        await _userManager.UpdateAsync(existingUser);
                    }

                    if (existingUser.ClientId == client.Id)
                    {
                        if (!await _userManager.IsInRoleAsync(existingUser, "Client"))
                        {
                            await _userManager.AddToRoleAsync(existingUser, "Client");
                        }

                        if (!string.IsNullOrWhiteSpace(client.Password))
                        {
                            string token = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                            IdentityResult resetResult = await _userManager.ResetPasswordAsync(
                                existingUser,
                                token,
                                client.Password);
                            if (!resetResult.Succeeded)
                            {
                                result.UsersFailedCount++;
                                result.Errors.Add(
                                    $"المستخدم {client.UserName}: تعذر مطابقة كلمة مرور المنصة ({string.Join(", ", resetResult.Errors.Select(e => e.Description))})");
                            }
                        }

                        continue;
                    }
                }

                string userEmail = !string.IsNullOrWhiteSpace(client.UserName) && client.UserName!.Contains('@')
                    ? client.UserName
                    : $"{client.UserName}@radatik.local";

                ApplicationUser appUser = new()
                {
                    UserName = client.UserName,
                    Email = userEmail,
                    FullName = client.Name ?? client.UserName,
                    PhoneNumber = client.PhoneNumber ?? "0",
                    CreatedDate = DateTime.Now,
                    IsActive = client.IsActive,
                    ClientId = client.Id,
                    NetworkId = client.NetworkId,
                    MustChangePassword = true
                };

                IdentityResult createResult = await _userManager.CreateAsync(appUser, client.Password!);
                if (!createResult.Succeeded)
                {
                    result.UsersFailedCount++;
                    result.Errors.Add(
                        $"المستخدم {client.UserName}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                    continue;
                }

                await _userManager.AddToRoleAsync(appUser, "Client");
                result.UsersCreatedCount++;
            }
            catch (Exception ex)
            {
                result.UsersFailedCount++;
                result.Errors.Add($"المستخدم {client.UserName}: {ex.Message}");
                _logger.LogError(ex, "خطأ في إنشاء حساب نظام للمشترك المستورد {UserName}", client.UserName);
            }
        }
    }

    private static string ResolveSid(string? sidFromMikroTik)
    {
        if (string.IsNullOrWhiteSpace(sidFromMikroTik))
        {
            return SubscriberSid.GenerateNew();
        }

        ServiceResult<SubscriberSid> sid = SubscriberSid.TryCreate(sidFromMikroTik);
        return sid.IsSuccess ? sid.Value!.Value : SubscriberSid.GenerateNew();
    }
}
