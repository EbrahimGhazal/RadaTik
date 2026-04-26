using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> entity)
    {
        // IMPORTANT: avoid collision with an existing legacy table named "UserNotifications"
        entity.ToTable("AppUserNotifications");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
        entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
        entity.Property(e => e.Type).HasConversion<int>();
        entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.IsRead).HasDefaultValue(false);
        entity.HasIndex(e => e.Key).IsUnique();
        entity.HasIndex(e => e.UserId);
        entity.HasIndex(e => e.IsRead);
        entity.HasIndex(e => e.CreatedAt);
    }
}
