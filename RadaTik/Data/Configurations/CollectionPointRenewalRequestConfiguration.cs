using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CollectionPointRenewalRequestConfiguration : IEntityTypeConfiguration<CollectionPointRenewalRequest>
{
    public void Configure(EntityTypeBuilder<CollectionPointRenewalRequest> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.RequestedByUserId).HasMaxLength(450);
        entity.Property(e => e.ProcessedByUserId).HasMaxLength(450);
        entity.Property(e => e.AdminNotes).HasMaxLength(500);

        entity.HasIndex(e => e.ClientId);
        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.RequestedAt);

        entity.HasOne(e => e.Client).WithMany().HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Network).WithMany().HasForeignKey(e => e.NetworkId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.RequestedByUser).WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ProcessedByUser).WithMany().HasForeignKey(e => e.ProcessedByUserId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
    }
}
