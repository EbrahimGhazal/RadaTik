using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SubscriberInstallationMaterialPriceConfiguration : IEntityTypeConfiguration<SubscriberInstallationMaterialPrice>
{
    public void Configure(EntityTypeBuilder<SubscriberInstallationMaterialPrice> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.MaterialKey).HasMaxLength(60).IsRequired();
        entity.Property(e => e.MaterialName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => new { e.NetworkId, e.MaterialKey }).IsUnique();
        entity.HasIndex(e => e.IsActive);

        entity.HasOne(e => e.Network)
            .WithMany()
            .HasForeignKey(e => e.NetworkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
