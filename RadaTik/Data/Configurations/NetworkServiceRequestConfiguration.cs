using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class NetworkServiceRequestConfiguration : IEntityTypeConfiguration<NetworkServiceRequest>
{
    public void Configure(EntityTypeBuilder<NetworkServiceRequest> entity)
    {
        entity.ToTable("NetworkServiceRequests");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.FeatureKey).IsRequired().HasMaxLength(100);
        entity.Property(e => e.BillingPeriod).HasConversion<int>();
        entity.Property(e => e.Currency).HasConversion<int>();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.AmountSYP).HasColumnType("decimal(18,2)");
        entity.Property(e => e.AmountUSD).HasColumnType("decimal(18,2)");
        entity.Property(e => e.RequestedByUserId).IsRequired().HasMaxLength(450);
        entity.Property(e => e.DecidedByUserId).HasMaxLength(450);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.RequestedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.RequestedAt);
        entity.HasIndex(e => e.FeatureKey);

        entity.HasOne(e => e.Network)
              .WithMany()
              .HasForeignKey(e => e.NetworkId)
              .OnDelete(DeleteBehavior.Cascade)
              .IsRequired();

        entity.HasOne(e => e.FeaturePricing)
              .WithMany()
              .HasForeignKey(e => e.FeaturePricingId)
              .OnDelete(DeleteBehavior.SetNull)
              .IsRequired(false);

        entity.HasOne(e => e.RequestedByUser)
              .WithMany()
              .HasForeignKey(e => e.RequestedByUserId)
              .OnDelete(DeleteBehavior.NoAction)
              .IsRequired();

        entity.HasOne(e => e.DecidedByUser)
              .WithMany()
              .HasForeignKey(e => e.DecidedByUserId)
              .OnDelete(DeleteBehavior.NoAction)
              .IsRequired(false);
    }
}
