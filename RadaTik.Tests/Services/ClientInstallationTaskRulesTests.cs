using RadaTik.Models;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientInstallationTaskRulesTests
{
    [Fact]
    public void ImportedClientWithoutOpenInvoice_IsNotPendingInstallation()
    {
        DateTime today = new(2026, 8, 26);
        Assert.False(ClientInstallationTaskRules.CountsAsPendingInstallation(
            createdDate: today,
            referenceDate: today,
            hasOpenInitialSetupInvoice: false));
    }

    [Fact]
    public void NewSubscriberWithOpenInvoice_IsPendingInstallation()
    {
        DateTime today = new(2026, 8, 26);
        Assert.True(ClientInstallationTaskRules.CountsAsPendingInstallation(
            createdDate: today.AddDays(-2),
            referenceDate: today,
            hasOpenInitialSetupInvoice: true));
    }

    [Theory]
    [InlineData(SubscriberInstallationInvoiceStatus.Draft, true)]
    [InlineData(SubscriberInstallationInvoiceStatus.PendingWalletPayment, true)]
    [InlineData(SubscriberInstallationInvoiceStatus.PartiallyPaid, true)]
    [InlineData(SubscriberInstallationInvoiceStatus.Paid, false)]
    [InlineData(SubscriberInstallationInvoiceStatus.Cancelled, false)]
    [InlineData(SubscriberInstallationInvoiceStatus.Finalized, false)]
    public void OpenInitialSetupStatus_MatchesFieldVisitExpectation(
        SubscriberInstallationInvoiceStatus status,
        bool expectedOpen) =>
        Assert.Equal(expectedOpen, ClientInstallationTaskRules.IsOpenInitialSetupStatus(status));
}
