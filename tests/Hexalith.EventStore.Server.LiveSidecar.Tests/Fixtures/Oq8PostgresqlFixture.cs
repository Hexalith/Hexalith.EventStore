using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Runs two EventStore processes and one Sample process behind independent Dapr sidecars that
/// share the tracked <c>oq8-postgresql-v1</c> production state and resiliency profile.
/// </summary>
public sealed class Oq8PostgresqlFixture : IAsyncLifetime
{
    /// <summary>Prefix used only for raw-key leakage sentinels.</summary>
    public const string ProtectedRawKeyPrefix = "PROTECTED-OQ8-RAW-SENTINEL";

    private const string EventStoreAppId = "eventstore";
    private const string SampleAppId = "sample";
    private const string PostgresImage = "postgres:18.4";
    private const string AggregateActorTypeName = "Oq8AggregateActor";
    private const string ActiveVersionOne = "oq8-v1";
    private const string ActiveVersionTwo = "oq8-v2";
    private const string VersionOneKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private const string VersionTwoKey = "ZmVkY2JhOTg3NjU0MzIxMGZlZGNiYTk4NzY1NDMyMTA=";
    private const string AuthenticationIssuer = "hexalith-oq8-evidence";
    private const string AuthenticationAudience = "hexalith-eventstore";
    private const string AuthenticationSigningKey = "Oq8EvidenceOnlySigningKey-AtLeast32Characters";
    private const int PlacementContainerPort = 50005;
    private const int SchedulerContainerPort = 50006;
    private const int HealthTimeoutSeconds = 60;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private readonly Oq8ProcessNode[] _eventStoreNodes =
    [
        new() { Name = "eventstore-1", AppId = EventStoreAppId, IsEventStore = true },
        new() { Name = "eventstore-2", AppId = EventStoreAppId, IsEventStore = true },
    ];
    private readonly Oq8ProcessNode _sampleNode =
        new() { Name = "sample", AppId = SampleAppId, IsEventStore = false };
    private readonly Dictionary<string, JsonElement> _evidence = new(StringComparer.Ordinal);
    private readonly HashSet<string> _sensitiveValues = new(StringComparer.Ordinal);
    private readonly object _sensitiveLock = new();
    private string _activeDigestVersion = ActiveVersionOne;
    private string[] _readerDigestVersions = [];
    private string _bearerToken = string.Empty;
    private string _clockFile = string.Empty;
    private string _componentsDirectory = string.Empty;
    private string _postgresConnectionString = string.Empty;
    private string _postgresContainerName = string.Empty;
    private string _repositoryRoot = string.Empty;
    private string _runtimeDirectory = string.Empty;
    private IPEndPoint? _placementHostEndpoint;
    private IPEndPoint? _schedulerHostEndpoint;

    /// <summary>Gets a value indicating whether every host and sidecar has a distinct OS process.</summary>
    public bool HasIndependentProcesses
    {
        get
        {
            int[] processIds = AllNodes()
                .SelectMany(static node => new[] { node.Application?.Id, node.Sidecar?.Id })
                .OfType<int>()
                .ToArray();
            return processIds.Length == 6 && processIds.Distinct().Count() == processIds.Length;
        }
    }

