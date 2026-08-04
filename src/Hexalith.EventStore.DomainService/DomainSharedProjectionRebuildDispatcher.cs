using System.Text;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Coordinates durable tenant/domain shared-projection rebuild sessions.</summary>
public static class DomainSharedProjectionRebuildDispatcher {
    /// <summary>Executes one validated shared rebuild session transition.</summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="request">The versioned shared rebuild request.</param>
    /// <param name="options">The validated projection dispatch and rebuild bounds.</param>
    /// <param name="identityOptions">The authoritative local service binding.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
    /// <returns>The last proven durable session outcome.</returns>
    public static async Task<DomainSharedProjectionRebuildResponse> DispatchAsync(
        IServiceProvider serviceProvider,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identityOptions,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(identityOptions);
        options.Validate();
        identityOptions.Validate();
        ValidateRequest(request, options);

        IAsyncDomainSharedProjectionRebuildHandler handler = ResolveHandler(
            serviceProvider,
            request.Identity,
            options,
            identityOptions);
        ValidateComponent(handler.RebuildStoreName);
        IReadModelStore? sessionStore = serviceProvider.GetService<IReadModelStore>();
        if (sessionStore is null) {
            return Failure(
                DomainSharedProjectionRebuildPhase.Accumulating,
                ProjectionDispatchStatus.Failed,
                0,
                DomainSharedProjectionRebuildFingerprint.EmptyInventory,
                null,
                ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        try {
            return request.Action switch {
                DomainSharedProjectionRebuildAction.Begin => await BeginAsync(
                    sessionStore,
                    handler,
                    request.Identity,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Accumulate => await AccumulateAsync(
                    sessionStore,
                    handler,
                    request,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Finalize => await FinalizeAsync(
                    sessionStore,
                    handler,
                    request,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Stage => await StageAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request.Identity,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Commit => await CommitAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request.Identity,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Verify => await VerifyAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request.Identity,
                    options,
                    cancellationToken).ConfigureAwait(false),
                _ => await AbortAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request.Identity,
                    options,
                    cancellationToken).ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception) {
            return Failure(
                DomainSharedProjectionRebuildPhase.Accumulating,
                ProjectionDispatchStatus.Indeterminate,
                0,
                DomainSharedProjectionRebuildFingerprint.EmptyInventory,
                null,
                ProjectionDispatchReasonCodes.HandlerFailure);
        }
    }

    private static async Task<DomainSharedProjectionRebuildResponse> AbortAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase == DomainSharedProjectionRebuildPhase.Aborted) {
                return FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
            }

            if (state.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (state.Phase == DomainSharedProjectionRebuildPhase.Accumulating) {
                DomainSharedProjectionRebuildSessionState aborted = state with {
                    Phase = DomainSharedProjectionRebuildPhase.Aborted,
                };
                if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, aborted, entry.ETag, cancellationToken)
                    .ConfigureAwait(false)) {
                    return FromState(aborted, ProjectionDispatchStatus.Completed, null);
                }

                continue;
            }

            IReadModelBatchStagingStore? stagingStore = serviceProvider.GetService<IReadModelBatchStagingStore>();
            if (stagingStore is null) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.UnsupportedCapability);
            }

