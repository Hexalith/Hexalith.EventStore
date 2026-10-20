using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

using Dapr;
using Dapr.Client;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>Local runtime qualification of the per-sidecar Dapr Redis Streams fan-out candidate.</summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("SignalRDaprDistributionProofTests")]
public sealed class SignalRDaprDistributionRuntimeProofTests
{
    private static readonly TimeSpan s_positiveWait = TimeSpan.FromSeconds(15);

    /// <summary>Checks two real hosts, independent sidecars, scoped clients, and logical Dapr readback.</summary>
    [Fact]
    public async Task DaprRedisStreams_TwoHosts_DeliverScopedSignalsAndPersistedReadback()
    {
        DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        string suffix = Guid.NewGuid().ToString("N")[..12];
        string tenantA = $"tenant-a-{suffix}";
        string tenantB = $"tenant-b-{suffix}";
        string projection = $"counter-{suffix}";
        string scope = $"scope-{suffix}";
        string topic = NamingConventionEngine.GetProjectionChangedTopic(projection, tenantA);
        string tenantBTopic = NamingConventionEngine.GetProjectionChangedTopic(projection, tenantB);
        string deniedProjection = $"denied-{suffix}";
        string deniedTopic = NamingConventionEngine.GetProjectionChangedTopic(deniedProjection, tenantA);
        string recoveredProjection = $"recovered-{suffix}";
        string recoveredTopic = NamingConventionEngine.GetProjectionChangedTopic(recoveredProjection, tenantA);
        string unsubscribedProjection = $"dynamic-{suffix}";
        string unsubscribedTopic = NamingConventionEngine.GetProjectionChangedTopic(unsubscribedProjection, tenantA);
        await using SignalRDaprDistributionFixture fixture = await SignalRDaprDistributionFixture
            .StartAsync([topic, deniedTopic, recoveredTopic, tenantBTopic], cancellation.Token).ConfigureAwait(true);
        string readModelKey = $"story-2-13:{tenantA}:{projection}";
        int originalSecondSidecarProcessId = fixture.Second.SidecarProcessId;

        using DaprClient publisher = new DaprClientBuilder().UseGrpcEndpoint(fixture.First.DaprGrpcEndpoint).Build();
        using DaprClient reader = new DaprClientBuilder().UseGrpcEndpoint(fixture.Second.DaprGrpcEndpoint).Build();
        var writerStore = new DaprReadModelStore(publisher);
        var readerStore = new DaprReadModelStore(reader);
        await writerStore.SaveAsync("statestore", readModelKey, new Dictionary<string, int> { ["version"] = 1 }, cancellation.Token).ConfigureAwait(true);
        ReadModelEntry<Dictionary<string, int>> before = await readerStore
            .GetAsync<Dictionary<string, int>>("statestore", readModelKey, cancellation.Token).ConfigureAwait(true);
        before.Value.ShouldNotBeNull();
        before.Value["version"].ShouldBe(1);
        before.ETag.ShouldNotBeNullOrWhiteSpace();

        var firstSignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var secondSignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var firstDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        var secondDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        var tenantBFirstSignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var tenantBSecondSignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var tenantBFirstDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        var tenantBSecondDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        await using HubConnection first = CreateConnection(fixture.First.AppEndpoint, tenantA, firstSignals.Writer, firstDetails.Writer);
        await using HubConnection second = CreateConnection(fixture.Second.AppEndpoint, tenantA, secondSignals.Writer, secondDetails.Writer);
        await using HubConnection otherTenantFirst = CreateConnection(
            fixture.First.AppEndpoint, tenantB, tenantBFirstSignals.Writer, tenantBFirstDetails.Writer);
        await using HubConnection otherTenantSecond = CreateConnection(
            fixture.Second.AppEndpoint, tenantB, tenantBSecondSignals.Writer, tenantBSecondDetails.Writer);
        int authorizedTenantBReplayHints = 0;

        async Task AssertNoTenantBDeliveryAsync(string scenario)
        {
            (bool ForeignTenant, int AuthorizedHints)[] observations = await Task.WhenAll(
                ObserveTenantBoundaryAsync(tenantBFirstSignals.Reader, static notice => notice.Tenant, tenantB, cancellation.Token),
                ObserveTenantBoundaryAsync(tenantBSecondSignals.Reader, static notice => notice.Tenant, tenantB, cancellation.Token),
                ObserveTenantBoundaryAsync(tenantBFirstDetails.Reader, static notice => notice.Tenant, tenantB, cancellation.Token),
                ObserveTenantBoundaryAsync(tenantBSecondDetails.Reader, static notice => notice.Tenant, tenantB, cancellation.Token)).ConfigureAwait(true);
            authorizedTenantBReplayHints += observations.Sum(static observation => observation.AuthorizedHints);
            observations.Any(static observation => observation.ForeignTenant).ShouldBeFalse(scenario);
        }
        foreach (Uri endpoint in new[] { fixture.First.AppEndpoint, fixture.Second.AppEndpoint })
        {
            await AssertConnectionDenialsAsync(endpoint, tenantA, tenantB, projection, cancellation.Token).ConfigureAwait(true);
        }
        await first.StartAsync(cancellation.Token).ConfigureAwait(true);
        await second.StartAsync(cancellation.Token).ConfigureAwait(true);
        await otherTenantFirst.StartAsync(cancellation.Token).ConfigureAwait(true);
        await otherTenantSecond.StartAsync(cancellation.Token).ConfigureAwait(true);
        string? firstConnectionId = first.ConnectionId;
        string? secondConnectionId = second.ConnectionId;
        firstConnectionId.ShouldNotBeNullOrWhiteSpace();
        secondConnectionId.ShouldNotBeNullOrWhiteSpace();
        await first.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token).ConfigureAwait(true);
        await first.InvokeAsync("JoinGroup", deniedProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroup", deniedProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        foreach (HubConnection otherTenant in new[] { otherTenantFirst, otherTenantSecond })
        {
            foreach (string subscribedProjection in new[] { projection, deniedProjection, recoveredProjection })
            {
                await otherTenant.InvokeAsync("JoinGroup", subscribedProjection, tenantB, cancellation.Token).ConfigureAwait(true);
                await otherTenant.InvokeAsync("JoinGroupScoped", subscribedProjection, tenantB, scope, cancellation.Token).ConfigureAwait(true);
            }

            await Should.ThrowAsync<HubException>(() => otherTenant.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token)).ConfigureAwait(true);
            await Should.ThrowAsync<HubException>(() => otherTenant.InvokeAsync(
                "JoinGroupScoped", projection, tenantA, scope, cancellation.Token)).ConfigureAwait(true);
        }
        await Should.ThrowAsync<HubException>(() => first.InvokeAsync(
            "JoinGroup", projection, "malformed:tenant", cancellation.Token)).ConfigureAwait(true);

