using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Sectors;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class SectorExcelImportServiceTests
{
    [Fact]
    public async Task BuildExportWorkbookAsync_WritesRequiredHeadersAndSectorRows()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedNetwork(db);
        SectorExcelImportService sut = new(db, NullLogger<SectorExcelImportService>.Instance);

        byte[] bytes = await sut.BuildExportWorkbookAsync(networkId: 1);

        using XLWorkbook wb = new(new MemoryStream(bytes));
        IXLWorksheet ws = wb.Worksheet("مرسلات");
        Assert.Equal("اسم المرسل", ws.Cell(1, 1).GetString());
        Assert.Equal("اسم الخادم", ws.Cell(1, 2).GetString());
        Assert.Equal("عنوان IP", ws.Cell(1, 3).GetString());
        Assert.Equal("خط الطول", ws.Cell(1, 4).GetString());
        Assert.Equal("خط العرض", ws.Cell(1, 5).GetString());
        Assert.Equal("الاتجاه", ws.Cell(1, 6).GetString());
        Assert.Equal("الزاوية", ws.Cell(1, 7).GetString());
        Assert.Equal("المدى", ws.Cell(1, 8).GetString());
        Assert.Equal("ارتفاع الهوائي", ws.Cell(1, 9).GetString());
        Assert.Equal("قطاع الشمال", ws.Cell(2, 1).GetString());
        Assert.Equal("برج 1", ws.Cell(2, 2).GetString());
        Assert.Equal("10.1.1.10", ws.Cell(2, 3).GetString());
        Assert.Equal(36.29, ws.Cell(2, 4).GetDouble(), 3);
        Assert.Equal(33.51, ws.Cell(2, 5).GetDouble(), 3);
        Assert.Equal(45, ws.Cell(2, 6).GetDouble());
        Assert.Equal(90, ws.Cell(2, 7).GetDouble());
        Assert.Equal(5, ws.Cell(2, 8).GetDouble());
        Assert.Equal(18, ws.Cell(2, 9).GetDouble());
    }

    [Fact]
    public async Task ParseAsync_AcceptsExportedWorkbook()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedNetwork(db);
        SectorExcelImportService sut = new(db, NullLogger<SectorExcelImportService>.Instance);
        byte[] exported = await sut.BuildExportWorkbookAsync(1);

        await using ApplicationDbContext emptyDb = CreateDb();
        emptyDb.Networks.Add(new Network { Id = 1, Name = "Net" });
        emptyDb.MikroTikServers.Add(new MikroTikServer
        {
            Id = 5,
            Name = "برج 1",
            Host = "10.0.0.1",
            User = "admin",
            Pass = "p",
            NetworkId = 1
        });
        await emptyDb.SaveChangesAsync();
        SectorExcelImportService importer = new(emptyDb, NullLogger<SectorExcelImportService>.Instance);

        await using MemoryStream stream = new(exported);
        SectorExcelImportParseResult parsed = await importer.ParseAsync(stream, "المرسلات.xlsx", 1);

        Assert.True(parsed.Success);
        Assert.Equal(1, parsed.ImportableCount);
        Assert.Equal("قطاع الشمال", parsed.SectorsToAdd[0].Name);
        Assert.Equal(5, parsed.SectorsToAdd[0].MikroTikServerId);
        Assert.Equal("10.1.1.10", parsed.SectorsToAdd[0].IPAddress);
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
        db.MikroTikServers.Add(new MikroTikServer
        {
            Id = 5,
            Name = "برج 1",
            Host = "10.0.0.1",
            User = "admin",
            Pass = "p",
            NetworkId = 1
        });
        db.Sectors.Add(new Sector
        {
            Id = 11,
            Name = "قطاع الشمال",
            IPAddress = "10.1.1.10",
            NetworkMask = "255.255.255.0",
            Latitude = 33.51,
            Longitude = 36.29,
            Direction = 45,
            CoverageAngle = 90,
            CoverageRange = 5,
            AntennaHeightAglMeters = 18,
            MikroTikServerId = 5,
            NetworkId = 1,
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        db.SaveChanges();
    }
}
