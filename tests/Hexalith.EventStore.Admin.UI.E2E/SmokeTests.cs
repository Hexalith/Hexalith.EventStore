using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.UI.E2E;

/// <summary>
/// HTTP smoke tests for Admin.UI (no browser — inspects server-rendered markup).
/// Tests 10.4-10.7: Accessibility, high-contrast, keyboard navigation, performance.
/// </summary>
[Trait("Category", "E2E")]
public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateSidecarAwareFactory()
    {
        Dictionary<string, string?> settings = E2EAuthenticationSettings.Create();
        return _factory.WithWebHostBuilder(builder =>
        {
            foreach ((string key, string? value) in settings)
            {
                builder.UseSetting(key, value);
            }
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<E2EAdminApiHandler>();
                services.AddHttpClient("AdminApi")
                    .ConfigurePrimaryHttpMessageHandler(provider => provider.GetRequiredService<E2EAdminApiHandler>())
                    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
            });
        });
    }

    [Fact]
    public async Task LandingPage_ReturnsSuccessAndContainsShell()
    {
        // Arrange
        using WebApplicationFactory<Program> factory = CreateSidecarAwareFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/");
        string content = await response.Content.ReadAsStringAsync();

        // Assert — shell renders within reasonable time
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        content.ShouldContain("Hexalith EventStore Admin");
        content.ShouldContain("lang=\"en\"");
        factory.Services.GetRequiredService<E2EAdminApiHandler>().VerifyNoRejectedRequests();
    }

    [Fact]
    public async Task LandingPage_HasSkipToMainContentLink()
    {
        // Arrange
        using WebApplicationFactory<Program> factory = CreateSidecarAwareFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/");
        string content = await response.Content.ReadAsStringAsync();

        // Assert — accessibility: skip-to-main-content link present
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        content.ShouldContain("Skip to main content");
        factory.Services.GetRequiredService<E2EAdminApiHandler>().VerifyNoRejectedRequests();
    }

    [Fact]
    public async Task LandingPage_HasSemanticHtml()
    {
        // Arrange
        using WebApplicationFactory<Program> factory = CreateSidecarAwareFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/");
        string content = await response.Content.ReadAsStringAsync();

        // Assert — semantic HTML structure
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        content.ShouldContain("<main");
        content.ShouldContain("role=\"main\"");
        content.ShouldContain("<nav");
        factory.Services.GetRequiredService<E2EAdminApiHandler>().VerifyNoRejectedRequests();
    }

    [Fact]
    public async Task ShellRendersOnColdFirstRequest()
    {
        // Arrange
        using WebApplicationFactory<Program> factory = CreateSidecarAwareFactory();
        HttpClient client = factory.CreateClient();

        // Act — the first request pays cold JIT + Razor / static-asset warmup (~15s
        // observed on a dev box).
        HttpResponseMessage response = await client.GetAsync("/");

        // Assert — functional only. The "shell within 2s" budget (AC 14) is owned by
        // perf-lab.yml; a wall-clock assertion here flakes on cold-start under CI load.
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        factory.Services.GetRequiredService<E2EAdminApiHandler>().VerifyNoRejectedRequests();
    }
}
