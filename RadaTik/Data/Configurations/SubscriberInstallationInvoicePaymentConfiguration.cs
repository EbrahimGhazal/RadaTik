using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SubscriberInstallationInvoicePaymentConfiguration : IEntityTypeConfiguration<SubscriberInstallationInvoicePayment>
{
    public void Configure(EntityTypeBuilder<SubscriberInstallationInvoicePayment> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.ReceivedByUserId).HasMaxLength(450).IsRequired();
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.SubscriberInstallationInvoiceId);
        entity.HasIndex(e => e.PaymentTransactionId).IsUnique();
        entity.HasIndex(e => e.CreatedAt);

        entity.HasOne(e => e.SubscriberInstallationInvoice)
            .WithMany()
            .HasForeignKey(e => e.SubscriberInstallationInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.PaymentTransaction)
            .WithMany()
            .HasForeignKey(e => e.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        // NoAction: SQL Server forbids multiple cascade paths from AspNetUsers (CreatedBy on invoices is NoAction;
        // this FK must not cascade-delete from users either).
        entity.HasOne(e => e.ReceivedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReceivedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
