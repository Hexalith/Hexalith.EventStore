namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Names shared by every host that authenticates internal EventStore workload calls.
/// </summary>
/// <remarks>
/// <para>
/// An internal call is admitted only when two independent proofs succeed: the Dapr application-channel token
/// (<c>APP_API_TOKEN</c>, AD-28) proves the request crossed the receiver's own sidecar, and a short-lived
/// workload assertion issued by the trusted JWT issuer proves the caller, the receiving audience, and the
/// granted operations. A plaintext <c>dapr-caller-app-id</c> header, loopback, ACLs, or mTLS never establish
/// identity or grants.
/// </para>
/// <para>
/// The authenticated principal carries only the workload identity and its granted operations. It never carries
/// tenant, domain, permission, or global-administrator claims.
/// </para>
/// </remarks>
public static class EventStoreWorkloadAuthenticationDefaults
{
    /// <summary>Gets the workload-assertion scheme used by domain-service hosts.</summary>
    public const string WorkloadScheme = "EventStoreWorkload";

    /// <summary>Gets the scheme that authenticates only the Dapr application channel.</summary>
    public const string SidecarChannelScheme = "EventStoreSidecarChannel";

    /// <summary>Gets the authorization policy admitting sidecar-originated deliveries (pub/sub, actor runtime).</summary>
    public const string SidecarChannelPolicy = "EventStoreSidecarChannel";

    /// <summary>Gets the request header carrying the workload assertion.</summary>
    public const string AssertionHeaderName = "X-Hexalith-Workload-Assertion";

    /// <summary>Gets the configuration section holding the shared JWT validation contract.</summary>
    public const string JwtContractSection = "Authentication:JwtBearer";

    /// <summary>Gets the named options instance that carries the shared JWT contract for workload schemes.</summary>
    public const string JwtContractOptionsName = "EventStoreWorkloadContract";

    /// <summary>Gets the authorized-party claim naming the workload that obtained the assertion.</summary>
    public const string CallerClaimType = "azp";

    /// <summary>Gets the claim type naming the authenticated workload on the rebuilt principal.</summary>
    public const string WorkloadClaimType = "eventstore:workload";

    /// <summary>Gets the claim type naming one operation the assertion grants.</summary>
    public const string OperationClaimType = "eventstore:operation";

    /// <summary>Gets the claim type recording an authenticated sidecar channel.</summary>
    public const string ChannelClaimType = "eventstore:channel";

    /// <summary>Gets the legacy caller-attribution claim kept for existing consumers; derived only from the assertion.</summary>
    public const string LegacyCallerClaimType = "dapr_caller_app_id";

    /// <summary>Gets the header a Dapr sidecar uses to attribute the calling application.</summary>
    public const string DaprCallerHeaderName = "dapr-caller-app-id";

    /// <summary>Gets the prefix of claims that bind an assertion to one protected resource.</summary>
    public const string BindingClaimPrefix = "eventstore:bound-";

    /// <summary>Gets the claim binding an assertion to one tenant.</summary>
    public const string TenantBindingClaimType = BindingClaimPrefix + "tenant";

    /// <summary>Gets the claim binding an assertion to one projection type.</summary>
    public const string ProjectionTypeBindingClaimType = BindingClaimPrefix + "projection-type";

    /// <summary>Gets the claim binding an assertion to one pub/sub topic.</summary>
    public const string TopicBindingClaimType = BindingClaimPrefix + "topic";

    /// <summary>Gets the maximum accepted assertion size in UTF-8 characters.</summary>
    public const int MaximumAssertionLength = 16_384;

    /// <summary>
    /// Builds the authorization-policy name requiring one operation from one workload scheme.
    /// </summary>
    /// <param name="schemeName">The workload scheme.</param>
    /// <param name="operation">The required operation.</param>
    /// <returns>The policy name.</returns>
    public static string OperationPolicy(string schemeName, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        return $"{schemeName}:{operation}";
    }

    /// <summary>
    /// Builds the authorization-policy name requiring any authenticated workload from one scheme.
    /// </summary>
    /// <param name="schemeName">The workload scheme.</param>
    /// <returns>The policy name.</returns>
    public static string AnyWorkloadPolicy(string schemeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);
        return $"{schemeName}:any";
    }
}
