namespace Hexalith.EventStore.DomainService;

/// <summary>Bounded, payload-free result of one hosted recovery pass.</summary>
/// <param name="Sources">Sources examined in this pass.</param>
/// <param name="CycleComplete">Whether both durable sweeps finished.</param>
/// <param name="Unresolved">Unresolved source or scan observations in the cycle.</param>
public sealed record ProcessReconciliationPass(int Sources, bool CycleComplete, int Unresolved);