        // Both tenant B clients are live recipients of their own same-named projection and scope.
        await publisher.PublishEventAsync("pubsub", tenantBTopic,
            new ProjectionChangedNotification(projection, tenantB)
            {
                Provenance = CreateProvenance(projection, tenantB, tenantBTopic),
            }, cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(tenantBFirstSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantB));
        (await ReadAsync(tenantBSecondSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantB));
        await publisher.PublishEventAsync("pubsub", tenantBTopic,
            new ProjectionChangedNotification(projection, tenantB, GroupScope: scope,
                Metadata: new Dictionary<string, string> { ["opaque"] = "control" })
            {
                Provenance = CreateProvenance(projection, tenantB, tenantBTopic),
            }, cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(tenantBFirstDetails.Reader, cancellation.Token).ConfigureAwait(true)).Tenant.ShouldBe(tenantB);
        (await ReadAsync(tenantBSecondDetails.Reader, cancellation.Token).ConfigureAwait(true)).Tenant.ShouldBe(tenantB);

        // Reproduce the shipped wildcard gap beside a live exact-topic positive control.
        foreach (SignalRDaprDistributionNode node in new[] { fixture.First, fixture.Second })
        {
            JsonElement metadataSnapshot = await node.GetRuntimeMetadataAsync(cancellation.Token).ConfigureAwait(true);
            metadataSnapshot.GetProperty("subscriptions").EnumerateArray()
                .Any(subscription => subscription.GetProperty("topic").GetString() == "*.*.projection-changed")
                .ShouldBeTrue("the shipped programmatic wildcard must actually be registered for the gap observation");
        }

