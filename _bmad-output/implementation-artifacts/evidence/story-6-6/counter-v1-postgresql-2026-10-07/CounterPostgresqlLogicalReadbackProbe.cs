using System.Net;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Events;

/// <summary>Exercises the real bounded Counter writer with two Dapr/PostgreSQL-backed EventStore hosts.</summary>
/// <remarks>This isolated Testing topology supplies logical application evidence only.</remarks>
[Collection("Oq8Postgresql")]
public sealed class CounterPostgresqlLogicalReadbackProbe(Oq8PostgresqlFixture fixture)
{
    /// <summary>Checks immutable logical bytes, same-message duplicate recovery and host restart.</summary>
    [Fact]
    public async Task BoundedCounterWriterPreservesLogicalBytesAcrossTwoHostsAndRestart()
    {
        var identity = new AggregateIdentity("tenant-oq8", "counter", $"counter-proof-{Guid.NewGuid():N}");
        string key = $"counter-proof-{Guid.NewGuid():N}";
        fixture.HasIndependentProcesses.ShouldBeTrue();
        int before = fixture.SampleBoundaryCount;

        Oq8CommandObservation first = await fixture.SubmitAsync(0, identity.TenantId, identity.AggregateId, key, "{}");
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await fixture.WaitForSampleBoundaryCountAsync(before + 1);
        EventEnvelope original = await ReadEventAsync(0, identity);
        original.Payload.ShouldBe("{}"u8.ToArray());
        original.EventTypeName.ShouldBe(typeof(CounterIncremented).FullName);
        original.SerializationFormat.ShouldBe("json");
        original.MetadataVersion.ShouldBe(1);
        original.EventContractType.ShouldBeNull();
        original.PayloadVersion.ShouldBeNull();
        original.SequenceNumber.ShouldBe(1);
        original.ApplicationPayloadDigest.ShouldNotBeNullOrWhiteSpace();
        EventLogicalDigest.RequireMatching(original, original.SerializationFormat, original.Payload);
        original.MessageId.ShouldNotBeNullOrWhiteSpace();
        original.Identity.ShouldBe(identity);
        await RequireHeadAsync(0, identity);

        Oq8CommandObservation duplicate = await fixture.SubmitAsync(1, identity.TenantId, identity.AggregateId, key, "{}");
        RequireSameResponse(first, duplicate);
        RequireSameEvent(original, await ReadEventAsync(1, identity));
        await RequireHeadAsync(1, identity);
        fixture.SampleBoundaryCount.ShouldBe(before + 1);

        await fixture.StopEventStoreNodeAsync(0);
        (Oq8CommandObservation failover, _) = await fixture.SubmitAfterFailoverAsync(
            1, identity.TenantId, identity.AggregateId, key, "{}");
        RequireSameResponse(first, failover);
        RequireSameEvent(original, await ReadEventAsync(1, identity));
        await RequireHeadAsync(1, identity);
        fixture.SampleBoundaryCount.ShouldBe(before + 1);

        await fixture.RestartEventStoreNodeAsync(0);
        Oq8CommandObservation restarted = await fixture.SubmitAsync(0, identity.TenantId, identity.AggregateId, key, "{}");
        RequireSameResponse(first, restarted);
        RequireSameEvent(original, await ReadEventAsync(0, identity));
        await RequireHeadAsync(0, identity);
        fixture.SampleBoundaryCount.ShouldBe(before + 1);
        fixture.RecordEvidence("counter-bounded-v1-logical-readback", new
        {
            observedHosts = 2,
            hostRestarted = true,
            payloadSha256 = Convert.ToHexStringLower(SHA256.HashData(original.Payload)),
            eventMessageIdentitySha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(original.MessageId))),
            commandMessageIdentitySha256 = first.MessageIdentitySha256,
            sequence = original.SequenceNumber,
            distinctDomainExecutions = fixture.SampleBoundaryCount - before,
            persistedLogicalEventUnchanged = true,
            directDatabaseReads = 0,
        });
    }

    private Task<EventEnvelope> ReadEventAsync(int node, AggregateIdentity identity)
        => fixture.ReadCounterLogicalStateAsync<EventEnvelope>(node, identity.ActorId, identity.EventStreamKeyPrefix + "1");

    private async Task RequireHeadAsync(int node, AggregateIdentity identity)
        => (await fixture.ReadCounterLogicalStateAsync<AggregateMetadata>(node, identity.ActorId, identity.MetadataKey))
            .CurrentSequence.ShouldBe(1);

    private static void RequireSameResponse(Oq8CommandObservation expected, Oq8CommandObservation actual)
    {
        actual.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        actual.MessageIdentitySha256.ShouldNotBeNull().ShouldBe(expected.MessageIdentitySha256);
        actual.ResultSha256.ShouldNotBeNull().ShouldBe(expected.ResultSha256);
    }

    private static void RequireSameEvent(EventEnvelope expected, EventEnvelope actual)
    {
        actual.ShouldBe(expected with { Payload = actual.Payload, Extensions = actual.Extensions });
        actual.Payload.ShouldBe(expected.Payload);
        if (expected.Extensions is null)
        {
            actual.Extensions.ShouldBeNull();
        }
        else
        {
            actual.Extensions.ShouldNotBeNull().Count.ShouldBe(expected.Extensions.Count);
            foreach ((string key, string value) in expected.Extensions)
            {
                actual.Extensions[key].ShouldBe(value);
            }
        }
        EventLogicalDigest.RequireMatching(actual, actual.SerializationFormat, actual.Payload);
    }
}
