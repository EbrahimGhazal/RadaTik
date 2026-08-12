using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SectorRadioEventConfiguration : IEntityTypeConfiguration<SectorRadioEvent>
{
    public void Configure(EntityTypeBuilder<SectorRadioEvent> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Severity).HasMaxLength(16);
        entity.Property(e => e.EventType).HasMaxLength(32);
        entity.Property(e => e.MetricName).HasMaxLength(64);
        entity.Property(e => e.Message).HasMaxLength(400);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.MetricValue).HasColumnType("decimal(10,2)");
        entity.Property(e => e.ThresholdValue).HasColumnType("decimal(10,2)");

        entity.HasIndex(e => new { e.SectorId, e.CreatedAt });
        entity.HasIndex(e => new { e.SectorId, e.EventType, e.MetricName, e.CreatedAt });

        entity.HasOne(e => e.Sector)
            .WithMany()
            .HasForeignKey(e => e.SectorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.MetricSample)
            .WithMany()
            .HasForeignKey(e => e.MetricSampleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
