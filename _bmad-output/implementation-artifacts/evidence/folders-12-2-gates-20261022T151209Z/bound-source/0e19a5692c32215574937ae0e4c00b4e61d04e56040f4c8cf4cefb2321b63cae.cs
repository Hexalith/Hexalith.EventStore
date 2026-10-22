namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Exposes bounded read access to a privately owned immutable payload.</summary>
public interface IReadOnlyPayload {
    /// <summary>Gets the payload byte length.</summary>
    int Length { get; }

    /// <summary>Copies exactly the requested payload range to caller-owned storage.</summary>
    /// <param name="sourceOffset">The zero-based offset in the payload.</param>
    /// <param name="destination">The destination span that receives the payload bytes.</param>
    void CopyTo(int sourceOffset, Span<byte> destination);
}
