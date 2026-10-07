namespace Hexalith.EventStore.Client.Events;

/// <summary>Offsets into a privately owned, structurally admitted proof container.</summary>
internal readonly record struct EventEvolutionProofEntrySlice(
    int ClaimOffset, int ClaimLength, int KeyOffset, int KeyLength, int SignatureOffset);
