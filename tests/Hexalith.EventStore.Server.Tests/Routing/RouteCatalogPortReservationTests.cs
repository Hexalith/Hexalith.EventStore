using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises fixture ownership with in-memory reservations, never sockets or live fixtures.</summary>
public sealed class RouteCatalogPortReservationTests
{
    [Fact]
    public void ConstructionRetainsAllSixReservations()
    {
        var owner = new Reservations();
        using var ports = new RouteCatalogPortReservations(owner.Reserve);
        owner.Held.Count.ShouldBe(6);
        Enumerable.Range(0, 6).Select(index => ports[index]).ShouldBe(owner.Held, ignoreOrder: true);
        owner.Released.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplicationHandoffReleasesOnlyItsPortAtConsumerStartupAsync()
    {
        var owner = new Reservations();
        using var ports = new RouteCatalogPortReservations(owner.Reserve);
        await ports.StartApplicationAsync(() =>
        {
            owner.Held.ShouldBe(Enumerable.Range(0, 5).Select(index => ports[index]), ignoreOrder: true);
            owner.Released.ShouldBe([ports[5]]);
            return Task.CompletedTask;
        });
        owner.Held.Count.ShouldBe(5);
    }

    [Fact]
    public void SidecarHandoffReleasesOnlyItsFivePortsAtConsumerStartup()
    {
        var owner = new Reservations();
        using var ports = new RouteCatalogPortReservations(owner.Reserve);
        ports.StartSidecar(() =>
        {
            owner.Held.ShouldBe([ports[5]]);
            owner.Released.ShouldBe(Enumerable.Range(0, 5).Select(index => ports[index]));
        });
        owner.Held.ShouldBe([ports[5]]);
    }

    [Fact]
    public async Task AppStartupCannotStealAnySidecarReservationAsync()
    {
        var owner = new Reservations();
        using var ports = new RouteCatalogPortReservations(owner.Reserve);
        await ports.StartApplicationAsync(() =>
        {
            Enumerable.Range(0, 5).All(index => owner.Held.Contains(ports[index])).ShouldBeTrue();
            return Task.CompletedTask;
        });
        ports.StartSidecar(() => owner.Held.ShouldBeEmpty());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void PartialAcquisitionReleasesExactlyAlreadyAcquiredReservations(int failAt)
    {
        var first = new InvalidOperationException("first acquisition failure");
        var owner = new Reservations { FailAt = failAt, Failure = first };
        Should.Throw<InvalidOperationException>(() => new RouteCatalogPortReservations(owner.Reserve)).ShouldBeSameAs(first);
        owner.Held.ShouldBeEmpty();
        owner.Released.Count.ShouldBe(failAt);
        owner.Released.Distinct().Count().ShouldBe(failAt);
    }

    [Fact]
    public void DisposalBeforeStartupReleasesEveryOwnedReservationExactlyOnce()
    {
        var owner = new Reservations();
        var ports = new RouteCatalogPortReservations(owner.Reserve);
        ports.Dispose();
        ports.Dispose();
        owner.Held.ShouldBeEmpty();
        owner.Released.Count.ShouldBe(6);
        owner.Released.Distinct().Count().ShouldBe(6);
    }

    [Fact]
    public void DisposalNeverReleasesAnotherFixturesReservations()
    {
        var first = new Reservations();
        var foreign = new Reservations();
        using var other = new RouteCatalogPortReservations(foreign.Reserve);
        var ports = new RouteCatalogPortReservations(first.Reserve);
        ports.Dispose();
        first.Held.ShouldBeEmpty();
        foreign.Held.Count.ShouldBe(6);
        foreign.Released.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplicationFailurePreservesFirstOutcomeAndReleasesPendingSidecarPortsAsync(bool synchronous)
    {
        var first = new InvalidOperationException("first application failure");
        var owner = new Reservations();
        using var ports = new RouteCatalogPortReservations(owner.Reserve);
        Func<Task> start = synchronous ? () => throw first : () => Task.FromException(first);
        (await Should.ThrowAsync<InvalidOperationException>(() => ports.StartApplicationAsync(start))).ShouldBeSameAs(first);
        owner.Held.ShouldBeEmpty();
        owner.Released.Count.ShouldBe(6);
    }

    [Fact]
    public void ProcessLaunchFailurePreservesFirstOutcomeAndReleasesUnusedAppPort()
    {
        var first = new InvalidOperationException("first launch failure");
        var owner = new Reservations();
        using var ports = new RouteCatalogPortReservations(owner.Reserve);
        Should.Throw<InvalidOperationException>(() => ports.StartSidecar(() => throw first)).ShouldBeSameAs(first);
        owner.Held.ShouldBeEmpty();
        owner.Released.Count.ShouldBe(6);
    }

    [Fact]
    public void DisposalAttemptsAllReleasesAndPreservesFirstReleaseFailure()
    {
        var first = new InvalidOperationException("first release failure");
        var owner = new Reservations { ReleaseFailure = first };
        var ports = new RouteCatalogPortReservations(owner.Reserve);
        Should.Throw<InvalidOperationException>(ports.Dispose).ShouldBeSameAs(first);
        owner.Held.ShouldBeEmpty();
        owner.Released.Count.ShouldBe(6);
        ports.Dispose();
        owner.Released.Count.ShouldBe(6);
    }

    [Fact]
    public async Task AppReservationReleaseFailurePreventsStartupAndCleansOtherPortsAsync()
    {
        var first = new InvalidOperationException("app release failure");
        var owner = new Reservations { ThrowOnRelease = 5, ReleaseFailure = first };
        var ports = new RouteCatalogPortReservations(owner.Reserve);
        bool started = false;
        (await Should.ThrowAsync<InvalidOperationException>(() => ports.StartApplicationAsync(() => { started = true; return Task.CompletedTask; }))).ShouldBeSameAs(first);
        started.ShouldBeFalse();
        owner.Held.ShouldBeEmpty();
        owner.Released.Count.ShouldBe(6);
    }

    [Fact]
    public void SidecarReservationReleaseFailurePreventsLaunchAndCleansOtherPorts()
    {
        var first = new InvalidOperationException("sidecar release failure");
        var owner = new Reservations { ReleaseFailure = first };
        var ports = new RouteCatalogPortReservations(owner.Reserve);
        bool started = false;
        Should.Throw<InvalidOperationException>(() => ports.StartSidecar(() => started = true)).ShouldBeSameAs(first);
        started.ShouldBeFalse();
        owner.Held.ShouldBeEmpty();
        owner.Released.Count.ShouldBe(6);
    }

    [Fact]
    public void AcquisitionFailureIsNotOverwrittenByCleanupFailure()
    {
        var first = new InvalidOperationException("first acquisition failure");
        var owner = new Reservations { FailAt = 3, Failure = first, ReleaseFailure = new IOException("later cleanup failure") };
        Should.Throw<InvalidOperationException>(() => new RouteCatalogPortReservations(owner.Reserve)).ShouldBeSameAs(first);
        owner.Held.ShouldBeEmpty();
        owner.Released.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ApplicationFailureIsNotOverwrittenByCleanupFailureAsync()
    {
        var first = new InvalidOperationException("first application failure");
        var owner = new Reservations { ReleaseFailure = new IOException("later cleanup failure") };
        var ports = new RouteCatalogPortReservations(owner.Reserve);
        (await Should.ThrowAsync<InvalidOperationException>(() => ports.StartApplicationAsync(() => Task.FromException(first)))).ShouldBeSameAs(first);
        owner.Held.ShouldBeEmpty();
        owner.Released.Count.ShouldBe(6);
    }

    private sealed class Reservations
    {
        private int _next;
        internal HashSet<int> Held { get; } = [];
        internal List<int> Released { get; } = [];
        internal int FailAt { get; init; } = -1;
        internal int ThrowOnRelease { get; init; }
        internal Exception Failure { get; init; } = new InvalidOperationException("allocation failed");
        internal Exception? ReleaseFailure { get; init; }

        internal (int Port, Action Release) Reserve()
        {
            int index = _next++;
            if (index == FailAt) throw Failure;
            int port = 20000 + index;
            Held.Add(port).ShouldBeTrue();
            return (port, () =>
            {
                Held.Remove(port).ShouldBeTrue();
                Released.Add(port);
                if (index == ThrowOnRelease && ReleaseFailure is not null) throw ReleaseFailure;
            });
        }
    }
}
