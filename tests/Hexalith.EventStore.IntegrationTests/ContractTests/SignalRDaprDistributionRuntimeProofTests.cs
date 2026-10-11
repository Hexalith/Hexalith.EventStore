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
        string topic = NamingConventionEngine.GetProjectionChangedTopic(projection, tenantA);
        string deniedProjection = $"denied-{suffix}";
        string deniedTopic = NamingConventionEngine.GetProjectionChangedTopic(deniedProjection, tenantA);
        string recoveredProjection = $"recovered-{suffix}";
        string recoveredTopic = NamingConventionEngine.GetProjectionChangedTopic(recoveredProjection, tenantA);
        await using SignalRDaprDistributionFixture fixture = await SignalRDaprDistributionFixture
            .StartAsync([topic, deniedTopic, recoveredTopic], cancellation.Token).ConfigureAwait(true);
        string readModelKey = $"story-2-13:{tenantA}:{projection}";

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
        var tenantBSignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        await using HubConnection first = CreateConnection(fixture.First.AppEndpoint, tenantA, firstSignals.Writer, firstDetails.Writer);
        await using HubConnection second = CreateConnection(fixture.Second.AppEndpoint, tenantA, secondSignals.Writer, secondDetails.Writer);
        await using HubConnection otherTenant = CreateConnection(fixture.Second.AppEndpoint, tenantB, tenantBSignals.Writer, null);
        Uri hubUri = new(fixture.First.AppEndpoint, "/hubs/projection-changes");
        await using HubConnection anonymous = new HubConnectionBuilder().WithUrl(hubUri).Build();
        await Should.ThrowAsync<HttpRequestException>(() => anonymous.StartAsync(cancellation.Token)).ConfigureAwait(true);
        string expiredToken = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenantA], permissions: ["query:read"], expires: DateTime.UtcNow.AddMinutes(-10));
        await using HubConnection expired = new HubConnectionBuilder()
            .WithUrl(hubUri, options => options.AccessTokenProvider = () => Task.FromResult<string?>(expiredToken))
            .Build();
        await Should.ThrowAsync<HttpRequestException>(() => expired.StartAsync(cancellation.Token)).ConfigureAwait(true);
        await first.StartAsync(cancellation.Token).ConfigureAwait(true);
        await second.StartAsync(cancellation.Token).ConfigureAwait(true);
        await otherTenant.StartAsync(cancellation.Token).ConfigureAwait(true);
        await first.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token).ConfigureAwait(true);
        await first.InvokeAsync("JoinGroup", deniedProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroup", deniedProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        await otherTenant.InvokeAsync("JoinGroup", projection, tenantB, cancellation.Token).ConfigureAwait(true);
        await Should.ThrowAsync<HubException>(() => otherTenant.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token)).ConfigureAwait(true);
        await Should.ThrowAsync<HubException>(() => otherTenant.InvokeAsync(
            "JoinGroupScoped", projection, tenantA, $"scope-{suffix}", cancellation.Token)).ConfigureAwait(true);
        await Should.ThrowAsync<HubException>(() => first.InvokeAsync(
            "JoinGroup", projection, "malformed:tenant", cancellation.Token)).ConfigureAwait(true);

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
        (await ReceivesWithinAsync(tenantBSignals.Reader, TimeSpan.FromSeconds(1), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("tenant B has a same-named projection but no tenant A grant");

        await writerStore.SaveAsync("statestore", readModelKey, new Dictionary<string, int> { ["version"] = 2 }, cancellation.Token).ConfigureAwait(true);
        string scope = $"scope-{suffix}";
        await first.InvokeAsync("JoinGroupScoped", projection, tenantA, scope, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroupScoped", projection, tenantA, scope, cancellation.Token).ConfigureAwait(true);
        var metadata = Enumerable.Range(0, 16).ToDictionary(i => $"k{i:D2}", _ => new string('x', 124), StringComparer.Ordinal);
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
        firstDetail.Metadata.Count.ShouldBe(16);
        secondDetail.Metadata.Count.ShouldBe(16);
        Encoding.UTF8.GetByteCount(string.Concat(firstDetail.Metadata.Select(kv => kv.Key + kv.Value))).ShouldBeLessThanOrEqualTo(2048);
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

        // A reconnect obtains fresh group membership. A replacement identity without the tenant grant cannot rejoin.
        await first.StopAsync(cancellation.Token).ConfigureAwait(true);
        await using HubConnection revoked = CreateConnection(fixture.First.AppEndpoint, tenantB, tenantBSignals.Writer, null);
        await revoked.StartAsync(cancellation.Token).ConfigureAwait(true);
        await Should.ThrowAsync<HubException>(() => revoked.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token)).ConfigureAwait(true);
        await using HubConnection reconnected = CreateConnection(fixture.First.AppEndpoint, tenantA, firstSignals.Writer, firstDetails.Writer);
        await reconnected.StartAsync(cancellation.Token).ConfigureAwait(true);
        await reconnected.InvokeAsync("JoinGroup", projection, tenantA, cancellation.Token).ConfigureAwait(true);
        await publisher.PublishEventAsync(
            "pubsub", topic,
            new ProjectionChangedNotification(projection, tenantA) { Provenance = CreateProvenance(projection, tenantA, topic) },
            cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantA));
        (await ReadAsync(secondSignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((projection, tenantA));
        (await ReceivesWithinAsync(tenantBSignals.Reader, TimeSpan.FromSeconds(1), cancellation.Token).ConfigureAwait(true))
            .ShouldBeFalse("a revoked or cross-tenant client must not receive the later notification");

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
        await using HubConnection recoveryFirst = CreateConnection(fixture.First.AppEndpoint, tenantA, firstRecoverySignals.Writer, null);
        await using HubConnection recoverySecond = CreateConnection(fixture.Second.AppEndpoint, tenantA, secondRecoverySignals.Writer, null);
        await recoveryFirst.StartAsync(cancellation.Token).ConfigureAwait(true);
        await recoverySecond.StartAsync(cancellation.Token).ConfigureAwait(true);
        await recoveryFirst.InvokeAsync("JoinGroup", recoveredProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        await recoverySecond.InvokeAsync("JoinGroup", recoveredProjection, tenantA, cancellation.Token).ConfigureAwait(true);
        await publisher.PublishEventAsync(
            "pubsub", recoveredTopic,
            new ProjectionChangedNotification(recoveredProjection, tenantA)
            {
                Provenance = CreateProvenance(recoveredProjection, tenantA, recoveredTopic),
            },
            cancellation.Token).ConfigureAwait(true);
        (await ReadAsync(firstRecoverySignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((recoveredProjection, tenantA));
        (await ReadAsync(secondRecoverySignals.Reader, cancellation.Token).ConfigureAwait(true)).ShouldBe((recoveredProjection, tenantA));
        ReadModelEntry<Dictionary<string, int>> final = await readerStore
            .GetAsync<Dictionary<string, int>>("statestore", readModelKey, cancellation.Token).ConfigureAwait(true);
        final.ETag.ShouldBe(after.ETag);

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
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(evidencePath, evidence + "\n", cancellation.Token).ConfigureAwait(true);
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

    private static string CreateProvenance(string projection, string tenant, string topic)
    {
        var claims = new[] {
            new Claim(EventStoreWorkloadAuthenticationDefaults.CallerClaimType, "eventstore"),
            new Claim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.ProjectionNotify),
            new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, tenant),
            new Claim(EventStoreWorkloadAuthenticationDefaults.ProjectionTypeBindingClaimType, projection),
            new Claim(EventStoreWorkloadAuthenticationDefaults.TopicBindingClaimType, topic),
        };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = TestJwtTokenGenerator.Issuer,
            Audience = "eventstore",
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(2),
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
