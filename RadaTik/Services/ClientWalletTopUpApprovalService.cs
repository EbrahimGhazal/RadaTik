using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services;

public sealed class ClientWalletTopUpApprovalResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>معالجة طلبات تغذية رصيد المشترك من البوابة (موافقة / رفض).</summary>
public class ClientWalletTopUpApprovalService(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    public async Task<ClientWalletTopUpApprovalResult> ApproveAsync(
        int requestId,
        string processorUserId,
        ClientWalletTopUpRecipientTarget expectedTarget,
        int? collectionPointAccountIdForCpTarget,
        string? adminNotes = null)
    {
        ClientWalletTopUpRequest? req = await _context.ClientWalletTopUpRequests
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (req == null)
        {
            return Fail("الطلب غير موجود.");
        }

        if (req.Status != ClientWalletTopUpRequestStatus.Pending)
        {
            return Fail("لا يمكن الموافقة على طلب غير معلّق.");
        }

        if (req.RecipientTarget != expectedTarget)
        {
            return Fail("جهة معالجة الطلب غير مطابقة.");
        }

        if (expectedTarget == ClientWalletTopUpRecipientTarget.CollectionPoint)
        {
            if (!collectionPointAccountIdForCpTarget.HasValue ||
                req.TargetCollectionPointAccountId != collectionPointAccountIdForCpTarget.Value)
            {
                return Fail("لا يمكنك معالجة هذا الطلب.");
            }
        }

        if (req.Client == null)
        {
            return Fail("تعذر العثور على المشترك.");
        }

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();
        try
        {
            DateTime now = DateTime.Now;
            Client client = req.Client;
            decimal prevBalance = client.Balance;
            client.Balance += req.Amount;
            client.LastUpdated = now;

            _context.ClientTopUpTransactions.Add(new ClientTopUpTransaction
            {
                ClientId = client.Id,
                Amount = req.Amount,
                PreviousBalance = prevBalance,
                NewBalance = client.Balance,
                SourceType = ClientTopUpSource.ClientPortalRequest,
                CreatedByUserId = processorUserId,
                CreatedAt = now,
                Notes = BuildTopUpNote(req, adminNotes),
                NetworkId = expectedTarget == ClientWalletTopUpRecipientTarget.CompanyManager ? req.NetworkId : null,
                CollectionPointAccountId = expectedTarget == ClientWalletTopUpRecipientTarget.CollectionPoint
                    ? req.TargetCollectionPointAccountId
                    : null
            });

            req.Status = ClientWalletTopUpRequestStatus.Approved;
            req.ProcessedByUserId = processorUserId;
            req.ProcessedAt = now;
            if (!string.IsNullOrWhiteSpace(adminNotes))
            {
                req.AdminNotes = adminNotes.Trim();
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return new ClientWalletTopUpApprovalResult { Success = true };
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            return Fail("حدث خطأ أثناء الموافقة على الطلب.");
        }
    }

    public async Task<ClientWalletTopUpApprovalResult> RejectAsync(
        int requestId,
        string processorUserId,
        ClientWalletTopUpRecipientTarget expectedTarget,
        int? collectionPointAccountIdForCpTarget,
        string? adminNotes = null)
    {
        ClientWalletTopUpRequest? req = await _context.ClientWalletTopUpRequests
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (req == null)
        {
            return Fail("الطلب غير موجود.");
        }

        if (req.Status != ClientWalletTopUpRequestStatus.Pending)
        {
            return Fail("لا يمكن رفض طلب غير معلّق.");
        }

        if (req.RecipientTarget != expectedTarget)
        {
            return Fail("جهة معالجة الطلب غير مطابقة.");
        }

        if (expectedTarget == ClientWalletTopUpRecipientTarget.CollectionPoint)
        {
            if (!collectionPointAccountIdForCpTarget.HasValue ||
                req.TargetCollectionPointAccountId != collectionPointAccountIdForCpTarget.Value)
            {
                return Fail("لا يمكنك معالجة هذا الطلب.");
            }
        }

        req.Status = ClientWalletTopUpRequestStatus.Rejected;
        req.ProcessedByUserId = processorUserId;
        req.ProcessedAt = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(adminNotes))
        {
            req.AdminNotes = adminNotes.Trim();
        }

        await _context.SaveChangesAsync();
        return new ClientWalletTopUpApprovalResult { Success = true };
    }

    private static string BuildTopUpNote(ClientWalletTopUpRequest req, string? adminNotes)
    {
        string baseNote = $"موافقة طلب تغذية #{req.Id}";
        if (string.IsNullOrWhiteSpace(adminNotes))
        {
            return baseNote;
        }

        return $"{baseNote} — {adminNotes.Trim()}";
    }

    private static ClientWalletTopUpApprovalResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
