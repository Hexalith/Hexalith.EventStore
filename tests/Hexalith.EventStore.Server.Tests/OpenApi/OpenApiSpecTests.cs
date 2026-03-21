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

    private async Task<JsonElement> GetOpenApiDocumentAsync() {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json").ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    [Fact]
    public async Task OpenApiDocument_ContainsExpectedTopLevelTags() {
        JsonElement doc = await GetOpenApiDocumentAsync();

        doc.TryGetProperty("tags", out JsonElement tags).ShouldBeTrue("OpenAPI document should have tags array");
        string[] tagNames = tags.EnumerateArray()
            .Select(tag => tag.GetProperty("name").GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        tagNames.ShouldContain("Commands");
        tagNames.ShouldContain("Queries");
        tagNames.ShouldContain("Validation");
    }

    [Fact]
    public async Task OpenApiDocument_Operations_AreGroupedUnderExpectedTags() {
        JsonElement doc = await GetOpenApiDocumentAsync();

        JsonElement paths = doc.GetProperty("paths");
        OperationHasTag(paths, "/api/v1/commands", "post", "Commands").ShouldBeTrue();
        OperationHasTag(paths, "/api/v1/commands/status/{correlationId}", "get", "Commands").ShouldBeTrue();
        OperationHasTag(paths, "/api/v1/queries", "post", "Queries").ShouldBeTrue();
        OperationHasTag(paths, "/api/v1/commands/validate", "post", "Validation").ShouldBeTrue();
        OperationHasTag(paths, "/api/v1/queries/validate", "post", "Validation").ShouldBeTrue();
    }

    [Fact]
    public async Task OpenApiDocument_ContainsCommandExamplePayload() {
        JsonElement doc = await GetOpenApiDocumentAsync();

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
        messageId.GetString().ShouldBe("01JAXYZ1234567890ABCDEFGHJ");

        value.TryGetProperty("tenant", out JsonElement tenant).ShouldBeTrue();
        tenant.GetString().ShouldBe("tenant-a");

        value.TryGetProperty("domain", out JsonElement domain).ShouldBeTrue();
        domain.GetString().ShouldBe("counter");

        value.TryGetProperty("aggregateId", out JsonElement aggregateId).ShouldBeTrue();
        aggregateId.GetString().ShouldBe("01JAXYZ1234567890ABCDEFJKM");

        value.TryGetProperty("commandType", out JsonElement commandType).ShouldBeTrue();
        commandType.GetString().ShouldBe("IncrementCounter");

        value.TryGetProperty("payload", out JsonElement payload).ShouldBeTrue();
        payload.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public async Task OpenApiDocument_CommandEndpoints_ContainXmlDocumentationDescriptions() {
        JsonElement doc = await GetOpenApiDocumentAsync();

        JsonElement paths = doc.GetProperty("paths");
        JsonElement submitOperation = paths.GetProperty("/api/v1/commands").GetProperty("post");
        JsonElement statusOperation = paths.GetProperty("/api/v1/commands/status/{correlationId}").GetProperty("get");

        string? submitDescription = submitOperation.GetProperty("description").GetString();
        string? statusDescription = statusOperation.GetProperty("description").GetString();

        submitOperation.GetProperty("summary").GetString().ShouldBe("Submits a command for asynchronous processing.");
        _ = submitDescription.ShouldNotBeNull();
        submitDescription.ShouldContain("Location header pointing to the status polling endpoint");
        submitOperation.GetProperty("responses").GetProperty("202").GetProperty("description").GetString().ShouldBe("Command accepted for processing. Check status at the Location header URL.");

        statusOperation.GetProperty("summary").GetString().ShouldBe("Gets the current processing status of a command by correlation ID.");
        _ = statusDescription.ShouldNotBeNull();
        statusDescription.ShouldContain("Command Lifecycle States");
        statusDescription.ShouldContain("Terminal states mean the command has reached its final outcome");
        statusOperation.GetProperty("responses").GetProperty("404").GetProperty("description").GetString().ShouldBe("No command status found for the given correlation ID.");
    }

    [Fact]
    public async Task SwaggerUi_ReturnsHtml() {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/swagger/index.html");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("swagger", Case.Insensitive);

        HttpResponseMessage initializerResponse = await client.GetAsync("/swagger/swagger-initializer.js");
        initializerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        initializerResponse.Content.Headers.ContentType?.MediaType.ShouldBeOneOf("text/javascript", "application/javascript");

        string initializerBody = await initializerResponse.Content.ReadAsStringAsync();
        initializerBody.ShouldContain("SwaggerUIBundle");
        initializerBody.ShouldContain("window.ui");
    }

    private static bool OperationHasTag(JsonElement paths, string path, string method, string expectedTag) {
        if (!paths.TryGetProperty(path, out JsonElement pathElement)
            || !pathElement.TryGetProperty(method, out JsonElement operation)
            || !operation.TryGetProperty("tags", out JsonElement tags)) {
            return false;
        }

        return tags.EnumerateArray()
            .Select(tag => tag.GetString())
            .Any(tag => string.Equals(tag, expectedTag, StringComparison.Ordinal));
    }
}
