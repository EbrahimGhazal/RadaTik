using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class PayrollWithdrawalRequestConfiguration : IEntityTypeConfiguration<PayrollWithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<PayrollWithdrawalRequest> entity)
    {
        entity.ToTable("PayrollWithdrawalRequests");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.ReviewNotes).HasMaxLength(500);
        entity.Property(e => e.RequestedByUserId).HasMaxLength(450).IsRequired();
        entity.Property(e => e.ReviewedByUserId).HasMaxLength(450);
        entity.HasIndex(e => new { e.PayrollEmployeeId, e.Year, e.Month, e.Status });

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.PayrollEmployee)
            .WithMany(e => e.WithdrawalRequests)
            .HasForeignKey(e => e.PayrollEmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.PayrollTransaction)
            .WithMany()
            .HasForeignKey(e => e.PayrollTransactionId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(e => e.RequestedByUser)
            .WithMany()
            .HasForeignKey(e => e.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ReviewedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
