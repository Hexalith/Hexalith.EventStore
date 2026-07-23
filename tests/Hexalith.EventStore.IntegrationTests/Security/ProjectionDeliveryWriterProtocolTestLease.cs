using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>
/// Isolates, activates, and restores the store-global writer-protocol marker for a disposable topology.
/// </summary>
internal sealed class ProjectionDeliveryWriterProtocolTestLease {
    private const string HealthCheckName = "projection-delivery-writer-protocol";
    private const string IsolationLockKey = "projection-delivery-writer-protocol:test-isolation-lock";
    private const string MarkerKey = "projection-delivery-writer-protocol";
    private const string RedisEndpoint = "localhost:6379";
    private const int SchemaVersion = 1;
    private const int WriterProtocolVersion = 2;

    private static readonly TimeSpan s_lockLease = TimeSpan.FromHours(2);
    private static readonly TimeSpan s_retryDelay = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.Web);

    private byte[]? _markerSnapshot;
    private TimeSpan? _markerSnapshotExpiry;
    private string? _sourceCommit;
    private string? _lockToken;
    private bool _isActive;

    /// <summary>Resolves the exact Git commit containing the running test code.</summary>
    internal static async Task<string> ReadExactSourceCommitAsync(CancellationToken cancellationToken) {
        var startInfo = new ProcessStartInfo {
            FileName = "git",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("HEAD");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git for exact source identity resolution.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string commit = (await standardOutput.ConfigureAwait(false)).Trim();
        string error = (await standardError.ConfigureAwait(false)).Trim();
        if (process.ExitCode != 0
            || commit.Length != 40
            || commit.Any(static character => !Uri.IsHexDigit(character))) {
            throw new InvalidOperationException(
                $"Unable to resolve the exact runtime source commit. ExitCode={process.ExitCode}; "
                + $"Output={commit}; Error={error}");
        }

        return commit.ToLowerInvariant();
    }

    /// <summary>Returns whether the activation response can be retried safely.</summary>
    internal static bool ShouldRetryActivationResponse(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.OK
            || statusCode == HttpStatusCode.Conflict
            || ProjectionDeliveryWriterProtocolCutoverPolicy.IsTransientActivationStatus(statusCode);

    /// <summary>
    /// Takes exclusive ownership of the global marker, activates it through the production API,
    /// and verifies that the persisted marker identifies the exact test commit.
    /// </summary>
    internal async Task ActivateAsync(
        HttpClient eventStoreClient,
        string administratorToken,
        string sourceCommit,
        string backupReference,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(eventStoreClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(administratorToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCommit);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupReference);

        using IConnectionMultiplexer redis = await ConnectRedisAsync().ConfigureAwait(false);
        IDatabase database = redis.GetDatabase();
        await IsolateMarkerAsync(database, sourceCommit, cancellationToken).ConfigureAwait(false);
        string lastDiagnostic = "No activation attempt completed.";

        try {
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();

                try {
                    WriterProtocolMarker? marker = await ReadMarkerAsync(database, cancellationToken)
                        .ConfigureAwait(false);
                    if (marker is not null) {
                        AssertMarker(marker, sourceCommit);
                        return;
                    }
                }
                catch (RedisException ex) {
                    lastDiagnostic = $"Redis marker read is not ready: {ex.Message}";
                    await Task.Delay(s_retryDelay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                try {
                    using HttpResponseMessage health = await eventStoreClient
                        .GetAsync("/health", cancellationToken)
                        .ConfigureAwait(false);
                    string healthBody = await health.Content
                        .ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (!ProjectionDeliveryWriterProtocolCutoverPolicy.WriterProtocolIsOnlyUnhealthyCheck(
                        healthBody,
                        HealthCheckName)) {
                        lastDiagnostic = "EventStore dependencies are not ready for cutover. "
                            + $"Status={(int)health.StatusCode} ({health.StatusCode}); Body={healthBody}";
                        await Task.Delay(s_retryDelay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    using var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        "/api/v1/admin/projections/delivery-writer-protocol/activate");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administratorToken);
                    request.Content = JsonContent.Create(new {
                        CutoverCommit = sourceCommit,
                        BackupReference = backupReference,
                        WritersQuiesced = true,
                        RetryWorkersQuiesced = true,
                        DowngradeProhibitedAcknowledged = true,
                    });

                    using HttpResponseMessage response = await eventStoreClient
                        .SendAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                    string body = await response.Content
                        .ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (ShouldRetryActivationResponse(response.StatusCode)) {
                        lastDiagnostic = "Activation has not produced a verified marker yet. "
                            + $"Status={(int)response.StatusCode} ({response.StatusCode}); Body={body}";
                        await Task.Delay(s_retryDelay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new InvalidOperationException(
                        "Unable to activate the disposable topology's projection delivery writer protocol. "
                        + $"Status={(int)response.StatusCode} ({response.StatusCode}); Body={body}");
                }
                catch (HttpRequestException ex) {
                    lastDiagnostic = $"EventStore cutover transport is not ready: {ex.Message}";
                    await Task.Delay(s_retryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested) {
                    lastDiagnostic = $"EventStore cutover request timed out before the startup budget elapsed: {ex.Message}";
                    await Task.Delay(s_retryDelay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested) {
            throw new TimeoutException(
                "Projection delivery writer-protocol activation did not complete within the topology startup budget. "
                + lastDiagnostic,
                ex);
        }
        finally {
            await redis.CloseAsync(allowCommandsToComplete: true).ConfigureAwait(false);
        }
    }

    /// <summary>Atomically restores the marker state captured before activation.</summary>
    internal async Task RestoreAsync() {
        if (!_isActive) {
            return;
        }

        using IConnectionMultiplexer redis = await ConnectRedisAsync().ConfigureAwait(false);
        IDatabase database = redis.GetDatabase();
        string lockToken = _lockToken
            ?? throw new InvalidOperationException("The writer-protocol marker isolation lease is missing.");
        bool leaseExtended = await database.LockExtendAsync(IsolationLockKey, lockToken, s_lockLease)
            .ConfigureAwait(false);
        if (!leaseExtended) {
            throw new InvalidOperationException(
                "The writer-protocol marker isolation lease expired before restoration.");
        }

        try {
            RedisValue currentPayload = await database.HashGetAsync(MarkerKey, "data").ConfigureAwait(false);
            WriterProtocolMarker? current = DeserializeMarker(currentPayload);
            if (current is not null
                && !string.Equals(current.CutoverCommit, _sourceCommit, StringComparison.Ordinal)) {
                throw new InvalidOperationException(
                    "The store-global writer-protocol marker changed after fixture isolation; "
                    + "refusing to overwrite state owned by another topology.");
            }

            ITransaction transaction = database.CreateTransaction();
            _ = transaction.AddCondition(Condition.StringEqual(IsolationLockKey, lockToken));
            _ = transaction.AddCondition(currentPayload.HasValue
                ? Condition.HashEqual(MarkerKey, "data", currentPayload)
                : Condition.KeyNotExists(MarkerKey));
            Task<bool> markerDeleted = transaction.KeyDeleteAsync(MarkerKey);
            Task? markerRestored = null;
            if (_markerSnapshot is not null) {
                markerRestored = transaction.KeyRestoreAsync(MarkerKey, _markerSnapshot, _markerSnapshotExpiry);
            }

            bool committed = await transaction.ExecuteAsync().ConfigureAwait(false);
            if (!committed) {
                throw new InvalidOperationException(
                    "The writer-protocol marker or its isolation lease changed before atomic restoration.");
            }

            _ = await markerDeleted.ConfigureAwait(false);
            if (markerRestored is not null) {
                await markerRestored.ConfigureAwait(false);
            }
        }
        finally {
            _ = await database.LockReleaseAsync(IsolationLockKey, lockToken).ConfigureAwait(false);
            await redis.CloseAsync(allowCommandsToComplete: true).ConfigureAwait(false);
        }

        _markerSnapshot = null;
        _markerSnapshotExpiry = null;
        _sourceCommit = null;
        _lockToken = null;
        _isActive = false;
    }

    private static async Task<IConnectionMultiplexer> ConnectRedisAsync()
        => await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
            EndPoints = { RedisEndpoint },
            ConnectTimeout = 5_000,
            SyncTimeout = 5_000,
            AbortOnConnectFail = false,
            AllowAdmin = false,
        }).ConfigureAwait(false);

    private async Task IsolateMarkerAsync(
        IDatabase database,
        string sourceCommit,
        CancellationToken cancellationToken) {
        if (_isActive) {
            throw new InvalidOperationException(
                "The disposable topology attempted to isolate the writer-protocol marker more than once.");
        }

        string lockToken = Guid.NewGuid().ToString("N");
        _lockToken = lockToken;
        bool lockAcquired = false;

        try {
            // StackExchange.Redis mutation tasks are not cancellable. Await them to completion
            // before honoring caller cancellation so a late lock/transaction cannot mutate the
            // global marker after this method has reported cancellation.
            lockAcquired = await database
                .LockTakeAsync(IsolationLockKey, lockToken, s_lockLease)
                .ConfigureAwait(false);
            if (!lockAcquired) {
                throw new InvalidOperationException(
                    "Another topology owns the store-global writer-protocol marker isolation lease.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            ITransaction transaction = database.CreateTransaction();
            _ = transaction.AddCondition(Condition.StringEqual(IsolationLockKey, lockToken));
            Task<byte[]?> snapshot = transaction.KeyDumpAsync(MarkerKey);
            Task<TimeSpan?> snapshotExpiry = transaction.KeyTimeToLiveAsync(MarkerKey);
            Task<bool> markerDeleted = transaction.KeyDeleteAsync(MarkerKey);
            bool committed = await transaction.ExecuteAsync().ConfigureAwait(false);
            if (!committed) {
                throw new InvalidOperationException(
                    "The writer-protocol marker isolation lease changed before the atomic snapshot completed.");
            }

            _markerSnapshot = await snapshot.ConfigureAwait(false);
            _markerSnapshotExpiry = await snapshotExpiry.ConfigureAwait(false);
            _ = await markerDeleted.ConfigureAwait(false);
            _sourceCommit = sourceCommit;
            _isActive = true;
        }
        catch {
            if (lockAcquired) {
                _ = await database.LockReleaseAsync(IsolationLockKey, lockToken).ConfigureAwait(false);
            }

            _lockToken = null;
            throw;
        }
    }

    private static async Task<WriterProtocolMarker?> ReadMarkerAsync(
        IDatabase database,
        CancellationToken cancellationToken) {
        RedisValue payload = await database
            .HashGetAsync(MarkerKey, "data")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return DeserializeMarker(payload);
    }

    private static WriterProtocolMarker? DeserializeMarker(RedisValue payload) {
        if (!payload.HasValue) {
            return null;
        }

        return JsonSerializer.Deserialize<WriterProtocolMarker>(payload.ToString(), s_serializerOptions)
            ?? throw new InvalidOperationException(
                "Redis returned an empty projection delivery writer-protocol marker payload.");
    }

    private static void AssertMarker(WriterProtocolMarker marker, string sourceCommit) {
        if (marker.SchemaVersion != SchemaVersion
            || marker.WriterProtocolVersion != WriterProtocolVersion
            || !string.Equals(marker.CutoverCommit, sourceCommit, StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                "The persisted projection delivery writer-protocol marker does not identify the exact test runtime. "
                + $"Expected schema={SchemaVersion}, protocol={WriterProtocolVersion}, commit={sourceCommit}; "
                + $"actual schema={marker.SchemaVersion}, protocol={marker.WriterProtocolVersion}, "
                + $"commit={marker.CutoverCommit}.");
        }
    }

    private sealed record WriterProtocolMarker(
        int SchemaVersion,
        int WriterProtocolVersion,
        string CutoverCommit);
}
