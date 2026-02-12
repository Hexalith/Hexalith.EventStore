namespace Hexalith.EventStore.Contracts.Events;

using System.Collections.ObjectModel;

/// <summary>
/// The complete serializable event unit combining metadata, payload, and extensions.
/// Record equality uses reference equality for <see cref="Payload"/> (byte[]).
/// Use <see cref="System.Linq.Enumerable.SequenceEqual{TSource}(System.Collections.Generic.IEnumerable{TSource}, System.Collections.Generic.IEnumerable{TSource})"/>
/// for byte-level payload comparison in tests.
/// </summary>
/// <param name="Metadata">The 11-field event metadata.</param>
/// <param name="Payload">The serialized event payload as raw bytes.</param>
/// <param name="Extensions">Optional extension metadata. Null is stored as an empty read-only dictionary.</param>
public record EventEnvelope(EventMetadata Metadata, byte[] Payload, IReadOnlyDictionary<string, string>? Extensions)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyExtensions =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>Gets the 11-field event metadata.</summary>
    public EventMetadata Metadata { get; } = Metadata ?? throw new ArgumentNullException(nameof(Metadata));

    /// <summary>Gets the serialized event payload as raw bytes.</summary>
    public byte[] Payload { get; } = Payload ?? throw new ArgumentNullException(nameof(Payload));

    /// <summary>Gets the extension metadata, guaranteed non-null (empty dictionary if none provided). Defensively copied to preserve immutability.</summary>
    public IReadOnlyDictionary<string, string> Extensions { get; } = Extensions is not null
        ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Extensions))
        : EmptyExtensions;
}
