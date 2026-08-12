namespace RadaTik.ViewModels.Admin;

public class EmployeeDepartmentGroupUiItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = "fa-folder";
    public List<EmployeeDepartmentTemplateUiItem> Departments { get; set; } = [];
}
