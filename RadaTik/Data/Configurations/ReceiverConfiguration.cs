using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class ReceiverConfiguration : IEntityTypeConfiguration<Receiver>
{
    public void Configure(EntityTypeBuilder<Receiver> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.IPAddress).IsRequired();
        entity.Property(e => e.NetworkMask).IsRequired();
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasOne(r => r.Sector)
            .WithMany(s => s.Receivers)
            .HasForeignKey(r => r.SectorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(r => r.Network)
            .WithMany(n => n.Receivers)
            .HasForeignKey(r => r.NetworkId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        entity.Ignore(e => e.UserCount);
        entity.Ignore(e => e.ProfileNames);
        entity.Ignore(e => e.MikroTikServerName);
    }
}
