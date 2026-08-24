using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CompanyDocumentAppearanceConfiguration : IEntityTypeConfiguration<CompanyDocumentAppearance>
{
    public void Configure(EntityTypeBuilder<CompanyDocumentAppearance> entity)
    {
        entity.ToTable("CompanyDocumentAppearances");
        entity.Property(e => e.CustomLogoPath).HasMaxLength(500);
        entity.Property(e => e.PrimaryColor).HasMaxLength(7).IsRequired();
        entity.Property(e => e.TableHeaderColor).HasMaxLength(7).IsRequired();
        entity.Property(e => e.WatermarkText).HasMaxLength(80);
        entity.Property(e => e.FooterText).HasMaxLength(250);
        entity.Property(e => e.UpdatedByUserId).HasMaxLength(450);
        entity.HasIndex(e => e.CompanyNetworkId).IsUnique();
        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
