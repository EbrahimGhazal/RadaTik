using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class FeaturePricingConfiguration : IEntityTypeConfiguration<FeaturePricing>
{
    public void Configure(EntityTypeBuilder<FeaturePricing> entity)
    {
        entity.ToTable("FeaturePricings");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.FeatureKey).IsRequired().HasMaxLength(100);
        entity.Property(e => e.BillingPeriod).HasConversion<int>();
        entity.Property(e => e.ChargeUnit).HasConversion<int>().HasDefaultValue(PricingChargeUnit.Flat);
        entity.Property(e => e.Currency).HasConversion<int>();
        entity.Property(e => e.AmountSYP).HasColumnType("decimal(18,2)");
        entity.Property(e => e.AmountUSD).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.FeatureKey);
        entity.HasIndex(e => new { e.FeatureKey, e.BillingPeriod }).IsUnique();
    }
}
