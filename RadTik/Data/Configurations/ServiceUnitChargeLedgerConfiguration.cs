using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class ServiceUnitChargeLedgerConfiguration : IEntityTypeConfiguration<ServiceUnitChargeLedger>
{
    public void Configure(EntityTypeBuilder<ServiceUnitChargeLedger> entity)
    {
        entity.ToTable("ServiceUnitChargeLedgers");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ChargeUnit).HasConversion<int>();
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.UnitEntityKey).IsRequired().HasMaxLength(128);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.NetworkServiceSubscriptionId);
        entity.HasIndex(e => new { e.NetworkServiceSubscriptionId, e.ChargeUnit, e.UnitEntityKey }).IsUnique();

        entity.HasOne(e => e.Subscription)
              .WithMany()
              .HasForeignKey(e => e.NetworkServiceSubscriptionId)
              .OnDelete(DeleteBehavior.Cascade)
              .IsRequired();
    }
}
