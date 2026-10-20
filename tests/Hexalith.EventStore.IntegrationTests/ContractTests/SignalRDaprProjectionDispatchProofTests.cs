using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>Composes a real sample projection dispatch with two Dapr-fed local hubs.</summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspirePubSubProjectionTests")]
public sealed class SignalRDaprProjectionDispatchProofTests(AspirePubSubProjectionTestFixture aspire)
{
    /// <summary>Checks a real projection dispatch through two exact-topic Dapr subscribers.</summary>
    [Fact]
    public async Task IncrementCounter_PublishesProjectionHintToBothHosts_WithPersistedReadback()
    {
        DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
        string tenant = $"tenant-a-{Guid.NewGuid():N}";
        const string projection = "counter";
        const string scope = "counter-status";
        string aggregateId = $"distribution-projection-{Guid.NewGuid():N}";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using SignalRDaprDistributionFixture distribution = await SignalRDaprDistributionFixture
            .StartAsync(
                [$"{tenant}.counter.projection-changed"],
                cancellation.Token,
                enablePubSubProjectionTransport: true,
                aggregateActorTypeName: aspire.AggregateActorTypeName).ConfigureAwait(true);

        var firstSignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var secondSignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var otherTenantFirstSignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var otherTenantSecondSignals = Channel.CreateUnbounded<(string Projection, string Tenant)>();
        var firstDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        var secondDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        var otherTenantFirstDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        var otherTenantSecondDetails = Channel.CreateUnbounded<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)>();
        await using HubConnection first = CreateConnection(distribution.First.AppEndpoint, tenant, firstSignals.Writer, firstDetails.Writer);
        await using HubConnection second = CreateConnection(distribution.Second.AppEndpoint, tenant, secondSignals.Writer, secondDetails.Writer);
        await using HubConnection otherTenantFirst = CreateConnection(distribution.First.AppEndpoint, "tenant-b", otherTenantFirstSignals.Writer, otherTenantFirstDetails.Writer);
        await using HubConnection otherTenantSecond = CreateConnection(distribution.Second.AppEndpoint, "tenant-b", otherTenantSecondSignals.Writer, otherTenantSecondDetails.Writer);
        await first.StartAsync(cancellation.Token).ConfigureAwait(true);
        await second.StartAsync(cancellation.Token).ConfigureAwait(true);
        await otherTenantFirst.StartAsync(cancellation.Token).ConfigureAwait(true);
        await otherTenantSecond.StartAsync(cancellation.Token).ConfigureAwait(true);
        await first.InvokeAsync("JoinGroup", projection, tenant, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroup", projection, tenant, cancellation.Token).ConfigureAwait(true);
        await first.InvokeAsync("JoinGroupScoped", projection, tenant, scope, cancellation.Token).ConfigureAwait(true);
        await second.InvokeAsync("JoinGroupScoped", projection, tenant, scope, cancellation.Token).ConfigureAwait(true);
        foreach (HubConnection otherTenant in new[] { otherTenantFirst, otherTenantSecond })
        {
            await otherTenant.InvokeAsync("JoinGroup", projection, "tenant-b", cancellation.Token).ConfigureAwait(true);
            await otherTenant.InvokeAsync("JoinGroupScoped", projection, "tenant-b", scope, cancellation.Token).ConfigureAwait(true);
            await Should.ThrowAsync<HubException>(() => otherTenant.InvokeAsync("JoinGroup", projection, tenant, cancellation.Token)).ConfigureAwait(true);
            await Should.ThrowAsync<HubException>(() => otherTenant.InvokeAsync("JoinGroupScoped", projection, tenant, scope, cancellation.Token)).ConfigureAwait(true);
        }

        string correlationId;
        try
        {
            correlationId = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdWithRetryAsync(
                aspire.EventStoreClient, tenant, projection, aggregateId, "IncrementCounter",
                retryTimeout: TimeSpan.FromSeconds(12)).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Composed command failed. First host: {distribution.First.SnapshotRecentApplicationLogs()} Second host: {distribution.Second.SnapshotRecentApplicationLogs()}",
                exception);
        }
        JsonElement status = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            aspire.EventStoreClient, correlationId, tenant, timeout: TimeSpan.FromSeconds(90)).ConfigureAwait(true);
        status.GetProperty("status").GetString().ShouldBe("Completed", status.ToString());
        status.GetProperty("eventCount").GetInt32().ShouldBeGreaterThan(0);

        (await ReadNoticeAsync(firstSignals.Reader, distribution, cancellation.Token).ConfigureAwait(true))
            .ShouldBe((projection, tenant));
        (await ReadNoticeAsync(secondSignals.Reader, distribution, cancellation.Token).ConfigureAwait(true))
            .ShouldBe((projection, tenant));
        var firstDetail = await ReadNoticeAsync(firstDetails.Reader, distribution, cancellation.Token).ConfigureAwait(true);
        var secondDetail = await ReadNoticeAsync(secondDetails.Reader, distribution, cancellation.Token).ConfigureAwait(true);
        foreach (var detail in new[] { firstDetail, secondDetail })
        {
            detail.Projection.ShouldBe(projection);
            detail.Tenant.ShouldBe(tenant);
            detail.Scope.ShouldBe(scope);
            detail.Metadata.Count.ShouldBe(1);
            detail.Metadata["change-kind"].ShouldBe("counter-projection-updated");
            JsonSerializer.SerializeToUtf8Bytes(detail.Metadata).Length.ShouldBeLessThanOrEqualTo(2_048);
        }
        bool[] deniedDelivery = await Task.WhenAll(
            ReceivedWithinAsync(otherTenantFirstSignals.Reader, TimeSpan.FromSeconds(1), cancellation.Token),
            ReceivedWithinAsync(otherTenantSecondSignals.Reader, TimeSpan.FromSeconds(1), cancellation.Token),
            ReceivedWithinAsync(otherTenantFirstDetails.Reader, TimeSpan.FromSeconds(1), cancellation.Token),
            ReceivedWithinAsync(otherTenantSecondDetails.Reader, TimeSpan.FromSeconds(1), cancellation.Token)).ConfigureAwait(true);
        deniedDelivery.Any(static received => received)
            .ShouldBeFalse("tenant B must not receive tenant A's projection dispatch on either host");

        using var firstClient = new HttpClient { BaseAddress = distribution.First.AppEndpoint };
        using var secondClient = new HttpClient { BaseAddress = distribution.Second.AppEndpoint };
        (int firstCount, string firstETag) = await PollQueryAsync(firstClient, tenant, aggregateId, cancellation.Token).ConfigureAwait(true);
        (int secondCount, string secondETag) = await PollQueryAsync(secondClient, tenant, aggregateId, cancellation.Token).ConfigureAwait(true);
        firstCount.ShouldBe(1);
        secondCount.ShouldBe(1);
        firstETag.ShouldNotBeNullOrWhiteSpace();
        secondETag.ShouldNotBeNullOrWhiteSpace();
        secondETag.ShouldBe(firstETag, "both hosts must read the same authoritative projection version");

        await distribution.CaptureEvidenceAsync("real-projection-distribution", startedUtc, new
        {
            Tenant = tenant,
            Projection = projection,
            Topic = $"{tenant}.counter.projection-changed",
            AggregateId = aggregateId,
            CorrelationId = correlationId,
            CommandStatus = status.GetProperty("status").GetString(),
            EventCount = status.GetProperty("eventCount").GetInt32(),
            FirstConnectionId = first.ConnectionId,
            SecondConnectionId = second.ConnectionId,
            TenantBFirstConnectionId = otherTenantFirst.ConnectionId,
            TenantBSecondConnectionId = otherTenantSecond.ConnectionId,
            FirstQueryCount = firstCount,
            SecondQueryCount = secondCount,
            FirstQueryETag = firstETag,
            SecondQueryETag = secondETag,
            QueryProvenance = "ProjectionBacked",
            RealProjectionScopedDetail = true,
            GroupScope = scope,
            FirstDetailMetadata = firstDetail.Metadata,
            SecondDetailMetadata = secondDetail.Metadata,
            OtherTenantReceivedSignalOnEitherHost = false,
            OtherTenantReceivedDetailOnEitherHost = false,
            Limits = new[] { "exact-topic-development-profile-only", "optional-legacy-projection-response-detail-only",
                "no-production-issuer-or-profile", "no-dynamic-topic-coverage", "no-transition-or-rollback-approval" },
        }, cancellation.Token).ConfigureAwait(true);

        string? evidencePath = Environment.GetEnvironmentVariable("HEXALITH_STORY_2_13_COMPOSED_EVIDENCE_PATH");
        if (!string.IsNullOrWhiteSpace(evidencePath))
        {
            string evidence = JsonSerializer.Serialize(new
            {
                StartedUtc = startedUtc,
                CompletedUtc = DateTimeOffset.UtcNow,
                Result = "passed-local-real-projection-dispatch",
                Transport = "PubSub",
                AppId = "eventstore",
                Tenant = tenant,
                Topic = $"{tenant}.counter.projection-changed",
                AggregateId = aggregateId,
                CorrelationId = correlationId,
                CommandStatus = status.GetProperty("status").GetString(),
                EventCount = status.GetProperty("eventCount").GetInt32(),
                FirstHostProcessId = distribution.First.ApplicationProcessId,
                SecondHostProcessId = distribution.Second.ApplicationProcessId,
                FirstSidecarProcessId = distribution.First.SidecarProcessId,
                SecondSidecarProcessId = distribution.Second.SidecarProcessId,
                FirstHostPort = distribution.First.AppPort,
                SecondHostPort = distribution.Second.AppPort,
                PubSubComponentSha256 = distribution.PubSubComponentSha256,
                StateComponentSha256 = distribution.StateComponentSha256,
                ExactSubscriptionSha256 = distribution.SubscriptionSha256(0),
                FirstQueryCount = firstCount,
                SecondQueryCount = secondCount,
                FirstQueryETag = firstETag,
                SecondQueryETag = secondETag,
                QueryProvenance = "ProjectionBacked",
                OtherTenantReceivedSignal = false,
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(evidencePath, evidence + "\n", cancellation.Token).ConfigureAwait(true);
        }
    }

    private static HubConnection CreateConnection(Uri endpoint, string tenant, ChannelWriter<(string Projection, string Tenant)> sink,
        ChannelWriter<(string Projection, string Tenant, string? Scope, IReadOnlyDictionary<string, string> Metadata)> details)
    {
        string token = TestJwtTokenGenerator.GenerateToken(tenants: [tenant], permissions: ["query:read"]);
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(endpoint, "/hubs/projection-changes"),
                options => options.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .Build();
        connection.On<string, string>("ProjectionChanged", (projection, tenant) => sink.TryWrite((projection, tenant)));
        connection.On<string, string, string?, IReadOnlyDictionary<string, string>>("ProjectionChangedDetail",
            (projection, tenant, scope, metadata) => details.TryWrite((projection, tenant, scope, metadata)));
        return connection;
    }

    private static async Task<T> ReadNoticeAsync<T>(
        ChannelReader<T> reader,
        SignalRDaprDistributionFixture distribution,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            return await reader.ReadAsync(timeout.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Projection dispatch was not observed on both hosts. First: {distribution.First.SnapshotDiagnostics()} Second: {distribution.Second.SnapshotDiagnostics()}",
                exception);
        }
    }

    private static async Task<bool> ReceivedWithinAsync<T>(
        ChannelReader<T> reader,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(window);
        try
        {
            return await reader.WaitToReadAsync(timeout.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task<(int Count, string ETag)> PollQueryAsync(
        HttpClient client,
        string tenant,
        string aggregateId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        string last = "no response";
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenGenerator.GenerateToken(
                    tenants: [tenant], domains: ["counter"], permissions: ["query:read"]));
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                tenant,
                domain = "counter",
                aggregateId,
                projectionType = "counter",
                queryType = "get-counter-status",
                entityId = aggregateId,
                payload = new { id = aggregateId },
            }), Encoding.UTF8, "application/json");
            try
            {
                using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(true);
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
                last = $"{(int)response.StatusCode} {body}";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using JsonDocument document = JsonDocument.Parse(body);
                    JsonElement payload = document.RootElement.GetProperty("payload");
                    int count;
                    if (payload.ValueKind == JsonValueKind.String)
                    {
                        using JsonDocument decoded = JsonDocument.Parse(Convert.FromBase64String(payload.GetString()!));
                        count = decoded.RootElement.GetProperty("count").GetInt32();
                    }
                    else
                    {
                        count = payload.GetProperty("count").GetInt32();
                    }

                    if (count == 1)
                    {
                        response.Headers.GetValues("X-Hexalith-Query-Provenance").Single()
                            .ShouldBe("ProjectionBacked");
                        document.RootElement.GetProperty("metadata").GetProperty("provenance").GetString()
                            .ShouldBe("ProjectionBacked");
                        response.Headers.ETag.ShouldNotBeNull();
                        response.Headers.ETag.IsWeak.ShouldBeFalse();
                        return (count, response.Headers.ETag?.ToString() ?? string.Empty);
                    }
                }
            }
            catch (HttpRequestException exception)
            {
                last = exception.GetType().Name;
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(true);
        }

        throw new TimeoutException($"The secondary EventStore host did not query the real projection: {last}");
    }
}