        await first.InvokeAsync("JoinGroup", unsubscribedProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroup", unsubscribedProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        await publisher.PublishEventAsync(
            "pubsub", unsubscribedTopic,
            new ProjectionChangedNotification(unsubscribedProjection, tenantA)
            {
                Provenance = CreateProvenance(unsubscribedProjection, tenantA, unsubscribedTopic),
            }, cancellation.Token).ConfigureAwait(true);
        (await ReceivesWithinAsync(firstSignals.Reader, TimeSpan.FromSeconds(1), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("this profile's wildcard remains unqualified for concrete dynamic topics");
        (await ReceivesWithinAsync(secondSignals.Reader, TimeSpan.FromSeconds(1), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("the same gap must be observed on host B beside the exact-topic positive control");

        await publisher.PublishEventAsync(
            "pubsub", topic,
            new ProjectionChangedNotification(projection, tenantA) { Provenance = CreateProvenance(projection, tenantA, topic) },
            cancellation.Token).ConfigureAwait(true);
        try
        {
            (await ReadAsync(firstSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantA));
            (await ReadAsync(secondSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantA));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Dapr fan-out failed. First: {fixture.First.SnapshotDiagnostics()} Second: {fixture.Second.SnapshotDiagnostics()}",
                exception);
        }
        await AssertNoTenantBDeliveryAsync("both tenant B clients must receive no tenant A signal or detail").ConfigureAwait(true);

        await writerStore.SaveAsync("statestore", readModelKey, new Dictionary<string, int> { ["version"] = 2 }, cancellation.Token).ConfigureAwait(true);
        await first.InvokeAsync("JoinGroupScoped", projection, tenantA, scope, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroupScoped", projection, tenantA, scope, cancellation.Token).ConfigureAwait(true);
        var metadata = Enumerable.Range(0, 16).ToDictionary(i => $"k{i:D2}", _ => new string('x', 116), StringComparer.Ordinal);
        await publisher.PublishEventAsync(
            "pubsub", topic,
            new ProjectionChangedNotification(projection, tenantA, GroupScope: scope, Metadata: metadata)
            {
                Provenance = CreateProvenance(projection, tenantA, topic),
            }, cancellation.Token).ConfigureAwait(true);
        (string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata) firstDetail;
        (string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata) secondDetail;
        try
        {
            firstDetail = await ReadAsync(firstDetails.Reader, cancellation.Token).ConfigureAwait(true);
            secondDetail = await ReadAsync(secondDetails.Reader, cancellation.Token).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Dapr detail delivery failed. First: {fixture.First.SnapshotDiagnostics()} Second: {fixture.Second.SnapshotDiagnostics()}",
                exception);
        }
        firstDetail.Scope.ShouldBe(scope);
        secondDetail.Scope.ShouldBe(scope);
        firstDetail.Projection.ShouldBe(projection);
        secondDetail.Projection.ShouldBe(projection);
        firstDetail.Tenant.ShouldBe(tenantA);
        secondDetail.Tenant.ShouldBe(tenantA);
        firstDetail.Metadata.Count.ShouldBe(16);
        secondDetail.Metadata.Count.ShouldBe(16);
        Encoding.UTF8.GetByteCount(string.Concat(firstDetail.Metadata.Select(kv => kv.Key + kv.Value))).ShouldBeLessThanOrEqualTo(2048);
        int serializedMetadataBytes = JsonSerializer.SerializeToUtf8Bytes(firstDetail.Metadata).Length;
        serializedMetadataBytes.ShouldBeLessThanOrEqualTo(2048);
        firstDetail.Metadata.ShouldBe(metadata);
        secondDetail.Metadata.ShouldBe(metadata);
        await AssertNoTenantBDeliveryAsync("neither tenant B scoped group may receive tenant A detail").ConfigureAwait(true);
        ReadModelEntry<Dictionary<string, int>> after = await readerStore
            .GetAsync<Dictionary<string, int>>("statestore", readModelKey, cancellation.Token).ConfigureAwait(true);
        after.Value.ShouldNotBeNull();
        after.Value["version"].ShouldBe(2);
        after.ETag.ShouldNotBe(before.ETag);

        // An unbound or missing credential is sent through Dapr, then denied by the real callback.
        await publisher.PublishEventAsync(
            "pubsub", deniedTopic,
            new ProjectionChangedNotification(deniedProjection, tenantA), cancellation.Token).ConfigureAwait(true);
        (await ReceivesWithinAsync(firstSignals.Reader, TimeSpan.FromSeconds(2), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("the unbound callback must not reach an authorized client");
        (await ReceivesWithinAsync(secondSignals.Reader, TimeSpan.FromSeconds(2), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("the unbound callback must not reach the other replica");

        // Valid signatures do not authorize a different tenant, projection, or topic binding.
        string[] invalidProvenance = [
            CreateProvenance(deniedProjection, tenantB, deniedTopic),
            CreateProvenance(projection, tenantA, deniedTopic),
            CreateProvenance(deniedProjection, tenantA, topic),
            CreateProvenance(deniedProjection, tenantA, deniedTopic, DateTime.UtcNow.AddMinutes(-10)),
        ];
        foreach (string provenance in invalidProvenance)
        {
            await publisher.PublishEventAsync(
                "pubsub", deniedTopic,
                new ProjectionChangedNotification(deniedProjection, tenantA) { Provenance = provenance },
                cancellation.Token).ConfigureAwait(true);
        }

        (await ReceivesWithinAsync(firstSignals.Reader, TimeSpan.FromSeconds(2), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("a signed but mismatched or expired callback must not reach host A");
        (await ReceivesWithinAsync(secondSignals.Reader, TimeSpan.FromSeconds(2), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("a signed but mismatched or expired callback must not reach host B");

        // The same subscriptions must deliver a bound positive control, excluding an inactive subscriber.
        await publisher.PublishEventAsync(
            "pubsub", deniedTopic,
            new ProjectionChangedNotification(deniedProjection, tenantA)
            {
                Provenance = CreateProvenance(deniedProjection, tenantA, deniedTopic),
            }, cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((deniedProjection, tenantA));
        (await ReadAsync(secondSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((deniedProjection, tenantA));

        await first.InvokeAsync("JoinGroupScoped", deniedProjection, tenantA, scope, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroupScoped", deniedProjection, tenantA, scope, cancellation.Token).ConfigureAwait(true);
        IReadOnlyDictionary<string, string>[] oversizedMetadata = [
            Enumerable.Range(0, 17).ToDictionary(i => $"k{i:D2}", _ => "opaque", StringComparer.Ordinal),
            new Dictionary<string, string> { ["opaque"] = new string('é', 1_025) },
        ];
        foreach (IReadOnlyDictionary<string, string> oversized in oversizedMetadata)
        {
            await publisher.PublishEventAsync(
                "pubsub", deniedTopic,
                new ProjectionChangedNotification(deniedProjection, tenantA, GroupScope: scope, Metadata: oversized)
                {
                    Provenance = CreateProvenance(deniedProjection, tenantA, deniedTopic),
                }, cancellation.Token).ConfigureAwait(true);
        }

        (await ReceivesWithinAsync(firstDetails.Reader, TimeSpan.FromSeconds(2), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("host A must reject the 17-entry and oversized multibyte details");
        (await ReceivesWithinAsync(secondDetails.Reader, TimeSpan.FromSeconds(2), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("host B must reject the 17-entry and oversized multibyte details");
        await publisher.PublishEventAsync(
            "pubsub", deniedTopic,
            new ProjectionChangedNotification(deniedProjection, tenantA, GroupScope: scope, Metadata: metadata)
            {
                Provenance = CreateProvenance(deniedProjection, tenantA, deniedTopic),
            }, cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstDetails.Reader, cancellation.Token).ConfigureAwait(true)).Projection.ShouldBe(deniedProjection);
        (await ReadAsync(secondDetails.Reader, cancellation.Token).ConfigureAwait(true)).Projection.ShouldBe(deniedProjection);

        // Repeated hints do not advance a persisted read model or establish a second completion.
        await publisher.PublishEventAsync(
            "pubsub", topic,
            new ProjectionChangedNotification(projection, tenantA) { Provenance = CreateProvenance(projection, tenantA, topic) },
            cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantA));
        (await ReadAsync(secondSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantA));
        ReadModelEntry<Dictionary<string, int>> repeated = await readerStore
            .GetAsync<Dictionary<string, int>>("statestore", readModelKey, cancellation.Token).ConfigureAwait(true);
        repeated.Value!["version"].ShouldBe(2);
        repeated.ETag.ShouldBe(after.ETag);

        // Producer hint order and repeats never order or advance authoritative projection state.
        foreach (string hint in new[] { "newer", "older", "older" })
        {
            await publisher.PublishEventAsync(
                "pubsub", topic,
                new ProjectionChangedNotification(projection, tenantA, GroupScope: scope,
                    Metadata: new Dictionary<string, string> { ["opaque"] = hint })
                {
                    Provenance = CreateProvenance(projection, tenantA, topic),
                }, cancellation.Token).ConfigureAwait(true);
            (await ReadAsync(firstDetails.Reader, cancellation.Token).ConfigureAwait(true)).Metadata["opaque"].ShouldBe(hint);
            (await ReadAsync(secondDetails.Reader, cancellation.Token).ConfigureAwait(true)).Metadata["opaque"].ShouldBe(hint);
            ReadModelEntry<Dictionary<string, int>> reconciled = await readerStore
                .GetAsync<Dictionary<string, int>>("statestore", readModelKey, cancellation.Token).ConfigureAwait(true);
            reconciled.Value!["version"].ShouldBe(2);
            reconciled.ETag.ShouldBe(after.ETag);
        }

        // A reconnect obtains fresh group membership. A replacement identity without the tenant grant cannot rejoin.
        await first.StopAsync(cancellation.Token).ConfigureAwait(true);
        await AssertNoTenantBDeliveryAsync("repeated or reordered tenant A hints must remain isolated on both hosts").ConfigureAwait(true);
        await using HubConnection revoked = CreateConnection(fixture.First.AppEndpoint, tenantB, tenantBFirstSignals.Writer, tenantBFirstDetails.Writer);
        await revoked.StartAsync(cancellation.Token).ConfigureAwait(true);
        await Should.ThrowAsync<HubException>(() => revoked.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token)).ConfigureAwait(true);
        await Should.ThrowAsync<HubException>(() => revoked.InvokeAsync("JoinGroupScoped", projection, tenantA, scope, cancellation.Token)).ConfigureAwait(true);
        await using HubConnection reconnected = CreateConnection(fixture.First.AppEndpoint, tenantA, firstSignals.Writer, firstDetails.Writer);
        await reconnected.StartAsync(cancellation.Token).ConfigureAwait(true);
        await reconnected.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token).ConfigureAwait(true);
        await reconnected.InvokeAsync("JoinGroupScoped", projection, tenantA, scope, cancellation.Token).ConfigureAwait(true);
        ReadModelEntry<Dictionary<string, int>> rejoined = await readerStore
            .GetAsync<Dictionary<string, int>>("statestore", readModelKey, cancellation.Token).ConfigureAwait(true);
        rejoined.Value!["version"].ShouldBe(2);
        rejoined.ETag.ShouldBe(after.ETag);
        await publisher.PublishEventAsync(
            "pubsub", topic,
            new ProjectionChangedNotification(projection, tenantA) { Provenance = CreateProvenance(projection, tenantA, topic) },
            cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantA));
        (await ReadAsync(secondSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantA));
        await publisher.PublishEventAsync("pubsub", topic,
            new ProjectionChangedNotification(projection, tenantA, GroupScope: scope,
                Metadata: new Dictionary<string, string> { ["opaque"] = "rejoin" })
            {
                Provenance = CreateProvenance(projection, tenantA, topic),
            }, cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstDetails.Reader, cancellation.Token).ConfigureAwait(true)).Metadata["opaque"].ShouldBe("rejoin");
        (await ReadAsync(secondDetails.Reader, cancellation.Token).ConfigureAwait(true)).Metadata["opaque"].ShouldBe("rejoin");
        await AssertNoTenantBDeliveryAsync("revoked and persistent tenant B clients must receive no tenant A signal/detail after rejoin").ConfigureAwait(true);

        // During a sidecar outage the attached B client cannot receive a fresh local callback.
        await fixture.Second.StopSidecarAsync().ConfigureAwait(true);
        await publisher.PublishEventAsync(
            "pubsub", topic,
            new ProjectionChangedNotification(projection, tenantA) { Provenance = CreateProvenance(projection, tenantA, topic) },
            cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantA));
        (await ReceivesWithinAsync(secondSignals.Reader, TimeSpan.FromSeconds(1), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("the B sidecar is unavailable");
        using (var unavailable = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
        {
            unavailable.CancelAfter(TimeSpan.FromSeconds(3));
            bool queryFailed = false;
            try
            {
                _ = await readerStore.GetAsync<Dictionary<string, int>>("statestore", readModelKey, unavailable.Token).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is DaprException or OperationCanceledException or Grpc.Core.RpcException)
            {
                queryFailed = true;
            }

            queryFailed.ShouldBeTrue("Dapr query cannot claim a fresh read while its sidecar is unavailable");
        }

        await fixture.RestartSecondSidecarAsync(cancellation.Token).ConfigureAwait(true);
        ReadModelEntry<Dictionary<string, int>> recovered = await WaitForReadbackAsync(
            readerStore, readModelKey, cancellation.Token).ConfigureAwait(true);
        recovered.Value!["version"].ShouldBe(2);
        recovered.ETag.ShouldBe(after.ETag);
        var firstRecoverySignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var secondRecoverySignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var firstRecoveryDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        var secondRecoveryDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        await using HubConnection recoveryFirst = CreateConnection(fixture.First.AppEndpoint, tenantA, firstRecoverySignals.Writer, firstRecoveryDetails.Writer);
        await using HubConnection recoverySecond = CreateConnection(fixture.Second.AppEndpoint, tenantA, secondRecoverySignals.Writer, secondRecoveryDetails.Writer);
        await recoveryFirst.StartAsync(cancellation.Token).ConfigureAwait(true);
        await recoverySecond.StartAsync(cancellation.Token).ConfigureAwait(true);
        await recoveryFirst.InvokeAsync("JoinGroup", recoveredProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        await recoverySecond.InvokeAsync("JoinGroup", recoveredProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        await recoveryFirst.InvokeAsync("JoinGroupScoped", recoveredProjection, tenantA, scope, cancellation.Token).ConfigureAwait(true);
        await recoverySecond.InvokeAsync("JoinGroupScoped", recoveredProjection, tenantA, scope, cancellation.Token).ConfigureAwait(true);
        await publisher.PublishEventAsync(
            "pubsub", recoveredTopic,
            new ProjectionChangedNotification(recoveredProjection, tenantA)
            {
                Provenance = CreateProvenance(recoveredProjection, tenantA, recoveredTopic),
            },
            cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstRecoverySignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((recoveredProjection, tenantA));
        (await ReadAsync(secondRecoverySignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((recoveredProjection, tenantA));
        await publisher.PublishEventAsync("pubsub", recoveredTopic,
            new ProjectionChangedNotification(recoveredProjection, tenantA, GroupScope: scope,
                Metadata: new Dictionary<string, string> { ["opaque"] = "recovery" })
            {
                Provenance = CreateProvenance(recoveredProjection, tenantA, recoveredTopic),
            }, cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstRecoveryDetails.Reader, cancellation.Token).ConfigureAwait(true)).Metadata["opaque"].ShouldBe("recovery");
        (await ReadAsync(secondRecoveryDetails.Reader, cancellation.Token).ConfigureAwait(true)).Metadata["opaque"].ShouldBe("recovery");
        await AssertNoTenantBDeliveryAsync("neither tenant B client may receive tenant A recovery signal/detail").ConfigureAwait(true);
        ReadModelEntry<Dictionary<string, int>> final = await readerStore
            .GetAsync<Dictionary<string, int>>("statestore", readModelKey, cancellation.Token).ConfigureAwait(true);
        final.ETag.ShouldBe(after.ETag);
        foreach (SignalRDaprDistributionNode node in new[] { fixture.First, fixture.Second })
        {
            string logs = node.SnapshotRedactedLogs();
            logs.Contains(new string('x', 116), StringComparison.Ordinal)
                .ShouldBeFalse("metadata values must not appear in Information-level logs");
            logs.ShouldContain(WorkloadAuthenticationReasons.BindingMismatch);
            logs.ShouldContain(WorkloadAuthenticationReasons.AssertionExpired);
            logs.ShouldContain("Metadata cannot exceed 16 entries");
            logs.ShouldContain("Metadata cannot exceed 2048 UTF-8 bytes");
        }

        string? evidencePath = Environment.GetEnvironmentVariable("HEXALITH_STORY_2_13_EVIDENCE_PATH");
        if (!string.IsNullOrWhiteSpace(evidencePath))
        {
            string evidence = JsonSerializer.Serialize(new
            {
                StartedUtc = startedUtc,
                CompletedUtc = DateTimeOffset.UtcNow,
                Result = "passed-local-exact-topic-candidate",
                AppId = "eventstore",
                Topic = topic,
                DeniedTopic = deniedTopic,
                RecoveredTopic = recoveredTopic,
                ReadModelKey = readModelKey,
                FirstAppProcessId = fixture.First.ApplicationProcessId,
                SecondAppProcessId = fixture.Second.ApplicationProcessId,
                FirstSidecarProcessId = fixture.First.SidecarProcessId,
                SecondSidecarProcessIdAfterRestart = fixture.Second.SidecarProcessId,
                FirstAppPort = fixture.First.AppPort,
                SecondAppPort = fixture.Second.AppPort,
                PubSubComponentSha256 = fixture.PubSubComponentSha256,
                StateComponentSha256 = fixture.StateComponentSha256,
                ExactSubscriptionSha256 = fixture.SubscriptionSha256(0),
                DenialSubscriptionSha256 = fixture.SubscriptionSha256(1),
                RecoverySubscriptionSha256 = fixture.SubscriptionSha256(2),
                InitialVersion = before.Value["version"],
                FinalVersion = final.Value!["version"],
                InitialETag = before.ETag,
                FinalETag = final.ETag,
                DetailMetadataEntries = firstDetail.Metadata.Count,
                DetailMetadataUtf8Bytes = Encoding.UTF8.GetByteCount(string.Concat(firstDetail.Metadata.Select(kv => kv.Key + kv.Value))),
                DetailMetadataSerializedUtf8Bytes = serializedMetadataBytes,
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(evidencePath, evidence + "\n", cancellation.Token).ConfigureAwait(true);
        }

        await fixture.CaptureEvidenceAsync("synthetic-distribution", startedUtc, new
        {
            Topic = topic,
            DeniedTopic = deniedTopic,
            RecoveredTopic = recoveredTopic,
            UnsubscribedTopic = unsubscribedTopic,
            TenantA = tenantA,
            TenantB = tenantB,
            TenantBTopic = tenantBTopic,
            Projection = projection,
            Scope = scope,
            FirstConnectionId = firstConnectionId,
            SecondConnectionId = secondConnectionId,
            RejoinedConnectionId = reconnected.ConnectionId,
            TenantBFirstConnectionId = otherTenantFirst.ConnectionId,
            TenantBSecondConnectionId = otherTenantSecond.ConnectionId,
            TenantBSignalAndDetailPositiveControlsOnBothHosts = true,
            TenantBReceivedTenantASignalOrDetail = false,
            AuthorizedTenantBReplayHintsObserved = authorizedTenantBReplayHints,
            OriginalSecondSidecarProcessId = originalSecondSidecarProcessId,
            ReadModelKey = readModelKey,
            InitialVersion = before.Value["version"],
            FinalVersion = final.Value!["version"],
            InitialETag = before.ETag,
            FinalETag = final.ETag,
            DetailMetadataEntries = metadata.Count,
            DetailMetadataKeyValueUtf8Bytes = Encoding.UTF8.GetByteCount(string.Concat(metadata.Select(kv => kv.Key + kv.Value))),
            DetailMetadataSerializedUtf8Bytes = serializedMetadataBytes,
            Scenarios = new[] { "two-host-signal-and-scoped-detail", "both-host-anonymous-expired-wrong-tenant-missing-grant-forged-scope-denials",
                "missing-mismatched-expired-provenance-with-positive-control", "reordered-and-repeated-hints-persisted-readback",
                "both-host-17-entry-and-multibyte-oversize-denials-with-positive-control", "information-level-metadata-value-suppression",
                "registered-shipped-wildcard-misses-dynamic-topic-beside-exact-topic-control",
                "both-host-persistent-tenant-b-signal-detail-isolation-during-publication-rejoin-recovery",
                "reauthorized-rejoin-readback", "sidecar-outage-bounded-query-and-recovery" },
            Limits = new[] { "synthetic-state-does-not-prove-a-projection-change", "no-production-issuer-or-profile",
                "no-dynamic-topic-coverage", "no-ui-completion-or-freshness-state-proof", "no-provider-host-outage-or-transition-rollback" },
        }, cancellation.Token).ConfigureAwait(true);
    }

    private static async Task AssertConnectionDenialsAsync(
        Uri endpoint, string tenantA, string tenantB, string projection, CancellationToken cancellationToken)
    {
        Uri hubUri = new(endpoint, "/hubs/projection-changes");
        await using HubConnection anonymous = new HubConnectionBuilder().WithUrl(hubUri).Build();
        await Should.ThrowAsync<HttpRequestException>(() => anonymous.StartAsync(cancellationToken)).ConfigureAwait(true);
        string expiredToken = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenantA], permissions: ["query:read"], expires: DateTime.UtcNow.AddMinutes(-10));
        await using HubConnection expired = new HubConnectionBuilder()
            .WithUrl(hubUri, options => options.AccessTokenProvider = () => Task.FromResult<string?>(expiredToken)).Build();
        await Should.ThrowAsync<HttpRequestException>(() => expired.StartAsync(cancellationToken)).ConfigureAwait(true);

        foreach (string[] grants in new[] { new[] { tenantB }, Array.Empty<string>() })
        {
            string token = TestJwtTokenGenerator.GenerateToken(tenants: grants, permissions: ["query:read"]);
            await using HubConnection denied = new HubConnectionBuilder()
                .WithUrl(hubUri, options => options.AccessTokenProvider = () => Task.FromResult<string?>(token)).Build();
            await denied.StartAsync(cancellationToken).ConfigureAwait(true);
            HubException denial = await Should.ThrowAsync<HubException>(() =>
                denied.InvokeAsync("JoinGroup", projection, tenantA, cancellationToken)).ConfigureAwait(true);
            denial.Message.ShouldNotContain(tenantA);
            await Should.ThrowAsync<HubException>(() => denied.InvokeAsync(
                "JoinGroupScoped", projection, tenantA, "forged-scope", cancellationToken)).ConfigureAwait(true);
            await Should.ThrowAsync<HubException>(() => denied.InvokeAsync(
                "JoinGroup", projection, "malformed:tenant", cancellationToken)).ConfigureAwait(true);
        }
    }

    private static async Task<(bool ForeignTenant, int AuthorizedHints)> ObserveTenantBoundaryAsync<T>(
        ChannelReader<T> reader,
        Func<T, string> getTenant,
        string authorizedTenant,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        int authorizedHints = 0;
        try
        {
            while (true)
            {
                T notice = await reader.ReadAsync(timeout.Token).ConfigureAwait(false);
                if (!string.Equals(getTenant(notice), authorizedTenant, StringComparison.Ordinal))
                {
                    return (true, authorizedHints);
                }

                // A restarted {uuid} consumer may replay this tenant's own positive controls.
                authorizedHints++;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, authorizedHints);
        }
    }

    private static HubConnection CreateConnection(
        Uri endpoint,
        string tenant,
        ChannelWriter<(string Projection, string Tenant)> signalWriter,
        ChannelWriter<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>? detailWriter)
    {
        string token = TestJwtTokenGenerator.GenerateToken(tenants: [tenant], permissions: ["query:read"]);
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(endpoint, "/hubs/projection-changes").ToString(), options => options.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .Build();
        _ = connection.On<string, string>("ProjectionChanged", (projection, targetTenant) => signalWriter.TryWrite((projection, targetTenant)));
        if (detailWriter is not null)
        {
            _ = connection.On<string, string, string?, IReadOnlyDictionary<string, string>>(
                "ProjectionChangedDetail",
                (projection, targetTenant, scope, metadata) => detailWriter.TryWrite((projection, targetTenant, scope, metadata)));
        }

        return connection;
    }

    private static string CreateProvenance(string projection, string tenant, string topic, DateTime? expires = null)
    {
        var claims = new[] {
            new Claim(EventStoreWorkloadAuthenticationDefaults.CallerClaimType, "eventstore"),
            new Claim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.ProjectionNotify),
            new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, tenant),
            new Claim(EventStoreWorkloadAuthenticationDefaults.ProjectionTypeBindingClaimType, projection),
            new Claim(EventStoreWorkloadAuthenticationDefaults.TopicBindingClaimType, topic),
        };
        DateTime expiry = expires ?? DateTime.UtcNow.AddMinutes(2);
        DateTime issuedAt = expiry < DateTime.UtcNow ? expiry.AddMinutes(-2) : DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = TestJwtTokenGenerator.Issuer,
            Audience = "eventstore",
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiry,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtTokenGenerator.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };
        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    private static async Task<T> ReadAsync<T>(ChannelReader<T> reader, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(s_positiveWait);
        return await reader.ReadAsync(timeout.Token).ConfigureAwait(false);
    }

    private static async Task<bool> ReceivesWithinAsync<T>(
        ChannelReader<T> reader,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(window);
        try
        {
            _ = await reader.ReadAsync(timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task<ReadModelEntry<Dictionary<string, int>>> WaitForReadbackAsync(
        DaprReadModelStore store,
        string key,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            try
            {
                return await store.GetAsync<Dictionary<string, int>>("statestore", key, timeout.Token).ConfigureAwait(false);
            }
            catch (DaprException) when (!timeout.IsCancellationRequested)
            {
                await Task.Delay(250, timeout.Token).ConfigureAwait(false);
            }
        }
    }
}
