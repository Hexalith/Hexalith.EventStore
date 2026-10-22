using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Validates event identity before append and at stored-event read boundaries.</summary>
public static class EventIdentityValidator
{
    /// <summary>Validates every required event identity component for a new write.</summary>
    public static void ValidateForWrite(string? tenantId, string? domain, string? aggregateId,
        string? aggregateType, string? eventTypeName, string? messageId, string? correlationId,
        string? causationId, long sequenceNumber)
        => Validate(tenantId, domain, aggregateId, aggregateType, eventTypeName,
            messageId, correlationId, causationId, sequenceNumber, allowLegacyGuid: false,
            optionalDomain: false, optionalAggregateType: false, optionalCausation: false);

    /// <summary>Validates every required identity component of a stored event.</summary>
    public static void ValidateForRead(string? tenantId, string? domain, string? aggregateId,
        string? aggregateType, string? eventTypeName, string? messageId, string? correlationId,
        string? causationId, long sequenceNumber)
        => Validate(tenantId, domain, aggregateId, aggregateType, eventTypeName,
            messageId, correlationId, causationId, sequenceNumber, allowLegacyGuid: true,
            optionalDomain: false, optionalAggregateType: false, optionalCausation: false);

    /// <summary>Validates a stored Contracts metadata object.</summary>
    public static void ValidateForRead(EventMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ValidateForRead(metadata.TenantId, metadata.Domain, metadata.AggregateId,
            metadata.AggregateType, metadata.EventTypeName, metadata.MessageId,
            metadata.CorrelationId, metadata.CausationId, metadata.SequenceNumber);
    }

    /// <summary>Validates a subscription envelope, treating unavailable optional fields as absent.</summary>
    public static void ValidateSubscription(string? tenantId, string? domain, string? aggregateId,
        string? aggregateType, string? eventTypeName, string? messageId, string? correlationId,
        string? causationId, long sequenceNumber)
        => Validate(tenantId, domain, aggregateId, aggregateType, eventTypeName,
            messageId, correlationId, causationId, sequenceNumber, allowLegacyGuid: true,
            optionalDomain: true, optionalAggregateType: true, optionalCausation: true);

    private static void Validate(string? tenantId, string? domain, string? aggregateId,
        string? aggregateType, string? eventTypeName, string? messageId, string? correlationId,
        string? causationId, long sequenceNumber, bool allowLegacyGuid,
        bool optionalDomain, bool optionalAggregateType, bool optionalCausation)
    {
        RequireAggregatePart(tenantId, "TenantId", static value => new AggregateIdentity(value, "valid", "valid"));
        if (!(optionalDomain && domain is null))
        {
            RequireAggregatePart(domain, "Domain", static value => new AggregateIdentity("valid", value, "valid"));
        }
        RequireAggregatePart(aggregateId, "AggregateId", static value => new AggregateIdentity("valid", "valid", value));
        if (!(optionalAggregateType && aggregateType is null) && string.IsNullOrWhiteSpace(aggregateType))
        {
            throw new EventIdentityValidationException("AggregateType");
        }
        if (string.IsNullOrWhiteSpace(eventTypeName))
        {
            throw new EventIdentityValidationException("EventTypeName");
        }
        if (!IsUlid(messageId) && !(allowLegacyGuid && messageId is { Length: 36 }
            && Guid.TryParseExact(messageId, "D", out _)))
        {
            throw new EventIdentityValidationException("MessageId");
        }
        if (!IsTraceId(correlationId))
        {
            throw new EventIdentityValidationException("CorrelationId");
        }
        if (!(optionalCausation && causationId is null) && !IsTraceId(causationId))
        {
            throw new EventIdentityValidationException("CausationId");
        }
        if (sequenceNumber < 1)
        {
            throw new EventIdentityValidationException("SequenceNumber");
        }
    }

    private static void RequireAggregatePart(string? value, string componentName, Func<string, AggregateIdentity> validate)
    {
        try
        {
            _ = validate(value!);
        }
        catch (ArgumentException)
        {
            throw new EventIdentityValidationException(componentName);
        }
    }

    private static bool IsUlid(string? value)
    {
        if (value is not { Length: 26 }) { return false; }
        try
        {
            _ = UniqueIdHelper.ToGuid(value);
            return true;
        }
        catch (Exception error) when (error is FormatException or ArgumentException or OverflowException)
        {
            return false;
        }
    }

    private static bool IsTraceId(string? value)
    {
        if (value is null || value.Length is < 1 or > 128) { return false; }
        foreach (char character in value)
        {
            if (character is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-'))
            {
                return false;
            }
        }
        return true;
    }
}