            ReadModelBatch batch = await BuildBatchAsync(handler, state, sessionKey, cancellationToken).ConfigureAwait(false);
            ReadModelBatchStagingResult result = await stagingStore.AbortAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.Status != ReadModelBatchStagingStatus.Aborted) {
                return FromStaging(state, result);
            }

            if (!FingerprintMatches(state, result.Fingerprint)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = DomainSharedProjectionRebuildPhase.Aborted,
                BatchFingerprint = result.Fingerprint,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static async Task<DomainSharedProjectionRebuildResponse> AccumulateAsync(
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string historyFingerprint = DomainSharedProjectionRebuildFingerprint.ComputeHistory(
            request.AggregateId!,
            request.IsErased,
            request.Events!);
        long ordinal = request.AggregateOrdinal!.Value;
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (ordinal < state!.AcceptedAggregateCount) {
                DomainSharedProjectionRebuildReceipt receipt = state.Receipts[(int)ordinal];
                return string.Equals(receipt.HistoryFingerprint, historyFingerprint, StringComparison.Ordinal)
                    ? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null)
                    : FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (ordinal > state.AcceptedAggregateCount) {
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryGap);
            }

            if (state.Phase != DomainSharedProjectionRebuildPhase.Accumulating) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (state.AcceptedAggregateCount >= options.MaxSharedRebuildAggregateCount
                || (state.LastAggregateId is not null
                    && string.CompareOrdinal(request.AggregateId, state.LastAggregateId) <= 0)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.MalformedOutcome);
            }

            byte[] candidateState = state.CandidateState;
            if (!request.IsErased) {
                var candidate = new DomainSharedProjectionRebuildCandidate(state.CandidateState);
                DomainSharedProjectionRebuildCandidate accumulated = await handler
                    .AccumulateAsync(
                        identity,
                        candidate,
                        new ProjectionRequest(identity.TenantId, identity.Domain, request.AggregateId!, request.Events!),
                        cancellationToken)
                    .ConfigureAwait(false);
                candidateState = CopyAndValidateCandidate(accumulated, options);
            }

            var newReceipt = new DomainSharedProjectionRebuildReceipt(ordinal, historyFingerprint);
            DomainSharedProjectionRebuildSessionState updated = state with {
                CandidateState = candidateState,
                AcceptedAggregateCount = state.AcceptedAggregateCount + 1,
                InventoryFingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
                    state.InventoryFingerprint,
                    ordinal,
                    historyFingerprint),
                LastAggregateId = request.AggregateId,
                Receipts = [.. state.Receipts, newReceipt],
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static async Task<DomainSharedProjectionRebuildResponse> BeginAsync(
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        byte[]? emptyCandidate = null;
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            if (entry.Value is not null) {
                return TryValidateState(entry.Value, identity, options, out DomainSharedProjectionRebuildResponse? failure)
                    ? FromState(entry.Value, ProjectionDispatchStatus.AlreadyCompleted, null)
                    : failure!;
            }

            if (entry.ETag is not null) {
                return CorruptState();
            }

            if (emptyCandidate is null) {
                DomainSharedProjectionRebuildCandidate candidate = await handler
                    .CreateEmptyCandidateAsync(identity, cancellationToken)
                    .ConfigureAwait(false);
                emptyCandidate = CopyAndValidateCandidate(candidate, options);
            }

            var state = new DomainSharedProjectionRebuildSessionState(
                DomainSharedProjectionRebuildProtocol.Version,
                identity,
                DomainSharedProjectionRebuildPhase.Accumulating,
                emptyCandidate,
                0,
                DomainSharedProjectionRebuildFingerprint.EmptyInventory,
                null,
                [],
                null,
                null,
                null,
                null);
            if (await sessionStore
                .TrySaveAsync(handler.RebuildStoreName, sessionKey, state, string.Empty, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(state, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static async Task<ReadModelBatch> BuildBatchAsync(
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildSessionState state,
        string sessionKey,
        CancellationToken cancellationToken) {
        DomainProjectionRebuildPlan plan = await BuildPlanAsync(handler, state, cancellationToken).ConfigureAwait(false);
        if (plan.Operations.Any(operation => string.Equals(operation.Key, sessionKey, StringComparison.Ordinal))) {
            throw new InvalidOperationException("The finalized shared rebuild manifest targets its private session key.");
        }

        return new ReadModelBatch(
            new ReadModelBatchScope(
                plan.StoreName,
                state.Identity.TenantId,
                state.Identity.Domain,
                "shared-" + state.Identity.ProjectionType,
                state.Identity.ProjectionType,
                state.Identity.OperationId),
            plan.Operations);
    }

    private static async Task<DomainProjectionRebuildPlan> BuildPlanAsync(
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildSessionState state,
        CancellationToken cancellationToken) {
        DomainProjectionRebuildPlan plan = await handler
            .FinalizeAsync(
                state.Identity,
                new DomainSharedProjectionRebuildCandidate(state.CandidateState),
                cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(plan.StoreName, handler.RebuildStoreName, StringComparison.Ordinal)) {
            throw new InvalidOperationException("The finalized shared rebuild manifest is outside its admitted store boundary.");
        }

        return plan;
    }

    private static async Task<DomainSharedProjectionRebuildResponse> CommitAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        IReadModelBatchStagingStore? stagingStore = serviceProvider.GetService<IReadModelBatchStagingStore>();
        if (stagingStore is null) {
            return Unsupported();
        }

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                    handler,
                    state,
                    cancellationToken).ConfigureAwait(false);
                return incomplete ?? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
            }

            if (state.Phase != DomainSharedProjectionRebuildPhase.Prepared) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            ReadModelBatch batch = await BuildBatchAsync(handler, state, sessionKey, cancellationToken).ConfigureAwait(false);
            ReadModelBatchStagingResult committed = await stagingStore.CommitAsync(batch, cancellationToken).ConfigureAwait(false);
            if (committed.Status != ReadModelBatchStagingStatus.Committed) {
                return FromStaging(state, committed);
            }

            ReadModelBatchStagingResult verified = await stagingStore.VerifyAsync(batch, cancellationToken).ConfigureAwait(false);
            if (verified.Status != ReadModelBatchStagingStatus.Committed
                || !string.Equals(committed.Fingerprint, verified.Fingerprint, StringComparison.Ordinal)
                || !FingerprintMatches(state, committed.Fingerprint)) {
                return FromState(state, ProjectionDispatchStatus.Indeterminate, ProjectionDispatchReasonCodes.HandlerFailure);
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = DomainSharedProjectionRebuildPhase.Committed,
                BatchFingerprint = committed.Fingerprint,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                    handler,
                    updated,
                    cancellationToken).ConfigureAwait(false);
                return incomplete ?? FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static byte[] CopyAndValidateCandidate(
        DomainSharedProjectionRebuildCandidate? candidate,
        ProjectionDispatchOptions options) {
        if (candidate is null || candidate.State.Length > options.MaxSharedRebuildCandidateBytes) {
            throw new InvalidOperationException("The shared rebuild handler returned an invalid or over-limit candidate.");
        }

        return candidate.CopyState();
    }

    private static byte[]? CopyAndValidateCompletionState(
        ReadOnlyMemory<byte> completionState,
        ProjectionDispatchOptions options) {
        if (completionState.IsEmpty) {
            return null;
        }

        if (completionState.Length > options.MaxSharedRebuildCandidateBytes) {
            throw new InvalidOperationException("The shared rebuild handler returned over-limit completion state.");
        }

        return completionState.ToArray();
    }

    private static async Task<DomainSharedProjectionRebuildResponse?> CompleteCommittedRebuildAsync(
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildSessionState state,
        CancellationToken cancellationToken) {
        if (handler is not IAsyncDomainSharedProjectionRebuildCompletionHandler completionHandler) {
            return null;
        }

        DomainProjectionHandlerResult result;
        try {
            result = await completionHandler
                .CompleteRebuildAsync(
                    state.Identity,
                    new DomainSharedProjectionRebuildCandidate(state.CandidateState),
                    state.CompletionState ?? ReadOnlyMemory<byte>.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception) {
            return FromState(
                state,
                ProjectionDispatchStatus.Indeterminate,
                ProjectionDispatchReasonCodes.HandlerFailure);
        }
        if (result.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted) {
            return null;
        }

        string reasonCode = string.IsNullOrWhiteSpace(result.ReasonCode)
            ? ProjectionDispatchReasonCodes.HandlerFailure
            : result.ReasonCode;
        return FromState(state, result.Status, reasonCode);
    }

    private static DomainSharedProjectionRebuildResponse CorruptState()
        => Failure(
            DomainSharedProjectionRebuildPhase.Accumulating,
            ProjectionDispatchStatus.Indeterminate,
            0,
            DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            null,
            ProjectionDispatchReasonCodes.HandlerFailure);

    private static async Task<DomainSharedProjectionRebuildResponse> FinalizeAsync(
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase != DomainSharedProjectionRebuildPhase.Accumulating) {
                return state.ExpectedAggregateCount == request.ExpectedAggregateCount
                    && string.Equals(
                        state.ExpectedInventoryFingerprint,
                        request.ExpectedInventoryFingerprint,
                        StringComparison.Ordinal)
                    ? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null)
                    : FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (state.AcceptedAggregateCount != request.ExpectedAggregateCount
                || !string.Equals(
                    state.InventoryFingerprint,
                    request.ExpectedInventoryFingerprint,
                    StringComparison.Ordinal)) {
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryGap);
            }

            DomainProjectionRebuildPlan plan = await BuildPlanAsync(handler, state, cancellationToken).ConfigureAwait(false);
            if (plan.Operations.Any(operation => string.Equals(operation.Key, sessionKey, StringComparison.Ordinal))) {
                throw new InvalidOperationException("The finalized shared rebuild manifest targets its private session key.");
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = DomainSharedProjectionRebuildPhase.Finalized,
                ExpectedAggregateCount = request.ExpectedAggregateCount,
                ExpectedInventoryFingerprint = request.ExpectedInventoryFingerprint,
                CompletionState = CopyAndValidateCompletionState(plan.CompletionState, options),
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static bool FingerprintMatches(DomainSharedProjectionRebuildSessionState state, string fingerprint)
        => !string.IsNullOrWhiteSpace(fingerprint)
            && (state.BatchFingerprint is null
                || string.Equals(state.BatchFingerprint, fingerprint, StringComparison.Ordinal));

    private static DomainSharedProjectionRebuildResponse Failure(
        DomainSharedProjectionRebuildPhase phase,
        ProjectionDispatchStatus status,
        long count,
        string inventoryFingerprint,
        string? batchFingerprint,
        string reasonCode)
        => new(
            DomainSharedProjectionRebuildProtocol.Version,
            phase,
            status,
            count,
            inventoryFingerprint,
            batchFingerprint,
            reasonCode);

    private static DomainSharedProjectionRebuildResponse FromStaging(
        DomainSharedProjectionRebuildSessionState state,
        ReadModelBatchStagingResult result) {
        (ProjectionDispatchStatus Status, string Reason) mapped = result.Status switch {
            ReadModelBatchStagingStatus.Conflict => (
                ProjectionDispatchStatus.Failed,
                ProjectionDispatchReasonCodes.DeliveryIdentityConflict),
            _ => (ProjectionDispatchStatus.Indeterminate, ProjectionDispatchReasonCodes.HandlerFailure),
        };
        return FromState(state, mapped.Status, mapped.Reason);
    }

    private static DomainSharedProjectionRebuildResponse FromState(
        DomainSharedProjectionRebuildSessionState state,
        ProjectionDispatchStatus status,
        string? reasonCode)
        => new(
            DomainSharedProjectionRebuildProtocol.Version,
            state.Phase,
            status,
            state.AcceptedAggregateCount,
            state.InventoryFingerprint,
            state.BatchFingerprint,
            reasonCode);

    private static IAsyncDomainSharedProjectionRebuildHandler ResolveHandler(
        IServiceProvider serviceProvider,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identityOptions) {
        IAsyncDomainProjectionHandler[] handlers = DomainProjectionHandlerRouteValidator
            .MaterializeAndValidateNamed(serviceProvider.GetServices<IAsyncDomainProjectionHandler>(), options)
            .ToArray();
        ProjectionDispatchRoute[] routes = [.. handlers
            .Where(handler => string.Equals(handler.Domain, identity.Domain, StringComparison.Ordinal))
            .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        if (routes.Length == 0
            || !string.Equals(
                ProjectionRouteCatalogFingerprint.Compute(identityOptions.AppId, identityOptions.ServiceVersion, routes),
                identity.CatalogFingerprint,
                StringComparison.Ordinal)) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        IAsyncDomainProjectionHandler? handler = handlers.SingleOrDefault(candidate =>
            string.Equals(candidate.Domain, identity.Domain, StringComparison.Ordinal)
            && string.Equals(candidate.ProjectionType, identity.ProjectionType, StringComparison.Ordinal));
        return handler as IAsyncDomainSharedProjectionRebuildHandler
            ?? throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
    }

    private static DomainSharedProjectionRebuildResponse RetryExhausted()
        => Failure(
            DomainSharedProjectionRebuildPhase.Accumulating,
            ProjectionDispatchStatus.Retryable,
            0,
            DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            null,
            ProjectionDispatchReasonCodes.PartialRetry);

    private static async Task<DomainSharedProjectionRebuildResponse> StageAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        IReadModelBatchStagingStore? stagingStore = serviceProvider.GetService<IReadModelBatchStagingStore>();
        if (stagingStore is null) {
            return Unsupported();
        }

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                    handler,
                    state,
                    cancellationToken).ConfigureAwait(false);
                return incomplete ?? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
            }

            if (state.Phase is not DomainSharedProjectionRebuildPhase.Finalized
                and not DomainSharedProjectionRebuildPhase.Prepared) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            ReadModelBatch batch = await BuildBatchAsync(handler, state, sessionKey, cancellationToken).ConfigureAwait(false);
            ReadModelBatchStagingResult result = await stagingStore.StageAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.Status is not ReadModelBatchStagingStatus.Prepared
                and not ReadModelBatchStagingStatus.Committed) {
                return FromStaging(state, result);
            }

            if (!FingerprintMatches(state, result.Fingerprint)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = result.Status == ReadModelBatchStagingStatus.Committed
                    ? DomainSharedProjectionRebuildPhase.Committed
                    : DomainSharedProjectionRebuildPhase.Prepared,
                BatchFingerprint = result.Fingerprint,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                if (updated.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                    DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                        handler,
                        updated,
                        cancellationToken).ConfigureAwait(false);
                    if (incomplete is not null) {
                        return incomplete;
                    }
                }

                ProjectionDispatchStatus status = state.Phase == updated.Phase
                    ? ProjectionDispatchStatus.AlreadyCompleted
                    : ProjectionDispatchStatus.Completed;
                return FromState(updated, status, null);
            }
        }

        return RetryExhausted();
    }

    private static bool TryValidateState(
        DomainSharedProjectionRebuildSessionState? state,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        out DomainSharedProjectionRebuildResponse? failure) {
        if (state is null) {
            failure = Failure(
                DomainSharedProjectionRebuildPhase.Accumulating,
                ProjectionDispatchStatus.Retryable,
                0,
                DomainSharedProjectionRebuildFingerprint.EmptyInventory,
                null,
                ProjectionDispatchReasonCodes.DeliveryGap);
            return false;
        }

        if (state.Version != DomainSharedProjectionRebuildProtocol.Version
            || state.Identity != identity
            || !Enum.IsDefined(state.Phase)
            || state.CandidateState is null
            || state.CandidateState.Length > options.MaxSharedRebuildCandidateBytes
            || state.CompletionState?.Length > options.MaxSharedRebuildCandidateBytes
            || state.AcceptedAggregateCount < 0
            || state.AcceptedAggregateCount > options.MaxSharedRebuildAggregateCount
            || state.Receipts is null
            || state.Receipts.LongLength != state.AcceptedAggregateCount
            || state.Receipts.Where((receipt, index) => receipt is null || receipt.Ordinal != index).Any()
            || string.IsNullOrWhiteSpace(state.InventoryFingerprint)) {
            failure = CorruptState();
            return false;
        }

        failure = null;
        return true;
    }

    private static async Task<bool> TrySaveAsync(
        IReadModelStore sessionStore,
        string storeName,
        string sessionKey,
        DomainSharedProjectionRebuildSessionState state,
        string? etag,
        CancellationToken cancellationToken) {
        if (etag is null) {
            return false;
        }

        return await sessionStore
            .TrySaveAsync(storeName, sessionKey, state, etag, cancellationToken)
            .ConfigureAwait(false);
    }

    private static DomainSharedProjectionRebuildResponse Unsupported()
        => Failure(
            DomainSharedProjectionRebuildPhase.Accumulating,
            ProjectionDispatchStatus.Failed,
            0,
            DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            null,
            ProjectionDispatchReasonCodes.UnsupportedCapability);

    private static void ValidateComponent(string value) {
        if (string.IsNullOrWhiteSpace(value)
            || Encoding.UTF8.GetByteCount(value) > ReadModelBatchScope.MaxComponentByteLength) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }
    }

    private static void ValidateEventHistory(IReadOnlyList<ProjectionEventDto> events, int maxEventCount) {
        if (events.Count > maxEventCount) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        long expectedSequence = 1;
        foreach (ProjectionEventDto item in events) {
            if (item is null
                || item.SequenceNumber != expectedSequence
                || item.Payload is null
                || string.IsNullOrWhiteSpace(item.EventTypeName)
                || string.IsNullOrWhiteSpace(item.SerializationFormat)) {
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
            }

            expectedSequence++;
        }
    }

    private static void ValidateRequest(
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options) {
        if (request.Version != DomainSharedProjectionRebuildProtocol.Version) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        if (!Enum.IsDefined(request.Action) || request.Identity is null) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        ValidateComponent(request.Identity.TenantId);
        ValidateComponent(request.Identity.Domain);
        ValidateComponent(request.Identity.ProjectionType);
        ValidateComponent(request.Identity.OperationId);
        ValidateComponent(request.Identity.CatalogFingerprint);
        try {
            NamingConventionEngine.ValidateKebabCase(request.Identity.Domain, nameof(request.Identity.Domain));
            NamingConventionEngine.ValidateKebabCase(request.Identity.ProjectionType, nameof(request.Identity.ProjectionType));
        }
        catch (ArgumentException) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
        }

        bool hasAggregateFields = request.AggregateOrdinal is not null
            || request.AggregateId is not null
            || request.Events is not null
            || request.IsErased;
        bool hasFinalizeFields = request.ExpectedAggregateCount is not null
            || request.ExpectedInventoryFingerprint is not null;
        switch (request.Action) {
            case DomainSharedProjectionRebuildAction.Accumulate:
                if (request.AggregateOrdinal is null or < 0
                    || string.IsNullOrWhiteSpace(request.AggregateId)
                    || request.Events is null
                    || hasFinalizeFields) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                ValidateComponent(request.AggregateId);
                if (request.IsErased && request.Events.Length != 0) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                ValidateEventHistory(request.Events, options.MaxRebuildEventCount);
                break;
            case DomainSharedProjectionRebuildAction.Finalize:
                if (hasAggregateFields
                    || request.ExpectedAggregateCount is null or < 0
                    || string.IsNullOrWhiteSpace(request.ExpectedInventoryFingerprint)) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                break;
            default:
                if (hasAggregateFields || hasFinalizeFields) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                break;
        }
    }

    private static async Task<DomainSharedProjectionRebuildResponse> VerifyAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        IReadModelBatchStagingStore? stagingStore = serviceProvider.GetService<IReadModelBatchStagingStore>();
        if (stagingStore is null) {
            return Unsupported();
        }

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase is DomainSharedProjectionRebuildPhase.Accumulating or DomainSharedProjectionRebuildPhase.Aborted) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            ReadModelBatch batch = await BuildBatchAsync(handler, state, sessionKey, cancellationToken).ConfigureAwait(false);
            ReadModelBatchStagingResult result = await stagingStore.VerifyAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.Status is not ReadModelBatchStagingStatus.Prepared
                and not ReadModelBatchStagingStatus.Committed
                and not ReadModelBatchStagingStatus.Aborted) {
                return FromStaging(state, result);
            }

            if (!FingerprintMatches(state, result.Fingerprint)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            DomainSharedProjectionRebuildPhase phase = result.Status switch {
                ReadModelBatchStagingStatus.Prepared => DomainSharedProjectionRebuildPhase.Prepared,
                ReadModelBatchStagingStatus.Committed => DomainSharedProjectionRebuildPhase.Committed,
                _ => DomainSharedProjectionRebuildPhase.Aborted,
            };
            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = phase,
                BatchFingerprint = result.Fingerprint,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                if (updated.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                    DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                        handler,
                        updated,
                        cancellationToken).ConfigureAwait(false);
                    if (incomplete is not null) {
                        return incomplete;
                    }
                }

                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }
}
