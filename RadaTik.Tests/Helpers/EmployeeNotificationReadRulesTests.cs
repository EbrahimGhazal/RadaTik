using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Models.Business;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class EmployeeNotificationReadRulesTests
{
    [Fact]
    public void TryParseErpTaskId_ParsesTaskIdFromKey()
    {
        int? id = EmployeeNotificationReadRules.TryParseErpTaskId("ErpTaskAssigned:42:user-1:abcdef");
        Assert.Equal(42, id);
    }

    [Fact]
    public void CanMarkAsRead_BlocksIncompleteTaskNotifications()
    {
        UserNotification n = new()
        {
            Type = NotificationType.ErpTaskAssigned,
            Key = "ErpTaskAssigned:7:u:g",
            Title = "مهمة",
            Message = "x",
            UserId = "u"
        };

        Assert.False(EmployeeNotificationReadRules.CanMarkAsRead(n, CompanyEmployeeTaskStatus.Pending));
        Assert.False(EmployeeNotificationReadRules.CanMarkAsRead(n, CompanyEmployeeTaskStatus.InProgress));
        Assert.True(EmployeeNotificationReadRules.CanMarkAsRead(n, CompanyEmployeeTaskStatus.Completed));
        Assert.True(EmployeeNotificationReadRules.CanMarkAsRead(n, CompanyEmployeeTaskStatus.Cancelled));
        Assert.True(EmployeeNotificationReadRules.CanMarkAsRead(n, null));
    }

    [Fact]
    public void CanMarkAsRead_AllowsNonTaskNotifications()
    {
        UserNotification n = new()
        {
            Type = NotificationType.ErpRewardPenaltyReviewed,
            Key = "ErpRewardPenaltyReviewed:1:u:g",
            Title = "مكافأة",
            Message = "x",
            UserId = "u"
        };

        Assert.True(EmployeeNotificationReadRules.CanMarkAsRead(n, CompanyEmployeeTaskStatus.Pending));
    }
}
