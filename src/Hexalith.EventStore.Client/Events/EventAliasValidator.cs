using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Validates one exact alias's schema, identity extractor and output format.</summary>
/// <param name="domain">The admitted domain.</param>
/// <param name="canonicalType">The alias's canonical type.</param>
/// <param name="alias">The exact case-sensitive V1 discriminator.</param>
/// <param name="sourceVersion">The alias's explicit source version.</param>
/// <param name="format">The exact alias serializer format.</param>
/// <param name="payload">An invocation-scoped immutable output facade.</param>
/// <param name="cancellationToken">The original operation token.</param>
internal delegate void EventAliasValidator(string domain, string canonicalType, string alias, int sourceVersion,
    string format, IReadOnlyPayload payload, CancellationToken cancellationToken);
