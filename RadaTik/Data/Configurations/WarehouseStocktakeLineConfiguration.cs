using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class WarehouseStocktakeLineConfiguration : IEntityTypeConfiguration<WarehouseStocktakeLine>
{
    public void Configure(EntityTypeBuilder<WarehouseStocktakeLine> entity)
    {
        entity.ToTable("WarehouseStocktakeLines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SystemQuantity).HasColumnType("decimal(18,3)");
        entity.Property(e => e.CountedQuantity).HasColumnType("decimal(18,3)");
        entity.Property(e => e.Difference).HasColumnType("decimal(18,3)");

        entity.HasOne(e => e.Stocktake)
          .WithMany(s => s.Lines)
          .HasForeignKey(e => e.WarehouseStocktakeId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.WarehouseItem)
          .WithMany()
          .HasForeignKey(e => e.WarehouseItemId)
          .OnDelete(DeleteBehavior.Restrict);
    }
}
