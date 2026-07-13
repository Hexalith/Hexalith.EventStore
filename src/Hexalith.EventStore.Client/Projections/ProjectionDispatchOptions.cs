using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Validated limits for named projection dispatch and its durable immediate-mode retry worker.
/// </summary>
public sealed class ProjectionDispatchOptions {
    /// <summary>The default maximum handlers registered for one domain.</summary>
    public const int DefaultMaxHandlersPerDomain = 32;

    /// <summary>The default maximum outcomes in one v2 response.</summary>
    public const int DefaultMaxOutcomes = 32;

    /// <summary>The default maximum serialized outcome-envelope size.</summary>
    public const int DefaultMaxOutcomeEnvelopeBytes = 1_048_576;

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

    /// <summary>Validates all quantitative bounds and retry ordering.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A bound is invalid.</exception>
    public void Validate() {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxHandlersPerDomain);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxOutcomes);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOutcomes, MaxHandlersPerDomain);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxReasonCodeBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxReasonCodeBytes, ProjectionDispatchReasonCodes.MaxAsciiBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxOutcomeEnvelopeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRetryAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RetryScanBatchSize);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RetryBaseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RetryMaxDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(RetryMaxDelay, RetryBaseDelay);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RetryWorkerInterval, TimeSpan.Zero);
    }
}
