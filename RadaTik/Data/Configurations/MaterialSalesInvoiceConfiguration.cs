using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class MaterialSalesInvoiceConfiguration : IEntityTypeConfiguration<MaterialSalesInvoice>
{
    public void Configure(EntityTypeBuilder<MaterialSalesInvoice> entity)
    {
        entity.ToTable("MaterialSalesInvoices");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Currency).HasConversion<int>().HasDefaultValue(PricingCurrency.SYP_New);
        entity.Property(e => e.CustomerName).HasMaxLength(120);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => e.InvoiceDate);
        entity.HasIndex(e => e.MoneyDiaryEntryId);
        entity.HasIndex(e => e.CashBoxDepositId);

        entity.HasOne(e => e.CompanyNetwork)
          .WithMany()
          .HasForeignKey(e => e.CompanyNetworkId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByUser)
          .WithMany()
          .HasForeignKey(e => e.CreatedByUserId)
          .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.ErpCustomer)
          .WithMany(c => c.SalesInvoices)
          .HasForeignKey(e => e.ErpCustomerId)
          .OnDelete(DeleteBehavior.SetNull);
    }
}
