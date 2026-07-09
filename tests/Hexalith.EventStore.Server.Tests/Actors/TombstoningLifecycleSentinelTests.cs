using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Testing.Compliance;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Story R1-A7 / ADR R1A7-02: paired R1-A2 sentinel pin in Server.Tests. Lives in a NON-collection
/// class deliberately so it does NOT pay the live <c>daprd</c> startup cost; the assertion is a
/// pure Tier 1 reflection check.
/// </summary>
public class TombstoningLifecycleSentinelTests
{
    [Fact]
    public void Counter_TerminatableComplianceMatchesRuntime()
        => TerminatableComplianceAssertions.AssertTerminatableCompliance<CounterState>();
}
