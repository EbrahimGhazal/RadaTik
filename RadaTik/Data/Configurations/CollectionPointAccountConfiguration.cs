using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Data.Infrastructure;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CollectionPointAccountConfiguration : IEntityTypeConfiguration<CollectionPointAccount>
{
    public void Configure(EntityTypeBuilder<CollectionPointAccount> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Balance).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
        entity.ConfigureBalanceRowVersion();

        entity.HasIndex(e => e.UserId).IsUnique();
        entity.HasIndex(e => e.NetworkId);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Network)
            .WithMany()
            .HasForeignKey(e => e.NetworkId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
