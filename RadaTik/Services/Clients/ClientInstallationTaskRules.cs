using RadaTik.Models;

namespace RadaTik.Services.Clients;

/// <summary>
/// قواعد ظهور مهام التركيب في لوحة الموظف.
/// المشترك المستورد من MikroTik ليس مهمة تركيب — التركيب منجز مسبقاً على السيرفر.
/// </summary>
public static class ClientInstallationTaskRules
{
    public static bool IsOpenInitialSetupStatus(SubscriberInstallationInvoiceStatus status) =>
        status is SubscriberInstallationInvoiceStatus.Draft
            or SubscriberInstallationInvoiceStatus.PendingWalletPayment
            or SubscriberInstallationInvoiceStatus.PartiallyPaid;

    public static bool CountsAsPendingInstallation(
        DateTime createdDate,
        DateTime referenceDate,
        bool hasOpenInitialSetupInvoice) =>
        hasOpenInitialSetupInvoice && createdDate.Date <= referenceDate.Date;

    public static bool CountsAsScheduledInstallationOn(
        DateTime createdDate,
        DateTime scheduledDate,
        bool hasOpenInitialSetupInvoice) =>
        hasOpenInitialSetupInvoice && createdDate.Date == scheduledDate.Date;
}
