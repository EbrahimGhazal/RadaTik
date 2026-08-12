using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.IPAddress).IsRequired();
        entity.Property(e => e.NetworkMask).IsRequired();
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.RadioInterfaceName).HasMaxLength(100);
        entity.Property(e => e.NoiseAlertThresholdDbm).HasDefaultValue(-90);
        entity.Property(e => e.SnrAlertMinDb).HasDefaultValue(20);
        entity.Property(e => e.CcqAlertMinPercent).HasDefaultValue(70);

        entity.HasOne(s => s.MikroTikServer)
            .WithMany(m => m.Sectors)
            .HasForeignKey(s => s.MikroTikServerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(s => s.Network)
            .WithMany(n => n.Sectors)
            .HasForeignKey(s => s.NetworkId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        entity.Ignore(e => e.ReceiverCount);
        entity.Ignore(e => e.UserCount);
        entity.Ignore(e => e.ProfileNames);
    }
}
