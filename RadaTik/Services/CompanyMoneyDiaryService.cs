using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public interface ICompanyMoneyDiaryService
{
    Task<bool> TryPostSalaryExpenseAsync(
        int companyNetworkId,
        int payrollPaymentId,
        decimal amount,
        string description,
        string? userId,
        CancellationToken cancellationToken = default);
}

public sealed class CompanyMoneyDiaryService(ApplicationDbContext context) : ICompanyMoneyDiaryService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<bool> TryPostSalaryExpenseAsync(
        int companyNetworkId,
        int payrollPaymentId,
        decimal amount,
        string description,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
        {
            return false;
        }

        bool exists = await _context.MoneyDiaryEntries.AnyAsync(
            e => e.PayrollPaymentId == payrollPaymentId,
            cancellationToken);
        if (exists)
        {
            return false;
        }

        _context.MoneyDiaryEntries.Add(new MoneyDiaryEntry
        {
            CompanyNetworkId = companyNetworkId,
            EntryType = MoneyDiaryEntryType.Expense,
            CategoryKey = "expense_salary",
            Amount = amount,
            Currency = PricingCurrency.SYP_New,
            EntryDate = DateTime.Today,
            Description = description,
            CreatedByUserId = userId,
            PayrollPaymentId = payrollPaymentId,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
