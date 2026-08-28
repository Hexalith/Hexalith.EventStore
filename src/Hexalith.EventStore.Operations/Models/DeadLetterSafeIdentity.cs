namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Contains identity fields that are safe to expose without exposing a payload.
/// </summary>
/// <param name="MessageId">The CloudEvent or EventStore message identifier.</param>
/// <param name="TenantId">The tenant identifier, when safely identified.</param>
/// <param name="Domain">The domain, when safely identified.</param>
/// <param name="AggregateId">The aggregate identifier, when safely identified.</param>
/// <param name="CorrelationId">The correlation identifier, when safely identified.</param>
/// <param name="EventType">The event type, when safely identified.</param>
public sealed record DeadLetterSafeIdentity(
    string MessageId,
    string? TenantId,
    string? Domain,
    string? AggregateId,
    string? CorrelationId,
    string? EventType)
{
    /// <summary>Gets the maximum retained length of any identity field.</summary>
    public const int MaxValueLength = 256;

    /// <summary>Gets the tenant scope used for retained envelopes whose tenant cannot be identified safely.</summary>
    public const string UnidentifiedTenantId = "unidentified";

    /// <summary>Gets the placeholder shown for a non-tenant identity field that could not be identified safely.</summary>
    public const string UnknownValue = "unknown";

    /// <summary>Gets a value indicating whether direct replay has a complete safe identity.</summary>
    public bool IsReplayable =>
        IsValidValue(MessageId)
        && IsValidValue(TenantId)
        && IsValidValue(Domain)
        && IsValidValue(AggregateId)
        && IsValidValue(CorrelationId)
        && IsValidValue(EventType);

    internal static bool IsValidValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxValueLength
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && !value.Any(char.IsControl);

    internal bool HasBoundedRetainedValues()
        => IsValidValue(MessageId)
            && IsNullOrValid(TenantId)
            && IsNullOrValid(Domain)
            && IsNullOrValid(AggregateId)
            && IsNullOrValid(CorrelationId)
            && IsNullOrValid(EventType);

    private static bool IsNullOrValid(string? value) => value is null || IsValidValue(value);
}
