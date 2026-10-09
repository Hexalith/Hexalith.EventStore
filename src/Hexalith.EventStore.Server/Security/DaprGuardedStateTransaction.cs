using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Actual shared joint backend CAS primitive for guard, target/source/outbox/phase cells and exact outcome; no active registration or actor-private key access.</summary>
/// <param name="client">Existing qualified transactional component client.</param><param name="clock">One whole-call budget.</param>
/// <param name="authority">Independent exact installation/current writer/source/restore authority; omission disables every effect.</param>
/// <remarks>Existing aggregate writers remain unchanged until an independently qualified migration/routing installation uses this owner.
/// Every destructive/domain guard transition still requires its owning exact verified policy; this primitive supplies only joint commit atomicity.</remarks>
public sealed class DaprGuardedStateTransaction(DaprClient client, TimeProvider clock, IGuardedStateTransactionAuthority? authority = null)
{
    private const int MaxBytes = 16 * 1024 * 1024;
    /// <summary>Exact restart lookup after later guard mutations; immutable receipt provenance and terminal current authority are required.</summary>
    public async Task<GuardedStateCommitResult> LookupByIntentAsync(string tenantId, string operationId, string intentDigest, CancellationToken cancellationToken = default)
        => await LookupOriginalCoreAsync(tenantId, operationId, intentDigest, true, cancellationToken).ConfigureAwait(false);

    /// <summary>Private exact original outcome lookup under an independently authenticated artifact-scope digest; caller validates typed original artifact correlation, never creates an effect.</summary>
    public async Task<GuardedStateCommitResult> LookupOriginalAsync(string tenantId, string operationId, string artifactScopeDigest, CancellationToken cancellationToken = default)
        => await LookupOriginalCoreAsync(tenantId, operationId, artifactScopeDigest, false, cancellationToken).ConfigureAwait(false);

