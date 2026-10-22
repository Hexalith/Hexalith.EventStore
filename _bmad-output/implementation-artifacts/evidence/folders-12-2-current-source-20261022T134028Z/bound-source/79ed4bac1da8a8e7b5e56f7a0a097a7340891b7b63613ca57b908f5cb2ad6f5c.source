using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the StateInspectorModal component.
/// </summary>
public class StateInspectorModalTests : AdminUITestContext {
    private const System.Reflection.BindingFlags PrivateStatic =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

    private readonly AdminStreamApiClient _mockApiClient;

    public StateInspectorModalTests() {
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
    public void StateInspectorModal_RendersWithPreFilledSequence() {
        // Arrange & Act
        IRenderedComponent<StateInspectorModal> cut = RenderInspector(42L);

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("State Inspector");
        markup.ShouldContain("Sequence Number");
    }

    [Fact]
    public void StateInspectorModal_DialogBodyUsesFlatV5Content() {
        // Arrange & Act
        IRenderedComponent<StateInspectorModal> cut = RenderInspector(42L);

        // Assert: the dialog body element is rendered.
        AngleSharp.Dom.IElement body = cut.Find("fluent-dialog-body");
        _ = body.ShouldNotBeNull();

        // Assert: the State Inspector h3 heading is reachable inside the dialog body.
        AngleSharp.Dom.IElement? heading = body.QuerySelector("h3");
        _ = heading.ShouldNotBeNull();
        heading.TextContent.ShouldContain("State Inspector");

        // Assert: the scrollable content wrapper exists for sizing/overflow control.
        AngleSharp.Dom.IElement? contentWrapper = body.QuerySelector(".state-inspector-modal-body");
        _ = contentWrapper.ShouldNotBeNull();

        // Assert: the heading is NOT a descendant of the scrollable wrapper. This proves
        // the title row sits outside the overflow:auto region (close button stays visible
        // on tall content) and that <TitleTemplate>/<ChildContent> render-fragment slots
        // were not reintroduced as wrappers around the title bar.
        contentWrapper.Contains(heading).ShouldBeFalse();
    }

    [Fact]
    public void StateInspectorModal_FetchesStateOnSubmit() {
        // Arrange
        AggregateStateSnapshot snapshot = new(
            "test-tenant", "counter", "agg-001", 42,
            DateTimeOffset.UtcNow, """{"count": 5}""");
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            "test-tenant", "counter", "agg-001", 42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(snapshot));

        IRenderedComponent<StateInspectorModal> cut = RenderInspector(42L);

        // Act — click Inspect button via markup
        AngleSharp.Dom.IElement inspectBtn = cut.Find("fluent-button[appearance='primary']");
        inspectBtn.Click();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("State at Sequence #42"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StateInspectorModal_ShowsNoStateAvailable_OnNullResponse() {
        // Arrange
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(null));

        IRenderedComponent<StateInspectorModal> cut = RenderInspector(10L);

        // Act — click Inspect button
        AngleSharp.Dom.IElement inspectBtn = cut.Find("fluent-button[appearance='primary']");
        inspectBtn.Click();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No state available at this position"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StateInspectorModal_RendersToggleForTimestampMode() {
        // Arrange & Act
        IRenderedComponent<StateInspectorModal> cut = RenderInspector(1L);

        // Assert — initially shows sequence mode with toggle available
        string markup = cut.Markup;
        markup.ShouldContain("Sequence Number");
        markup.ShouldContain("By Timestamp");
        markup.ShouldContain("fluent-switch");
    }

    [Fact]
    public void StateInspectorModal_StaysOpenAfterSubmit() {
        // Arrange
        AggregateStateSnapshot snapshot = new(
            "test-tenant", "counter", "agg-001", 5,
            DateTimeOffset.UtcNow, """{"count": 3}""");
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(snapshot));

        IRenderedComponent<StateInspectorModal> cut = RenderInspector(5L);

        // Act — click Inspect
        AngleSharp.Dom.IElement inspectBtn = cut.Find("fluent-button[appearance='primary']");
        inspectBtn.Click();

        // Assert — modal stays open with result and title still visible
        cut.WaitForAssertion(() => {
            cut.Markup.ShouldContain("State at Sequence");
            cut.Markup.ShouldContain("State Inspector");
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StateInspectorModal_KeepsStreamIdentityVisible() {
        IRenderedComponent<StateInspectorModal> cut = RenderInspector(7L);

        string markup = cut.Markup;
        markup.ShouldContain("test-tenant");
        markup.ShouldContain("counter");
        markup.ShouldContain("agg-001");
        markup.ShouldContain("#7");
    }

    [Fact]
    public void StateInspectorModal_ShowsBackendErrorMessage_OnServiceUnavailable() {
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns<Task<AggregateStateSnapshot?>>(_ => throw new Hexalith.EventStore.Admin.UI.Services.Exceptions.ServiceUnavailableException("backend"));

        IRenderedComponent<StateInspectorModal> cut = RenderInspector(3L);
        AngleSharp.Dom.IElement inspectBtn = cut.Find("fluent-button[appearance='primary']");
        inspectBtn.Click();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Backend unavailable"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StateInspectorModal_ShowsSignInError_OnUnauthorized() {
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns<Task<AggregateStateSnapshot?>>(_ => throw new UnauthorizedAccessException());

        IRenderedComponent<StateInspectorModal> cut = RenderInspector(3L);
        AngleSharp.Dom.IElement inspectBtn = cut.Find("fluent-button[appearance='primary']");
        inspectBtn.Click();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Sign in required"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StateInspectorModal_Dw7_ShowFailureDoesNotRetryAndClosesHostOnce() {
        int showAttempts = 0;
        int closeNotifications = 0;
        StateInspectorModal.ShowDialogAsyncOverride = _ => {
            showAttempts++;
            throw new JSDisconnectedException("simulated dialog show failure");
        };

        try {
            IRenderedComponent<StateInspectorModal> cut = RenderInspector(42L, () => closeNotifications++);

            cut.WaitForAssertion(() => showAttempts.ShouldBe(1), TimeSpan.FromSeconds(5));

            cut.Render();

            cut.WaitForAssertion(() => {
                showAttempts.ShouldBe(1);
                closeNotifications.ShouldBe(1);
            }, TimeSpan.FromSeconds(5));
        }
        finally {
            StateInspectorModal.ShowDialogAsyncOverride = null;
        }
    }

    [Fact]
    public void StateInspectorModal_Dw7_ShowFailureRendersExistingErrorInFlatBodyAndLogsMetadataOnly() {
        CapturingStateInspectorLogger logger = new();
        _ = Services.RemoveAll<ILogger<StateInspectorModal>>();
        _ = Services.AddSingleton<ILogger<StateInspectorModal>>(logger);

        StateInspectorModal.ShowDialogAsyncOverride = _ => throw new JSDisconnectedException("simulated dialog show failure");

        try {
            IRenderedComponent<StateInspectorModal> cut = RenderInspector(42L);

            cut.WaitForAssertion(() => {
                cut.Markup.ShouldContain("Could not open the state inspector dialog. Try again.");
                AngleSharp.Dom.IElement body = cut.Find("fluent-dialog-body");
                AngleSharp.Dom.IElement? contentWrapper = body.QuerySelector(".state-inspector-modal-body");
                _ = contentWrapper.ShouldNotBeNull();
                AngleSharp.Dom.IElement? error = contentWrapper.QuerySelector(".state-inspector-error");
                _ = error.ShouldNotBeNull();
                error.TextContent.ShouldContain("Could not open the state inspector dialog. Try again.");
            }, TimeSpan.FromSeconds(5));

            CapturedLog log = logger.Entries.Single(e => e.Level == LogLevel.Warning);
            log.Message.ShouldContain("test-tenant");
            log.Message.ShouldContain("counter");
            log.Message.ShouldContain("agg-001");
            log.Message.ShouldNotContain("{\"count\"");
            log.Message.ShouldNotContain("Bearer ");
            log.Message.ShouldNotContain("localStorage");
        }
        finally {
            StateInspectorModal.ShowDialogAsyncOverride = null;
        }
    }

    [Fact]
    public void StateInspectorModal_Dw7_ParentRemovalCallbackDoesNotReenterShowOrClose() {
        int showAttempts = 0;
        StateInspectorModal.ShowDialogAsyncOverride = _ => {
            showAttempts++;
            throw new JSDisconnectedException("simulated dialog show failure");
        };

        try {
            IRenderedComponent<StateInspectorFailureHost> host = Render<StateInspectorFailureHost>();

            host.WaitForAssertion(() => {
                showAttempts.ShouldBe(1);
                host.Instance.CloseNotifications.ShouldBe(1);
                host.Markup.ShouldNotContain("State Inspector");
            }, TimeSpan.FromSeconds(5));

            host.Render();

            host.WaitForAssertion(() => {
                showAttempts.ShouldBe(1);
                host.Instance.CloseNotifications.ShouldBe(1);
            }, TimeSpan.FromSeconds(5));
        }
        finally {
            StateInspectorModal.ShowDialogAsyncOverride = null;
        }
    }

    [Fact]
    public void StateInspectorModal_Dw7_ShowFailureTestSeamStaysInternal() {
        System.Reflection.PropertyInfo? seam = typeof(StateInspectorModal)
            .GetProperty(nameof(StateInspectorModal.ShowDialogAsyncOverride), PrivateStatic);

        _ = seam.ShouldNotBeNull();
        seam.GetMethod!.IsPublic.ShouldBeFalse();
        seam.GetMethod!.IsStatic.ShouldBeTrue();
        seam.SetMethod!.IsPublic.ShouldBeFalse();
        seam.SetMethod!.IsStatic.ShouldBeTrue();
    }

    private IRenderedComponent<StateInspectorModal> RenderInspector(long? seq) => Render<StateInspectorModal>(p => p
                                                                                           .Add(c => c.TenantId, "test-tenant")
                                                                                           .Add(c => c.Domain, "counter")
                                                                                           .Add(c => c.AggregateId, "agg-001")
                                                                                           .Add(c => c.InitialSequenceNumber, seq));

    private IRenderedComponent<StateInspectorModal> RenderInspector(long? seq, Action onClose) => Render<StateInspectorModal>(p => p
                                                                                           .Add(c => c.TenantId, "test-tenant")
                                                                                           .Add(c => c.Domain, "counter")
                                                                                           .Add(c => c.AggregateId, "agg-001")
                                                                                           .Add(c => c.InitialSequenceNumber, seq)
                                                                                           .Add(c => c.OnClose, EventCallback.Factory.Create(this, onClose)));

    private sealed class StateInspectorFailureHost : ComponentBase {
        private bool _show = true;

        public int CloseNotifications { get; private set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) {
            if (!_show) {
                return;
            }

            builder.OpenComponent<StateInspectorModal>(0);
            builder.AddAttribute(1, nameof(StateInspectorModal.TenantId), "test-tenant");
            builder.AddAttribute(2, nameof(StateInspectorModal.Domain), "counter");
            builder.AddAttribute(3, nameof(StateInspectorModal.AggregateId), "agg-001");
            builder.AddAttribute(4, nameof(StateInspectorModal.InitialSequenceNumber), 42L);
            builder.AddAttribute(5, nameof(StateInspectorModal.OnClose), EventCallback.Factory.Create(this, CloseInspector));
            builder.CloseComponent();
        }

        private void CloseInspector() {
            CloseNotifications++;
            _show = false;
            StateHasChanged();
        }
    }

    private sealed record CapturedLog(LogLevel Level, string Message);

    private sealed class CapturingStateInspectorLogger : ILogger<StateInspectorModal> {
        public List<CapturedLog> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new CapturedLog(logLevel, formatter(state, exception)));
    }
}
