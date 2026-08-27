using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class ClientOperationsHubViewModel
{
    public int ClientId { get; init; }
    public bool IsPendingApproval { get; init; }
    public int? PendingApprovalRequestId { get; init; }

    public SubscriberInstallationInvoiceSummaryViewModel? InstallationInvoice { get; init; }
    public IReadOnlyList<ClientTimelineItemViewModel> Timeline { get; init; } = Array.Empty<ClientTimelineItemViewModel>();
}

public sealed class SubscriberInstallationInvoiceSummaryViewModel
{
    public int Id { get; init; }
    public SubscriberInstallationInvoiceStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public DateTime? FinalizedAt { get; init; }
    public bool CanFinalizeInWizard { get; init; }
    public bool CanCollectPayment { get; init; }
}

public sealed class ClientTimelineItemViewModel
{
    public DateTime At { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Detail { get; init; }
}

public sealed class ClientOperationsHubService(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    public async Task<ClientOperationsHubViewModel?> LoadAsync(
        Client client,
        IReadOnlyCollection<int> companyScopeNetworkIds,
        CancellationToken cancellationToken = default)
    {
        if (!client.NetworkId.HasValue)
        {
            return null;
        }

        bool isPending = EmployeeApprovalStates.IsPendingClientCreate(client);

        int? approvalRequestId = null;
        if (isPending)
        {
            List<NetworkServiceRequest> pending = await _context.NetworkServiceRequests
                .AsNoTracking()
                .Where(r =>
                    companyScopeNetworkIds.Contains(r.NetworkId) &&
                    r.Status == NetworkServiceRequestStatus.Pending &&
                    r.Notes != null &&
                    r.Notes.StartsWith("EMP_REQ:"))
                .OrderByDescending(r => r.RequestedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            foreach (NetworkServiceRequest request in pending)
            {
                if (EmployeeApprovalRequestHelper.TryParse(request.Notes, out EmployeeApprovalRequestKind kind, out int entityId, out _)
                    && kind == EmployeeApprovalRequestKind.ClientCreate
                    && entityId == client.Id)
                {
                    approvalRequestId = request.Id;
                    break;
                }
            }
        }

        SubscriberInstallationInvoice? invoice = await _context.SubscriberInstallationInvoices
            .AsNoTracking()
            .Where(i => i.ClientId == client.Id && i.Kind == SubscriberInstallationInvoiceKind.InitialSetup)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        SubscriberInstallationInvoiceSummaryViewModel? invoiceVm = invoice == null
            ? null
            : new SubscriberInstallationInvoiceSummaryViewModel
            {
                Id = invoice.Id,
                Status = invoice.Status,
                TotalAmount = invoice.TotalAmount,
                PaidAmount = invoice.PaidAmount,
                RemainingAmount = invoice.RemainingAmount,
                FinalizedAt = invoice.FinalizedAt,
                CanFinalizeInWizard = invoice.Status == SubscriberInstallationInvoiceStatus.Draft,
                CanCollectPayment = invoice.Status is SubscriberInstallationInvoiceStatus.Finalized
                    or SubscriberInstallationInvoiceStatus.PartiallyPaid
                    or SubscriberInstallationInvoiceStatus.PendingWalletPayment
            };

        List<ClientTimelineItemViewModel> timeline = [];
        timeline.Add(new ClientTimelineItemViewModel
        {
            At = client.CreatedDate,
            Label = "إنشاء السجل",
            Detail = client.ConnectionStatus
        });

        if (invoice != null)
        {
            timeline.Add(new ClientTimelineItemViewModel
            {
                At = invoice.CreatedAt,
                Label = "فاتورة تجهيز",
                Detail = $"#{invoice.Id} — {invoice.Status} — {invoice.TotalAmount:N0} ل.س"
            });
            if (invoice.FinalizedAt.HasValue)
            {
                timeline.Add(new ClientTimelineItemViewModel
                {
                    At = invoice.FinalizedAt.Value,
                    Label = "إصدار فاتورة التركيب",
                    Detail = null
                });
            }
        }

        if (client.LastUpdated != default)
        {
            timeline.Add(new ClientTimelineItemViewModel
            {
                At = client.LastUpdated,
                Label = "آخر تحديث",
                Detail = null
            });
        }

        return new ClientOperationsHubViewModel
        {
            ClientId = client.Id,
            IsPendingApproval = isPending,
            PendingApprovalRequestId = approvalRequestId,
            InstallationInvoice = invoiceVm,
            Timeline = timeline.OrderByDescending(t => t.At).Take(8).ToList()
        };
    }

    private async Task<int> ResolveCompanyNetworkIdAsync(int networkId, CancellationToken cancellationToken)
    {
        int? parentId = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == networkId)
            .Select(n => n.ParentNetworkId)
            .FirstOrDefaultAsync(cancellationToken);
        return parentId ?? networkId;
    }
}
