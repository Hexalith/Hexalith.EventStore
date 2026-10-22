namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Classifies the Dapr application-channel token presented on an inbound request (AD-28).
/// </summary>
/// <remarks>
/// The token only proves that the request crossed the receiving application's own sidecar. It never
/// identifies or authorizes the calling workload.
/// </remarks>
public enum DaprAppChannelTokenStatus
{
    /// <summary>The single presented token matched the configured token.</summary>
    Valid,

    /// <summary>No token is configured and the host runs in Development, where AD-28 does not require one.</summary>
    NotRequired,

    /// <summary>No token is configured outside Development, so the channel cannot be authenticated.</summary>
    Unconfigured,

    /// <summary>The request carried no token.</summary>
    Missing,

    /// <summary>The request carried more than one token value.</summary>
    Duplicate,

    /// <summary>The request carried a token that does not match the configured token.</summary>
    Invalid,
}
