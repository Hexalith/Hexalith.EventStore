using System.Text.Json;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

var command = new CommandEnvelope("m", "tenant", "domain", "agg", nameof(E), "{}"u8.ToArray(), "corr", null, "u", null);
var metadata = new EventMetadata("m", "agg", "aggregate", "tenant", "domain", 1, 1, DateTimeOffset.UnixEpoch, "corr", "cause", "u", "v1", nameof(E), 1, "json") { EventContractType="bogus", PayloadVersion=987 };
var envelope = new Hexalith.EventStore.Contracts.Events.EventEnvelope(metadata, "{}"u8.ToArray(), null);
var agg = new Agg();
var typed = await agg.ProcessAsync(command, new DomainServiceCurrentState(null, [envelope], 0, 1));
Console.WriteLine($"V1-with-versioned-fields: applied={State.Applied}, noOp={typed.IsNoOp}");
State.Applied=0;
var json = JsonSerializer.SerializeToElement(new[]{new{eventTypeName=nameof(E), MetadataVersion=987, SerializationFormat="protected+json",payload=new{}}});
var legacy = await agg.ProcessAsync(command,json);
Console.WriteLine($"Pascal-metadata: applied={State.Applied}, noOp={legacy.IsNoOp}");
var snapshot=JsonDocument.Parse("{\"format\":\"json\",\"Format\":\"json+pdenc-v2\",\"SnapshotTypeId\":\"counter\",\"Envelope\":\"encrypted\"}").RootElement;
var noOp=new NoOpEventPayloadProtectionService();
var protection=await noOp.TryUnprotectSnapshotAsync(new AggregateIdentity("tenant","domain","agg"),snapshot,null);
Console.WriteLine($"Conflicting-snapshot-aliases: readable={protection.IsReadable}, reason={protection.UnreadableReason}");
using var cancellation=new CancellationTokenSource();
TerminatingState.Cancellation=cancellation;
try { await new TerminatingAgg().ProcessAsync(command,new TerminatingState(),cancellation.Token); }
catch(Exception error) { Console.WriteLine($"Termination-getter-cancel-and-fail: exception={error.GetType().Name}, canceled={cancellation.IsCancellationRequested}"); }

public sealed class E : IEventPayload {}
public sealed class State { public static int Applied; public void Apply(E e){Applied++;} }
public sealed class Agg : EventStoreAggregate<State> { public static DomainResult Handle(E e, State? state)=>DomainResult.NoOp(); }
public sealed class TerminatingState : ITerminatable { public static CancellationTokenSource? Cancellation; public bool IsTerminated {get {Cancellation!.Cancel();throw new InvalidOperationException("getter failed");}} public void Apply(E e){} }
public sealed class TerminatingAgg : EventStoreAggregate<TerminatingState> { public static DomainResult Handle(E e,TerminatingState? state)=>DomainResult.NoOp(); }
