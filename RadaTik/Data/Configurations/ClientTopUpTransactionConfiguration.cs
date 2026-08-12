using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class ClientTopUpTransactionConfiguration : IEntityTypeConfiguration<ClientTopUpTransaction>
{
    public void Configure(EntityTypeBuilder<ClientTopUpTransaction> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.PreviousBalance).HasColumnType("decimal(18,2)");
        entity.Property(e => e.NewBalance).HasColumnType("decimal(18,2)");

        entity.HasIndex(e => e.ClientId);
        entity.HasIndex(e => e.CreatedAt);
        entity.HasIndex(e => e.SourceType);

        entity.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Network)
            .WithMany()
            .HasForeignKey(e => e.NetworkId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        entity.HasOne(e => e.CollectionPointAccount)
            .WithMany()
            .HasForeignKey(e => e.CollectionPointAccountId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
