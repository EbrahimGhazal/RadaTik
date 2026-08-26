using RadaTik.Models;

namespace RadaTik.Services.Clients;

/// <summary>
/// قواعد مهام التركيب في لوحة الموظف: المشترك المستورد من السيرفر تركيبه منجز مسبقاً،
/// وكذلك من أُغلقت فاتورة تركيبه الأولي (تثبيت/تحصيل).
/// </summary>
public static class ClientInstallationTaskQuery
{
    public static IQueryable<Client> WherePendingInstallation(
        this IQueryable<Client> clients,
        IQueryable<SubscriberInstallationInvoice> invoices)
    {
        return clients.Where(client =>
            !client.IsImportedFromServer
            && !invoices.Any(invoice =>
                invoice.ClientId == client.Id
                && invoice.Kind == SubscriberInstallationInvoiceKind.InitialSetup
                && (invoice.Status == SubscriberInstallationInvoiceStatus.Finalized
                    || invoice.Status == SubscriberInstallationInvoiceStatus.PartiallyPaid
                    || invoice.Status == SubscriberInstallationInvoiceStatus.Paid)));
    }
}
