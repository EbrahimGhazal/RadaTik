using RadaTik.Domain.Common;
using RadaTik.Models;

namespace RadaTik.Domain.ValueObjects;

/// <summary>قيمة نقدية غير سالبة مع عملتها (كائن قيمة — تغليف).</summary>
public readonly record struct MoneyAmount
{
    public decimal Amount { get; }
    public PricingCurrency Currency { get; }

    private MoneyAmount(decimal amount, PricingCurrency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static ServiceResult<MoneyAmount> TryCreate(decimal amount, PricingCurrency currency)
    {
        if (amount < 0.01m)
        {
            return ServiceResult<MoneyAmount>.Fail("المبلغ يجب أن يكون أكبر من صفر.");
        }

        return ServiceResult<MoneyAmount>.Ok(new MoneyAmount(amount, currency));
    }
}
