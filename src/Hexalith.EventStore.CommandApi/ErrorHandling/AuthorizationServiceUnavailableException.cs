
namespace Hexalith.EventStore.CommandApi.ErrorHandling;

/// <summary>
/// Thrown when an actor-based authorization service is unreachable, times out,
/// returns null, or throws an unexpected exception. Produces a 503 Service Unavailable
/// response via <see cref="AuthorizationServiceUnavailableHandler"/>.
/// </summary>
/// <remarks>
/// <para>Properties <see cref="ActorTypeName"/> and <see cref="ActorId"/> are for
/// server-side diagnostics only. They MUST NOT appear in the HTTP response body
/// (reveals deployment topology).</para>
/// </remarks>
public class AuthorizationServiceUnavailableException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationServiceUnavailableException"/> class
    /// with full diagnostic context.
    /// </summary>
    /// <param name="actorTypeName">The DAPR actor type name that was unreachable.</param>
    /// <param name="actorId">The actor ID (typically tenant ID) that was targeted.</param>
    /// <param name="reason">A human-readable reason for the failure.</param>
    /// <param name="innerException">The underlying exception that caused the failure.</param>
    public AuthorizationServiceUnavailableException(
        string actorTypeName, string actorId, string reason, Exception innerException)
        : base($"Authorization service unavailable: actor '{actorTypeName}' (ID: {actorId}): {reason}", innerException) {
        ActorTypeName = actorTypeName;
        ActorId = actorId;
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationServiceUnavailableException"/> class.
    /// </summary>
    public AuthorizationServiceUnavailableException()
        : base() {
        ActorTypeName = string.Empty;
        ActorId = string.Empty;
        Reason = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationServiceUnavailableException"/> class
    /// with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public AuthorizationServiceUnavailableException(string message)
        : base(message) {
        ActorTypeName = string.Empty;
        ActorId = string.Empty;
        Reason = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationServiceUnavailableException"/> class
    /// with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AuthorizationServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException) {
        ActorTypeName = string.Empty;
        ActorId = string.Empty;
        Reason = message;
    }

    /// <summary>Gets the DAPR actor type name (server-side diagnostics only).</summary>
    public string ActorTypeName { get; }

    /// <summary>Gets the actor ID that was targeted (server-side diagnostics only).</summary>
    public string ActorId { get; }

    /// <summary>Gets the reason for the authorization service failure.</summary>
    public string Reason { get; }
}
