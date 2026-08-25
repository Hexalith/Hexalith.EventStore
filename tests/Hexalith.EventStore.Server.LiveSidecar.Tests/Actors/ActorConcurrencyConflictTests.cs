
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;
/// <summary>
/// Serialized-actor controls plus generic state-store ETag semantics.
/// The generic-state probe does not establish actor-state conflict behavior; Story 4.5 records
/// actor-state evidence separately in <see cref="AppendDurabilityRaceLiveSidecarTests"/>.
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class ActorConcurrencyConflictTests
{
    private const string EvidenceDirectoryEnvironmentVariable = "HEXALITH_STORY_4_5_EVIDENCE_DIR";
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly DaprTestContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ActorConcurrencyConflictTests(DaprTestContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Task 3.1: Test ETag-based conflict detection on aggregate metadata key.
    /// Sequential commands to the same aggregate should succeed (no conflict).
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_SequentialCommands_NoConflict()
    {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"concurrency-seq-{Guid.NewGuid():N}";
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            _fixture.AggregateActorTypeName);

        // Act - send multiple commands sequentially
        for (int i = 0; i < 5; i++)
        {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command).ConfigureAwait(true);

            // Assert
            result.Accepted.ShouldBeTrue($"Sequential command {i + 1} should succeed");
        }
    }

    /// <summary>
    /// Task 3.2: Test concurrent command submissions produce conflict responses.
    /// Dapr actors are single-threaded per actor, so concurrent calls are serialized.
    /// This test verifies that rapid sequential calls to the same actor all succeed
    /// (Dapr's turn-based concurrency model prevents conflicts at the actor level).
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_RapidSequentialCommands_AllSucceed()
    {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"concurrency-rapid-{Guid.NewGuid():N}";
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            _fixture.AggregateActorTypeName);

        // Act - fire multiple commands as quickly as possible
        var tasks = new List<Task<CommandProcessingResult>>();
        for (int i = 0; i < 3; i++)
        {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            tasks.Add(proxy.ProcessCommandAsync(command));
        }

        CommandProcessingResult[] results = await Task.WhenAll(tasks).ConfigureAwait(true);

        // Assert - all should succeed (Dapr serializes calls to the same actor)
        foreach (CommandProcessingResult result in results)
        {
            result.Accepted.ShouldBeTrue("All commands to same actor should succeed (turn-based concurrency)");
        }
    }

    /// <summary>
    /// Proves a generic-state ETag became stale, requires Dapr's exact 409 mismatch surface, and
    /// verifies through Redis that the successful first conditional update remains persisted.
    /// This is deliberately a generic-state control, not aggregate actor-state evidence.
    /// </summary>
    [Fact]
    public async Task MetadataKey_StaleEtagUpdate_IsRejected()
    {
        string? mutationArmed = Story45MutationSwitch.Armed;
        using var operationCancellation = new CancellationTokenSource(OperationTimeout);
        string key = $"story-4-5-generic-etag-{Guid.NewGuid():N}";
        string seedJson = JsonSerializer.Serialize(new { writer = "seed", version = 0 });
        string firstUpdateJson = JsonSerializer.Serialize(new { writer = "first", version = 1 });
        string rejectedUpdateJson = JsonSerializer.Serialize(new { writer = "stale", version = 2 });

        using HttpResponseMessage seed = await SaveStateAsync(
            key,
            seedJson,
            etag: null,
            operationCancellation.Token).ConfigureAwait(true);
        seed.StatusCode.ShouldBe(
            HttpStatusCode.NoContent,
            "the generic-state seed write is a harness precondition, not one of the recorded invariants");

        (string originalJson, string originalEtag) = await GetStateWithEtagAsync(
            key,
            operationCancellation.Token).ConfigureAwait(true);
        JsonCapture original = JsonCapture.From(originalJson);

        using HttpResponseMessage first = await SaveStateAsync(
            key,
            firstUpdateJson,
            originalEtag,
            operationCancellation.Token).ConfigureAwait(true);
        first.StatusCode.ShouldBe(
            HttpStatusCode.NoContent,
            "the intervening conditional write is a harness precondition, not one of the recorded invariants");

        (string currentJson, string currentEtag) = await GetStateWithEtagAsync(
            key,
            operationCancellation.Token).ConfigureAwait(true);
        JsonCapture current = JsonCapture.From(currentJson);
        bool etagAdvanced = !string.Equals(originalEtag, currentEtag, StringComparison.Ordinal);
        bool seedThenFirstObserved = string.Equals(original.Writer, "seed", StringComparison.Ordinal)
            && string.Equals(current.Writer, "first", StringComparison.Ordinal);

        // Perturbation: replay the token that is still current instead of the stale one, so the
        // provider has nothing to reject and the 409 surface never appears.
        string replayedEtag = Story45MutationSwitch.IsArmed("generic-409-semantics")
            ? currentEtag
            : originalEtag;
        using HttpResponseMessage stale = await SaveStateAsync(
            key,
            rejectedUpdateJson,
            replayedEtag,
            operationCancellation.Token).ConfigureAwait(true);
        string staleResponseBody = await stale.Content
            .ReadAsStringAsync(operationCancellation.Token)
            .ConfigureAwait(true);
        DaprStateErrorParser.Capture staleError = DaprStateErrorParser.Parse(staleResponseBody);

        // The store must retain the value of the last write the provider *acknowledged*. When the
        // stale replay is rejected that is the first conditional update; when a perturbation makes
        // the provider accept the replay, the expectation follows the acknowledgement instead of
        // spuriously falsifying the retention invariant.
        bool staleReplayAcknowledged = (int)stale.StatusCode is >= 200 and < 300;
        string expectedRetainedJson = staleReplayAcknowledged ? rejectedUpdateJson : firstUpdateJson;
        string expectedRetainedWriter = staleReplayAcknowledged ? "stale" : "first";

        // Perturbation: read the retained value from a key the run never wrote, so the retention
        // check stops inspecting the value the provider actually kept.
        string retainedReadKey = Story45MutationSwitch.IsArmed("retained-generic-value")
            ? $"{key}-never-written"
            : key;

        string? retainedRedisJson = null;
        string? retainedReadExceptionType = null;
        string? retainedReadExceptionMessage = null;
        JsonNode? retainedRedisNode = null;
        JsonElement? retainedRedisValue = null;
        try
        {
            retainedRedisJson = await _fixture.GetGenericStateJsonAsync(retainedReadKey).ConfigureAwait(true);

            // Parse inside the guard: a malformed or non-JSON body must be captured as a read
            // diagnostic, never thrown before the evidence file is written.
            if (retainedRedisJson is not null)
            {
                retainedRedisNode = JsonNode.Parse(retainedRedisJson);
                retainedRedisValue = JsonSerializer.Deserialize<JsonElement>(retainedRedisJson);
            }
        }
        catch (Exception ex)
        {
            retainedReadExceptionType = ex.GetType().FullName;
            retainedReadExceptionMessage = ex.Message;
        }

        bool exactConflictSurface = stale.StatusCode == HttpStatusCode.Conflict
            && string.Equals(staleError.ErrorCode, "ERR_STATE_SAVE", StringComparison.Ordinal)
            && staleError.Message?.Contains("etag mismatch", StringComparison.OrdinalIgnoreCase) == true;
        bool retainedValueMatchesExpected = retainedRedisNode is not null
            && JsonNode.DeepEquals(retainedRedisNode, JsonNode.Parse(expectedRetainedJson));
        bool staleTokenProvenStale = etagAdvanced && seedThenFirstObserved;

        var invariants = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["stale-token-proven-stale"] = staleTokenProvenStale,
            ["generic-409-semantics"] = exactConflictSurface,
            ["retained-generic-value"] = retainedValueMatchesExpected,
        };
        var evidence = new
        {
            schemaVersion = 3,
            baselineCommit = "0776785f494fcefc8ad933b5b17b9c8d5cbe0513",
            mutationArmed,
            key,
            seedStatus = (int)seed.StatusCode,
            original = new
            {
                etag = originalEtag,
                rawJson = originalJson,
                value = original.Value,
                parseExceptionType = original.ExceptionType,
            },
            interveningUpdate = new
            {
                status = (int)first.StatusCode,
                etag = currentEtag,
                rawJson = currentJson,
                value = current.Value,
                parseExceptionType = current.ExceptionType,
                etagAdvanced,
            },
            staleReplay = new
            {
                suppliedEtag = replayedEtag,
                suppliedEtagWasStale = !Story45MutationSwitch.IsArmed("generic-409-semantics"),
                status = (int)stale.StatusCode,
                responseBody = staleResponseBody,
                errorCode = staleError.ErrorCode,
                errorMessage = staleError.Message,
                parseError = staleError.ParseError,
                acknowledged = staleReplayAcknowledged,
            },
            retainedReadKey,
            expectedRetainedWriter,
            redisRetainedValue = retainedRedisValue,
            redisRetainedRawJson = retainedRedisJson,
            retainedValueMatchesExpected,
            retainedReadExceptionType,
            retainedReadExceptionMessage,
            invariants,
        };
        await WriteEvidenceAsync("generic-etag-control.json", evidence).ConfigureAwait(true);

        AssertInvariants(invariants);
    }

    /// <summary>A non-throwing capture of a Dapr-sourced JSON body.</summary>
    /// <param name="Value">The parsed document, when it parsed.</param>
    /// <param name="Writer">The <c>writer</c> property, when present as a string.</param>
    /// <param name="ExceptionType">Why the body could not be parsed.</param>
    private sealed record JsonCapture(JsonElement? Value, string? Writer, string? ExceptionType)
    {
        /// <summary>Parses a Dapr-sourced body without throwing.</summary>
        /// <param name="json">The verbatim body.</param>
        /// <returns>The capture.</returns>
        public static JsonCapture From(string json)
        {
            try
            {
                JsonElement value = JsonSerializer.Deserialize<JsonElement>(json);
                string? writer = value.ValueKind == JsonValueKind.Object
                    && value.TryGetProperty("writer", out JsonElement writerElement)
                    && writerElement.ValueKind == JsonValueKind.String
                        ? writerElement.GetString()
                        : null;
                return new JsonCapture(value, writer, null);
            }
            catch (Exception ex)
            {
                return new JsonCapture(null, null, ex.GetType().FullName);
            }
        }
    }

    private async Task<(string Json, string ETag)> GetStateWithEtagAsync(
        string key,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        string url = $"{_fixture.DaprHttpEndpoint}/v1.0/state/statestore/{Uri.EscapeDataString(key)}";

        for (int attempt = 0; attempt < 10; attempt++)
        {
            using HttpResponseMessage response = await http
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(true);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                string json = await response.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(true);
                string? etagHeader = response.Headers.ETag?.Tag;
                if (string.IsNullOrWhiteSpace(etagHeader)
                    && response.Headers.TryGetValues("ETag", out IEnumerable<string>? values))
                    {
                    etagHeader = values.FirstOrDefault();
                }

                string etag = (etagHeader ?? string.Empty).Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(etag))
                {
                    return (json, etag);
                }
            }

            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
            await Task.Delay(50, cancellationToken).ConfigureAwait(true);
        }

        throw new ShouldAssertException($"Generic state '{key}' did not become readable with an ETag.");
    }

    private async Task<HttpResponseMessage> SaveStateAsync(
        string key,
        string valueJson,
        string? etag,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        string escapedKey = JsonSerializer.Serialize(key);
        string body = etag is null
            ? $"[{{\"key\":{escapedKey},\"value\":{valueJson}}}]"
            : $"[{{\"key\":{escapedKey},\"value\":{valueJson},\"etag\":{JsonSerializer.Serialize(etag)},\"options\":{{\"concurrency\":\"first-write\",\"consistency\":\"strong\"}}}}]";

        var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await http
            .PostAsync($"{_fixture.DaprHttpEndpoint}/v1.0/state/statestore", content, cancellationToken)
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Asserts every named invariant in one assertion whose message enumerates <em>all</em> failed
    /// invariant tags, so attribution never depends on assertion order.
    /// </summary>
    /// <param name="invariants">The named invariant results.</param>
    private static void AssertInvariants(IReadOnlyDictionary<string, bool> invariants)
    {
        string[] failed = [.. invariants.Where(item => !item.Value).Select(item => item.Key)];
        failed.ShouldBeEmpty(
            "Falsified Story 4.5 invariants: "
            + string.Join(" ", failed.Select(name => $"[invariant:{name}]")));
    }

    private async Task WriteEvidenceAsync(string fileName, object evidence)
    {
        string json = JsonSerializer.Serialize(evidence, EvidenceJsonOptions);
        _output.WriteLine(json);

        string? directory = Environment.GetEnvironmentVariable(EvidenceDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory) || Story45MutationSwitch.Armed is not null)
        {
            return;
        }

        _ = Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, fileName),
            string.Concat(json, Environment.NewLine)).ConfigureAwait(true);
    }
}
