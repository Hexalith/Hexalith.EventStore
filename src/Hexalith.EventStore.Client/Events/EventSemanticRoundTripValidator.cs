using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Requires identity and semantic preservation for one exact current-to-legacy conversion.</summary>
/// <param name="current">The invocation-scoped current bytes.</param>
/// <param name="legacy">The invocation-scoped alias-specific output bytes.</param>
/// <param name="cancellationToken">The original operation token.</param>
internal delegate void EventSemanticRoundTripValidator(IReadOnlyPayload current, IReadOnlyPayload legacy, CancellationToken cancellationToken);
