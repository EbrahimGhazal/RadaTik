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

        try
        {
            await _mikroTik.UpdatePPPoEUser(client);
            return ClientOperationOutcome.Success("تم مزامنة البيانات مع المايكروتك بنجاح");
        }
        catch (Exception ex)
        {
            return ClientOperationOutcome.Fail(MikroTikErrorFormatter.Format("خطأ في المزامنة مع المايكروتك", ex.Message));
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

    private async Task<Client?> LoadClientAsync(int clientId, int networkId, CancellationToken ct) =>
        await Db.Clients
            .Where(c => c.NetworkId == networkId)
            .Include(c => c.MikroTikServer)
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
}
