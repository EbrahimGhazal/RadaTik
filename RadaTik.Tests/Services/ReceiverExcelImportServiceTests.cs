using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Receivers;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ReceiverExcelImportServiceTests
{
    [Fact]
    public async Task BuildExportWorkbookAsync_WritesRequiredHeadersAndReceiverRows()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedNetwork(db);
        ReceiverExcelImportService sut = new(db, NullLogger<ReceiverExcelImportService>.Instance);

        byte[] bytes = await sut.BuildExportWorkbookAsync(networkId: 1);

        using XLWorkbook wb = new(new MemoryStream(bytes));
        IXLWorksheet ws = wb.Worksheet("مستقبلات");
        Assert.Equal("اسم المستقبل", ws.Cell(1, 1).GetString());
        Assert.Equal("اسم المرسل", ws.Cell(1, 2).GetString());
        Assert.Equal("عنوان IP", ws.Cell(1, 3).GetString());
        Assert.Equal("خط الطول", ws.Cell(1, 4).GetString());
        Assert.Equal("خط العرض", ws.Cell(1, 5).GetString());
        Assert.Equal("ارتفاع الهوائي", ws.Cell(1, 6).GetString());
        Assert.Equal("لاقط الشمال", ws.Cell(2, 1).GetString());
        Assert.Equal("قطاع الشمال", ws.Cell(2, 2).GetString());
        Assert.Equal("10.1.1.20", ws.Cell(2, 3).GetString());
        Assert.Equal(36.29, ws.Cell(2, 4).GetDouble(), 3);
        Assert.Equal(33.51, ws.Cell(2, 5).GetDouble(), 3);
        Assert.Equal(6, ws.Cell(2, 6).GetDouble());
    }

    [Fact]
    public async Task ParseAsync_AcceptsExportedWorkbook()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedNetwork(db);
        ReceiverExcelImportService sut = new(db, NullLogger<ReceiverExcelImportService>.Instance);
        byte[] exported = await sut.BuildExportWorkbookAsync(1);

        await using ApplicationDbContext emptyDb = CreateDb();
        emptyDb.Networks.Add(new Network { Id = 1, Name = "Net" });
        emptyDb.Sectors.Add(new Sector
        {
            Id = 11,
            Name = "قطاع الشمال",
            IPAddress = "10.1.1.10",
            NetworkMask = "255.255.255.0",
            Latitude = 33.51,
            Longitude = 36.29,
            NetworkId = 1,
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        await emptyDb.SaveChangesAsync();
        ReceiverExcelImportService importer = new(emptyDb, NullLogger<ReceiverExcelImportService>.Instance);

        await using MemoryStream stream = new(exported);
        ReceiverExcelImportParseResult parsed = await importer.ParseAsync(stream, "المستقبلات.xlsx", 1);

        Assert.True(parsed.Success);
        Assert.Equal(1, parsed.ImportableCount);
        Assert.Equal("لاقط الشمال", parsed.ReceiversToAdd[0].Name);
        Assert.Equal(11, parsed.ReceiversToAdd[0].SectorId);
        Assert.Equal("10.1.1.20", parsed.ReceiversToAdd[0].IPAddress);
        Assert.Equal("255.255.255.0", parsed.ReceiversToAdd[0].NetworkMask);
        Assert.Equal(6, parsed.ReceiversToAdd[0].AntennaHeightAglMeters);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedNetwork(ApplicationDbContext db)
    {
        db.Networks.Add(new Network { Id = 1, Name = "Net" });
        db.Sectors.Add(new Sector
        {
            Id = 11,
            Name = "قطاع الشمال",
            IPAddress = "10.1.1.10",
            NetworkMask = "255.255.255.0",
            Latitude = 33.51,
            Longitude = 36.29,
            NetworkId = 1,
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        db.Receivers.Add(new Receiver
        {
            Id = 21,
            Name = "لاقط الشمال",
            IPAddress = "10.1.1.20",
            NetworkMask = "255.255.255.0",
            Latitude = 33.51,
            Longitude = 36.29,
            AntennaHeightAglMeters = 6,
            SectorId = 11,
            NetworkId = 1,
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        db.SaveChanges();
    }
}
