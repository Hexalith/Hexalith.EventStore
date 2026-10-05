using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Owns a private effective payload resolved from one allow-listed logical event.</summary>
internal sealed class ResolvedLogicalEvent : IDisposable
{
    private readonly ImmutablePayload _payload;

    /// <summary>Creates a result that takes ownership of the effective payload.</summary>
    internal ResolvedLogicalEvent(string canonicalType, int sourceVersion, int currentVersion,
        string serializationFormat, ImmutablePayload payload)
    {
        CanonicalType = canonicalType;
        SourceVersion = sourceVersion;
        CurrentVersion = currentVersion;
        SerializationFormat = serializationFormat;
        _payload = payload;
    }

    /// <summary>Gets the registered stable event contract type.</summary>
    internal string CanonicalType { get; }

    /// <summary>Gets the retained logical payload version.</summary>
    internal int SourceVersion { get; }

    /// <summary>Gets the resolved current payload version.</summary>
    internal int CurrentVersion { get; }

    /// <summary>Gets the current registered format.</summary>
    internal string SerializationFormat { get; }

    /// <summary>Gets the effective private payload without exposing its backing buffer.</summary>
    internal IReadOnlyPayload Payload => _payload;

    /// <inheritdoc/>
    public void Dispose() => _payload.Dispose();
}
