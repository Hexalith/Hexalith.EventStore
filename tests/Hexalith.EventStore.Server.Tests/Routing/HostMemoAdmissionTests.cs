using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Proves host byte retention never retains admission authority or leaks into independent host operations.</summary>
public sealed class HostMemoAdmissionTests
{
#if DEBUG
    private static bool PrivateExpected => true;
#else
    private static bool PrivateExpected => false;
#endif

    /// <summary>Only explicit retention makes the first decode of a later acquisition warm; every scope is still new and closing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CrossAcquisitionBenefitRequiresExplicitPrivateOption(bool enabled)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var scopes = new List<RouteCatalogDecodeScope?>();
        var hits = new List<(int Hits, int Captures, bool Memo)>();
        using RouteCatalogAuthority authority = Create(fixture, _ =>
        {
            var current = RouteCatalogDecodeScope.Current;
            scopes.Add(current);
            hits.Add((current?.CacheHits ?? 0, current?.CaptureCount ?? 0, current?.HasHostMemo == true));
            return Task.FromResult(fixture.Observations[0]);
        }, retain: enabled);
        await using (var first = await authority.AcquireAsync()) { }
        await using (var second = await authority.AcquireAsync()) { }
        if (PrivateExpected)
        {
            scopes[0].ShouldNotBeSameAs(scopes[1]);
            hits[0].Hits.ShouldBe(hits[0].Captures - 1);
            hits[1].Memo.ShouldBe(enabled);
            hits[1].Hits.ShouldBe(hits[1].Captures - (enabled ? 0 : 1));
            scopes.All(scope => scope!.Count == 0 && scope.RetainedBytes == 0).ShouldBeTrue();
        }
        else scopes.ShouldAllBe(scope => scope == null);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>A nested disabled or independently enabled host cannot inherit A's active memo during acquisition or release.</summary>
    [Theory]
    [InlineData("disabled")]
    [InlineData("scope-only")]
    [InlineData("enabled")]
    public async Task NestedForeignAcquisitionAndReleaseUseOnlyTheirOwnComputationContext(string mode)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var foreignReads = new List<(RouteCatalogDecodeScope? Scope, bool Memo, int Count)>();
        var foreignStore = Substitute.For<IRouteCatalogStateStore>();
        foreignStore.Coordinates.Returns(fixture.Store.Coordinates);
        foreignStore.ReadAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            var current = RouteCatalogDecodeScope.Current;
            foreignReads.Add((current, current?.HasHostMemo == true, current?.Count ?? 0));
            return fixture.Store.ReadAsync(call.Arg<CancellationToken>());
        });
        foreignStore.CompareExchangeAsync(Arg.Any<RouteCatalogActivationState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => fixture.Store.CompareExchangeAsync(call.Arg<RouteCatalogActivationState>(), call.Arg<string>(), call.Arg<CancellationToken>()));
        using RouteCatalogAuthority foreign = Create(fixture, _ => Task.FromResult(fixture.Observations[0]),
            retain: mode == "enabled", scoped: mode != "disabled", store: foreignStore);
        bool nested = false;
        RouteCatalogDecodeScope? outer = null;
        using RouteCatalogAuthority first = Create(fixture, async _ =>
        {
            if (nested)
            {
                nested = false;
                outer = RouteCatalogDecodeScope.Current;
                int captures = outer?.CaptureCount ?? 0;
                await using (var lease = await foreign.AcquireAsync().WaitAsync(TimeSpan.FromSeconds(5))) { }
                RouteCatalogDecodeScope.Current.ShouldBeSameAs(outer);
                (outer?.CaptureCount ?? 0).ShouldBe(captures);
            }
            return fixture.Observations[0];
        });
        await using (var seed = await first.AcquireAsync()) { }
        nested = true;

        await using (var next = await first.AcquireAsync().WaitAsync(TimeSpan.FromSeconds(5))) { }

        foreignReads.Count.ShouldBe(3); // Initial authority, fresh admission inventory, then completed-lease release.
        foreignReads[0].Count.ShouldBe(0); // A was warm; this exact separate host was not.
        if (PrivateExpected && mode != "disabled") foreignReads[0].Scope.ShouldNotBeNull().ShouldNotBeSameAs(outer);
        else foreignReads[0].Scope.ShouldBeNull();
        foreignReads[0].Memo.ShouldBe(PrivateExpected && mode == "enabled");
        foreignReads[1].Scope.ShouldBeSameAs(foreignReads[0].Scope);
        foreignReads[1].Memo.ShouldBe(PrivateExpected && mode == "enabled");
        foreignReads[2].Memo.ShouldBe(PrivateExpected && mode == "enabled");
        if (mode != "enabled" || !PrivateExpected) foreignReads[2].Scope.ShouldBeNull();
        else foreignReads[2].Scope.ShouldNotBeNull().ShouldNotBeSameAs(outer);
        RouteCatalogDecodeScope.Current.ShouldBeNull();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Warm identical document bytes never replace current epoch, catalog, local host or trust checks.</summary>
    [Theory]
    [InlineData("epoch")]
    [InlineData("catalog")]
    [InlineData("host")]
    [InlineData("trust")]
    [InlineData("different-bytes-same-root")]
    public async Task ChangedAuthorityStillRevalidatesAfterEarlierScopeClosed(string changed)
    {
        var fixture = new RouteCatalogTestFixture();
        RouteCatalogActivation initial = fixture.Candidate();
        await fixture.ActivateAsync(initial);
        bool changedHost = false;
        using RouteCatalogAuthority authority = Create(fixture, _ => Task.FromResult(changedHost
            ? fixture.Observations[0] with { AuthenticationProfile = "changed-authentication"u8.ToArray() } : fixture.Observations[0]));
        await using (var seed = await authority.AcquireAsync()) { }
        RouteCatalogActivation next = fixture.Candidate(2, initial.RootDigest);
        var stored = await fixture.Store.ReadAsync(default);
        var state = changed switch
        {
            "epoch" => stored.State with { Epoch = stored.State.Epoch + 1 },
            "catalog" => stored.State with { Epoch = stored.State.Epoch + 1, Committed = next },
            "trust" => stored.State with { Committed = initial with { TrustProof = "changed-proof" } },
            "different-bytes-same-root" => stored.State with { Committed = initial with { CanonicalCatalog = next.CanonicalCatalog } },
            _ => stored.State,
        };
        (await fixture.Store.CompareExchangeAsync(state, stored.ETag, default)).ShouldBeTrue();
        changedHost = changed == "host";

        if (changed == "different-bytes-same-root")
            (await Should.ThrowAsync<RouteCatalogValidationException>(() => authority.AcquireAsync()))
                .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        else if (changed is "host" or "trust") await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync());
        else
        {
            await using var lease = await authority.AcquireAsync();
            lease.Snapshot.Epoch.ShouldBe(2);
            lease.Snapshot.RootDigest.ShouldBe(changed == "catalog" ? next.RootDigest : initial.RootDigest);
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Host disposal clears only deterministic bytes; an existing fence can still complete, followed by fresh uncached admission.</summary>
    [Fact]
    public async Task DisposalPreservesInFlightFenceCleanupAndFreshFallback()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var borrowed = new List<bool>();
        using RouteCatalogAuthority authority = Create(fixture, _ =>
        {
            borrowed.Add(RouteCatalogDecodeScope.Current?.HasHostMemo == true);
            return Task.FromResult(fixture.Observations[0]);
        });
        RouteCatalogDispatchLease lease = await authority.AcquireAsync();
        var memo = (RouteCatalogValidatedBytesMemo)typeof(RouteCatalogAuthority).GetField("_validatedBytes",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(authority)!;
        memo.Count.ShouldBe(PrivateExpected ? 1 : 0);
        var fence = (await fixture.Store.ReadAsync(default)).State.Leases.Single();

        authority.Dispose();

        memo.IsClosed.ShouldBeTrue();
        memo.Count.ShouldBe(0);
        memo.RetainedBytes.ShouldBe(0);
        (await fixture.Store.ReadAsync(default)).State.Leases.Single().ShouldBe(fence);
        await lease.DisposeAsync();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        await using (var fallback = await authority.AcquireAsync()) { }
        borrowed.ShouldBe([PrivateExpected, false]);
        memo.Count.ShouldBe(0);
    }

    private static RouteCatalogAuthority Create(RouteCatalogTestFixture fixture,
        Func<CancellationToken, Task<RouteCatalogHostObservation>> capture, bool retain = true, bool scoped = true,
        IRouteCatalogStateStore? store = null)
    {
        var options = Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development",
            DevelopmentFixture = true, RequireTechnicalHttpBinding = true, ScopedDecodeReuse = scoped,
            SerializeLocalLeaseRelease = true, RetainValidatedCatalogBytes = retain });
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        return new(store ?? fixture.Store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(capture), environment: environment);
    }
}
