using System.ComponentModel.DataAnnotations;

namespace RadaTik.ViewModels.Receipts;

public enum ReceiptSourceType
{
    [Display(Name = "تغذية رصيد شركة")]
    CompanyTopUp = 1,
    [Display(Name = "تغذية رصيد نقطة تحصيل")]
    CollectionPointTopUp = 2,
    [Display(Name = "عملية نقطة تحصيل")]
    CollectionPointOperation = 3
}

public class ReceiptRowViewModel
{
    public ReceiptSourceType SourceType { get; set; }
    public int SourceId { get; set; }

    public DateTime ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }

    public string? PartyName { get; set; } // شركة أو نقطة تحصيل

    public string PaymentMethod { get; set; } = "—";
    public string? ReferenceNumber { get; set; }

    public decimal AmountSYP { get; set; }
    public decimal AmountUSD { get; set; }

    public string? ReceiptImagePath { get; set; }
    public string? Notes { get; set; }
}

public class ReceiptMethodSummaryViewModel
{
    public string Method { get; set; } = "—";
    public int Count { get; set; }
    public decimal TotalSYP { get; set; }
    public decimal TotalUSD { get; set; }
}

public class ReceiptsIndexViewModel
{
    public string Title { get; set; } = "سجل القبض";

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int TotalCount { get; set; }
    public decimal TotalSYP { get; set; }
    public decimal TotalUSD { get; set; }

    public List<ReceiptMethodSummaryViewModel> ByMethod { get; set; } = new();
    public List<ReceiptRowViewModel> Items { get; set; } = new();
}

