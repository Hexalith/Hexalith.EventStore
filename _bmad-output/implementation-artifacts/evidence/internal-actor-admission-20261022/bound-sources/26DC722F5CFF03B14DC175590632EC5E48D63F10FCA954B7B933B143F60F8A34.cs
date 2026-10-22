namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Bounded, support-safe reason codes emitted when an internal credential is denied.
/// </summary>
/// <remarks>
/// Reason codes are the only denial detail that reaches logs and traces. They never carry tokens, raw claims,
/// payloads, secret values, or tenant inventories.
/// </remarks>
public static class WorkloadAuthenticationReasons
{
    /// <summary>The application channel was admitted.</summary>
    public const string ChannelAdmitted = "channel-admitted";

    /// <summary>No application-channel token is configured outside Development.</summary>
    public const string ChannelUnconfigured = "channel-unconfigured";

    /// <summary>The request carried no application-channel token.</summary>
    public const string ChannelTokenMissing = "channel-token-missing";

    /// <summary>The request carried more than one application-channel token.</summary>
    public const string ChannelTokenDuplicate = "channel-token-duplicate";

    /// <summary>The request carried a wrong application-channel token.</summary>
    public const string ChannelTokenInvalid = "channel-token-invalid";

    /// <summary>The request carried no workload assertion.</summary>
    public const string AssertionMissing = "assertion-missing";

    /// <summary>The request carried more than one workload assertion.</summary>
    public const string AssertionDuplicate = "assertion-duplicate";

    /// <summary>The request carried a workload assertion together with another credential.</summary>
    public const string CredentialConflict = "credential-conflict";

    /// <summary>The workload assertion could not be parsed.</summary>
    public const string AssertionMalformed = "assertion-malformed";

    /// <summary>The workload assertion has expired.</summary>
    public const string AssertionExpired = "assertion-expired";

    /// <summary>The workload assertion is not valid yet.</summary>
    public const string AssertionNotYetValid = "assertion-not-yet-valid";

    /// <summary>The workload assertion is not short-lived or lacks an issue time.</summary>
    public const string AssertionStale = "assertion-stale";

    /// <summary>The workload assertion failed validation for another reason.</summary>
    public const string AssertionInvalid = "assertion-invalid";

    /// <summary>The workload assertion was not issued for this receiver.</summary>
    public const string AudienceInvalid = "audience-invalid";

    /// <summary>The workload assertion was not issued by the trusted issuer.</summary>
    public const string IssuerInvalid = "issuer-invalid";

    /// <summary>The workload assertion signature could not be verified.</summary>
    public const string SignatureInvalid = "signature-invalid";

    /// <summary>The workload assertion uses a signing algorithm outside the allow-list.</summary>
    public const string AlgorithmInvalid = "algorithm-invalid";

    /// <summary>The workload assertion does not name its caller.</summary>
    public const string CallerMissing = "caller-missing";

    /// <summary>The workload assertion names more than one caller.</summary>
    public const string CallerAmbiguous = "caller-ambiguous";

    /// <summary>The workload assertion names a caller this receiver does not accept.</summary>
    public const string CallerNotAllowed = "caller-not-allowed";

    /// <summary>The sidecar-established caller attribution contradicts the asserted caller.</summary>
    public const string CallerConflict = "caller-conflict";

    /// <summary>The workload assertion grants no operation.</summary>
    public const string OperationMissing = "operation-missing";

    /// <summary>The workload assertion does not grant the requested operation.</summary>
    public const string OperationNotGranted = "operation-not-granted";

    /// <summary>A bound claim (tenant, projection type, or topic) is ambiguous.</summary>
    public const string BindingAmbiguous = "binding-ambiguous";

    /// <summary>A bound claim (tenant, projection type, or topic) does not match the protected request.</summary>
    public const string BindingMismatch = "binding-mismatch";

    /// <summary>A bound claim (tenant, projection type, or topic) that the protected request requires is absent.</summary>
    public const string BindingMissing = "binding-missing";

    /// <summary>The receiving host has no usable credential-verification configuration.</summary>
    public const string VerifierUnconfigured = "verifier-unconfigured";

    /// <summary>Credential-verification infrastructure, such as issuer metadata, is unavailable.</summary>
    public const string VerifierUnavailable = "verifier-unavailable";

    /// <summary>
    /// The domain's administrator verifier could not confirm or refute a wire administrator assertion, so the
    /// request is refused before any domain work.
    /// </summary>
    public const string AdministratorVerifierUnavailable = "administrator-verifier-unavailable";
}
