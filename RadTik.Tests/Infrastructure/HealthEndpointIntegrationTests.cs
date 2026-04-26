using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RadTik.Tests.Infrastructure;

public class HealthEndpointIntegrationTests : IClassFixture<WebApplicationFactory<RadTik.Program>>
{
    private readonly WebApplicationFactory<RadTik.Program> _factory;

    public HealthEndpointIntegrationTests(WebApplicationFactory<RadTik.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
