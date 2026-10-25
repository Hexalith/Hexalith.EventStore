using System.Text.Json;
using System.Security.Cryptography;
using Hexalith.EventStore.Testing.Fakes;

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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OrdinaryAndStreamedPlansPropagateOnlyExplicitPrivateRecoveryOptIn(bool streamed, bool optIn)
    {
        var token = TestContext.Current.CancellationToken;
        var persisted = new InMemoryReadModelStore();
        var handler = Substitute.For<IAsyncDomainProjectionRebuildHandler, IAsyncDomainProjectionStreamedRebuildHandler>();
        handler.Domain.Returns("widget");
        handler.ProjectionType.Returns("widget-detail");
        handler.OrdinaryDeliveryEnabled.Returns(true);
        handler.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        ((IAsyncDomainProjectionStreamedRebuildHandler)handler).RebuildTransportStoreName.Returns("statestore");
        var plan = new DomainProjectionRebuildPlan("statestore",
            [ReadModelBatchOperation.Write("canonical", new { count = 1 }, ReadModelBatchConcurrency.CreateOnly)]) { DiscoverableRecovery = optIn };
        handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(plan);
        ((IAsyncDomainProjectionStreamedRebuildHandler)handler).PrepareStreamedRebuildAsync(Arg.Any<ProjectionRequest>(),
            Arg.Any<IAsyncEnumerable<ProjectionEventDto>>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                await foreach (var item in call.Arg<IAsyncEnumerable<ProjectionEventDto>>()) _ = item;
                return plan;
            });
        ReadModelBatch? observed = null;
        var batches = Substitute.For<IReadModelBatchStore>();
        batches.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            observed = call.Arg<ReadModelBatch>();
            return persisted.ExecuteAsync(observed, call.Arg<CancellationToken>());
        });
        var staging = Substitute.For<IReadModelBatchStagingStore>();
        staging.StageAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            observed = call.Arg<ReadModelBatch>();
            return ((IReadModelBatchStagingStore)persisted).StageAsync(observed, call.Arg<CancellationToken>());
        });
        using var services = new ServiceCollection().AddSingleton<IReadModelStore>(persisted).AddSingleton(batches)
            .AddSingleton(staging).AddSingleton<IAsyncDomainProjectionHandler>(handler).BuildServiceProvider();
        var identity = new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" };
        var options = new ProjectionDispatchOptions();
        var request = CreateRequest(ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion, [new("widget", "widget-detail")]), "widget-detail");
        if (streamed)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request.Request.Events, JsonSerializerOptions.Web);
            request = request with { Request = request.Request with { Events = [], RebuildSourceHeadSequence = 1 }, RebuildTransport = new(1, Convert.ToHexString(SHA256.HashData(bytes))) };
            await DomainProjectionRebuildTransport.StoreAsync(services, new(request, 0, bytes), options, identity, token);
        }
        var result = streamed ? await DomainProjectionDispatcher.StageRebuildAsync(services, request, options, identity, token)
            : await DomainProjectionDispatcher.RebuildAsync(services, request, options, identity, token);
        result.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        observed.ShouldNotBeNull().DiscoverableRecovery.ShouldBe(optIn);
        observed.RetainManifestForRecovery.ShouldBe(optIn);
        observed.OwnerSelections.ShouldBeNull();
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
    public async Task ReconcileAsync_InvokesOnlyExplicitReconciliationHandlers() {
        IAsyncDomainProjectionReconciliationHandler handler =
            Substitute.For<IAsyncDomainProjectionReconciliationHandler>();
        handler.Domain.Returns("widget");
        handler.ProjectionType.Returns("widget-detail");
        handler.OrdinaryDeliveryEnabled.Returns(true);
        handler.ReconcileAsync(Arg.Any<ProjectionRequest>(), "dispatch-1", Arg.Any<CancellationToken>())
            .Returns(DomainProjectionHandlerResult.Completed());
        using ServiceProvider provider = BuildProvider(handler);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]);

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.ReconcileAsync(
            provider,
            CreateRequest(fingerprint, "widget-detail"),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);

        response.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        _ = await handler.Received(1).ReconcileAsync(
            Arg.Any<ProjectionRequest>(),
            "dispatch-1",
            Arg.Any<CancellationToken>());
        _ = handler.DidNotReceiveWithAnyArgs().ProjectAsync(default!, default!, default);
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

    [Theory]
    [InlineData("duplicate-canonical")]
    [InlineData("canonical-over-limit")]
    [InlineData("late-invalid-generation")]
    public async Task RebuildRejectsMalformedCombinedPlansBeforeAnyGenerationWrites(string defect)
    {
        var handler = CreateRebuildHandler("widget", "widget-detail");
        ReadModelBatchOperation write = ReadModelBatchOperation.Write("canonical", new { count = 1 }, ReadModelBatchConcurrency.CreateOnly);
        ReadModelBatchOperation[] operations = defect == "duplicate-canonical" ? [write, write]
            : defect == "canonical-over-limit" ? Enumerable.Range(0, 101).Select(i => ReadModelBatchOperation.Write("canonical-" + i, new { count = i }, ReadModelBatchConcurrency.CreateOnly)).ToArray()
            : [write];
        ReadModelBatchOperation[] staged = Enumerable.Range(0, 33).Select(i => ReadModelBatchOperation.Write("generation-" + i,
            new { count = i }, ReadModelBatchConcurrency.CreateOnly)).ToArray();
        if (defect == "late-invalid-generation") staged[^1] = staged[^1].WithConcurrency(ReadModelBatchConcurrency.LastWrite);
        handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainProjectionRebuildPlan("statestore", operations) { StagedOperations = staged });
        var store = Substitute.For<IReadModelBatchStore>();
        using var services = BuildRebuildProvider(store, handler);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute("widget-service", "v1", [new ProjectionDispatchRoute("widget", "widget-detail")]);
        var result = await DomainProjectionDispatcher.RebuildAsync(services, CreateRequest(fingerprint, "widget-detail"),
            new(), new() { AppId = "widget-service", ServiceVersion = "v1" }, TestContext.Current.CancellationToken);
        result.Outcomes.ShouldHaveSingleItem().ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
        _ = store.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Theory]
    [InlineData(ReadModelBatchStatus.Incomplete, ProjectionDispatchStatus.Retryable)]
    [InlineData(ReadModelBatchStatus.Indeterminate, ProjectionDispatchStatus.Indeterminate)]
    [InlineData(ReadModelBatchStatus.Conflict, ProjectionDispatchStatus.Retryable)]
    public async Task RebuildPreservesStructuredGenerationStorageOutcomes(ReadModelBatchStatus status, ProjectionDispatchStatus expected)
    {
        var handler = CreateRebuildHandler("widget", "widget-detail");
        handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainProjectionRebuildPlan("statestore", [ReadModelBatchOperation.Write("canonical", new { count = 1 }, ReadModelBatchConcurrency.CreateOnly)])
                { StagedOperations = [ReadModelBatchOperation.Write("generation", new { count = 1 }, ReadModelBatchConcurrency.CreateOnly)] });
        var store = Substitute.For<IReadModelBatchStore>();
        store.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>()).Returns(new ReadModelBatchResult(status, "fingerprint",
            status == ReadModelBatchStatus.Conflict ? ReadModelBatchConflictKind.Optimistic : ReadModelBatchConflictKind.None, null));
        using var services = BuildRebuildProvider(store, handler);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute("widget-service", "v1", [new ProjectionDispatchRoute("widget", "widget-detail")]);
        var result = await DomainProjectionDispatcher.RebuildAsync(services, CreateRequest(fingerprint, "widget-detail"),
            new(), new() { AppId = "widget-service", ServiceVersion = "v1" }, TestContext.Current.CancellationToken);
        result.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(expected);
        _ = store.DidNotReceive().ExecuteAsync(Arg.Is<ReadModelBatch>(batch => batch.Scope.ProjectionType == "rebuild"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, "null-etag")]
    [InlineData(true, "null-etag")]
    [InlineData(false, "late-key")]
    [InlineData(true, "late-key")]
    [InlineData(false, "late-manifest")]
    [InlineData(true, "late-manifest")]
    [InlineData(false, "incomplete")]
    [InlineData(true, "incomplete")]
    [InlineData(false, "indeterminate")]
    [InlineData(true, "indeterminate")]
    public async Task OrdinaryAndStreamedRebuildPreflightEveryGenerationAndPreserveStorageOutcomes(bool streamed, string defect)
    {
        var token = TestContext.Current.CancellationToken;
        var handler = Substitute.For<IAsyncDomainProjectionRebuildHandler, IAsyncDomainProjectionStreamedRebuildHandler>();
        handler.Domain.Returns("widget");
        handler.ProjectionType.Returns("widget-detail");
        handler.OrdinaryDeliveryEnabled.Returns(true);
        handler.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        ((IAsyncDomainProjectionStreamedRebuildHandler)handler).RebuildTransportStoreName.Returns("statestore");
        var persisted = new InMemoryReadModelStore();
        var operations = Enumerable.Range(0, 33).Select(i => ReadModelBatchOperation.Write("generation-" + i,
            new { count = i }, ReadModelBatchConcurrency.CreateOnly)).ToArray();
        if (defect == "null-etag") operations[^1] = ReadModelBatchOperation.Write("generation-32", new { count = 32 }, new(ReadModelBatchConcurrencyMode.ExpectedETag, null!));
        if (defect == "late-key")
        {
            persisted.BatchOptions.MaxKeyByteLength = 20;
            operations[^1] = ReadModelBatchOperation.Write(new string('k', 21), new { count = 1 }, ReadModelBatchConcurrency.CreateOnly);
        }
        if (defect == "late-manifest")
        {
            persisted.BatchOptions.MaxCanonicalManifestBytes = 12_000;
            operations[^1] = ReadModelBatchOperation.Write("generation-32", new string('x', 12_000), ReadModelBatchConcurrency.CreateOnly);
        }
        var plan = new DomainProjectionRebuildPlan("statestore", [ReadModelBatchOperation.Write("canonical", new { count = 1 }, ReadModelBatchConcurrency.CreateOnly)])
            { StagedOperations = operations };
        handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(plan);
        ((IAsyncDomainProjectionStreamedRebuildHandler)handler).PrepareStreamedRebuildAsync(Arg.Any<ProjectionRequest>(),
            Arg.Any<IAsyncEnumerable<ProjectionEventDto>>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                await foreach (var item in call.Arg<IAsyncEnumerable<ProjectionEventDto>>()) _ = item;
                return plan;
            });
        var batches = Substitute.For<IReadModelBatchStore>();
        batches.When(store => store.ValidateBatch(Arg.Any<ReadModelBatch>())).Do(call => persisted.ValidateBatch(call.Arg<ReadModelBatch>()));
        int writes = 0;
        batches.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            writes++;
            return defect == "incomplete" ? ReadModelBatchResult.Incomplete("test") : ReadModelBatchResult.Indeterminate("test");
        });
        using var services = new ServiceCollection().AddSingleton<IReadModelStore>(persisted)
            .AddSingleton(batches).AddSingleton<IAsyncDomainProjectionHandler>(handler).BuildServiceProvider();
        var identity = new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" };
        var options = new ProjectionDispatchOptions();
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion, [new("widget", "widget-detail")]);
        var request = CreateRequest(fingerprint, "widget-detail");
        if (streamed)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request.Request.Events, JsonSerializerOptions.Web);
            request = request with { Request = request.Request with { Events = [], RebuildSourceHeadSequence = 1 }, RebuildTransport = new(1, Convert.ToHexString(SHA256.HashData(bytes))) };
            await DomainProjectionRebuildTransport.StoreAsync(services, new(request, 0, bytes), options, identity, token);
        }
        var result = streamed
            ? await DomainProjectionDispatcher.StageRebuildAsync(services, request, options, identity, token)
            : await DomainProjectionDispatcher.RebuildAsync(services, request, options, identity, token);
        var outcome = result.Outcomes.ShouldHaveSingleItem();
        if (defect is "incomplete" or "indeterminate")
        {
            outcome.Status.ShouldBe(defect == "incomplete" ? ProjectionDispatchStatus.Retryable : ProjectionDispatchStatus.Indeterminate);
            writes.ShouldBe(1);
        }
        else
        {
            outcome.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
            writes.ShouldBe(0);
        }
        (await persisted.GetAsync<int[]>("statestore", "canonical", token)).Value.ShouldBeNull();
        (await persisted.GetAsync<int[]>("statestore", "generation-0", token)).Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(false, "partition")]
    [InlineData(true, "partition")]
    [InlineData(false, "refusal")]
    [InlineData(true, "refusal")]
    [InlineData(false, "large-with-nonstaging-default")]
    [InlineData(true, "large-with-nonstaging-default")]
    public async Task OrdinaryAndStreamedRebuildHonorOnlyTheMostRestrictiveStagingPlanCeiling(bool streamed, string scenario)
    {
        var token = TestContext.Current.CancellationToken;
        bool large = scenario == "large-with-nonstaging-default";
        var handler = Substitute.For<IAsyncDomainProjectionRebuildHandler, IAsyncDomainProjectionStreamedRebuildHandler>();
        handler.Domain.Returns("widget"); handler.ProjectionType.Returns("widget-detail");
        handler.OrdinaryDeliveryEnabled.Returns(true); handler.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        ((IAsyncDomainProjectionStreamedRebuildHandler)handler).RebuildTransportStoreName.Returns("statestore");
        var operations = large
            ? new[] { ReadModelBatchOperation.Write("generation-large", new string('v', 600_000), ReadModelBatchConcurrency.CreateOnly) }
            : Enumerable.Range(0, scenario == "refusal" ? 33 : 4).Select(i => ReadModelBatchOperation.Write("generation-" + i,
                new string('v', scenario == "refusal" && i == 32 ? 9000 : 3000), ReadModelBatchConcurrency.CreateOnly)).ToArray();
        var plan = new DomainProjectionRebuildPlan("statestore", [ReadModelBatchOperation.Write("canonical-detail", new { count = 1 }, ReadModelBatchConcurrency.CreateOnly)])
            { StagedOperations = operations, GenerationStagingMaxCanonicalManifestBytes = large ? 1_048_576 : 8000 };
        handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(plan);
        ((IAsyncDomainProjectionStreamedRebuildHandler)handler).PrepareStreamedRebuildAsync(Arg.Any<ProjectionRequest>(),
            Arg.Any<IAsyncEnumerable<ProjectionEventDto>>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                await foreach (var item in call.Arg<IAsyncEnumerable<ProjectionEventDto>>()) _ = item;
                return plan;
            });
        var second = Substitute.For<IAsyncDomainProjectionRebuildHandler, IAsyncDomainProjectionStreamedRebuildHandler>();
        second.Domain.Returns("widget"); second.ProjectionType.Returns("widget-index");
        second.OrdinaryDeliveryEnabled.Returns(true); second.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        ((IAsyncDomainProjectionStreamedRebuildHandler)second).RebuildTransportStoreName.Returns("statestore");
        var secondPlan = new DomainProjectionRebuildPlan("statestore", [ReadModelBatchOperation.Write("canonical-index", new { count = 1 }, ReadModelBatchConcurrency.CreateOnly)])
            { StagedOperations = large ? [] : [ReadModelBatchOperation.Write("generation-index", new { count = 1 }, ReadModelBatchConcurrency.CreateOnly)],
                GenerationStagingMaxCanonicalManifestBytes = large ? 512 * 1024 : 12_000 };
        second.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(secondPlan);
        ((IAsyncDomainProjectionStreamedRebuildHandler)second).PrepareStreamedRebuildAsync(Arg.Any<ProjectionRequest>(),
            Arg.Any<IAsyncEnumerable<ProjectionEventDto>>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                await foreach (var item in call.Arg<IAsyncEnumerable<ProjectionEventDto>>()) _ = item;
                return secondPlan;
            });
        var persisted = new InMemoryReadModelStore();
        var batches = Substitute.For<IReadModelBatchStore>();
        var observed = new List<ReadModelBatch>();
        batches.When(store => store.ValidateBatch(Arg.Any<ReadModelBatch>())).Do(call => persisted.ValidateBatch(call.Arg<ReadModelBatch>()));
        batches.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            observed.Add(call.Arg<ReadModelBatch>());
            return persisted.ExecuteAsync(call.Arg<ReadModelBatch>(), call.Arg<CancellationToken>());
        });
        using var services = new ServiceCollection().AddSingleton<IReadModelStore>(persisted).AddSingleton(batches)
            .AddSingleton<IReadModelBatchStagingStore>(persisted).AddSingleton<IAsyncDomainProjectionHandler>(handler)
            .AddSingleton<IAsyncDomainProjectionHandler>(second).BuildServiceProvider();
        var identity = new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" };
        var options = new ProjectionDispatchOptions();
        var request = CreateRequest(ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion,
            [new("widget", "widget-detail"), new("widget", "widget-index")]), "widget-detail", "widget-index");
        if (streamed)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request.Request.Events, JsonSerializerOptions.Web);
            request = request with { Request = request.Request with { Events = [], RebuildSourceHeadSequence = 1 }, RebuildTransport = new(1, Convert.ToHexString(SHA256.HashData(bytes))) };
            await DomainProjectionRebuildTransport.StoreAsync(services, new(request, 0, bytes), options, identity, token);
        }
        var response = streamed ? await DomainProjectionDispatcher.StageRebuildAsync(services, request, options, identity, token)
            : await DomainProjectionDispatcher.RebuildAsync(services, request, options, identity, token);
        if (scenario == "refusal")
        {
            response.Outcomes.ShouldAllBe(outcome => outcome.ReasonCode == ProjectionDispatchReasonCodes.MalformedOutcome);
            observed.ShouldBeEmpty();
            (await persisted.GetAsync<string>("statestore", "generation-0", token)).Value.ShouldBeNull();
            (await persisted.GetAsync<string>("statestore", "canonical-detail", token)).Value.ShouldBeNull();
            return;
        }
        response.Outcomes.ShouldAllBe(outcome => outcome.Status == ProjectionDispatchStatus.Completed);
        var staged = observed.Where(batch => batch.Scope.ProjectionType.EndsWith(".generation", StringComparison.Ordinal)).ToArray();
        staged.Length.ShouldBe(large ? 1 : 4);
        foreach (var batch in staged)
            ReadModelBatchValidation.Validate(batch, new ReadModelBatchOptions { MaxCanonicalManifestBytes = large ? 1_048_576 : 8000 });
        var originalIdentities = staged.Select(batch => (batch.Scope.BatchId, Manifest: Snapshot(batch))).ToArray();
        observed.Clear();
        _ = streamed ? await DomainProjectionDispatcher.StageRebuildAsync(services, request, options, identity, token)
            : await DomainProjectionDispatcher.RebuildAsync(services, request, options, identity, token);
        observed.Where(batch => batch.Scope.ProjectionType.EndsWith(".generation", StringComparison.Ordinal))
            .Select(batch => (batch.Scope.BatchId, Manifest: Snapshot(batch))).ShouldBe(originalIdentities);
        static string Snapshot(ReadModelBatch batch) => JsonSerializer.Serialize(new
        {
            batch.Scope,
            Operations = batch.Operations.Select(operation => new { operation.Key,
                Value = Convert.ToBase64String(operation.CanonicalValue.Span), operation.Concurrency })
        });
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
    public async Task RebuildAsync_TypedDeterministicRejectionMapsToTerminalFailure() {
        IAsyncDomainProjectionRebuildHandler handler = CreateRebuildHandler("widget", "widget-detail");
        handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<DomainProjectionRebuildPlan>>(_ => throw new DomainProjectionRebuildRejectedException(
                ProjectionDispatchReasonCodes.HandlerFailure));
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        using ServiceProvider provider = BuildRebuildProvider(batchStore, handler);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]);

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.RebuildAsync(
            provider,
            CreateRequest(fingerprint, "widget-detail"),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);

        ProjectionDispatchOutcome outcome = response.Outcomes.ShouldHaveSingleItem();
        outcome.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        outcome.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.HandlerFailure);
        _ = await batchStore.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Fact]
    public async Task RebuildAsync_EmptyFullReplayHistoryIsRejectedBeforePreparation() {
        IAsyncDomainProjectionRebuildHandler handler = CreateRebuildHandler("widget", "widget-detail");
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        using ServiceProvider provider = BuildRebuildProvider(batchStore, handler);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]);
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "widget", "widget-1", []),
            ["widget-detail"],
            "dispatch-empty",
            fingerprint);

        ProjectionDispatchValidationException exception = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionDispatcher.RebuildAsync(
                provider,
                request,
                new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
                CancellationToken.None));

        exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
        _ = handler.DidNotReceiveWithAnyArgs().PrepareRebuildAsync(default!, default!, default);
    }

    [Fact]
    public async Task RebuildAsync_SealedErasedEmptyHistoryReachesFullReplayHandler() {
        IAsyncDomainProjectionRebuildHandler handler = CreateRebuildHandler("widget", "widget-detail");
        handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), "dispatch-erased", Arg.Any<CancellationToken>())
            .Returns(new DomainProjectionRebuildPlan("statestore",
                [ReadModelBatchOperation.Delete("widget-1:detail", ReadModelBatchConcurrency.IdempotentAbsent)]));
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        _ = batchStore.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelBatchResult(ReadModelBatchStatus.Completed, "fingerprint",
                ReadModelBatchConflictKind.None, null));
        using ServiceProvider provider = BuildRebuildProvider(batchStore, handler);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute("widget-service", "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]);
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "widget", "widget-1", []) {
                RebuildSourceHeadSequence = 0,
                RebuildSourceErased = true,
            },
            ["widget-detail"], "dispatch-erased", fingerprint);

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.RebuildAsync(
            provider, request, new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);

        response.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        _ = await handler.Received(1).PrepareRebuildAsync(
            Arg.Is<ProjectionRequest>(item => item.RebuildSourceErased && item.Events.Length == 0),
            "dispatch-erased", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RebuildAsync_AdmittedFullReplaySubsetUsesCompleteCatalogFingerprint() {
        IAsyncDomainProjectionRebuildHandler detail = CreateRebuildHandler("widget", "widget-detail");
        IAsyncDomainProjectionRebuildHandler index = CreateRebuildHandler("widget", "widget-index");
        detail.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainProjectionRebuildPlan("statestore",
                [ReadModelBatchOperation.Write("widget-1:detail", new Dictionary<string, int> { ["count"] = 2 },
                    ReadModelBatchConcurrency.LastWrite)]));
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        _ = batchStore.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelBatchResult(ReadModelBatchStatus.Completed, "fingerprint", ReadModelBatchConflictKind.None, null));
        using ServiceProvider provider = BuildRebuildProvider(batchStore, detail, index);
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail"), new ProjectionDispatchRoute("widget", "widget-index")]);

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.RebuildAsync(
                provider,
                CreateRequest(fingerprint, "widget-detail"),
                new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
                CancellationToken.None);

        response.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        _ = await detail.Received(1).PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = index.DidNotReceiveWithAnyArgs().PrepareRebuildAsync(default!, default!, default);
        _ = await batchStore.Received(1).ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
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
        handler.OrdinaryDeliveryEnabled.Returns(true);
        return handler;
    }

    private static IAsyncDomainProjectionRebuildHandler CreateRebuildHandler(string domain, string projectionType) {
        IAsyncDomainProjectionRebuildHandler handler = Substitute.For<IAsyncDomainProjectionRebuildHandler>();
        handler.Domain.Returns(domain);
        handler.ProjectionType.Returns(projectionType);
        handler.OrdinaryDeliveryEnabled.Returns(true);
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
            new ProjectionRequest("tenant-a", "widget", "widget-1", [ProjectionEvent(1)]),
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
