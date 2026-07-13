using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class DomainProjectionDispatcherV2Tests {
    [Fact]
    public async Task DispatchAsync_UsesOrdinalOrderAndContinuesAfterUnexpectedFailure() {
        var calls = new List<string>();
        IAsyncDomainProjectionHandler failing = CreateHandler("widget", "a-failing");
        failing.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => {
                calls.Add("a-failing");
                return Task.FromException<DomainProjectionHandlerResult>(
                    new InvalidOperationException("sensitive failure"));
            });
        IAsyncDomainProjectionHandler completed = CreateHandler("widget", "b-completed");
        completed.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => {
                calls.Add("b-completed");
                return DomainProjectionHandlerResult.Completed(JsonSerializer.SerializeToElement(new { count = 1 }));
            });
        using ServiceProvider provider = BuildProvider(completed, failing);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "a-failing", "b-completed");

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            CreateRequest("fingerprint-1", "b-completed", "a-failing"),
            new ProjectionDispatchOptions(),
            registry,
            CancellationToken.None);

        calls.ShouldBe(["a-failing", "b-completed"]);
        response.Version.ShouldBe(ProjectionDispatchProtocol.Version);
        response.Outcomes.Select(outcome => outcome.ProjectionType).ShouldBe(["a-failing", "b-completed"]);
        response.Outcomes[0].Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        response.Outcomes[0].ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.HandlerFailure);
        response.Outcomes[1].Status.ShouldBe(ProjectionDispatchStatus.Completed);
    }

    [Fact]
    public async Task DispatchAsync_RejectsUnknownRouteBeforeAnyInvocation() {
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        using ServiceProvider provider = BuildProvider(handler);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "widget-detail");

        ProjectionDispatchValidationException exception = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionDispatcher.DispatchAsync(
                provider,
                CreateRequest("fingerprint-1", "unknown-route"),
                new ProjectionDispatchOptions(),
                registry,
                CancellationToken.None));

        exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedRoute);
        _ = handler.DidNotReceive().ProjectAsync(
            Arg.Any<ProjectionRequest>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_RejectsFingerprintMismatchBeforeAnyInvocation() {
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        using ServiceProvider provider = BuildProvider(handler);
        DomainProjectionCatalogRegistry registry = CreateRegistry("current-fingerprint", "widget-detail");

        ProjectionDispatchValidationException exception = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionDispatcher.DispatchAsync(
                provider,
                CreateRequest("stale-fingerprint", "widget-detail"),
                new ProjectionDispatchOptions(),
                registry,
                CancellationToken.None));

        exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
        _ = handler.DidNotReceive().ProjectAsync(
            Arg.Any<ProjectionRequest>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_PropagatesCancellationAndDoesNotStartLaterHandler() {
        using var source = new CancellationTokenSource();
        IAsyncDomainProjectionHandler first = CreateHandler("widget", "a-first");
        first.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => {
                source.Cancel();
                return DomainProjectionHandlerResult.Completed();
            });
        IAsyncDomainProjectionHandler second = CreateHandler("widget", "b-second");
        using ServiceProvider provider = BuildProvider(second, first);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "a-first", "b-second");

        _ = await Should.ThrowAsync<OperationCanceledException>(() => DomainProjectionDispatcher.DispatchAsync(
            provider,
            CreateRequest("fingerprint-1", "a-first", "b-second"),
            new ProjectionDispatchOptions(),
            registry,
            source.Token));

        _ = second.DidNotReceive().ProjectAsync(
            Arg.Any<ProjectionRequest>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_MapsMalformedHandlerStateToSafeFailure() {
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainProjectionHandlerResult(
                ProjectionDispatchStatus.Retryable,
                JsonSerializer.SerializeToElement(new { forbidden = true }),
                ProjectionDispatchReasonCodes.PartialRetry));
        using ServiceProvider provider = BuildProvider(handler);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "widget-detail");

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            CreateRequest("fingerprint-1", "widget-detail"),
            new ProjectionDispatchOptions(),
            registry,
            CancellationToken.None);

        ProjectionDispatchOutcome outcome = response.Outcomes.ShouldHaveSingleItem();
        outcome.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        outcome.State.ShouldBeNull();
        outcome.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
    }

    [Theory]
    [InlineData(ReadModelBatchStatus.Completed, ReadModelBatchConflictKind.None, ProjectionDispatchStatus.Completed)]
    [InlineData(ReadModelBatchStatus.AlreadyCompleted, ReadModelBatchConflictKind.None, ProjectionDispatchStatus.AlreadyCompleted)]
    [InlineData(ReadModelBatchStatus.Conflict, ReadModelBatchConflictKind.Optimistic, ProjectionDispatchStatus.Retryable)]
    [InlineData(ReadModelBatchStatus.Incomplete, ReadModelBatchConflictKind.None, ProjectionDispatchStatus.Retryable)]
    [InlineData(ReadModelBatchStatus.Conflict, ReadModelBatchConflictKind.Identity, ProjectionDispatchStatus.Failed)]
    [InlineData(ReadModelBatchStatus.Indeterminate, ReadModelBatchConflictKind.None, ProjectionDispatchStatus.Indeterminate)]
    public void ReadModelBatchResultMapper_MapsClosedDurableTruth(
        ReadModelBatchStatus batchStatus,
        ReadModelBatchConflictKind conflictKind,
        ProjectionDispatchStatus expected) {
        var batchResult = new ReadModelBatchResult(batchStatus, "batch-fingerprint", conflictKind, null);

        DomainProjectionHandlerResult result = ReadModelBatchProjectionResultMapper.Map(batchResult);

        result.Status.ShouldBe(expected);
    }

    private static IAsyncDomainProjectionHandler CreateHandler(string domain, string projectionType) {
        IAsyncDomainProjectionHandler handler = Substitute.For<IAsyncDomainProjectionHandler>();
        handler.Domain.Returns(domain);
        handler.ProjectionType.Returns(projectionType);
        return handler;
    }

    private static ServiceProvider BuildProvider(params IAsyncDomainProjectionHandler[] handlers) {
        var services = new ServiceCollection();
        foreach (IAsyncDomainProjectionHandler handler in handlers) {
            _ = services.AddScoped(_ => handler);
        }

        return services.BuildServiceProvider();
    }

    private static DomainProjectionCatalogRegistry CreateRegistry(string fingerprint, params string[] projectionTypes) {
        var registry = new DomainProjectionCatalogRegistry();
        registry.Register(fingerprint, [.. projectionTypes.Select(type => new ProjectionDispatchRoute("widget", type))]);
        return registry;
    }

    private static ProjectionDispatchRequest CreateRequest(string fingerprint, params string[] projectionTypes)
        => new(
            new ProjectionRequest("tenant-a", "widget", "widget-1", []),
            projectionTypes,
            "dispatch-1",
            fingerprint);
}
