using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SystemPricingItemConfiguration : IEntityTypeConfiguration<SystemPricingItem>
{
    public void Configure(EntityTypeBuilder<SystemPricingItem> entity)
    {
        entity.ToTable("ItemPricings");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.AmountSYP).HasColumnType("decimal(18,2)");
        entity.Property(e => e.AmountUSD).HasColumnType("decimal(18,2)");
        entity.Property(e => e.ItemType).HasConversion<int>();
        entity.Property(e => e.Currency).HasConversion<int>();
        entity.Property(e => e.BillingPeriod).HasConversion<int>();
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
        entity.HasIndex(e => e.ItemType);
    }
}
