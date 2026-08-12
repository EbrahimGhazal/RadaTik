using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Helpers;

public static class PricingDisplay
{
    public static string NetworkServiceRequestStatusLabel(NetworkServiceRequestStatus status) => status switch
    {
        NetworkServiceRequestStatus.Pending => "معلّق",
        NetworkServiceRequestStatus.Approved => "مقبول",
        NetworkServiceRequestStatus.Rejected => "مرفوض",
        NetworkServiceRequestStatus.Cancelled => "ملغي",
        _ => status.ToString()
    };

    public static string CollectionPointRenewalStatusLabel(CollectionPointRenewalStatus status) => status switch
    {
        CollectionPointRenewalStatus.Pending => "قيد الانتظار",
        CollectionPointRenewalStatus.Approved => "مقبول",
        CollectionPointRenewalStatus.Rejected => "مرفوض",
        _ => status.ToString()
    };

    public static string NetworkTopUpRequestStatusLabel(NetworkTopUpRequestStatus status) => status switch
    {
        NetworkTopUpRequestStatus.Pending => "معلّق",
        NetworkTopUpRequestStatus.Approved => "مقبول",
        NetworkTopUpRequestStatus.Rejected => "مرفوض",
        NetworkTopUpRequestStatus.Cancelled => "ملغي",
        _ => status.ToString()
    };

    public static string CollectionPointTopUpStatusLabel(CollectionPointTopUpStatus status) => status switch
    {
        CollectionPointTopUpStatus.Pending => "قيد الانتظار",
        CollectionPointTopUpStatus.Approved => "مقبول",
        CollectionPointTopUpStatus.Rejected => "مرفوض",
        CollectionPointTopUpStatus.Cancelled => "ملغي",
        _ => status.ToString()
    };

    public static string ClientWalletTopUpRequestStatusLabel(ClientWalletTopUpRequestStatus status) => status switch
    {
        ClientWalletTopUpRequestStatus.Pending => "قيد الانتظار",
        ClientWalletTopUpRequestStatus.Approved => "مقبول",
        ClientWalletTopUpRequestStatus.Rejected => "مرفوض",
        ClientWalletTopUpRequestStatus.Cancelled => "ملغي",
        _ => status.ToString()
    };

    public static string EmployeeWalletTopUpRequestStatusLabel(EmployeeWalletTopUpRequestStatus status) => status switch
    {
        EmployeeWalletTopUpRequestStatus.Pending => "قيد الانتظار",
        EmployeeWalletTopUpRequestStatus.Approved => "مقبول",
        EmployeeWalletTopUpRequestStatus.Rejected => "مرفوض",
        EmployeeWalletTopUpRequestStatus.Cancelled => "ملغي",
        _ => status.ToString()
    };

    public static string EmployeeWalletTopUpSourceLabel(EmployeeWalletTopUpRequestSource source) => source switch
    {
        EmployeeWalletTopUpRequestSource.EmployeeSelf => "موظف",
        EmployeeWalletTopUpRequestSource.CompanyManager => "مدير الشركة",
        _ => source.ToString()
    };

    public static string EmployeeWalletTransactionSourceLabel(EmployeeWalletTransactionSource source) => source switch
    {
        EmployeeWalletTransactionSource.TopUpRequestApproved => "موافقة طلب",
        EmployeeWalletTransactionSource.DirectTopUpByManager => "تغذية مباشرة",
        _ => source.ToString()
    };

    public static string CollectionPointTopUpTargetLabel(CollectionPointTopUpTarget target) => target switch
    {
        CollectionPointTopUpTarget.SystemAdmin => "مدير النظام",
        CollectionPointTopUpTarget.CompanyManager => "مدير الشركة",
        _ => target.ToString()
    };

    public static string ClientWalletTopUpRecipientTargetLabel(ClientWalletTopUpRecipientTarget target) => target switch
    {
        ClientWalletTopUpRecipientTarget.CompanyManager => "مدير الشركة",
        ClientWalletTopUpRecipientTarget.CollectionPoint => "نقطة تحصيل",
        _ => target.ToString()
    };

    public static string MaintenanceRequestStatusLabel(MaintenanceRequestStatus status) => status switch
    {
        MaintenanceRequestStatus.Pending => "في الانتظار",
        MaintenanceRequestStatus.Accepted => "مقبول",
        MaintenanceRequestStatus.InProgress => "قيد التنفيذ",
        MaintenanceRequestStatus.Completed => "مكتمل",
        MaintenanceRequestStatus.Rejected => "مرفوض",
        MaintenanceRequestStatus.Cancelled => "ملغي",
        _ => status.ToString()
    };

    public static string MaintenanceRequestStatusClass(MaintenanceRequestStatus status) => status switch
    {
        MaintenanceRequestStatus.Pending => "status-pending",
        MaintenanceRequestStatus.Accepted => "status-accepted",
        MaintenanceRequestStatus.InProgress => "status-progress",
        MaintenanceRequestStatus.Completed => "status-completed",
        MaintenanceRequestStatus.Rejected => "status-rejected",
        MaintenanceRequestStatus.Cancelled => "status-cancelled",
        _ => string.Empty
    };

