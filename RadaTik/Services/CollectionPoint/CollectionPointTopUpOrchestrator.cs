using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services;

namespace RadaTik.Services.CollectionPoint;

public sealed class CollectionPointTopUpOrchestrator(
    ApplicationDbContext context,
    ICollectionPaymentService collectionPaymentService,
    IRequestNotificationService requestNotificationService,
    ICurrencyHelper currencyHelper,
    ICompanyFinancialHelper companyFinancial)
    : ApplicationServiceBase(context), ICollectionPointTopUpOrchestrator
{
    private readonly ICollectionPaymentService _collectionPayment = collectionPaymentService;
    private readonly IRequestNotificationService _notifications = requestNotificationService;
    private readonly ICurrencyHelper _currency = currencyHelper;
    private readonly ICompanyFinancialHelper _companyFinancial = companyFinancial;

    public async Task<CollectionPointOperationOutcome> TopUpAsync(
        TopUpClientBalanceCommand command,
        CancellationToken ct = default)
    {
        object routeValues = new { id = command.ClientId };

        if (command.Amount < 0.01m)
        {
            return CollectionPointOperationOutcome.Fail(
                "المبلغ يجب أن يكون أكبر من صفر.",
                "ClientDetails",
                routeValues);
        }

        Client? client = await Db.Clients
            .FirstOrDefaultAsync(c => c.Id == command.ClientId && c.NetworkId == command.NetworkId, ct);
        if (client == null)
        {
            return CollectionPointOperationOutcome.NotFoundClient();
        }

        decimal? rate = command.ExchangeRate;
        if (_currency.RequiresExchangeAtCollection(client.AccountCurrency) && !rate.HasValue)
        {
            CompanyFinancialSnapshot financial = await _companyFinancial.GetSnapshotAsync(command.NetworkId, ct);
            rate = financial.DefaultUsdToSypExchangeRate;
        }

        CollectionPaymentApplyResult computed = _collectionPayment.ValidateAndCompute(
            client,
            command.Amount,
            PricingCurrency.SYP_New,
            rate,
            accountAmountOverride: null);
        if (!computed.Success)
        {
            return CollectionPointOperationOutcome.Fail(
                computed.ErrorMessage ?? "تعذر احتساب التغذية.",
                "ClientDetails",
                routeValues);
        }

        CollectionPointAccount? account = await Db.CollectionPointAccounts
            .FirstOrDefaultAsync(a => a.UserId == command.UserId, ct);
        if (account == null || account.Balance < computed.PointBalanceDelta)
        {
            return CollectionPointOperationOutcome.Fail(
                $"رصيد نقطة التحصيل غير كافٍ. المطلوب {SyrianCurrencyHelper.FormatNew(computed.PointBalanceDelta)} ل.س.ج.",
                "ClientDetails",
                routeValues);
        }

        await using IDbContextTransaction tx = await Db.Database.BeginTransactionAsync(ct);
        try
        {
            decimal prevBalance = client.Balance;
            client.Balance += computed.ClientBalanceDelta;
            client.LastUpdated = DateTime.Now;

            account.Balance -= computed.PointBalanceDelta;
            account.UpdatedAt = DateTime.Now;

            string referenceNumber = BuildReferenceNumber("TOP");
            Db.ClientTopUpTransactions.Add(new ClientTopUpTransaction
            {
                ClientId = client.Id,
                Amount = computed.AccountAmountApplied,
                PreviousBalance = prevBalance,
                NewBalance = client.Balance,
                SourceType = ClientTopUpSource.CollectionPoint,
                CreatedByUserId = command.UserId,
                CollectionPointAccountId = account.Id,
                Notes = command.Notes?.Trim()
            });

            PaymentTransaction topUpPayment = new()
            {
                ClientId = client.Id,
                NetworkId = command.NetworkId,
                PaymentDate = DateTime.Now,
                ReceivedByUserId = command.UserId,
                OperationType = "ClientTopUp",
                ReferenceNumber = referenceNumber,
                Notes = string.IsNullOrWhiteSpace(command.Notes)
                    ? "تغذية رصيد مشترك من نقطة التحصيل"
                    : command.Notes.Trim(),
                PreviousClientBalance = prevBalance,
                NewClientBalance = client.Balance,
                PreviousPointBalance = account.Balance + computed.PointBalanceDelta,
                NewPointBalance = account.Balance
            };
            _collectionPayment.FillPaymentTransaction(topUpPayment, computed, client.AccountCurrency);
            Db.PaymentTransactions.Add(topUpPayment);

            await Db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _notifications.NotifyClientTopUpSubmittedAsync(
                client.Id,
                command.NetworkId,
                computed.AccountAmountApplied,
                "نقطة التحصيل",
                command.UserDisplayName);

            string successLabel = _currency.RequiresExchangeAtCollection(client.AccountCurrency)
                ? $"{_currency.FormatAmount(computed.AccountAmountApplied, client.AccountCurrency)} (خصم {SyrianCurrencyHelper.FormatNew(computed.PaymentAmountApplied)} ل.س.ج من النقطة)"
                : $"{SyrianCurrencyHelper.FormatNew(computed.PaymentAmountApplied)} ل.س.ج";

            return CollectionPointOperationOutcome.Success(
                $"تم تغذية رصيد العميل بمبلغ {successLabel}. المرجع: {referenceNumber}. الرصيد الحالي: {_currency.FormatAmount(client.Balance, client.AccountCurrency)}",
                "ClientDetails",
                routeValues);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static string BuildReferenceNumber(string operationType) =>
        $"{operationType}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
}
