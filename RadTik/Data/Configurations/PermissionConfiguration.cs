using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
        entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Category).HasMaxLength(100);
        entity.HasIndex(e => e.Key).IsUnique();
        entity.HasIndex(e => e.Category);
    }
}
