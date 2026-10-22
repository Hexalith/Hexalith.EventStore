using Hexalith.EventStore.Contracts.Processes;

namespace Hexalith.EventStore.DomainService;

/// <summary>Bounded original effects retained before submission until every target receipt is acknowledged.</summary>
/// <param name="Sequence">Committed source envelope position.</param>
/// <param name="EvidenceDigest">Digest binding the immutable committed envelope semantics.</param>
/// <param name="Effects">Copied deterministic effect plan, including decoded no-op plans.</param>
public sealed record ProcessEnvelopePlan(long Sequence, string EvidenceDigest, IReadOnlyList<ProcessEffect> Effects);
