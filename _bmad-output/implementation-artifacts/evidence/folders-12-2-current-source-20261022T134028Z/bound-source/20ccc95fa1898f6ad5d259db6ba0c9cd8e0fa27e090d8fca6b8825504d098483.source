using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Projects closed safe publication metadata from an authenticated contiguous source prefix.</summary>
public interface ISourcePublicationProjector
{
    /// <summary>Validates the complete source and projects its existing source-atomic publication events.</summary>
    /// <remarks>Descriptors carry no general content or secrets. Unsupported or conflicting source semantics must fail closed.</remarks>
    IReadOnlyList<SourcePublicationDescriptor> Project(AuthoritativeEventStream source, CancellationToken cancellationToken = default);
}
