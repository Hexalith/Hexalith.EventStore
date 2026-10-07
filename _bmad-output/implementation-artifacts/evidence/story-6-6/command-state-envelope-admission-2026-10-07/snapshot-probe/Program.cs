using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using SnapshotAliasProbe;

var snapshot = new MutationState();
var processor = new ProbeProcessor();
EventEnvelope Event(long sequence, int amount, bool fail) => new(
    new EventMetadata("message", "aggregate", "mutation", "tenant", "domain", sequence, sequence,
        DateTimeOffset.UnixEpoch, "correlation", "cause", "user", "1", nameof(MutationEvent), 1, "json"),
    JsonSerializer.SerializeToUtf8Bytes(new MutationEvent(amount, fail)), null);
var state = new DomainServiceCurrentState(snapshot, [Event(6, 1, false), Event(7, 2, true)], 5, 7);
var command = new CommandEnvelope("command", "tenant", "domain", "aggregate", "probe", [], "correlation", null, "user", null);
bool threw = false;
try { await processor.ProcessAsync(command, state).ConfigureAwait(false); }
catch (Exception) { threw = true; }
Console.WriteLine(JsonSerializer.Serialize(new { threw, snapshotTotalAfterFailure = snapshot.Total, processor.Handled }));
// Zero exit confirms reproduction only. The unresolved safety requirement needs Total == 0.
return threw && snapshot.Total == 3 && !processor.Handled ? 0 : 1;
