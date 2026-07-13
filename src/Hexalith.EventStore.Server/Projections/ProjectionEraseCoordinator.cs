using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Coordinated, resumable, structured-outcome projection eraser. It resolves the canonical targets, refuses
/// erasure while an operator rebuild is active, runs through the persisted lifecycle actor
/// (begin/record/complete with resume), erases the read-model targets, then the aggregate-specific rebuild
/// checkpoint row, then the projection-scoped delivery checkpoint (in that order), classifies every target
/// via an internal ETag read-back, and NEVER uses a state transaction (resumable-only). Callers supply only
/// the scope, the logical slot identifiers, and a stable operation identifier.
/// </summary>
internal sealed partial class ProjectionEraseCoordinator : IProjectionEraseCoordinator {
    private const string OutcomeComplete = "Complete";
    private const string OutcomeConflict = "Conflict";
    private const string OutcomeIncomplete = "Incomplete";
    private const string OutcomeUnknown = "Unknown";

    private static readonly IReadOnlyDictionary<string, string> s_noResumeOutcomes =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly IProjectionReadModelAddressFactory _addressFactory;
    private readonly IProjectionRebuildCheckpointStore _rebuildStore;
    private readonly IProjectionLifecycleGateway _lifecycleGateway;
    private readonly ILogger<ProjectionEraseCoordinator> _logger;
    private readonly IReadModelConditionalEraser? _readModelEraser;
    private readonly IProjectionRebuildCheckpointEraser? _rebuildEraser;
    private readonly IProjectionDeliveryCheckpointStore? _deliveryCheckpointStore;

    /// <summary>Initializes a new <see cref="ProjectionEraseCoordinator"/>.</summary>
    /// <param name="addressFactory">The canonical read-model address factory.</param>
    /// <param name="rebuildStore">The rebuild checkpoint store providing the active-rebuild gate.</param>
    /// <param name="lifecycleGateway">The lifecycle actor gateway.</param>
    /// <param name="logger">The logger for bounded, support-safe structured events.</param>
    /// <param name="readModelEraser">The opt-in read-model conditional eraser capability.</param>
    /// <param name="rebuildEraser">The opt-in aggregate rebuild checkpoint eraser capability.</param>
    /// <param name="deliveryCheckpointStore">The opt-in projection-scoped delivery checkpoint store.</param>
    public ProjectionEraseCoordinator(
        IProjectionReadModelAddressFactory addressFactory,
        IProjectionRebuildCheckpointStore rebuildStore,
        IProjectionLifecycleGateway lifecycleGateway,
        ILogger<ProjectionEraseCoordinator> logger,
        IReadModelConditionalEraser? readModelEraser = null,
        IProjectionRebuildCheckpointEraser? rebuildEraser = null,
        IProjectionDeliveryCheckpointStore? deliveryCheckpointStore = null) {
        _addressFactory = addressFactory ?? throw new ArgumentNullException(nameof(addressFactory));
        _rebuildStore = rebuildStore ?? throw new ArgumentNullException(nameof(rebuildStore));
        _lifecycleGateway = lifecycleGateway ?? throw new ArgumentNullException(nameof(lifecycleGateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _readModelEraser = readModelEraser;
        _rebuildEraser = rebuildEraser;
        _deliveryCheckpointStore = deliveryCheckpointStore;
    }

    /// <inheritdoc/>
    public async Task<ProjectionEraseResult> EraseAsync(
        ProjectionEraseRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate scalar inputs (no mutation). Missing scope components or an empty slot set are a
        //    required guard: erasing only the checkpoint would vacuously "succeed" while orphaning the
        //    read-model values.
        if (string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.Domain)
            || string.IsNullOrWhiteSpace(request.AggregateId)
            || string.IsNullOrWhiteSpace(request.ProjectionName)
            || string.IsNullOrWhiteSpace(request.OperationId)) {
            Log.EraseUnsupported(_logger, DescribeRequest(request), "invalid-request");
            return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unsupported, "invalid-request");
        }

        if (request.Slots is null || request.Slots.Count == 0) {
            Log.EraseUnsupported(_logger, DescribeRequest(request), "no-slots");
            return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unsupported, "no-slots");
        }

