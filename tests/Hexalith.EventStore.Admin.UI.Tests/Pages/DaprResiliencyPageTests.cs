using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the DaprResiliency page.
/// </summary>
public class DaprResiliencyPageTests : AdminUITestContext
{
    private readonly AdminResiliencyApiClient _mockClient;

    public DaprResiliencyPageTests()
    {
        _mockClient = Substitute.For<AdminResiliencyApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminResiliencyApiClient>.Instance);
        Services.AddScoped(_ => _mockClient);
    }

    [Fact]
    public void ResiliencyPage_RendersTitle()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprResiliency> cut = Render<DaprResiliency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Resiliency Policies"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("DAPR Resiliency Policies");
    }

    [Fact]
    public void ResiliencyPage_RendersBackLink()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprResiliency> cut = Render<DaprResiliency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Infrastructure"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("DAPR Infrastructure");
    }

    [Fact]
    public void ResiliencyPage_RendersStatCards_WithData()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprResiliency> cut = Render<DaprResiliency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Policies"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Retry Policies");
        markup.ShouldContain("Timeout Policies");
        markup.ShouldContain("Circuit Breakers");
        markup.ShouldContain("Target Bindings");
    }

    [Fact]
    public void ResiliencyPage_RendersEmptyState_WhenConfigUnavailable()
    {
        // Arrange
        _ = _mockClient.GetResiliencySpecAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprResiliencySpec?>(DaprResiliencySpec.Unavailable));

        // Act
        IRenderedComponent<DaprResiliency> cut = Render<DaprResiliency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Resiliency configuration not available"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Resiliency configuration not available");
    }

    [Fact]
    public void ResiliencyPage_RendersIssueBanner_WhenParseError()
    {
        // Arrange
        DaprResiliencySpec spec = DaprResiliencySpec.ParseError(
            "/etc/dapr/resiliency.yaml",
            "invalid: yaml: content",
            "Unexpected token");
        _ = _mockClient.GetResiliencySpecAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprResiliencySpec?>(spec));

        // Act
        IRenderedComponent<DaprResiliency> cut = Render<DaprResiliency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Failed to parse resiliency YAML"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Failed to parse resiliency YAML from /etc/dapr/resiliency.yaml: Unexpected token");
    }

    [Fact]
    public void ResiliencyPage_RendersReloadButton()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprResiliency> cut = Render<DaprResiliency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Reload"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Reload");
    }

    [Fact]
    public void ResiliencyPage_RendersIssueBanner_WhenApiUnavailable()
    {
        // Arrange
        _ = _mockClient.GetResiliencySpecAsync(Arg.Any<CancellationToken>())
            .Returns<DaprResiliencySpec?>(_ => throw new InvalidOperationException("API down"));

        // Act
        IRenderedComponent<DaprResiliency> cut = Render<DaprResiliency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load resiliency information"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load resiliency information");
    }

    [Fact]
    public void ResiliencyPage_RendersYamlSourceViewer_WhenAvailable()
    {
        // Arrange
        const string rawYaml = "apiVersion: dapr.io/v1alpha1\nkind: Resiliency\nmetadata:\n  name: myresiliency";
        DaprResiliencySpec spec = new(
            [new DaprRetryPolicy("defaultRetry", "constant", 3, "1s", null)],
            [new DaprTimeoutPolicy("defaultTimeout", "5s")],
            [new DaprCircuitBreakerPolicy("defaultBreaker", 1, "60s", "30s", "consecutiveFailures > 3")],
            [new DaprResiliencyTargetBinding("commandapi", "App", null, "defaultRetry", "defaultTimeout", "defaultBreaker")],
            IsConfigurationAvailable: true,
            RawYamlContent: rawYaml,
            ErrorMessage: null);
        _ = _mockClient.GetResiliencySpecAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprResiliencySpec?>(spec));

        // Act
        IRenderedComponent<DaprResiliency> cut = Render<DaprResiliency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain(rawYaml), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain(rawYaml);
        cut.Markup.ShouldContain("Raw YAML Configuration");
    }

    // ===== Helper methods =====

    private void SetupSuccessfulResponse()
    {
        DaprResiliencySpec spec = new(
            [new DaprRetryPolicy("defaultRetry", "exponential", 5, null, "15s")],
            [new DaprTimeoutPolicy("daprSidecar", "10s")],
            [new DaprCircuitBreakerPolicy("defaultBreaker", 1, "60s", "60s", "consecutiveFailures > 5")],
            [new DaprResiliencyTargetBinding("commandapi", "App", null, "defaultRetry", "daprSidecar", "defaultBreaker")],
            IsConfigurationAvailable: true,
            RawYamlContent: null,
            ErrorMessage: null);
        _ = _mockClient.GetResiliencySpecAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprResiliencySpec?>(spec));
    }
}
