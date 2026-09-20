using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class CalibrationScenarioConfiguration : IEntityTypeConfiguration<CalibrationScenario>
{
    public void Configure(EntityTypeBuilder<CalibrationScenario> entity)
    {
        entity.ToTable("CalibrationScenarios");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(240);
        entity.Property(e => e.CrewMode).HasMaxLength(16).IsRequired();
        entity.Property(e => e.AimMode).HasMaxLength(16).IsRequired();
        entity.Property(e => e.AuthMode).HasMaxLength(16).IsRequired();
        entity.Property(e => e.DisplayMode).HasMaxLength(16).IsRequired();
        entity.Property(e => e.SuccessMode).HasMaxLength(16).IsRequired();
        entity.HasIndex(e => new { e.NetworkId, e.Name });
        entity.HasIndex(e => new { e.NetworkId, e.IsDefault });
    }
}
