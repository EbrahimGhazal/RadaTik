namespace RadaTik.Models.Business;

/// <summary>نوع حركة المخزون — لا يُخلط مع المحاسبة أو المحفظة.</summary>
public enum WarehouseMovementType
{
    /// <summary>إدخال كمية للمستودع (شراء، استلام، إرجاع).</summary>
    In = 1,

    /// <summary>إخراج كمية من المستودع (تركيب، صيانة، بيع معدات).</summary>
    Out = 2,

    /// <summary>تصحيح بعد الجرد — الكمية موجبة تزيد والسالبة تنقص.</summary>
    Adjustment = 3
}
