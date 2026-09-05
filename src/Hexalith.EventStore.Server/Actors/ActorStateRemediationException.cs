namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Reports support-safe primary, remediation, and observed-durability classifications without
/// retaining either exception message.
/// </summary>
internal sealed class ActorStateRemediationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorStateRemediationException"/> class.
    /// </summary>
    /// <param name="primaryFailureStage">The stage at which the command first failed.</param>
    /// <param name="primaryExceptionType">The type of the earliest causal exception.</param>
    /// <param name="remediationOperation">The remediation operation that failed.</param>
    /// <param name="remediationExceptionType">The support-safe remediation exception type.</param>
    /// <param name="failedBatchDiscarded">
    /// Whether the mutations belonging to the failed batch were discarded by a completed clear.
    /// This fact does not describe mutations from any later finalizer operation.
    /// </param>
    /// <param name="durableStateObservation">The bounded durable consequence observed after cleanup.</param>
    public ActorStateRemediationException(
        string primaryFailureStage,
        string primaryExceptionType,
        string remediationOperation,
        string remediationExceptionType,
        bool failedBatchDiscarded,
        string durableStateObservation)
        : base(
            $"Actor state remediation failed. PrimaryFailureStage={primaryFailureStage}; "
            + $"PrimaryExceptionType={primaryExceptionType}; RemediationOperation={remediationOperation}; "
            + $"RemediationExceptionType={remediationExceptionType}; "
            + $"FailedBatchDiscarded={failedBatchDiscarded}; "
            + $"DurableStateObservation={durableStateObservation}.")
    {
        PrimaryFailureStage = primaryFailureStage;
        PrimaryExceptionType = primaryExceptionType;
        RemediationOperation = remediationOperation;
        RemediationExceptionType = remediationExceptionType;
        FailedBatchDiscarded = failedBatchDiscarded;
        DurableStateObservation = durableStateObservation;
    }

    /// <summary>Gets the stage at which the command first failed.</summary>
    public string PrimaryFailureStage { get; }

    /// <summary>Gets the type of the earliest causal exception.</summary>
    public string PrimaryExceptionType { get; }

    /// <summary>Gets the remediation operation that failed.</summary>
    public string RemediationOperation { get; }

    /// <summary>Gets the support-safe remediation exception type.</summary>
    public string RemediationExceptionType { get; }

    /// <summary>Gets whether the failed batch was discarded by a completed cache clear.</summary>
    public bool FailedBatchDiscarded { get; }

    /// <summary>Gets the bounded durable consequence observed after cleanup.</summary>
    public string DurableStateObservation { get; }
}
