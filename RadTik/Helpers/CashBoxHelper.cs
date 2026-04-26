using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;

namespace RadTik.Helpers
{
    /// <summary>مساعد الحصول على أو إنشاء الصندوق النقدي حسب نوع المالك</summary>
    public static class CashBoxHelper
    {
        public static async Task<CashBox?> GetOrCreateCashBoxAsync(
            ApplicationDbContext context,
            CashBoxOwnerType ownerType,
            int ownerId)
        {
            var box = await context.CashBoxes
                .FirstOrDefaultAsync(c => c.OwnerType == ownerType && c.OwnerId == ownerId);
            if (box != null) return box;

            box = new CashBox
            {
                OwnerType = ownerType,
                OwnerId = ownerId,
                Balance = 0m,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            context.CashBoxes.Add(box);
            await context.SaveChangesAsync();
            return box;
        }
    }
}
