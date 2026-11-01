using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises the real retained-file bootstrap path, serialized CAS authority and mandatory owner reconciliation.</summary>
public sealed class DevelopmentPredecessorBootstrapTests
{
    [Fact]
    public async Task RetainedFenceReachesPreparedSuccessorThenOnlyActualOwnerCompletionAllowsCommit()
        => await VerifyAsync("held-fence");

    [Fact]
    public async Task PeerDelayedAfterObservationAcceptsOnlyTheExactAlreadyPreparedSuccessor()
        => await VerifyAsync("late-peer");

    [Theory]
    [InlineData("foreign-attempt")]
    [InlineData("missing-attempt")]
    [InlineData("duplicate-attempt")]
    [InlineData("changed-binding")]
    [InlineData("wrong-predecessor")]
    [InlineData("wrong-old-epoch")]
    public async Task DelayedPeerRejectsForeignOrUnrecordedSuccessorWithoutReadyOrCommit(string scenario)
        => await VerifyAsync(scenario);

    private static async Task VerifyAsync(string scenario)
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var verifier = new ObservedQuiescenceFixture();
        var execution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RouteCatalogDispatchFence? fence = null;
        if (scenario == "held-fence")
        {
            var lease = await fixture.Authorities[0].AcquireAsync();
            lease.Retain();
            await lease.DisposeAsync();
            fence = (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
            verifier.Observe(fence, execution.Task);
        }
        var held = await fixture.Authorities[0].ReadbackAsync();
        string directory = Directory.CreateTempSubdirectory("predecessor-bootstrap-").FullName;
        string predecessor = Path.Combine(directory, "retained.activation.json"), readback = Path.Combine(directory, "retained.readback.json");
        await File.WriteAllBytesAsync(predecessor, JsonSerializer.SerializeToUtf8Bytes(first));
        await File.WriteAllBytesAsync(readback, JsonSerializer.SerializeToUtf8Bytes(held));
        fixture.Store.Coordinates = fixture.Store.Coordinates with { HttpEndpoint = "http://127.0.0.1:3600/" };
        for (int index = 0; index < fixture.Observations.Length; index++)
            fixture.Observations[index] = fixture.Observations[index] with
            {
                AuthorityCoordinates = fixture.Store.Coordinates,
                TopologyDocuments = [.. fixture.Observations[index].TopologyDocuments, Encoding.UTF8.GetBytes("independently-observed-successor-sidecar")],
            };
        var nextEnvelope = RouteCatalogCodec.Decode(fixture.Candidate(2, first.RootDigest).CanonicalCatalog);
        string[] hosts = fixture.Observations.Select(observation => observation.HostId).ToArray();
        var providers = new List<ServiceProvider>();
        var tasks = new List<Task>();
        var delayed = new BootstrapReadGateStore(fixture.Store);
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            foreach (var actual in fixture.Observations)
            {
                var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(Environments.Development);
                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:PredecessorActivationPath"] = predecessor,
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:PredecessorReadbackPath"] = readback,
                }).Build());
                services.AddSingleton<IRouteCatalogStateStore>(actual.HostId == hosts[^1] ? delayed : fixture.Store);
                services.AddSingleton<IOptions<RouteCatalogHostOptions>>(Options.Create(new RouteCatalogHostOptions { Profile = "Development", DevelopmentFixture = true }));
                services.AddSingleton<IRouteCatalogAuthority>(provider => new RouteCatalogAuthority(provider.GetRequiredService<IRouteCatalogStateStore>(),
                    new DevelopmentRouteCatalogTrustValidator(provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(), environment),
                    provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(), new ObservedRouteCatalogHost(_ => Task.FromResult(actual)), verifier));
                var provider = services.BuildServiceProvider(); providers.Add(provider);
                tasks.Add(DevelopmentRouteCatalogBootstrap.ActivateAsync(provider, directory, actual.HostId, hosts[0], "successor-private-attempt", hosts,
                    (_, _) => Task.FromResult(actual), actual.HostId == hosts[0] ? _ => nextEnvelope : null, bound.Token, first.RootDigest));
            }
            await delayed.Entered.Task.WaitAsync(bound.Token);
            RouteCatalogActivationState state;
            while ((state = (await fixture.Store.ReadAsync(bound.Token)).State).Pending is null)
            {
                if (tasks.FirstOrDefault(task => task.IsFaulted) is { } failed) await failed;
                await Task.Delay(10, bound.Token);
            }
            var successor = state.Pending.ShouldNotBeNull();
            successor.PredecessorRootDigest.ShouldBe(first.RootDigest);
            successor.AttemptId.ShouldBe("successor-private-attempt");
            (await fixture.Store.ReadAsync(default)).State.Committed!.RootDigest.ShouldBe(first.RootDigest);
            var coordinator = providers[0].GetRequiredService<IRouteCatalogAuthority>();
            if (fence is not null)
            {
                var draining = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => coordinator.ReadyAsync(successor.AttemptId));
                draining.Category.ShouldBe(RouteCatalogRefusalReason.Draining);
                (await fixture.Store.ReadAsync(default)).State.Ready.ShouldBeEmpty();
                await Should.ThrowAsync<RouteCatalogUnavailableException>(() => coordinator.AcquireAsync());
                var before = JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State);
                await Should.ThrowAsync<RouteCatalogUnavailableException>(() => coordinator.ReconcileAsync(new(fence.LeaseId, fence.RootDigest, fence.Epoch, "unobserved-owner-proof")));
                JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State).ShouldBe(before);
                execution.SetResult(); // Actual registered server execution is now complete; elapsed time alone never authorizes removal.
                await coordinator.ReconcileAsync(verifier.Issue(fence));
                var reconciled = (await fixture.Store.ReadAsync(default)).State;
                reconciled.Leases.ShouldBeEmpty(); reconciled.Epoch.ShouldBe(held.Epoch);
                reconciled.Committed!.RootDigest.ShouldBe(first.RootDigest);
            }
            while ((state = (await fixture.Store.ReadAsync(bound.Token)).State).Ready.Count != hosts.Length - 1)
            {
                if (tasks.FirstOrDefault(task => task.IsFaulted) is { } failed) await failed;
                await Task.Delay(10, bound.Token);
            }
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => coordinator.CommitAsync(successor.AttemptId));
            if (scenario is not ("held-fence" or "late-peer"))
            {
                var (current, etag) = await fixture.Store.ReadAsync(bound.Token);
                var pending = current.Pending!;
                if (scenario == "foreign-attempt") current = current with { Pending = pending with { AttemptId = "foreign-attempt" } };
                else if (scenario == "missing-attempt") current = current with { Attempts = current.Attempts.Where(attempt => attempt.AttemptId != pending.AttemptId).ToArray() };
                else if (scenario == "duplicate-attempt") current = current with { Attempts = [.. current.Attempts, current.Attempts.Single(attempt => attempt.AttemptId == pending.AttemptId)] };
                else if (scenario == "wrong-old-epoch") current = current with { Epoch = current.Epoch + 1 };
                else
                {
                    pending = scenario == "wrong-predecessor" ? pending with { PredecessorRootDigest = new string('0', 64) }
                        : pending with { RequiredHosts = pending.RequiredHosts.Select((host, index) => index == 0 ? host with { TopologyDigest = RouteCatalogTestFixture.Hash("changed") } : host).ToArray() };
                    pending = pending with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(pending) };
                    current = current with { Pending = pending };
                }
                (await fixture.Store.CompareExchangeAsync(current, etag, bound.Token)).ShouldBeTrue();
                string before = JsonSerializer.Serialize(current);
                delayed.Continue.SetResult();
                await Should.ThrowAsync<RouteCatalogUnavailableException>(() => tasks[^1]);
                JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State).ShouldBe(before);
                File.Exists(Path.Combine(directory, hosts[^1] + ".ready.json")).ShouldBeFalse();
                Directory.GetFiles(directory, "*.committed.json").ShouldBeEmpty();
                return;
            }
            delayed.Continue.SetResult();
            await Task.WhenAll(tasks).WaitAsync(bound.Token);
            var committed = (await fixture.Store.ReadAsync(default)).State;
            committed.Epoch.ShouldBe(held.Epoch + 1); committed.Leases.ShouldBeEmpty(); committed.Pending.ShouldBeNull();
            committed.Committed!.RootDigest.ShouldBe(RouteCatalogCodec.ComputeRootDigest(nextEnvelope));
            committed.Committed.RequiredHosts.ShouldBe(fixture.Observations.Select(ObservedRouteCatalogHost.Identity));
            Directory.GetFiles(directory, "*.committed.json").Length.ShouldBe(hosts.Length);
        }
        finally
        {
            bound.Cancel(); delayed.Continue.TrySetResult();
            try { await Task.WhenAll(tasks); } catch (Exception) { /* Observe genuine refusal/cancellation before deleting this owned test directory. */ }
            foreach (var provider in providers) await provider.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
