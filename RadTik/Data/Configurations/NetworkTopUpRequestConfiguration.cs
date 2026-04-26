using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class NetworkTopUpRequestConfiguration : IEntityTypeConfiguration<NetworkTopUpRequest>
{
    public void Configure(EntityTypeBuilder<NetworkTopUpRequest> entity)
    {
        entity.ToTable("NetworkTopUpRequests");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.Method).HasMaxLength(200);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(200);
        entity.Property(e => e.ReceiptImagePath).HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.RequestedByUserId).IsRequired().HasMaxLength(450);
        entity.Property(e => e.DecidedByUserId).HasMaxLength(450);
        entity.Property(e => e.RequestedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.RequestedAt);
        entity.HasIndex(e => e.PaymentMethodId);

        entity.HasOne(e => e.Network)
              .WithMany()
              .HasForeignKey(e => e.NetworkId)
              .OnDelete(DeleteBehavior.Cascade)
              .IsRequired();

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

        entity.HasOne(e => e.PaymentMethod)
              .WithMany()
              .HasForeignKey(e => e.PaymentMethodId)
              .OnDelete(DeleteBehavior.SetNull)
              .IsRequired(false);
    }
}
