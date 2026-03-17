extern alias commandapi;

using System.Net;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.OpenApi;

[Trait("Category", "Integration")]
[Trait("Tier", "2")]
public class OpenApiSpecTests : IClassFixture<OpenApiWebApplicationFactory> {
    private readonly OpenApiWebApplicationFactory _factory;

    public OpenApiSpecTests(OpenApiWebApplicationFactory factory) {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public async Task OpenApiDocument_ContainsCommandsTag() {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string content = await response.Content.ReadAsStringAsync();
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(content);

        doc.TryGetProperty("tags", out JsonElement tags).ShouldBeTrue("OpenAPI document should have tags array");
        bool hasCommandsTag = false;
        foreach (JsonElement tag in tags.EnumerateArray()) {
            if (tag.TryGetProperty("name", out JsonElement name)
                && string.Equals(name.GetString(), "Commands", StringComparison.Ordinal)) {
                hasCommandsTag = true;
                break;
            }
        }

        hasCommandsTag.ShouldBeTrue("Tags should contain 'Commands'");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsCommandExamplePayload() {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string content = await response.Content.ReadAsStringAsync();
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Navigate to paths -> /api/v1/commands -> post -> requestBody -> content -> application/json -> examples
        doc.TryGetProperty("paths", out JsonElement paths).ShouldBeTrue();
        paths.TryGetProperty("/api/v1/commands", out JsonElement commandsPath).ShouldBeTrue();
        commandsPath.TryGetProperty("post", out JsonElement post).ShouldBeTrue();
        post.TryGetProperty("requestBody", out JsonElement requestBody).ShouldBeTrue();
        requestBody.TryGetProperty("content", out JsonElement contentProp).ShouldBeTrue();
        contentProp.TryGetProperty("application/json", out JsonElement jsonContent).ShouldBeTrue();
        jsonContent.TryGetProperty("examples", out JsonElement examples).ShouldBeTrue("POST /commands should have examples");
        examples.TryGetProperty("IncrementCounter", out JsonElement counterExample).ShouldBeTrue("Should have IncrementCounter named example");

        counterExample.TryGetProperty("value", out JsonElement value).ShouldBeTrue();
        value.TryGetProperty("messageId", out JsonElement messageId).ShouldBeTrue();
        messageId.GetString().ShouldBe("01JAXYZ1234567890ABCDEFGH");

        value.TryGetProperty("tenant", out JsonElement tenant).ShouldBeTrue();
        tenant.GetString().ShouldBe("tenant-a");

        value.TryGetProperty("domain", out JsonElement domain).ShouldBeTrue();
        domain.GetString().ShouldBe("counter");

        value.TryGetProperty("aggregateId", out JsonElement aggregateId).ShouldBeTrue();
        aggregateId.GetString().ShouldBe("01JAXYZ1234567890ABCDEFJK");

        value.TryGetProperty("commandType", out JsonElement commandType).ShouldBeTrue();
        commandType.GetString().ShouldBe("IncrementCounter");

        value.TryGetProperty("payload", out JsonElement payload).ShouldBeTrue();
        payload.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public async Task SwaggerUi_ReturnsHtml() {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/swagger/index.html");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("swagger", Case.Insensitive);
    }
}
