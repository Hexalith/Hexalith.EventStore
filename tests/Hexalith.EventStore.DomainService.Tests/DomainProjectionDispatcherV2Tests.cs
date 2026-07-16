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

    [Theory]
    [InlineData("", "widget-1")]
    [InlineData("tenant-a", " ")]
    public async Task DispatchAsync_RejectsBlankAggregateIdentityBeforeAnyInvocation(
        string tenantId,
        string aggregateId) {
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        using ServiceProvider provider = BuildProvider(handler);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "widget-detail");
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest(tenantId, "widget", aggregateId, []),
            ["widget-detail"],
            "dispatch-1",
            "fingerprint-1");

        ProjectionDispatchValidationException exception = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionDispatcher.DispatchAsync(
                provider,
                request,
                new ProjectionDispatchOptions(),
                registry,
                CancellationToken.None));

        exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
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

    [Fact]
    public async Task DispatchAsync_OverLimitEnvelopeMapsOutcomeToBoundedSafeFailure() {
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DomainProjectionHandlerResult.Completed(
                JsonSerializer.SerializeToElement(new { content = new string('x', 1024) })));
        using ServiceProvider provider = BuildProvider(handler);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "widget-detail");
        var options = new ProjectionDispatchOptions {
            MaxHandlersPerDomain = 1,
            MaxOutcomes = 1,
            MaxOutcomeEnvelopeBytes = 256,
        };

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            CreateRequest("fingerprint-1", "widget-detail"),
            options,
            registry,
            CancellationToken.None);

        ProjectionDispatchOutcome outcome = response.Outcomes.ShouldHaveSingleItem();
        outcome.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        outcome.State.ShouldBeNull();
        outcome.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
        JsonSerializer.SerializeToUtf8Bytes(response).Length.ShouldBeLessThanOrEqualTo(options.MaxOutcomeEnvelopeBytes);
    }

    [Fact]
    public async Task DispatchAsync_AccumulatedEnvelopePressureStillReturnsOneSafeOutcomePerRoute() {
        IAsyncDomainProjectionHandler first = CreateHandler("widget", "a-first");
        IAsyncDomainProjectionHandler second = CreateHandler("widget", "b-second");
        first.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DomainProjectionHandlerResult.Completed(
                JsonSerializer.SerializeToElement(new { content = new string('a', 1024) })));
        second.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DomainProjectionHandlerResult.Completed(
                JsonSerializer.SerializeToElement(new { content = new string('b', 1024) })));
        using ServiceProvider provider = BuildProvider(first, second);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "a-first", "b-second");
        var options = new ProjectionDispatchOptions {
            MaxHandlersPerDomain = 2,
            MaxOutcomes = 2,
            MaxOutcomeEnvelopeBytes = ProjectionDispatchOptions.GetMinimumOutcomeEnvelopeBytes(2) + 16,
        };

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            CreateRequest("fingerprint-1", "a-first", "b-second"),
            options,
            registry,
            CancellationToken.None);

        response.Outcomes.Count.ShouldBe(2);
        response.Outcomes.ShouldAllBe(static outcome => outcome.Status == ProjectionDispatchStatus.Failed);
        JsonSerializer.SerializeToUtf8Bytes(response, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Length.ShouldBeLessThanOrEqualTo(options.MaxOutcomeEnvelopeBytes);
    }

    [Fact]
    public async Task DispatchAsync_StateExceedingSerializerDepthMapsToSafeFailure() {
        string deeplyNestedJson = $"{new string('[', 80)}0{new string(']', 80)}";
        using JsonDocument document = JsonDocument.Parse(
            deeplyNestedJson,
            new JsonDocumentOptions { MaxDepth = 128 });
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DomainProjectionHandlerResult.Completed(document.RootElement.Clone()));
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

    [Fact]
    public async Task DispatchAsync_DoesNotCompleteBeforeDurableHandlerBarrier() {
        var barrier = new TaskCompletionSource<DomainProjectionHandlerResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), "dispatch-1", Arg.Any<CancellationToken>())
            .Returns(barrier.Task);
        using ServiceProvider provider = BuildProvider(handler);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "widget-detail");

        Task<ProjectionDispatchResponse> dispatch = DomainProjectionDispatcher.DispatchAsync(
            provider,
            CreateRequest("fingerprint-1", "widget-detail"),
            new ProjectionDispatchOptions(),
            registry,
            CancellationToken.None);

        dispatch.IsCompleted.ShouldBeFalse();
        barrier.SetResult(DomainProjectionHandlerResult.Completed());
        ProjectionDispatchResponse response = await dispatch;

        response.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        _ = handler.Received(1).ProjectAsync(
            Arg.Any<ProjectionRequest>(),
            "dispatch-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_HandlerLocalCancellationMapsFailureAndContinues() {
        IAsyncDomainProjectionHandler first = CreateHandler("widget", "a-first");
        first.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<DomainProjectionHandlerResult>>(_ => throw new OperationCanceledException("handler-local"));
        IAsyncDomainProjectionHandler second = CreateHandler("widget", "b-second");
        second.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DomainProjectionHandlerResult.Completed());
        using ServiceProvider provider = BuildProvider(first, second);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "a-first", "b-second");

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            CreateRequest("fingerprint-1", "a-first", "b-second"),
            new ProjectionDispatchOptions(),
            registry,
            CancellationToken.None);

        response.Outcomes[0].Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        response.Outcomes[1].Status.ShouldBe(ProjectionDispatchStatus.Completed);
        _ = second.Received(1).ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_AuthoritativeFingerprintWorksWithoutProcessLocalIssuanceHistory() {
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DomainProjectionHandlerResult.Completed());
        using ServiceProvider provider = BuildProvider(handler);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]);

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            CreateRequest(fingerprint, "widget-detail"),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);

        response.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
    }

    [Fact]
    public async Task DispatchAsync_NullEventArrayIsRejectedBeforeHandlerInvocation() {
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        using ServiceProvider provider = BuildProvider(handler);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "widget-detail");
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "widget", "widget-1", null!),
            ["widget-detail"],
            "dispatch-1",
            "fingerprint-1");

        ProjectionDispatchValidationException exception = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionDispatcher.DispatchAsync(
                provider,
                request,
                new ProjectionDispatchOptions(),
                registry,
                CancellationToken.None));

        exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
        _ = handler.DidNotReceiveWithAnyArgs().ProjectAsync(default!, default!, default);
    }

    [Fact]
    public async Task DispatchAsync_NullEventElementIsRejectedBeforeHandlerInvocation() {
        IAsyncDomainProjectionHandler handler = CreateHandler("widget", "widget-detail");
        using ServiceProvider provider = BuildProvider(handler);
        DomainProjectionCatalogRegistry registry = CreateRegistry("fingerprint-1", "widget-detail");
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "widget", "widget-1", [null!]),
            ["widget-detail"],
            "dispatch-1",
            "fingerprint-1");

        ProjectionDispatchValidationException exception = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionDispatcher.DispatchAsync(
                provider,
                request,
                new ProjectionDispatchOptions(),
                registry,
                CancellationToken.None));

        exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
        _ = handler.DidNotReceiveWithAnyArgs().ProjectAsync(default!, default!, default);
    }

    [Fact]
    public async Task RebuildAsync_FullReplayHandlersPromoteOneCoordinatedBatch() {
        IAsyncDomainProjectionRebuildHandler detail = CreateRebuildHandler("widget", "widget-detail");
        IAsyncDomainProjectionRebuildHandler index = CreateRebuildHandler("widget", "widget-index");
        detail.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), "dispatch-1", Arg.Any<CancellationToken>())
            .Returns(new DomainProjectionRebuildPlan(
                "statestore",
                [ReadModelBatchOperation.Write("widget-1:detail", new Dictionary<string, int> { ["count"] = 2 }, ReadModelBatchConcurrency.LastWrite)]));
        index.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), "dispatch-1", Arg.Any<CancellationToken>())
            .Returns(new DomainProjectionRebuildPlan(
                "statestore",
                [ReadModelBatchOperation.Write("widget-1:index", new Dictionary<string, int> { ["count"] = 2 }, ReadModelBatchConcurrency.LastWrite)]));
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        ReadModelBatch? captured = null;
        _ = batchStore.ExecuteAsync(Arg.Do<ReadModelBatch>(batch => captured = batch), Arg.Any<CancellationToken>())
            .Returns(new ReadModelBatchResult(
                ReadModelBatchStatus.Completed,
                "fingerprint",
                ReadModelBatchConflictKind.None,
                null));
        using ServiceProvider provider = BuildRebuildProvider(batchStore, detail, index);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail"), new ProjectionDispatchRoute("widget", "widget-index")]);

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.RebuildAsync(
            provider,
            CreateRequest(fingerprint, "widget-detail", "widget-index"),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);

        response.Outcomes.Select(static outcome => outcome.ProjectionType)
            .ShouldBe(["widget-detail", "widget-index"]);
        response.Outcomes.ShouldAllBe(static outcome => outcome.Status == ProjectionDispatchStatus.Completed);
        captured.ShouldNotBeNull().Scope.ShouldBe(new ReadModelBatchScope(
            "statestore",
            "tenant-a",
            "widget",
            "widget-1",
            "rebuild",
            "dispatch-1"));
        captured.Operations.Select(static operation => operation.Key)
            .ShouldBe(["widget-1:detail", "widget-1:index"]);
        _ = await batchStore.Received(1).ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
        _ = detail.DidNotReceiveWithAnyArgs().ProjectAsync(default!, default!, default);
        _ = index.DidNotReceiveWithAnyArgs().ProjectAsync(default!, default!, default);
    }

    [Fact]
    public async Task RebuildAsync_OnePreparationFailureDoesNotPromoteAnyRoute() {
        IAsyncDomainProjectionRebuildHandler detail = CreateRebuildHandler("widget", "widget-detail");
        IAsyncDomainProjectionRebuildHandler index = CreateRebuildHandler("widget", "widget-index");
        detail.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainProjectionRebuildPlan(
                "statestore",
                [ReadModelBatchOperation.Write("widget-1:detail", new Dictionary<string, int> { ["count"] = 2 }, ReadModelBatchConcurrency.LastWrite)]));
        index.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<DomainProjectionRebuildPlan>>(_ => throw new InvalidOperationException("sensitive failure"));
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        using ServiceProvider provider = BuildRebuildProvider(batchStore, detail, index);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail"), new ProjectionDispatchRoute("widget", "widget-index")]);

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.RebuildAsync(
            provider,
            CreateRequest(fingerprint, "widget-detail", "widget-index"),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);

        response.Outcomes[0].Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        response.Outcomes[0].ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.PartialRetry);
        response.Outcomes[1].Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        response.Outcomes[1].ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.HandlerFailure);
        _ = await batchStore.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Fact]
    public async Task RebuildAsync_SubsetOfAuthoritativeRoutesIsRejectedBeforePreparation() {
        IAsyncDomainProjectionRebuildHandler detail = CreateRebuildHandler("widget", "widget-detail");
        IAsyncDomainProjectionRebuildHandler index = CreateRebuildHandler("widget", "widget-index");
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        using ServiceProvider provider = BuildRebuildProvider(batchStore, detail, index);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail"), new ProjectionDispatchRoute("widget", "widget-index")]);

        ProjectionDispatchValidationException exception = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionDispatcher.RebuildAsync(
                provider,
                CreateRequest(fingerprint, "widget-detail"),
                new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
                CancellationToken.None));

        exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedRoute);
        _ = detail.DidNotReceiveWithAnyArgs().PrepareRebuildAsync(default!, default!, default);
        _ = index.DidNotReceiveWithAnyArgs().PrepareRebuildAsync(default!, default!, default);
        _ = await batchStore.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Fact]
    public async Task RebuildAsync_NonCanonicalEventHistoriesAreRejectedBeforePreparation() {
        IAsyncDomainProjectionRebuildHandler handler = CreateRebuildHandler("widget", "widget-detail");
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        using ServiceProvider provider = BuildRebuildProvider(batchStore, handler);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]);
        long[][] invalidSequences = [[2], [1, 3], [1, 1], [2, 1]];

        foreach (long[] sequences in invalidSequences) {
            var request = new ProjectionDispatchRequest(
                new ProjectionRequest(
                    "tenant-a",
                    "widget",
                    "widget-1",
                    [.. sequences.Select(ProjectionEvent)]),
                ["widget-detail"],
                "dispatch-1",
                fingerprint);

            ProjectionDispatchValidationException exception = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
                DomainProjectionDispatcher.RebuildAsync(
                    provider,
                    request,
                    new ProjectionDispatchOptions(),
                    new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
                    CancellationToken.None));

            exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        _ = handler.DidNotReceiveWithAnyArgs().PrepareRebuildAsync(default!, default!, default);
        _ = await batchStore.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Fact]
    public async Task RebuildAsync_EventHistoryOverConfiguredBoundIsRejectedBeforePreparation() {
        IAsyncDomainProjectionRebuildHandler handler = CreateRebuildHandler("widget", "widget-detail");
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        using ServiceProvider provider = BuildRebuildProvider(batchStore, handler);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]);
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "widget", "widget-1", [ProjectionEvent(1), ProjectionEvent(2)]),
            ["widget-detail"],
            "dispatch-1",
            fingerprint);

        ProjectionDispatchValidationException exception = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionDispatcher.RebuildAsync(
                provider,
                request,
                new ProjectionDispatchOptions { MaxRebuildEventCount = 1 },
                new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
                CancellationToken.None));

        exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
        _ = handler.DidNotReceiveWithAnyArgs().PrepareRebuildAsync(default!, default!, default);
        _ = await batchStore.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Fact]
    public void DomainProjectionRebuildPlan_OperationsCannotBeMutatedThroughRuntimeListShape() {
        var plan = new DomainProjectionRebuildPlan(
            "statestore",
            [ReadModelBatchOperation.Write(
                "widget-1:detail",
                new Dictionary<string, int> { ["count"] = 2 },
                ReadModelBatchConcurrency.LastWrite)]);
        _ = Should.Throw<InvalidCastException>(() =>
            _ = (ReadModelBatchOperation[])plan.Operations);
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

    [Fact]
    public void ReadModelBatchResultMapper_InconsistentStatusAndConflictUsesSafeDefault() {
        var batchResult = new ReadModelBatchResult(
            ReadModelBatchStatus.Completed,
            "batch-fingerprint",
            ReadModelBatchConflictKind.Identity,
            null);

        DomainProjectionHandlerResult result = ReadModelBatchProjectionResultMapper.Map(batchResult);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.State.ShouldBeNull();
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
    }

    private static IAsyncDomainProjectionHandler CreateHandler(string domain, string projectionType) {
        IAsyncDomainProjectionHandler handler = Substitute.For<IAsyncDomainProjectionHandler>();
        handler.Domain.Returns(domain);
        handler.ProjectionType.Returns(projectionType);
        return handler;
    }

    private static IAsyncDomainProjectionRebuildHandler CreateRebuildHandler(string domain, string projectionType) {
        IAsyncDomainProjectionRebuildHandler handler = Substitute.For<IAsyncDomainProjectionRebuildHandler>();
        handler.Domain.Returns(domain);
        handler.ProjectionType.Returns(projectionType);
        handler.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        return handler;
    }

    private static ServiceProvider BuildProvider(params IAsyncDomainProjectionHandler[] handlers) {
        var services = new ServiceCollection();
        foreach (IAsyncDomainProjectionHandler handler in handlers) {
            _ = services.AddScoped(_ => handler);
        }

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildRebuildProvider(
        IReadModelBatchStore batchStore,
        params IAsyncDomainProjectionRebuildHandler[] handlers) {
        var services = new ServiceCollection();
        _ = services.AddSingleton(batchStore);
        foreach (IAsyncDomainProjectionRebuildHandler handler in handlers) {
            _ = services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
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

    private static ProjectionEventDto ProjectionEvent(long sequence)
        => new(
            "widget-updated",
            [],
            "json",
            sequence,
            DateTimeOffset.UnixEpoch,
            "correlation-1",
            $"message-{sequence}",
            "user-1");
}
