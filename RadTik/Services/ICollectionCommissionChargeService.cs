namespace RadTik.Services;

public sealed class CollectionCommissionChargeResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public decimal FeeChargedSyp { get; init; }
    public bool SkippedNoPricing { get; init; }
}

public interface ICollectionCommissionChargeService
{
    /// <summary>
    /// يخصم عمولة التحصيل من محفظة الشركة بعد تسجيل عملية الدفع. يُستدعى داخل نفس معاملة قاعدة البيانات.
    /// </summary>
    Task<CollectionCommissionChargeResult> ChargeAfterPaymentRecordedAsync(
        int paymentTransactionId,
        decimal paymentAmountSyp,
        CancellationToken ct = default);
}
