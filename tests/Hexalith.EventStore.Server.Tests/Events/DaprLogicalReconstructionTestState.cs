namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Exposes an intentionally mutable state for canonical isolation controls.</summary>
internal sealed class DaprLogicalReconstructionTestState
{
    /// <summary>Gets or sets the folded counter.</summary>
    internal int Value
    {
        get; set;
    }
}
