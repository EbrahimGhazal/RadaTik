using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public class WalletLedgerUnifiedEntryConfiguration : IEntityTypeConfiguration<WalletLedgerUnifiedEntry>
{
    public void Configure(EntityTypeBuilder<WalletLedgerUnifiedEntry> entity)
    {
        entity.ToView("vw_WalletLedgerUnified");
        entity.HasNoKey();
        entity.Property(e => e.LedgerSource).HasMaxLength(32);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.CurrencyCode).HasMaxLength(16);
        entity.Property(e => e.Category).HasMaxLength(64);
        entity.Property(e => e.Notes).HasMaxLength(1000);
    }
}
