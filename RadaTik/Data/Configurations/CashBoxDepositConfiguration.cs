using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CashBoxDepositConfiguration : IEntityTypeConfiguration<CashBoxDeposit>
{
    public void Configure(EntityTypeBuilder<CashBoxDeposit> entity)
    {
        entity.ToTable("CashBoxDeposits");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Currency).HasConversion<int>().HasDefaultValue(PricingCurrency.SYP_New);
        entity.Property(e => e.DepositedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.DepositedByUserId).HasMaxLength(450);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,2)");
        entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,2)");
        entity.HasIndex(e => e.CashBoxId);
        entity.HasIndex(e => e.DepositedAt);
        entity.HasIndex(e => e.PaymentMethodId);
        entity.HasIndex(e => e.NetworkTopUpRequestId).IsUnique().HasFilter("[NetworkTopUpRequestId] IS NOT NULL");
        entity.HasIndex(e => e.CollectionPointTopUpRequestId).IsUnique().HasFilter("[CollectionPointTopUpRequestId] IS NOT NULL");
        entity.HasOne(e => e.CashBox)
            .WithMany(c => c.Deposits)
            .HasForeignKey(e => e.CashBoxId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.DepositedByUser)
            .WithMany()
            .HasForeignKey(e => e.DepositedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
        entity.HasOne(e => e.PaymentMethod)
            .WithMany()
            .HasForeignKey(e => e.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
        entity.HasOne(e => e.NetworkTopUpRequest)
            .WithMany()
            .HasForeignKey(e => e.NetworkTopUpRequestId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
        entity.HasOne(e => e.CollectionPointTopUpRequest)
            .WithMany()
            .HasForeignKey(e => e.CollectionPointTopUpRequestId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
