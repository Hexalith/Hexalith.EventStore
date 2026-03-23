using System.Net;

namespace Hexalith.EventStore.Admin.Server.Tests.OpenApi;

[Trait("Category", "Integration")]
[Trait("Tier", "1")]
public class AdminSwaggerUiTests : IClassFixture<AdminOpenApiWebApplicationFactory>
{
    private readonly AdminOpenApiWebApplicationFactory _factory;

    public AdminSwaggerUiTests(AdminOpenApiWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public async Task SwaggerUi_ReturnsHtml()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/swagger/index.html");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("swagger", Case.Insensitive);
    }

    [Fact]
    public async Task SwaggerUi_InitializerJs_ReturnsJs()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/swagger/swagger-initializer.js");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBeOneOf("text/javascript", "application/javascript");

        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("SwaggerUIBundle");
    }
}
