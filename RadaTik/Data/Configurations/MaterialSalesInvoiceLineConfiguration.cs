using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class MaterialSalesInvoiceLineConfiguration : IEntityTypeConfiguration<MaterialSalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<MaterialSalesInvoiceLine> entity)
    {
        entity.ToTable("MaterialSalesInvoiceLines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.PriceMode).HasConversion<int>();
        entity.Property(e => e.Quantity).HasColumnType("decimal(18,3)");
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");

        entity.HasOne(e => e.Invoice)
          .WithMany(i => i.Lines)
          .HasForeignKey(e => e.MaterialSalesInvoiceId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.WarehouseItem)
          .WithMany()
          .HasForeignKey(e => e.WarehouseItemId)
          .OnDelete(DeleteBehavior.Restrict);
    }
}
