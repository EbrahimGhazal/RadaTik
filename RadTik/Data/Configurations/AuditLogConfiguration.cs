using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.Roles).HasMaxLength(500);
        entity.Property(e => e.HttpMethod).HasMaxLength(50);
        entity.Property(e => e.Controller).HasMaxLength(200);
        entity.Property(e => e.Action).HasMaxLength(200);
        entity.Property(e => e.Path).HasMaxLength(500);
        entity.Property(e => e.EntityType).HasMaxLength(200);
        entity.Property(e => e.EntityId).HasMaxLength(100);
        entity.Property(e => e.Summary).HasMaxLength(1000);

        entity.HasIndex(e => e.CreatedAt);
        entity.HasIndex(e => e.UserId);
        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => new { e.Controller, e.Action });
    }
}
