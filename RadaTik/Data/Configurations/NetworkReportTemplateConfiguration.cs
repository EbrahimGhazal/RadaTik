using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class NetworkReportTemplateConfiguration : IEntityTypeConfiguration<NetworkReportTemplate>
{
    public void Configure(EntityTypeBuilder<NetworkReportTemplate> entity)
    {
        entity.ToTable("NetworkReportTemplates");
        entity.Property(e => e.BodyContent).HasColumnType("nvarchar(max)");
        entity.Property(e => e.UpdatedByUserId).HasMaxLength(450);
        entity.HasIndex(e => new { e.CompanyNetworkId, e.ReportKind }).IsUnique();
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
