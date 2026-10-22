
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 end-to-end tests verifying OpenAPI documentation includes
/// all Epic 17 endpoints with correct response schemas.
/// AC: #19.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class OpenApiE2ETests {
    private readonly AspireContractTestFixture _fixture;

    public OpenApiE2ETests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC #19: OpenAPI JSON includes the query endpoint.
    /// </summary>
    [Fact]
    public async Task OpenApiJson_IncludesQueryEndpoint() {
        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement swagger = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement paths = swagger.GetProperty("paths");

        paths.TryGetProperty("/api/v1/queries", out JsonElement queryPath).ShouldBeTrue(
            "OpenAPI JSON should include /api/v1/queries path");
        queryPath.TryGetProperty("post", out _).ShouldBeTrue(
            "/api/v1/queries should have a POST method");
    }

    /// <summary>
    /// AC #19: OpenAPI JSON includes the command validation endpoint.
    /// </summary>
    [Fact]
    public async Task OpenApiJson_IncludesCommandValidationEndpoint() {
        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement swagger = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement paths = swagger.GetProperty("paths");

        paths.TryGetProperty("/api/v1/commands/validate", out JsonElement valPath).ShouldBeTrue(
            "OpenAPI JSON should include /api/v1/commands/validate path");
        valPath.TryGetProperty("post", out _).ShouldBeTrue(
            "/api/v1/commands/validate should have a POST method");
    }

    /// <summary>
    /// AC #19: OpenAPI JSON includes the query validation endpoint.
    /// </summary>
    [Fact]
    public async Task OpenApiJson_IncludesQueryValidationEndpoint() {
        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement swagger = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement paths = swagger.GetProperty("paths");

        paths.TryGetProperty("/api/v1/queries/validate", out JsonElement valPath).ShouldBeTrue(
            "OpenAPI JSON should include /api/v1/queries/validate path");
        valPath.TryGetProperty("post", out _).ShouldBeTrue(
            "/api/v1/queries/validate should have a POST method");
    }

    /// <summary>
    /// AC #19: Validation endpoint 200 response schema includes isAuthorized (boolean) and reason (string, nullable).
    /// </summary>
    [Fact]
    public async Task OpenApiJson_ValidationEndpointResponseSchema_UsesBooleanAndNullableString() {
        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement swagger = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement paths = swagger.GetProperty("paths");

        // Check both validation endpoints
        string[] validationPaths = ["/api/v1/commands/validate", "/api/v1/queries/validate"];

        foreach (string path in validationPaths) {
            paths.TryGetProperty(path, out JsonElement endpoint).ShouldBeTrue(
                $"OpenAPI JSON should include {path}");
            JsonElement post = endpoint.GetProperty("post");
            JsonElement responses = post.GetProperty("responses");
            responses.TryGetProperty("200", out JsonElement ok200).ShouldBeTrue(
                $"{path} should have a 200 response");

            JsonElement schema = ok200.GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema");

            JsonElement resolvedSchema = ResolveSchema(swagger, schema);
            JsonElement properties = resolvedSchema.GetProperty("properties");

            AssertBooleanProperty(properties, "isAuthorized", path);
            AssertNullableStringProperty(properties, "reason", path);
        }
    }

    private static JsonElement ResolveSchema(JsonElement openApiDocument, JsonElement schema) {
        if (!schema.TryGetProperty("$ref", out JsonElement reference)) {
            return schema;
        }

        string schemaName = reference.GetString()!.Split('/')[^1];
        JsonElement schemas = openApiDocument.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty(schemaName, out JsonElement resolvedSchema).ShouldBeTrue(
            $"Referenced schema '{schemaName}' should exist in components/schemas");
        return resolvedSchema;
    }

    private static void AssertBooleanProperty(JsonElement properties, string propertyName, string path) {
        properties.TryGetProperty(propertyName, out JsonElement propertySchema).ShouldBeTrue(
            $"{path} schema should include '{propertyName}' property");
        propertySchema.GetProperty("type").GetString().ShouldBe("boolean",
            $"{path} property '{propertyName}' should be a boolean");
    }

    private static void AssertNullableStringProperty(JsonElement properties, string propertyName, string path) {
        properties.TryGetProperty(propertyName, out JsonElement propertySchema).ShouldBeTrue(
            $"{path} schema should include '{propertyName}' property");

        if (propertySchema.TryGetProperty("type", out JsonElement typeProperty)
            && typeProperty.ValueKind == JsonValueKind.String) {
            typeProperty.GetString().ShouldBe("string",
                $"{path} property '{propertyName}' should be a string");

            if (propertySchema.TryGetProperty("nullable", out JsonElement nullableProperty)) {
                nullableProperty.GetBoolean().ShouldBeTrue(
                    $"{path} property '{propertyName}' should be nullable");
                return;
            }
        }

        if (propertySchema.TryGetProperty("type", out JsonElement unionTypeProperty)
            && unionTypeProperty.ValueKind == JsonValueKind.Array) {
            AssertTypeArrayContainsStringAndNull(unionTypeProperty, propertyName, path);
            return;
        }

        if (propertySchema.TryGetProperty("anyOf", out JsonElement anyOf)) {
            AssertContainsStringAndNull(anyOf, propertyName, path);
            return;
        }

        if (propertySchema.TryGetProperty("oneOf", out JsonElement oneOf)) {
            AssertContainsStringAndNull(oneOf, propertyName, path);
            return;
        }

        throw new ShouldAssertException(
            $"{path} property '{propertyName}' should be represented as a nullable string.");
    }

    private static void AssertContainsStringAndNull(JsonElement unionSchema, string propertyName, string path) {
        bool hasString = false;
        bool hasNull = false;

        foreach (JsonElement option in unionSchema.EnumerateArray()) {
            if (!option.TryGetProperty("type", out JsonElement optionType)) {
                continue;
            }

            string? typeName = optionType.GetString();
            hasString |= string.Equals(typeName, "string", StringComparison.Ordinal);
            hasNull |= string.Equals(typeName, "null", StringComparison.Ordinal);
        }

        hasString.ShouldBeTrue($"{path} property '{propertyName}' should allow string values");
        hasNull.ShouldBeTrue($"{path} property '{propertyName}' should allow null values");
    }

    private static void AssertTypeArrayContainsStringAndNull(JsonElement unionTypes, string propertyName, string path) {
        bool hasString = false;
        bool hasNull = false;

        foreach (JsonElement option in unionTypes.EnumerateArray()) {
            string? typeName = option.GetString();
            hasString |= string.Equals(typeName, "string", StringComparison.Ordinal);
            hasNull |= string.Equals(typeName, "null", StringComparison.Ordinal);
        }

        hasString.ShouldBeTrue($"{path} property '{propertyName}' should allow string values");
        hasNull.ShouldBeTrue($"{path} property '{propertyName}' should allow null values");
    }
}
