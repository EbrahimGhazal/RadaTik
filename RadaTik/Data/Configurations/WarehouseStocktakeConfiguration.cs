using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class WarehouseStocktakeConfiguration : IEntityTypeConfiguration<WarehouseStocktake>
{
    public void Configure(EntityTypeBuilder<WarehouseStocktake> entity)
    {
        entity.ToTable("WarehouseStocktakes");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => e.StocktakeDate);

        entity.HasOne(e => e.CompanyNetwork)
          .WithMany()
          .HasForeignKey(e => e.CompanyNetworkId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.WarehouseItem)
          .WithMany()
          .HasForeignKey(e => e.WarehouseItemId)
          .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.CreatedByUser)
          .WithMany()
          .HasForeignKey(e => e.CreatedByUserId)
          .OnDelete(DeleteBehavior.SetNull);
    }
}
