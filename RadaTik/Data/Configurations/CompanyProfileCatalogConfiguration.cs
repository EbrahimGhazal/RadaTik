using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CompanyProfileCatalogConfiguration : IEntityTypeConfiguration<CompanyProfileCatalog>
{
    public void Configure(EntityTypeBuilder<CompanyProfileCatalog> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
        entity.Property(e => e.VATPercentage).HasPrecision(5, 2).HasDefaultValue(15m);
        entity.Property(e => e.DownloadSpeedUnit).HasConversion<int>();
        entity.Property(e => e.UploadSpeedUnit).HasConversion<int?>();
        entity.Property(e => e.DataLimit).HasColumnType("decimal(18,2)");
        entity.Property(e => e.MikroTikService).HasDefaultValue("pppoe");
        entity.Property(e => e.MikroTikOnlyOne).HasDefaultValue(true);
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.IsForNewClients).HasDefaultValue(true);

        entity.HasIndex(e => new { e.CompanyNetworkId, e.Name }).IsUnique();

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