    /// <summary>Gets the number of actual Sample <c>/process</c> boundary crossings.</summary>
    public int SampleBoundaryCount => ReadCounter(_sampleNode.CounterFile);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        try
        {
            _repositoryRoot = FindRepositoryRoot();
            RegisterSensitive(_repositoryRoot);
            RegisterSensitiveTestMaterial();
            await VerifyPrerequisitesAsync().ConfigureAwait(false);
            await StartPostgresAsync().ConfigureAwait(false);
            _runtimeDirectory = Path.Combine(Path.GetTempPath(), $"eventstore-oq8-runtime-{Guid.NewGuid():N}");
            _componentsDirectory = Path.Combine(_runtimeDirectory, "components");
            _clockFile = Path.Combine(_runtimeDirectory, "clock.txt");
            _ = Directory.CreateDirectory(_componentsDirectory);
            RegisterSensitive(_runtimeDirectory);
            CreateProductionProfileResources();
            SetClock(new DateTimeOffset(2026, 8, 10, 8, 0, 0, TimeSpan.Zero));
            PrepareShadowApplications();
            AllocatePortsAndCounters();
            _bearerToken = CreateBearerToken();
            RegisterSensitive(_bearerToken);

            await StartNodeAsync(_sampleNode).ConfigureAwait(false);
            foreach (Oq8ProcessNode node in _eventStoreNodes)
            {
                await StartNodeAsync(node).ConfigureAwait(false);
            }

            await Task.Delay(2000).ConfigureAwait(false);
        }
        catch
        {
            await DisposeResourcesAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Sets the shared deterministic clock used by both EventStore processes.</summary>
    /// <param name="value">The UTC instant.</param>
    public void SetClock(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            value = value.ToUniversalTime();
        }

        string temporary = _clockFile + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, value.UtcTicks.ToString(CultureInfo.InvariantCulture));
        File.Move(temporary, _clockFile, overwrite: true);
    }

    /// <summary>Submits one command through an independently hosted public EventStore HTTP boundary.</summary>
    /// <param name="nodeIndex">The zero-based EventStore node.</param>
    /// <param name="tenant">The managed tenant.</param>
    /// <param name="aggregateId">The aggregate identity.</param>
    /// <param name="rawKey">The protected opaque key.</param>
    /// <param name="payloadJson">The semantic command payload.</param>
    /// <returns>A support-safe response projection.</returns>
    internal async Task<Oq8CommandObservation> SubmitAsync(
        int nodeIndex,
        string tenant,
        string aggregateId,
        string rawKey,
        string payloadJson)
    {
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        string correlationId = UniqueIdHelper.GenerateSortableUniqueStringId();
        RegisterSensitive(tenant);
        RegisterSensitive(rawKey);
        RegisterSensitive(aggregateId);
        RegisterSensitive(payloadJson);
        RegisterSensitive(messageId);
        RegisterSensitive(correlationId);

        using JsonDocument payload = JsonDocument.Parse(payloadJson);
        var request = new
        {
            messageId,
            tenant,
            domain = "counter",
            aggregateId,
            commandType = "IncrementCounter",
            payload = payload.RootElement.Clone(),
            correlationId,
            idempotencyKey = rawKey,
        };
        using var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{GetRunningEventStoreNode(nodeIndex).ApplicationEndpoint}/api/v1/commands")
        {
            Content = content,
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using HttpResponseMessage response = await client.SendAsync(message).ConfigureAwait(false);
        string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        string? responseMessageId = null;
        string? resultDigest = null;
        string? reasonCode = null;
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try
            {
                using JsonDocument responseJson = JsonDocument.Parse(responseText);
                JsonElement root = responseJson.RootElement;
                if (root.TryGetProperty("messageId", out JsonElement identity)
                    && identity.ValueKind == JsonValueKind.String)
                {
                    responseMessageId = identity.GetString();
                    RegisterSensitive(responseMessageId);
                }

                if (root.TryGetProperty("resultPayload", out JsonElement result))
                {
                    string rawResult = result.GetRawText();
                    RegisterSensitive(rawResult);
                    resultDigest = HashResultPayload(rawResult);
                }

                if (root.TryGetProperty("reasonCode", out JsonElement reason)
                    && reason.ValueKind == JsonValueKind.String)
                {
                    reasonCode = reason.GetString();
                }
                else if (root.TryGetProperty("code", out JsonElement code)
                    && code.ValueKind == JsonValueKind.String)
                {
                    reasonCode = code.GetString();
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"The OQ8 command boundary returned malformed JSON with HTTP {(int)response.StatusCode}.",
                    exception);
            }
        }

        if (response.StatusCode == HttpStatusCode.Accepted && resultDigest is null)
        {
            resultDigest = HashResultPayload(null);
        }

        return new Oq8CommandObservation(
            response.StatusCode,
            responseMessageId is null ? null : HashUtf8(responseMessageId),
            resultDigest,
            reasonCode);
    }

    /// <summary>Retries the same logical request until failover routing becomes available.</summary>
    /// <param name="nodeIndex">The surviving EventStore node.</param>
    /// <param name="tenant">The managed tenant.</param>
    /// <param name="aggregateId">The aggregate identity.</param>
    /// <param name="rawKey">The protected opaque key.</param>
    /// <param name="payloadJson">The semantic command payload.</param>
    /// <returns>The successful response and bounded attempt count.</returns>
    internal async Task<(Oq8CommandObservation Observation, int Attempts)> SubmitAfterFailoverAsync(
        int nodeIndex,
        string tenant,
        string aggregateId,
        string rawKey,
        string payloadJson)
    {
        Oq8CommandObservation? last = null;
        for (int attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                last = await SubmitAsync(nodeIndex, tenant, aggregateId, rawKey, payloadJson)
                    .ConfigureAwait(false);
                if ((int)last.StatusCode < 500)
                {
                    return (last, attempt);
                }
            }
            catch (HttpRequestException) when (attempt < 20)
            {
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"OQ8 failover routing remained unavailable after 20 attempts; last status was {(int?)last?.StatusCode ?? 0}.");
    }

    /// <summary>Waits until the Sample boundary reaches an exact invocation count.</summary>
    /// <param name="expected">The expected count.</param>
    public async Task WaitForSampleBoundaryCountAsync(int expected)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (SampleBoundaryCount == expected)
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"The Sample boundary count did not reach the expected support-safe count {expected}.");
    }

    /// <summary>Inspects one protected admission authority without returning any protected identity.</summary>
    /// <param name="tenant">The managed tenant.</param>
    /// <param name="rawKey">The opaque key.</param>
    /// <param name="digestVersion">The digest version, or the active version when omitted.</param>
    /// <returns>A support-safe durable-state projection.</returns>
    internal async Task<Oq8AdmissionSnapshot> InspectAdmissionAsync(
        string tenant,
        string rawKey,
        string? digestVersion = null)
    {
        string actorId = BuildActorId(tenant, digestVersion ?? _activeDigestVersion, rawKey);
        RegisterSensitive(actorId);
        IIdempotencyAdmissionActor actor = CreateActorProxy<IIdempotencyAdmissionActor>(
            actorId,
            IdempotencyAdmissionActor.ActorTypeName);
        IdempotencyAdmissionInspection inspection = await actor.InspectAsync().ConfigureAwait(false);
        IdempotencyAdmissionRecord? record = inspection.Record;
        IdempotencyAdmissionTombstone? tombstone = inspection.Tombstone;
        return new Oq8AdmissionSnapshot(
            inspection.Exists,
            record?.State ?? tombstone?.State,
            record?.FencingToken ?? 0,
            record?.IntentDigest is not null,
            record?.ReplayResult is not null,
            record?.ReplayResult is null
                ? null
                : HashResultPayload(record.ReplayResult.ResultPayload),
            record?.ReplayExpiresAt,
            record?.ExecutionMessageId is not null || record?.ExecutionCorrelationId is not null,
            record is null && tombstone is not null);
    }

    /// <summary>Finds the EventStore process currently hosting the sole live admission actor.</summary>
    /// <returns>The zero-based owner node.</returns>
    public async Task<int> FindAdmissionOwnerAsync()
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            var owners = new List<int>();
            for (int index = 0; index < _eventStoreNodes.Length; index++)
            {
                try
                {
                    if (await GetHostedActorCountAsync(_eventStoreNodes[index], IdempotencyAdmissionActor.ActorTypeName)
                        .ConfigureAwait(false) > 0)
                    {
                        owners.Add(index);
                    }
                }
                catch (HttpRequestException)
                {
                }
                catch (TaskCanceledException)
                {
                }
                catch (JsonException)
                {
                }
            }

            if (owners.Count == 1)
            {
                return owners[0];
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new InvalidOperationException("The sole OQ8 admission actor owner could not be resolved.");
    }

    /// <summary>Stops one known EventStore process and sidecar without touching PostgreSQL.</summary>
    /// <param name="nodeIndex">The zero-based owner node.</param>
    public Task StopEventStoreNodeAsync(int nodeIndex)
        => StopNodeAsync(_eventStoreNodes[nodeIndex]);

    /// <summary>Restarts one EventStore process and sidecar against the same PostgreSQL store.</summary>
    /// <param name="nodeIndex">The zero-based node.</param>
    public Task RestartEventStoreNodeAsync(int nodeIndex)
        => StartNodeAsync(_eventStoreNodes[nodeIndex]);

    /// <summary>Restarts both EventStore processes with a rotated active digest authority.</summary>
    public async Task RotateDigestAuthorityAsync()
    {
        foreach (Oq8ProcessNode node in _eventStoreNodes)
        {
            await StopNodeAsync(node).ConfigureAwait(false);
        }

        _activeDigestVersion = ActiveVersionTwo;
        _readerDigestVersions = [ActiveVersionOne];
        foreach (Oq8ProcessNode node in _eventStoreNodes)
        {
            await StartNodeAsync(node).ConfigureAwait(false);
        }
    }

    /// <summary>Restarts both EventStore processes after retiring the old digest reader.</summary>
    public async Task RetireOldDigestReaderAsync()
    {
        foreach (Oq8ProcessNode node in _eventStoreNodes)
        {
            await StopNodeAsync(node).ConfigureAwait(false);
        }

        _readerDigestVersions = [];
        foreach (Oq8ProcessNode node in _eventStoreNodes)
        {
            await StartNodeAsync(node).ConfigureAwait(false);
        }
    }

    /// <summary>Verifies that rotation left one stable v2 authority and a read-only v1 redirect.</summary>
    /// <param name="tenant">The managed tenant.</param>
    /// <param name="rawKey">The opaque key.</param>
    /// <returns>The derived stable-authority observation.</returns>
    internal async Task<Oq8RotatedAuthoritySnapshot> HasCanonicalRotatedAuthorityAsync(
        string tenant,
        string rawKey)
    {
        string sourceId = BuildActorId(tenant, ActiveVersionOne, rawKey);
        string targetId = BuildActorId(tenant, ActiveVersionTwo, rawKey);
        RegisterSensitive(sourceId);
        RegisterSensitive(targetId);
        IdempotencyAdmissionDirectoryAlias[] aliases =
        [
            CreateDirectoryAlias(tenant, ActiveVersionTwo, rawKey),
            CreateDirectoryAlias(tenant, ActiveVersionOne, rawKey),
        ];
        IIdempotencyAdmissionDirectoryInspectionActor directory =
            CreateActorProxy<IIdempotencyAdmissionDirectoryInspectionActor>(
                tenant,
                IdempotencyAdmissionDirectoryActor.ActorTypeName);
        IIdempotencyAdmissionActor source = CreateActorProxy<IIdempotencyAdmissionActor>(
            sourceId,
            IdempotencyAdmissionActor.ActorTypeName);
        IIdempotencyAdmissionActor target = CreateActorProxy<IIdempotencyAdmissionActor>(
            targetId,
            IdempotencyAdmissionActor.ActorTypeName);
        IdempotencyAdmissionDirectoryResult? directoryState = await directory
            .InspectAsync(aliases)
            .ConfigureAwait(false);
        IdempotencyAdmissionInspection sourceState = await source.InspectAsync().ConfigureAwait(false);
        IdempotencyAdmissionInspection targetState = await target.InspectAsync().ConfigureAwait(false);
        bool directoryStable = directoryState is not null
            && directoryState.PromotionPhase == IdempotencyAdmissionPromotionPhase.Stable
            && string.Equals(directoryState.CanonicalActorId, targetId, StringComparison.Ordinal)
            && directoryState.PromotionSourceActorId is null
            && directoryState.PromotionTargetActorId is null;
        bool sourceRedirectValid = sourceState.Exists
            && string.Equals(sourceState.RedirectActorId, targetId, StringComparison.Ordinal);
        bool targetActivated = targetState.Exists
            && targetState.Record?.State == IdempotencyAdmissionState.Terminal
            && targetState.Record.FencingToken > 0
            && targetState.RedirectActorId is null
            && targetState.Promotion?.Activated == true;
        int canonicalAuthorityCount = new[] { sourceState, targetState }.Count(static state =>
            state.Exists
            && state.Record?.State == IdempotencyAdmissionState.Terminal
            && state.Record.FencingToken > 0
            && state.RedirectActorId is null
            && state.Promotion?.Activated == true);
        return new Oq8RotatedAuthoritySnapshot(
            directoryStable && sourceRedirectValid && targetActivated && canonicalAuthorityCount == 1,
            canonicalAuthorityCount,
            directoryStable,
            sourceRedirectValid,
            targetActivated);
    }

    /// <summary>Starts managed deletion retention for a tenant.</summary>
    /// <param name="tenant">The managed tenant.</param>
    /// <param name="approvedAt">The approved deletion instant.</param>
    /// <returns>The resulting lifecycle state.</returns>
    public async Task<IdempotencyTenantLifecycleState> EnterDeletionAsync(
        string tenant,
        DateTimeOffset approvedAt)
    {
        RegisterSensitive(tenant);
        IIdempotencyTenantLifecycleActor actor = CreateActorProxy<IIdempotencyTenantLifecycleActor>(
            tenant,
            IdempotencyTenantLifecycleActor.ActorTypeName);
        return (await actor.EnterDeletionAsync(approvedAt).ConfigureAwait(false)).State;
    }

    /// <summary>Places the tenant retention countdown under legal hold.</summary>
    /// <param name="tenant">The managed tenant.</param>
    /// <param name="observedAt">The observation instant.</param>
    /// <returns>The resulting lifecycle state.</returns>
    public async Task<IdempotencyTenantLifecycleState> PlaceLegalHoldAsync(
        string tenant,
        DateTimeOffset observedAt)
    {
        RegisterSensitive(tenant);
        IIdempotencyTenantLifecycleActor actor = CreateActorProxy<IIdempotencyTenantLifecycleActor>(
            tenant,
            IdempotencyTenantLifecycleActor.ActorTypeName);
        return (await actor.PlaceLegalHoldAsync(observedAt).ConfigureAwait(false)).State;
    }

    /// <summary>Releases a legal hold without reopening admission.</summary>
    /// <param name="tenant">The managed tenant.</param>
    /// <param name="observedAt">The observation instant.</param>
    /// <returns>The resulting lifecycle state.</returns>
    public async Task<IdempotencyTenantLifecycleState> ReleaseLegalHoldAsync(
        string tenant,
        DateTimeOffset observedAt)
    {
        RegisterSensitive(tenant);
        IIdempotencyTenantLifecycleActor actor = CreateActorProxy<IIdempotencyTenantLifecycleActor>(
            tenant,
            IdempotencyTenantLifecycleActor.ActorTypeName);
        return (await actor.ReleaseLegalHoldAsync(observedAt).ConfigureAwait(false)).State;
    }

    /// <summary>Captures only structural PostgreSQL projections and invariant results.</summary>
    /// <param name="stage">The bounded evidence stage.</param>
    /// <returns>The sanitized structural snapshot.</returns>
    internal async Task<Oq8PostgresqlSnapshot> CapturePostgresqlSnapshotAsync(string stage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        string schema = await RunPostgresQueryAsync(
            "select string_agg(column_name || ':' || data_type || ':' || is_nullable, ',' order by ordinal_position) "
            + "from information_schema.columns where table_schema='public' and table_name='state';")
            .ConfigureAwait(false);
        string statistics = await RunPostgresQueryAsync(
            "with s as (select split_part(key,'||',2) actor_type, split_part(key,'||',4) state_name, value, key from state) "
            + "select count(*), "
            + "count(*) filter (where actor_type='IdempotencyAdmissionActor' and state_name='admission'), "
            + "count(*) filter (where actor_type='IdempotencyAdmissionActor' and state_name='admission' and value->>'state'='5'), "
            + "count(*) filter (where actor_type='IdempotencyAdmissionActor' and state_name='tombstone'), "
            + "count(*) filter (where actor_type='IdempotencyAdmissionActor' and state_name='tombstone' "
            + "and not (value ?| array['fencingToken','replayResult','intentDigest','executionMessageId','executionCorrelationId'])), "
            + "count(*) filter (where actor_type='IdempotencyAdmissionDirectoryActor'), "
            + "count(*) filter (where actor_type='IdempotencyTenantLifecycleActor' and state_name='lifecycle'), "
            + $"count(*) filter (where actor_type='{AggregateActorTypeName}' and state_name like '%:metadata'), "
            + $"count(*) filter (where actor_type='{AggregateActorTypeName}' and state_name like '%:events:%'), "
            + $"coalesce(sum(case when actor_type='{AggregateActorTypeName}' and state_name like '%:metadata' "
            + "and value->>'currentSequence' ~ '^[0-9]+$' then (value->>'currentSequence')::int else 0 end),0), "
            + $"count(*) filter (where position('{ProtectedRawKeyPrefix}' in key || value::text) > 0) from s;")
            .ConfigureAwait(false);
        string[] fields = statistics.Trim().Split('|');
        if (fields.Length != 11)
        {
            throw new InvalidOperationException("The OQ8 PostgreSQL structural projection was incomplete.");
        }

        int[] counts = fields.Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        string schemaHash = HashUtf8(schema.Trim());
        string projection = string.Join('|', new[] { stage, schemaHash }.Concat(fields));
        return new Oq8PostgresqlSnapshot(
            stage,
            counts[0],
            counts[1],
            counts[2],
            counts[3],
            counts[4],
            counts[5],
            counts[6],
            counts[7],
            counts[8],
            counts[9],
            counts[10],
            schemaHash,
            HashUtf8(projection));
    }

    /// <summary>Adds one already-sanitized scenario observation to the opt-in evidence capture.</summary>
    /// <param name="name">The stable scenario name.</param>
    /// <param name="observation">The support-safe observation.</param>
    public void RecordEvidence(string name, object observation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(observation);
        string json = JsonSerializer.Serialize(observation, _jsonOptions);
        EnsureSanitized(json);
        using JsonDocument document = JsonDocument.Parse(json);
        _evidence[name] = document.RootElement.Clone();
    }

    /// <summary>Scans a proposed committed projection for any registered protected identifier.</summary>
    /// <param name="projection">The support-safe projection candidate.</param>
    /// <returns><see langword="true"/> when protected material remains.</returns>
    public bool ContainsProtectedIdentifiers(object projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        string json = JsonSerializer.Serialize(projection, _jsonOptions);
        lock (_sensitiveLock)
        {
            return _sensitiveValues
                .Where(static item => item.Length >= 6)
                .Any(value => json.Contains(value, StringComparison.Ordinal));
        }
    }

    /// <summary>Builds and scans the complete opt-in evidence preview without writing it.</summary>
    /// <returns>The SHA-256 of the sanitized preview.</returns>
    public async Task<string> ValidateEvidencePreviewAsync()
    {
        string preview = await BuildEvidenceJsonAsync().ConfigureAwait(false);
        EnsureSanitized(preview);
        return HashUtf8(preview);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await EmitEvidenceIfRequestedAsync().ConfigureAwait(false);
        }
        finally
        {
            await DisposeResourcesAsync().ConfigureAwait(false);
        }
    }

    private async Task StartPostgresAsync()
    {
        int postgresPort = GetAvailablePorts(1)[0];
        _postgresContainerName = $"eventstore-oq8-{Guid.NewGuid():N}";
        string password = $"oq8-{Guid.NewGuid():N}";
        RegisterSensitive(_postgresContainerName);
        RegisterSensitive(password);
        _postgresConnectionString =
            $"host=127.0.0.1 port={postgresPort} user=postgres password={password} dbname=eventstore sslmode=disable connect_timeout=10";
        RegisterSensitive(_postgresConnectionString);

        string containerId = await RunProcessAsync(
            "docker",
            [
                "run", "--rm", "-d",
                "--name", _postgresContainerName,
                "-e", $"POSTGRES_PASSWORD={password}",
                "-e", "POSTGRES_DB=eventstore",
                "-p", $"127.0.0.1:{postgresPort}:5432",
                PostgresImage,
            ]).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(containerId))
        {
            throw new InvalidOperationException("The OQ8 PostgreSQL container did not return an identity.");
        }

        Exception? lastError = null;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                _ = await RunProcessAsync(
                    "docker",
                    ["exec", _postgresContainerName, "pg_isready", "-h", "127.0.0.1", "-p", "5432", "-U", "postgres", "-d", "eventstore"])
                    .ConfigureAwait(false);
                return;
            }
            catch (Exception exception)
            {
                lastError = exception;
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("The OQ8 PostgreSQL container did not become ready.", lastError);
    }

    private void CreateProductionProfileResources()
    {
        string trackedStateStore = File.ReadAllText(
            Path.Combine(_repositoryRoot, "deploy", "dapr", "statestore-postgresql.yaml"));
        const string ConnectionPlaceholder = "{env:POSTGRES_CONNECTION_STRING}";
        if (!trackedStateStore.Contains(ConnectionPlaceholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The tracked PostgreSQL component no longer exposes its connection placeholder.");
        }

        File.WriteAllText(
            Path.Combine(_componentsDirectory, "statestore-postgresql.yaml"),
            trackedStateStore.Replace(ConnectionPlaceholder, _postgresConnectionString, StringComparison.Ordinal));
        File.Copy(
            Path.Combine(_repositoryRoot, "deploy", "dapr", "resiliency.yaml"),
            Path.Combine(_componentsDirectory, "resiliency.yaml"));
        File.WriteAllText(
            Path.Combine(_componentsDirectory, "pubsub.yaml"),
            """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "localhost:6379"
                - name: redisPassword
                  value: ""
            scopes:
              - eventstore
            """);
    }

    private void PrepareShadowApplications()
    {
        string eventStoreOutput = Path.Combine(
            _repositoryRoot,
            "src",
            "Hexalith.EventStore",
            "bin",
            "Release",
            "net10.0");
        string sampleOutput = Path.Combine(
            _repositoryRoot,
            "samples",
            "Hexalith.EventStore.Sample",
            "bin",
            "Release",
            "net10.0");
        string eventStoreShadow = Path.Combine(_runtimeDirectory, "eventstore");
        string sampleShadow = Path.Combine(_runtimeDirectory, "sample");
        CopyDirectory(eventStoreOutput, eventStoreShadow);
        CopyDirectory(sampleOutput, sampleShadow);
        CopyHostingStartupDependencies(eventStoreShadow);
        CopyHostingStartupDependencies(sampleShadow);

        foreach (Oq8ProcessNode node in _eventStoreNodes)
        {
            node.ApplicationDirectory = eventStoreShadow;
            node.ApplicationAssembly = Path.Combine(eventStoreShadow, "Hexalith.EventStore.dll");
        }

        _sampleNode.ApplicationDirectory = sampleShadow;
        _sampleNode.ApplicationAssembly = Path.Combine(sampleShadow, "Hexalith.EventStore.Sample.dll");
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new InvalidOperationException("A required Release application output is absent; build the OQ8 test project first.");
        }

        _ = Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void CopyHostingStartupDependencies(string destination)
    {
        foreach (string file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            string target = Path.Combine(destination, Path.GetFileName(file));
            if (!File.Exists(target))
            {
                File.Copy(file, target);
            }
        }
    }

    private void AllocatePortsAndCounters()
    {
        Oq8ProcessNode[] nodes = [.. AllNodes()];
        int[] ports = GetAvailablePorts(nodes.Length * 6);
        for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
        {
            Oq8ProcessNode node = nodes[nodeIndex];
            int offset = nodeIndex * 6;
            node.AppPort = ports[offset];
            node.DaprHttpPort = ports[offset + 1];
            node.DaprGrpcPort = ports[offset + 2];
            node.DaprInternalGrpcPort = ports[offset + 3];
            node.DaprMetricsPort = ports[offset + 4];
            node.DaprProfilePort = ports[offset + 5];
            node.CounterFile = Path.Combine(_runtimeDirectory, $"{node.Name}-boundary-count.txt");
            File.WriteAllText(node.CounterFile, "0");
        }
    }

    private async Task StartNodeAsync(Oq8ProcessNode node)
    {
        if (node.Application is not null || node.Sidecar is not null)
        {
            throw new InvalidOperationException($"OQ8 node {node.Name} is already running.");
        }

        StartApplication(node);
        StartSidecar(node);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(HealthTimeoutSeconds);
        await WaitForSidecarHealthAsync(node, deadline).ConfigureAwait(false);
        await WaitForApplicationHealthAsync(node, deadline).ConfigureAwait(false);
        if (node.IsEventStore)
        {
            await WaitForActorRuntimeReadinessAsync(node, deadline).ConfigureAwait(false);
        }
    }

    private void StartApplication(Oq8ProcessNode node)
    {
        string testDependencies = Path.Combine(
            AppContext.BaseDirectory,
            "Hexalith.EventStore.Server.LiveSidecar.Tests.deps.json");
        var startInfo = CreateRedirectedProcessStartInfo("dotnet");
        startInfo.WorkingDirectory = node.ApplicationDirectory;
        foreach (string argument in new[]
        {
            "exec",
            "--additional-deps", testDependencies,
            "--additionalprobingpath", AppContext.BaseDirectory,
            "--additionalprobingpath", ResolveNuGetPackageCache(),
            node.ApplicationAssembly,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] =
            "Hexalith.EventStore.Server.LiveSidecar.Tests";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        startInfo.Environment["ASPNETCORE_URLS"] = node.ApplicationEndpoint;
        startInfo.Environment["DAPR_APP_ID"] = node.AppId;
        startInfo.Environment["DAPR_HTTP_PORT"] = node.DaprHttpPort.ToString(CultureInfo.InvariantCulture);
        startInfo.Environment["DAPR_GRPC_PORT"] = node.DaprGrpcPort.ToString(CultureInfo.InvariantCulture);
        startInfo.Environment["DAPR_HTTP_ENDPOINT"] = node.DaprHttpEndpoint;
        startInfo.Environment["DAPR_GRPC_ENDPOINT"] = $"http://127.0.0.1:{node.DaprGrpcPort}";
        startInfo.Environment["POSTGRES_CONNECTION_STRING"] = _postgresConnectionString;
        startInfo.Environment[Oq8FileTimeProvider.ClockFileEnvironmentVariable] = _clockFile;
        startInfo.Environment[Oq8BoundaryCounterStartupFilter.CounterFileEnvironmentVariable] = node.CounterFile;
        startInfo.Environment["Logging__LogLevel__Default"] = "Warning";
        startInfo.Environment["Logging__LogLevel__Microsoft_AspNetCore"] = "Warning";
        if (node.IsEventStore)
        {
            ConfigureEventStoreEnvironment(startInfo.Environment);
        }

        node.Application = StartCapturedProcess(
            startInfo,
            node.ApplicationOutput,
            node.ApplicationError);
    }

    private void ConfigureEventStoreEnvironment(IDictionary<string, string?> environment)
    {
        environment["Authentication__JwtBearer__Issuer"] = AuthenticationIssuer;
        environment["Authentication__JwtBearer__Audience"] = AuthenticationAudience;
        environment["Authentication__JwtBearer__SigningKey"] = AuthenticationSigningKey;
        environment["Authentication__JwtBearer__RequireHttpsMetadata"] = "false";
        environment["Authentication__JwtBearer__AllowInsecureSymmetricKey"] = "true";
        environment["EventStore__Actors__AggregateActorTypeName"] = AggregateActorTypeName;
        environment["EventStore__ProjectionDispatch__RetryWorkerInterval"] = "00:10:00";
        environment["EventStore__IdempotencyAdmission__Enabled"] = "true";
        environment["EventStore__IdempotencyAdmission__ActiveDigestKeyVersion"] = _activeDigestVersion;
        environment["EventStore__IdempotencyAdmission__DigestKeySource"] = "Configuration";
        environment[$"EventStore__IdempotencyAdmission__DigestKeys__{_activeDigestVersion}"] =
            _activeDigestVersion == ActiveVersionOne ? VersionOneKey : VersionTwoKey;
        for (int index = 0; index < _readerDigestVersions.Length; index++)
        {
            string readerVersion = _readerDigestVersions[index];
            environment[$"EventStore__IdempotencyAdmission__ReaderDigestKeyVersions__{index}"] = readerVersion;
            environment[$"EventStore__IdempotencyAdmission__DigestKeys__{readerVersion}"] =
                readerVersion == ActiveVersionOne ? VersionOneKey : VersionTwoKey;
        }

        environment["EventStore__DomainServices__Registrations__*|counter|v1__AppId"] = SampleAppId;
        environment["EventStore__DomainServices__Registrations__*|counter|v1__MethodName"] = "process";
        environment["EventStore__DomainServices__Registrations__*|counter|v1__TenantId"] = "*";
        environment["EventStore__DomainServices__Registrations__*|counter|v1__Domain"] = "counter";
        environment["EventStore__DomainServices__Registrations__*|counter|v1__Version"] = "v1";
        environment["EventStore__DomainServices__Registrations__system|tenants|v1__AppId"] = SampleAppId;
        environment["EventStore__DomainServices__Registrations__system|global-administrators|v1__AppId"] = SampleAppId;
        environment["EventStore__OpenApi__Enabled"] = "false";
        environment["EventStore__SignalR__Enabled"] = "false";
    }

    private void StartSidecar(Oq8ProcessNode node)
    {
        var startInfo = CreateRedirectedProcessStartInfo(ResolveDaprdPath());
        foreach (string argument in new[]
        {
            "--app-id", node.AppId,
            "--app-port", node.AppPort.ToString(CultureInfo.InvariantCulture),
            "--app-protocol", "http",
            "--app-channel-address", "127.0.0.1",
            "--dapr-http-port", node.DaprHttpPort.ToString(CultureInfo.InvariantCulture),
            "--dapr-grpc-port", node.DaprGrpcPort.ToString(CultureInfo.InvariantCulture),
            "--dapr-internal-grpc-port", node.DaprInternalGrpcPort.ToString(CultureInfo.InvariantCulture),
            "--metrics-port", node.DaprMetricsPort.ToString(CultureInfo.InvariantCulture),
            "--profile-port", node.DaprProfilePort.ToString(CultureInfo.InvariantCulture),
            "--resources-path", _componentsDirectory,
            "--log-level", "warn",
            "--placement-host-address", FormatControlPlaneEndpoint(_placementHostEndpoint, "placement"),
            "--scheduler-host-address", FormatControlPlaneEndpoint(_schedulerHostEndpoint, "scheduler"),
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["POSTGRES_CONNECTION_STRING"] = _postgresConnectionString;
        node.Sidecar = StartCapturedProcess(startInfo, node.SidecarOutput, node.SidecarError);
    }

    private static ProcessStartInfo CreateRedirectedProcessStartInfo(string fileName)
        => new()
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

    private static Process StartCapturedProcess(
        ProcessStartInfo startInfo,
        Oq8BoundedLog output,
        Oq8BoundedLog error)
    {
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, eventArgs) => output.Append(eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => error.Append(eventArgs.Data);
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("An OQ8 child process could not be started.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private async Task WaitForSidecarHealthAsync(Oq8ProcessNode node, DateTimeOffset deadline)
    {
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        string health = $"{node.DaprHttpEndpoint}/v1.0/healthz/outbound";
        while (DateTimeOffset.UtcNow < deadline)
        {
            ThrowIfExited(node);
            try
            {
                using var cancellation = new CancellationTokenSource(GetRemainingTimeout(deadline, TimeSpan.FromSeconds(2)));
                using HttpResponseMessage response = await client
                    .GetAsync(health, cancellation.Token)
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await DelayWithinDeadlineAsync(deadline, TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"OQ8 sidecar health timed out. {Diagnostics(node)}");
    }

    private async Task WaitForApplicationHealthAsync(Oq8ProcessNode node, DateTimeOffset deadline)
    {
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        while (DateTimeOffset.UtcNow < deadline)
        {
            ThrowIfExited(node);
            try
            {
                using var cancellation = new CancellationTokenSource(GetRemainingTimeout(deadline, TimeSpan.FromSeconds(2)));
                using HttpResponseMessage response = await client
                    .GetAsync($"{node.ApplicationEndpoint}/alive", cancellation.Token)
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await DelayWithinDeadlineAsync(deadline, TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"OQ8 application health timed out. {Diagnostics(node)}");
    }

    private async Task WaitForActorRuntimeReadinessAsync(Oq8ProcessNode node, DateTimeOffset deadline)
    {
        string[] requiredActorTypes =
        [
            AggregateActorTypeName,
            IdempotencyAdmissionActor.ActorTypeName,
            IdempotencyAdmissionDirectoryActor.ActorTypeName,
            IdempotencyTenantLifecycleActor.ActorTypeName,
        ];
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        while (DateTimeOffset.UtcNow < deadline)
        {
            ThrowIfExited(node);
            try
            {
                using var cancellation = new CancellationTokenSource(GetRemainingTimeout(deadline, TimeSpan.FromSeconds(2)));
                using HttpResponseMessage metadataResponse = await client
                    .GetAsync($"{node.DaprHttpEndpoint}/v1.0/metadata", cancellation.Token)
                    .ConfigureAwait(false);
                using HttpResponseMessage configResponse = await client
                    .GetAsync($"{node.ApplicationEndpoint}/dapr/config", cancellation.Token)
                    .ConfigureAwait(false);
                if (metadataResponse.IsSuccessStatusCode && configResponse.IsSuccessStatusCode)
                {
                    using JsonDocument metadata = JsonDocument.Parse(
                        await metadataResponse.Content.ReadAsStringAsync(cancellation.Token).ConfigureAwait(false));
                    using JsonDocument config = JsonDocument.Parse(
                        await configResponse.Content.ReadAsStringAsync(cancellation.Token).ConfigureAwait(false));
                    if (IsActorRuntimeReady(metadata.RootElement)
                        && ExposesRequiredActorTypes(config.RootElement, requiredActorTypes))
                    {
                        return;
                    }
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
            catch (JsonException)
            {
            }

            await DelayWithinDeadlineAsync(deadline, TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"OQ8 actor runtime readiness timed out. {Diagnostics(node)}");
    }

    private static bool IsActorRuntimeReady(JsonElement metadata)
        => metadata.TryGetProperty("actorRuntime", out JsonElement actorRuntime)
            && actorRuntime.ValueKind == JsonValueKind.Object
            && actorRuntime.TryGetProperty("runtimeStatus", out JsonElement runtimeStatus)
            && string.Equals(runtimeStatus.GetString(), "RUNNING", StringComparison.OrdinalIgnoreCase)
            && actorRuntime.TryGetProperty("hostReady", out JsonElement hostReady)
            && hostReady.ValueKind == JsonValueKind.True;

    private static bool ExposesRequiredActorTypes(JsonElement config, IReadOnlyCollection<string> requiredActorTypes)
    {
        if (!config.TryGetProperty("entities", out JsonElement entities)
            || entities.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var exposed = entities.EnumerateArray()
            .Where(static entity => entity.ValueKind == JsonValueKind.String)
            .Select(static entity => entity.GetString())
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        return requiredActorTypes.All(exposed.Contains);
    }

    private static TimeSpan GetRemainingTimeout(DateTimeOffset deadline, TimeSpan maximum)
    {
        TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
        return remaining <= TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(1)
            : remaining < maximum ? remaining : maximum;
    }

    private static Task DelayWithinDeadlineAsync(DateTimeOffset deadline, TimeSpan maximum)
    {
        TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
        return remaining <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(remaining < maximum ? remaining : maximum);
    }

    private void ThrowIfExited(Oq8ProcessNode node)
    {
        if (node.Application?.HasExited == true || node.Sidecar?.HasExited == true)
        {
            throw new InvalidOperationException($"An OQ8 child process exited early. {Diagnostics(node)}");
        }
    }

    private string Diagnostics(Oq8ProcessNode node)
        => Sanitize(
            $"Node={node.Name}; AppOut={node.ApplicationOutput}; AppErr={node.ApplicationError}; "
            + $"SidecarOut={node.SidecarOutput}; SidecarErr={node.SidecarError}");

    private static async Task<int> GetHostedActorCountAsync(Oq8ProcessNode node, string actorType)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using HttpResponseMessage response = await client
            .GetAsync($"{node.DaprHttpEndpoint}/v1.0/metadata")
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        using JsonDocument metadata = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        if (!metadata.RootElement.TryGetProperty("actors", out JsonElement actors)
            || actors.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        foreach (JsonElement actor in actors.EnumerateArray())
        {
            if (actor.TryGetProperty("type", out JsonElement type)
                && string.Equals(type.GetString(), actorType, StringComparison.Ordinal)
                && actor.TryGetProperty("count", out JsonElement count)
                && count.TryGetInt32(out int hostedCount))
            {
                return hostedCount;
            }
        }

        return 0;
    }

    private TActor CreateActorProxy<TActor>(string actorId, string actorType)
        where TActor : Dapr.Actors.IActor
    {
        Oq8ProcessNode node = _eventStoreNodes.First(static candidate => candidate.Sidecar is not null);
        return new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = node.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(20),
        }).CreateActorProxy<TActor>(new ActorId(actorId), actorType);
    }

    private Oq8ProcessNode GetRunningEventStoreNode(int index)
    {
        Oq8ProcessNode node = _eventStoreNodes[index];
        return node.Application is not null && node.Sidecar is not null
            ? node
            : throw new InvalidOperationException($"OQ8 EventStore node {index + 1} is not running.");
    }

    private async Task<string> RunPostgresQueryAsync(string query)
        => await RunProcessAsync(
            "docker",
            ["exec", _postgresContainerName, "psql", "-U", "postgres", "-d", "eventstore", "-At", "-F", "|", "-c", query])
            .ConfigureAwait(false);

    private static string BuildActorId(string tenant, string digestVersion, string rawKey)
    {
        string encodedKey = digestVersion switch
        {
            ActiveVersionOne => VersionOneKey,
            ActiveVersionTwo => VersionTwoKey,
            _ => throw new InvalidOperationException("The requested OQ8 digest version is unavailable."),
        };
        byte[] master = Convert.FromBase64String(encodedKey);
        byte[] tenantBytes = Encoding.UTF8.GetBytes(tenant);
        byte[] rawBytes = Encoding.UTF8.GetBytes(rawKey);
        byte[]? tenantKey = null;
        byte[]? digest = null;
        try
        {
            tenantKey = ComputeHmac(master, "hexalith-eventstore-idempotency-tenant-v1\0"u8, tenantBytes);
            digest = ComputeHmac(tenantKey, "key-partition-v1\0"u8, rawBytes);
            string keyDigest = Convert.ToBase64String(digest).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"{tenant}:{digestVersion}:{keyDigest}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(master);
            CryptographicOperations.ZeroMemory(tenantBytes);
            CryptographicOperations.ZeroMemory(rawBytes);
            if (tenantKey is not null)
            {
                CryptographicOperations.ZeroMemory(tenantKey);
            }

            if (digest is not null)
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
    }

    private static IdempotencyAdmissionDirectoryAlias CreateDirectoryAlias(
        string tenant,
        string digestVersion,
        string rawKey)
    {
        string actorId = BuildActorId(tenant, digestVersion, rawKey);
        return new IdempotencyAdmissionDirectoryAlias(
            digestVersion,
            actorId,
            actorId[(actorId.LastIndexOf(':') + 1)..]);
    }

    private static byte[] ComputeHmac(byte[] key, ReadOnlySpan<byte> domain, byte[] value)
    {
        byte[] input = new byte[domain.Length + value.Length];
        domain.CopyTo(input);
        Buffer.BlockCopy(value, 0, input, domain.Length, value.Length);
        byte[] result = HMACSHA256.HashData(key, input);
        CryptographicOperations.ZeroMemory(input);
        return result;
    }

    private static string CreateBearerToken()
    {
        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = "oq8-evidence-runner",
            iss = AuthenticationIssuer,
            aud = AuthenticationAudience,
            exp = new DateTimeOffset(2035, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
            tenants = new[] { "tenant-oq8" },
            tenant_id = "tenant-oq8-governance",
            domains = new[] { "counter" },
            permissions = new[] { "commands:*" },
        }));
        string unsigned = $"{header}.{payload}";
        byte[] signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(AuthenticationSigningKey),
            Encoding.UTF8.GetBytes(unsigned));
        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<string> BuildEvidenceJsonAsync()
    {
        JsonElement diagnostics = ScanBoundedDiagnostics();
        string eventStoreAssembly = _eventStoreNodes[0].ApplicationAssembly;
        string sampleAssembly = _sampleNode.ApplicationAssembly;
        string hostingStartupAssembly = Path.Combine(
            _eventStoreNodes[0].ApplicationDirectory,
            "Hexalith.EventStore.Server.LiveSidecar.Tests.dll");
        string additionalDependencies = Path.Combine(
            AppContext.BaseDirectory,
            "Hexalith.EventStore.Server.LiveSidecar.Tests.deps.json");
        string postgresIdentity = await RunProcessAsync(
            "docker",
            ["image", "inspect", PostgresImage, "--format", "{{.Id}}"])
            .ConfigureAwait(false);
        string daprVersion = await RunProcessAsync(ResolveDaprdPath(), ["--version"]).ConfigureAwait(false);
        var document = new
        {
            schemaVersion = 1,
            captureKind = "release-entry-binaries-test-seams-sidecar-postgresql",
            capturedOn = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            topology = new
            {
                eventStoreProcessCount = 2,
                eventStoreSidecarCount = 2,
                sampleProcessCount = 1,
                sampleSidecarCount = 1,
                independentProcessIdentities = HasIndependentProcesses,
            },
            profile = new
            {
                name = "oq8-postgresql-v1",
                stateStoreType = "state.postgresql",
                stateComponentSha256 = HashFile(Path.Combine(_repositoryRoot, "deploy", "dapr", "statestore-postgresql.yaml")),
                resiliencySha256 = HashFile(Path.Combine(_repositoryRoot, "deploy", "dapr", "resiliency.yaml")),
            },
            runtime = new
            {
                dotnet = Environment.Version.ToString(),
                dapr = daprVersion.Trim(),
                postgresImage = PostgresImage,
                postgresImageIdentity = postgresIdentity.Trim(),
            },
            executionConfiguration = new
            {
                shippedReleaseEntryAssemblies = true,
                shadowCopiedBeforeLaunch = true,
                environmentName = "Testing",
                testOnlyHostingStartup = true,
                productionConfigurationUntouched = false,
                seams = new[]
                {
                    "deterministic-time",
                    "idempotency-intent-adapter",
                    "boundary-counter",
                },
            },
            artifacts = new
            {
                eventStoreSha256 = HashFile(eventStoreAssembly),
                sampleSha256 = HashFile(sampleAssembly),
                eventStoreRuntimeSetSha256 = HashRuntimeDependencySet(_eventStoreNodes[0].ApplicationDirectory),
                sampleRuntimeSetSha256 = HashRuntimeDependencySet(_sampleNode.ApplicationDirectory),
                hostingStartupSha256 = HashFile(hostingStartupAssembly),
                additionalDepsSha256 = HashFile(additionalDependencies),
            },
            diagnostics,
            observations = _evidence,
        };
        return JsonSerializer.Serialize(document, _jsonOptions);
    }

    private async Task EmitEvidenceIfRequestedAsync()
    {
        string? evidenceDirectory = Environment.GetEnvironmentVariable("HEXALITH_OQ8_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(evidenceDirectory))
        {
            return;
        }

        string json = await BuildEvidenceJsonAsync().ConfigureAwait(false);
        EnsureSanitized(json);
        _ = Directory.CreateDirectory(evidenceDirectory);
        string target = Path.Combine(evidenceDirectory, "observations.json");
        string temporary = target + ".tmp";
        await File.WriteAllTextAsync(temporary, json).ConfigureAwait(false);
        File.Move(temporary, target, overwrite: true);
    }

    private void EnsureSanitized(string value)
    {
        lock (_sensitiveLock)
        {
            foreach (string sensitive in _sensitiveValues.Where(static item => item.Length >= 6))
            {
                if (value.Contains(sensitive, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("OQ8 evidence retained a protected input or private identifier.");
                }
            }
        }

        foreach (string forbidden in new[]
        {
            ProtectedRawKeyPrefix,
            AuthenticationSigningKey,
            VersionOneKey,
            VersionTwoKey,
            "password=",
            "POSTGRES_PASSWORD",
            "Bearer ",
            "eyJhbGci",
        })
        {
            if (value.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("OQ8 evidence retained a forbidden protected-data class.");
            }
        }

        if (value.Contains("/home/", StringComparison.Ordinal)
            || value.Contains("/Users/", StringComparison.Ordinal)
            || value.Contains(":\\Users\\", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Folders OQ8 closed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("OQ8 evidence retained a private path or forbidden closure claim.");
        }
    }

    private void RegisterSensitive(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        lock (_sensitiveLock)
        {
            _ = _sensitiveValues.Add(value);
        }
    }

    private void RegisterSensitiveTestMaterial()
    {
        RegisterSensitive(AuthenticationSigningKey);
        RegisterSensitive(VersionOneKey);
        RegisterSensitive(VersionTwoKey);
        RegisterSensitive(AuthenticationIssuer);
        RegisterSensitive(AuthenticationAudience);
        RegisterSensitive("oq8-evidence-runner");
    }

    private JsonElement ScanBoundedDiagnostics()
    {
        string[] streams = [.. AllNodes().SelectMany(static node => new[]
        {
            node.ApplicationOutput.ToString(),
            node.ApplicationError.ToString(),
            node.SidecarOutput.ToString(),
            node.SidecarError.ToString(),
        })];
        string[] sanitizedHashes = [.. streams.Select(value =>
        {
            string sanitized = Sanitize(value);
            EnsureSanitized(sanitized);
            return HashUtf8(sanitized);
        })];
        return JsonSerializer.SerializeToElement(new
        {
            streamsScanned = streams.Length,
            boundedCharacterLimitPerStream = Oq8BoundedLog.MaximumCharacters,
            forbiddenTermClassesScanned = new[]
            {
                "protected-input",
                "protected-result",
                "request-identifier",
                "test-key-material",
                "bearer-token",
                "database-credential",
                "private-path",
            },
            postRedactionProtectedMatches = 0,
            rawDiagnosticsCommitted = false,
            sanitizedProjectionSha256 = HashUtf8(string.Join('|', sanitizedHashes)),
        }, _jsonOptions);
    }

    private string Sanitize(string value)
    {
        string sanitized = value;
        lock (_sensitiveLock)
        {
            foreach (string sensitive in _sensitiveValues.OrderByDescending(static item => item.Length))
            {
                sanitized = sanitized.Replace(sensitive, "[redacted]", StringComparison.Ordinal);
            }
        }

        return sanitized.Replace(_repositoryRoot, "[workspace]", StringComparison.Ordinal);
    }

    private async Task DisposeResourcesAsync()
    {
        foreach (Oq8ProcessNode node in AllNodes())
        {
            try
            {
                await StopNodeAsync(node).ConfigureAwait(false);
            }
            catch
            {
                // Fixture-owned process cleanup must continue through every node and resource.
            }
        }

        if (!string.IsNullOrWhiteSpace(_postgresContainerName))
        {
            try
            {
                _ = await RunProcessAsync("docker", ["rm", "-f", _postgresContainerName]).ConfigureAwait(false);
            }
            catch
            {
                // Exact-container cleanup is best effort so the original failure remains visible.
            }

            _postgresContainerName = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(_runtimeDirectory) && Directory.Exists(_runtimeDirectory))
        {
            try
            {
                Directory.Delete(_runtimeDirectory, recursive: true);
            }
            catch
            {
                // Runtime scratch cleanup is best effort after all owned processes are stopped.
            }
        }
    }

    private static async Task StopNodeAsync(Oq8ProcessNode node)
    {
        await StopProcessAsync(node.Application).ConfigureAwait(false);
        node.Application = null;
        await StopProcessAsync(node.Sidecar).ConfigureAwait(false);
        node.Sidecar = null;
    }

    private static async Task StopProcessAsync(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // The exact fixture-owned process has already received its final best-effort kill.
            }

            process.Dispose();
        }
    }

    private IEnumerable<Oq8ProcessNode> AllNodes()
        => _eventStoreNodes.Append(_sampleNode);

    private static int ReadCounter(string path)
        => string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? 0
            : int.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture);

    private async Task VerifyPrerequisitesAsync()
    {
        _placementHostEndpoint = await ResolveDockerPublishedHostEndpointAsync(
            "dapr_placement",
            PlacementContainerPort).ConfigureAwait(false);
        _schedulerHostEndpoint = await ResolveDockerPublishedHostEndpointAsync(
            "dapr_scheduler",
            SchedulerContainerPort).ConfigureAwait(false);

        foreach ((IPEndPoint Endpoint, string Name) prerequisite in new[]
        {
            (GetConnectEndpoint(_placementHostEndpoint, "placement"), "Dapr placement"),
            (GetConnectEndpoint(_schedulerHostEndpoint, "scheduler"), "Dapr scheduler"),
        })
        {
            using var client = new TcpClient();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await client.ConnectAsync(prerequisite.Endpoint.Address, prerequisite.Endpoint.Port, cancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"{prerequisite.Name} is unavailable on its required local control-plane port.",
                    exception);
            }
        }

        _ = await RunProcessAsync("docker", ["image", "inspect", PostgresImage]).ConfigureAwait(false);
    }

    private static async Task<IPEndPoint> ResolveDockerPublishedHostEndpointAsync(
        string containerName,
        int containerPort)
    {
        string output = await RunProcessAsync(
            "docker",
            ["port", containerName, $"{containerPort.ToString(CultureInfo.InvariantCulture)}/tcp"])
            .ConfigureAwait(false);
        return DockerPublishedPortResolver.ParseHostEndpoint(output, containerName, containerPort);
    }

    private static string FormatControlPlaneEndpoint(IPEndPoint? endpoint, string serviceName)
        => GetConnectEndpoint(endpoint, serviceName).ToString();

    private static IPEndPoint GetConnectEndpoint(IPEndPoint? endpoint, string serviceName)
    {
        if (endpoint is null)
        {
            throw new InvalidOperationException(
                $"The Docker-published Dapr {serviceName} host endpoint was not resolved before sidecar startup.");
        }

        IPAddress address = endpoint.Address.Equals(IPAddress.Any)
            ? IPAddress.Loopback
            : endpoint.Address.Equals(IPAddress.IPv6Any)
                ? IPAddress.IPv6Loopback
                : endpoint.Address;
        return new IPEndPoint(address, endpoint.Port);
    }

    private static async Task<string> RunProcessAsync(string fileName, IReadOnlyList<string> arguments)
    {
        ProcessStartInfo startInfo = CreateRedirectedProcessStartInfo(fileName);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        _ = process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException(
                $"Required process '{Path.GetFileName(fileName)}' exceeded its 60-second limit.");
        }

        string output = await stdout.ConfigureAwait(false);
        string error = await stderr.ConfigureAwait(false);
        return process.ExitCode == 0
            ? output.Trim()
            : throw new InvalidOperationException(
                $"Required process '{Path.GetFileName(fileName)}' exited with code {process.ExitCode}; "
                + $"stderrSha256={HashUtf8(error.Trim())}.");
    }

    private static string ResolveDaprdPath()
    {
        string candidate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dapr",
            "bin",
            "daprd" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
        return File.Exists(candidate)
            ? candidate
            : OperatingSystem.IsWindows() ? "daprd.exe" : "daprd";
    }

    private static string ResolveNuGetPackageCache()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        string candidate = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages")
            : Path.GetFullPath(configuredPath);
        return Directory.Exists(candidate)
            ? candidate
            : throw new InvalidOperationException(
                "The NuGet package cache required by OQ8 hosting-startup dependencies is unavailable.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The EventStore repository root could not be located.");
    }

    private static int[] GetAvailablePorts(int count)
    {
        var listeners = new TcpListener[count];
        int[] ports = new int[count];
        for (int index = 0; index < count; index++)
        {
            listeners[index] = new TcpListener(IPAddress.Loopback, 0);
            listeners[index].Start();
            ports[index] = ((IPEndPoint)listeners[index].LocalEndpoint).Port;
        }

        foreach (TcpListener listener in listeners)
        {
            listener.Stop();
        }

        return ports;
    }

    private static string HashUtf8(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string HashResultPayload(string? value)
    {
        if (value is null)
        {
            return HashUtf8("null");
        }

        using JsonDocument document = JsonDocument.Parse(value);
        return HashUtf8(document.RootElement.GetRawText());
    }

    private static string HashFile(string path)
        => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static string HashRuntimeDependencySet(string directory)
    {
        string[] identities =
        [
            .. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => $"{Path.GetRelativePath(directory, path).Replace('\\', '/')}:{HashFile(path)}")
                .Order(StringComparer.Ordinal),
        ];
        return HashUtf8(string.Join('\n', identities) + "\n");
    }
}
