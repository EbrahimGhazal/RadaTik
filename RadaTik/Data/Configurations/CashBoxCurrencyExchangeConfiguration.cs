using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CashBoxCurrencyExchangeConfiguration : IEntityTypeConfiguration<CashBoxCurrencyExchange>
{
    public void Configure(EntityTypeBuilder<CashBoxCurrencyExchange> entity)
    {
        entity.ToTable("CashBoxCurrencyExchanges");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.FromCurrency).HasConversion<int>();
        entity.Property(e => e.ToCurrency).HasConversion<int>();
        entity.Property(e => e.SourceAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,4)");
        entity.Property(e => e.TargetAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.HasIndex(e => e.CashBoxId);
        entity.HasIndex(e => e.CreatedAt);
        entity.HasIndex(e => e.CashBoxWithdrawalId).IsUnique();
        entity.HasIndex(e => e.CashBoxDepositId).IsUnique();
        entity.HasOne(e => e.CashBox)
            .WithMany()
            .HasForeignKey(e => e.CashBoxId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Withdrawal)
            .WithMany()
            .HasForeignKey(e => e.CashBoxWithdrawalId)
            .OnDelete(DeleteBehavior.NoAction);
        entity.HasOne(e => e.Deposit)
            .WithMany()
            .HasForeignKey(e => e.CashBoxDepositId)
            .OnDelete(DeleteBehavior.NoAction);
        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
