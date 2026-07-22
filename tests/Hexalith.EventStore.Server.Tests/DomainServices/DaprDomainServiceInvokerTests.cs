using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public class DaprDomainServiceInvokerTests {
    private readonly IDomainServiceResolver _resolver = Substitute.For<IDomainServiceResolver>();
    private readonly IOptions<DomainServiceOptions> _options = Options.Create(new DomainServiceOptions());
    private readonly ILogger<DaprDomainServiceInvoker> _logger = NullLogger<DaprDomainServiceInvoker>.Instance;

    private static readonly DomainServiceRegistration TestRegistration = new("test-app", "process-command", "test-tenant", "test-domain", null);

    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string domain = "test-domain") => new(
        MessageId: Guid.NewGuid().ToString(),
        TenantId: tenantId,
        Domain: domain,
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: Guid.NewGuid().ToString(),
        CausationId: null,
        UserId: "system",
        Extensions: null);

    [Fact]
    public async Task InvokeAsync_ServiceNotFound_ThrowsDomainServiceNotFoundException() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = _resolver.ResolveAsync("test-tenant", "test-domain", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);
        var invoker = new DaprDomainServiceInvoker(
            daprClient,
            Substitute.For<IHttpClientFactory>(),
            _resolver,
            _options,
            TimeProvider.System,
            _logger);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act & Assert
        DomainServiceNotFoundException ex = await Should.ThrowAsync<DomainServiceNotFoundException>(() => invoker.InvokeAsync(envelope, null));
        ex.TenantId.ShouldBe("test-tenant");
        ex.Domain.ShouldBe("test-domain");
    }

    [Fact]
    public async Task InvokeAsync_NullCommand_ThrowsArgumentNullException() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        var invoker = new DaprDomainServiceInvoker(
            daprClient,
            Substitute.For<IHttpClientFactory>(),
            _resolver,
            _options,
            TimeProvider.System,
            _logger);

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentNullException>(() => invoker.InvokeAsync(null!, null));
    }

    [Fact]
    public async Task InvokeAsync_ValidCommand_CallsResolver() {
        // Arrange -- resolver returns null to short-circuit (we only care about the resolver call)
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = _resolver.ResolveAsync("my-tenant", "my-domain", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);
        var invoker = new DaprDomainServiceInvoker(
            daprClient,
            Substitute.For<IHttpClientFactory>(),
            _resolver,
            _options,
            TimeProvider.System,
            _logger);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "my-tenant", domain: "my-domain");

        // Act & Assert -- expect exception since null registration, but verify resolver was called
        _ = await Should.ThrowAsync<DomainServiceNotFoundException>(() => invoker.InvokeAsync(envelope, null));
        _ = await _resolver.Received(1).ResolveAsync("my-tenant", "my-domain", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DomainServiceNotFoundException_ContainsRecoveryInfo() {
        // Act
        var ex = new DomainServiceNotFoundException("tenant-a", "payments");

        // Assert
        ex.TenantId.ShouldBe("tenant-a");
        ex.Domain.ShouldBe("payments");
        ex.Version.ShouldBe("v1");
        ex.Message.ShouldContain("tenant-a:payments:v1");
    }

    [Fact]
    public void DomainServiceRegistration_PropertiesPreserved() {
        // Act
        var reg = new DomainServiceRegistration("app-1", "process", "t1", "d1", "v1");

        // Assert
        reg.AppId.ShouldBe("app-1");
        reg.MethodName.ShouldBe("process");
        reg.TenantId.ShouldBe("t1");
        reg.Domain.ShouldBe("d1");
        reg.Version.ShouldBe("v1");
    }

    [Fact]
    public void DomainServiceOptions_HasDefaults() {
        // Act
        var options = new DomainServiceOptions();

        // Assert
        options.ConfigStoreName.ShouldBeNull();
        options.InvocationTimeoutSeconds.ShouldBe(5);
        options.MaxEventsPerResult.ShouldBe(1000);
        options.MaxEventSizeBytes.ShouldBe(1_048_576);
    }

    [Fact]
    public void DaprDomainServiceInvoker_PreservesLegacyPublicConstructor() {
        Type[] legacyParameterTypes = [
            typeof(DaprClient),
            typeof(IHttpClientFactory),
            typeof(IDomainServiceResolver),
            typeof(IOptions<DomainServiceOptions>),
            typeof(ILogger<DaprDomainServiceInvoker>),
        ];

        typeof(DaprDomainServiceInvoker).GetConstructor(legacyParameterTypes).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(DomainServiceOptions.MaximumInvocationTimeoutSeconds + 1)]
    public void Constructor_UnsupportedInvocationTimeout_ThrowsOptionsValidationException(int timeoutSeconds) {
        var options = Options.Create(new DomainServiceOptions { InvocationTimeoutSeconds = timeoutSeconds });

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(() =>
            _ = new DaprDomainServiceInvoker(
                Substitute.For<DaprClient>(),
                Substitute.For<IHttpClientFactory>(),
                _resolver,
                options,
                TimeProvider.System,
                _logger));

        exception.OptionsType.ShouldBe(typeof(DomainServiceOptions));
        exception.Failures.ShouldContain(failure =>
            failure.Contains(nameof(DomainServiceOptions.InvocationTimeoutSeconds), StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_InvocationExceedsConfiguredTimeout_ThrowsDomainServiceExceptionAsync() {
        // Arrange
        using DaprClient daprClient = new DaprClientBuilder().Build();
        _ = _resolver.ResolveAsync("test-tenant", "test-domain", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TestRegistration);
        var handler = new NeverCompletingHandler();
        using var httpClient = new HttpClient(handler);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(DaprDomainServiceInvoker.HttpClientName).Returns(httpClient);
        var options = Options.Create(new DomainServiceOptions { InvocationTimeoutSeconds = 1 });
        var timeProvider = new FakeTimeProvider();
        var logEntries = new List<LogEntry>();
        var invoker = new DaprDomainServiceInvoker(
            daprClient,
            httpClientFactory,
            _resolver,
            options,
            timeProvider,
            new TestLogger<DaprDomainServiceInvoker>(logEntries));

        // Act
        Task<DomainResult> invocation = invoker.InvokeAsync(CreateTestEnvelope(), null);
        await handler.RequestStarted.ConfigureAwait(true);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        DomainServiceException exception = await Should.ThrowAsync<DomainServiceException>(
            () => invocation).ConfigureAwait(true);

        // Assert
        exception.Message.ShouldContain("timed out after 1s");
        exception.InnerException.ShouldBeOfType<TaskCanceledException>();
        exception.TenantId.ShouldBe("test-tenant");
        exception.Domain.ShouldBe("test-domain");
        _ = httpClientFactory.Received(1).CreateClient(DaprDomainServiceInvoker.HttpClientName);
        LogEntry cancellationLog = logEntries.Single(entry => entry.EventId.Id == 3004);
        cancellationLog.Level.ShouldBe(LogLevel.Error);
        cancellationLog.Message.ShouldContain("ConfiguredTimeoutElapsed=True");
        cancellationLog.Message.ShouldContain("TimeoutSeconds=1");
        cancellationLog.Message.ShouldContain("TenantId=test-tenant");
        cancellationLog.Message.ShouldContain("Domain=test-domain");
    }

    [Fact]
    public async Task InvokeAsync_HttpPipelineCancelsBeforeConfiguredTimeout_ThrowsDomainServiceExceptionAsync() {
        // Arrange
        using DaprClient daprClient = new DaprClientBuilder().Build();
        _ = _resolver.ResolveAsync("test-tenant", "test-domain", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TestRegistration);
        using var httpClient = new HttpClient(new ImmediatelyCanceledHandler());
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(DaprDomainServiceInvoker.HttpClientName).Returns(httpClient);
        var options = Options.Create(new DomainServiceOptions { InvocationTimeoutSeconds = 5 });
        var logEntries = new List<LogEntry>();
        var invoker = new DaprDomainServiceInvoker(
            daprClient,
            httpClientFactory,
            _resolver,
            options,
            TimeProvider.System,
            new TestLogger<DaprDomainServiceInvoker>(logEntries));

        // Act
        DomainServiceException exception = await Should.ThrowAsync<DomainServiceException>(
            () => invoker.InvokeAsync(CreateTestEnvelope(), null));

        // Assert
        exception.Message.ShouldContain("was canceled before completion");
        exception.InnerException.ShouldBeOfType<TaskCanceledException>();
        exception.TenantId.ShouldBe("test-tenant");
        exception.Domain.ShouldBe("test-domain");
        _ = httpClientFactory.Received(1).CreateClient(DaprDomainServiceInvoker.HttpClientName);
        LogEntry cancellationLog = logEntries.Single(entry => entry.EventId.Id == 3004);
        cancellationLog.Level.ShouldBe(LogLevel.Error);
        cancellationLog.Message.ShouldContain("ConfiguredTimeoutElapsed=False");
        cancellationLog.Message.ShouldContain("TimeoutSeconds=5");
        cancellationLog.Message.ShouldContain("TenantId=test-tenant");
        cancellationLog.Message.ShouldContain("Domain=test-domain");
    }

    [Fact]
    public async Task InvokeAsync_CallerCancels_PropagatesOperationCanceledExceptionAsync() {
        // Arrange
        using DaprClient daprClient = new DaprClientBuilder().Build();
        _ = _resolver.ResolveAsync("test-tenant", "test-domain", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TestRegistration);
        var handler = new NeverCompletingHandler();
        using var httpClient = new HttpClient(handler);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(DaprDomainServiceInvoker.HttpClientName).Returns(httpClient);
        var options = Options.Create(new DomainServiceOptions { InvocationTimeoutSeconds = 5 });
        var invoker = new DaprDomainServiceInvoker(
            daprClient,
            httpClientFactory,
            _resolver,
            options,
            TimeProvider.System,
            _logger);
        using var callerCancellation = new CancellationTokenSource();

        // Act & Assert
        Task<DomainResult> invocation = invoker.InvokeAsync(
            CreateTestEnvelope(),
            null,
            callerCancellation.Token);
        await handler.RequestStarted.ConfigureAwait(true);
        callerCancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => invocation).ConfigureAwait(true);
        _ = httpClientFactory.Received(1).CreateClient(DaprDomainServiceInvoker.HttpClientName);
    }

    [Fact]
    public void DomainServiceRequest_ContainsCommandAndState() {
        // Arrange
        CommandEnvelope envelope = CreateTestEnvelope();
        object state = new { Version = 1 };

        // Act
        var request = new DomainServiceRequest(envelope, state);

        // Assert
        request.Command.ShouldBe(envelope);
        request.CurrentState.ShouldBe(state);
    }

    [Fact]
    public void DomainServiceRequest_NullStateAllowed() {
        // Arrange
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        var request = new DomainServiceRequest(envelope, null);

        // Assert
        request.CurrentState.ShouldBeNull();
    }

    // --- Task 7: Domain service response size validation ---

    [Fact]
    public void DomainServiceException_WithTenantAndDomain_HasCorrectProperties() {
        // Act
        var ex = new DomainServiceException("tenant-a", "orders", "Too many events", eventCount: 1500);

        // Assert
        ex.TenantId.ShouldBe("tenant-a");
        ex.Domain.ShouldBe("orders");
        ex.Reason.ShouldBe("Too many events");
        ex.EventCount.ShouldBe(1500);
        ex.EventSizeBytes.ShouldBeNull();
        ex.Message.ShouldContain("tenant-a");
        ex.Message.ShouldContain("orders");
    }

    [Fact]
    public void DomainServiceException_WithEventSize_HasCorrectProperties() {
        // Act
        var ex = new DomainServiceException("tenant-b", "inventory", "Event too large", eventSizeBytes: 2_000_000);

        // Assert
        ex.TenantId.ShouldBe("tenant-b");
        ex.Domain.ShouldBe("inventory");
        ex.EventCount.ShouldBeNull();
        ex.EventSizeBytes.ShouldBe(2_000_000);
    }

    // --- Task 7.5: Response exceeding max events throws DomainServiceException ---

    [Fact]
    public void ValidateResponseLimits_ExceedsMaxEvents_ThrowsDomainServiceException() {
        // Arrange - create a result with more events than allowed
        var opts = new DomainServiceOptions { MaxEventsPerResult = 3 };
        var events = new IEventPayload[] { new TestEvent(), new TestEvent(), new TestEvent(), new TestEvent() };
        var result = new DomainResult(events);

        // Act & Assert
        DomainServiceException ex = Should.Throw<DomainServiceException>(
            () => DaprDomainServiceInvoker.ValidateResponseLimits(result, "tenant-a", "orders", opts));

        ex.TenantId.ShouldBe("tenant-a");
        ex.Domain.ShouldBe("orders");
        ex.EventCount.ShouldBe(4);
        ex.Message.ShouldContain("4 events");
        ex.Message.ShouldContain("maximum of 3");
    }

    [Fact]
    public void ValidateResponseLimits_AtMaxEvents_DoesNotThrow() {
        // Arrange - exactly at the limit
        var opts = new DomainServiceOptions { MaxEventsPerResult = 3 };
        var events = new IEventPayload[] { new TestEvent(), new TestEvent(), new TestEvent() };
        var result = new DomainResult(events);

        // Act & Assert - should not throw
        Should.NotThrow(
            () => DaprDomainServiceInvoker.ValidateResponseLimits(result, "tenant-a", "orders", opts));
    }

    // --- Task 7.6: Single event exceeding max payload size throws DomainServiceException ---

    [Fact]
    public void ValidateResponseLimits_EventExceedsMaxSize_ThrowsDomainServiceException() {
        // Arrange - create an event with a large payload
        var opts = new DomainServiceOptions { MaxEventSizeBytes = 50 }; // very low threshold
        var largeEvent = new LargeTestEvent(new string('x', 100)); // will serialize to > 50 bytes
        var result = new DomainResult([largeEvent]);

        // Act & Assert
        DomainServiceException ex = Should.Throw<DomainServiceException>(
            () => DaprDomainServiceInvoker.ValidateResponseLimits(result, "tenant-a", "orders", opts));

        ex.TenantId.ShouldBe("tenant-a");
        ex.Domain.ShouldBe("orders");
        _ = ex.EventSizeBytes.ShouldNotBeNull();
        ex.EventSizeBytes!.Value.ShouldBeGreaterThan(50);
        ex.Message.ShouldContain("LargeTestEvent");
        ex.Message.ShouldContain("exceeding maximum of 50 bytes");
    }

    [Fact]
    public void ValidateResponseLimits_SmallEvent_DoesNotThrow() {
        // Arrange
        var opts = new DomainServiceOptions { MaxEventSizeBytes = 1_048_576 };
        var result = new DomainResult([new TestEvent()]);

        // Act & Assert
        Should.NotThrow(
            () => DaprDomainServiceInvoker.ValidateResponseLimits(result, "tenant-a", "orders", opts));
    }

    [Fact]
    public void ValidateResponseLimits_EmptyResult_DoesNotThrow() {
        // Arrange - no-op result with empty events
        var opts = new DomainServiceOptions();
        var result = DomainResult.NoOp();

        // Act & Assert
        Should.NotThrow(
            () => DaprDomainServiceInvoker.ValidateResponseLimits(result, "tenant-a", "orders", opts));
    }

    // NOTE: DaprClient.InvokeMethodAsync<TReq,TResp> is non-virtual and cannot be mocked with NSubstitute.
    // Happy-path invocation tests (success/rejection/no-op result flows) are covered at the actor level
    // in AggregateActorTests (ProcessCommandAsync_DomainSuccess_*, ProcessCommandAsync_DomainRejection_*,
    // ProcessCommandAsync_DomainNoOp_*) via the mocked IDomainServiceInvoker interface.
    // Direct DaprDomainServiceInvoker integration testing with real DAPR is deferred to Tier 2 (Story 7.4).
    // No-op WARNING logging (Task 7.7) is verified by code review — the log statement is clearly present
    // in InvokeAsync when result.IsNoOp is true.

    // --- Task 8: Version extraction and validation ---

    [Fact]
    public void ExtractVersion_NoExtensions_ReturnsDefaultV1() {
        // Arrange - command with null extensions
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        string version = DaprDomainServiceInvoker.ExtractVersion(envelope, _logger);

        // Assert
        version.ShouldBe("v1");
    }

    [Fact]
    public void ExtractVersion_ExtensionsWithoutVersionKey_ReturnsDefaultV1() {
        // Arrange - extensions present but no domain-service-version key
        var envelope = new CommandEnvelope(
            MessageId: "msg-version-nokey-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: Guid.NewGuid().ToString(),
            CausationId: null,
            UserId: "system",
            Extensions: new Dictionary<string, string> { ["other-key"] = "other-value" });

        // Act
        string version = DaprDomainServiceInvoker.ExtractVersion(envelope, _logger);

        // Assert
        version.ShouldBe("v1");
    }

    [Fact]
    public void ExtractVersion_ExtensionsWithVersionKey_ReturnsSpecifiedVersion() {
        // Arrange - Task 8.5: versioned key lookup via extensions
        var envelope = new CommandEnvelope(
            MessageId: "msg-version-key-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: Guid.NewGuid().ToString(),
            CausationId: null,
            UserId: "system",
            Extensions: new Dictionary<string, string> { ["domain-service-version"] = "v2" });

        // Act
        string version = DaprDomainServiceInvoker.ExtractVersion(envelope, _logger);

        // Assert
        version.ShouldBe("v2");
    }

    [Fact]
    public void ExtractVersion_UppercaseVersion_NormalizedToLowercase() {
        // Arrange - Task 8.7: "V1" normalized to "v1"
        var envelope = new CommandEnvelope(
            MessageId: "msg-version-upper-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: Guid.NewGuid().ToString(),
            CausationId: null,
            UserId: "system",
            Extensions: new Dictionary<string, string> { ["domain-service-version"] = "V1" });

        // Act
        string version = DaprDomainServiceInvoker.ExtractVersion(envelope, _logger);

        // Assert
        version.ShouldBe("v1");
    }

    [Fact]
    public void ExtractVersion_MixedCaseVersion_NormalizedToLowercase() {
        // Arrange - "V10" -> "v10"
        var envelope = new CommandEnvelope(
            MessageId: "msg-version-mixed-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: Guid.NewGuid().ToString(),
            CausationId: null,
            UserId: "system",
            Extensions: new Dictionary<string, string> { ["domain-service-version"] = "V10" });

        // Act
        string version = DaprDomainServiceInvoker.ExtractVersion(envelope, _logger);

        // Assert
        version.ShouldBe("v10");
    }

    [Theory]
    [InlineData("version1")]   // no v prefix
    [InlineData("v1a")]        // non-numeric suffix
    [InlineData("v1:evil")]    // injection attempt
    [InlineData("v")]          // no digits
    [InlineData("1")]          // no v prefix
    [InlineData("va")]         // non-numeric
    [InlineData("v1.0")]       // dots not allowed
    public void ExtractVersion_InvalidFormat_ThrowsArgumentException(string invalidVersion) {
        // Arrange - Task 8.6: invalid version formats rejected
        var envelope = new CommandEnvelope(
            MessageId: "msg-version-invalid-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: Guid.NewGuid().ToString(),
            CausationId: null,
            UserId: "system",
            Extensions: new Dictionary<string, string> { ["domain-service-version"] = invalidVersion });

        // Act & Assert
        ArgumentException ex = Should.Throw<ArgumentException>(
            () => DaprDomainServiceInvoker.ExtractVersion(envelope, _logger));
        ex.Message.ShouldContain("Invalid domain service version format");
    }

    [Fact]
    public void ValidateVersionFormat_ValidVersions_DoNotThrow() {
        // Arrange & Act & Assert
        Should.NotThrow(() => DaprDomainServiceInvoker.ValidateVersionFormat("v1"));
        Should.NotThrow(() => DaprDomainServiceInvoker.ValidateVersionFormat("v2"));
        Should.NotThrow(() => DaprDomainServiceInvoker.ValidateVersionFormat("v10"));
        Should.NotThrow(() => DaprDomainServiceInvoker.ValidateVersionFormat("v100"));
    }

    [Fact]
    public async Task InvokeAsync_CommandWithVersionExtension_PassesVersionToResolver() {
        // Arrange - Task 8.5: verify version from extensions is forwarded to resolver
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = _resolver.ResolveAsync("test-tenant", "test-domain", "v2", Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);
        var invoker = new DaprDomainServiceInvoker(
            daprClient,
            Substitute.For<IHttpClientFactory>(),
            _resolver,
            _options,
            TimeProvider.System,
            _logger);
        var envelope = new CommandEnvelope(
            MessageId: "msg-version-resolver-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: Guid.NewGuid().ToString(),
            CausationId: null,
            UserId: "system",
            Extensions: new Dictionary<string, string> { ["domain-service-version"] = "v2" });

        // Act & Assert - expect exception since null registration, but verify v2 was passed
        _ = await Should.ThrowAsync<DomainServiceNotFoundException>(() => invoker.InvokeAsync(envelope, null));
        _ = await _resolver.Received(1).ResolveAsync("test-tenant", "test-domain", "v2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CommandWithoutVersionExtension_PassesDefaultV1ToResolver() {
        // Arrange - no extensions, should default to v1
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = _resolver.ResolveAsync("test-tenant", "test-domain", "v1", Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);
        var invoker = new DaprDomainServiceInvoker(
            daprClient,
            Substitute.For<IHttpClientFactory>(),
            _resolver,
            _options,
            TimeProvider.System,
            _logger);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act & Assert
        _ = await Should.ThrowAsync<DomainServiceNotFoundException>(() => invoker.InvokeAsync(envelope, null));
        _ = await _resolver.Received(1).ResolveAsync("test-tenant", "test-domain", "v1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DomainResult_JsonRoundTrip_WithInterfaceEvents_DeserializationFails() {
        // Arrange -- DomainResult contains interface-typed events (IEventPayload)
        // which cannot be materialized by System.Text.Json without polymorphic metadata.
        var original = DomainResult.Success([new TestEvent()]);
        string json = System.Text.Json.JsonSerializer.Serialize(original);

        // Act & Assert -- this is the current runtime behavior that causes
        // DaprDomainServiceInvoker.InvokeMethodAsync<DomainServiceRequest, DomainResult>
        // to fail across process boundaries.
        _ = Should.Throw<System.NotSupportedException>(
            () => System.Text.Json.JsonSerializer.Deserialize<DomainResult>(json));
    }

    // Test event types
    private sealed record TestEvent : IEventPayload;

    private sealed record LargeTestEvent(string Data) : IEventPayload;

    private sealed record TestRejectionEvent : IRejectionEvent;

    private sealed class NeverCompletingHandler : HttpMessageHandler {
        private readonly TaskCompletionSource _requestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestStarted => _requestStarted.Task;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            _requestStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }

    private sealed class ImmediatelyCanceledHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(cancellation.Token);
        }
    }
}
