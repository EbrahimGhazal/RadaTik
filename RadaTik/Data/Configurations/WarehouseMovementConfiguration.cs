using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class WarehouseMovementConfiguration : IEntityTypeConfiguration<WarehouseMovement>
{
    public void Configure(EntityTypeBuilder<WarehouseMovement> entity)
    {
        entity.ToTable("WarehouseMovements");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.MovementType).HasConversion<int>();
        entity.Property(e => e.Quantity).HasColumnType("decimal(18,3)");
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => e.WarehouseItemId);
        entity.HasIndex(e => e.MovementDate);

        entity.HasOne(e => e.CompanyNetwork)
          .WithMany()
          .HasForeignKey(e => e.CompanyNetworkId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.WarehouseItem)
          .WithMany(i => i.Movements)
          .HasForeignKey(e => e.WarehouseItemId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.CreatedByUser)
          .WithMany()
          .HasForeignKey(e => e.CreatedByUserId)
          .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.MaterialPurchaseInvoice)
          .WithMany()
          .HasForeignKey(e => e.MaterialPurchaseInvoiceId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.MaterialSalesInvoice)
          .WithMany()
          .HasForeignKey(e => e.MaterialSalesInvoiceId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.WarehouseStocktake)
          .WithMany()
          .HasForeignKey(e => e.WarehouseStocktakeId)
          .OnDelete(DeleteBehavior.Restrict);
    }
}
