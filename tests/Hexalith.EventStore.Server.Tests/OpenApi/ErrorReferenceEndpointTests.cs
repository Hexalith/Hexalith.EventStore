extern alias eventstore;

using System.Net;
using System.Reflection;

using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.OpenApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.OpenApi;

[Trait("Category", "Integration")]
[Trait("Tier", "2")]
public class ErrorReferenceEndpointTests : IClassFixture<OpenApiWebApplicationFactory> {
    private readonly OpenApiWebApplicationFactory _factory;

    public ErrorReferenceEndpointTests(OpenApiWebApplicationFactory factory) {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public static IEnumerable<object[]> ErrorReferenceModels() => ErrorReferenceEndpoints.ErrorModels
        .Select(model => new object[] { model.Slug, model.Title, model.StatusCode, model.ExampleJson, model.ResolutionSteps[0] });

    [Theory]
    [MemberData(nameof(ErrorReferenceModels))]
    public async Task ErrorReferencePage_ReturnsHtmlWithExpectedContent(
        string errorType,
        string title,
        int statusCode,
        string exampleJson,
        string firstResolutionStep) {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/problems/{errorType}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Error type '{errorType}' should return 200");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain($"<h1>{WebUtility.HtmlEncode($"{title} ({statusCode})")}</h1>");
        body.ShouldContain("<pre>");
        body.ShouldContain("<h2>Resolution</h2>");
        body.ShouldContain(WebUtility.HtmlEncode(exampleJson));
        body.ShouldContain(WebUtility.HtmlEncode(firstResolutionStep));
    }

    [Fact]
    public async Task ErrorReferencePage_CommandAmbiguity_ExplainsCrossTenantMessageIdCollisions()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/problems/command-correlation-ambiguous");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync();

        body.ShouldContain("multiple commands or authorized tenant scopes");
        body.ShouldContain("A MessageId can still be ambiguous across authorized tenant scopes.");
        body.ShouldContain("Have an operator verify the intended tenant scope");
        body.ShouldContain("Do not infer command success or resubmit the business command");
        body.ShouldContain("response correlationId (the request diagnostic reference)");
        body.ShouldNotContain("Query again using the command MessageId.");
        body.ShouldNotContain("Do not retry correlation lookup until a unique MessageId is available.");
    }

    [Fact]
    public async Task ErrorReferencePage_UnknownType_Returns404() {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/problems/does-not-exist");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ErrorReferencePage_RemainsAvailable_WhenOpenApiIsDisabled() {
        using WebApplicationFactory<eventstore::Program> factory = _factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:OpenApi:Enabled"] = "false",
            })));

        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/problems/validation-error");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Validation Error (400)");
    }

    [Fact]
    public void AllProblemTypeUris_HaveCorrespondingErrorModel() {
        // Verify every ProblemTypeUris constant has a matching ErrorReferenceModel
        FieldInfo[] fields = typeof(ProblemTypeUris).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        var modelSlugs = ErrorReferenceEndpoints.ErrorModels
            .Select(m => m.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (FieldInfo field in fields) {
            if (field.GetValue(null) is string uri) {
                string slug = uri[(uri.LastIndexOf('/') + 1)..];
                modelSlugs.Contains(slug).ShouldBeTrue(
                    $"ProblemTypeUris constant '{field.Name}' (slug: '{slug}') has no matching ErrorReferenceModel");
            }
        }
    }

    [Fact]
    public void AllErrorModels_HaveCorrespondingProblemTypeUri() {
        // Reverse check: every ErrorReferenceModel slug must exist in ProblemTypeUris
        FieldInfo[] fields = typeof(ProblemTypeUris).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        var uriSlugs = fields
            .Select(f => f.GetValue(null) as string)
            .Where(uri => uri is not null)
            .Select(uri => uri![(uri!.LastIndexOf('/') + 1)..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (ErrorReferenceEndpoints.ErrorReferenceModel model in ErrorReferenceEndpoints.ErrorModels) {
            uriSlugs.Contains(model.Slug).ShouldBeTrue(
                $"ErrorReferenceModel slug '{model.Slug}' has no corresponding ProblemTypeUris constant");
        }
    }
}
