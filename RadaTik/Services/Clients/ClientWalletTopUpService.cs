using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services;

namespace RadaTik.Services.Clients;

public sealed class ClientWalletTopUpService(
    ApplicationDbContext context,
    IRequestNotificationService requestNotificationService)
    : ApplicationServiceBase(context), IClientWalletTopUpService
{
    public async Task<ClientWalletTopUpOutcome> TopUpAsync(ClientWalletTopUpCommand command, CancellationToken ct = default)
    {
        if (command.Amount < 0.01m)
        {
            return ClientWalletTopUpOutcome.Fail("المبلغ يجب أن يكون أكبر من صفر.");
        }

        Client? client = await Db.Clients
            .Include(c => c.Network)
            .FirstOrDefaultAsync(c => c.Id == command.ClientId, ct);
        if (client == null)
        {
            return ClientWalletTopUpOutcome.NotFoundClient();
        }

        if (command.SourceType == ClientTopUpSource.NetworkManager)
        {
            if (!command.ActorNetworkId.HasValue || client.NetworkId != command.ActorNetworkId.Value)
            {
                return ClientWalletTopUpOutcome.Fail("لا يمكن تغذية رصيد عميل من شبكة أخرى.");
            }
        }

        await using IDbContextTransaction tx = await Db.Database.BeginTransactionAsync(ct);
        try
        {
            decimal prevBalance = client.Balance;
            client.Balance += command.Amount;
            client.LastUpdated = DateTime.Now;

            if (command.SourceType == ClientTopUpSource.NetworkManager && client.NetworkId.HasValue)
            {
                Network? network = await Db.Networks.FindAsync([client.NetworkId.Value], ct);
                if (network == null)
                {
                    await tx.RollbackAsync(ct);
                    return ClientWalletTopUpOutcome.Fail("لم يتم العثور على الشبكة.");
                }

                if (network.Balance < command.Amount)
                {
                    await tx.RollbackAsync(ct);
                    return ClientWalletTopUpOutcome.Fail(
                        $"رصيد الشبكة غير كافٍ. الرصيد الحالي: {network.Balance:N0} ل.س");
                }

                decimal prevNetworkBalance = network.Balance;
                network.Balance -= command.Amount;

                Db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
                {
                    NetworkId = network.Id,
                    Type = NetworkWalletTransactionType.Adjustment,
                    SignedAmount = -command.Amount,
                    PreviousBalance = prevNetworkBalance,
                    NewBalance = network.Balance,
                    CreatedByUserId = command.ActorUserId,
                    CreatedAt = DateTime.Now,
                    Notes = $"تغذية رصيد عميل #{client.Id} ({client.UserName})"
                });
            }

            Db.ClientTopUpTransactions.Add(new ClientTopUpTransaction
            {
                ClientId = client.Id,
                Amount = command.Amount,
                PreviousBalance = prevBalance,
                NewBalance = client.Balance,
                SourceType = command.SourceType,
                CreatedByUserId = command.ActorUserId,
                Notes = command.Notes?.Trim(),
                NetworkId = command.SourceType == ClientTopUpSource.NetworkManager ? client.NetworkId : null
            });

            await Db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            string sourceName = command.SourceType == ClientTopUpSource.SystemAdmin ? "مدير النظام" : "مدير الشبكة";
            await requestNotificationService.NotifyClientTopUpSubmittedAsync(
                client.Id,
                client.NetworkId,
                command.Amount,
                sourceName,
                command.ActorDisplayName);

            return ClientWalletTopUpOutcome.Success(
                $"تم تغذية رصيد العميل بمبلغ {command.Amount:N0} ل.س من {sourceName}. الرصيد الحالي: {client.Balance:N0} ل.س");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<BulkClientWalletTopUpOutcome> BulkTopUpAsync(
        BulkClientWalletTopUpCommand command,
        CancellationToken ct = default)
    {
        if (command.Mode == BulkTopUpMode.Fixed)
        {
            if (command.Value < 0.01m)
            {
                return BulkClientWalletTopUpOutcome.Fail("المبلغ الثابت يجب أن يكون أكبر من صفر.");
            }
        }
        else if (command.Mode == BulkTopUpMode.PercentOfPackage)
        {
            if (command.Value < 0.01m || command.Value > 100m)
            {
                return BulkClientWalletTopUpOutcome.Fail("النسبة المئوية يجب أن تكون بين 0.01 و 100.");
            }
        }
        else
        {
            return BulkClientWalletTopUpOutcome.Fail("نوع الشحن غير مدعوم.");
        }

        if (!command.ApplyToAll && (command.ClientIds == null || command.ClientIds.Count == 0))
        {
            return BulkClientWalletTopUpOutcome.Fail("لم يتم تحديد أي مشترك للشحن.");
        }

        IQueryable<Client> query = Db.Clients
            .AsNoTracking()
            .Include(c => c.Profile)
            .Where(c => c.NetworkId == command.NetworkId);

        if (!command.ApplyToAll)
        {
            HashSet<int> ids = command.ClientIds!.ToHashSet();
            query = query.Where(c => ids.Contains(c.Id));
        }

        List<Client> clients = await query.OrderBy(c => c.Id).ToListAsync(ct);
        if (clients.Count == 0)
        {
            return BulkClientWalletTopUpOutcome.Fail("لا يوجد مشتركين مطابقين في الشبكة الحالية.");
        }

        List<(int ClientId, string Label, decimal Amount)> planned = [];
        List<string> errors = [];
        int skipped = 0;

        foreach (Client client in clients)
        {
            string label = client.UserName ?? client.Name ?? $"#{client.Id}";
            decimal? amount = ResolveAmount(client, command.Mode, command.Value, out string? skipReason);
            if (amount == null)
            {
                skipped++;
                if (!string.IsNullOrWhiteSpace(skipReason))
                {
                    errors.Add($"{label}: {skipReason}");
                }

                continue;
            }

            planned.Add((client.Id, label, amount.Value));
        }

        if (planned.Count == 0)
        {
            return BulkClientWalletTopUpOutcome.Fail(
                skipped > 0
                    ? $"تعذر شحن أي مشترك. تم تخطي {skipped}. " + string.Join(" | ", errors.Take(5))
                    : "لا يوجد مشتركين قابلين للشحن.");
        }

        decimal totalRequired = planned.Sum(p => p.Amount);

        if (command.SourceType == ClientTopUpSource.NetworkManager)
        {
            if (!command.ActorNetworkId.HasValue || command.ActorNetworkId.Value != command.NetworkId)
            {
                return BulkClientWalletTopUpOutcome.Fail("لا يمكن تغذية أرصدة من شبكة أخرى.");
            }

            Network? network = await Db.Networks.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == command.NetworkId, ct);
            if (network == null)
            {
                return BulkClientWalletTopUpOutcome.Fail("لم يتم العثور على الشبكة.");
            }

            if (network.Balance < totalRequired)
            {
                return BulkClientWalletTopUpOutcome.Fail(
                    $"رصيد الشبكة غير كافٍ للشحن الجماعي. المطلوب {totalRequired:N0} ل.س، الرصيد الحالي {network.Balance:N0} ل.س.");
            }
        }

        int succeeded = 0;
        int failed = 0;
        decimal totalCredited = 0m;
        string? notes = string.IsNullOrWhiteSpace(command.Notes)
            ? "شحن رصيد جماعي"
            : command.Notes.Trim();

        foreach ((int clientId, string label, decimal amount) in planned)
        {
            try
            {
                ClientWalletTopUpOutcome outcome = await TopUpAsync(
                    new ClientWalletTopUpCommand
                    {
                        ClientId = clientId,
                        Amount = amount,
                        ActorUserId = command.ActorUserId,
                        SourceType = command.SourceType,
                        ActorNetworkId = command.ActorNetworkId,
                        Notes = notes,
                        ActorDisplayName = command.ActorDisplayName
                    },
                    ct);

                if (outcome.IsSuccess)
                {
                    succeeded++;
                    totalCredited += amount;
                }
                else
                {
                    failed++;
                    errors.Add($"{label}: {outcome.ErrorMessage ?? "فشل الشحن"}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{label}: {ex.Message}");
            }
        }

        string message =
            $"تم شحن {succeeded} مشترك بمبلغ إجمالي {totalCredited:N0} ل.س" +
            (skipped > 0 ? $"، تخطي {skipped}" : "") +
            (failed > 0 ? $"، فشل {failed}" : "") +
            ".";

        if (errors.Count > 0)
        {
            message += " " + string.Join(" | ", errors.Take(8));
        }

        return BulkClientWalletTopUpOutcome.Ok(
            message,
            clients.Count,
            succeeded,
            skipped,
            failed,
            totalCredited,
            errors);
    }

    private static decimal? ResolveAmount(
        Client client,
        BulkTopUpMode mode,
        decimal value,
        out string? skipReason)
    {
        skipReason = null;

        if (mode == BulkTopUpMode.Fixed)
        {
            decimal fixedAmount = WalletMath.CeilSyp(value);
            if (fixedAmount < 0.01m)
            {
                skipReason = "المبلغ بعد التقريب غير صالح.";
                return null;
            }

            return fixedAmount;
        }

        decimal packagePrice = client.Profile?.Price ?? 0m;
        if (packagePrice <= 0m)
        {
            skipReason = "لا يوجد سعر باقة صالح للمشترك.";
            return null;
        }

        decimal percentAmount = WalletMath.CeilSyp(packagePrice * value / 100m);
        if (percentAmount < 0.01m)
        {
            skipReason = "المبلغ المحسوب من نسبة الباقة غير صالح.";
            return null;
        }

        return percentAmount;
    }
}
