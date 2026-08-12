using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class EmployeeWalletTransactionConfiguration : IEntityTypeConfiguration<EmployeeWalletTransaction>
{
    public void Configure(EntityTypeBuilder<EmployeeWalletTransaction> entity)
    {
        entity.ToTable("EmployeeWalletTransactions");
        entity.HasIndex(e => new { e.PayrollEmployeeId, e.CreatedAt });

        entity.HasOne(e => e.PayrollEmployee)
            .WithMany(p => p.WalletTransactions)
            .HasForeignKey(e => e.PayrollEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.EmployeeWalletTopUpRequest)
            .WithMany()
            .HasForeignKey(e => e.EmployeeWalletTopUpRequestId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
