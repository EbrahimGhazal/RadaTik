namespace RadaTik.Services.Clients;

public sealed class ClientOperationOutcome
{
    public bool IsSuccess { get; init; }
    public bool NotFound { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }

    public static ClientOperationOutcome Success(string message) =>
        new() { IsSuccess = true, SuccessMessage = message };

    public static ClientOperationOutcome Fail(string message) =>
        new() { ErrorMessage = message };

    public static ClientOperationOutcome NotFoundClient() =>
        new() { NotFound = true };
}

public interface IClientMikroTikLifecycleService
{
    Task<ClientOperationOutcome> ToggleActiveAsync(int clientId, int networkId, CancellationToken ct = default);

    Task<ClientOperationOutcome> FreezeAsync(int clientId, int networkId, CancellationToken ct = default);

    Task<ClientOperationOutcome> UnfreezeAsync(int clientId, int networkId, CancellationToken ct = default);

    Task<ClientOperationOutcome> RenewOneMonthAsync(int clientId, int networkId, CancellationToken ct = default);

    Task<ClientOperationOutcome> RenewSubscriptionAsync(
        int clientId,
        int networkId,
        DateTime? expirationDate,
        int? renewDays,
        CancellationToken ct = default);

    Task<ClientOperationOutcome> RenewTo8thNextMonthAsync(int clientId, int networkId, CancellationToken ct = default);

    Task<ClientOperationOutcome> QuickExtendAsync(int clientId, int networkId, int days, CancellationToken ct = default);

    Task<ClientOperationOutcome> SetAccountExpirationDateAsync(
        int clientId,
        int networkId,
        DateTime expirationDate,
        CancellationToken ct = default);

    Task<BulkExpirationUpdateResult> BulkSetAccountExpirationAsync(
        int networkId,
        IReadOnlyList<int>? clientIds,
        DateTime expirationDate,
        bool applyToAllInNetwork,
        CancellationToken ct = default);

    Task<ClientRenewSubscriptionPageModel?> BuildRenewSubscriptionPageAsync(
        int clientId,
        int networkId,
        CancellationToken ct = default);

    Task<ClientOperationOutcome> SyncWithMikroTikAsync(int clientId, int networkId, CancellationToken ct = default);

    /// <summary>
    /// نقل أو نسخ حسابات المشتركين إلى برج جديد.
    /// النقل يضيف الحساب على السيرفر المطلوب ثم يحذفه من البرج السابق ويحدّث قاعدة البيانات.
    /// النسخ يضيف الحساب على البرج الجديد ويربط حضوراً احتياطياً دون إنشاء صف مشترك مكرر.
    /// </summary>
    Task<BulkCopyAccountsToServerResult> BulkCopyAccountsToServerAsync(
        int networkId,
        int targetServerId,
        IReadOnlyList<int>? clientIds,
        bool applyToAllInNetwork,
        bool removeFromSource = true,
        CancellationToken ct = default);

    /// <summary>
    /// إعادة السيرفر الفعّال إلى البرج الأساسي، مع خيار حذف الحسابات الاحتياطية من MikroTik.
    /// </summary>
    Task<ClientOperationOutcome> EndFailoverAsync(
        int clientId,
        int networkId,
        bool removeStandbyAccounts = true,
        CancellationToken ct = default);
}
