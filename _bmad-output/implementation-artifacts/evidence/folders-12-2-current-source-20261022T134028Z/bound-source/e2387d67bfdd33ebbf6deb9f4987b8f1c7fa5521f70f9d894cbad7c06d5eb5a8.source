using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Builders;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Events;

/// <summary>Source-only retained restore, append, restart and supported signed-fence persistence evidence.</summary>
/// <remarks>Logical disposable fixture restoration does not qualify an actual published rollback package or database backup.</remarks>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class P1RRemediationPersistenceTests(DaprTestContainerFixture fixture) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The restored retained inventory survives the real writer and fresh actor processes.</summary>
    [Fact]
    public async Task RestoredFloorFiveHeadTwelveSnapshotNineAppendsThirteenAndSurvivesRestart()
    {
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        var first = new AggregateIdentity("tenant-a", "counter", $"p1r-{Guid.NewGuid():N}");
        var second = new AggregateIdentity("tenant-b", "counter", first.AggregateId);
        IAggregateActor writer = Proxy(first);
        for (int sequence = 1; sequence <= 12; sequence++)
        {
            (await writer.ProcessCommandAsync(Command(first))).Accepted.ShouldBeTrue();
        }
        IAggregateActor other = Proxy(second);
        for (int sequence = 1; sequence <= 3; sequence++)
        {
            (await other.ProcessCommandAsync(Command(second))).Accepted.ShouldBeTrue();
        }

        var prior = new Dictionary<string, string>();
        for (int sequence = 5; sequence <= 12; sequence++)
        {
            string key = first.EventStreamKeyPrefix + sequence;
            prior[key] = await ReadJsonAsync(first, key);
        }
        var isolated = new Dictionary<string, string>
        {
            [second.MetadataKey] = await ReadJsonAsync(second, second.MetadataKey),
        };
        for (int sequence = 1; sequence <= 3; sequence++)
        {
            string key = second.EventStreamKeyPrefix + sequence;
            isolated[key] = await ReadJsonAsync(second, key);
        }
        AggregateMetadata head = JsonSerializer.Deserialize<AggregateMetadata>(await ReadJsonAsync(first, first.MetadataKey), JsonOptions)!;
        var snapshot = new SnapshotRecord(9, JsonSerializer.SerializeToElement(new { Count = 9, IsTerminated = false }),
            DateTimeOffset.UnixEpoch, first.Domain, first.AggregateId, first.TenantId,
            EventStorePayloadProtectionMetadata.Unprotected());
        // No command writer is running while the invocation-owned disposable retained fixture is restored.
        var operations = new List<object>();
        operations.Add(new { operation = "upsert", request = new { key = first.MetadataKey, value = new AggregateMetadata(12, head.LastModified, head.ETag, 5) } });
        operations.Add(new { operation = "upsert", request = new { key = first.SnapshotKey, value = snapshot } });
        for (int sequence = 1; sequence < 5; sequence++)
        {
            operations.Add(new { operation = "delete", request = new { key = first.EventStreamKeyPrefix + sequence } });
        }
        using (var client = new HttpClient())
        {
            using HttpResponseMessage restored = await client.PostAsJsonAsync(StatePath(first), operations, JsonOptions,
                TestContext.Current.CancellationToken);
            restored.EnsureSuccessStatusCode();
        }
        string snapshotHash = Hash(await ReadJsonAsync(first, first.SnapshotKey));
        await fixture.RestartHostAndSidecarAsync();
        var appendProbe = new P1RCountProbeProcessor(12, append: true);
        fixture.DomainServiceInvoker.SetupAggregateHandler(first, appendProbe.ProcessAsync);
        writer = Proxy(first);
        (await writer.ProcessCommandAsync(Command(first))).Accepted.ShouldBeTrue();

        appendProbe.ObservedCount.ShouldBe(12);
        AggregateMetadata appended = JsonSerializer.Deserialize<AggregateMetadata>(await ReadJsonAsync(first, first.MetadataKey), JsonOptions)!;
        appended.CurrentSequence.ShouldBe(13);
        appended.RetainedFloor.ShouldBe(5);
        EventEnvelope latest = JsonSerializer.Deserialize<EventEnvelope>(await ReadJsonAsync(first, first.EventStreamKeyPrefix + "13"), JsonOptions)!;
        latest.SequenceNumber.ShouldBe(13);
        string latestHash = Hash(await ReadJsonAsync(first, first.EventStreamKeyPrefix + "13"));
        await fixture.RestartHostAndSidecarAsync();
        var restartProbe = new P1RCountProbeProcessor(13, append: false);
        fixture.DomainServiceInvoker.SetupAggregateHandler(first, restartProbe.ProcessAsync);
        writer = Proxy(first);
        (await writer.ProcessCommandAsync(Command(first))).Accepted.ShouldBeTrue();
        restartProbe.ObservedCount.ShouldBe(13);
        (await writer.GetCurrentSequenceAsync()).ShouldBe(13);
        (await writer.GetRetainedFloorAsync()).ShouldBe(5);
        Hash(await ReadJsonAsync(first, first.SnapshotKey)).ShouldBe(snapshotHash);
        Hash(await ReadJsonAsync(first, first.EventStreamKeyPrefix + "13")).ShouldBe(latestHash);
        foreach ((string key, string value) in prior)
        {
            Hash(await ReadJsonAsync(first, key)).ShouldBe(Hash(value));
        }
        foreach ((string key, string value) in isolated)
        {
            Hash(await ReadJsonAsync(second, key)).ShouldBe(Hash(value));
        }
        foreach (System.Reflection.Assembly assembly in new[] { typeof(AggregateActor).Assembly,
            typeof(CommandEnvelope).Assembly, typeof(P1RCountProbeProcessor).Assembly,
            typeof(Hexalith.EventStore.Client.Handlers.DomainProcessorBase<>).Assembly })
        {
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"P1R loaded assembly={assembly.FullName}; path={assembly.Location}; sha256={Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location)))}");
        }
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(new
        {
            fixture.AppId, fixture.AggregateActorTypeName, fixtureId = first.AggregateId,
            head = 13, floor = 5, snapshot = 9, snapshotHash, latestHash,
            reconstructedBeforeAppend = appendProbe.ObservedCount, reconstructedAfterRestart = restartProbe.ObservedCount,
            priorHashes = prior.ToDictionary(pair => pair.Key, pair => Hash(pair.Value)),
            secondTenantHashes = isolated.ToDictionary(pair => pair.Key, pair => Hash(pair.Value)),
            restoreKind = "logical-disposable-actor-inventory", packageQualification = "unverified",
        }));
    }

    /// <summary>Recoverable and terminal authority refuses a previously valid signed fence and forged proof before persisted effects.</summary>
    [Fact]
    public async Task CurrentFencePersistsEventWhileStaleAndForgedCapabilitiesLeaveInventoryUnchanged()
    {
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        var identity = new AggregateIdentity("tenant-a", "counter", $"p1r-fence-{Guid.NewGuid():N}");
        var request = new SubmitCommand("p1r-message", identity.TenantId, identity.Domain, identity.AggregateId,
            "IncrementCounter", "{}"u8.ToArray(), "p1r-correlation", "user", IdempotencyKey: $"p1r-key-{Guid.NewGuid():N}");
        IIdempotencyAdmissionCoordinator admission = fixture.Services.GetRequiredService<IIdempotencyAdmissionCoordinator>();
        IdempotencyAdmissionSession original = (await admission.AdmitAsync(request))!;
        original.Decision.ShouldBe(IdempotencyAdmissionDecision.Execute);
        await admission.BeginAsync(original);
        await admission.MarkRecoveryAsync(original, IdempotencyAdmissionState.Recoverable);
        IdempotencyAdmissionSession current = (await admission.AdmitAsync(request))!;
        current.Decision.ShouldBe(IdempotencyAdmissionDecision.Recoverable);
        current.FencingToken.ShouldBe(original.FencingToken);
        var execution = request with { MessageId = current.ExecutionMessageId!, CorrelationId = current.ExecutionCorrelationId!, IdempotencyKey = null };
        ICommandRouter router = fixture.Services.GetRequiredService<ICommandRouter>();
        int before = fixture.DomainServiceInvoker.Invocations.Count;
        _ = await Should.ThrowAsync<Exception>(() => router.RouteFencedCommandAsync(execution, original.ExecutionContext!));
        _ = await Should.ThrowAsync<Exception>(() => router.RouteFencedCommandAsync(execution, current.ExecutionContext! with { Proof = "forged-proof" }));
        fixture.DomainServiceInvoker.Invocations.Count.ShouldBe(before);
        (await Proxy(identity).GetCurrentSequenceAsync()).ShouldBe(0);

        await admission.BeginAsync(current);
        CommandProcessingResult result = await router.RouteFencedCommandAsync(execution, current.ExecutionContext!);
        result.Accepted.ShouldBeTrue();
        await admission.CompleteAsync(current, result);
        string metadata = await ReadJsonAsync(identity, identity.MetadataKey);
        string committed = await ReadJsonAsync(identity, identity.EventStreamKeyPrefix + "1");
        JsonSerializer.Deserialize<AggregateMetadata>(metadata, JsonOptions)!.CurrentSequence.ShouldBe(1);
        JsonSerializer.Deserialize<EventEnvelope>(committed, JsonOptions)!.MessageId.ShouldNotBeNullOrWhiteSpace();
        string authority = await fixture.GetActorStateJsonAsync(IdempotencyAdmissionActor.ActorTypeName,
            current.ActorId, IdempotencyAdmissionActor.StateName);
        authority.ShouldContain("replayResult");
        _ = await Should.ThrowAsync<Exception>(() => router.RouteFencedCommandAsync(execution, original.ExecutionContext!));
        Hash(await ReadJsonAsync(identity, identity.MetadataKey)).ShouldBe(Hash(metadata));
        Hash(await ReadJsonAsync(identity, identity.EventStreamKeyPrefix + "1")).ShouldBe(Hash(committed));
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"P1R fenced fixture={identity.AggregateId}; old_fence={original.FencingToken}; current_fence={current.FencingToken}; event_sha256={Hash(committed)}; metadata_sha256={Hash(metadata)}; authority_sha256={Hash(authority)}");
    }

    /// <inheritdoc/>
    public void Dispose() => fixture.ResetTestState();

    private IAggregateActor Proxy(AggregateIdentity identity)
        => new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = fixture.DaprHttpEndpoint })
            .CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), fixture.AggregateActorTypeName);

    private static CommandEnvelope Command(AggregateIdentity identity)
        => new CommandEnvelopeBuilder().WithTenantId(identity.TenantId).WithDomain(identity.Domain)
            .WithAggregateId(identity.AggregateId).WithCommandType("IncrementCounter").WithPayload("{}"u8.ToArray()).Build();

    private string StatePath(AggregateIdentity identity)
        => $"{fixture.DaprHttpEndpoint}/v1.0/actors/{Uri.EscapeDataString(fixture.AggregateActorTypeName)}/{Uri.EscapeDataString(identity.ActorId)}/state";

    private async Task<string> ReadJsonAsync(AggregateIdentity identity, string key)
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(StatePath(identity) + "/" + Uri.EscapeDataString(key), TestContext.Current.CancellationToken);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
