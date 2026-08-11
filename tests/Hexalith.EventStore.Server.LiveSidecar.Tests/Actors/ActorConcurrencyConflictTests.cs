
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
    private const string MutationEnvironmentVariable = "HEXALITH_STORY_4_5_MUTATION";
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
        seed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (string originalJson, string originalEtag) = await GetStateWithEtagAsync(
            key,
            operationCancellation.Token).ConfigureAwait(true);
        JsonDocument.Parse(originalJson).RootElement.GetProperty("writer").GetString().ShouldBe("seed");

        using HttpResponseMessage first = await SaveStateAsync(
            key,
            firstUpdateJson,
            originalEtag,
            operationCancellation.Token).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (string currentJson, string currentEtag) = await GetStateWithEtagAsync(
            key,
            operationCancellation.Token).ConfigureAwait(true);
        currentEtag.ShouldNotBe(originalEtag, "the successful intervening write must advance the ETag before the original token is called stale");
        JsonDocument.Parse(currentJson).RootElement.GetProperty("writer").GetString().ShouldBe("first");

        // Perturbation: replay the token that is still current instead of the stale one, so the
        // provider has nothing to reject and the 409 surface never appears.
        string replayedEtag = IsMutation("generic-409-semantics") ? currentEtag : originalEtag;
        using HttpResponseMessage stale = await SaveStateAsync(
            key,
            rejectedUpdateJson,
            replayedEtag,
            operationCancellation.Token).ConfigureAwait(true);
        string staleResponseBody = await stale.Content
            .ReadAsStringAsync(operationCancellation.Token)
            .ConfigureAwait(true);
        DaprStateErrorParser.Capture staleError = DaprStateErrorParser.Parse(staleResponseBody);

        // Perturbation: overwrite the retained value after the rejection, leaving the 409 surface
        // intact so attribution stays on the retention invariant.
        if (IsMutation("retained-generic-value"))
        {
            using HttpResponseMessage overwrite = await SaveStateAsync(
                key,
                rejectedUpdateJson,
                etag: null,
                operationCancellation.Token).ConfigureAwait(true);
            overwrite.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        string? retainedRedisJson = null;
        string? retainedReadExceptionType = null;
        string? retainedReadExceptionMessage = null;
        JsonNode? retainedRedisNode = null;
        JsonElement? retainedRedisValue = null;
        try
        {
            retainedRedisJson = await _fixture.GetGenericStateJsonAsync(key).ConfigureAwait(true);

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
            && JsonNode.DeepEquals(retainedRedisNode, JsonNode.Parse(firstUpdateJson));

        var evidence = new
        {
            schemaVersion = 2,
            baselineCommit = "0776785f494fcefc8ad933b5b17b9c8d5cbe0513",
            key,
            seedStatus = (int)seed.StatusCode,
            original = new
            {
                etag = originalEtag,
                value = JsonSerializer.Deserialize<JsonElement>(originalJson),
            },
            interveningUpdate = new
            {
                status = (int)first.StatusCode,
                etag = currentEtag,
                value = JsonSerializer.Deserialize<JsonElement>(currentJson),
                etagAdvanced = !string.Equals(originalEtag, currentEtag, StringComparison.Ordinal),
            },
            staleReplay = new
            {
                suppliedEtag = replayedEtag,
                status = (int)stale.StatusCode,
                responseBody = staleResponseBody,
                errorCode = staleError.ErrorCode,
                errorMessage = staleError.Message,
                parseError = staleError.ParseError,
            },
            redisRetainedValue = retainedRedisValue,
            redisRetainedRawJson = retainedRedisJson,
            retainedValueMatchesExpected,
            retainedReadExceptionType,
            retainedReadExceptionMessage,
        };
        await WriteEvidenceAsync("generic-etag-control.json", evidence).ConfigureAwait(true);

        AssertInvariant(
            exactConflictSurface,
            "generic-409-semantics",
            "the stale generic-state update must return HTTP 409 with Dapr ERR_STATE_SAVE ETag-mismatch semantics");
        AssertInvariant(
            retainedValueMatchesExpected,
            "retained-generic-value",
            "Redis must retain the full successful first conditional value after rejecting the stale replay");
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
    /// Asserts one material invariant, tagging the failure message with the invariant name so a
    /// mutation receipt binds to the invariant that actually failed.
    /// </summary>
    /// <param name="satisfied">The observed invariant value.</param>
    /// <param name="invariantName">The stable invariant name.</param>
    /// <param name="message">The human-readable requirement.</param>
    private static void AssertInvariant(bool satisfied, string invariantName, string message)
        => satisfied.ShouldBeTrue($"[invariant:{invariantName}] {message}");

    /// <summary>
    /// Indicates whether the named input perturbation is armed. A mutation changes what the
    /// harness does; it never inverts an assertion.
    /// </summary>
    /// <param name="mutationName">The perturbation name.</param>
    /// <returns><see langword="true"/> when the perturbation is armed.</returns>
    private static bool IsMutation(string mutationName)
        => string.Equals(
            Environment.GetEnvironmentVariable(MutationEnvironmentVariable),
            mutationName,
            StringComparison.Ordinal);

    private async Task WriteEvidenceAsync(string fileName, object evidence)
    {
        string json = JsonSerializer.Serialize(evidence, EvidenceJsonOptions);
        _output.WriteLine(json);

        string? directory = Environment.GetEnvironmentVariable(EvidenceDirectoryEnvironmentVariable);
        string? mutation = Environment.GetEnvironmentVariable(MutationEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory) || !string.IsNullOrWhiteSpace(mutation))
        {
            return;
        }

        _ = Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, fileName),
            string.Concat(json, Environment.NewLine)).ConfigureAwait(true);
    }
}
