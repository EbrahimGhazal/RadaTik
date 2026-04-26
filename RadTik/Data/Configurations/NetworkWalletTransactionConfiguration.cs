using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class NetworkWalletTransactionConfiguration : IEntityTypeConfiguration<NetworkWalletTransaction>
{
    public void Configure(EntityTypeBuilder<NetworkWalletTransaction> entity)
    {
        entity.ToTable("NetworkWalletTransactions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Type).HasConversion<int>();
        entity.Property(e => e.SignedAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.PreviousBalance).HasColumnType("decimal(18,2)");
        entity.Property(e => e.NewBalance).HasColumnType("decimal(18,2)");
        entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => e.CreatedAt);
        entity.HasIndex(e => e.Type);

        entity.HasOne(e => e.Network)
              .WithMany()
              .HasForeignKey(e => e.NetworkId)
              .OnDelete(DeleteBehavior.Cascade)
              .IsRequired();

        entity.HasOne(e => e.CreatedByUser)
              .WithMany()
              .HasForeignKey(e => e.CreatedByUserId)
              .OnDelete(DeleteBehavior.NoAction)
              .IsRequired();

        entity.HasOne(e => e.RelatedPaymentTransaction)
              .WithMany()
              .HasForeignKey(e => e.RelatedPaymentTransactionId)
              .OnDelete(DeleteBehavior.NoAction);
    }
}
