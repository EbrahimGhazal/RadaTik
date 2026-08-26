using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Helpers;

/// <summary>
/// قواعد تعليم تنبيهات الموظف كمقروءة — تنبيه المهمة لا يُقفل إلا بعد إنجازها.
/// </summary>
public static class EmployeeNotificationReadRules
{
    public static int? TryParseErpTaskId(string? notificationKey)
    {
        if (string.IsNullOrWhiteSpace(notificationKey))
        {
            return null;
        }

        // Key format: ErpTaskAssigned:{taskId}:{userId}:{guid}
        string[] parts = notificationKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        if (!string.Equals(parts[0], "ErpTaskAssigned", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(parts[1], out int taskId) ? taskId : null;
    }

    /// <summary>
    /// هل يُسمح بتعليم التنبيه كمقروء؟
    /// تنبيهات المهام: فقط إذا كانت المهمة مكتملة (أو ملغاة/محذوفة).
    /// باقي الأنواع: مسموح دائماً.
    /// </summary>
    public static bool CanMarkAsRead(UserNotification notification, CompanyEmployeeTaskStatus? relatedTaskStatus)
    {
        if (notification.Type != NotificationType.ErpTaskAssigned)
        {
            return true;
        }

        if (relatedTaskStatus is null)
        {
            // المهمة غير موجودة — اسمح بإغلاق التنبيه.
            return true;
        }

        return relatedTaskStatus is CompanyEmployeeTaskStatus.Completed
            or CompanyEmployeeTaskStatus.Cancelled;
    }
}
