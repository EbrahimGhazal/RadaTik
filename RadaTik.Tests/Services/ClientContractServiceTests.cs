using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.Documents;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientContractServiceTests
{
    [Fact]
    public async Task BuildMembershipContractPageAsync_WhenRenewalBlocked_ReturnsBlocked()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 2, Name = "N2" });
        db.Profiles.Add(new Profile { Id = 1, Name = "P1", NetworkId = 2 });
        Client client = new()
        {
            Name = "c",
            UserName = "c",
            Password = "p",
            SID = "1",
            PhoneNumber = "0",
            ProfileId = 1,
            NetworkId = 2
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        Mock<IClientRenewalGuardService> guard = new();
        guard.Setup(g => g.CheckBlockingInvoicesAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenewalBlockResult { CanRenew = false, PendingInvoicesCount = 2 });

        ClientContractService sut = new(db, guard.Object, Mock.Of<ICompanyDocumentAppearanceService>());
        ClientMembershipContractPageResult result = await sut.BuildMembershipContractPageAsync(client.Id, 2);

        Assert.Equal(ClientContractPageStatus.RenewalBlocked, result.Status);
    }

    [Fact]
    public void ValidateSettingsSave_UnknownVariable_ReturnsInvalid()
    {
        using ApplicationDbContext db = CreateDb();
        ClientContractService sut = new(db, Mock.Of<IClientRenewalGuardService>(), Mock.Of<ICompanyDocumentAppearanceService>());
        Network network = new() { Id = 1, Name = "Net" };

        ClientContractSettingsSaveResult result = sut.ValidateSettingsSave(network, new ClientContractSettingsSaveCommand
        {
            ContractTitle = "عقد",
            ContractBodyTemplate = "نص {{BadVar}}"
        });

        Assert.False(result.IsValid);
        Assert.NotNull(result.InvalidView);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
