using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CustomServiceItemConfiguration : IEntityTypeConfiguration<CustomServiceItem>
{
    public void Configure(EntityTypeBuilder<CustomServiceItem> entity)
    {
        entity.ToTable("CustomServiceItems");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ServiceKey).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Body).HasMaxLength(2000);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
        entity.HasIndex(e => new { e.NetworkId, e.ServiceKey });
        entity.HasIndex(e => e.CreatedAt);
    }
}
