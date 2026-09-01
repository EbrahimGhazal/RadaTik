using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ClientsIndexColumnPickerTests
{
    [Fact]
    public void CompanyAdminClientsIndex_ReappliesDisplayAfterDataTablesVisibility()
    {
        string text = File.ReadAllText(FindView("Areas", "CompanyAdmin", "Views", "Clients", "Index.cshtml"));
        Assert.Contains("applyInlineColumnDisplay", text);
        Assert.Contains("columns.adjust().draw(false)", text);
        Assert.Contains("column-toggle", text);
    }

    [Fact]
    public void CompanyEmployeeClientsIndex_ReappliesDisplayAfterDataTablesVisibility()
    {
        string text = File.ReadAllText(FindView("Areas", "CompanyEmployee", "Views", "Clients", "Index.cshtml"));
        Assert.Contains("applyInlineColumnDisplay", text);
        Assert.Contains("columns.adjust().draw(false)", text);
        Assert.Contains("column-toggle", text);
        Assert.Contains("clients-table-counts", text);
        Assert.Contains("clientsVisibleCount", text);
        Assert.Contains("clients.employee.pageLength", text);
        Assert.Contains("CLIENTS_PAGE_LENGTH_DEFAULT = -1", text);
        Assert.Contains("readSavedClientsPageLength", text);
        Assert.Contains("length.dt", text);
    }

    [Fact]
    public void CompanyAdminClientsIndex_MobileSearchHidesRowsWithImportantClass()
    {
        string view = File.ReadAllText(FindView("Areas", "CompanyAdmin", "Views", "Clients", "Index.cshtml"));
        Assert.Contains("clientsMobileSearchInput", view);
        Assert.Contains("rowMatchesCardSearch", view);
        Assert.Contains("setClientRowCardVisible", view);
        Assert.Contains("is-card-hidden", view);
        Assert.Contains("normalizeClientSearchText", view);
        Assert.Contains("rowMatchesStatusFilter(rowNode, currentFilter) && rowMatchesCardSearch(rowNode)", view);
        Assert.Contains("clients-bulk-actions", view);
        Assert.Contains("clients-select-hit", view);
        Assert.Contains("clients-index-cards.css", view);
        Assert.Contains("clients.admin.pageLength", view);
        Assert.Contains("CLIENTS_PAGE_LENGTH_DEFAULT = -1", view);
        Assert.Contains("readSavedClientsPageLength", view);
        Assert.Contains("btnOpenBulkDeleteSelected", view);
        Assert.Contains("bulkDeleteSelectedModal", view);
        Assert.Contains("clients-table-counts", view);
        Assert.Contains("clientsVisibleCount", view);
        Assert.Contains("clientsSelectedCountChip", view);
        Assert.Contains("للتأكيد اكتب كلمة", view);
        Assert.Contains("BulkDeleteSelectedJson", view);
    }

    [Fact]
    public void ClientsIndexCardsCss_HidesFilteredRowsAndKeepsCheckboxBesideName()
    {
        string css = File.ReadAllText(FindView("wwwroot", "css", "clients-index-cards.css"));
        Assert.Contains("tr.client-row.is-card-hidden", css);
        Assert.Contains("display: none !important", css);
        Assert.Contains("td.clients-select-cell", css);
        Assert.Contains("grid-template-columns: minmax(0, 1fr) 2.5rem", css);
        Assert.Contains("clients-bulk-actions", css);
        Assert.DoesNotContain("content: \"تحديد\";", css);
        Assert.DoesNotContain("position: absolute !important", css);
    }

    [Fact]
    public void EmbeddedClientsCardsCss_DoesNotOverlayCheckboxOnName()
    {
        string css = File.ReadAllText(FindView("wwwroot", "css", "radtk-embedded-pages.css"));
        Assert.Contains("tr.client-row.is-card-hidden", css);
        Assert.Contains("display: none !important", css);
        Assert.Contains("td.clients-select-cell", css);
        Assert.DoesNotContain("content: \"تحديد\";", css);
    }

    private static string FindView(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(new[] { dir, "RadaTik" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException("لم يتم العثور على عرض قائمة المشتركين: " + Path.Combine(relativeParts));
    }
}
