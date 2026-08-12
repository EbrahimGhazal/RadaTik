namespace RadaTik.Areas.CompanyAdmin.ViewModels;



public sealed class SubscriberInstallationPricingRowViewModel

{

    public string MaterialKey { get; init; } = string.Empty;

    public string MaterialName { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public bool IsActive { get; init; }

    public int? DefaultWarehouseItemId { get; init; }

    public List<int> LinkedWarehouseItemIds { get; init; } = [];

    public bool RequiresWarehouse { get; init; }

    public decimal? WarehouseOnHand { get; init; }

}



public sealed class WarehouseItemOptionViewModel

{

    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? ModelNumber { get; init; }

    public string? Sku { get; init; }

    public decimal OnHand { get; init; }



    public string DisplayLabel =>

        $"{Name}{(string.IsNullOrWhiteSpace(ModelNumber) ? "" : $" — موديل {ModelNumber}")}{(string.IsNullOrWhiteSpace(Sku) ? "" : $" [{Sku}]")} (متاح: {OnHand:0.##})";

}



public sealed class SubscriberInstallationPricingPageViewModel

{

    public int NetworkId { get; init; }

    public string NetworkName { get; init; } = string.Empty;

    public List<SubscriberInstallationPricingRowViewModel> Rows { get; init; } = new();

    public List<WarehouseItemOptionViewModel> WarehouseItems { get; init; } = new();

    public int UnlinkedStockLineCount { get; init; }

    public bool IsReadyForWarehouseFinalize { get; init; }

}



public sealed class SubscriberInstallationPricingSaveRowInput

{

    public string MaterialKey { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; }

    public int? DefaultWarehouseItemId { get; set; }

    public List<int> WarehouseItemIds { get; set; } = [];

}



public sealed class SubscriberInstallationPricingSaveInput

{

    public List<SubscriberInstallationPricingSaveRowInput> Rows { get; set; } = new();

}

