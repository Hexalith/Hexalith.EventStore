namespace Hexalith.EventStore.Server.Commands;

/// <summary>Represents a stable support-safe fail-closed admission or reconciliation outcome.</summary>
public sealed class IdempotencyAdmissionFailureException : InvalidOperationException
{
    /// <summary>Initializes a stable admission failure without retaining a potentially sensitive inner exception.</summary>
    public IdempotencyAdmissionFailureException(
        string correlationId,
        string code,
        string category,
        bool retryable,
        string clientAction,
        int statusCode,
        string detail)
        : base(detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientAction);
        CorrelationId = correlationId;
        Code = code;
        Category = category;
        Retryable = retryable;
        ClientAction = clientAction;
        StatusCode = statusCode;
    }

    /// <summary>Gets the current request correlation identifier.</summary>
    public string CorrelationId { get; }

    /// <summary>Gets the stable machine-readable code.</summary>
    public string Code { get; }

    /// <summary>Gets the stable error category.</summary>
    public string Category { get; }

    /// <summary>Gets whether a later identical request may succeed.</summary>
    public bool Retryable { get; }

    /// <summary>Gets stable client remediation guidance.</summary>
    public string ClientAction { get; }

    /// <summary>Gets the HTTP status selected by the gateway mapping.</summary>
    public int StatusCode { get; }
}
