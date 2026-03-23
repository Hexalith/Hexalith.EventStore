using System.Net;

namespace Hexalith.EventStore.Admin.Server.Tests.OpenApi;

[Trait("Category", "Integration")]
[Trait("Tier", "1")]
public class AdminOpenApiConfigGatingTests : IClassFixture<AdminOpenApiDisabledFactory>
{
    private readonly AdminOpenApiDisabledFactory _factory;

    public AdminOpenApiConfigGatingTests(AdminOpenApiDisabledFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public async Task OpenApiEndpoint_WhenDisabled_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SwaggerUi_WhenDisabled_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/swagger/index.html");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
