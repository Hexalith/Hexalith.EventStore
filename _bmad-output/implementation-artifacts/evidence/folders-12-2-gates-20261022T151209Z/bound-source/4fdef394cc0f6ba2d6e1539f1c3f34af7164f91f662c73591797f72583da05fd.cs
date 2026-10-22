using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Optional private host binding enforced before event persistence; qualifying a namespace requires every writer to use it.</summary>
public interface ISourcePublicationWriterRegistration
{
    /// <summary>Durably registers an exact source before its first/new append; missing qualified installation fails the configured write.</summary>
    Task RegisterBeforeWriteAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);
}
