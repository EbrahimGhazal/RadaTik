using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class PayrollPaymentConfiguration : IEntityTypeConfiguration<PayrollPayment>
{
    public void Configure(EntityTypeBuilder<PayrollPayment> entity)
    {
        entity.ToTable("PayrollPayments");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.BaseAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Bonus).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Deduction).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Year, e.Month });
        entity.HasIndex(e => new { e.PayrollEmployeeId, e.Year, e.Month }).IsUnique();

        entity.HasOne(e => e.CompanyNetwork)
          .WithMany()
          .HasForeignKey(e => e.CompanyNetworkId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.PayrollEmployee)
          .WithMany(emp => emp.Payments)
          .HasForeignKey(e => e.PayrollEmployeeId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.CreatedByUser)
          .WithMany()
          .HasForeignKey(e => e.CreatedByUserId)
          .OnDelete(DeleteBehavior.SetNull);
    }
}
