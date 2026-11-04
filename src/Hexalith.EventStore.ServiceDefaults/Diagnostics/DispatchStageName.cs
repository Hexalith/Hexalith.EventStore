namespace Hexalith.EventStore.ServiceDefaults.Diagnostics;

/// <summary>Closed vocabulary for command response await boundaries.</summary>
internal enum DispatchStageName
{
    /// <summary>Acquiring the outer HTTP receiver lease.</summary>
    ReceiverAcquire,
    /// <summary>Executing the admitted HTTP pipeline.</summary>
    ReceiverNext,
    /// <summary>Disposing the outer HTTP receiver lease.</summary>
    ReceiverRelease,
    /// <summary>Acquiring the native callback lease.</summary>
    NativeAcquire,
    /// <summary>Executing the native callback pipeline.</summary>
    NativeCallback,
    /// <summary>Disposing the native callback lease.</summary>
    NativeRelease,
    /// <summary>Acquiring the command router lease.</summary>
    RouterAcquire,
    /// <summary>Awaiting the actual actor RPC.</summary>
    ActorRpc,
    /// <summary>Disposing the command router lease.</summary>
    RouterRelease,
    /// <summary>Acquiring the submission handler lease.</summary>
    SubmitAcquire,
    /// <summary>Persisting the projection activation before routing.</summary>
    ProjectionActivation,
    /// <summary>Awaiting command routing and its disposal.</summary>
    Routing,
    /// <summary>Reading advisory command status.</summary>
    StatusRead,
    /// <summary>Writing advisory command status.</summary>
    StatusWrite,
    /// <summary>Reading the archived command.</summary>
    ArchiveRead,
    /// <summary>Writing the archived command.</summary>
    ArchiveWrite,
    /// <summary>Maintaining the correlation index.</summary>
    CorrelationWrite,
    /// <summary>Awaiting projection delivery with its original independent token.</summary>
    ProjectionDelivery,
    /// <summary>Updating the command activity index.</summary>
    ActivityTracking,
    /// <summary>Updating the stream activity index.</summary>
    StreamTracking,
    /// <summary>Validating the protected execution fence.</summary>
    ExecutionFence,
    /// <summary>Completing protected admission and its recovery handling.</summary>
    AdmissionCompletion,
    /// <summary>Awaiting protected actor execution.</summary>
    FencedActorRpc,
    /// <summary>Awaiting the actor reconciliation RPC.</summary>
    ReconciliationRpc,
    /// <summary>Awaiting protected admission.</summary>
    Admission,
    /// <summary>Validating protected execution capability.</summary>
    ExecutionCapability,
    /// <summary>Beginning protected execution.</summary>
    ExecutionBegin,
    /// <summary>Awaiting protected outcome reconciliation.</summary>
    Reconciliation,
    /// <summary>Persisting protected recovery state with its original independent token.</summary>
    RecoveryPersistence,
    /// <summary>Disposing the submission handler lease.</summary>
    SubmitRelease,
    /// <summary>Obtaining or revalidating the outgoing invocation's existing catalog lease.</summary>
    InvocationAcquire,
    /// <summary>Checking the existing committed target and binding the outgoing headers.</summary>
    InvocationValidation,
    /// <summary>Entering and awaiting the actual owned outgoing invocation transport.</summary>
    InvocationTransport,
    /// <summary>Disposing only the outgoing invocation's owned lease.</summary>
    InvocationRelease,
    /// <summary>Waiting for the existing local catalog admission semaphore.</summary>
    CatalogAdmissionWait,
    /// <summary>Reading the existing durable catalog state.</summary>
    CatalogStateRead,
    /// <summary>Comparing and exchanging the existing durable catalog state.</summary>
    CatalogCompareExchange,
    /// <summary>Computing the complete existing activation binding.</summary>
    CatalogActivationDigest,
    /// <summary>Capturing and verifying fresh independent host observations.</summary>
    CatalogHostCapture,
    /// <summary>Validating the existing owner trust proof.</summary>
    CatalogTrust,
    /// <summary>Constructing the immutable snapshot after durable acquisition.</summary>
    CatalogSnapshot,
}
