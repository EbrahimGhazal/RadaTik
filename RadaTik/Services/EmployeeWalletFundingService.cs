using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class EmployeeWalletFundingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? EmployeeWalletTransactionId { get; init; }
    public decimal NewEmployeeBalance { get; init; }
    public decimal CommissionCharged { get; init; }

    public static EmployeeWalletFundingResult Ok(int txId, decimal newBalance, decimal commission) => new()
    {
        Success = true,
        EmployeeWalletTransactionId = txId,
        NewEmployeeBalance = newBalance,
        CommissionCharged = commission
    };

    public static EmployeeWalletFundingResult Fail(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}

/// <summary>تنفيذ تغذية محفظة الموظف: خصم من محفظة الشركة + عمولة المنصة + إيداع للموظف.</summary>
public sealed class EmployeeWalletFundingService(
    ApplicationDbContext context,
    EmployeeWalletTopUpCommissionService commissionService)
{
    public async Task<EmployeeWalletFundingResult> FundAsync(
        PayrollEmployee employee,
        int companyNetworkId,
        decimal amount,
        string actorUserId,
        EmployeeWalletTransactionSource source,
        int? topUpRequestId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction != null)
        {
            return await ExecuteFundingAsync(
                employee, companyNetworkId, amount, actorUserId, source, topUpRequestId, notes, cancellationToken);
        }

        await using IDbContextTransaction tx = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            EmployeeWalletFundingResult result = await ExecuteFundingAsync(
                employee, companyNetworkId, amount, actorUserId, source, topUpRequestId, notes, cancellationToken);
            if (!result.Success)
            {
                await tx.RollbackAsync(cancellationToken);
                return result;
            }

            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    internal async Task<EmployeeWalletFundingResult> ExecuteFundingAsync(
        PayrollEmployee employee,
        int companyNetworkId,
        decimal amount,
        string actorUserId,
        EmployeeWalletTransactionSource source,
        int? topUpRequestId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
        {
            return EmployeeWalletFundingResult.Fail("المبلغ يجب أن يكون أكبر من صفر.");
        }

        if (!employee.IsActive)
        {
            return EmployeeWalletFundingResult.Fail("لا يمكن تغذية محفظة موظف غير نشط.");
        }

        EmployeeWalletTransferPricingResult pricing = await commissionService.CalculateAsync(amount, cancellationToken);
        decimal totalDebit = pricing.TotalCompanyDebit;

        Network? company = await context.Networks
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, cancellationToken);
        if (company == null)
        {
            return EmployeeWalletFundingResult.Fail("تعذر تحديد محفظة الشركة.");
        }

        if (company.Balance < totalDebit)
        {
            return EmployeeWalletFundingResult.Fail(
                $"رصيد محفظة الشركة غير كافٍ. المطلوب {totalDebit:N0} ل.س.ج (تغذية {amount:N0}" +
                (pricing.HasCommission ? $" + عمولة {pricing.CommissionSyp:N0}" : "") +
                $") والرصيد {company.Balance:N0} ل.س.ج.");
        }

        try
        {
            PayrollEmployee? lockedEmployee = await context.PayrollEmployees
                .FirstOrDefaultAsync(e => e.Id == employee.Id, cancellationToken);
            if (lockedEmployee == null)
            {
                return EmployeeWalletFundingResult.Fail("سجل الموظف غير موجود.");
            }

            decimal prevEmployeeBalance = lockedEmployee.WalletBalance;
            lockedEmployee.WalletBalance = prevEmployeeBalance + amount;

            EmployeeWalletTransaction walletTx = new()
            {
                CompanyNetworkId = companyNetworkId,
                PayrollEmployeeId = lockedEmployee.Id,
                Amount = amount,
                PreviousBalance = prevEmployeeBalance,
                NewBalance = lockedEmployee.WalletBalance,
                PlatformCommissionAmount = pricing.CommissionSyp,
                Source = source,
                EmployeeWalletTopUpRequestId = topUpRequestId,
                CreatedByUserId = actorUserId,
                Notes = notes?.Trim()
            };
            context.EmployeeWalletTransactions.Add(walletTx);

            decimal prevCompanyBalance = company.Balance;
            company.Balance = prevCompanyBalance - amount;

            context.NetworkWalletTransactions.Add(new NetworkWalletTransaction
            {
                NetworkId = companyNetworkId,
                Type = NetworkWalletTransactionType.Adjustment,
                SignedAmount = -amount,
                PreviousBalance = prevCompanyBalance,
                NewBalance = company.Balance,
                EmployeeWalletTopUpRequestId = topUpRequestId,
                CreatedByUserId = actorUserId,
                CreatedAt = DateTime.Now,
                Notes = $"تغذية محفظة موظف #{lockedEmployee.Id} ({lockedEmployee.FullName})"
            });

            if (pricing.CommissionSyp > 0m)
            {
                decimal beforeCommission = company.Balance;
                company.Balance = beforeCommission - pricing.CommissionSyp;

                string commissionNote = pricing.CommissionPercent.HasValue
                    ? $"عمولة تغذية محفظة موظف {pricing.CommissionPercent.Value:N2}% من {amount:N0} ل.س.ج"
                    : $"عمولة ثابتة لتغذية محفظة موظف — {lockedEmployee.FullName}";

                context.NetworkWalletTransactions.Add(new NetworkWalletTransaction
                {
                    NetworkId = companyNetworkId,
                    Type = NetworkWalletTransactionType.ServiceCharge,
                    SignedAmount = -pricing.CommissionSyp,
                    PreviousBalance = beforeCommission,
                    NewBalance = company.Balance,
                    EmployeeWalletTopUpRequestId = topUpRequestId,
                    CreatedByUserId = actorUserId,
                    CreatedAt = DateTime.Now,
                    Notes = commissionNote
                });
            }

            await context.SaveChangesAsync(cancellationToken);

            return EmployeeWalletFundingResult.Ok(
                walletTx.Id,
                lockedEmployee.WalletBalance,
                pricing.CommissionSyp);
        }
        catch
        {
            throw;
        }
    }
}
