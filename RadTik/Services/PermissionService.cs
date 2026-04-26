using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using System.Security.Claims;

namespace RadTik.Services
{
    /// <summary>
    /// خدمة التحقق من الصلاحيات (Permissions) للمستخدمين.
    /// </summary>
    public class PermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        // كاش بسيط داخل الطلب لتقليل الاستعلامات المتكررة
        private readonly Dictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

        public PermissionService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                return false;
            }

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            // مدير النظام/مدير الشركة: صلاحيات كاملة
            if (principal.IsInRole(RoleNames.SystemAdministrator) || principal.IsInRole(RoleNames.NetworkAdministrator))
            {
                return true;
            }

            // حالياً: الصلاحيات التفصيلية مخصصة للموظفين
            // نعتبر EmployeeLegacy = CompanyEmployee للتوافق
            if (!(principal.IsInRole(RoleNames.CompanyEmployee) || principal.IsInRole(RoleNames.EmployeeLegacy)))
            {
                return false;
            }

            // سياسة تشغيلية حالية:
            // - تجميد المخدمات (MikroTik) بالكامل للموظفين.
            // - إبقاء طلبات الصيانة فقط، وتعطيل تدفقات تغيير السرعة حالياً.
            if (permissionKey.StartsWith("MikroTikServers.", StringComparison.OrdinalIgnoreCase) ||
                permissionKey.StartsWith("SpeedChange.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var userId = _userManager.GetUserId(principal);
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var cacheKey = $"{userId}::{permissionKey}";
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var has = await _context.UserPermissions
                    .Include(up => up.Permission)
                    .AnyAsync(up => up.UserId == userId && up.Permission != null && up.Permission.Key == permissionKey);

                if (!has &&
                    permissionKey.StartsWith("MikroTikServers.", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(permissionKey, "MikroTikServers.Manage", StringComparison.OrdinalIgnoreCase))
                {
                    // توافق: صلاحية Manage القديمة تشمل عرض/إضافة/تعديل/حذف الخادم
                    has = await _context.UserPermissions
                        .Include(up => up.Permission)
                        .AnyAsync(up => up.UserId == userId && up.Permission != null &&
                                         up.Permission.Key == "MikroTikServers.Manage");
                }

                if (!has && string.Equals(permissionKey, "Requests.View", StringComparison.OrdinalIgnoreCase))
                {
                    // توافق: من لديه تعديل طلبات سابقاً دون صلاحية عرض صريحة
                    var requestKeys = new[] { "MaintenanceRequests.Manage" };
                    has = await _context.UserPermissions
                        .Include(up => up.Permission)
                        .AnyAsync(up => up.UserId == userId && up.Permission != null &&
                                         requestKeys.Contains(up.Permission.Key));
                }

                _cache[cacheKey] = has;
                return has;
            }
            catch
            {
                // في حال لم تكن migrations مطبقة بعد أو حدث خطأ في الاستعلام
                _cache[cacheKey] = false;
                return false;
            }
        }

        public async Task<List<Permission>> GetAllPermissionsAsync()
        {
            return await _context.Permissions
                .OrderBy(p => p.Category)
                .ThenBy(p => p.DisplayName)
                .ToListAsync();
        }

        public async Task<HashSet<string>> GetUserPermissionKeysAsync(string userId)
        {
            var keys = await _context.UserPermissions
                .Include(up => up.Permission)
                .Where(up => up.UserId == userId)
                .Select(up => up.Permission!.Key)
                .ToListAsync();

            return keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}

