using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Snapshots;
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
        if (args.Length != 7)
        {
            throw new ArgumentException("Expected old assembly, old wire/snapshot/request and new snapshot/request/wire paths.");
        }
        Assembly oldAssembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        Type oldProbe = oldAssembly.GetType("FoldedSnapshotCompatibility.LegacyConsumer", throwOnError: true)!;
        if (oldProbe.GetMethod("AssertLegacyApis")!.Invoke(null, null) is not true)
        {
            throw new InvalidOperationException("The old positional consumer failed against current packages.");
        }

        DomainServiceWireResult oldWire = await ParseBoundedWireAsync(
            await File.ReadAllBytesAsync(args[1]).ConfigureAwait(false)).ConfigureAwait(false);
        if (oldWire.PostCommandStateJson is not null || oldWire.Events.Count != 0
            || oldWire.IsRejection || oldWire.ResultPayload is not null)
        {
            throw new InvalidOperationException("The old wire result did not default optional evidence to null.");
        }
        DomainServiceRequest oldRequest = JsonSerializer.Deserialize<DomainServiceRequest>(
            await File.ReadAllTextAsync(args[3]).ConfigureAwait(false), JsonSerializerOptions.Web)!;
        if (oldRequest.IncludePostCommandState || oldRequest.SnapshotCaptureProfileId is not null
            || oldRequest.Command.CommandType != "Create")
        {
            throw new InvalidOperationException("The old request did not default optional capture fields.");
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
            || oldSnapshot.State is not JsonElement { ValueKind: JsonValueKind.Object } oldState
            || oldState.GetProperty("count").GetInt32() != 7)
        {
            throw new InvalidOperationException("The old snapshot did not default its envelope version to zero.");
        }
        await File.WriteAllTextAsync(args[4], JsonSerializer.Serialize(snapshot with
        {
            State = JsonSerializer.Deserialize<JsonElement>("{\"count\":7}"),
        }, JsonSerializerOptions.Web)).ConfigureAwait(false);
        await File.WriteAllTextAsync(args[5], JsonSerializer.Serialize(request,
            JsonSerializerOptions.Web)).ConfigureAwait(false);
        SnapshotRecord newSnapshot = JsonSerializer.Deserialize<SnapshotRecord>(
            await File.ReadAllTextAsync(args[4]).ConfigureAwait(false), JsonSerializerOptions.Web)!;
        DomainServiceRequest newRequest = JsonSerializer.Deserialize<DomainServiceRequest>(
            await File.ReadAllTextAsync(args[5]).ConfigureAwait(false), JsonSerializerOptions.Web)!;
        if (newSnapshot.SnapshotEnvelopeVersion != 1
            || newSnapshot.State is not JsonElement { ValueKind: JsonValueKind.Object } newState
            || newState.GetProperty("count").GetInt32() != 7
            || !newRequest.IncludePostCommandState
            || newRequest.SnapshotCaptureProfileId != "qualified-profile")
        {
            throw new InvalidOperationException("The new snapshot and capture request did not survive wire serialization.");
        }

        var wire = new DomainServiceWireResult(false,
            [new DomainServiceWireEvent("compat-event", "{\"count\":7}"u8.ToArray(), "json")], "new-wire")
        {
            PostCommandStateJson = "{\"count\":7}",
        };
        byte[] newWireBytes = await WriteBoundedWireAsync(wire).ConfigureAwait(false);
        await File.WriteAllBytesAsync(args[6], newWireBytes).ConfigureAwait(false);
        DomainServiceWireResult newWire = await ParseBoundedWireAsync(
            await File.ReadAllBytesAsync(args[6]).ConfigureAwait(false)).ConfigureAwait(false);
        if (newWire.PostCommandStateJson != "{\"count\":7}"
            || newWire.ResultPayload != "new-wire" || newWire.Events.Count != 1
            || newWire.Events[0].EventTypeName != "compat-event"
            || !newWire.Events[0].Payload.SequenceEqual("{\"count\":7}"u8.ToArray()))
        {
            throw new InvalidOperationException("The bounded writer/parser did not preserve new optional wire evidence.");
        }

        Type invokerType = oldAssembly.GetType("FoldedSnapshotCompatibility.LegacyInvoker", throwOnError: true)!;
        IDomainServiceInvoker oldInvoker = (IDomainServiceInvoker)Activator.CreateInstance(invokerType)!;
        object priorState = new();
        using var cancellation = new CancellationTokenSource();
        DomainResult fallback = await oldInvoker.InvokeAsync(command, priorState, true,
            "qualified-profile", cancellation.Token).ConfigureAwait(false);
        if (!ReferenceEquals(fallback, invokerType.GetProperty("Result")!.GetValue(null))
            || (int)invokerType.GetProperty("Calls")!.GetValue(null)! != 1
            || !ReferenceEquals(invokerType.GetProperty("LastCommand")!.GetValue(null), command)
            || !ReferenceEquals(invokerType.GetProperty("LastState")!.GetValue(null), priorState)
            || (CancellationToken)invokerType.GetProperty("LastCancellationToken")!.GetValue(null)! != cancellation.Token
            || fallback.PostCommandStateJson is not null)
        {
            throw new InvalidOperationException("The optional invoker overload did not delegate to the old binary method.");
        }

        await AssertLegacySnapshotManagerDelegationAsync(oldAssembly, cancellation.Token).ConfigureAwait(false);

        Type processorType = oldAssembly.GetType("FoldedSnapshotCompatibility.LegacyProcessor", throwOnError: true)!;
        IAsyncDomainProcessor oldProcessor = (IAsyncDomainProcessor)Activator.CreateInstance(processorType)!;
        var registrations = new ServiceCollection();
        registrations.AddKeyedSingleton<IAsyncDomainProcessor>("domain", oldProcessor);
        registrations.AddEventStoreBoundedV1DomainSerialization("domain", _ => { });
        registrations.AddEventStoreFoldedSnapshotQualification<object>("domain", "aggregate",
            "qualified-profile", "compat-parity", new FoldedSnapshotCaptureLimits(
                40_000, 1024, 1024, 4096, 2, 1024, 1024, 8192, 6146,
                1024, 8192, 1024, 8192, 100, 16, 2048, 40_000, 40_000));
        using ServiceProvider services = registrations.BuildServiceProvider();
        if (services.GetRequiredService<FoldedSnapshotQualificationRegistry>()
            .FindByProfile("domain", "qualified-profile") is null)
        {
            throw new InvalidOperationException("The capture qualification was not enrolled.");
        }
        DomainServiceWireResult processed = await DomainServiceRequestRouter.ProcessAsync(
            services, request, cancellation.Token).ConfigureAwait(false);
        if ((int)processorType.GetProperty("Calls")!.GetValue(null)! != 1
            || !ReferenceEquals(processorType.GetProperty("LastCommand")!.GetValue(null), command)
            || processorType.GetProperty("LastState")!.GetValue(null) is not null
            || (CancellationToken)processorType.GetProperty("LastCancellationToken")!.GetValue(null)! != cancellation.Token
            || processed.Events.Count != 0 || processed.PostCommandStateJson is not null)
        {
            throw new InvalidOperationException("The qualified router did not use the old processor without capture evidence.");
        }

        Console.WriteLine("PASS: published old package, old binary on current package, observable fallbacks, bounded old/new wire and positional APIs");
        return 0;
    }

    private static async Task AssertLegacySnapshotManagerDelegationAsync(
        Assembly oldAssembly, CancellationToken cancellationToken)
    {
        Type managerType = oldAssembly.GetType("FoldedSnapshotCompatibility.LegacySnapshotManager", throwOnError: true)!;
        ISnapshotManager manager = (ISnapshotManager)Activator.CreateInstance(managerType)!;
        var identity = new AggregateIdentity("tenant", "domain", "aggregate");
        object state = JsonSerializer.Deserialize<JsonElement>("{\"count\":7}");
        // A reference-only sentinel: the forwarding fixture never invokes actor-manager operations.
        Type actorManagerType = typeof(IActorStateManager).Assembly.GetType(
            "Dapr.Actors.Runtime.ActorStateManager", throwOnError: true)!;
        var actorManager = (IActorStateManager)RuntimeHelpers.GetUninitializedObject(actorManagerType);
        var limits = new FoldedSnapshotCaptureLimits(
            40_000, 1024, 1024, 4096, 2, 1024, 1024, 8192, 6146,
            1024, 8192, 1024, 8192, 100, 16, 2048, 40_000, 40_000);
        var providerFailure = new InvalidOperationException("legacy provider failure");
        int calls = 0;
        foreach (bool failProvider in new[] { false, true })
        {
            managerType.GetProperty("ProviderFailure")!.SetValue(null, failProvider ? providerFailure : null);
            foreach (bool useLimits in new[] { false, true })
            {
                foreach (bool throwOnFailure in new[] { false, true })
                {
                    long sequence = 7 + calls;
                    string correlationId = $"snapshot-compat-{calls}";
                    Exception? observedFailure = null;
                    try
                    {
                        if (useLimits)
                        {
                            await manager.CreateSnapshotAsync(identity, sequence, state, actorManager,
                                JsonSerializerOptions.Web, limits, correlationId, cancellationToken,
                                throwOnFailure).ConfigureAwait(false);
                        }
                        else
                        {
                            await manager.CreateSnapshotAsync(identity, sequence, state, actorManager,
                                JsonSerializerOptions.Web, correlationId, cancellationToken,
                                throwOnFailure).ConfigureAwait(false);
                        }
                    }
                    catch (Exception error) { observedFailure = error; }
                    calls++;
                    if ((int)managerType.GetProperty("Calls")!.GetValue(null)! != calls
                        || !ReferenceEquals(managerType.GetProperty("LastIdentity")!.GetValue(null), identity)
                        || (long)managerType.GetProperty("LastSequenceNumber")!.GetValue(null)! != sequence
                        || !ReferenceEquals(managerType.GetProperty("LastState")!.GetValue(null), state)
                        || !ReferenceEquals(managerType.GetProperty("LastStateManager")!.GetValue(null), actorManager)
                        || (string?)managerType.GetProperty("LastCorrelationId")!.GetValue(null) != correlationId
                        || (CancellationToken)managerType.GetProperty("LastCancellationToken")!.GetValue(null)! != cancellationToken
                        || (bool)managerType.GetProperty("LastThrowOnFailure")!.GetValue(null)! != throwOnFailure
                        || !ReferenceEquals(observedFailure, failProvider && throwOnFailure ? providerFailure : null))
                    {
                        throw new InvalidOperationException("A snapshot overload did not preserve old binary arguments or failure posture.");
                    }
                }
            }
        }
        managerType.GetProperty("ProviderFailure")!.SetValue(null, null);
    }

    private static async Task<byte[]> WriteBoundedWireAsync(DomainServiceWireResult wire)
    {
        Type writerType = typeof(DomainServiceRequestRouter).Assembly.GetType(
            "Hexalith.EventStore.DomainService.BoundedV1WireResultResponse", throwOnError: true)!;
        MethodInfo writer = writerType.GetMethod("WriteAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var output = new MemoryStream();
        await (Task)writer.Invoke(null, [output, wire, CancellationToken.None])!;
        return output.ToArray();
    }

    private static async Task<DomainServiceWireResult> ParseBoundedWireAsync(byte[] wire)
    {
        Type parserType = typeof(IDomainServiceInvoker).Assembly.GetType(
            "Hexalith.EventStore.Server.DomainServices.BoundedV1DomainResponseParser", throwOnError: true)!;
        ConstructorInfo constructor = parserType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        using var input = new MemoryStream(wire, writable: false);
        using IDisposable parser = (IDisposable)constructor.Invoke([input, CancellationToken.None, 1024 * 1024, 1000]);
        MethodInfo parse = parserType.GetMethod("ParseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return await (Task<DomainServiceWireResult>)parse.Invoke(parser, null)!;
    }
}
