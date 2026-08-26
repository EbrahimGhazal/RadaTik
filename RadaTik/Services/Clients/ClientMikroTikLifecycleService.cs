using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.Clients;

public sealed class ClientMikroTikLifecycleService(
    ApplicationDbContext context,
    IMikroTikPppoeUserService mikroTikUsers)
    : ApplicationServiceBase(context), IClientMikroTikLifecycleService
{
    private readonly IMikroTikPppoeUserService _mikroTik = mikroTikUsers;

    public async Task<ClientOperationOutcome> ToggleActiveAsync(int clientId, int networkId, CancellationToken ct = default)
    {
        Client? client = await Db.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId && c.NetworkId == networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        client.IsActive = !client.IsActive;
        client.LastUpdated = DateTime.Now;
        Db.Update(client);
        await Db.SaveChangesAsync(ct);

        string status = client.IsActive ? "مفعل" : "معطل";
        return ClientOperationOutcome.Success($"تم {status} العميل بنجاح");
    }

    public async Task<ClientOperationOutcome> FreezeAsync(int clientId, int networkId, CancellationToken ct = default)
    {
        Client? client = await LoadClientAsync(clientId, networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        if (!client.MikroTikServerId.HasValue || string.IsNullOrEmpty(client.UserName))
        {
            return ClientOperationOutcome.Fail("لا يمكن تجميد الحساب: لم يتم تحديد خادم المايكروتك أو اسم المستخدم");
        }

        try
        {
            await _mikroTik.FreezeAccount(client.MikroTikServerId.Value, client.UserName);
            return ClientOperationOutcome.Success("تم تجميد الإنترنت للمشترك على المايكروتك فقط");
        }
        catch (Exception ex)
        {
            return ClientOperationOutcome.Fail(MikroTikErrorFormatter.Format("خطأ في تجميد الحساب", ex.Message));
        }
    }

    public async Task<ClientOperationOutcome> UnfreezeAsync(int clientId, int networkId, CancellationToken ct = default)
    {
        Client? client = await LoadClientAsync(clientId, networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        if (!client.MikroTikServerId.HasValue || string.IsNullOrEmpty(client.UserName))
        {
            return ClientOperationOutcome.Fail("لا يمكن تفعيل الحساب: لم يتم تحديد خادم المايكروتك أو اسم المستخدم");
        }

        try
        {
            await _mikroTik.UnfreezeAccount(client.MikroTikServerId.Value, client.UserName);
            return ClientOperationOutcome.Success("تم تفعيل الحساب بنجاح");
        }
        catch (Exception ex)
        {
            return ClientOperationOutcome.Fail(MikroTikErrorFormatter.Format("خطأ في تفعيل الحساب", ex.Message));
        }
    }

    public async Task<ClientOperationOutcome> RenewOneMonthAsync(int clientId, int networkId, CancellationToken ct = default)
    {
        Client? client = await LoadClientAsync(clientId, networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        try
        {
            DateTime baseDate = client.AccountExpirationDate?.Date ?? DateTime.Now.Date;
            DateTime newExpirationDate = baseDate.AddMonths(1).AddDays(-1);

            if (client.MikroTikServerId.HasValue && !string.IsNullOrWhiteSpace(client.UserName))
            {
                await _mikroTik.RenewPPPoESubscription(
                    client.UserName,
                    client.MikroTikServerId.Value,
                    newExpirationDate);
            }

            client.AccountExpirationDate = newExpirationDate;
            client.LastRenewalDate = DateTime.Now.Date;
            client.LastUpdated = DateTime.Now;
            Db.Update(client);
            await Db.SaveChangesAsync(ct);

            return ClientOperationOutcome.Success(
                $"تم تجديد الاشتراك لمدة شهر حتى تاريخ {newExpirationDate:yyyy/MM/dd}");
        }
        catch (Exception ex)
        {
            return ClientOperationOutcome.Fail(MikroTikErrorFormatter.Format("خطأ في تجديد الاشتراك", ex.Message));
        }
    }

    public async Task<ClientOperationOutcome> RenewSubscriptionAsync(
        int clientId,
        int networkId,
        DateTime? expirationDate,
        int? renewDays,
        CancellationToken ct = default)
    {
        Client? client = await LoadClientAsync(clientId, networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        DateTime newExpirationDate;
        if (renewDays.HasValue && renewDays.Value > 0)
        {
            newExpirationDate = DateTime.Now.AddDays(renewDays.Value);
        }
        else if (expirationDate.HasValue)
        {
            newExpirationDate = expirationDate.Value;
        }
        else
        {
            return ClientOperationOutcome.Fail("يجب تحديد تاريخ انتهاء الصلاحية أو عدد الأيام للتجديد");
        }

        if (!client.MikroTikServerId.HasValue || string.IsNullOrEmpty(client.UserName))
        {
            return ClientOperationOutcome.Fail("لا يمكن التجديد: لم يتم تحديد خادم المايكروتك أو اسم المستخدم");
        }

        try
        {
            await _mikroTik.RenewPPPoESubscription(
                client.UserName,
                client.MikroTikServerId.Value,
                newExpirationDate);

            client.AccountExpirationDate = newExpirationDate;
            client.LastRenewalDate = DateTime.Now.Date;
            client.LastUpdated = DateTime.Now;
            Db.Update(client);
            await Db.SaveChangesAsync(ct);

            return ClientOperationOutcome.Success(
                $"تم تجديد الاشتراك بنجاح حتى تاريخ {newExpirationDate:yyyy/MM/dd}");
        }
        catch (Exception ex)
        {
            return ClientOperationOutcome.Fail(MikroTikErrorFormatter.Format("خطأ في تجديد الاشتراك", ex.Message));
        }
    }

    public async Task<ClientOperationOutcome> RenewTo8thNextMonthAsync(int clientId, int networkId, CancellationToken ct = default)
    {
        Client? client = await LoadClientAsync(clientId, networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        if (!client.MikroTikServerId.HasValue || string.IsNullOrEmpty(client.UserName))
        {
            return ClientOperationOutcome.Fail("لا يمكن التجديد: لم يتم تحديد خادم المايكروتك أو اسم المستخدم");
        }

        try
        {
            await _mikroTik.RenewSubscriptionTo8thNextMonth(client.UserName, client.MikroTikServerId.Value);

            DateTime today = DateTime.Now;
            DateTime nextMonth = today.AddMonths(1);
            DateTime renewalDate = new(nextMonth.Year, nextMonth.Month, 8);

            client.AccountExpirationDate = renewalDate;
            client.LastRenewalDate = DateTime.Now.Date;
            client.LastUpdated = DateTime.Now;
            Db.Update(client);
            await Db.SaveChangesAsync(ct);

            return ClientOperationOutcome.Success(
                $"تم تجديد الاشتراك بنجاح حتى تاريخ {renewalDate:yyyy/MM/dd} (8 من الشهر القادم)");
        }
        catch (Exception ex)
        {
            return ClientOperationOutcome.Fail(MikroTikErrorFormatter.Format("خطأ في تجديد الاشتراك", ex.Message));
        }
    }

    public async Task<ClientOperationOutcome> QuickExtendAsync(
        int clientId,
        int networkId,
        int days,
        CancellationToken ct = default)
    {
        if (days <= 0)
        {
            return ClientOperationOutcome.Fail("عدد الأيام يجب أن يكون أكبر من صفر");
        }

        Client? client = await LoadClientAsync(clientId, networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        DateTime newExpirationDate = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value > DateTime.Now
            ? client.AccountExpirationDate.Value.AddDays(days)
            : DateTime.Now.AddDays(days);

        if (!client.MikroTikServerId.HasValue || string.IsNullOrEmpty(client.UserName))
        {
            client.AccountExpirationDate = newExpirationDate;
            client.LastRenewalDate = DateTime.Now.Date;
            client.IsActive = true;
            client.LastUpdated = DateTime.Now;
            Db.Update(client);
            await Db.SaveChangesAsync(ct);
            return ClientOperationOutcome.Success(
                $"تم تمديد اشتراك {client.Name} لمدة {days} أيام حتى {newExpirationDate:yyyy/MM/dd}");
        }

        try
        {
            await _mikroTik.RenewPPPoESubscription(
                client.UserName,
                client.MikroTikServerId.Value,
                newExpirationDate);

            client.AccountExpirationDate = newExpirationDate;
            client.LastRenewalDate = DateTime.Now.Date;
            client.IsActive = true;
            client.LastUpdated = DateTime.Now;
            Db.Update(client);
            await Db.SaveChangesAsync(ct);

            return ClientOperationOutcome.Success(
                $"تم تمديد اشتراك {client.Name} لمدة {days} أيام حتى {newExpirationDate:yyyy/MM/dd}");
        }
        catch (Exception ex)
        {
            return ClientOperationOutcome.Fail(MikroTikErrorFormatter.Format("خطأ في التمديد", ex.Message));
        }
    }

    public async Task<ClientRenewSubscriptionPageModel?> BuildRenewSubscriptionPageAsync(
        int clientId,
        int networkId,
        CancellationToken ct = default)
    {
        Client? client = await Db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId && c.NetworkId == networkId, ct);
        if (client == null)
        {
            return null;
        }

        return new ClientRenewSubscriptionPageModel
        {
            ClientId = client.Id,
            ClientName = client.Name,
            CurrentExpirationDate = client.AccountExpirationDate
        };
    }

    public async Task<ClientOperationOutcome> SyncWithMikroTikAsync(int clientId, int networkId, CancellationToken ct = default)
    {
        Client? client = await LoadClientAsync(clientId, networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        if (!client.MikroTikServerId.HasValue || string.IsNullOrEmpty(client.UserName))
        {
            return ClientOperationOutcome.Fail("لا يمكن المزامنة: لم يتم تحديد خادم المايكروتك أو اسم المستخدم");
        }

        if (!string.IsNullOrWhiteSpace(client.Profile?.Name))
        {
            client.ProfileName = client.Profile.Name;
        }

        if (string.IsNullOrWhiteSpace(client.ProfileName))
        {
            return ClientOperationOutcome.Fail("لا يمكن المزامنة: لم يتم تحديد بروفايل للمشترك في قاعدة البيانات");
        }

        if (string.IsNullOrEmpty(client.Password))
        {
            return ClientOperationOutcome.Fail("لا يمكن المزامنة: كلمة المرور غير موجودة في قاعدة البيانات");
        }

        try
        {
            bool exists = await _mikroTik.CheckUserExists(client.UserName, client.MikroTikServerId.Value);
            if (exists)
            {
                await _mikroTik.UpdatePPPoEUser(client);
                return ClientOperationOutcome.Success(
                    "تم تحديث كلمة المرور والبروفايل على MikroTik من بيانات النظام.");
            }

            await _mikroTik.AddPPPoEUser(client);
            return ClientOperationOutcome.Success("تمت إضافة المشترك إلى MikroTik من بيانات النظام.");
        }
        catch (Exception ex)
        {
            return ClientOperationOutcome.Fail(MikroTikErrorFormatter.Format("خطأ في المزامنة مع المايكروتك", ex));
        }
    }

    public async Task<ClientOperationOutcome> SetAccountExpirationDateAsync(
        int clientId,
        int networkId,
        DateTime expirationDate,
        CancellationToken ct = default)
    {
        Client? client = await LoadClientAsync(clientId, networkId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        // تاريخ الانتهاء يُدار من قاعدة البيانات (إيقاف المنتهي يتم لاحقاً عبر فحص النظام).
        // تجنّب اتصال MikroTik لكل سجل لأنه كان يبطئ التحديث الجماعي بشدة.
        DateTime dateOnly = expirationDate.Date;
        client.AccountExpirationDate = dateOnly;
        client.LastUpdated = DateTime.Now;
        Db.Update(client);
        await Db.SaveChangesAsync(ct);

        return ClientOperationOutcome.Success($"تم تعيين تاريخ الانتهاء إلى {dateOnly:yyyy/MM/dd}");
    }

    public async Task<BulkExpirationUpdateResult> BulkSetAccountExpirationAsync(
        int networkId,
        IReadOnlyList<int>? clientIds,
        DateTime expirationDate,
        bool applyToAllInNetwork,
        CancellationToken ct = default)
    {
        DateTime dateOnly = expirationDate.Date;
        DateTime now = DateTime.Now;

        IQueryable<Client> query = Db.Clients.Where(c => c.NetworkId == networkId);
        int requested;

        if (applyToAllInNetwork)
        {
            requested = await query.CountAsync(ct);
        }
        else
        {
            int[] ids = (clientIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
            if (ids.Length == 0)
            {
                return BulkExpirationUpdateResult.Fail("لم يتم تحديد أي مشترك.");
            }

            requested = ids.Length;
            query = query.Where(c => ids.Contains(c.Id));
        }

        if (requested <= 0)
        {
            return BulkExpirationUpdateResult.Fail("لا يوجد مشتركين لتحديثهم.");
        }

        // تحديث جماعي واحد في SQL بدل طلب لكل مشترك + اتصال MikroTik
        int updated = await query.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(c => c.AccountExpirationDate, dateOnly)
                .SetProperty(c => c.LastUpdated, now),
            ct);

        return BulkExpirationUpdateResult.Ok(
            updated,
            requested,
            $"تم تحديث تاريخ الانتهاء إلى {dateOnly:yyyy/MM/dd} لـ {updated} مشتركاً خلال ثوانٍ.");
    }

    public async Task<BulkCopyAccountsToServerResult> BulkCopyAccountsToServerAsync(
        int networkId,
        int targetServerId,
        IReadOnlyList<int>? clientIds,
        bool applyToAllInNetwork,
        bool removeFromSource = true,
        CancellationToken ct = default)
    {
        bool serverExists = await Db.MikroTikServers.AnyAsync(
            s => s.Id == targetServerId && s.NetworkId == networkId && s.IsActive,
            ct);
        if (!serverExists)
        {
            return BulkCopyAccountsToServerResult.Fail("السيرفر المطلوب غير موجود أو لا يتبع الشبكة الحالية.");
        }

        IQueryable<Client> query = Db.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId == networkId);

        if (!applyToAllInNetwork)
        {
            int[] ids = (clientIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
            if (ids.Length == 0)
            {
                return BulkCopyAccountsToServerResult.Fail("لم يتم تحديد أي مشترك.");
            }

            query = query.Where(c => ids.Contains(c.Id));
        }

        List<Client> clients = await query.ToListAsync(ct);
        if (!removeFromSource)
        {
            clients = clients.Where(c => c.MikroTikServerId != targetServerId).ToList();
        }

        if (clients.Count == 0)
        {
            return BulkCopyAccountsToServerResult.Fail(
                removeFromSource
                    ? "لا يوجد مشتركين لنقل حساباتهم."
                    : "لا يوجد مشتركين لنسخ حساباتهم إلى البرج المحدد.");
        }

        await FillMissingProfileNamesAsync(clients, ct);

        BulkAddPppoeUsersResult copyResult = await _mikroTik.AddPPPoEUsersToServerAsync(
            targetServerId,
            clients,
            ct);

        if (!copyResult.Success)
        {
            return BulkCopyAccountsToServerResult.Fail(
                copyResult.Message ?? (removeFromSource
                    ? "فشل نقل الحسابات إلى السيرفر."
                    : "فشل نسخ الحسابات إلى السيرفر."));
        }

        HashSet<int> placedIds = copyResult.PlacedClientIds.Where(id => id > 0).ToHashSet();
        List<string> errors = copyResult.Errors.ToList();
        int removedFromOld = 0;
        int reassigned = 0;
        int cloned = 0;

        if (removeFromSource)
        {
            List<IGrouping<int, Client>> oldServerGroups = clients
                .Where(c =>
                    placedIds.Contains(c.Id)
                    && c.MikroTikServerId.HasValue
                    && c.MikroTikServerId.Value != targetServerId
                    && !string.IsNullOrWhiteSpace(c.UserName))
                .GroupBy(c => c.MikroTikServerId!.Value)
                .ToList();

            foreach (IGrouping<int, Client> group in oldServerGroups)
            {
                ct.ThrowIfCancellationRequested();
                string[] oldUserNames = group
                    .Select(c => c.UserName!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                BulkDeletePppoeUsersResult deleteResult = await _mikroTik.DeletePPPoEUsersFromServerAsync(
                    group.Key,
                    oldUserNames,
                    ct);

                if (!deleteResult.Success)
                {
                    errors.Add(deleteResult.Message);
                    continue;
                }

                removedFromOld += deleteResult.DeletedCount;
                errors.AddRange(deleteResult.Errors);
            }

            if (placedIds.Count > 0)
            {
                DateTime now = DateTime.Now;
                List<Client> toReassign = await Db.Clients
                    .Where(c => c.NetworkId == networkId && placedIds.Contains(c.Id))
                    .ToListAsync(ct);

                foreach (Client client in toReassign)
                {
                    client.MikroTikServerId = targetServerId;
                    client.LastUpdated = now;
                }

                await Db.SaveChangesAsync(ct);
                reassigned = toReassign.Count;
            }
        }
        else if (placedIds.Count > 0)
        {
            cloned = await ClonePlacedClientsToTargetAsync(
                networkId,
                targetServerId,
                clients.Where(c => placedIds.Contains(c.Id)).ToList(),
                ct);
        }

        string message = removeFromSource
            ? $"تم نقل الحسابات إلى البرج الجديد: أُضيف {copyResult.AddedCount}، موجود مسبقاً {copyResult.SkippedExistingCount}، حُذف من القديم {removedFromOld}، حُدّث في قاعدة البيانات {reassigned}، غير مكتمل {copyResult.SkippedInvalidCount}، فشل {copyResult.FailedCount}."
            : $"تم نسخ الحسابات إلى البرج الجديد دون حذف المشتركين: أُضيف {copyResult.AddedCount}، موجود مسبقاً {copyResult.SkippedExistingCount}، أُنشئ في قاعدة البيانات {cloned}، بقي الأصل على البرج القديم، غير مكتمل {copyResult.SkippedInvalidCount}، فشل {copyResult.FailedCount}.";

        return BulkCopyAccountsToServerResult.Ok(
            clients.Count,
            copyResult.AddedCount,
            copyResult.SkippedExistingCount,
            copyResult.SkippedInvalidCount,
            copyResult.FailedCount,
            reassigned,
            removedFromOld,
            message,
            errors.Take(20).ToList(),
            cloned);
    }

    private async Task<int> ClonePlacedClientsToTargetAsync(
        int networkId,
        int targetServerId,
        List<Client> placedSources,
        CancellationToken ct)
    {
        HashSet<string> existingOnTarget = (await Db.Clients
                .AsNoTracking()
                .Where(c =>
                    c.NetworkId == networkId
                    && c.MikroTikServerId == targetServerId
                    && c.UserName != null)
                .Select(c => c.UserName!)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        DateTime now = DateTime.Now;
        List<Client> clones = [];
        HashSet<int> sourceIdsToMark = [];

        foreach (Client source in placedSources)
        {
            string? userName = source.UserName?.Trim();
            if (string.IsNullOrWhiteSpace(userName) || existingOnTarget.Contains(userName))
            {
                continue;
            }

            clones.Add(CloneClientForTargetServer(source, targetServerId, now));
            existingOnTarget.Add(userName);
            sourceIdsToMark.Add(source.Id);
        }

        if (clones.Count == 0 && sourceIdsToMark.Count == 0)
        {
            return 0;
        }

        if (clones.Count > 0)
        {
            Db.Clients.AddRange(clones);
        }

        List<Client> originals = await Db.Clients
            .Where(c => c.NetworkId == networkId && sourceIdsToMark.Contains(c.Id))
            .ToListAsync(ct);
        foreach (Client original in originals)
        {
            original.IsCrossServerDuplicate = true;
            original.LastUpdated = now;
        }

        await Db.SaveChangesAsync(ct);
        return clones.Count;
    }

    private static Client CloneClientForTargetServer(Client source, int targetServerId, DateTime now) =>
        new()
        {
            Name = source.Name,
            SID = source.SID,
            UserName = source.UserName,
            Password = source.Password,
            ProfileId = source.ProfileId,
            ProfileName = source.ProfileName,
            PhoneNumber = source.PhoneNumber,
            TelegramChatId = source.TelegramChatId,
            ResidenceAddress = source.ResidenceAddress,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            IsActive = source.IsActive,
            ReceiverId = source.ReceiverId,
            Service = source.Service,
            Address = source.Address,
            PowerSource = source.PowerSource,
            Building = source.Building,
            Floor = source.Floor,
            MikroTikServerId = targetServerId,
            IsCrossServerDuplicate = true,
            IsImportedFromServer = source.IsImportedFromServer,
            NetworkId = source.NetworkId,
            ServiceStartDate = source.ServiceStartDate,
            AccountExpirationDate = source.AccountExpirationDate,
            LastRenewalDate = source.LastRenewalDate,
            AccountCurrency = source.AccountCurrency,
            Balance = 0,
            CreatedDate = now,
            LastUpdated = now
        };

    private async Task FillMissingProfileNamesAsync(List<Client> clients, CancellationToken ct)
    {
        int[] profileIds = clients
            .Where(c => string.IsNullOrWhiteSpace(c.ProfileName) && c.ProfileId > 0)
            .Select(c => c.ProfileId)
            .Distinct()
            .ToArray();
        if (profileIds.Length == 0)
        {
            return;
        }

        Dictionary<int, string> names = await Db.Profiles
            .AsNoTracking()
            .Where(p => profileIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        foreach (Client client in clients)
        {
            if (!string.IsNullOrWhiteSpace(client.ProfileName))
            {
                continue;
            }

            if (names.TryGetValue(client.ProfileId, out string? profileName))
            {
                client.ProfileName = profileName;
            }
        }
    }

    private async Task<Client?> LoadClientAsync(int clientId, int networkId, CancellationToken ct)
    {
        Client? client = await Db.Clients
            .Where(c => c.NetworkId == networkId)
            .Include(c => c.MikroTikServer)
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null)
        {
            return null;
        }

        client.Profile = await Db.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == client.ProfileId, ct);
        return client;
    }
}
