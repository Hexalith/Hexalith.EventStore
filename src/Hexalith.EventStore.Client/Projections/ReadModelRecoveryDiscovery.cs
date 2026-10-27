using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>SDK-owned bounded discovery enrolled before private preparation, with permanent write barriers on retirement.</summary>
internal static class ReadModelRecoveryDiscovery
{
    internal static readonly byte[] RetiredManifest = "{\"retiredRecoveryManifest\":true}"u8.ToArray();
    internal static string RegistrationKey(ReadModelBatchScope scope) => ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash()) + ":discovery";
    private static string DirectoryKey(ReadModelBatchScope scope) => "hxrmb:v1:recovery-scope:" +
        (scope with { ProjectionType = "recovery-scope", BatchId = "directory" }).ComputeScopeHash();

    internal static async Task<(ReadModelRecoveryDirectory Directory, string ETag)> ReadAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchScope scope, CancellationToken token)
    {
        RawStateEntry row = await accessor.ReadAsync(DirectoryKey(scope), token).ConfigureAwait(false);
        if (!row.Exists) return (new(0, []), string.Empty);
        if (row.Value.Length > 512 * 1024) throw new JsonException("Invalid recovery directory.");
        var directory = JsonSerializer.Deserialize<ReadModelRecoveryDirectory>(row.Value.Span, JsonSerializerOptions.Web);
        if (directory is null || directory.Epoch < 0 || directory.Entries is null || directory.Entries.Length > 32
            || directory.Entries.Any(entry => entry is null || entry.Scope is null || entry.OperationCount is < 1 or > 100
                || !ReadModelBatchFingerprint.IsCanonical(entry.Fingerprint) || !SameAggregate(entry.Scope, scope))
            || directory.Entries.Select(entry => entry.Scope.ComputeScopeHash()).Distinct().Count() != directory.Entries.Length
            || !ReadModelBatchCanonicalJson.Serialize(directory).Span.SequenceEqual(row.Value.Span)) throw new JsonException("Invalid recovery directory.");
        return (directory, row.ETag);
    }

    internal static bool SameAggregate(ReadModelBatchScope left, ReadModelBatchScope right) => left.StoreName == right.StoreName
        && left.TenantId == right.TenantId && left.Domain == right.Domain && left.AggregateId == right.AggregateId;

    internal static Task<bool> WriteAsync(IReadModelBatchStateAccessor accessor, ReadModelBatchScope scope,
        ReadModelRecoveryDirectory value, string etag, CancellationToken token)
    {
        ReadOnlyMemory<byte> bytes = ReadModelBatchCanonicalJson.Serialize(value);
        if (value.Entries.Length > 32 || bytes.Length > 512 * 1024) throw new InvalidOperationException("recovery-scope-capacity");
        return accessor.TryWriteAsync(DirectoryKey(scope), bytes, etag, token);
    }

    internal static async Task<bool> EnrollAsync(IReadModelBatchStateAccessor accessor, ReadModelBatch batch, string fingerprint,
        Func<Task<bool>> validateSnapshot, CancellationToken token)
    {
        var registration = new ReadModelRecoveryRegistration(batch.Scope, fingerprint, batch.Operations.Count);
        (ReadModelRecoveryDirectory directory, string etag) = await ReadAsync(accessor, batch.Scope, token).ConfigureAwait(false);
        if (directory.Closed) throw new ReadModelBatchSnapshotConflictException();
        ReadModelRecoveryRegistration? prior = directory.Entries.SingleOrDefault(entry => entry.Scope == batch.Scope);
        if (prior is not null && prior != registration) throw new ArgumentException("Recovery registration identity conflict.");
        if (prior is null)
        {
            // Read the epoch before validating all canonical preconditions. A concurrent retirement
            // invalidates this exact CAS; a stale snapshot cannot enroll in the new epoch.
            if (directory.Entries.Length >= 32) return false;
            if (!await validateSnapshot().ConfigureAwait(false)
                || !await WriteAsync(accessor, batch.Scope, directory with { Entries = [.. directory.Entries, registration] }, etag, token).ConfigureAwait(false))
                throw new ReadModelBatchSnapshotConflictException();
        }
        return await ReadModelBatchImmutableRecord.SealAsync(accessor, RegistrationKey(batch.Scope),
            ReadModelBatchCanonicalJson.Serialize(registration), token).ConfigureAwait(false);
    }

    internal static async Task RemoveAsync(IReadModelBatchStateAccessor accessor, ReadModelBatchScope scope, string fingerprint, CancellationToken token)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var (directory, etag) = await ReadAsync(accessor, scope, token).ConfigureAwait(false);
            var remaining = directory.Entries.Where(entry => entry.Scope != scope || entry.Fingerprint != fingerprint).ToArray();
            if (remaining.Length == directory.Entries.Length) return;
            if (await WriteAsync(accessor, scope, directory with { Entries = remaining }, etag, token).ConfigureAwait(false)) return;
        }
        throw new InvalidOperationException("recovery-scope-conflict");
    }

    internal static async Task ReleaseAsync(IReadModelBatchStateAccessor accessor, ReadModelBatchScope scope, CancellationToken token)
    {
        var (directory, etag) = await ReadAsync(accessor, scope, token).ConfigureAwait(false);
        if (directory.Entries.Length != 0 || !await WriteAsync(accessor, scope, directory with { Closed = false }, etag, token).ConfigureAwait(false))
            throw new InvalidOperationException("recovery-scope-conflict");
    }

    internal static async Task<ReadModelPendingBatchRecoveryResult> SettleAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchOptions options, ILogger logger, ReadModelBatchScope scope, CancellationToken token)
    {
        try
        {
            scope.Validate();
            var (directory, _) = await ReadAsync(accessor, scope, token).ConfigureAwait(false);
            foreach (ReadModelRecoveryRegistration entry in directory.Entries)
            {
                if (!await new ReadModelBatchProtocol(accessor, options, logger).SettleRegisteredAsync(entry, token).ConfigureAwait(false))
                    return new(false, "recovery-incomplete");
            }
            return new(true, WasRecovered: directory.Entries.Length != 0);
        }
        catch (OperationCanceledException) { throw; }
        catch { return new(false, "recovery-unavailable"); }
    }

    internal static async Task<ReadModelPendingBatchRecoveryResult> RetireAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchOptions options, ILogger logger, ReadModelBatchScope scope, CancellationToken token)
    {
        try
        {
            scope.Validate();
            var (directory, etag) = await ReadAsync(accessor, scope, token).ConfigureAwait(false);
            if (!await WriteAsync(accessor, scope, directory with { Epoch = checked(directory.Epoch + 1), Closed = true }, etag, token).ConfigureAwait(false))
                return new(false, "recovery-scope-conflict");
            foreach (ReadModelRecoveryRegistration entry in directory.Entries)
            {
                if (!await ReadModelBatchImmutableRecord.SealAsync(accessor, RegistrationKey(entry.Scope),
                    ReadModelBatchCanonicalJson.Serialize(entry), token).ConfigureAwait(false)) return new(false, "invalid-recovery-evidence");
                var result = await new ReadModelBatchProtocol(accessor, options, logger).RetireRegisteredAsync(entry, token).ConfigureAwait(false);
                if (!result) return new(false, "recovery-incomplete");
                await RemoveAsync(accessor, entry.Scope, entry.Fingerprint, token).ConfigureAwait(false);
            }
            return new(true, WasRecovered: directory.Entries.Length != 0);
        }
        catch (OperationCanceledException) { throw; }
        catch { return new(false, "recovery-unavailable"); }
    }
}
