using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class MikroTikServerTrafficSampleConfiguration : IEntityTypeConfiguration<MikroTikServerTrafficSample>
{
    public void Configure(EntityTypeBuilder<MikroTikServerTrafficSample> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CapturedAtUtc).HasDefaultValueSql("GETUTCDATE()");

        entity.HasIndex(e => new { e.MikroTikServerId, e.CapturedAtUtc });
        entity.HasIndex(e => new { e.NetworkId, e.CapturedAtUtc });

        entity.HasOne(e => e.MikroTikServer)
            .WithMany()
            .HasForeignKey(e => e.MikroTikServerId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Network)
            .WithMany()
            .HasForeignKey(e => e.NetworkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
