using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class PayrollEmployeeConfiguration : IEntityTypeConfiguration<PayrollEmployee>
{
    public void Configure(EntityTypeBuilder<PayrollEmployee> entity)
    {
        entity.ToTable("PayrollEmployees");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.FullName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.JobTitle).HasMaxLength(80);
        entity.Property(e => e.Phone).HasMaxLength(30);
        entity.Property(e => e.ApplicationUserId).HasMaxLength(450);
        entity.Property(e => e.MonthlySalary).HasColumnType("decimal(18,2)");
        entity.Property(e => e.WeeklyWorkHours).HasColumnType("decimal(8,2)");
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => e.ApplicationUserId);

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ApplicationUser)
            .WithMany()
            .HasForeignKey(e => e.ApplicationUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
