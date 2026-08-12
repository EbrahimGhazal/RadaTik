using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RadaTik.Data.Infrastructure;

/// <summary>تفعيل RowVersion لكيانات الأرصدة الحرجة.</summary>
public static class BalanceConcurrencyExtensions
{
    public static void ConfigureBalanceRowVersion<TEntity>(this EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        entity.Property<byte[]>("RowVersion").IsRowVersion();
    }
}
