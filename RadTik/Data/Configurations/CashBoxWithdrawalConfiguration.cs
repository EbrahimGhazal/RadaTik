using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class CashBoxWithdrawalConfiguration : IEntityTypeConfiguration<CashBoxWithdrawal>
{
    public void Configure(EntityTypeBuilder<CashBoxWithdrawal> entity)
    {
        entity.ToTable("CashBoxWithdrawals");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.WithdrawnAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.WithdrawnByUserId).HasMaxLength(450);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,2)");
        entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,2)");
        entity.HasIndex(e => e.CashBoxId);
        entity.HasIndex(e => e.WithdrawnAt);
        entity.HasOne(e => e.CashBox)
            .WithMany(c => c.Withdrawals)
            .HasForeignKey(e => e.CashBoxId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.WithdrawnByUser)
            .WithMany()
            .HasForeignKey(e => e.WithdrawnByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
