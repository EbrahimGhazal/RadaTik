namespace RadaTik.Services.Approvals;

public interface IEmployeeServiceApprovalRequestService
{
    Task<int> CreatePendingAsync(
        int selectedNetworkId,
        string actorUserId,
        string featureKey,
        string notes,
        decimal expectedChargeAmountSyp = 0m,
        CancellationToken ct = default);
}
