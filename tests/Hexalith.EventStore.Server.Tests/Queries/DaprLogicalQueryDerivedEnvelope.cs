using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Server.Tests.Queries;
/// <summary>Carries an unpinned derived field that logical intake must refuse.</summary>
public sealed record DaprLogicalQueryDerivedEnvelope : QueryEnvelope
{
    public DaprLogicalQueryDerivedEnvelope() : base("tenant", "d", "a", "get-total", "{}"u8.ToArray(), "c", "u")
    {
    }

    public string UnpinnedValue { get; set; } = "substitution";
}
