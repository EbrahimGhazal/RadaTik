using RadaTik.Models.Business;

namespace RadaTik.Domain.Warehouse;

/// <summary>حساب الرصيد الفعلي من حركات المستودع (منطق نقي — قابل للاختبار دون قاعدة بيانات).</summary>
public static class WarehouseStockCalculator
{
    public static decimal ComputeOnHand(IEnumerable<WarehouseMovement> movements) =>
        ComputeOnHand(movements, asOfDate: null);

    public static decimal ComputeOnHand(IEnumerable<WarehouseMovement> movements, DateTime? asOfDate)
    {
        IEnumerable<WarehouseMovement> filtered = movements;
        if (asOfDate.HasValue)
        {
            DateTime end = asOfDate.Value.Date;
            filtered = movements.Where(m => m.MovementDate.Date <= end);
        }

        decimal total = 0m;
        foreach (WarehouseMovement m in filtered)
        {
            total += m.MovementType switch
            {
                WarehouseMovementType.In => m.Quantity,
                WarehouseMovementType.Out => -m.Quantity,
                WarehouseMovementType.Adjustment => m.Quantity,
                _ => 0m
            };
        }

        return total;
    }
}
