using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Models.Business;
using RadaTik.Security;

namespace RadaTik.Services;

public interface IMaintenanceEmployeeTaskService
{
    Task<int?> ResolveCompanyNetworkIdForClientAsync(int clientId, CancellationToken ct = default);
    Task<List<ApplicationUser>> GetAssignableEmployeesAsync(int companyNetworkId, CancellationToken ct = default);
    Task<List<SelectListItem>> GetAssignableEmployeeSelectItemsAsync(
        int companyNetworkId,
        string? selectedUserId = null,
        CancellationToken ct = default);
    Task<bool> IsAssignableEmployeeAsync(int companyNetworkId, string userId, CancellationToken ct = default);
    Task EnsureTaskForAssignedMaintenanceAsync(
        MaintenanceRequest request,
        string? assignedByUserId,
        CancellationToken ct = default);
    Task CancelLinkedOpenTaskAsync(int maintenanceRequestId, CancellationToken ct = default);
}

public sealed class MaintenanceEmployeeTaskService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IErpNotificationService erpNotifications) : IMaintenanceEmployeeTaskService
{
    public async Task<int?> ResolveCompanyNetworkIdForClientAsync(int clientId, CancellationToken ct = default)
    {
        int? networkId = await context.Clients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => c.NetworkId)
            .FirstOrDefaultAsync(ct);
        if (!networkId.HasValue)
        {
            return null;
        }

        Network? network = await context.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId.Value, ct);
        return network == null ? null : network.ParentNetworkId ?? network.Id;
    }

    public async Task<List<ApplicationUser>> GetAssignableEmployeesAsync(
        int companyNetworkId,
        CancellationToken ct = default)
    {
        List<int> networkIds = await context.Networks.AsNoTracking()
            .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
            .Select(n => n.Id)
            .ToListAsync(ct);

        HashSet<string> employeeIds = (await userManager.GetUsersInRoleAsync(RoleNames.CompanyEmployee))
            .Select(u => u.Id)
            .Concat((await userManager.GetUsersInRoleAsync(RoleNames.EmployeeLegacy)).Select(u => u.Id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return await context.Users.AsNoTracking()
            .Where(u =>
                u.IsActive
                && u.NetworkId != null
                && networkIds.Contains(u.NetworkId.Value)
                && employeeIds.Contains(u.Id))
            .OrderBy(u => u.EmployeeDepartment == EmployeeDepartment.FieldTechnician ? 0 : 1)
            .ThenBy(u => u.FullName)
            .ToListAsync(ct);
    }

    public async Task<List<SelectListItem>> GetAssignableEmployeeSelectItemsAsync(
        int companyNetworkId,
        string? selectedUserId = null,
        CancellationToken ct = default)
    {
        List<ApplicationUser> employees = await GetAssignableEmployeesAsync(companyNetworkId, ct);
        return employees.Select(u => new SelectListItem
        {
            Value = u.Id,
            Text = FormatEmployeeLabel(u),
            Selected = !string.IsNullOrWhiteSpace(selectedUserId)
                && string.Equals(u.Id, selectedUserId, StringComparison.OrdinalIgnoreCase)
        }).ToList();
    }

    public async Task<bool> IsAssignableEmployeeAsync(
        int companyNetworkId,
        string userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        List<ApplicationUser> employees = await GetAssignableEmployeesAsync(companyNetworkId, ct);
        return employees.Any(u => string.Equals(u.Id, userId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task EnsureTaskForAssignedMaintenanceAsync(
        MaintenanceRequest request,
        string? assignedByUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AssignedToId))
        {
            return;
        }

        int? companyNetworkId = request.ClientId > 0
            ? await ResolveCompanyNetworkIdForClientAsync(request.ClientId, ct)
            : null;
        if (!companyNetworkId.HasValue)
        {
            return;
        }

        if (!await IsAssignableEmployeeAsync(companyNetworkId.Value, request.AssignedToId, ct))
        {
            return;
        }

        CompanyEmployeeTask? existing = await context.CompanyEmployeeTasks
            .FirstOrDefaultAsync(t => t.MaintenanceRequestId == request.Id, ct);
        if (existing != null)
        {
            if (!string.Equals(existing.AssignedToUserId, request.AssignedToId, StringComparison.OrdinalIgnoreCase)
                && existing.Status is CompanyEmployeeTaskStatus.Pending or CompanyEmployeeTaskStatus.InProgress)
            {
                existing.AssignedToUserId = request.AssignedToId;
                existing.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(ct);
                await erpNotifications.NotifyTaskAssignedAsync(existing, ct);
            }

            return;
        }

        string clientName = request.Client?.Name
            ?? await context.Clients.AsNoTracking()
                .Where(c => c.Id == request.ClientId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct)
            ?? "مشترك";

        CompanyEmployeeTask task = new()
        {
            CompanyNetworkId = companyNetworkId.Value,
            Title = $"صيانة — {clientName}: {MaintenanceCatalog.GetDisplayName(request.Type)}",
            Description = request.Description,
            AssignedToUserId = request.AssignedToId,
            AssignedByUserId = assignedByUserId,
            ClientId = request.ClientId,
            MaintenanceRequestId = request.Id,
            Priority = MapPriority(request.Priority),
            Status = CompanyEmployeeTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.CompanyEmployeeTasks.Add(task);
        await context.SaveChangesAsync(ct);
        await erpNotifications.NotifyTaskAssignedAsync(task, ct);
    }

    public async Task CancelLinkedOpenTaskAsync(int maintenanceRequestId, CancellationToken ct = default)
    {
        CompanyEmployeeTask? task = await context.CompanyEmployeeTasks
            .FirstOrDefaultAsync(t => t.MaintenanceRequestId == maintenanceRequestId, ct);
        if (task == null
            || task.Status is CompanyEmployeeTaskStatus.Completed or CompanyEmployeeTaskStatus.Cancelled)
        {
            return;
        }

        task.Status = CompanyEmployeeTaskStatus.Cancelled;
        task.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
    }

    public static CompanyEmployeeTaskPriority MapPriority(RequestPriority priority) => priority switch
    {
        RequestPriority.Low => CompanyEmployeeTaskPriority.Low,
        RequestPriority.High => CompanyEmployeeTaskPriority.High,
        RequestPriority.Urgent => CompanyEmployeeTaskPriority.Urgent,
        _ => CompanyEmployeeTaskPriority.Normal
    };

    public static string FormatEmployeeLabel(ApplicationUser user)
    {
        string name = user.FullName ?? user.UserName ?? user.Id;
        if (user.EmployeeDepartment is EmployeeDepartment.None or EmployeeDepartment.Custom)
        {
            return name;
        }

        return $"{name} — {EmployeeDepartmentTemplates.GetDisplayName(user.EmployeeDepartment)}";
    }
}
