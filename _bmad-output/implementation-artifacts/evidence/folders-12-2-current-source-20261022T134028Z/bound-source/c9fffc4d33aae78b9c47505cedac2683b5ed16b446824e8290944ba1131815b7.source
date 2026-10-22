using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Proves the V5 byte bound from materially different canonical event prefixes.</summary>
public sealed class FoldedSnapshotByteIdentityTests(ITestOutputHelper output)
{
    [Fact]
    public async Task DifferentEventPrefixesConvergeToIdenticalLargeFoldedStateBytes()
    {
        var identity = new AggregateIdentity("tenant", "convergent", "aggregate");
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()),
            NullLogger<SnapshotManager>.Instance, new NoOpEventPayloadProtectionService());
        var canonicalStates = new List<byte[]>();

        foreach (int eventCount in new[] { 1, 100, 1000 })
        {
            ReplayEventEnvelope[] prefix = Enumerable.Range(1, eventCount)
                .Select(sequence => new ReplayEventEnvelope(
                    sequence, typeof(ConvergentSnapshotEvent).FullName!,
                    JsonSerializer.SerializeToUtf8Bytes(new ConvergentSnapshotEvent(
                        sequence == eventCount ? 42 : sequence), JsonSerializerOptions.Web),
                    "json", 1, Guid.NewGuid().ToString(), null, null))
                .ToArray();
            prefix.Length.ShouldBe(eventCount);
            AggregateReconstructionResult result = AggregateReplayer.Replay<ConvergentSnapshotState>(
                new AggregateReconstructionRequest(identity.TenantId, identity.Domain,
                    "convergent", identity.AggregateId, eventCount, prefix, false, null));
            result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
            using JsonDocument folded = JsonDocument.Parse(result.StateJson.ShouldNotBeNull());
            folded.RootElement.GetProperty("values").GetArrayLength().ShouldBe(5000);

            var stateManager = new InMemoryStateManager();
            await manager.CreateSnapshotAsync(identity, eventCount, folded.RootElement.Clone(), stateManager,
                JsonSerializerOptions.Web, SnapshotTestLimits.ForState(), throwOnFailure: true);
            await stateManager.SaveStateAsync();
            SnapshotRecord persisted = stateManager.CommittedState[identity.SnapshotKey]
                .ShouldBeOfType<SnapshotRecord>();
            persisted.SequenceNumber.ShouldBe(eventCount);
            persisted.SnapshotEnvelopeVersion.ShouldBe(1);
            byte[] stateBytes = JsonSerializer.SerializeToUtf8Bytes(persisted.State, JsonSerializerOptions.Web);
            byte[] envelopeBytes = JsonSerializer.SerializeToUtf8Bytes(persisted, JsonSerializerOptions.Web);
            canonicalStates.Add(stateBytes);
            envelopeBytes.Length.ShouldBeLessThanOrEqualTo(stateBytes.Length + 4096);
            output.WriteLine($"EventCount={eventCount}, FoldedStateBytes={stateBytes.Length}, SnapshotEnvelopeBytes={envelopeBytes.Length}");
        }

        canonicalStates[0].Length.ShouldBeGreaterThan(10_000);
        canonicalStates[1].ShouldBe(canonicalStates[0]);
        canonicalStates[2].ShouldBe(canonicalStates[0]);
    }

    [Fact]
    public async Task LargeIndentedStateIsNotStagedByEitherWriterPosture()
    {
        var identity = new AggregateIdentity("tenant", "convergent", "aggregate");
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()),
            NullLogger<SnapshotManager>.Instance, new NoOpEventPayloadProtectionService());
        var stateManager = new InMemoryStateManager();
        JsonElement state = JsonSerializer.SerializeToElement(
            new ConvergentSnapshotState { Values = Enumerable.Range(0, 5000).ToArray() },
            JsonSerializerOptions.Web);
        var indented = new JsonSerializerOptions(JsonSerializerOptions.Web) { WriteIndented = true };

        await manager.CreateSnapshotAsync(identity, 1, state, stateManager, indented,
            SnapshotTestLimits.ForState(), throwOnFailure: false);
        (await stateManager.ContainsStateAsync(identity.SnapshotKey)).ShouldBeFalse();

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            manager.CreateSnapshotAsync(identity, 1, state, stateManager, indented,
                SnapshotTestLimits.ForState(), throwOnFailure: true));
        failure.Message.ShouldBe("SnapshotSerializerUnsupported");
        (await stateManager.ContainsStateAsync(identity.SnapshotKey)).ShouldBeFalse();
    }
}
