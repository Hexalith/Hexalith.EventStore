using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Validated limits for named projection dispatch and its durable immediate-mode retry worker.
/// </summary>
public sealed class ProjectionDispatchOptions {
    private const int MaximumConfiguredOutcomes = 4096;
    /// <summary>The default maximum handlers registered for one domain.</summary>
    public const int DefaultMaxHandlersPerDomain = 32;

    /// <summary>The default maximum outcomes in one v2 response.</summary>
    public const int DefaultMaxOutcomes = 32;

    /// <summary>The default maximum serialized outcome-envelope size.</summary>
    public const int DefaultMaxOutcomeEnvelopeBytes = 1_048_576;

    /// <summary>The default maximum number of events accepted by the direct rebuild endpoint.</summary>
    public const int DefaultMaxRebuildEventCount = 10_000;

    /// <summary>The default maximum retry attempts per activation.</summary>
    public const int DefaultMaxRetryAttempts = 8;

    /// <summary>The default maximum number of due work items drained in one worker scan.</summary>
    public const int DefaultRetryScanBatchSize = 64;

    /// <summary>Gets or sets the maximum handlers registered for one domain.</summary>
    public int MaxHandlersPerDomain { get; set; } = DefaultMaxHandlersPerDomain;

    /// <summary>Gets or sets the maximum outcomes in one v2 response.</summary>
    public int MaxOutcomes { get; set; } = DefaultMaxOutcomes;

    /// <summary>Gets or sets the maximum ASCII byte length of a reason code.</summary>
    public int MaxReasonCodeBytes { get; set; } = ProjectionDispatchReasonCodes.MaxAsciiBytes;

    /// <summary>Gets or sets the maximum serialized outcome-envelope size.</summary>
    public int MaxOutcomeEnvelopeBytes { get; set; } = DefaultMaxOutcomeEnvelopeBytes;

    /// <summary>Gets or sets the maximum complete-prefix event count accepted for rebuild.</summary>
    public int MaxRebuildEventCount { get; set; } = DefaultMaxRebuildEventCount;

    /// <summary>Gets or sets the maximum retry attempts performed in one worker activation.</summary>
    public int MaxRetryAttempts { get; set; } = DefaultMaxRetryAttempts;

    /// <summary>Gets or sets the maximum number of due work items drained in one worker scan.</summary>
    public int RetryScanBatchSize { get; set; } = DefaultRetryScanBatchSize;

    /// <summary>Gets or sets the initial retry backoff.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the maximum retry backoff.</summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the interval between durable retry scans.</summary>
    public TimeSpan RetryWorkerInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the cross-replica aggregate lease duration.</summary>
    public TimeSpan RetryLeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the interval for refreshing verified named route catalogs.</summary>
    public TimeSpan CatalogRefreshInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets whether the maintenance-only v1 retry-ledger migration is enabled.</summary>
    public bool EnableLegacyRetryLedgerMigration { get; set; }

    /// <summary>Gets or sets operator confirmation that every legacy v1 writer is stopped.</summary>
    public bool LegacyRetryLedgerWritersQuiesced { get; set; }

    /// <summary>Gets or sets the operator marker proving all v1 writers are quiesced.</summary>
    public string? LegacyRetryLedgerMigrationMarker { get; set; }

    /// <summary>Validates all quantitative bounds and retry ordering.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A bound is invalid.</exception>
    public void Validate() {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxHandlersPerDomain);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxHandlersPerDomain, MaximumConfiguredOutcomes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxOutcomes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxOutcomes, MaximumConfiguredOutcomes);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOutcomes, MaxHandlersPerDomain);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxReasonCodeBytes);
        int minimumReasonCodeBytes = new[] {
            ProjectionDispatchReasonCodes.DuplicateRoute,
            ProjectionDispatchReasonCodes.UnsupportedRoute,
            ProjectionDispatchReasonCodes.UnsupportedCapability,
            ProjectionDispatchReasonCodes.MalformedOutcome,
            ProjectionDispatchReasonCodes.HandlerFailure,
            ProjectionDispatchReasonCodes.Cancellation,
            ProjectionDispatchReasonCodes.PartialRetry,
            ProjectionDispatchReasonCodes.DeliveryAlreadyCompleted,
            ProjectionDispatchReasonCodes.DeliveryInProgress,
            ProjectionDispatchReasonCodes.DeliveryGap,
            ProjectionDispatchReasonCodes.DeliveryIdentityConflict,
            ProjectionDispatchReasonCodes.DeliveryReconciliationRequired,
            ProjectionDispatchReasonCodes.DeliverySchemaRegression,
            ProjectionDispatchReasonCodes.DeliveryStateUnavailable,
            ProjectionDispatchReasonCodes.DeliveryLeaseReclaimed,
            ProjectionDispatchReasonCodes.DeliveryReconciled,
            ProjectionDispatchReasonCodes.DeliveryRebuildRequired,
        }.Max(static value => value.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxReasonCodeBytes, minimumReasonCodeBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxReasonCodeBytes, ProjectionDispatchReasonCodes.MaxAsciiBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxOutcomeEnvelopeBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            MaxOutcomeEnvelopeBytes,
            GetMinimumOutcomeEnvelopeBytes(MaxOutcomes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRebuildEventCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRetryAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RetryScanBatchSize);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RetryBaseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RetryMaxDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(RetryMaxDelay, RetryBaseDelay);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RetryWorkerInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RetryLeaseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(CatalogRefreshInterval, TimeSpan.Zero);
        if (EnableLegacyRetryLedgerMigration
            && (!LegacyRetryLedgerWritersQuiesced
                || !string.Equals(LegacyRetryLedgerMigrationMarker, "v1-writers-quiesced", StringComparison.Ordinal))) {
            throw new ArgumentException(
                "Legacy retry-ledger migration requires quiesced v1 writers and the exact maintenance marker 'v1-writers-quiesced'.",
                nameof(LegacyRetryLedgerMigrationMarker));
        }
    }

    /// <summary>Gets the minimum envelope size needed for one safe outcome per admitted route.</summary>
    /// <param name="maximumOutcomes">The configured maximum outcome count.</param>
    /// <returns>The serialized byte count of the worst-case bounded safe-failure envelope.</returns>
    public static int GetMinimumOutcomeEnvelopeBytes(int maximumOutcomes) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutcomes);
        string maximumRoute = new('a', 64);
        ProjectionDispatchOutcome[] outcomes = [.. Enumerable.Range(0, maximumOutcomes)
            .Select(_ => new ProjectionDispatchOutcome(
                maximumRoute,
                ProjectionDispatchStatus.Failed,
                null,
                ProjectionDispatchReasonCodes.MalformedOutcome))];
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version, outcomes),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)).Length;
    }
}
