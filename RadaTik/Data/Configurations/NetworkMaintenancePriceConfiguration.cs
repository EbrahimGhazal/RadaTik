using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class NetworkMaintenancePriceConfiguration : IEntityTypeConfiguration<NetworkMaintenancePrice>
{
    public void Configure(EntityTypeBuilder<NetworkMaintenancePrice> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.AmountSYP).HasColumnType("decimal(18,2)");
        entity.Property(e => e.UpdatedByUserId).HasMaxLength(450).IsRequired();
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => new { e.NetworkId, e.MaintenanceType }).IsUnique();
        entity.HasIndex(e => e.IsActive);
    }
}
