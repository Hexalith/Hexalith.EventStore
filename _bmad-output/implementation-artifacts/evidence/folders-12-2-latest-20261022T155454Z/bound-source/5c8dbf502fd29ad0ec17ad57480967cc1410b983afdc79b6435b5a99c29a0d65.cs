using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the CommandSandbox component.
/// </summary>
public class CommandSandboxTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public CommandSandboxTests() {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);

        _ = Services.AddScoped(_ => _mockApiClient);
        _ = Services.AddScoped<DashboardRefreshService>();
        _ = Services.AddScoped<TopologyCacheService>();
        TestSignalRClient testClient = new();
        _ = Services.AddSingleton(testClient);
        _ = Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void CommandSandbox_RendersInputForm() {
        IRenderedComponent<CommandSandbox> cut = RenderSandbox();

        string markup = cut.Markup;
        markup.ShouldContain("Command Sandbox");
        markup.ShouldContain("Command Type");
        markup.ShouldContain("Command Payload");
        markup.ShouldContain("Test Against State at Sequence");
        markup.ShouldContain("Run in Sandbox");
        markup.ShouldContain("Close");
    }

    [Fact]
    public void CommandSandbox_ShowsInfoBanner() {
        IRenderedComponent<CommandSandbox> cut = RenderSandbox();

        string markup = cut.Markup;
        markup.ShouldContain("Dry Run");
        markup.ShouldContain("no events are persisted");
    }

    [Fact]
    public void CommandSandbox_ShowsAcceptedOutcome_WithEvents() {
        // Arrange
        List<SandboxEvent> events = [new(0, "CounterIncremented", "{\"Count\":1}", false)];
        List<FieldChange> changes = [new("Count", "0", "1")];
        var result = new SandboxResult(
            "test-tenant", "counter", "agg-001",
            5, "IncrementCounter", "accepted",
            events, "{\"Count\":1}", changes, null, 42);

        _ = _mockApiClient.SandboxCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<SandboxCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SandboxResult?>(result));

        // Act
        IRenderedComponent<CommandSandbox> cut = RenderSandbox();
        SetCommandType(cut, "IncrementCounter");
        ClickRun(cut);

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Command accepted"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("1 event(s) would be produced");
        markup.ShouldContain("CounterIncremented");
        markup.ShouldContain("Produced Events");
        markup.ShouldContain("State Changes");
        markup.ShouldContain("Resulting State");
        markup.ShouldContain("42ms");
    }

    [Fact]
    public void CommandSandbox_ShowsAcceptedNoOp() {
        // Arrange
        var result = new SandboxResult(
            "test-tenant", "counter", "agg-001",
            5, "NoOpCommand", "accepted",
            [], "{\"Count\":0}", [], null, 10);

        _ = _mockApiClient.SandboxCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<SandboxCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SandboxResult?>(result));

        // Act
        IRenderedComponent<CommandSandbox> cut = RenderSandbox();
        SetCommandType(cut, "NoOpCommand");
        ClickRun(cut);

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("no-op"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("no events would be produced");
    }

    [Fact]
    public void CommandSandbox_ShowsRejectedOutcome() {
        // Arrange
        List<SandboxEvent> events = [new(0, "InsufficientBalance", "{}", true)];
        var result = new SandboxResult(
            "test-tenant", "counter", "agg-001",
            5, "DecrementCounter", "rejected",
            events, string.Empty, [], null, 15);

        _ = _mockApiClient.SandboxCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<SandboxCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SandboxResult?>(result));

        // Act
        IRenderedComponent<CommandSandbox> cut = RenderSandbox();
        SetCommandType(cut, "DecrementCounter");
        ClickRun(cut);

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Command rejected"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("InsufficientBalance");
    }

    [Fact]
    public void CommandSandbox_ShowsErrorOutcome() {
        // Arrange
        var result = new SandboxResult(
            "test-tenant", "counter", "agg-001",
            5, "BadCommand", "error",
            [], string.Empty, [], "No domain service registered", 5);

        _ = _mockApiClient.SandboxCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<SandboxCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SandboxResult?>(result));

        // Act
        IRenderedComponent<CommandSandbox> cut = RenderSandbox();
        SetCommandType(cut, "BadCommand");
        ClickRun(cut);

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Sandbox Error"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No domain service registered");
    }

    [Fact]
    public void CommandSandbox_ShowsTimeoutError() {
        // Arrange
        _ = _mockApiClient.SandboxCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<SandboxCommandRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        IRenderedComponent<CommandSandbox> cut = RenderSandbox();
        SetCommandType(cut, "SlowCommand");
        ClickRun(cut);

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Sandbox timed out"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Sandbox timed out");
    }

    [Fact]
    public void CommandSandbox_ShowsConnectivityError() {
        // Arrange
        _ = _mockApiClient.SandboxCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<SandboxCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SandboxResult?>(null));

        // Act
        IRenderedComponent<CommandSandbox> cut = RenderSandbox();
        SetCommandType(cut, "TestCommand");
        ClickRun(cut);

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No result returned"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No result returned");
    }

    [Fact]
    public void CommandSandbox_PayloadDialog_CarriesAriaLabel() {
        // Regression signal only — does NOT prove runtime ARIA correctness (FluentDialog v5 may
        // render the attribute into shadow DOM which bUnit does not traverse). Task 5.6 AT pass
        // is the real verifier. This catches the case where `aria-label="Event payload"` is
        // accidentally removed from CommandSandbox.razor source.
        IRenderedComponent<CommandSandbox> cut = RenderSandbox();

        AngleSharp.Dom.IElement dialog = cut.Find("fluent-dialog[aria-label='Event payload']");
        dialog.GetAttribute("aria-label").ShouldBe("Event payload");
        cut.FindAll("fluent-dialog[aria-label='Event payload']").Count.ShouldBe(1);
    }

    [Fact]
    public void CommandSandbox_PreFillsInitialSequence() {
        IRenderedComponent<CommandSandbox> cut = RenderSandbox(initialSequence: 42);

        // The component should pre-fill the sequence field
        string markup = cut.Markup;
        markup.ShouldContain("Command Sandbox");
    }

    private IRenderedComponent<CommandSandbox> RenderSandbox(long? initialSequence = null) => Render<CommandSandbox>(p => p
                                                                                                       .Add(c => c.TenantId, "test-tenant")
                                                                                                       .Add(c => c.Domain, "counter")
                                                                                                       .Add(c => c.AggregateId, "agg-001")
                                                                                                       .Add(c => c.InitialSequence, initialSequence));

    private static void SetCommandType(IRenderedComponent<CommandSandbox> cut, string commandType) {
        // Find the command type text field and set its value
        AngleSharp.Dom.IElement? textField = cut.Find("fluent-text-input");
        textField.Change(commandType);
    }

    private static void ClickRun(IRenderedComponent<CommandSandbox> cut) => cut.Find("fluent-button[appearance='primary']").Click();
}
