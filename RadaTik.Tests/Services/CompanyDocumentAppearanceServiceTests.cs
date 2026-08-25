using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Documents;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class CompanyDocumentAppearanceServiceTests
{
    [Fact]
    public async Task GetChromeAsync_DoesNotLeakAnotherCompanyAppearance()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.AddRange(
            new Network { Id = 1, Name = "شركة ألف", LogoPath = "/uploads/networks/a.png" },
            new Network { Id = 2, Name = "شركة باء", LogoPath = "/uploads/networks/b.png" });
        db.CompanyDocumentAppearances.Add(new CompanyDocumentAppearance
        {
            CompanyNetworkId = 1,
            WatermarkMode = DocumentWatermarkMode.CustomText,
            WatermarkText = "سري-ألف",
            FooterText = "تذييل ألف",
            PrimaryColor = "#112233"
        });
        await db.SaveChangesAsync();

        CompanyDocumentAppearanceService sut = CreateSut(db);

        CompanyDocumentChrome chromeB = await sut.GetChromeAsync(2, "تقرير", null, "2026/01/01");

        Assert.Equal(2, chromeB.CompanyNetworkId);
        Assert.Equal("شركة باء", chromeB.CompanyName);
        Assert.Equal("/uploads/networks/b.png", chromeB.LogoUrl);
        Assert.Equal(DocumentWatermarkMode.None, chromeB.WatermarkMode);
        Assert.Null(chromeB.WatermarkText);
        Assert.Null(chromeB.FooterText);
        Assert.NotEqual("#112233", chromeB.PrimaryColor);
    }

    [Fact]
    public async Task GetChromeAsync_ChildNetworkUsesParentCompanyIdentity()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.AddRange(
            new Network { Id = 10, Name = "الشركة الأم", LogoPath = "/uploads/networks/parent.png" },
            new Network { Id = 11, Name = "شبكة فرعية", ParentNetworkId = 10 });
        db.CompanyDocumentAppearances.Add(new CompanyDocumentAppearance
        {
            CompanyNetworkId = 10,
            WatermarkMode = DocumentWatermarkMode.CompanyName,
            FooterText = "يُقدّم للهيئة الناظمة للاتصالات والبريد"
        });
        await db.SaveChangesAsync();

        CompanyDocumentAppearanceService sut = CreateSut(db);
        CompanyDocumentChrome chrome = await sut.GetChromeAsync(11, "عقد انضمام");

        Assert.Equal(10, chrome.CompanyNetworkId);
        Assert.Equal("الشركة الأم", chrome.CompanyName);
        Assert.Equal(DocumentWatermarkMode.CompanyName, chrome.WatermarkMode);
        Assert.Equal("الشركة الأم", chrome.WatermarkText);
        Assert.Equal("يُقدّم للهيئة الناظمة للاتصالات والبريد", chrome.FooterText);
    }

    [Fact]
    public async Task SaveAsync_RejectsChildNetworkAsCompany()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.AddRange(
            new Network { Id = 10, Name = "الشركة الأم" },
            new Network { Id = 11, Name = "فرعية", ParentNetworkId = 10 });
        await db.SaveChangesAsync();
        CompanyDocumentAppearanceService sut = CreateSut(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveAsync(
            11,
            "user-1",
            new CompanyDocumentAppearanceSaveCommand()));
    }

    private static CompanyDocumentAppearanceService CreateSut(ApplicationDbContext db)
    {
        Mock<IWebHostEnvironment> env = new();
        env.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());
        return new CompanyDocumentAppearanceService(db, env.Object);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
