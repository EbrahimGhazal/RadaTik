using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => new { e.UserId, e.PermissionId }).IsUnique();

        entity.HasOne(e => e.User)
              .WithMany()
              .HasForeignKey(e => e.UserId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Permission)
              .WithMany()
              .HasForeignKey(e => e.PermissionId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
