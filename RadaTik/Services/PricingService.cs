using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Services
{
    /// <summary>
    /// خدمة التسعيرات: تُحمّل من قاعدة البيانات وتُطبَّق على واجهات مدراء الشركات.
    /// </summary>
    public interface IPricingService
    {
        Task<IReadOnlyList<SystemPricingItem>> GetAllPricingItemsAsync(CancellationToken cancellationToken = default);
    }

    public class PricingService : IPricingService
    {
        private readonly ApplicationDbContext _context;

        public PricingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<SystemPricingItem>> GetAllPricingItemsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SystemPricingItems
                .OrderBy(p => p.ItemType)
                .ToListAsync(cancellationToken);
        }
    }
}
