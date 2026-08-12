using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class MaterialPurchaseInvoiceLineConfiguration : IEntityTypeConfiguration<MaterialPurchaseInvoiceLine>
{
    public void Configure(EntityTypeBuilder<MaterialPurchaseInvoiceLine> entity)
    {
        entity.ToTable("MaterialPurchaseInvoiceLines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.PackageUnit).HasConversion<int>();
        entity.Property(e => e.PackageQuantity).HasColumnType("decimal(18,3)");
        entity.Property(e => e.BaseQuantity).HasColumnType("decimal(18,3)");
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
        entity.Property(e => e.WholesalePrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.RetailPrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.ItemName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.ModelNumber).HasMaxLength(60);

        entity.HasOne(e => e.Invoice)
          .WithMany(i => i.Lines)
          .HasForeignKey(e => e.MaterialPurchaseInvoiceId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.WarehouseItem)
          .WithMany()
          .HasForeignKey(e => e.WarehouseItemId)
          .OnDelete(DeleteBehavior.SetNull);
    }
}
