using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models.Business;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public class CompanyPayrollProrationTests
{
    [Fact]
    public void CalculateProratedMonthlyBase_FullMonth_ReturnsFullSalary()
    {
        PayrollEmployee employee = new()
        {
            MonthlySalary = 30_000m,
            EmploymentType = PayrollEmploymentType.FullTime,
            HireDate = new DateTime(2020, 1, 1),
            IsActive = true
        };

        decimal result = CompanyPayrollService.CalculateProratedMonthlyBase(employee, 2026, 5);
        Assert.Equal(30_000m, result);
    }

    [Fact]
    public void CalculateProratedMonthlyBase_HiredMidMonth_ProratesFromHireDate()
    {
        PayrollEmployee employee = new()
        {
            MonthlySalary = 30_000m,
            EmploymentType = PayrollEmploymentType.FullTime,
            HireDate = new DateTime(2026, 5, 16),
            IsActive = true
        };

        PayrollMonthEmploymentPeriod period = CompanyPayrollService.GetEmploymentPeriod(employee, 2026, 5);
        Assert.Equal(16, period.WorkedDays);

        decimal result = CompanyPayrollService.CalculateProratedMonthlyBase(employee, 2026, 5);
        Assert.Equal(15_483.87m, result);
    }

    [Fact]
    public void CalculateProratedMonthlyBase_TerminatedMidMonth_ProratesUntilTermination()
    {
        PayrollEmployee employee = new()
        {
            MonthlySalary = 30_000m,
            EmploymentType = PayrollEmploymentType.FullTime,
            HireDate = new DateTime(2020, 1, 1),
            TerminationDate = new DateTime(2026, 5, 10),
            IsActive = false
        };

        PayrollMonthEmploymentPeriod period = CompanyPayrollService.GetEmploymentPeriod(employee, 2026, 5);
        Assert.Equal(10, period.WorkedDays);

        decimal result = CompanyPayrollService.CalculateProratedMonthlyBase(employee, 2026, 5);
        Assert.Equal(9_677.42m, result);
    }

    [Fact]
    public async Task BuildMonthLedger_WithoutPaymentRecord_AccruedBaseIsZero()
    {
        await using ApplicationDbContext context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        PayrollEmployee employee = new()
        {
            MonthlySalary = 18_000m,
            EmploymentType = PayrollEmploymentType.FullTime,
            HireDate = DateTime.Today,
            IsActive = true
        };

        CompanyPayrollService service = new(context);
        PayrollMonthLedger ledger = await service.BuildMonthLedgerAsync(employee, 2026, 5, payment: null);

        Assert.Equal(0m, ledger.AccruedBase);
        Assert.Equal(0m, ledger.NetPayable);
    }

    [Fact]
    public async Task BuildMonthLedger_WithPreparedPayment_UsesBaseAmount()
    {
        await using ApplicationDbContext context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        PayrollEmployee employee = new()
        {
            MonthlySalary = 18_000m,
            EmploymentType = PayrollEmploymentType.FullTime,
            HireDate = DateTime.Today,
            IsActive = true
        };

        PayrollPayment payment = new()
        {
            BaseAmount = 18_000m,
            Year = 2026,
            Month = 5
        };

        CompanyPayrollService service = new(context);
        PayrollMonthLedger ledger = await service.BuildMonthLedgerAsync(employee, 2026, 5, payment);

        Assert.Equal(18_000m, ledger.AccruedBase);
        Assert.Equal(18_000m, ledger.NetPayable);
    }
}
