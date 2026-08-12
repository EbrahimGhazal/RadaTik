using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.ViewModels.CollectionPoint;

namespace RadaTik.Services.CollectionPoint;

public sealed class CollectionPointReceivePaymentService(
    ApplicationDbContext context,
    ICollectionPaymentService collectionPaymentService,
    ICollectionCommissionChargeService collectionCommissionChargeService,
    ICurrencyHelper currencyHelper)
    : ApplicationServiceBase(context), ICollectionPointReceivePaymentService
{
    private readonly ICollectionPaymentService _collectionPayment = collectionPaymentService;
    private readonly ICollectionCommissionChargeService _commission = collectionCommissionChargeService;
    private readonly ICurrencyHelper _currency = currencyHelper;

    public async Task<ReceivePaymentOutcome> ProcessAsync(ReceivePaymentCommand command, CancellationToken ct = default)
    {
        await using IDbContextTransaction tx = await Db.Database.BeginTransactionAsync(ct);
        try
        {
            Client? client = await Db.Clients
                .FirstOrDefaultAsync(c => c.Id == command.ClientId && c.NetworkId == command.NetworkId, ct);
            if (client == null)
            {
                return ReceivePaymentOutcome.NotFoundClient();
            }

            CollectionPointAccount? account = await Db.CollectionPointAccounts
                .FirstOrDefaultAsync(a => a.UserId == command.UserId, ct);
            if (account == null)
            {
                account = new CollectionPointAccount
                {
                    UserId = command.UserId,
                    NetworkId = command.NetworkId,
                    Balance = 0m,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                Db.CollectionPointAccounts.Add(account);
                await Db.SaveChangesAsync(ct);
            }

            CollectionPaymentApplyResult computed = _collectionPayment.ValidateAndCompute(
                client,
                command.Amount,
                PricingCurrency.SYP_New,
                command.ExchangeRate,
                accountAmountOverride: null);

            if (!computed.Success)
            {
                return ReceivePaymentOutcome.ViewError(
                    computed.ErrorMessage ?? "تعذر احتساب التحصيل.",
                    BuildViewModel(client, command, _currency));
            }

            decimal previousClientBalance = client.Balance;
            decimal previousPointBalance = account.Balance;

            client.Balance += computed.ClientBalanceDelta;
            client.LastUpdated = DateTime.Now;

            account.Balance += computed.PointBalanceDelta;
            account.UpdatedAt = DateTime.Now;

            PaymentTransaction payment = new()
            {
                ClientId = client.Id,
                NetworkId = command.NetworkId,
                PaymentDate = DateTime.Now,
                ReceivedByUserId = command.UserId,
                OperationType = "ReceivePayment",
                ReferenceNumber = BuildReferenceNumber("REC"),
                Notes = command.Notes,
                PreviousClientBalance = previousClientBalance,
                NewClientBalance = client.Balance,
                PreviousPointBalance = previousPointBalance,
                NewPointBalance = account.Balance
            };
            _collectionPayment.FillPaymentTransaction(payment, computed, client.AccountCurrency);

            Db.PaymentTransactions.Add(payment);
            Db.Update(client);
            Db.Update(account);
            await Db.SaveChangesAsync(ct);

            CollectionCommissionChargeResult commission = await _commission.ChargeAfterPaymentRecordedAsync(
                payment.Id,
                payment.CollectionAmountSyp);
            if (!commission.Success)
            {
                await tx.RollbackAsync(ct);
                return ReceivePaymentOutcome.ViewError(
                    commission.ErrorMessage ?? "تعذر إتمام عمولة التحصيل (محفظة الشركة).",
                    BuildViewModel(client, command, _currency));
            }

            await tx.CommitAsync(ct);

            string successAmount = _currency.RequiresExchangeAtCollection(client.AccountCurrency)
                ? $"+{_currency.FormatAmount(computed.AccountAmountApplied, client.AccountCurrency)} (مقابل {SyrianCurrencyHelper.FormatNew(computed.PaymentAmountApplied)} ل.س.ج)"
                : $"+{SyrianCurrencyHelper.FormatNew(computed.PaymentAmountApplied)} ل.س.ج";

            return ReceivePaymentOutcome.Success(
                $"تم تسجيل التحصيل بنجاح {successAmount} للعميل {client.Name}",
                client.UserName ?? string.Empty);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static ReceivePaymentViewModel BuildViewModel(
        Client client,
        ReceivePaymentCommand command,
        ICurrencyHelper currency) =>
        new()
        {
            ClientId = client.Id,
            ClientName = client.Name,
            ClientUserName = client.UserName,
            Amount = command.Amount,
            ExchangeRate = command.ExchangeRate,
            Notes = command.Notes,
            AccountCurrency = client.AccountCurrency,
            RequiresExchange = currency.RequiresExchangeAtCollection(client.AccountCurrency),
            CurrentClientBalance = client.Balance
        };

    private static string BuildReferenceNumber(string operationType) =>
        $"{operationType}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
}
