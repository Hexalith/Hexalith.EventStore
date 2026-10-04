namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Runs synchronously with a temporary writable scratch span.</summary>
/// <param name="span">The privately owned scratch span, valid only during this callback.</param>
public delegate void ScratchSpanAction(Span<byte> span);
