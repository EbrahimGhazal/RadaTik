using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.ViewModels.Admin;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class EmployeeServicePermissionMatrixTests
{
    [Fact]
    public void ClientsSection_ExposesImportFromServerAsSeparateSlot()
    {
        List<Permission> permissions =
        [
            new() { Id = 1, Key = "Clients.View", DisplayName = "عرض" },
            new() { Id = 2, Key = "Clients.Create", DisplayName = "إضافة" },
            new() { Id = 3, Key = "Clients.Edit", DisplayName = "تعديل" },
            new() { Id = 4, Key = "Clients.ImportFromServer", DisplayName = "استيراد" }
        ];

        List<EmployeeServicePermissionUiRow> rows = EmployeeServicePermissionMatrix.BuildRows(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FeatureKeys.Clients },
            permissions);

        EmployeeServicePermissionUiRow clients = Assert.Single(rows);
        Assert.Equal("العملاء", clients.Title);

        Assert.Contains(clients.Slots, s => s.Label == "إضافة" && s.PermissionIds.SequenceEqual([2]));
        Assert.Contains(clients.Slots, s =>
            s.Label == "استيراد من السيرفر" && s.PermissionIds.SequenceEqual([4]));
        Assert.DoesNotContain(clients.Slots, s =>
            s.Label == "إضافة" && s.PermissionIds.Contains(4));
    }
}
