namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Stable support-safe reason codes for named projection dispatch.
/// </summary>
public static class ProjectionDispatchReasonCodes {
    /// <summary>The maximum ASCII byte length of a reason code.</summary>
    public const int MaxAsciiBytes = 128;

    /// <summary>Multiple handlers attempted to own one canonical projection route.</summary>
    public const string DuplicateRoute = "duplicate_projection_route";

    /// <summary>The requested named projection route is not registered or admitted.</summary>
    public const string UnsupportedRoute = "unsupported_projection_route";

    /// <summary>The requested dispatch capability or version is unsupported.</summary>
    public const string UnsupportedCapability = "unsupported_dispatch_capability";

    /// <summary>A handler or endpoint returned an invalid outcome.</summary>
    public const string MalformedOutcome = "malformed_projection_outcome";

    /// <summary>A handler reported a known terminal failure.</summary>
    public const string HandlerFailure = "projection_handler_failure";

    /// <summary>The dispatch was canceled before all admitted handlers started.</summary>
    public const string Cancellation = "projection_dispatch_cancelled";

    /// <summary>One or more projection outcomes remain pending for durable retry.</summary>
    public const string PartialRetry = "projection_partial_retry";

    /// <summary>The exact delivery was already completed durably.</summary>
    public const string DeliveryAlreadyCompleted = "delivery_already_completed";

    /// <summary>The same delivery identity is already reserved.</summary>
    public const string DeliveryInProgress = "delivery_in_progress";

    /// <summary>The supplied aggregate-local history leaves a sequence gap.</summary>
    public const string DeliveryGap = "delivery_gap";

    /// <summary>The supplied identity or content conflicts with durable delivery evidence.</summary>
    public const string DeliveryIdentityConflict = "delivery_identity_conflict";

    /// <summary>Exact completion cannot be proven without authorized EventStore reconciliation.</summary>
    public const string DeliveryReconciliationRequired = "delivery_reconciliation_required";

    /// <summary>A delivery row regressed behind the activated writer protocol.</summary>
    public const string DeliverySchemaRegression = "delivery_schema_regression";

    /// <summary>The delivery row could not be read or conditionally transitioned.</summary>
    public const string DeliveryStateUnavailable = "delivery_state_unavailable";

    /// <summary>An expired same-identity reservation was reclaimed with a higher fence.</summary>
    public const string DeliveryLeaseReclaimed = "delivery_lease_reclaimed";

    /// <summary>Authorized EventStore hydration restored exact delivery evidence.</summary>
    public const string DeliveryReconciled = "delivery_reconciled";

    /// <summary>Authoritative history could not prove the persisted delivery checkpoint.</summary>
    public const string DeliveryRebuildRequired = "delivery_rebuild_required";
}
