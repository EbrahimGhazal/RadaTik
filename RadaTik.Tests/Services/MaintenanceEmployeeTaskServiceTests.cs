using RadaTik.Models;
using RadaTik.Models.Business;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public class MaintenanceEmployeeTaskServiceTests
{
    [Theory]
    [InlineData(RequestPriority.Low, CompanyEmployeeTaskPriority.Low)]
    [InlineData(RequestPriority.Normal, CompanyEmployeeTaskPriority.Normal)]
    [InlineData(RequestPriority.High, CompanyEmployeeTaskPriority.High)]
    [InlineData(RequestPriority.Urgent, CompanyEmployeeTaskPriority.Urgent)]
    public void MapPriority_MapsRequestPriorityToTaskPriority(
        RequestPriority source,
        CompanyEmployeeTaskPriority expected)
    {
        Assert.Equal(expected, MaintenanceEmployeeTaskService.MapPriority(source));
    }

    [Fact]
    public void FormatEmployeeLabel_IncludesFieldTechnicianDepartment()
    {
        ApplicationUser user = new()
        {
            FullName = "أحمد",
            EmployeeDepartment = EmployeeDepartment.FieldTechnician
        };

        string label = MaintenanceEmployeeTaskService.FormatEmployeeLabel(user);

        Assert.Contains("أحمد", label, StringComparison.Ordinal);
        Assert.Contains("فني ميداني", label, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatEmployeeLabel_OmitsNoneDepartment()
    {
        ApplicationUser user = new()
        {
            FullName = "سارة",
            EmployeeDepartment = EmployeeDepartment.None
        };

        Assert.Equal("سارة", MaintenanceEmployeeTaskService.FormatEmployeeLabel(user));
    }
}
