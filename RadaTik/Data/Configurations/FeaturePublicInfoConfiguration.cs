using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class FeaturePublicInfoConfiguration : IEntityTypeConfiguration<FeaturePublicInfo>
{
    public void Configure(EntityTypeBuilder<FeaturePublicInfo> entity)
    {
        entity.ToTable("FeaturePublicInfos");
        entity.HasKey(e => e.FeatureKey);
        entity.Property(e => e.FeatureKey).HasMaxLength(100);
        entity.Property(e => e.DetailHtml).HasColumnType("nvarchar(max)");
        entity.Property(e => e.PricingPolicyHtml).HasColumnType("nvarchar(max)");
        entity.Property(e => e.RenewalPolicyHtml).HasColumnType("nvarchar(max)");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
    }
}
