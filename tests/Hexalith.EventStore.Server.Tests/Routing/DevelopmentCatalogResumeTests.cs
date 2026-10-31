using System.Text.Json;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Ordinary process restarts validate the actual retained authority instead of rewriting readiness receipts.</summary>
public sealed class DevelopmentCatalogResumeTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("retained-lease")]
    [InlineData("topology")]
    [InlineData("authentication")]
    [InlineData("required-host")]
    [InlineData("activation-file")]
    [InlineData("pending-successor")]
    public async Task ResumeRequiresExactIndependentObservationAndRetainedCasGeneration(string scenario)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        if (scenario == "retained-lease")
        {
            var lease = await fixture.Authorities[0].AcquireAsync();
            lease.Retain();
            await lease.DisposeAsync();
        }
        if (scenario == "pending-successor") await fixture.Authorities[0].PrepareAsync(fixture.Candidate(2, first.RootDigest));
        string directory = Directory.CreateTempSubdirectory("catalog-resume-").FullName;
        try
        {
            string activation = Path.Combine(directory, "activation.json");
            await File.WriteAllBytesAsync(activation, JsonSerializer.SerializeToUtf8Bytes(scenario == "activation-file"
                ? first with { AttemptId = "different-attempt" } : first));
            string marker = Path.Combine(directory, fixture.Hosts[0].HostId + ".committed.json");
            byte[] oldMarker = "historical-commit-receipt"u8.ToArray();
            await File.WriteAllBytesAsync(marker, oldMarker);
            var services = new ServiceCollection();
            services.AddSingleton<IRouteCatalogAuthority>(fixture.Authorities[0]);
            services.AddSingleton<IRouteCatalogStateStore>(fixture.Store);
            services.AddSingleton(Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0] }));
            await using var provider = services.BuildServiceProvider();
            var actual = fixture.Observations[0];
            if (scenario == "authentication") actual = actual with { AuthenticationProfile = "different-policy"u8.ToArray() };
            if (scenario == "topology") actual = actual with { TopologyDocuments = [.. actual.TopologyDocuments, "different-coordinates"u8.ToArray()] };
            string[] hosts = fixture.Hosts.Select(host => host.HostId).ToArray();
            if (scenario == "required-host") hosts = hosts.SkipLast(1).ToArray();
            Task Run() => DevelopmentRouteCatalogBootstrap.ActivateAsync(provider, directory, fixture.Hosts[0].HostId,
                fixture.Hosts[0].HostId, first.AttemptId, hosts, (_, _) => Task.FromResult(actual), _ => fixture.Envelope, default);
            if (scenario is "valid" or "retained-lease")
            {
                await Run();
                Directory.GetFiles(directory, "*.resume-*.json").ShouldHaveSingleItem();
                (await fixture.Authorities[0].ReadbackAsync()).Epoch.ShouldBe(1);
            }
            else
            {
                await Should.ThrowAsync<RouteCatalogUnavailableException>(Run);
                Directory.GetFiles(directory, "*.resume-*.json").ShouldBeEmpty();
            }
            (await File.ReadAllBytesAsync(marker)).ShouldBe(oldMarker);
            var state = (await fixture.Store.ReadAsync(default)).State;
            state.Committed!.RootDigest.ShouldBe(first.RootDigest);
            state.Leases.Count.ShouldBe(scenario == "retained-lease" ? 1 : 0);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
