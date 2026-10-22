namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Observable state used to prove canonical event and JSON restore parity.</summary>
public sealed class ParityState
{
    /// <summary>Gets or sets the total of canonical event values.</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the count of applied events.</summary>
    public int EventCount { get; set; }

    /// <summary>Applies an event and mutates only the detached Apply input.</summary>
    public void Apply(ParityEvent payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Total += payload.Amount + payload.Defaulted + payload.Ignored;
        EventCount++;
        payload.Amount = 500;
    }
}
