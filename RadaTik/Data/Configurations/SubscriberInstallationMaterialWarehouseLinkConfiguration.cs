using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SubscriberInstallationMaterialWarehouseLinkConfiguration
    : IEntityTypeConfiguration<SubscriberInstallationMaterialWarehouseLink>
{
    public void Configure(EntityTypeBuilder<SubscriberInstallationMaterialWarehouseLink> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.MaterialPriceId, e.WarehouseItemId }).IsUnique();
        entity.HasIndex(e => e.WarehouseItemId);

        entity.HasOne(e => e.MaterialPrice)
            .WithMany(m => m.WarehouseLinks)
            .HasForeignKey(e => e.MaterialPriceId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.WarehouseItem)
            .WithMany()
            .HasForeignKey(e => e.WarehouseItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
