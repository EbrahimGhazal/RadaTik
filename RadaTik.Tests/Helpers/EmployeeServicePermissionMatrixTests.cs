using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.ViewModels.Admin;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class EmployeeServicePermissionMatrixTests
{
    [Fact]
    public void ClientsSection_ExposesImportFromServerAsSeparateSpecial()
    {
        List<Permission> permissions =
        [
            new() { Id = 1, Key = "Clients.View", DisplayName = "عرض" },
            new() { Id = 2, Key = "Clients.Create", DisplayName = "إضافة" },
            new() { Id = 3, Key = "Clients.Edit", DisplayName = "تعديل" },
            new() { Id = 4, Key = "Clients.ImportFromServer", DisplayName = "استيراد" },
            new() { Id = 5, Key = "Clients.Delete", DisplayName = "حذف" }
        ];

        List<EmployeeServicePermissionUiRow> rows = EmployeeServicePermissionMatrix.BuildRows(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FeatureKeys.Clients },
            permissions);

        EmployeeServicePermissionUiRow clients = Assert.Single(rows);
        Assert.Equal("العملاء", clients.Title);
        Assert.Equal(4, clients.CrudCells.Count);

        Assert.Contains(clients.CrudCells, c =>
            c.Action == EmployeePermissionCrudAction.Create && c.PermissionIds.SequenceEqual([2]));
        Assert.Contains(clients.CrudCells, c =>
            c.Action == EmployeePermissionCrudAction.Delete && c.PermissionIds.SequenceEqual([5]));
        Assert.Contains(clients.SpecialSlots, s =>
            s.Label == "استيراد من السيرفر" && s.PermissionIds.SequenceEqual([4]));
        Assert.DoesNotContain(clients.CrudCells, c => c.PermissionIds.Contains(4));
    }

    [Fact]
    public void BuildRows_OnlyIncludesEnabledFeatures_AndKeepsUnavailableCrudCells()
    {
        List<Permission> permissions =
        [
            new() { Id = 10, Key = "Warehouse.View", DisplayName = "عرض" },
            new() { Id = 11, Key = "Warehouse.Manage", DisplayName = "إدارة" },
            new() { Id = 12, Key = "WarehouseStocktake.Manage", DisplayName = "جرد" }
        ];

        List<EmployeeServicePermissionUiRow> rows = EmployeeServicePermissionMatrix.BuildRows(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FeatureKeys.Warehouse },
            permissions);

        Assert.Equal(2, rows.Count);
        EmployeeServicePermissionUiRow warehouse = Assert.Single(rows, r => r.Title == "المستودع");
        Assert.True(warehouse.CrudCells[(int)EmployeePermissionCrudAction.View].IsAvailable);
        Assert.False(warehouse.CrudCells[(int)EmployeePermissionCrudAction.Create].IsAvailable);
        Assert.False(warehouse.CrudCells[(int)EmployeePermissionCrudAction.Edit].IsAvailable);
        Assert.False(warehouse.CrudCells[(int)EmployeePermissionCrudAction.Delete].IsAvailable);
        Assert.Contains(warehouse.SpecialSlots, s => s.Label == "إدارة مخزون" && s.PermissionIds.SequenceEqual([11]));

        EmployeeServicePermissionUiRow stocktake = Assert.Single(rows, r => r.Title == "جرد المستودع");
        Assert.All(stocktake.CrudCells, c => Assert.False(c.IsAvailable));
        Assert.Contains(stocktake.SpecialSlots, s => s.PermissionIds.SequenceEqual([12]));
    }

    [Fact]
    public void GetAllowedPermissionIds_IncludesCrudAndSpecials()
    {
        List<Permission> permissions =
        [
            new() { Id = 1, Key = "Sectors.View", DisplayName = "عرض" },
            new() { Id = 2, Key = "Sectors.Create", DisplayName = "إضافة" },
            new() { Id = 3, Key = "Sectors.Edit", DisplayName = "تعديل" },
            new() { Id = 4, Key = "Sectors.Delete", DisplayName = "حذف" },
            new() { Id = 99, Key = "Clients.View", DisplayName = "عرض عملاء" }
        ];

        HashSet<int> allowed = EmployeeServicePermissionMatrix.GetAllowedPermissionIds(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FeatureKeys.Sectors },
            permissions);

        Assert.Equal(new HashSet<int> { 1, 2, 3, 4 }, allowed);
        Assert.DoesNotContain(99, allowed);
    }

    [Fact]
    public void RequestsSection_ExposesMaintenanceSpecials()
    {
        List<Permission> permissions =
        [
            new() { Id = 1, Key = "Requests.View", DisplayName = "عرض" },
            new() { Id = 2, Key = "MaintenanceRequests.Manage", DisplayName = "صيانة" },
            new() { Id = 3, Key = "MaintenanceInvoices.View", DisplayName = "فواتير" },
            new() { Id = 4, Key = "MaintenanceInvoices.Pay", DisplayName = "تسديد" }
        ];

        List<EmployeeServicePermissionUiRow> rows = EmployeeServicePermissionMatrix.BuildRows(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FeatureKeys.Requests },
            permissions);

        EmployeeServicePermissionUiRow requests = Assert.Single(rows);
        Assert.True(requests.CrudCells[(int)EmployeePermissionCrudAction.View].IsAvailable);
        Assert.False(requests.CrudCells[(int)EmployeePermissionCrudAction.Create].IsAvailable);
        Assert.Equal(3, requests.SpecialSlots.Count);
    }
}