    public static string MaintenanceRequestStatusBadgeClass(MaintenanceRequestStatus status) => status switch
    {
        MaintenanceRequestStatus.Completed => "bg-success",
        MaintenanceRequestStatus.Rejected => "bg-danger",
        MaintenanceRequestStatus.Cancelled => "bg-secondary",
        MaintenanceRequestStatus.Accepted => "bg-primary",
        MaintenanceRequestStatus.InProgress => "bg-warning text-dark",
        _ => "bg-info text-dark"
    };

    public static string SpeedChangeRequestStatusLabel(SpeedChangeRequestStatus status) => status switch
    {
        SpeedChangeRequestStatus.Pending => "في الانتظار",
        SpeedChangeRequestStatus.Approved => "تمت الموافقة",
        SpeedChangeRequestStatus.Rejected => "مرفوض",
        SpeedChangeRequestStatus.Implemented => "تم التنفيذ",
        SpeedChangeRequestStatus.Cancelled => "ملغي",
        _ => status.ToString()
    };

    public static string SpeedChangeRequestStatusClass(SpeedChangeRequestStatus status) => status switch
    {
        SpeedChangeRequestStatus.Pending => "status-pending",
        SpeedChangeRequestStatus.Approved => "status-approved",
        SpeedChangeRequestStatus.Rejected => "status-rejected",
        SpeedChangeRequestStatus.Implemented => "status-implemented",
        SpeedChangeRequestStatus.Cancelled => "status-cancelled",
        _ => string.Empty
    };

    public static string RequestPriorityLabel(RequestPriority priority) => priority switch
    {
        RequestPriority.Low => "منخفضة",
        RequestPriority.Normal => "عادية",
        RequestPriority.High => "عالية",
        RequestPriority.Urgent => "عاجلة",
        _ => priority.ToString()
    };

    public static string RequestPriorityClass(RequestPriority priority) => priority switch
    {
        RequestPriority.Low => "priority-low",
        RequestPriority.Normal => "priority-normal",
        RequestPriority.High => "priority-high",
        RequestPriority.Urgent => "priority-urgent",
        _ => string.Empty
    };

    public static string BillingPeriodLabel(PricingBillingPeriod period) => period switch
    {
        PricingBillingPeriod.OneTime => "مرة واحدة",
        PricingBillingPeriod.Daily => "يومي",
        PricingBillingPeriod.Monthly => "شهري",
        PricingBillingPeriod.Every3Months => "كل 3 أشهر",
        PricingBillingPeriod.Every6Months => "كل 6 أشهر",
        PricingBillingPeriod.Every12Months => "سنوي",
        _ => period.ToString()
    };

    public static string BillingPeriodSubjectLabel(PricingBillingPeriod period) => period switch
    {
        PricingBillingPeriod.OneTime => "مرة واحدة",
        PricingBillingPeriod.Daily => "يوم",
        PricingBillingPeriod.Monthly => "شهر",
        PricingBillingPeriod.Every3Months => "كل 3 أشهر",
        PricingBillingPeriod.Every6Months => "كل 6 أشهر",
        PricingBillingPeriod.Every12Months => "سنة",
        _ => period.ToString()
    };

    public static string ChargeUnitLabel(PricingChargeUnit chargeUnit) => chargeUnit switch
    {
        PricingChargeUnit.Flat => "مبلغ ثابت",
        PricingChargeUnit.PerNetwork => "لكل شبكة",
        PricingChargeUnit.PerUser => "لكل موظف/مستخدم",
        PricingChargeUnit.PerSubscriber => "لكل مشترك/عميل",
        PricingChargeUnit.PerSector => "لكل مرسل",
        PricingChargeUnit.PerReceiver => "لكل مستقبل",
        PricingChargeUnit.PerServer => "لكل خادم",
        PricingChargeUnit.PerCollectionPoint => "لكل نقطة تحصيل",
        PricingChargeUnit.PerSpeedProfile => "لكل بروفايل سرعة",
        PricingChargeUnit.PerRequest => "لكل طلب",
        PricingChargeUnit.PercentOfCollectedAmount => "نسبة من مبلغ التحصيل",
        PricingChargeUnit.PerReport => "لكل تقرير",
        _ => chargeUnit.ToString()
    };

    public static string ChargeUnitSubjectLabel(PricingChargeUnit chargeUnit) => chargeUnit switch
    {
        PricingChargeUnit.Flat => "شبكة",
        PricingChargeUnit.PerNetwork => "شبكة إضافية",
        PricingChargeUnit.PerUser => "موظف/مستخدم",
        PricingChargeUnit.PerSubscriber => "مشترك/عميل",
        PricingChargeUnit.PerSector => "مرسل",
        PricingChargeUnit.PerReceiver => "مستقبل",
        PricingChargeUnit.PerServer => "خادم",
        PricingChargeUnit.PerCollectionPoint => "نقطة تحصيل",
        PricingChargeUnit.PerSpeedProfile => "سرعة/بروفايل",
        PricingChargeUnit.PerRequest => "طلب",
        PricingChargeUnit.PercentOfCollectedAmount => "% من التحصيل",
        PricingChargeUnit.PerReport => "تقرير",
        _ => chargeUnit.ToString()
    };
}
