using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CollectionPointTopUpRequestConfiguration : IEntityTypeConfiguration<CollectionPointTopUpRequest>
{
    public void Configure(EntityTypeBuilder<CollectionPointTopUpRequest> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Method).HasMaxLength(200);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.ReceiptImagePath).HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.RequestedByUserId).HasMaxLength(450);
        entity.Property(e => e.ProcessedByUserId).HasMaxLength(450);
        entity.Property(e => e.AdminNotes).HasMaxLength(500);

        entity.HasIndex(e => e.CollectionPointAccountId);
        entity.HasIndex(e => e.PaymentMethodId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.RequestedAt);

        entity.HasOne(e => e.CollectionPointAccount)
            .WithMany()
            .HasForeignKey(e => e.CollectionPointAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.PaymentMethod)
            .WithMany()
            .HasForeignKey(e => e.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        entity.HasOne(e => e.RequestedByUser)
            .WithMany()
            .HasForeignKey(e => e.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ProcessedByUser)
            .WithMany()
            .HasForeignKey(e => e.ProcessedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
