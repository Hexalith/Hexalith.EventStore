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
}
