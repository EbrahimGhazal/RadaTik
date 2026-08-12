using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SectorRadioMetricSampleConfiguration : IEntityTypeConfiguration<SectorRadioMetricSample>
{
    public void Configure(EntityTypeBuilder<SectorRadioMetricSample> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CapturedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.Source).HasMaxLength(40).HasDefaultValue("MikroTik");
        entity.Property(e => e.StatusMessage).HasMaxLength(500);
        entity.Property(e => e.TxRateMbps).HasColumnType("decimal(10,2)");
        entity.Property(e => e.RxRateMbps).HasColumnType("decimal(10,2)");

        entity.HasIndex(e => new { e.SectorId, e.CapturedAt });

        entity.HasOne(e => e.Sector)
            .WithMany()
            .HasForeignKey(e => e.SectorId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.MikroTikServer)
            .WithMany()
            .HasForeignKey(e => e.MikroTikServerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
