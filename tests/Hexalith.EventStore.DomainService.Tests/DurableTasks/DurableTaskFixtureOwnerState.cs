using Hexalith.EventStore.DomainService.DurableTasks;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Retains the synthetic owner's effect history and receipt in one conditional write.</summary>
/// <param name="Effects">The committed synthetic owner events.</param>
/// <param name="Receipt">The receipt committed with the effect.</param>
internal sealed record DurableTaskFixtureOwnerState(DurableTaskStep[] Effects, DurableTaskOwnerReceipt Receipt);
