using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class NetworkServiceSubscriptionConfiguration : IEntityTypeConfiguration<NetworkServiceSubscription>
{
    public void Configure(EntityTypeBuilder<NetworkServiceSubscription> entity)
    {
        entity.ToTable("NetworkServiceSubscriptions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.FeatureKey).IsRequired().HasMaxLength(100);
        entity.Property(e => e.BillingPeriod).HasConversion<int>();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.StartAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => new { e.NetworkId, e.FeatureKey }).IsUnique();
        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => e.ExpiresAt);
        entity.HasIndex(e => e.Status);

        entity.HasOne(e => e.Network)
              .WithMany()
              .HasForeignKey(e => e.NetworkId)
              .OnDelete(DeleteBehavior.Cascade)
              .IsRequired();
    }
}
