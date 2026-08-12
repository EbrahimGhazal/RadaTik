namespace RadaTik.Helpers;

/// <summary>
/// تقريب مبالغ المحفظة لأعلى (ل.س.ج) كما اتُفق عليه عند وجود كسور.
/// </summary>
public static class WalletMath
{
    public static decimal CeilSyp(decimal amount)
    {
        if (amount <= 0m)
        {
            return amount;
        }

        var floored = decimal.Floor(amount);
        if (amount == floored)
        {
            return amount;
        }

        return floored + 1m;
    }
}
