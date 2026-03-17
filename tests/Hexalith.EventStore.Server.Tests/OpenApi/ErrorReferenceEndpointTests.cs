extern alias commandapi;

using System.Net;
using System.Reflection;

using commandapi::Hexalith.EventStore.CommandApi.ErrorHandling;
using commandapi::Hexalith.EventStore.CommandApi.OpenApi;

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

    /// <summary>
    /// Provides all ProblemTypeUri slugs via reflection over the constants class.
    /// Ensures tests stay in sync when new error types are added.
    /// </summary>
    public static IEnumerable<object[]> AllErrorTypeSlugs() {
        FieldInfo[] fields = typeof(ProblemTypeUris).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (FieldInfo field in fields) {
            if (field.GetValue(null) is string uri) {
                // Extract slug from URI: "https://hexalith.io/problems/validation-error" -> "validation-error"
                string slug = uri[(uri.LastIndexOf('/') + 1)..];
                yield return [slug];
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllErrorTypeSlugs))]
    public async Task ErrorReferencePage_ReturnsHtmlWithExpectedContent(string errorType) {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/problems/{errorType}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Error type '{errorType}' should return 200");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("<h1>");
        body.ShouldContain("<pre>");
        body.ShouldContain("<h2>Resolution</h2>");
    }

    [Fact]
    public async Task ErrorReferencePage_UnknownType_Returns404() {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/problems/does-not-exist");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
}
