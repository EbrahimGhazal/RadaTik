using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> entity)
    {
        entity.ToTable("PaymentMethods");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.IsCash).HasDefaultValue(false);
        entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.Name).IsUnique();
        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.IsCash);
        entity.HasIndex(e => e.DisplayOrder);
    }
}
