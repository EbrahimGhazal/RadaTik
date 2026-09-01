using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CompanyComplaintContactConfiguration : IEntityTypeConfiguration<CompanyComplaintContact>
{
    public void Configure(EntityTypeBuilder<CompanyComplaintContact> entity)
    {
        entity.ToTable("CompanyComplaintContacts");
        entity.Property(e => e.Label).HasMaxLength(80).IsRequired();
        entity.Property(e => e.PhoneNumber).HasMaxLength(40).IsRequired();
        entity.HasIndex(e => new { e.CompanyNetworkId, e.SortOrder });
        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
