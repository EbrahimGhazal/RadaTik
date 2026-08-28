using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SubscriberFaultDiagnosisRunConfiguration : IEntityTypeConfiguration<SubscriberFaultDiagnosisRun>
{
    public void Configure(EntityTypeBuilder<SubscriberFaultDiagnosisRun> entity)
    {
        entity.ToTable("SubscriberFaultDiagnosisRuns");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.CauseLabel).HasMaxLength(80).IsRequired();
        entity.Property(e => e.Summary).HasMaxLength(800).IsRequired();
        entity.Property(e => e.SuggestedAction).HasMaxLength(400);
        entity.Property(e => e.EvidenceJson).HasColumnType("nvarchar(max)");

        entity.HasIndex(e => new { e.ClientId, e.CreatedAt });
        entity.HasIndex(e => e.MaintenanceRequestId);

        entity.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Network)
            .WithMany()
            .HasForeignKey(e => e.NetworkId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.MaintenanceRequest)
            .WithMany()
            .HasForeignKey(e => e.MaintenanceRequestId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
