using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspirePubSubProofTests")]
public sealed class PubSubDeliveryProofTests {
    private static readonly TimeSpan SubscriberTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(20);

    private readonly AspirePubSubProofTestFixture _fixture;

    public PubSubDeliveryProofTests(AspirePubSubProofTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PubSubDelivery_CommandPublished_SubscriberReceivesCloudEvent() {
        await ClearSubscriberEventsAsync();
        string aggregateId = $"r4a5-delivery-{Guid.NewGuid():N}";

        string correlationId = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdAsync(
            _fixture.EventStoreClient,
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        JsonElement status = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            _fixture.EventStoreClient,
            correlationId,
            "tenant-a",
            timeout: TimeSpan.FromSeconds(45));

        status.GetProperty("status").GetString().ShouldBe("Completed");
        status.GetProperty("eventCount").GetInt32().ShouldBeGreaterThan(0);

        JsonElement[] events = await WaitForSubscriberEventsAsync(correlationId, expectedCount: 1);
        JsonElement received = events[0];
        received.GetProperty("topic").GetString().ShouldBe("tenant-a.counter.events");
        received.GetProperty("type").GetString().ShouldBe("CounterIncremented");
        received.GetProperty("source").GetString().ShouldBe("hexalith-eventstore/tenant-a/counter");
        received.GetProperty("id").GetString().ShouldBe($"{correlationId}:1");
        received.GetProperty("correlationId").GetString().ShouldBe(correlationId);
        received.GetProperty("sequenceNumber").GetString().ShouldBe("1");
        received.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
        received.GetProperty("domain").GetString().ShouldBe("counter");
        received.GetProperty("aggregateId").GetString().ShouldBe(aggregateId);
    }

    [Fact]
    public async Task PubSubDrain_PublishFailsThenRecovers_DrainsPersistedEvents() {
        await ClearSubscriberEventsAsync();
        File.WriteAllText(_fixture.FaultFilePath, "fault");
        string aggregateId = $"r4a5-drain-{Guid.NewGuid():N}";

        string correlationId = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdAsync(
            _fixture.EventStoreClient,
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        JsonElement publishFailedStatus = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            _fixture.EventStoreClient,
            correlationId,
            "tenant-a",
            timeout: TimeSpan.FromSeconds(45));

        publishFailedStatus.GetProperty("status").GetString().ShouldBe("PublishFailed");
        publishFailedStatus.GetProperty("eventCount").GetInt32().ShouldBe(1);

        JsonElement drainRecord = await ReadDrainRecordAsync(aggregateId, correlationId);
        GetString(drainRecord, "correlationId").ShouldBe(correlationId);
        GetInt64(drainRecord, "startSequence").ShouldBe(1);
        GetInt64(drainRecord, "endSequence").ShouldBe(1);
        GetInt32(drainRecord, "eventCount").ShouldBe(1);
        GetString(drainRecord, "commandType").ShouldBe("IncrementCounter");
        GetInt32(drainRecord, "retryCount").ShouldBe(0);
        string? failureReason = GetString(drainRecord, "lastFailureReason");
        failureReason.ShouldNotBeNull();
        failureReason.ShouldContain("Configured test publish fault");

        File.Delete(_fixture.FaultFilePath);

        JsonElement completedStatus = await PollUntilStatusAsync(correlationId, "tenant-a", "Completed", RecoveryTimeout)
            .ConfigureAwait(true);
        completedStatus.GetProperty("eventCount").GetInt32().ShouldBe(1);

        JsonElement[] events = await WaitForSubscriberEventsAsync(correlationId, expectedCount: 1);
        events.Select(e => e.GetProperty("sequenceNumber").GetString()).ShouldContain("1");
        events.ShouldAllBe(e => e.GetProperty("correlationId").GetString() == correlationId);

        JsonElement? removedRecord = await TryReadDrainRecordAsync(aggregateId, correlationId);
        removedRecord.ShouldBeNull("drain record should be removed after successful recovery publish");
    }

    private async Task ClearSubscriberEventsAsync() {
        using HttpResponseMessage response = await _fixture.SubscriberClient.DeleteAsync("/events").ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<JsonElement[]> WaitForSubscriberEventsAsync(string correlationId, int expectedCount) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(SubscriberTimeout);
        JsonElement[] lastEvents = [];

        while (DateTimeOffset.UtcNow < deadline) {
            lastEvents = await GetSubscriberEventsAsync(correlationId).ConfigureAwait(false);
            if (lastEvents.Length >= expectedCount) {
                return lastEvents;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Subscriber did not receive {expectedCount} event(s) for correlation id {correlationId}. Last count: {lastEvents.Length}.");
    }

    private async Task<JsonElement[]> GetSubscriberEventsAsync(string correlationId) {
        using HttpResponseMessage response = await _fixture.SubscriberClient
            .GetAsync($"/events?correlationId={Uri.EscapeDataString(correlationId)}")
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement[]>().ConfigureAwait(false) ?? [];
    }

    private async Task<JsonElement> PollUntilStatusAsync(
        string correlationId,
        string tenant,
        string expectedStatus,
        TimeSpan timeout) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: ["counter"],
            permissions: ["command:query"]);

        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        JsonElement lastStatus = default;

        while (DateTimeOffset.UtcNow < deadline) {
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{correlationId}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage statusResponse = await _fixture.EventStoreClient.SendAsync(statusRequest).ConfigureAwait(false);
            if (statusResponse.StatusCode == HttpStatusCode.OK) {
                lastStatus = await statusResponse.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
                if (lastStatus.GetProperty("status").GetString() == expectedStatus) {
                    return lastStatus;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Command {correlationId} did not reach {expectedStatus} within {timeout}. Last status: {lastStatus}.");
    }

    private static async Task<JsonElement> ReadDrainRecordAsync(string aggregateId, string correlationId) =>
        await TryReadDrainRecordAsync(aggregateId, correlationId).ConfigureAwait(false)
        ?? throw new ShouldAssertException($"Expected drain record for {correlationId}.");

    private static async Task<JsonElement?> TryReadDrainRecordAsync(string aggregateId, string correlationId) {
        string actorId = $"tenant-a:counter:{aggregateId}";
        string key = $"eventstore||AggregateActor||{actorId}||drain:{correlationId}";
        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:3501") };
        using HttpResponseMessage response = await client
            .GetAsync($"/v1.0/state/statestore/{Uri.EscapeDataString(key)}")
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound) {
            return null;
        }

        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string? GetString(JsonElement element, string propertyName) {
        if (TryGetProperty(element, propertyName, out JsonElement property)) {
            return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        }

        return null;
    }

    private static int GetInt32(JsonElement element, string propertyName) {
        if (TryGetProperty(element, propertyName, out JsonElement property)) {
            return property.GetInt32();
        }

        throw new InvalidOperationException($"Property '{propertyName}' was not found.");
    }

    private static long GetInt64(JsonElement element, string propertyName) {
        if (TryGetProperty(element, propertyName, out JsonElement property)) {
            return property.GetInt64();
        }

        throw new InvalidOperationException($"Property '{propertyName}' was not found.");
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value) {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value)) {
            return true;
        }

        string pascalName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(pascalName, out value)) {
            return true;
        }

        value = default;
        return false;
    }
}
