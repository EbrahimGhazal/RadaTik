using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class NetworkFeatureConfiguration : IEntityTypeConfiguration<NetworkFeature>
{
    public void Configure(EntityTypeBuilder<NetworkFeature> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
        entity.Property(e => e.IsEnabled).HasDefaultValue(true);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => new { e.NetworkId, e.Key }).IsUnique();
        entity.HasIndex(e => e.NetworkId);

        entity.HasOne(e => e.Network)
            .WithMany()
            .HasForeignKey(e => e.NetworkId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
