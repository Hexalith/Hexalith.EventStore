using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Proves single-host durable expiry compaction against the real Redis actor state store.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class IdempotencyAdmissionExpiryLiveSidecarTests(DaprTestContainerFixture fixture)
{
    [Fact]
    public async Task ExpiryReminder_AfterApplicationAndSidecarRestart_RetainsOnlyMinimalTombstone()
    {
        string keyDigest = $"expiry-digest-{Guid.NewGuid():N}";
        string actorId = $"tenant-expiry:v1:{keyDigest}";
        string protectedIntent = $"intent-{Guid.NewGuid():N}";
        string protectedPayload = $"payload-{Guid.NewGuid():N}";
        string executionMessageId = "01J77777777777777777777777";
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var record = new IdempotencyAdmissionRecord(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            IdempotencyAdmissionState.Terminal,
            "tenant-expiry",
            "v1",
            keyDigest,
            $"tag-{Guid.NewGuid():N}",
            protectedIntent,
            IdempotencyReplayRetentionTier.Mutation,
            now.AddMinutes(-1),
            now,
            now.AddSeconds(8),
            FencingToken: 7,
            new CommandProcessingResult(true, ResultPayload: protectedPayload),
            executionMessageId,
            "trace-expiry");
        IIdempotencyAdmissionActor proxy = CreateProxy(actorId);

        await proxy.PreparePromotionAsync(
            new IdempotencyAdmissionPromotionImportRequest("expiry-proof-source", Record: record));
        await fixture.RestartHostAndSidecarAsync();

        string persistedTombstone = await WaitForTombstoneAsync(actorId);

        persistedTombstone.ShouldContain(keyDigest);
        persistedTombstone.ShouldContain(record.VerificationTag);
        persistedTombstone.ShouldNotContain(protectedIntent);
        persistedTombstone.ShouldNotContain(protectedPayload);
        persistedTombstone.ShouldNotContain(executionMessageId);
        persistedTombstone.ShouldNotContain("fencingToken", Case.Insensitive);
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => fixture.GetActorStateJsonAsync(
                IdempotencyAdmissionActor.ActorTypeName,
                actorId,
                IdempotencyAdmissionActor.StateName));

        // This is a single-host durable restart/Redis end-state proof. Multi-host closure remains
        // explicitly outside Story 4.12 and belongs to Story 4.14.
    }

    private IIdempotencyAdmissionActor CreateProxy(string actorId)
    {
        var factory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        });
        return factory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(actorId),
            IdempotencyAdmissionActor.ActorTypeName);
    }

    private async Task<string> WaitForTombstoneAsync(string actorId)
    {
        InvalidOperationException? lastFailure = null;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                return await fixture.GetActorStateJsonAsync(
                    IdempotencyAdmissionActor.ActorTypeName,
                    actorId,
                    IdempotencyAdmissionActor.TombstoneStateName).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                lastFailure = exception;
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            "The durable expiry reminder did not produce a tombstone within the bounded wait.",
            lastFailure);
    }
}
