using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class WarehouseItemConfiguration : IEntityTypeConfiguration<WarehouseItem>
{
    public void Configure(EntityTypeBuilder<WarehouseItem> entity)
    {
        entity.ToTable("WarehouseItems");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
        entity.Property(e => e.Unit).HasMaxLength(40);
        entity.Property(e => e.Sku).HasMaxLength(60);
        entity.Property(e => e.ModelNumber).HasMaxLength(60);
        entity.Property(e => e.PurchasePrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.PurchaseCurrency).HasConversion<int>();
        entity.Property(e => e.WholesalePrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.RetailPrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Name });
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Name, e.ModelNumber });

        entity.HasOne(e => e.CompanyNetwork)
          .WithMany()
          .HasForeignKey(e => e.CompanyNetworkId)
          .OnDelete(DeleteBehavior.Restrict);
    }
}
