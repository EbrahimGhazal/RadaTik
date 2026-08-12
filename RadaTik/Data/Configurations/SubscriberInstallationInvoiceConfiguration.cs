using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SubscriberInstallationInvoiceConfiguration : IEntityTypeConfiguration<SubscriberInstallationInvoice>
{
    public void Configure(EntityTypeBuilder<SubscriberInstallationInvoice> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.CompanyName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.ClientName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.ClientSignature).HasMaxLength(500);
        entity.Property(e => e.EmployeeSignature).HasMaxLength(500);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(450).IsRequired();
        entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.ClientId);
        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => new { e.ClientId, e.Kind });
        entity.HasIndex(e => e.CreatedAt);

        // NoAction: avoids SQL Server multiple cascade paths with SubscriberInstallationInvoicePayments
        // (AspNetUsers -> Invoices -> Payments CASCADE plus AspNetUsers -> Payments on ReceivedByUserId).
        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Network)
            .WithMany()
            .HasForeignKey(e => e.NetworkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