    private async Task<GuardedStateCommitResult> LookupOriginalCoreAsync(string tenantId, string operationId, string intentDigest, bool matchIntent, CancellationToken cancellationToken)
    {
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        try
        {
            if (authority is null || !Text(tenantId) || !Text(operationId) || !Hex(intentDigest)) { return new(GuardedStateCommitStatus.Unavailable); }
            var target = await deadline.ReadAsync(token => authority.GetCurrentAsync(tenantId, token)).ConfigureAwait(false);
            if (!ValidTarget(target, tenantId) || !await deadline.ReadAsync(token => authority.AuthorizeLookupAsync(target!, operationId, intentDigest, token)).ConfigureAwait(false))
            { return new(GuardedStateCommitStatus.Unavailable); }
            var read = await deadline.ReadAsync(token => client.GetStateAsync<GuardedStateCommitReceipt>(target!.ComponentName, OutcomeKey(target, operationId), ConsistencyMode.Strong, Metadata(target), token)).ConfigureAwait(false);
            var receipt = read is null ? null : read with { Outcome = read.Outcome?.ToArray()! };
            if (receipt is not null && (!ValidReceipt(receipt, target!, operationId) || !await deadline.ReadAsync(token => authority.ValidateReceiptAsync(target!, receipt with { Outcome = receipt.Outcome.ToArray() }, token)).ConfigureAwait(false)))
            { return new(GuardedStateCommitStatus.Unavailable); }
            if (await deadline.ReadAsync(token => authority.GetCurrentAsync(tenantId, token)).ConfigureAwait(false) != target || !ValidTarget(target, tenantId)
                || !await deadline.ReadAsync(token => authority.AuthorizeLookupAsync(target!, operationId, intentDigest, token)).ConfigureAwait(false))
            { return new(GuardedStateCommitStatus.Unavailable); }
            if (!ValidTarget(target, tenantId)) { return new(GuardedStateCommitStatus.Unavailable); }
            deadline.ThrowIfCancellationRequested();
            return receipt is null ? new(GuardedStateCommitStatus.Unknown) : !matchIntent || receipt.LogicalIntentDigest == intentDigest
                ? new(GuardedStateCommitStatus.Committed, receipt) : new(GuardedStateCommitStatus.Conflict);
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return new(GuardedStateCommitStatus.Unavailable); }
    }
    /// <summary>Reads the exact installed guard under current independent authority; this read grants no append authority.</summary>
    public async Task<GuardedStateCell?> ReadGuardAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        try
        {
            if (authority is null || !Text(tenantId)) { return null; }
            var target = await deadline.ReadAsync(token => authority.GetCurrentAsync(tenantId, token)).ConfigureAwait(false);
            if (!ValidTarget(target, tenantId) || !await deadline.ReadAsync(token => authority.AuthorizeReadAsync(target!, target!.GuardCellId, token)).ConfigureAwait(false)) { return null; }
            var (read, etag) = await deadline.ReadAsync(token => client.GetStateAndETagAsync<GuardedStateCell>(target!.ComponentName,
                Key(target, target.GuardCellId), ConsistencyMode.Strong, Metadata(target), token)).ConfigureAwait(false);
            if (read is null || !Text(etag) || !ValidCell(read, target!, target!.GuardCellId)) { return null; }
            var cell = read with { Value = read.Value.ToArray() };
            if (!await deadline.ReadAsync(token => authority.ValidateCellAsync(target, cell with { Value = cell.Value.ToArray() }, cell.CellId, token)).ConfigureAwait(false)
                || await deadline.ReadAsync(token => authority.GetCurrentAsync(tenantId, token)).ConfigureAwait(false) != target
                || !ValidTarget(target, tenantId) || !await deadline.ReadAsync(token => authority.AuthorizeReadAsync(target, cell.CellId, token)).ConfigureAwait(false)) { return null; }
            if (!ValidTarget(target, tenantId)) { return null; }
            deadline.ThrowIfCancellationRequested(); return cell;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }
    /// <summary>Conditionally commits all source/guard cells and original receipt in one tenant transaction; acknowledgement loss uses exact lookup, never a blind second transaction.</summary>
    public async Task<GuardedStateCommitResult> CommitAsync(GuardedStateCommitRequest request, CancellationToken cancellationToken = default)
    {
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        try
        {
            request = Capture(request, deadline); string digest = Digest(request);
            var target = await CurrentAsync(request, digest, "Commit", deadline).ConfigureAwait(false);
            if (target is null) { return new(GuardedStateCommitStatus.Unavailable); }
            if (request.Guard.CellId != target.GuardCellId || request.Guard.ExpectedRevision <= 0) { return new(GuardedStateCommitStatus.Unavailable); }
            var prior = await LookupCoreAsync(target, request, digest, deadline).ConfigureAwait(false);
            if (prior is not null)
            { return await CurrentAsync(request, digest, "Commit", deadline).ConfigureAwait(false) == target ? prior : new(GuardedStateCommitStatus.Unavailable); }
            var operations = new List<StateTransactionRequest>();
            foreach (var mutation in new[] { request.Guard }.Concat(request.Targets))
            {
                var (readCell, etag) = await deadline.ReadAsync(token => client.GetStateAndETagAsync<GuardedStateCell>(target.ComponentName,
                    Key(target, mutation.CellId), ConsistencyMode.Strong, Metadata(target), token)).ConfigureAwait(false);
                deadline.ThrowIfCancellationRequested();
                if (!ValidCell(readCell, target, mutation.CellId)) { return new(GuardedStateCommitStatus.Unavailable); }
                var cell = readCell is null ? null : readCell with { Value = readCell.Value.ToArray() };
                if (mutation.CellId == target.GuardCellId && (cell is null || !Text(etag))
                    || !ValidCell(cell, target, mutation.CellId) || authority is null
                    || !await deadline.ReadAsync(token => authority.ValidateCellAsync(target, cell is null ? null : cell with { Value = cell.Value.ToArray() }, mutation.CellId, token)).ConfigureAwait(false))
                { return new(GuardedStateCommitStatus.Unavailable); }
                if ((cell?.Revision ?? 0) != mutation.ExpectedRevision || DigestBytes(cell?.Value ?? []) != mutation.ExpectedDigest)
                { return new(GuardedStateCommitStatus.Stale); }
                string? expectedEtag = cell is null ? null : etag;
                if (cell is not null && !Text(expectedEtag)) { return new(GuardedStateCommitStatus.Unavailable); }
                long nextRevision = checked(mutation.ExpectedRevision + 1);
                var next = new GuardedStateCell(target.TenantId, target.InstallationId, mutation.CellId, nextRevision, mutation.NextValue);
                operations.Add(new(Key(target, mutation.CellId), JsonSerializer.SerializeToUtf8Bytes(next), StateOperationType.Upsert,
                    expectedEtag, Metadata(target), new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }));
            }
            var receipt = new GuardedStateCommitReceipt(target.TenantId, target.InstallationId, request.OperationId, digest,
                checked(request.Guard.ExpectedRevision + 1), Digest(new[] { target.InstallationId, request.OperationId, digest }))
                { LogicalIntentDigest = request.LogicalIntentDigest, Outcome = request.Outcome.ToArray(),
                    LogicalGuardHighWater = request.LogicalGuardHighWater == 0 ? checked(request.Guard.ExpectedRevision + 1) : request.LogicalGuardHighWater };
            operations.Add(new(OutcomeKey(target, request.OperationId), JsonSerializer.SerializeToUtf8Bytes(receipt), StateOperationType.Upsert,
                null, Metadata(target), new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }));
            if (await CurrentAsync(request, digest, "Commit", deadline).ConfigureAwait(false) != target || Digest(request) != digest)
            { return new(GuardedStateCommitStatus.Unavailable); }
            try { await deadline.ReadAsync(async token => { await client.ExecuteStateTransactionAsync(target.ComponentName, operations, Metadata(target), token).ConfigureAwait(false); return true; }).ConfigureAwait(false); }
            catch (Exception) { deadline.ThrowIfCancellationRequested(); }
            var committed = await LookupCoreAsync(target, request, digest, deadline).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            return await CurrentAsync(request, digest, "Commit", deadline).ConfigureAwait(false) == target
                ? committed ?? new(GuardedStateCommitStatus.Unknown) : new(GuardedStateCommitStatus.Unavailable);
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return new(GuardedStateCommitStatus.Unavailable); }
    }

    /// <summary>Reads only the exact original outcome under current private authority; it never performs a new transaction.</summary>
    public async Task<GuardedStateCommitResult> LookupAsync(GuardedStateCommitRequest request, CancellationToken cancellationToken = default)
    {
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        try
        {
            request = Capture(request, deadline); string digest = Digest(request); var target = await CurrentAsync(request, digest, "Lookup", deadline).ConfigureAwait(false);
            if (target is null) { return new(GuardedStateCommitStatus.Unavailable); }
            var result = await LookupCoreAsync(target, request, digest, deadline).ConfigureAwait(false);
            return await CurrentAsync(request, digest, "Lookup", deadline).ConfigureAwait(false) == target
                ? result ?? new(GuardedStateCommitStatus.Unknown) : new(GuardedStateCommitStatus.Unavailable);
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return new(GuardedStateCommitStatus.Unavailable); }
    }

    private async Task<GuardedStateCommitResult?> LookupCoreAsync(GuardedStateTransactionTarget target, GuardedStateCommitRequest request,
        string digest, AuthoritativeStreamReadDeadline deadline)
    {
        var read = await deadline.ReadAsync(token => client.GetStateAsync<GuardedStateCommitReceipt>(target.ComponentName,
            OutcomeKey(target, request.OperationId), ConsistencyMode.Strong, Metadata(target), token)).ConfigureAwait(false);
        var receipt = read is null ? null : read with { Outcome = read.Outcome?.ToArray()! };
        if (receipt is null) { return null; }
        if (!ValidReceipt(receipt, target, request.OperationId) || authority is null
            || !await deadline.ReadAsync(token => authority.ValidateReceiptAsync(target, receipt with { Outcome = receipt.Outcome.ToArray() }, token)).ConfigureAwait(false))
        { return new(GuardedStateCommitStatus.Unavailable); }
        return receipt.RequestDigest == digest && receipt.GuardRevision == request.Guard.ExpectedRevision + 1
            ? new(GuardedStateCommitStatus.Committed, receipt) : new(GuardedStateCommitStatus.Conflict);
    }
    private async Task<GuardedStateTransactionTarget?> CurrentAsync(GuardedStateCommitRequest request, string digest, string operation,
        AuthoritativeStreamReadDeadline deadline)
    {
        if (authority is null) { return null; }
        var target = await deadline.ReadAsync(token => authority.GetCurrentAsync(request.TenantId, token)).ConfigureAwait(false);
        if (!ValidTarget(target, request.TenantId)
            || !await deadline.ReadAsync(token => authority.AuthorizeAsync(target!, Capture(request, deadline), digest, operation, token)).ConfigureAwait(false)) { return null; }
        deadline.ThrowIfCancellationRequested(); return ValidTarget(target, request.TenantId) ? target : null;
    }
    private static GuardedStateCommitRequest Capture(GuardedStateCommitRequest request, AuthoritativeStreamReadDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Text(request.TenantId) || !Text(request.OperationId) || request.Guard is null || request.Targets is null || request.Targets.Count > 1000)
        { throw new ArgumentException("Malformed private joint transaction."); }
        if (request.Outcome is null || request.Outcome.Length > MaxBytes || request.LogicalGuardHighWater < 0 || request.LogicalGuardHighWater > request.Guard.ExpectedRevision + 1
            || request.LogicalIntentDigest != "" && !Hex(request.LogicalIntentDigest))
        { throw new ArgumentException("Malformed bounded original outcome."); }
        byte[] outcome = request.Outcome.ToArray(); long bytes = outcome.Length; var ids = new HashSet<string>(StringComparer.Ordinal); var targets = new List<GuardedStateMutation>();
        GuardedStateMutation Copy(GuardedStateMutation value)
        {
            deadline.ThrowIfCancellationRequested();
            if (value is null || !Text(value.CellId) || !ids.Add(value.CellId) || value.ExpectedRevision < 0 || value.ExpectedRevision == long.MaxValue
                || !Hex(value.ExpectedDigest) || value.NextValue is null || value.NextValue.Length > MaxBytes - bytes)
            { throw new ArgumentException("Malformed bounded joint cell mutation."); }
            byte[] owned = value.NextValue.ToArray(); bytes += owned.Length; return value with { NextValue = owned };
        }
        var guard = Copy(request.Guard);
        foreach (var value in request.Targets) { if (targets.Count >= 1000) { throw new ArgumentException("Oversized joint transaction."); } targets.Add(Copy(value)); }
        if (request.CompareGuardWithoutMutation && (targets.Count != 0 || DigestBytes(guard.NextValue) != guard.ExpectedDigest))
        { throw new ArgumentException("A no-effect compare cannot mutate the guard or target cells."); }
        return request with { Guard = guard, Targets = targets.AsReadOnly(), Outcome = outcome };
    }
    private static bool ValidCell(GuardedStateCell? cell, GuardedStateTransactionTarget target, string id)
        => cell is null || cell.TenantId == target.TenantId && cell.InstallationId == target.InstallationId && cell.CellId == id
            && cell.Revision > 0 && cell.Value is { Length: <= MaxBytes };
    private bool ValidTarget(GuardedStateTransactionTarget? target, string tenant)
        => target is not null && target.TenantId == tenant && target.ObservedAt != default && target.ObservedAt <= clock.GetUtcNow()
            && target.ValidUntil > clock.GetUtcNow() && new[] { target.TenantId, target.ComponentName, target.PartitionKey, target.InstallationId,
                target.GuardCellId, target.WriterRevocationReceipt, target.AuthorityRevision }.All(Text);
    private static bool ValidReceipt(GuardedStateCommitReceipt receipt, GuardedStateTransactionTarget target, string operationId)
        => receipt.TenantId == target.TenantId && receipt.InstallationId == target.InstallationId && receipt.OperationId == operationId
            && receipt.GuardRevision > 0 && Text(receipt.ReceiptId) && Hex(receipt.RequestDigest) && receipt.Outcome is { Length: <= MaxBytes }
            && receipt.LogicalGuardHighWater >= 0 && receipt.LogicalGuardHighWater <= receipt.GuardRevision
            && (receipt.LogicalIntentDigest == "" || Hex(receipt.LogicalIntentDigest));
    private static IReadOnlyDictionary<string, string> Metadata(GuardedStateTransactionTarget target) => new Dictionary<string, string> { ["partitionKey"] = target.PartitionKey };
    private static string Key(GuardedStateTransactionTarget target, string id) => "governed/" + Digest(new[] { target.TenantId, target.InstallationId, "cell", id });
    private static string OutcomeKey(GuardedStateTransactionTarget target, string id) => "governed/" + Digest(new[] { target.TenantId, target.InstallationId, "outcome", id });
    private static bool Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) { return false; }
        try { _ = new System.Text.UTF8Encoding(false, true).GetByteCount(value); return true; } catch (System.Text.EncoderFallbackException) { return false; }
    }
    private static bool Hex(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    private static string Digest<T>(T value) => DigestBytes(JsonSerializer.SerializeToUtf8Bytes(value));
    private static string DigestBytes(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
}
