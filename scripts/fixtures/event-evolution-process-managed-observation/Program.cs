using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Tests;

/// <summary>Exercises real managed process observations in a fresh process for each fixture case.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string scenario = args.Single();
        AssemblyLoadContext declared = scenario.StartsWith("value-equal-", StringComparison.Ordinal) ? new ValueEqualManagedLoadContext()
            : new AssemblyLoadContext("same-context-name", isCollectible: true);
        AssemblyLoadContext unlisted = scenario.StartsWith("value-equal-", StringComparison.Ordinal) ? new ValueEqualManagedLoadContext()
            : new AssemblyLoadContext("same-context-name", isCollectible: true);
        try
        {
            using ObservationInventory inventory = ObservationInventory.Create(declared,
                scenario == "value-equal-declared" ? unlisted : null);
            EventDomainRegistry registry = inventory.Registry;
            int callbacks = 0;
            var executor = new EventUpcastChainExecutor(registry,
                new Dictionary<(string Type, int Version), RegisteredEventUpcaster>(), (_, _, _, _, _, _) =>
                {
                    callbacks++;
                    if (scenario == "in-flight-unlisted")
                    {
                        _ = unlisted.LoadFromAssemblyPath(typeof(EventDomainRegistry).Assembly.Location);
                    }
                });
            using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
            sourceWriter.Write([1, 2]); sourceWriter.Complete();
            using ImmutablePayload source = sourceWriter.TakeCompletedPayload();
            var budget = new EventBufferBudget();
            string file = typeof(EventDomainRegistry).Assembly.Location;
            if (scenario == "startup-unlisted") { _ = unlisted.LoadFromAssemblyPath(file); }
            if (scenario == "value-equal-unloaded") { declared.Unload(); }
            bool expectedAdmissionRefusal = scenario is "startup-unlisted" or "value-equal-unloaded";
            var clock = Stopwatch.StartNew();
            EventEvolutionManagedLoadObserver? observer = null;
            bool refused = false;
            try
            {
                try
                {
                    observer = new EventEvolutionManagedLoadObserver(registry, inventory.Graph,
                        inventory.Graph.Select(static node => node.Identity).ToArray(), inventory.Contexts,
                        CancellationToken.None, requireAllManagedContexts: true);
                }
                catch (InvalidOperationException failure) when (expectedAdmissionRefusal
                    && failure.Message.StartsWith("CapabilityMismatch:", StringComparison.Ordinal))
                {
                    refused = true;
                }
                catch (InvalidOperationException failure)
                {
                    throw new InvalidOperationException($"Fixture assertion failed: observer admission unexpectedly refused '{scenario}'.", failure);
                }

                if (!expectedAdmissionRefusal)
                {
                    if (observer is null) { throw new InvalidOperationException("Missing admitted observer."); }
                    switch (scenario)
                    {
                        case "declared":
                            _ = declared.LoadFromAssemblyPath(file);
                            observer.ReconcileCurrentLoads(CancellationToken.None);
                            break;
                        case "value-equal-declared":
                            _ = declared.LoadFromAssemblyPath(file);
                            _ = unlisted.LoadFromAssemblyPath(file);
                            observer.ReconcileCurrentLoads(CancellationToken.None);
                            break;
                        case "late-unlisted":
                        case "value-equal-unlisted":
                            _ = unlisted.LoadFromAssemblyPath(file);
                            break;
                        case "reflection-unlisted":
                            _ = typeof(AssemblyLoadContext).GetMethod(nameof(AssemblyLoadContext.LoadFromAssemblyPath))!
                                .Invoke(unlisted, [file]);
                            break;
                        case "dynamic-unlisted":
                            using (unlisted.EnterContextualReflection())
                            {
                                _ = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("undeclared-generated"),
                                    AssemblyBuilderAccess.RunAndCollect);
                            }
                            break;
                        case "in-flight-unlisted": break;
                        default: throw new InvalidOperationException("Unknown fixture case.");
                    }
                }

                try
                {
                    using ImmutablePayload effective = await executor.UpcastAsync("evt", 1, source, budget, CancellationToken.None);
                }
                catch (InvalidOperationException failure) when (failure.Message.StartsWith("CapabilityMismatch:", StringComparison.Ordinal))
                {
                    refused = true;
                }

                bool expectedRefusal = scenario is not ("declared" or "value-equal-declared");
                int expectedCallbacks = scenario is "declared" or "value-equal-declared" or "in-flight-unlisted" ? 1 : 0;
                if (refused != expectedRefusal || callbacks != expectedCallbacks || budget.LiveBytes != 0)
                {
                    throw new InvalidOperationException(
                        $"Fixture assertion failed: refusal={refused}, callbacks={callbacks}, liveBytes={budget.LiveBytes}.");
                }

                byte[] original = new byte[2]; source.CopyTo(0, original);
                if (!original.AsSpan().SequenceEqual(new byte[] { 1, 2 }))
                {
                    throw new InvalidOperationException("Fixture assertion failed: source bytes changed.");
                }
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    scenario,
                    result = "passed",
                    refused,
                    callbacks,
                    liveBytes = budget.LiveBytes,
                    managedDeclarations = inventory.Graph.Length,
                    manifestBytes = inventory.ManifestBytes,
                    elapsedMilliseconds = clock.ElapsedMilliseconds,
                    nativeObservation = false,
                    immutableProcessImageBinding = false,
                    activationAuthority = false,
                }));
                return 0;
            }
            finally { observer?.Dispose(); }
        }
        finally
        {
            if (scenario != "value-equal-unloaded") { declared.Unload(); }
            unlisted.Unload();
        }
    }
}
