using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CompanySocialLinkConfiguration : IEntityTypeConfiguration<CompanySocialLink>
{
    public void Configure(EntityTypeBuilder<CompanySocialLink> entity)
    {
        entity.ToTable("CompanySocialLinks");
        entity.Property(e => e.DisplayName).HasMaxLength(80).IsRequired();
        entity.Property(e => e.Url).HasMaxLength(500).IsRequired();
        entity.Property(e => e.Platform).HasConversion<int>();
        entity.HasIndex(e => new { e.CompanyNetworkId, e.SortOrder });
        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
