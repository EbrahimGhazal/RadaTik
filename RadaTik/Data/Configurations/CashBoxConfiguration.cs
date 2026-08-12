using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Data.Infrastructure;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CashBoxConfiguration : IEntityTypeConfiguration<CashBox>
{
    public void Configure(EntityTypeBuilder<CashBox> entity)
    {
        entity.ToTable("CashBoxes");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.OwnerType).HasConversion<int>();
        entity.Property(e => e.Balance).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        entity.Property(e => e.BalanceUsd).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
        entity.ConfigureBalanceRowVersion();
        entity.HasIndex(e => new { e.OwnerType, e.OwnerId }).IsUnique();
    }
}
