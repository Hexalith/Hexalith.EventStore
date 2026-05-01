using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspirePubSubProofTests")]
public sealed class PubSubDeliveryProofTests {
    private static readonly TimeSpan SubscriberTimeout = TimeSpan.FromSeconds(30);

    // Recovery wall-clock budget after the fault flag is removed: drain reminder fires every
    // DrainPeriod=1s up to MaxDrainPeriod=5s, then the published event is observed by the
    // subscriber and the status flips to Completed. 25s gives ~5 drain attempts of head-room
    // for slow CI workers without hiding genuine drain regressions.
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(25);

    private readonly AspirePubSubProofTestFixture _fixture;

    public PubSubDeliveryProofTests(AspirePubSubProofTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PubSubDelivery_CommandPublished_SubscriberReceivesCloudEvent() {
        // Each test uses a fresh GUID-derived correlation id, so the subscriber's
        // correlation-id-filtered GET starts empty for this run -- no global clear required.
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
        received.GetProperty("type").GetString().ShouldEndWith(".CounterIncremented");
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
        string aggregateId = $"r4a5-drain-{Guid.NewGuid():N}";
        TryWriteFaultFile();
        try {
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
            // retryCount may be >= 0: with InitialDrainDelay=1s the drain reminder can fire
            // between PublishFailed terminal-status poll and this read. At-least-once drain
            // semantics make any non-negative count valid; the assertion below proves the
            // counter is bounded and not pathologically growing.
            GetInt32(drainRecord, "retryCount").ShouldBeGreaterThanOrEqualTo(0);
            GetInt32(drainRecord, "retryCount").ShouldBeLessThan(20);
            string? failureReason = GetString(drainRecord, "lastFailureReason");
            failureReason.ShouldNotBeNull();
            failureReason.ShouldContain("Configured test publish fault");

            TryDeleteFaultFile();

            JsonElement completedStatus = await PollUntilStatusAsync(correlationId, "tenant-a", "Completed", RecoveryTimeout)
                .ConfigureAwait(true);
            completedStatus.GetProperty("eventCount").GetInt32().ShouldBe(1);

            JsonElement[] events = await WaitForSubscriberEventsAsync(correlationId, expectedCount: 1);
            events.Select(e => e.GetProperty("sequenceNumber").GetString()).ShouldContain("1");
            events.ShouldAllBe(e => e.GetProperty("correlationId").GetString() == correlationId);

            DrainRecordPresence absence = await CheckDrainRecordAbsentAsync(aggregateId, correlationId);
            absence.ShouldBe(DrainRecordPresence.Absent, $"drain record should be removed after successful recovery publish (probe outcome: {absence})");
        }
        finally {
            TryDeleteFaultFile();
        }
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
        HttpStatusCode lastCode = default;

        while (DateTimeOffset.UtcNow < deadline) {
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{correlationId}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage statusResponse = await _fixture.EventStoreClient.SendAsync(statusRequest).ConfigureAwait(false);
            lastCode = statusResponse.StatusCode;
            if (statusResponse.StatusCode == HttpStatusCode.OK) {
                lastStatus = await statusResponse.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
                if (lastStatus.GetProperty("status").GetString() == expectedStatus) {
                    return lastStatus;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Command {correlationId} did not reach {expectedStatus} within {timeout}. Last HTTP {(int)lastCode}, last body: {lastStatus}.");
    }

    private async Task<JsonElement> ReadDrainRecordAsync(string aggregateId, string correlationId) {
        JsonElement? record = await TryReadDrainRecordAsync(aggregateId, correlationId).ConfigureAwait(false);
        return record ?? throw new ShouldAssertException($"Expected drain record for {correlationId}.");
    }

    private async Task<JsonElement?> TryReadDrainRecordAsync(string aggregateId, string correlationId) {
        string key = BuildDrainKey(aggregateId, correlationId);
        IDatabase db = _fixture.RedisDatabase;

        // DAPR Redis state-store actor records can be stored either as plain string keys or as
        // hashes (depending on DAPR version / cluster topology). Try string first, then hash.
        // If the key is genuinely absent both probes return IsNull; any other failure is allowed
        // to surface so we never collapse "read failure" into "key absent" (R2-A6).
        RedisValue stringValue;
        try {
            stringValue = await db.StringGetAsync(key).ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("WRONGTYPE", StringComparison.Ordinal)) {
            stringValue = RedisValue.Null;
        }

        if (!stringValue.IsNull) {
            return ParseJson(stringValue!);
        }

        RedisValue hashValue = await db.HashGetAsync(key, "data").ConfigureAwait(false);
        return hashValue.IsNull ? null : ParseJson(hashValue!);

        static JsonElement ParseJson(string json) {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private async Task<DrainRecordPresence> CheckDrainRecordAbsentAsync(string aggregateId, string correlationId) {
        string key = BuildDrainKey(aggregateId, correlationId);
        IDatabase db = _fixture.RedisDatabase;
        try {
            bool keyExists = await db.KeyExistsAsync(key).ConfigureAwait(false);
            return keyExists ? DrainRecordPresence.Present : DrainRecordPresence.Absent;
        }
        catch (RedisException) {
            return DrainRecordPresence.ProbeFailed;
        }
    }

    private static string BuildDrainKey(string aggregateId, string correlationId) {
        string actorId = $"tenant-a:counter:{aggregateId}";
        return $"eventstore||AggregateActor||{actorId}||drain:{correlationId}";
    }

    private void TryWriteFaultFile() {
        Exception? lastError = null;
        for (int attempt = 0; attempt < 5; attempt++) {
            try {
                File.WriteAllText(_fixture.FaultFilePath, "fault");
                return;
            }
            catch (IOException ex) {
                lastError = ex;
            }
            catch (UnauthorizedAccessException ex) {
                lastError = ex;
            }

            Thread.Sleep(50);
        }

        throw new InvalidOperationException(
            $"Failed to write fault file at {_fixture.FaultFilePath} after retries.", lastError);
    }

    private void TryDeleteFaultFile() {
        for (int attempt = 0; attempt < 5; attempt++) {
            try {
                if (!File.Exists(_fixture.FaultFilePath)) {
                    return;
                }

                File.Delete(_fixture.FaultFilePath);
                return;
            }
            catch (IOException) {
            }
            catch (UnauthorizedAccessException) {
            }

            Thread.Sleep(50);
        }
        // Ignore final failure: the fixture's TryDeleteFaultFile in DisposeAsync will retry,
        // and even if it lingers it only affects the next run if env vars also leak.
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

        if (string.IsNullOrEmpty(propertyName)) {
            value = default;
            return false;
        }

        string pascalName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(pascalName, out value)) {
            return true;
        }

        value = default;
        return false;
    }

    private enum DrainRecordPresence {
        Absent,
        Present,
        ProbeFailed,
    }
}
