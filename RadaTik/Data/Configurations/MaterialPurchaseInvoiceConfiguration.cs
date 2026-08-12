using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class MaterialPurchaseInvoiceConfiguration : IEntityTypeConfiguration<MaterialPurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<MaterialPurchaseInvoice> entity)
    {
        entity.ToTable("MaterialPurchaseInvoices");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Currency).HasConversion<int>().HasDefaultValue(PricingCurrency.SYP_New);
        entity.Property(e => e.SupplierName).HasMaxLength(120);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => e.InvoiceDate);
        entity.HasIndex(e => e.MoneyDiaryEntryId);
        entity.HasIndex(e => e.CashBoxWithdrawalId);

        entity.HasOne(e => e.CompanyNetwork)
          .WithMany()
          .HasForeignKey(e => e.CompanyNetworkId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByUser)
          .WithMany()
          .HasForeignKey(e => e.CreatedByUserId)
          .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.ErpSupplier)
          .WithMany(s => s.PurchaseInvoices)
          .HasForeignKey(e => e.ErpSupplierId)
          .OnDelete(DeleteBehavior.SetNull);
    }
}
