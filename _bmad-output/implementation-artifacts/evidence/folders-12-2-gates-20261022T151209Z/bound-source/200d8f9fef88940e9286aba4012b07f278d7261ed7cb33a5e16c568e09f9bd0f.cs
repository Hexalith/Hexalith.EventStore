using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Checks sticky observed loss and sharing without conferring catalog readiness.</summary>
public sealed class EventEvolutionCapabilityLossTests
{
    /// <summary>Checks default catalogs share the Client load context's sticky process control.</summary>
    [Fact]
    public void DefaultRegistriesShareProcessLossControlWithoutGrantingReadiness()
    {
        using EventDomainRegistry fixture = EventUpcastChainExecutorTests.CreateRegistry();
        ReadOnlyMemory<byte>[] rows = fixture.Rows.Select(static row => (ReadOnlyMemory<byte>)row.Encoded.ToArray()).ToArray();
        using var first = new EventDomainRegistry("d", rows);
        using var second = new EventDomainRegistry("d", rows);

        first.CapabilityLoss.ShouldBeSameAs(EventEvolutionCapabilityLoss.Process);
        second.CapabilityLoss.ShouldBeSameAs(first.CapabilityLoss);
        fixture.CapabilityLoss.ShouldNotBeSameAs(first.CapabilityLoss);
    }

    /// <summary>Checks concurrent observations cannot clear the shared sticky loss state.</summary>
    [Fact]
    public async Task ConcurrentRepeatedObservationsRemainStickyForEveryFollowingCaller()
    {
        var loss = new EventEvolutionCapabilityLoss();
        loss.RequireNoObservedLoss();

        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(loss.ObserveViolation)));

        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            InvalidOperationException failure = Should.Throw<InvalidOperationException>(loss.RequireNoObservedLoss);
            failure.Message.ShouldContain("CapabilityMismatch");
        })));
    }
}
