namespace RadaTik.Services;

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
    /// بعد تسجيل <see cref="PaymentTransaction"/>: إيداع المبلغ الإجمالي في محفظة الشركة (الشبكة الأم للمشترك)،
    /// ثم خصم عمولة المنصة من ذلك الإيداع إن وُجد تسعير نشط لـ <c>FeatureKeys.CollectionCommission</c>.
    /// يُستدعى داخل نفس معاملة قاعدة البيانات مع حفظ عملية الدفع أولاً للحصول على المعرف.
    /// </summary>
    Task<CollectionCommissionChargeResult> ChargeAfterPaymentRecordedAsync(
        int paymentTransactionId,
        decimal paymentAmountSyp,
        CancellationToken ct = default);
}
