using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class AntennaCalibrationSessionRecordConfiguration : IEntityTypeConfiguration<AntennaCalibrationSessionRecord>
{
    public void Configure(EntityTypeBuilder<AntennaCalibrationSessionRecord> entity)
    {
        entity.ToTable("AntennaCalibrationSessions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(8).IsRequired();
        entity.Property(e => e.SectorName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.ReceiverName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.ReceiverIp).HasMaxLength(64);
        entity.Property(e => e.ReceiverMac).HasMaxLength(32);
        entity.Property(e => e.AlignmentJson).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => new { e.NetworkId, e.LastActivityUtc });
        entity.HasIndex(e => e.ExpiresAtUtc);
    }
}