        // Resolve every required erase capability BEFORE the first mutation; return Unsupported when any
        // has not opted in.
        if (_readModelEraser is null || _rebuildEraser is null || _deliveryCheckpointStore is null) {
            Log.EraseUnsupported(_logger, DescribeRequest(request), "capability-unavailable");
            return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unsupported, "capability-unavailable");
        }

        IReadModelConditionalEraser readModelEraser = _readModelEraser;
        IProjectionRebuildCheckpointEraser rebuildEraser = _rebuildEraser;
        IProjectionDeliveryCheckpointStore deliveryStore = _deliveryCheckpointStore;

        AggregateIdentity identity;
        try {
            identity = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
        }
        catch (ArgumentException) {
            Log.EraseUnsupported(_logger, DescribeRequest(request), "invalid-identity");
            return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unsupported, "invalid-identity");
        }

        string scope = $"{identity.ActorId}:{request.ProjectionName}";
        string operationId = request.OperationId;

        try {
            // 2. Resolve + validate the canonical manifest BEFORE any mutation. An unregistered / shared /
            //    legacy slot (or any invalid segment) is not erasable: report Unsupported without touching
            //    or disclosing target state.
            var readModelTargets = new List<ProjectionReadModelAddress>(request.Slots.Count);
            try {
                foreach (string slot in request.Slots) {
                    readModelTargets.Add(_addressFactory.Create(identity, request.ProjectionName, slot));
                }
            }
            catch (ProjectionReadModelAddressException) {
                Log.EraseUnsupported(_logger, scope, "unresolvable-target");
                return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unsupported, "unresolvable-target");
            }
            catch (ArgumentException) {
                Log.EraseUnsupported(_logger, scope, "invalid-target");
                return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unsupported, "invalid-target");
            }

            // 3. Active-rebuild gate (fail-closed): a transient gate fault is treated as ActiveRebuild so we
            //    never proceed to mutate during a possible operator rebuild.
            bool activeRebuild;
            try {
                activeRebuild = await _rebuildStore
                    .HasActiveOperatorRebuildForDomainAsync(identity.TenantId, identity.Domain, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                Log.EraseActiveRebuildGateUnavailable(_logger, ex, scope);
                return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.ActiveRebuild, "active-rebuild-gate-unavailable");
            }

            if (activeRebuild) {
                Log.EraseActiveRebuildDetected(_logger, scope);
                return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.ActiveRebuild, "active-rebuild");
            }

            // 4. Deterministic manifest digest over the sorted target keys (no Random / time).
            var rebuildScope = new ProjectionRebuildCheckpointScope(
                identity.TenantId,
                identity.Domain,
                request.ProjectionName,
                identity.AggregateId,
                operationId);
            string rebuildKey = ProjectionRebuildCheckpointStore.GetStateKey(rebuildScope);
            string deliveryKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(identity, request.ProjectionName);
            string manifestDigest = ComputeManifestDigest(readModelTargets, rebuildKey, deliveryKey);

            // 5. Begin (or resume) the erase via the lifecycle actor — the first mutation.
            ProjectionEraseAdmission admission = await _lifecycleGateway
                .BeginEraseAsync(identity, request.ProjectionName, operationId, manifestDigest, cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyDictionary<string, string> resumeOutcomes = s_noResumeOutcomes;
            switch (admission.Kind) {
                case ProjectionEraseAdmissionKind.Conflict:
                    Log.EraseConflict(_logger, scope, operationId, "operation-conflict");
                    return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Conflict, "operation-conflict");
                case ProjectionEraseAdmissionKind.Resume:
                    resumeOutcomes = admission.PerTargetOutcomes ?? s_noResumeOutcomes;
                    Log.EraseResumed(_logger, scope, operationId, resumeOutcomes.Count);
                    break;
                default:
                    Log.EraseAdmitted(_logger, scope, operationId);
                    break;
            }

            // 6. Erase in strict order: read-model targets -> rebuild row -> delivery checkpoint.
            //    Short-circuit on the first non-Complete outcome so a failed earlier target never erases a
            //    later one. In particular the delivery checkpoint (erased last) MUST NOT be erased after a
            //    read-model or rebuild-row failure: that preserves the "checkpoint last" safety ordering so
            //    a partial failure cannot leave a present read model behind a reset (0) delivery checkpoint.
            var orderedTargets = new List<EraseTarget>(readModelTargets.Count + 2);
            foreach (ProjectionReadModelAddress address in readModelTargets) {
                ProjectionReadModelAddress target = address;
                orderedTargets.Add(new EraseTarget(
                    target.Key,
                    ct => readModelEraser.TryReadEtagAsync(target.StoreName, target.Key, ct),
                    (etag, ct) => readModelEraser.TryEraseAsync(target.StoreName, target.Key, etag, ct)));
            }

            orderedTargets.Add(new EraseTarget(
                rebuildKey,
                ct => rebuildEraser.TryReadAggregateCheckpointEtagAsync(rebuildScope, ct),
                (etag, ct) => rebuildEraser.TryEraseAggregateCheckpointAsync(rebuildScope, etag, ct)));

            orderedTargets.Add(new EraseTarget(
                deliveryKey,
                ct => deliveryStore.TryReadDeliveryCheckpointEtagAsync(identity, request.ProjectionName, ct),
                (etag, ct) => deliveryStore.TryEraseAsync(identity, request.ProjectionName, etag, ct)));

            var targetOutcomes = new List<ProjectionEraseTargetOutcome>(orderedTargets.Count);
            foreach (EraseTarget target in orderedTargets) {
                string outcome = await ProcessTargetAsync(
                        identity,
                        request.ProjectionName,
                        operationId,
                        target.Key,
                        resumeOutcomes,
                        target.ReadEtagAsync,
                        target.EraseAsync,
                        cancellationToken)
                    .ConfigureAwait(false);
                targetOutcomes.Add(new ProjectionEraseTargetOutcome(target.Key, outcome));
                if (!string.Equals(outcome, OutcomeComplete, StringComparison.Ordinal)) {
                    // A target is not durably complete: stop before erasing any later target.
                    break;
                }
            }

            // 7. Aggregate. On any non-success outcome do NOT complete: the phase stays Erasing so the same
            //    operationId can resume.
            if (HasOutcome(targetOutcomes, OutcomeConflict)) {
                Log.EraseNonSuccess(_logger, scope, operationId, "target-conflict");
                return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Conflict, "target-conflict", targetOutcomes);
            }

            if (HasOutcome(targetOutcomes, OutcomeUnknown)) {
                Log.EraseNonSuccess(_logger, scope, operationId, "target-unknown");
                return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unknown, "target-unknown", targetOutcomes);
            }

            if (HasOutcome(targetOutcomes, OutcomeIncomplete)) {
                Log.EraseNonSuccess(_logger, scope, operationId, "target-incomplete");
                return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Incomplete, "target-incomplete", targetOutcomes);
            }

            // All targets Complete: verify every target is absent before completing.
            bool allAbsent = await VerifyAllAbsentAsync(
                    readModelTargets,
                    rebuildScope,
                    identity,
                    request.ProjectionName,
                    readModelEraser,
                    rebuildEraser,
                    deliveryStore,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!allAbsent) {
                Log.EraseNonSuccess(_logger, scope, operationId, "verify-present");
                return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Incomplete, "verify-present", targetOutcomes);
            }

            bool completed = await _lifecycleGateway
                .CompleteEraseAsync(identity, request.ProjectionName, operationId, cancellationToken)
                .ConfigureAwait(false);
            if (!completed) {
                // The lifecycle actor did not confirm completion (a different operation took over, or the
                // phase is no longer Erasing under this operationId). The targets are durably absent, but we
                // cannot claim Success and release queued delivery: report Unknown so the caller retries.
                Log.EraseNonSuccess(_logger, scope, operationId, "complete-unconfirmed");
                return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unknown, "complete-unconfirmed", targetOutcomes);
            }

            Log.EraseSucceeded(_logger, scope, operationId, targetOutcomes.Count);
            return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Success, reasonCode: null, targetOutcomes);
        }
        catch (OperationCanceledException) {
            // Cancellation is never reported as Success; the same operationId can resume.
            Log.EraseCanceled(_logger, scope, operationId);
            return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Canceled, "canceled");
        }
        catch (Exception ex) {
            // Narrow the surface: a genuinely-unexpected fault is logged as a bounded, support-safe line and
            // reported as Unknown (resumable) rather than swallowed into a generic failure.
            Log.EraseUnexpected(_logger, ex, scope, operationId, ex.GetType().Name);
            return ProjectionEraseResult.Of(ProjectionEraseOutcomeKind.Unknown, "unexpected");
        }
    }

    // Deterministic SHA-256 (lowercase hex) over the newline-joined, ordinally-sorted target keys.
    private static string ComputeManifestDigest(
        IReadOnlyList<ProjectionReadModelAddress> readModelTargets,
        string rebuildKey,
        string deliveryKey) {
        var keys = new List<string>(readModelTargets.Count + 2);
        foreach (ProjectionReadModelAddress address in readModelTargets) {
            keys.Add(address.Key);
        }

        keys.Add(rebuildKey);
        keys.Add(deliveryKey);
        keys.Sort(StringComparer.Ordinal);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', keys)));
        return Convert.ToHexStringLower(hash);
    }

    private static bool HasOutcome(IReadOnlyList<ProjectionEraseTargetOutcome> outcomes, string outcome) {
        foreach (ProjectionEraseTargetOutcome target in outcomes) {
            if (string.Equals(target.Outcome, outcome, StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    // Erase one target and record its outcome, skipping a target already recorded Complete on resume.
    private async Task<string> ProcessTargetAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        string targetKey,
        IReadOnlyDictionary<string, string> resumeOutcomes,
        Func<CancellationToken, Task<(bool Present, string Etag)>> readEtagAsync,
        Func<string, CancellationToken, Task<bool>> eraseAsync,
        CancellationToken cancellationToken) {
        if (resumeOutcomes.TryGetValue(targetKey, out string? prior)
            && string.Equals(prior, OutcomeComplete, StringComparison.Ordinal)) {
            Log.TargetOutcome(_logger, $"{identity.ActorId}:{projectionName}", operationId, targetKey, "Resumed-Complete");
            return OutcomeComplete;
        }

        string outcome = await ClassifyEraseAsync(readEtagAsync, eraseAsync, cancellationToken).ConfigureAwait(false);
        bool recorded = await _lifecycleGateway
            .RecordTargetOutcomeAsync(identity, projectionName, operationId, targetKey, outcome, cancellationToken)
            .ConfigureAwait(false);
        if (!recorded) {
            // The lifecycle actor no longer owns this operationId (a different operation took over, or the
            // phase left Erasing), so this target's outcome could not be durably recorded. Do not treat it
            // as durably complete: surface Unknown so the operation does not falsely complete.
            Log.TargetOutcome(_logger, $"{identity.ActorId}:{projectionName}", operationId, targetKey, "RecordRejected");
            return OutcomeUnknown;
        }

        Log.TargetOutcome(_logger, $"{identity.ActorId}:{projectionName}", operationId, targetKey, outcome);
        return outcome;
    }

    // The per-target classifier: read the ETag internally, conditionally erase, and read back to classify.
    private static async Task<string> ClassifyEraseAsync(
        Func<CancellationToken, Task<(bool Present, string Etag)>> readEtagAsync,
        Func<string, CancellationToken, Task<bool>> eraseAsync,
        CancellationToken cancellationToken) {
        (bool present, string etag) = await readEtagAsync(cancellationToken).ConfigureAwait(false);
        if (!present) {
            // Absent: an already-erased target is an idempotent success.
            return OutcomeComplete;
        }

        try {
            bool erased = await eraseAsync(etag, cancellationToken).ConfigureAwait(false);
            if (erased) {
                (bool stillPresent, _) = await readEtagAsync(cancellationToken).ConfigureAwait(false);
                return stillPresent ? OutcomeIncomplete : OutcomeComplete;
            }

            // Present with a different ETag: a newer value exists and MUST NOT be deleted.
            return OutcomeConflict;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            // Ambiguous transport: read back the target to reconcile.
            try {
                (bool afterPresent, string afterEtag) = await readEtagAsync(cancellationToken).ConfigureAwait(false);
                if (!afterPresent) {
                    return OutcomeComplete;
                }

                return string.Equals(afterEtag, etag, StringComparison.Ordinal)
                    ? OutcomeIncomplete
                    : OutcomeConflict;
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                return OutcomeUnknown;
            }
        }
    }

    private static async Task<bool> VerifyAllAbsentAsync(
        IReadOnlyList<ProjectionReadModelAddress> readModelTargets,
        ProjectionRebuildCheckpointScope rebuildScope,
        AggregateIdentity identity,
        string projectionName,
        IReadModelConditionalEraser readModelEraser,
        IProjectionRebuildCheckpointEraser rebuildEraser,
        IProjectionDeliveryCheckpointStore deliveryStore,
        CancellationToken cancellationToken) {
        foreach (ProjectionReadModelAddress address in readModelTargets) {
            (bool present, _) = await readModelEraser
                .TryReadEtagAsync(address.StoreName, address.Key, cancellationToken)
                .ConfigureAwait(false);
            if (present) {
                return false;
            }
        }

        (bool rebuildPresent, _) = await rebuildEraser
            .TryReadAggregateCheckpointEtagAsync(rebuildScope, cancellationToken)
            .ConfigureAwait(false);
        if (rebuildPresent) {
            return false;
        }

        (bool deliveryPresent, _) = await deliveryStore
            .TryReadDeliveryCheckpointEtagAsync(identity, projectionName, cancellationToken)
            .ConfigureAwait(false);
        return !deliveryPresent;
    }

    // Scope-only descriptor for logs before an AggregateIdentity is available. Discloses tenant/domain/
    // aggregate/projection identifiers only, never any target value.
    private static string DescribeRequest(ProjectionEraseRequest request) => string.Join(
        ':',
        request.TenantId ?? string.Empty,
        request.Domain ?? string.Empty,
        request.AggregateId ?? string.Empty,
        request.ProjectionName ?? string.Empty);

    // An ordered erase target: its canonical key plus the read-etag and conditional-erase delegates. The
    // coordinator processes these in strict order and stops at the first non-Complete outcome.
    private readonly record struct EraseTarget(
        string Key,
        Func<CancellationToken, Task<(bool Present, string Etag)>> ReadEtagAsync,
        Func<string, CancellationToken, Task<bool>> EraseAsync);

    private static partial class Log {
        [LoggerMessage(
            EventId = 5060,
            Level = LogLevel.Debug,
            Message = "Projection erase unsupported. Scope={Scope}, ReasonCode={ReasonCode}, Stage=ProjectionEraseUnsupported")]
        public static partial void EraseUnsupported(ILogger logger, string scope, string reasonCode);

        [LoggerMessage(
            EventId = 5061,
            Level = LogLevel.Warning,
            Message = "Projection erase refused: an operator rebuild is active. Scope={Scope}, Stage=ProjectionEraseActiveRebuild")]
        public static partial void EraseActiveRebuildDetected(ILogger logger, string scope);

        [LoggerMessage(
            EventId = 5062,
            Level = LogLevel.Warning,
            Message = "Projection erase refused: active-rebuild gate unavailable (fail-closed). Scope={Scope}, Stage=ProjectionEraseActiveRebuildGateUnavailable")]
        public static partial void EraseActiveRebuildGateUnavailable(ILogger logger, Exception exception, string scope);

        [LoggerMessage(
            EventId = 5063,
            Level = LogLevel.Debug,
            Message = "Projection erase admitted. Scope={Scope}, OperationId={OperationId}, Stage=ProjectionEraseAdmitted")]
        public static partial void EraseAdmitted(ILogger logger, string scope, string operationId);

        [LoggerMessage(
            EventId = 5064,
            Level = LogLevel.Debug,
            Message = "Projection erase resumed. Scope={Scope}, OperationId={OperationId}, RecordedTargets={RecordedTargets}, Stage=ProjectionEraseResumed")]
        public static partial void EraseResumed(ILogger logger, string scope, string operationId, int recordedTargets);

        [LoggerMessage(
            EventId = 5065,
            Level = LogLevel.Warning,
            Message = "Projection erase conflict. Scope={Scope}, OperationId={OperationId}, ReasonCode={ReasonCode}, Stage=ProjectionEraseConflict")]
        public static partial void EraseConflict(ILogger logger, string scope, string operationId, string reasonCode);

        [LoggerMessage(
            EventId = 5066,
            Level = LogLevel.Debug,
            Message = "Projection erase target outcome. Scope={Scope}, OperationId={OperationId}, TargetKey={TargetKey}, Outcome={Outcome}, Stage=ProjectionEraseTargetOutcome")]
        public static partial void TargetOutcome(ILogger logger, string scope, string operationId, string targetKey, string outcome);

        [LoggerMessage(
            EventId = 5067,
            Level = LogLevel.Warning,
            Message = "Projection erase did not complete. Scope={Scope}, OperationId={OperationId}, ReasonCode={ReasonCode}, Stage=ProjectionEraseIncomplete")]
        public static partial void EraseNonSuccess(ILogger logger, string scope, string operationId, string reasonCode);

        [LoggerMessage(
            EventId = 5068,
            Level = LogLevel.Information,
            Message = "Projection erase completed. Scope={Scope}, OperationId={OperationId}, TargetCount={TargetCount}, Stage=ProjectionEraseSucceeded")]
        public static partial void EraseSucceeded(ILogger logger, string scope, string operationId, int targetCount);

        [LoggerMessage(
            EventId = 5069,
            Level = LogLevel.Debug,
            Message = "Projection erase canceled. Scope={Scope}, OperationId={OperationId}, Stage=ProjectionEraseCanceled")]
        public static partial void EraseCanceled(ILogger logger, string scope, string operationId);

        [LoggerMessage(
            EventId = 5070,
            Level = LogLevel.Error,
            Message = "Projection erase failed unexpectedly. Scope={Scope}, OperationId={OperationId}, ExceptionType={ExceptionType}, Stage=ProjectionEraseUnexpected")]
        public static partial void EraseUnexpected(ILogger logger, Exception exception, string scope, string operationId, string exceptionType);
    }
}
