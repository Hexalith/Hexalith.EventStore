using System.Net;
using System.Text.Json;

namespace Hexalith.EventStore.Admin.Server.Tests.OpenApi;

[Trait("Category", "Integration")]
[Trait("Tier", "1")]
public class AdminOpenApiDocumentTests : IClassFixture<AdminOpenApiWebApplicationFactory>
{
    private readonly AdminOpenApiWebApplicationFactory _factory;

    public AdminOpenApiDocumentTests(AdminOpenApiWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public async Task OpenApiDocument_ReturnsValidJson()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string content = await response.Content.ReadAsStringAsync();
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(content);
        doc.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public async Task OpenApiDocument_HasCorrectTitle()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        doc.GetProperty("info").GetProperty("title").GetString()
            .ShouldBe("Hexalith EventStore Admin API");
    }

    [Fact]
    public async Task OpenApiDocument_HasCorrectVersion()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        doc.GetProperty("info").GetProperty("version").GetString()
            .ShouldBe("v1");
    }

    [Fact]
    public async Task OpenApiDocument_HasOpenApi31Version()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        string? version = doc.GetProperty("openapi").GetString();
        version.ShouldNotBeNull();
        version.ShouldStartWith("3.1");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsBearerSecurityScheme()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();

        JsonElement components = doc.GetProperty("components");
        JsonElement securitySchemes = components.GetProperty("securitySchemes");
        securitySchemes.TryGetProperty("Bearer", out JsonElement bearer).ShouldBeTrue("Should have Bearer security scheme");

        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
        bearer.GetProperty("bearerFormat").GetString().ShouldBe("JWT");
    }

    [Fact]
    public async Task OpenApiDocument_HasGlobalSecurityRequirement()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        doc.TryGetProperty("security", out JsonElement security)
            .ShouldBeTrue("OpenAPI document should have global security array");

        bool hasBearerRequirement = security.EnumerateArray()
            .Any(req => req.TryGetProperty("Bearer", out _));
        hasBearerRequirement.ShouldBeTrue("Global security should require Bearer scheme");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsExpectedTags()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();

        doc.TryGetProperty("tags", out JsonElement tags).ShouldBeTrue("OpenAPI document should have tags array");
        string[] tagNames = tags.EnumerateArray()
            .Select(tag => tag.GetProperty("name").GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        tagNames.ShouldContain("Admin - Streams");
        tagNames.ShouldContain("Admin - Projections");
        tagNames.ShouldContain("Admin - Type Catalog");
        tagNames.ShouldContain("Admin - Health");
        tagNames.ShouldContain("Admin - Storage");
        tagNames.ShouldContain("Admin - Dead Letters");
        tagNames.ShouldContain("Admin - Tenants");
    }

    [Fact]
    public async Task OpenApiDocument_StreamEndpoints_GroupedUnderStreamsTag()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        OperationHasTag(paths, "/api/v1/admin/streams", "get", "Admin - Streams").ShouldBeTrue();
    }

    [Fact]
    public async Task OpenApiDocument_ProjectionEndpoints_GroupedUnderProjectionsTag()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        OperationHasTag(paths, "/api/v1/admin/projections", "get", "Admin - Projections").ShouldBeTrue();
    }

    [Fact]
    public async Task OpenApiDocument_AllOperations_Have401And403Responses()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            // Only check admin endpoints
            if (!path.Name.StartsWith("/api/v1/admin/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (JsonProperty method in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(method.Name))
                {
                    continue;
                }

                JsonElement operation = method.Value;
                operation.TryGetProperty("responses", out JsonElement responses)
                    .ShouldBeTrue($"{method.Name.ToUpperInvariant()} {path.Name} should have responses");

                responses.TryGetProperty("401", out _)
                    .ShouldBeTrue($"{method.Name.ToUpperInvariant()} {path.Name} should have 401 response");
                responses.TryGetProperty("403", out _)
                    .ShouldBeTrue($"{method.Name.ToUpperInvariant()} {path.Name} should have 403 response");
            }
        }
    }

    [Fact]
    public async Task OpenApiDocument_AllOperations_Have503Response()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/admin/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (JsonProperty method in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(method.Name))
                {
                    continue;
                }

                JsonElement operation = method.Value;
                operation.TryGetProperty("responses", out JsonElement responses)
                    .ShouldBeTrue($"{method.Name.ToUpperInvariant()} {path.Name} should have responses");
                responses.TryGetProperty("503", out _)
                    .ShouldBeTrue($"{method.Name.ToUpperInvariant()} {path.Name} should have 503 response");
            }
        }
    }

    [Fact]
    public async Task OpenApiDocument_OperatorEndpoints_DescribeRequiredRole()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        // Projection pause is an Operator-level endpoint
        bool foundOperatorEndpoint = false;
        foreach (JsonProperty path in paths.EnumerateObject())
        {
            if (!path.Name.Contains("projections", StringComparison.OrdinalIgnoreCase)
                || !path.Name.Contains("pause", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.Value.TryGetProperty("post", out JsonElement post)
                && post.TryGetProperty("description", out JsonElement desc))
            {
                string? description = desc.GetString();
                description.ShouldNotBeNull();
                description.ShouldContain("Operator", Case.Insensitive);
                foundOperatorEndpoint = true;
            }
        }

        foundOperatorEndpoint.ShouldBeTrue("Should find at least one projection pause endpoint with Operator role description");
    }

    [Fact]
    public async Task OpenApiDocument_HasMinimumExpectedPaths()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        int adminPathCount = paths.EnumerateObject()
            .Count(p => p.Name.StartsWith("/api/v1/admin/", StringComparison.OrdinalIgnoreCase));

        // 7 controllers × ~2-4 endpoints = at least 15 paths
        adminPathCount.ShouldBeGreaterThanOrEqualTo(15,
            $"Expected at least 15 admin paths, found {adminPathCount}. Check that AddApplicationPart registered all admin controllers.");
    }

    [Fact]
    public async Task OpenApiDocument_AtLeastOneOperation_HasXmlDocDescription()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        bool foundDescription = false;
        foreach (JsonProperty path in paths.EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/admin/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (JsonProperty method in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(method.Name))
                {
                    continue;
                }

                if (method.Value.TryGetProperty("description", out JsonElement desc))
                {
                    string? description = desc.GetString();
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        foundDescription = true;
                        break;
                    }
                }
            }

            if (foundDescription)
            {
                break;
            }
        }

        foundDescription.ShouldBeTrue(
            "At least one admin operation should have a description from XML docs. " +
            "Check that <GenerateDocumentationFile> is enabled in Admin.Server.csproj.");
    }

    [Fact]
    public async Task OpenApiDocument_StreamEndpoints_Have200And404Responses()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        // List endpoint should have 200
        paths.TryGetProperty("/api/v1/admin/streams", out JsonElement streamsPath)
            .ShouldBeTrue("Should have streams list path");
        streamsPath.TryGetProperty("get", out JsonElement listOp).ShouldBeTrue();
        listOp.GetProperty("responses").TryGetProperty("200", out _)
            .ShouldBeTrue("GET /api/v1/admin/streams should have 200 response");

        // Detail endpoints (containing path parameters) should have 404
        bool foundDetailWith404 = false;
        foreach (JsonProperty path in paths.EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/admin/streams/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.Value.TryGetProperty("get", out JsonElement detailOp)
                && detailOp.TryGetProperty("responses", out JsonElement responses)
                && responses.TryGetProperty("404", out _))
            {
                foundDetailWith404 = true;
                break;
            }
        }

        foundDetailWith404.ShouldBeTrue("At least one stream detail endpoint should have 404 response");
    }

    [Fact]
    public async Task OpenApiDocument_StreamEndpoints_HaveTypedResponseSchema()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        paths.TryGetProperty("/api/v1/admin/streams", out JsonElement streamsPath)
            .ShouldBeTrue("Should have streams path");
        streamsPath.TryGetProperty("get", out JsonElement getOp).ShouldBeTrue();
        getOp.TryGetProperty("responses", out JsonElement responses).ShouldBeTrue();
        responses.TryGetProperty("200", out JsonElement ok).ShouldBeTrue();

        // Verify the 200 response has content with a schema (not untyped object)
        ok.TryGetProperty("content", out _).ShouldBeTrue(
            "GET /api/v1/admin/streams 200 response should have content (typed schema). " +
            "If missing, controllers may be using IActionResult instead of ActionResult<T>.");
    }

    [Fact]
    public async Task OpenApiDocument_OperationIds_AreReadable()
    {
        JsonElement doc = await GetOpenApiDocumentAsync();
        JsonElement paths = doc.GetProperty("paths");

        // Validate that any operation IDs present are human-readable (not auto-generated hashes).
        // Controllers may not define explicit operationIds — that's acceptable for now,
        // but if they exist they must be readable for CLI/MCP consumers.
        foreach (JsonProperty path in paths.EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/admin/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (JsonProperty method in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(method.Name))
                {
                    continue;
                }

                if (method.Value.TryGetProperty("operationId", out JsonElement opId))
                {
                    string? id = opId.GetString();
                    id.ShouldNotBeNullOrWhiteSpace(
                        $"{method.Name.ToUpperInvariant()} {path.Name} operationId should not be empty");
                    id!.Any(char.IsLetter).ShouldBeTrue(
                        $"operationId '{id}' on {method.Name.ToUpperInvariant()} {path.Name} should contain letters (not a hash)");
                }
            }
        }
    }

    private async Task<JsonElement> GetOpenApiDocumentAsync()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json").ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    private static bool OperationHasTag(JsonElement paths, string path, string method, string expectedTag)
    {
        if (!paths.TryGetProperty(path, out JsonElement pathElement)
            || !pathElement.TryGetProperty(method, out JsonElement operation)
            || !operation.TryGetProperty("tags", out JsonElement tags))
        {
            return false;
        }

        return tags.EnumerateArray()
            .Select(tag => tag.GetString())
            .Any(tag => string.Equals(tag, expectedTag, StringComparison.Ordinal));
    }

    private static bool IsHttpMethod(string name)
        => string.Equals(name, "get", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "post", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "put", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "delete", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "patch", StringComparison.OrdinalIgnoreCase);
}
