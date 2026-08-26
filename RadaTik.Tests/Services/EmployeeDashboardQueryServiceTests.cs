using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class EmployeeDashboardQueryServiceTests
{
    [Fact]
    public async Task GetPendingInstallationsUntilAsync_ExcludesImportedSubscribers()
    {
        await using ApplicationDbContext db = CreateDb();
        DateTime yesterday = DateTime.Now.Date.AddDays(-1).AddHours(10);
        db.Profiles.Add(Profile());
        db.Clients.AddRange(
            LocalClient(1, yesterday),
            ImportedClient(2, yesterday));
        await db.SaveChangesAsync();

        EmployeeDashboardQueryService sut = new(db);
        List<Client> pending = await sut.GetPendingInstallationsUntilAsync(10, DateTime.Now.Date);

        Client remaining = Assert.Single(pending);
        Assert.Equal(1, remaining.Id);
        Assert.False(remaining.IsImportedFromServer);
    }

    [Fact]
    public async Task GetPendingInstallationsUntilAsync_ExcludesClientsWithCompletedInstallationInvoice()
    {
        await using ApplicationDbContext db = CreateDb();
        DateTime yesterday = DateTime.Now.Date.AddDays(-1).AddHours(9);
        db.Profiles.Add(Profile());
        db.Clients.AddRange(
            LocalClient(1, yesterday),
            LocalClient(2, yesterday));
        db.SubscriberInstallationInvoices.Add(Invoice(2, SubscriberInstallationInvoiceStatus.Paid));
        await db.SaveChangesAsync();

        EmployeeDashboardQueryService sut = new(db);
        List<Client> pending = await sut.GetPendingInstallationsUntilAsync(10, DateTime.Now.Date);

        Client remaining = Assert.Single(pending);
        Assert.Equal(1, remaining.Id);
    }

    [Fact]
    public async Task GetPendingInstallationsUntilAsync_ExcludesFinalizedInstallationInvoice()
    {
        await using ApplicationDbContext db = CreateDb();
        DateTime yesterday = DateTime.Now.Date.AddDays(-1).AddHours(7);
        db.Profiles.Add(Profile());
        db.Clients.Add(LocalClient(7, yesterday));
        db.SubscriberInstallationInvoices.Add(Invoice(7, SubscriberInstallationInvoiceStatus.Finalized));
        await db.SaveChangesAsync();

        EmployeeDashboardQueryService sut = new(db);
        List<Client> pending = await sut.GetPendingInstallationsUntilAsync(10, DateTime.Now.Date);

        Assert.Empty(pending);
    }

    [Fact]
    public async Task GetPendingInstallationsUntilAsync_KeepsDraftInvoiceAsPendingTask()
    {
        await using ApplicationDbContext db = CreateDb();
        DateTime yesterday = DateTime.Now.Date.AddDays(-1).AddHours(8);
        db.Profiles.Add(Profile());
        db.Clients.Add(LocalClient(3, yesterday));
        db.SubscriberInstallationInvoices.Add(Invoice(3, SubscriberInstallationInvoiceStatus.Draft));
        await db.SaveChangesAsync();

        EmployeeDashboardQueryService sut = new(db);
        List<Client> pending = await sut.GetPendingInstallationsUntilAsync(10, DateTime.Now.Date);

        Assert.Equal(3, Assert.Single(pending).Id);
    }

    [Fact]
    public async Task GetPendingInstallationsUntilAsync_DoesNotCountOtherNetworkClients()
    {
        await using ApplicationDbContext db = CreateDb();
        DateTime yesterday = DateTime.Now.Date.AddDays(-1);
        db.Profiles.Add(Profile());
        db.Clients.Add(LocalClient(4, yesterday, networkId: 99));
        await db.SaveChangesAsync();

        EmployeeDashboardQueryService sut = new(db);
        List<Client> pending = await sut.GetPendingInstallationsUntilAsync(10, DateTime.Now.Date);

        Assert.Empty(pending);
    }

    [Fact]
    public async Task GetPendingInstallationsOnDateAsync_ReturnsOnlyThatDay()
    {
        await using ApplicationDbContext db = CreateDb();
        DateTime today = DateTime.Now.Date.AddHours(11);
        DateTime tomorrow = DateTime.Now.Date.AddDays(1).AddHours(11);
        db.Profiles.Add(Profile());
        db.Clients.AddRange(
            LocalClient(5, today),
            LocalClient(6, tomorrow));
        await db.SaveChangesAsync();

        EmployeeDashboardQueryService sut = new(db);
        List<Client> tomorrowTasks = await sut.GetPendingInstallationsOnDateAsync(10, DateTime.Now.Date.AddDays(1));

        Assert.Equal(6, Assert.Single(tomorrowTasks).Id);
    }

    private static Profile Profile() => new()
    {
        Id = 1,
        Name = "P1",
        NetworkId = 10
    };

    private static Client LocalClient(int id, DateTime created, int networkId = 10) =>
        Client(id, created, imported: false, networkId);

    private static Client ImportedClient(int id, DateTime created) =>
        Client(id, created, imported: true, 10);

    private static Client Client(int id, DateTime created, bool imported, int networkId) => new()
    {
        Id = id,
        Name = $"c{id}",
        UserName = $"u{id}",
        Password = "p",
        SID = "1",
        PhoneNumber = "0",
        ProfileId = 1,
        NetworkId = networkId,
        CreatedDate = created,
        IsImportedFromServer = imported
    };

    private static SubscriberInstallationInvoice Invoice(
        int clientId,
        SubscriberInstallationInvoiceStatus status) => new()
    {
        ClientId = clientId,
        NetworkId = 10,
        CompanyName = "Co",
        ClientName = "c",
        Kind = SubscriberInstallationInvoiceKind.InitialSetup,
        Status = status,
        CreatedByUserId = "actor"
    };

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
