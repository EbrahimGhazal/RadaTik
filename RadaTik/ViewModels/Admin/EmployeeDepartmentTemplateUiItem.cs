using RadaTik.Models;

namespace RadaTik.ViewModels.Admin;

public class EmployeeDepartmentTemplateUiItem
{
    public EmployeeDepartment Department { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "fa-user";
    public string DefaultPayrollJobTitle { get; set; } = string.Empty;
    public EmployeeDashboardFocus DashboardFocus { get; set; }
}
