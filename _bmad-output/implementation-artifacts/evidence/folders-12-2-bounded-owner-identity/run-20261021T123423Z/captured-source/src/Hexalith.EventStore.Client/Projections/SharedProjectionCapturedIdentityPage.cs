namespace Hexalith.EventStore.Client.Projections;

/// <summary>One bounded immutable leaf or index node of a captured owner identity tree.</summary>
/// <param name="Level">Zero for a leaf; otherwise one greater than its children's level.</param>
/// <param name="FirstSequence">The first original owner sequence covered by this node.</param>
/// <param name="Count">The exact number of original events covered by this node.</param>
/// <param name="LastPosition">The final original source position covered by this node.</param>
/// <param name="Positions">Original positions for a leaf.</param>
/// <param name="Digests">Canonical SHA-256 event identities for a leaf.</param>
/// <param name="MessageHashes">SHA-256 message identities, or empty strings for absent identities.</param>
/// <param name="Children">Content-addressed child digests for an index.</param>
/// <param name="ChildCounts">The exact event count covered by each child.</param>
public sealed record SharedProjectionCapturedIdentityPage(
    int Level, long FirstSequence, long Count, long LastPosition,
    long[] Positions, byte[] Digests, string[] MessageHashes, string[] Children, long[] ChildCounts);
