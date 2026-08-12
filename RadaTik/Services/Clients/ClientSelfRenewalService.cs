using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.Clients;

public sealed class ClientSelfRenewalService(
    ApplicationDbContext context,
    IClientRenewalGuardService renewalGuardService,
    IMikroTikPppoeUserService mikroTikPppoe)
    : ApplicationServiceBase(context), IClientSelfRenewalService
{
    public async Task<ClientOperationOutcome> RenewFromWalletAsync(int clientId, CancellationToken ct = default)
    {
        Client? client = await Db.Clients
            .Include(c => c.Profile)
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client == null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        decimal basePrice = client.Profile?.Price ?? 0m;
        decimal vatPercentage = client.Profile?.VATPercentage ?? 0m;
        decimal vatAmount = basePrice * (vatPercentage / 100m);
        decimal amountDue = basePrice + vatAmount;
        if (amountDue <= 0)
        {
            return ClientOperationOutcome.Fail("لا يوجد سعر محدد للباقة. يرجى التواصل مع الإدارة.");
        }

        RenewalBlockResult renewalGuard = await renewalGuardService.CheckBlockingInvoicesAsync(client.Id, ct);
        if (!renewalGuard.CanRenew)
        {
            return ClientOperationOutcome.Fail(
                $"لا يمكن تنفيذ التجديد حالياً قبل تسديد جميع فواتير الصيانة المستحقة (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).");
        }

        if (client.Balance < amountDue)
        {
            return ClientOperationOutcome.Fail(
                $"رصيد المحفظة غير كافٍ. المطلوب: {amountDue:N0} ل.س (السعر الأساسي: {basePrice:N0} + الضريبة {vatPercentage:N2}%: {vatAmount:N0})، ورصيدك: {client.Balance:N0} ل.س");
        }

        try
        {
            client.Balance -= amountDue;
            DateTime baseDate = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value > DateTime.Now
                ? client.AccountExpirationDate.Value
                : DateTime.Now;
            client.AccountExpirationDate = baseDate.AddMonths(1);
            client.LastRenewalDate = DateTime.Now.Date;
            client.LastUpdated = DateTime.Now;

            if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
            {
                await mikroTikPppoe.RenewPPPoESubscription(
                    client.UserName,
                    client.MikroTikServerId.Value,
                    client.AccountExpirationDate.Value);
            }

            await Db.SaveChangesAsync(ct);

            return ClientOperationOutcome.Success(
                $"تم تجديد اشتراكك بنجاح. تم خصم {amountDue:N0} ل.س من محفظتك (السعر الأساسي: {basePrice:N0} + الضريبة {vatPercentage:N2}%: {vatAmount:N0}). الاشتراك حتى {client.AccountExpirationDate:yyyy/MM/dd}");
        }
        catch (Exception ex)
        {
            return ClientOperationOutcome.Fail(MikroTikErrorFormatter.Format("خطأ في التجديد", ex.Message));
        }
    }
}
