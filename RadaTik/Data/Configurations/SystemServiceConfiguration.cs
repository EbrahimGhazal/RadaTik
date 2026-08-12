using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SystemServiceConfiguration : IEntityTypeConfiguration<SystemService>
{
    public void Configure(EntityTypeBuilder<SystemService> entity)
    {
        // IMPORTANT: avoid collision with an existing legacy table named "SystemServices"
        entity.ToTable("SystemServiceCatalog");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
        entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.Property(e => e.IconClass).HasMaxLength(100);
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
        entity.HasIndex(e => e.Key).IsUnique();
        entity.HasIndex(e => e.IsActive);
    }
}
