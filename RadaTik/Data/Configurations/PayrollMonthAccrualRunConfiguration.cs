using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class PayrollMonthAccrualRunConfiguration : IEntityTypeConfiguration<PayrollMonthAccrualRun>
{
    public void Configure(EntityTypeBuilder<PayrollMonthAccrualRun> entity)
    {
        entity.ToTable("PayrollMonthAccrualRuns");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Year, e.Month }).IsUnique();

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
