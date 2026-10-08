using System.Globalization;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Describes a trusted host's fixed actor/range logical source; a DTO alone confers no authority.</summary>
/// <param name="ApplicationId">The carried application id.</param>
/// <param name="Namespace">The carried namespace.</param>
/// <param name="ActorType">The carried actor type.</param>
/// <param name="Identity">The carried identity.</param>
/// <param name="AggregateType">The carried aggregate type.</param>
/// <param name="ActorHead">The carried actor head.</param>
/// <param name="TargetSequence">The carried target sequence.</param>
/// <param name="RetainedFloor">The carried retained floor.</param>
/// <param name="SourceConfigurationHash">The carried source configuration hash.</param>
/// <param name="MetadataETag">The carried metadata e tag.</param>
/// <param name="MetadataLastModified">The carried metadata last modified.</param>
/// <param name="MetadataPresent">Whether the owning actor actually returned a metadata value.</param>
internal sealed record DaprLogicalSourceBinding(
    string ApplicationId, string Namespace, string ActorType, AggregateIdentity Identity,
    string AggregateType, long ActorHead, long TargetSequence, long RetainedFloor,
    ReadOnlyMemory<byte> SourceConfigurationHash, string? MetadataETag, DateTimeOffset MetadataLastModified, bool MetadataPresent = true)
{
    /// <summary>Gets the separately identified logical evidence model.</summary>
    internal const string ModelId = "dapr-actor-logical-v1";

    /// <summary>Gets the pinned immutable application key mapping codec.</summary>
    internal const string KeyMappingCodec = "aggregate-identity-decimal-v1";

    /// <summary>Gets the preserved application payload digest codec.</summary>
    internal const string DigestCodec = "eventstore.logical-payload.v1";

    /// <summary>Derives a key solely from the fixed addressed identity and admitted signed sequence.</summary>
    internal string GetEventKey(long sequence)
    {
        if (sequence < 1 || sequence > TargetSequence) { throw new ArgumentOutOfRangeException(nameof(sequence)); }
        return Identity.EventStreamKeyPrefix + sequence.ToString(CultureInfo.InvariantCulture);
    }
}
