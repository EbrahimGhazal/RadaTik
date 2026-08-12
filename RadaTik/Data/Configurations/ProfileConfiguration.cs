using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
        entity.Property(e => e.VATPercentage).HasPrecision(5, 2).HasDefaultValue(15);
        entity.Property(e => e.DownloadSpeedUnit).HasConversion<int>();
        entity.Property(e => e.UploadSpeedUnit).HasConversion<int?>();
        entity.Property(e => e.DataLimit).HasColumnType("decimal(18,2)");
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.IsForNewClients).HasDefaultValue(true);
        entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
        entity.Property(e => e.IsSyncedWithMikroTik).HasDefaultValue(false);
        entity.Property(e => e.MikroTikOnlyOne).HasDefaultValue(true);
        entity.Property(e => e.MikroTikService).HasDefaultValue("pppoe");
        entity.Property(e => e.MinDevices).HasDefaultValue(1);
        entity.Property(e => e.MaxDevices).HasDefaultValue(1);
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedDate).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => new { e.MikroTikServerId, e.Name }).IsUnique();
        entity.HasIndex(e => new { e.CompanyProfileCatalogId, e.MikroTikServerId })
            .IsUnique()
            .HasFilter("[CompanyProfileCatalogId] IS NOT NULL");

        entity.HasOne(p => p.MikroTikServer)
            .WithMany()
            .HasForeignKey(p => p.MikroTikServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        entity.HasOne(p => p.CompanyProfileCatalog)
            .WithMany(c => c.Deployments)
            .HasForeignKey(p => p.CompanyProfileCatalogId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        entity.HasOne(p => p.Network)
            .WithMany(n => n.Profiles)
            .HasForeignKey(p => p.NetworkId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
