using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.DependencyInjection;

namespace FoldedSnapshotCompatibility;

/// <summary>Runs the old binary through the current packages and exercises new optional members.</summary>
public static class CurrentConsumer
{
    /// <summary>Checks package and binary compatibility for Story 6.2.</summary>
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 4)
        {
            throw new ArgumentException("Expected old assembly, old wire, old snapshot and new snapshot paths.");
        }
        Assembly oldAssembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        Type oldProbe = oldAssembly.GetType("FoldedSnapshotCompatibility.LegacyConsumer", throwOnError: true)!;
        if (oldProbe.GetMethod("AssertLegacyApis")!.Invoke(null, null) is not true)
        {
            throw new InvalidOperationException("The old positional consumer failed against current packages.");
        }

        string oldWireJson = await File.ReadAllTextAsync(args[1]).ConfigureAwait(false);
        DomainServiceWireResult oldWire = JsonSerializer.Deserialize<DomainServiceWireResult>(
            oldWireJson, JsonSerializerOptions.Web)!;
        if (oldWire.PostCommandStateJson is not null || oldWire.Events.Count != 0)
        {
            throw new InvalidOperationException("The old wire result did not default optional evidence to null.");
        }

        var command = new CommandEnvelope(
            "01HZX6FKA8B3YTX6MBQ3AYT3PT", "tenant", "domain", "aggregate",
            "Create", [], "01HZX6FKA8B3YTX6MBQ3AYT3PT", null, "user", null);
        var request = new DomainServiceRequest(command, null)
        {
            IncludePostCommandState = true,
            SnapshotCaptureProfileId = "qualified-profile",
        };
        var snapshot = new SnapshotRecord(7, JsonSerializer.Deserialize<JsonElement>("{}"),
            DateTimeOffset.UnixEpoch, "domain", "aggregate", "tenant")
        {
            SnapshotEnvelopeVersion = 1,
        };
        SnapshotRecord oldSnapshot = JsonSerializer.Deserialize<SnapshotRecord>(
            await File.ReadAllTextAsync(args[2]).ConfigureAwait(false), JsonSerializerOptions.Web)!;
        if (oldSnapshot.SnapshotEnvelopeVersion != 0
            || oldSnapshot.State is not JsonElement { ValueKind: JsonValueKind.Object })
        {
            throw new InvalidOperationException("The old snapshot did not default its envelope version to zero.");
        }
        await File.WriteAllTextAsync(args[3], JsonSerializer.Serialize(snapshot with
        {
            State = JsonSerializer.Deserialize<JsonElement>("{\"count\":7}"),
        }, JsonSerializerOptions.Web)).ConfigureAwait(false);
        var wire = new DomainServiceWireResult(false, [], null)
        {
            PostCommandStateJson = "{}",
        };
        string newWireJson = JsonSerializer.Serialize(wire, JsonSerializerOptions.Web);
        if (!request.IncludePostCommandState || snapshot.SnapshotEnvelopeVersion != 1
            || JsonSerializer.Deserialize<DomainServiceWireResult>(newWireJson,
                JsonSerializerOptions.Web)?.PostCommandStateJson != "{}")
        {
            throw new InvalidOperationException("The new optional package members did not round-trip.");
        }

        IDomainServiceInvoker oldInvoker = (IDomainServiceInvoker)Activator.CreateInstance(
            oldAssembly.GetType("FoldedSnapshotCompatibility.LegacyInvoker", throwOnError: true)!)!;
        DomainResult fallback = await oldInvoker.InvokeAsync(command, null, true,
            "qualified-profile", CancellationToken.None).ConfigureAwait(false);
        if (!fallback.IsNoOp || fallback.PostCommandStateJson is not null)
        {
            throw new InvalidOperationException("The old invoker did not use the optional default fallback.");
        }

        IAsyncDomainProcessor oldProcessor = (IAsyncDomainProcessor)Activator.CreateInstance(
            oldAssembly.GetType("FoldedSnapshotCompatibility.LegacyProcessor", throwOnError: true)!)!;
        using ServiceProvider services = new ServiceCollection()
            .AddKeyedSingleton("domain", oldProcessor)
            .BuildServiceProvider();
        DomainServiceWireResult processed = await DomainServiceRequestRouter.ProcessAsync(
            services, request, CancellationToken.None).ConfigureAwait(false);
        if (processed.Events.Count != 0 || processed.PostCommandStateJson is not null)
        {
            throw new InvalidOperationException("The old processor did not omit optional capture evidence.");
        }

        Console.WriteLine("PASS: published old package, old binary on current package, optional fallbacks, old/new wire and positional APIs");
        return 0;
    }
}
