using System.Net;
using Xunit;

namespace RadaTik.Tests.Infrastructure;

public class HealthEndpointIntegrationTests : IClassFixture<RadaTikWebApplicationFactory>
{
    private readonly RadaTikWebApplicationFactory _factory;

    public HealthEndpointIntegrationTests(RadaTikWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
