using Microsoft.AspNetCore.Hosting;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientNationalIdImageServiceTests
{
    [Fact]
    public void IsOwnedPath_AcceptsOnlySameClientFolder()
    {
        ClientNationalIdImageService sut = new(new StubEnvironment());

        Assert.True(sut.IsOwnedPath("/uploads/client-ids/12/abc.jpg", 12));
        Assert.False(sut.IsOwnedPath("/uploads/client-ids/12/../13/abc.jpg", 12));
        Assert.False(sut.IsOwnedPath("/uploads/client-ids/13/abc.jpg", 12));
        Assert.False(sut.IsOwnedPath("/uploads/receipts/abc.jpg", 12));
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string ApplicationName { get; set; } = "test";
        public string EnvironmentName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
