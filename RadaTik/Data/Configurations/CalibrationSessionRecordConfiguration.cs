using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CalibrationSessionRecordConfiguration : IEntityTypeConfiguration<CalibrationSessionRecord>
{
    public void Configure(EntityTypeBuilder<CalibrationSessionRecord> entity)
    {
        entity.ToTable("CalibrationSessions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(8).IsRequired();
        entity.Property(e => e.ScenarioName).HasMaxLength(80).IsRequired();
        entity.Property(e => e.ScenarioJson).IsRequired();
        entity.Property(e => e.SectorName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.ReceiverName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.ReceiverIp).HasMaxLength(64);
        entity.Property(e => e.ReceiverMac).HasMaxLength(32);
        entity.Property(e => e.AlignmentJson).IsRequired();
        entity.Property(e => e.PathSummary).HasMaxLength(400);
        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => new { e.NetworkId, e.LastActivityUtc });
        entity.HasIndex(e => e.ExpiresAtUtc);
    }
}
