using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.Clients;

public sealed class ClientPortalSelfRenewOrchestrator(
    ApplicationDbContext context,
    IClientRenewalGuardService renewalGuardService,
    IMikroTikPppoeUserService mikroTikPppoe,
    ICollectionCommissionChargeService collectionCommissionChargeService,
    IClientVipPolicyService vipPolicy)
    : ApplicationServiceBase(context), IClientPortalSelfRenewOrchestrator
{
    public async Task<ClientPortalSelfRenewOutcome> ExecuteAsync(
        ClientPortalSelfRenewCommand command,
        CancellationToken ct = default)
    {
        Client? client = await Db.Clients
            .Include(c => c.Profile)
            .FirstOrDefaultAsync(c => c.Id == command.ClientId, ct);
        if (client == null)
        {
            return ClientPortalSelfRenewOutcome.Fail(
                ClientPortalSelfRenewStatus.NotFound,
                "لم يتم العثور على الحساب",
                renewPage: false);
        }

        (decimal basePrice, decimal vatAmount, decimal amountDue) =
            await vipPolicy.ApplyMonthlyPriceAsync(client, ct);
        decimal vatPercentage = client.Profile?.VATPercentage ?? 0m;
        if (amountDue <= 0)
        {
            return ClientPortalSelfRenewOutcome.Fail(
                ClientPortalSelfRenewStatus.InvalidPrice,
                "لا يوجد سعر محدد للباقة. يرجى التواصل مع الإدارة.");
        }

        RenewalBlockResult renewalGuard = await renewalGuardService.CheckBlockingInvoicesAsync(client.Id, ct);
        if (!renewalGuard.CanRenew)
        {
            return ClientPortalSelfRenewOutcome.Fail(
                ClientPortalSelfRenewStatus.RenewalBlocked,
                $"لا يمكنك تجديد الاشتراك حالياً قبل تسديد جميع فواتير الصيانة المستحقة (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {CurrencyHelper.FormatAmount(renewalGuard.TotalOutstanding, client.AccountCurrency)}).",
                maintenance: true);
        }

        if (client.Balance < amountDue)
        {
            return ClientPortalSelfRenewOutcome.Fail(
                ClientPortalSelfRenewStatus.InsufficientBalance,
                $"رصيد المحفظة غير كافٍ. المطلوب: {CurrencyHelper.FormatAmount(amountDue, client.AccountCurrency)} (السعر: {CurrencyHelper.FormatAmount(basePrice, client.AccountCurrency)} + ضريبة {vatPercentage:N2}%: {CurrencyHelper.FormatAmount(vatAmount, client.AccountCurrency)})، ورصيدك: {CurrencyHelper.FormatAmount(client.Balance, client.AccountCurrency)}");
        }

        if (!client.NetworkId.HasValue)
        {
            return ClientPortalSelfRenewOutcome.Fail(
                ClientPortalSelfRenewStatus.MissingNetwork,
                "لم يتم ربط حسابك بشبكة. يرجى التواصل مع الإدارة لتسوية المحفظة.");
        }

        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            return ClientPortalSelfRenewOutcome.Fail(
                ClientPortalSelfRenewStatus.MissingActor,
                "يرجى تسجيل الدخول.");
        }

        await using IDbContextTransaction tx = await Db.Database.BeginTransactionAsync(ct);
        try
        {
            decimal previousClientBalance = client.Balance;
            client.Balance -= amountDue;
            DateTime baseDate = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value > DateTime.Now
                ? client.AccountExpirationDate.Value
                : DateTime.Now;
            client.AccountExpirationDate = baseDate.AddMonths(1);
            client.LastRenewalDate = DateTime.Now.Date;
            client.LastUpdated = DateTime.Now;

            bool wasStopped = !client.IsActive;

            if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
            {
                await mikroTikPppoe.RenewPPPoESubscription(
                    client.UserName,
                    client.MikroTikServerId.Value,
                    client.AccountExpirationDate.Value);
            }

            if (wasStopped)
            {
                client.IsActive = true;
                client.ConnectionStatus = "مفعل";
            }

            string referenceNumber = $"SELFREN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
            PaymentTransaction payment = new()
            {
                ClientId = client.Id,
                NetworkId = client.NetworkId,
                PaymentDate = DateTime.Now,
                ReceivedByUserId = command.ActorUserId,
                OperationType = "SelfRenewal",
                ReferenceNumber = referenceNumber,
                Notes = "تجديد اشتراك من محفظة المشترك",
                PreviousClientBalance = previousClientBalance,
                NewClientBalance = client.Balance,
                PreviousPointBalance = 0m,
                NewPointBalance = 0m
            };
            PaymentTransactionHelper.ApplyAccountWalletDebit(payment, amountDue, client.AccountCurrency);
            Db.PaymentTransactions.Add(payment);
            await Db.SaveChangesAsync(ct);

            CollectionCommissionChargeResult walletResult =
                await collectionCommissionChargeService.ChargeAfterPaymentRecordedAsync(
                    payment.Id,
                    payment.CollectionAmountSyp,
                    ct);
            if (!walletResult.Success)
            {
                await tx.RollbackAsync(ct);
                return ClientPortalSelfRenewOutcome.Fail(
                    ClientPortalSelfRenewStatus.CommissionFailed,
                    walletResult.ErrorMessage ?? "تعذر تحديث محفظة الشركة بعد التجديد. لم يُخصم المبلغ.");
            }

            await tx.CommitAsync(ct);

            string amt = CurrencyHelper.FormatAmount(amountDue, client.AccountCurrency);
            string msg = wasStopped
                ? $"تم تجديد اشتراكك وإعادة تفعيل حسابك بنجاح. تم خصم {amt} من محفظتك. الاشتراك حتى {client.AccountExpirationDate:yyyy/MM/dd}"
                : $"تم تجديد اشتراكك بنجاح. تم خصم {amt} من محفظتك. الاشتراك حتى {client.AccountExpirationDate:yyyy/MM/dd}";
            return ClientPortalSelfRenewOutcome.Success(msg);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return ClientPortalSelfRenewOutcome.Fail(
                ClientPortalSelfRenewStatus.Error,
                MikroTikErrorFormatter.Format("حدث خطأ أثناء التجديد", ex.Message));
        }
    }
}
