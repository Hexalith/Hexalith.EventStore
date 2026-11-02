using System.Collections.Immutable;
using System.Text;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.DomainService;

/// <summary>Detached bounded command data, without live request, state, source or administrator authority.</summary>
public sealed class DomainServiceTerminalRefusalContext
{
    internal DomainServiceTerminalRefusalContext(CommandEnvelope command, int maximumBytes)
    {
        if (command.Extensions is { Count: > 128 })
            throw new InvalidOperationException("PayloadLimit: terminal refusal extensions exceed the finite entry budget.");
        long size = command.Payload.LongLength;
        foreach (string? value in new[] { command.MessageId, command.TenantId, command.Domain, command.AggregateId,
            command.CommandType, command.CorrelationId, command.CausationId, command.UserId })
        {
            if (value is not null) size = checked(size + Encoding.UTF8.GetByteCount(value));
        }
        if (command.Extensions is { } extensions)
            foreach (var entry in extensions)
                size = checked(size + Encoding.UTF8.GetByteCount(entry.Key)
                    + (entry.Value is null ? 0 : Encoding.UTF8.GetByteCount(entry.Value)));
        if (maximumBytes is < 1 or > 1024 * 1024 || size > maximumBytes)
            throw new InvalidOperationException("PayloadLimit: terminal refusal context exceeds the command budget.");

        MessageId = command.MessageId;
        TenantId = command.TenantId;
        Domain = command.Domain;
        AggregateId = command.AggregateId;
        CommandType = command.CommandType;
        CorrelationId = command.CorrelationId;
        CausationId = command.CausationId;
        UserId = command.UserId;
        Payload = ImmutableArray.CreateRange(command.Payload);
        Extensions = command.Extensions?.ToImmutableDictionary(entry => entry.Key, entry => (string?)entry.Value, StringComparer.Ordinal);
    }

    /// <summary>Gets the command identity.</summary>
    public string MessageId { get; }
    /// <summary>Gets the tenant identifier; this value is data, not caller authority.</summary>
    public string TenantId { get; }
    /// <summary>Gets the exact domain.</summary>
    public string Domain { get; }
    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; }
    /// <summary>Gets the exact wire command name.</summary>
    public string CommandType { get; }
    /// <summary>Gets a private immutable copy of the bounded command payload.</summary>
    public ImmutableArray<byte> Payload { get; }
    /// <summary>Gets the correlation identifier.</summary>
    public string CorrelationId { get; }
    /// <summary>Gets the optional causation identifier.</summary>
    public string? CausationId { get; }
    /// <summary>Gets the asserted user identifier as data only.</summary>
    public string UserId { get; }
    /// <summary>Gets a detached ordinal immutable extension snapshot, or null when absent. Raw spelling, ambiguity and null values are data, never authority.</summary>
    public ImmutableDictionary<string, string?>? Extensions { get; }
}
