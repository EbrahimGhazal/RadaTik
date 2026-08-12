using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class PayrollSalaryRevisionConfiguration : IEntityTypeConfiguration<PayrollSalaryRevision>
{
    public void Configure(EntityTypeBuilder<PayrollSalaryRevision> entity)
    {
        entity.ToTable("PayrollSalaryRevisions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.PreviousSalary).HasColumnType("decimal(18,2)");
        entity.Property(e => e.NewSalary).HasColumnType("decimal(18,2)");
        entity.Property(e => e.AdjustmentValue).HasColumnType("decimal(18,4)");
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.PayrollEmployeeId);

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.PayrollEmployee)
            .WithMany(emp => emp.SalaryRevisions)
            .HasForeignKey(e => e.PayrollEmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
