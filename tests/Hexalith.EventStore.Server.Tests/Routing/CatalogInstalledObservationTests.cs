using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogInstalledObservationTests
{
    [Theory]
    [InlineData("adapter")]
    [InlineData("keys")]
    [InlineData("capability")]
    [InlineData("operation")]
    public async Task TrustedLocallyIncompatibleCandidateCannotBecomeReadyOrReplacePersistedAuthority(string mismatch)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        var candidate = fixture.Candidate(2, first.RootDigest);
        var envelope = RouteCatalogCodec.Decode(candidate.CanonicalCatalog);
        int recipient = Array.FindIndex(fixture.Hosts, h => h.AppId == (mismatch is "adapter" or "keys" ? "eventstore" : "sample"));
        if (mismatch is "adapter" or "keys")
        {
            envelope = RouteCatalogCodec.Seal(envelope with { Idempotency = envelope.Idempotency.Select(entry =>
            {
                if (mismatch == "adapter" && entry.RouteId != "counter-increment-counter") return entry;
                var changed = mismatch == "adapter" ? entry with { AdapterId = "incompatible-installed-adapter" }
                    : entry with { SecretMapGeneration = "incompatible-key-map" };
                return changed with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(changed) };
            }).ToArray() });
        }
        else if (mismatch == "capability")
        {
            var services = new ServiceCollection();
            var handler = Substitute.For<IAsyncDomainProjectionHandler>();
            handler.Domain.Returns("counter"); handler.ProjectionType.Returns("counter"); handler.OrdinaryDeliveryEnabled.Returns(true);
            services.AddSingleton(handler); services.AddSingleton(AssemblyScanner.ScanForDomainTypes(typeof(Hexalith.EventStore.Sample.Counter.CounterAggregate).Assembly.GetTypes()));
            services.AddOptions<ProjectionDispatchOptions>();
            services.Configure<DomainProjectionIdentityOptions>(options => { options.AppId = "sample"; options.ServiceVersion = "v1"; });
            using var provider = services.BuildServiceProvider();
            var actual = RouteCatalogDomainObservation.Capabilities(provider);
            actual.ShouldHaveSingleItem().ProjectionTypes.ShouldBe(["counter"]);
            envelope = RouteCatalogCodec.Seal(envelope with { Routes = envelope.Routes.Select(r => r.RouteId == "counter-projection" ? r with { Method = "project/v2" } : r).ToArray() });
            fixture.Observations[recipient] = fixture.Observations[recipient] with
            {
                Targets = envelope.Routes.Where(r => r.AppId == "sample").ToArray(), ProjectionCapabilities = actual,
            };
            candidate = candidate with { ProjectionCapabilities = [actual[0] with { Fingerprint = RouteCatalogTestFixture.Hash("wrong-capability") }] };
        }
        else
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.Configure<DomainProjectionIdentityOptions>(options => { options.AppId = "sample"; options.ServiceVersion = "v1"; });
            builder.Services.AddSingleton(AssemblyScanner.ScanForDomainTypes(typeof(Hexalith.EventStore.Sample.Counter.CounterAggregate).Assembly.GetTypes()));
            await using var app = builder.Build();
            app.MapPost("/replay-state", () => "fixture").RequireEventStoreDomainServicePolicy("/replay-state");
            var actual = RouteCatalogDomainObservation.Auxiliary(app.Services, ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints));
            actual.ShouldContain(a => a.Domain == "counter" && a.Operation == "domain-service:replay-state");
            fixture.Observations[recipient] = fixture.Observations[recipient] with { AuxiliaryOperations = actual };
            candidate = candidate with { AuxiliaryOperations = [actual.Single(a => a.Domain == "counter") with { Operation = "domain-service:project" }] };
        }
        var actualIdentity = ObservedRouteCatalogHost.Identity(fixture.Observations[recipient]);
        candidate = candidate with
        {
            CanonicalCatalog = RouteCatalogCodec.Encode(envelope), RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope),
            RequiredHosts = fixture.Hosts.Select(h => h.HostId == actualIdentity.HostId ? actualIdentity : h).ToArray(),
        };
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        // A different host can prepare the valid owner-trusted candidate, but its affected recipient must refuse readiness.
        int preparer = mismatch is "adapter" or "keys" ? 1 : 0;
        await fixture.Authorities[preparer].PrepareAsync(candidate);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.CreateAuthority(actualIdentity).ReadyAsync(candidate.AttemptId));
        var persisted = (await fixture.Store.ReadAsync(default)).State;
        persisted.Committed!.RootDigest.ShouldBe(first.RootDigest);
        persisted.Committed.CanonicalCatalog.ShouldBe(first.CanonicalCatalog);
        persisted.Ready.ShouldBeEmpty();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[preparer].AcquireAsync());
        await fixture.Authorities[preparer].RollbackAsync(new(candidate.AttemptId, RouteCatalogActivationBinding.ComputeDigest(candidate), persisted.Epoch, "pending"));
        var compatibleEnvelope = mismatch is "adapter" or "keys" ? RouteCatalogCodec.Decode(fixture.Candidate(2, first.RootDigest).CanonicalCatalog) : envelope;
        var compatible = candidate with
        {
            AttemptId = candidate.AttemptId + "-compatible", CanonicalCatalog = RouteCatalogCodec.Encode(compatibleEnvelope),
            RootDigest = RouteCatalogCodec.ComputeRootDigest(compatibleEnvelope),
            ProjectionCapabilities = mismatch == "capability" ? fixture.Observations[recipient].ProjectionCapabilities : candidate.ProjectionCapabilities,
            AuxiliaryOperations = mismatch == "operation" ? fixture.Observations[recipient].AuxiliaryOperations : candidate.AuxiliaryOperations,
        };
        compatible = compatible with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(compatible) };
        await fixture.Authorities[preparer].PrepareAsync(compatible);
        foreach (var host in compatible.RequiredHosts) await fixture.CreateAuthority(host).ReadyAsync(compatible.AttemptId);
        await fixture.Authorities[preparer].CommitAsync(compatible.AttemptId);
        (await fixture.CreateAuthority(actualIdentity).ReadbackAsync()).Serving.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstalledAdmissionObservationRefusesDisabledPolicyOrUnavailableKeyGeneration(bool enabled)
    {
        var services = new ServiceCollection();
        RouteCatalogAdmissionFixture.Configure(services, Hexalith.EventStore.Sample.Counter.SampleCatalog.CreateAdapters());
        services.AddSingleton<IOptions<Hexalith.EventStore.Server.Configuration.IdempotencyAdmissionOptions>>(Options.Create(
            new Hexalith.EventStore.Server.Configuration.IdempotencyAdmissionOptions
            {
                Enabled = enabled, ActiveDigestKeyVersion = "development-v1",
                DigestKeySource = Hexalith.EventStore.Server.Configuration.IdempotencyDigestKeySource.Configuration,
            }));
        using var provider = services.BuildServiceProvider();
        await Should.ThrowAsync<InvalidOperationException>(() => Hexalith.EventStore.Server.Routing.RouteCatalogAdmissionObservation.CaptureAsync(provider, "eventstore"));
    }

    [Fact]
    public void MissingIndependentPolicyFactsCannotProduceAHostIdentity()
    {
        var fixture = new RouteCatalogTestFixture();
        Should.Throw<RouteCatalogUnavailableException>(() => ObservedRouteCatalogHost.Identity(fixture.Observations[0] with { AdmissionPolicies = null }));
        Should.Throw<RouteCatalogUnavailableException>(() => ObservedRouteCatalogHost.Identity(fixture.Observations[0] with { ProjectionCapabilities = null }));
        Should.Throw<RouteCatalogUnavailableException>(() => ObservedRouteCatalogHost.Identity(fixture.Observations[0] with { AuxiliaryOperations = null }));
    }

    [Fact]
    public async Task QuiescentOwnLeaseReleaseMakesProgressPast32ConflictsAndConcurrentDispatchDrains()
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var contended = new CatalogContendedStateStore(fixture.Store);
        var options = Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true });
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
        var authority = new RouteCatalogAuthority(contended, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])));
        var lease = await authority.AcquireAsync(); contended.ConflictsRemaining = 64;
        await lease.DisposeAsync(); contended.ConflictsObserved.ShouldBe(64);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        long firstDispatch = (await fixture.Store.ReadAsync(default)).State.LastLease;
        for (int cohort = 0; cohort < 4; cohort++)
        {
            var acquireStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var acquisitions = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
            {
                await acquireStart.Task;
                return await authority.AcquireAsync();
            })).ToArray();
            acquireStart.SetResult();
            var dispatches = await Task.WhenAll(acquisitions);
            // Hold the complete cohort's actual durable fences before releasing any.
            var held = (await fixture.Store.ReadAsync(default)).State;
            held.Leases.Count.ShouldBe(12);
            held.Leases.Select(fence => fence.LeaseId).Distinct().Count().ShouldBe(12);
            held.LastLease.ShouldBe(firstDispatch + (cohort + 1) * 12);
            var releaseStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var drains = dispatches.Select(dispatch => Task.Run(async () =>
            {
                await releaseStart.Task;
                await dispatch.DisposeAsync();
            })).ToArray();
            releaseStart.SetResult();
            await Task.WhenAll(drains);
            (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        }
        (await fixture.Store.ReadAsync(default)).State.LastLease.ShouldBe(firstDispatch + 48);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        (await authority.ReadbackAsync()).Serving.ShouldBeTrue();
    }

    [Fact]
    public async Task AcquisitionRefusesAfter32ConflictsWithoutLeakingAndLaterSucceeds()
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var contended = new CatalogContendedStateStore(fixture.Store) { ConflictsRemaining = 32 };
        var options = Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true });
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
        var authority = new RouteCatalogAuthority(contended, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])));
        var before = await fixture.Store.ReadAsync(default);
        var refusal = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync());
        refusal.Message.ShouldBe("Concurrent catalog authority updates could not be serialized.");
        contended.ConflictsObserved.ShouldBe(32);
        contended.ConflictsRemaining.ShouldBe(0);
        var refused = await fixture.Store.ReadAsync(default);
        refused.ETag.ShouldBe(before.ETag);
        refused.State.Leases.ShouldBeEmpty();
        refused.State.LastLease.ShouldBe(before.State.LastLease);
        refused.State.Committed!.RootDigest.ShouldBe(before.State.Committed!.RootDigest);
        (await authority.ReadbackAsync()).Serving.ShouldBeTrue();
        var lease = await authority.AcquireAsync();
        var admitted = (await fixture.Store.ReadAsync(default)).State;
        admitted.Leases.ShouldHaveSingleItem();
        admitted.LastLease.ShouldBe(before.State.LastLease + 1);
        lease.Snapshot.RootDigest.ShouldBe(before.State.Committed.RootDigest);
        await lease.DisposeAsync();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        (await authority.ReadbackAsync()).Serving.ShouldBeTrue();
    }
}
