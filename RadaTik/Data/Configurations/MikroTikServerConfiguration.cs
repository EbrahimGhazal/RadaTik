using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Data.Infrastructure;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class MikroTikServerConfiguration : IEntityTypeConfiguration<MikroTikServer>
{
    public void Configure(EntityTypeBuilder<MikroTikServer> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(100);
        entity.Property(e => e.Host).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Port).HasDefaultValue(8728);
        entity.Property(e => e.User).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Pass)
            .IsRequired()
            .HasMaxLength(512)
            .HasConversion(SensitiveDataConverters.String);
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => new { e.NetworkId, e.Host, e.Port }).IsUnique();

        entity.HasOne(m => m.Network)
            .WithMany(n => n.MikroTikServers)
            .HasForeignKey(m => m.NetworkId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
