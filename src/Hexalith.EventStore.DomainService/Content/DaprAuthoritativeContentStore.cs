using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Dapr-owned body namespace and atomic, body-free CAS publication protocol.</summary>
/// <remarks>Requires a strongly consistent, first-write-CAS-qualified Dapr component. Domain events remain the outcome authority.</remarks>
public sealed class DaprAuthoritativeContentStore : IAuthoritativeContentStore
{
    private static readonly StateOptions _cas = new() { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong };
    private readonly DaprClient _dapr;
    private readonly IContentAuthority _authority;
    private readonly AuthoritativeContentOptions _options;
    private readonly TimeProvider _time;

    /// <summary>Creates the physical owner with explicit current authority and bounded options.</summary>
    public DaprAuthoritativeContentStore(DaprClient dapr, IContentAuthority authority, IOptions<AuthoritativeContentOptions> options, TimeProvider time)
    {
        _dapr = dapr ?? throw new ArgumentNullException(nameof(dapr));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _time = time ?? throw new ArgumentNullException(nameof(time));
        if (string.IsNullOrWhiteSpace(_options.StateStoreName) || _options.MaximumBodyBytes is < 1 or > 64 * 1024 * 1024
            || _options.MaximumEntries is < 1 or > 16384 || _options.WriteAttempts is < 1 or > 64
            || _options.OperationTimeout <= TimeSpan.Zero || _options.OperationTimeout > TimeSpan.FromMinutes(2))
            throw new ArgumentException("Invalid authoritative content options.", nameof(options));
    }

    /// <inheritdoc/>
    public Task<ContentReceipt> UploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, Stream bytes, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(bytes);
            await AuthorizeAsync(principal, request.Scope, ContentOperation.Upload, request, token).ConfigureAwait(false);
            if (request.ExpectedLength < 0 || request.ExpectedLength > _options.MaximumBodyBytes
                || !IsHex(request.ExpectedSha256, 64) || request.TaskId is { Length: > 128 }
                || request.UploadId is { Length: > 128 } || request.UploadId is not null && string.IsNullOrWhiteSpace(request.UploadId)
                || request.MediaType.Length > 128 || !MediaTypeHeaderValue.TryParse(request.MediaType, out _))
                throw new ArgumentException("Invalid bounded content upload.", nameof(request));
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[81920];
            while (true)
            {
                int length = await bytes.ReadAsync(chunk, token).AsTask().WaitAsync(token).ConfigureAwait(false);
                if (length == 0) break;
                if (buffer.Length + length > request.ExpectedLength || buffer.Length + length > _options.MaximumBodyBytes)
                    throw new ArgumentException("Content exceeds its declared byte bound.", nameof(bytes));
                await buffer.WriteAsync(chunk.AsMemory(0, length), token).ConfigureAwait(false);
            }
            byte[] actual = buffer.ToArray();
            string hash = Convert.ToHexStringLower(SHA256.HashData(actual));
            if (actual.LongLength != request.ExpectedLength || hash != request.ExpectedSha256)
                throw new ArgumentException("Content hash or length does not match actual bytes.", nameof(bytes));
            await _authority.ValidateUploadAsync(principal, request, actual, token).WaitAsync(token).ConfigureAwait(false);
            var receipt = new ContentReceipt("content_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), hash, actual.LongLength, request.MediaType, request.TaskId);
            // Reserve first: replacement processes can discover every possible body, including an interrupted upload.
            await MutateAsync(request.Scope, ledger =>
            {
                ContentStoredUpload? retry = request.UploadId is null ? null : ledger.Uploads.Values.FirstOrDefault(upload => upload.UploadId == request.UploadId && upload.TaskId == request.TaskId);
                if (retry is not null)
                {
                    if (retry.Receipt.Sha256 != hash || retry.Receipt.ByteLength != actual.LongLength || retry.Receipt.MediaType != request.MediaType || retry.Erased || retry.Erasing || retry.Abandoned)
                        throw new ContentConflictException();
                    receipt = retry.Receipt;
                    if (!retry.Complete) ledger.Uploads[receipt.Id] = retry with { CleanupEpoch = null, CleanupGeneration = null };
                    return ledger;
                }
                if (ledger.Uploads.Count >= _options.MaximumEntries) throw new ContentUnavailableException();
                ledger.Uploads.Add(receipt.Id, new(receipt, request.TaskId, false, UploadId: request.UploadId));
                return ledger;
            }, token).ConfigureAwait(false);
            if ((await ReadAsync<ContentStoredBody>(BodyKey(request.Scope, receipt.Id), token).ConfigureAwait(false)).Value is null)
                _ = await SaveAsync(BodyKey(request.Scope, receipt.Id), new ContentStoredBody(receipt, actual), string.Empty, token).ConfigureAwait(false);
            _ = await ReadBodyAsync(request.Scope, receipt, token).ConfigureAwait(false);
            await MutateAsync(request.Scope, ledger =>
            {
                ContentStoredUpload upload = ledger.Uploads[receipt.Id];
                if (upload.Erasing || upload.Erased) throw new ContentUnavailableException();
                ledger.Uploads[receipt.Id] = upload with { Complete = true };
                return ledger;
            }, token).ConfigureAwait(false);
            return receipt;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<ContentReceipt> VerifyUploadAsync(ClaimsPrincipal principal, ContentUploadVerification request, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            await AuthorizeAsync(principal, request.Scope, ContentOperation.Upload, request, token).ConfigureAwait(false);
            if (!IsReceiptId(request.ReceiptId)) throw new ContentUnavailableException();
            ContentLedger ledger = (await ReadLedgerAsync(request.Scope, token).ConfigureAwait(false)).Ledger;
            if (!ledger.Uploads.TryGetValue(request.ReceiptId, out ContentStoredUpload? upload) || !upload.Complete
                || upload.Erased || upload.Erasing || upload.TaskId != request.TaskId) throw new ContentUnavailableException();
            _ = await ReadBodyAsync(request.Scope, upload.Receipt, token).ConfigureAwait(false);
            return upload.Receipt;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<ContentReadResult> InspectUploadAsync(ClaimsPrincipal principal, ContentUploadVerification request, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            await AuthorizeAsync(principal, request.Scope, ContentOperation.InspectUpload, request, token).ConfigureAwait(false);
            if (!IsReceiptId(request.ReceiptId)) throw new ContentUnavailableException();
            ContentLedger ledger = (await ReadLedgerAsync(request.Scope, token).ConfigureAwait(false)).Ledger;
            if (!ledger.Uploads.TryGetValue(request.ReceiptId, out ContentStoredUpload? upload) || !upload.Complete
                || upload.Erased || upload.Erasing || upload.TaskId != request.TaskId) throw new ContentUnavailableException();
            ContentStoredBody body = await ReadBodyAsync(request.Scope, upload.Receipt, token).ConfigureAwait(false);
            await AuthorizeAsync(principal, request.Scope, ContentOperation.InspectUpload, request, token).ConfigureAwait(false);
            ContentLedger final = (await ReadLedgerAsync(request.Scope, token).ConfigureAwait(false)).Ledger;
            if (!final.Uploads.TryGetValue(request.ReceiptId, out ContentStoredUpload? current) || !current.Complete
                || current.Erased || current.Erasing || current.TaskId != request.TaskId || current.Receipt != upload.Receipt)
                throw new ContentUnavailableException();
            return new ContentReadResult(body.Receipt, body.Bytes);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task PrepareAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            await AuthorizeAsync(principal, request.Scope, ContentOperation.Prepare, request, token).ConfigureAwait(false);
            ContentPublicationRequest canonical = Canonicalize(request);
            ContentLedger initial = (await ReadLedgerAsync(canonical.Scope, token).ConfigureAwait(false)).Ledger;
            if (initial.Publications.TryGetValue(canonical.CommandId, out ContentStoredPublication? existing))
            {
                if (!Equivalent(existing.Request, canonical)) throw new ContentConflictException();
                return true;
            }
            await _authority.ValidatePreparationAsync(principal, canonical, token).WaitAsync(token).ConfigureAwait(false);
            foreach (ContentChange change in canonical.Changes.Where(static change => change.ReceiptId is not null))
            {
                if (!initial.Uploads.TryGetValue(change.ReceiptId!, out ContentStoredUpload? upload)
                    || !upload.Complete || upload.Erased || upload.Erasing || upload.TaskId != canonical.TaskId || upload.Receipt != change.ExpectedReceipt)
                    throw new ContentConflictException();
                _ = await ReadBodyAsync(canonical.Scope, upload.Receipt, token).ConfigureAwait(false);
            }
            await MutateAsync(canonical.Scope, ledger =>
            {
                if (ledger.Publications.TryGetValue(canonical.CommandId, out ContentStoredPublication? retry))
                {
                    if (!Equivalent(retry.Request, canonical)) throw new ContentConflictException();
                    return ledger;
                }
                if (ledger.Publications.Count >= _options.MaximumEntries || ledger.Bindings.Count + canonical.Changes.Count > _options.MaximumEntries
                    || ledger.Publications.Values.Any(static publication => publication.Outcome == ContentPublicationOutcome.Pending))
                    throw new ContentConflictException();
                foreach (ContentChange change in canonical.Changes)
                {
                    string incarnation = IncarnationKey(change.FileId, change.IncarnationId);
                    if (ledger.Tombstones.Contains(incarnation)
                        || ledger.Bindings.Values.Any(binding => binding.FileId == change.FileId && binding.IncarnationId != change.IncarnationId
                            && !ledger.Tombstones.Contains(IncarnationKey(binding.FileId, binding.IncarnationId)))) throw new ContentConflictException();
                    long latest = ledger.Bindings.Values.Where(binding => binding.FileId == change.FileId && binding.IncarnationId == change.IncarnationId)
                        .Select(static binding => binding.Version).DefaultIfEmpty(0).Max();
                    if (change.Version <= latest) throw new ContentConflictException();
                    if (change.ReceiptId is { } id)
                    {
                        if (!ledger.Uploads.TryGetValue(id, out ContentStoredUpload? upload) || !upload.Complete
                            || upload.Erased || upload.Erasing || upload.TaskId != canonical.TaskId || upload.Receipt != change.ExpectedReceipt)
                            throw new ContentConflictException();
                        ledger.Uploads[id] = upload with { CleanupEpoch = null, CleanupGeneration = null };
                    }
                }
                ledger.Publications.Add(canonical.CommandId, new(canonical, ContentPublicationOutcome.Pending));
                return ledger;
            }, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<ContentPublicationOutcome> RecoverAsync(ClaimsPrincipal principal, ContentScope scope, string commandId, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            await AuthorizeAsync(principal, scope, ContentOperation.Recover, commandId, token).ConfigureAwait(false);
            return await RecoverCoreAsync(scope, commandId, token).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<ContentReadResult> ReadAsync(ClaimsPrincipal principal, ContentReadRequest request, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            await AuthorizeAsync(principal, request.Scope, ContentOperation.Read, request, token).ConfigureAwait(false);
            if (request.MaximumBytes < 0 || request.MaximumBytes > _options.MaximumBodyBytes) throw new ArgumentOutOfRangeException(nameof(request));
            await RecoverPendingAsync(request.Scope, token).ConfigureAwait(false);
            ContentLedger ledger = (await ReadLedgerAsync(request.Scope, token).ConfigureAwait(false)).Ledger;
            string key = BindingKey(request.FileId, request.IncarnationId, request.Version);
            if (ledger.Tombstones.Contains(IncarnationKey(request.FileId, request.IncarnationId))
                || !ledger.Bindings.TryGetValue(key, out ContentChange? binding) || binding.ReceiptId is null
                || !ledger.Uploads.TryGetValue(binding.ReceiptId, out ContentStoredUpload? upload) || upload.Erasing || upload.Erased)
                throw new ContentUnavailableException();
            if (upload.Receipt.ByteLength > request.MaximumBytes) throw new ArgumentOutOfRangeException(nameof(request));
            ContentStoredBody body = await ReadBodyAsync(request.Scope, upload.Receipt, token).ConfigureAwait(false);
            // A deletion racing the physical read must be re-observed before returning bytes.
            await AuthorizeAsync(principal, request.Scope, ContentOperation.Read, request, token).ConfigureAwait(false);
            ContentLedger final = (await ReadLedgerAsync(request.Scope, token).ConfigureAwait(false)).Ledger;
            if (final.Tombstones.Contains(IncarnationKey(request.FileId, request.IncarnationId))
                || !final.Uploads.TryGetValue(upload.Receipt.Id, out ContentStoredUpload? finalUpload) || finalUpload.Erasing || finalUpload.Erased)
                throw new ContentUnavailableException();
            return new ContentReadResult(body.Receipt, body.Bytes);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ContentBinding>> ListAsync(ClaimsPrincipal principal, ContentScope scope, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<ContentBinding>>(async token =>
        {
            await AuthorizeAsync(principal, scope, ContentOperation.Read, scope, token).ConfigureAwait(false);
            await RecoverPendingAsync(scope, token).ConfigureAwait(false);
            ContentLedger ledger = (await ReadLedgerAsync(scope, token).ConfigureAwait(false)).Ledger;
            return ledger.Bindings.Values.Where(binding => !ledger.Tombstones.Contains(IncarnationKey(binding.FileId, binding.IncarnationId)))
                .GroupBy(static binding => binding.FileId, StringComparer.Ordinal)
                .Select(static group => group.MaxBy(static binding => binding.Version)!)
                .Where(binding => binding.ReceiptId is { } id && ledger.Uploads.TryGetValue(id, out ContentStoredUpload? upload) && !upload.Erased && !upload.Erasing)
                .OrderBy(static binding => binding.FileId, StringComparer.Ordinal)
                .Select(binding => new ContentBinding(binding, ledger.Uploads[binding.ReceiptId!].Receipt)).ToArray();
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ContentUploadStatus>> ListUploadsAsync(ClaimsPrincipal principal, ContentScope scope, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<ContentUploadStatus>>(async token =>
        {
            await AuthorizeAsync(principal, scope, ContentOperation.Disposition, scope, token).ConfigureAwait(false);
            return (await ReadLedgerAsync(scope, token).ConfigureAwait(false)).Ledger.Uploads.Values
                .OrderBy(static upload => upload.Receipt.Id, StringComparer.Ordinal)
                .Select(static upload => new ContentUploadStatus(upload.Receipt, upload.TaskId, upload.Complete, upload.Abandoned, upload.LegalHold, upload.CleanupEpoch, upload.Erased)).ToArray();
        }, cancellationToken);

    /// <inheritdoc/>
    public Task SetHoldAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, bool legalHold, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            await AuthorizeAsync(principal, scope, ContentOperation.Disposition, new ContentHoldRequest(receiptId, legalHold), token).ConfigureAwait(false);
            await MutateAsync(scope, ledger =>
            {
                if (!ledger.Uploads.TryGetValue(receiptId, out ContentStoredUpload? upload)) throw new ContentUnavailableException();
                if (upload.Erasing || upload.Erased) throw new ContentConflictException();
                if (upload.LegalHold != legalHold)
                    ledger.Uploads[receiptId] = upload with { LegalHold = legalHold, HoldGeneration = checked(upload.HoldGeneration + 1), CleanupEpoch = null, CleanupGeneration = null };
                return ledger;
            }, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task AbandonUploadAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            await AuthorizeAsync(principal, scope, ContentOperation.Disposition, new ContentAbandonRequest(receiptId), token).ConfigureAwait(false);
            await MutateAsync(scope, ledger =>
            {
                if (!ledger.Uploads.TryGetValue(receiptId, out ContentStoredUpload? upload)) throw new ContentUnavailableException();
                if (upload.TaskId is not null || upload.Erasing || upload.Erased) throw new ContentConflictException();
                if (!upload.Abandoned) ledger.Uploads[receiptId] = upload with { Abandoned = true, CleanupEpoch = null, CleanupGeneration = null };
                return ledger;
            }, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> ApplyDispositionAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            await AuthorizeAsync(principal, scope, ContentOperation.Disposition, receiptId, token).ConfigureAwait(false);
            ContentLedger ledger = (await ReadLedgerAsync(scope, token).ConfigureAwait(false)).Ledger;
            if (!ledger.Uploads.TryGetValue(receiptId, out ContentStoredUpload? upload)) throw new ContentUnavailableException();
            if (upload.Erased) return true;
            if (upload.DeletionAuthorized) return await CompleteErasureAsync(scope, receiptId, token).ConfigureAwait(false);
            await RecoverPendingAsync(scope, token).ConfigureAwait(false);
            ledger = (await ReadLedgerAsync(scope, token).ConfigureAwait(false)).Ledger;
            upload = ledger.Uploads[receiptId];
            if (upload.Erased) return true;
            if (upload.DeletionAuthorized) return await CompleteErasureAsync(scope, receiptId, token).ConfigureAwait(false);
            ContentDisposition disposition = await _authority.GetDispositionAsync(scope, upload.Receipt, token).WaitAsync(token).ConfigureAwait(false);
            bool eligible = (upload.TaskId is null ? upload.Abandoned : disposition.Terminal)
                && !disposition.HasActiveTask && !disposition.HasLiveReference && !disposition.LegalHold && !upload.LegalHold
                && !HasPendingReference(ledger, receiptId);
            DateTimeOffset now = _time.GetUtcNow();
            bool mature = eligible && upload.CleanupGeneration == disposition.Generation && upload.CleanupEpoch is { } epoch && now >= epoch.AddDays(7);
            string claimId = Guid.NewGuid().ToString("N");
            await MutateAsync(scope, current =>
            {
                ContentStoredUpload fresh = current.Uploads[receiptId];
                // This durable decision can never be reopened by a stale or ineligible visitor.
                if (fresh.Erased || fresh.DeletionAuthorized) return current;
                if (HasPendingReference(current, receiptId) || !eligible || fresh.LegalHold || fresh.HoldGeneration != upload.HoldGeneration)
                    current.Uploads[receiptId] = fresh with { CleanupEpoch = null, CleanupGeneration = null, Erasing = false, ErasureClaimId = null };
                else if (fresh.CleanupGeneration != disposition.Generation || fresh.CleanupEpoch is null)
                    current.Uploads[receiptId] = fresh with { CleanupEpoch = now, CleanupGeneration = disposition.Generation, Erasing = false, ErasureClaimId = null };
                else if (mature && fresh.CleanupEpoch == upload.CleanupEpoch)
                    // Taking over an interrupted sealing attempt fences its previous caller.
                    current.Uploads[receiptId] = fresh with { Erasing = true, ErasureClaimId = claimId };
                return current;
            }, token).ConfigureAwait(false);
            ContentStoredUpload claimed = (await ReadLedgerAsync(scope, token).ConfigureAwait(false)).Ledger.Uploads[receiptId];
            if (claimed.Erased) return true;
            if (claimed.DeletionAuthorized) return await CompleteErasureAsync(scope, receiptId, token).ConfigureAwait(false);
            if (!mature || !claimed.Erasing || claimed.ErasureClaimId != claimId || claimed.CleanupGeneration != disposition.Generation) return false;
            bool granted = await _authority.TryAuthorizeErasureAsync(scope, upload.Receipt, disposition.Generation, token).WaitAsync(token).ConfigureAwait(false);
            await MutateAsync(scope, current =>
            {
                ContentStoredUpload fresh = current.Uploads[receiptId];
                if (fresh.Erased || fresh.DeletionAuthorized || fresh.ErasureClaimId != claimId) return current;
                if (!granted)
                    current.Uploads[receiptId] = fresh with { CleanupEpoch = null, CleanupGeneration = null, Erasing = false, ErasureClaimId = null };
                else if (fresh.Erasing && fresh.CleanupGeneration == disposition.Generation && fresh.CleanupEpoch == upload.CleanupEpoch
                    && fresh.HoldGeneration == upload.HoldGeneration && !fresh.LegalHold && !HasPendingReference(current, receiptId))
                    current.Uploads[receiptId] = fresh with { DeletionAuthorized = true };
                return current;
            }, token).ConfigureAwait(false);
            ContentStoredUpload final = (await ReadLedgerAsync(scope, token).ConfigureAwait(false)).Ledger.Uploads[receiptId];
            return final.Erased || final.DeletionAuthorized && await CompleteErasureAsync(scope, receiptId, token).ConfigureAwait(false);
        }, cancellationToken);

    private async Task<bool> CompleteErasureAsync(ContentScope scope, string receiptId, CancellationToken token)
    {
        // A permanent body-free marker fences delayed create-only uploads at this exact key.
        ContentReceipt receipt = (await ReadLedgerAsync(scope, token).ConfigureAwait(false)).Ledger.Uploads[receiptId].Receipt;
        bool sealedBody = false;
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            (ContentStoredBody? body, string etag) = await ReadAsync<ContentStoredBody>(BodyKey(scope, receiptId), token).ConfigureAwait(false);
            if (body is { Erased: true } && body.Bytes is { Length: 0 } && body.Receipt == receipt)
            { sealedBody = true; break; }
            if (await SaveAsync(BodyKey(scope, receiptId), new ContentStoredBody(receipt, [], true), etag, token).ConfigureAwait(false))
            { sealedBody = true; break; }
        }
        if (!sealedBody) throw new ContentUnavailableException();
        await MutateAsync(scope, current =>
        {
            ContentStoredUpload fresh = current.Uploads[receiptId];
            if (!fresh.DeletionAuthorized) throw new ContentUnavailableException();
            current.Uploads[receiptId] = fresh with { Erased = true, Erasing = false, ErasureClaimId = null };
            return current;
        }, token).ConfigureAwait(false);
        return true;
    }

    private async Task RecoverPendingAsync(ContentScope scope, CancellationToken token)
    {
        ContentLedger ledger = (await ReadLedgerAsync(scope, token).ConfigureAwait(false)).Ledger;
        foreach (ContentStoredPublication pending in ledger.Publications.Values.Where(static publication => publication.Outcome == ContentPublicationOutcome.Pending))
            _ = await RecoverCoreAsync(scope, pending.Request.CommandId, token).ConfigureAwait(false);
    }

    private async Task<ContentPublicationOutcome> RecoverCoreAsync(ContentScope scope, string commandId, CancellationToken token)
    {
        ContentLedger ledger = (await ReadLedgerAsync(scope, token).ConfigureAwait(false)).Ledger;
        if (!ledger.Publications.TryGetValue(commandId, out ContentStoredPublication? publication)) throw new ContentUnavailableException();
        if (publication.Outcome != ContentPublicationOutcome.Pending) return publication.Outcome;
        ContentPublicationOutcome outcome = await _authority.GetPublicationOutcomeAsync(publication.Request, token).WaitAsync(token).ConfigureAwait(false);
        if (outcome == ContentPublicationOutcome.Pending) return outcome;
        if (outcome is not (ContentPublicationOutcome.Accepted or ContentPublicationOutcome.Rejected)) throw new ContentUnavailableException();
        if (outcome == ContentPublicationOutcome.Accepted)
        {
            foreach (ContentChange change in publication.Request.Changes.Where(static change => change.ReceiptId is not null))
                _ = await ReadBodyAsync(scope, ledger.Uploads[change.ReceiptId!].Receipt, token).ConfigureAwait(false);
        }
        await MutateAsync(scope, current =>
        {
            ContentStoredPublication fresh = current.Publications[commandId];
            if (!Equivalent(fresh.Request, publication.Request)) throw new ContentConflictException();
            if (fresh.Outcome != ContentPublicationOutcome.Pending)
            {
                if (fresh.Outcome != outcome) throw new ContentConflictException();
                return current;
            }
            if (outcome == ContentPublicationOutcome.Accepted)
            {
                foreach (ContentChange change in publication.Request.Changes)
                {
                    string incarnation = IncarnationKey(change.FileId, change.IncarnationId);
                    if (current.Tombstones.Contains(incarnation)) throw new ContentConflictException();
                    if (change.ReceiptId is null) _ = current.Tombstones.Add(incarnation);
                    else
                    {
                        ContentStoredUpload upload = current.Uploads[change.ReceiptId];
                        if (upload.Erased || upload.Erasing) throw new ContentUnavailableException();
                        current.Bindings.Add(BindingKey(change.FileId, change.IncarnationId, change.Version), change);
                    }
                }
            }
            current.Publications[commandId] = fresh with { Outcome = outcome };
            return current;
        }, token).ConfigureAwait(false);
        return outcome;
    }

    private async Task AuthorizeAsync(ClaimsPrincipal principal, ContentScope scope, ContentOperation operation, object resource, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(scope);
        token.ThrowIfCancellationRequested();
        if (principal.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException("Content operation is not authorized.");
        scope.Validate();
        await _authority.AuthorizeAsync(principal, scope, operation, resource, token).WaitAsync(token).ConfigureAwait(false);
    }

    private ContentPublicationRequest Canonicalize(ContentPublicationRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CommandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskId);
        if (request.CommandId.Length > 128 || request.TaskId.Length > 128 || request.LockFence < 1 || request.ExpectedVersion < 0
            || request.Changes is null || request.Changes.Count is < 1 || request.Changes.Count > Math.Min(_options.MaximumEntries, 1024))
            throw new ArgumentException("Invalid bounded content publication.");
        foreach (ContentChange change in request.Changes)
        {
            if (string.IsNullOrWhiteSpace(change.FileId) || change.FileId.Length > 128 || string.IsNullOrWhiteSpace(change.IncarnationId)
                || change.IncarnationId.Length > 128 || change.Version < 1 || change.ReceiptId is { } id && !IsReceiptId(id)
                || change.ReceiptId is not null && (change.ExpectedReceipt is null || change.ExpectedReceipt.Id != change.ReceiptId)
                || change.ResourceMetadata is { Count: > 16 }
                || change.ResourceMetadata?.Any(static pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 64 || pair.Value is null || pair.Value.Length > 2048) == true)
                throw new ArgumentException("Invalid immutable content change.");
        }
        if (request.Changes.Select(static change => change.FileId).Distinct(StringComparer.Ordinal).Count() != request.Changes.Count)
            throw new ContentConflictException();
        return request with { Changes = request.Changes.OrderBy(static change => change.FileId, StringComparer.Ordinal)
            .Select(static change => change with { ResourceMetadata = change.ResourceMetadata is null ? null : new SortedDictionary<string, string>(change.ResourceMetadata.ToDictionary(static pair => pair.Key, static pair => pair.Value), StringComparer.Ordinal) }).ToArray() };
    }

    private static bool Equivalent(ContentPublicationRequest first, ContentPublicationRequest second)
        => JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);

    private async Task<ContentStoredBody> ReadBodyAsync(ContentScope scope, ContentReceipt expected, CancellationToken token)
    {
        ContentStoredBody? body = (await ReadAsync<ContentStoredBody>(BodyKey(scope, expected.Id), token).ConfigureAwait(false)).Value;
        if (body is null || body.Erased || body.Receipt != expected || body.Bytes is null || body.Bytes.LongLength != expected.ByteLength
            || body.Bytes.Length > _options.MaximumBodyBytes || Convert.ToHexStringLower(SHA256.HashData(body.Bytes)) != expected.Sha256)
            throw new ContentUnavailableException();
        return body;
    }

    private async Task MutateAsync(ContentScope scope, Func<ContentLedger, ContentLedger> change, CancellationToken token)
    {
        for (int attempt = 0; attempt < _options.WriteAttempts; attempt++)
        {
            (ContentLedger ledger, string etag) = await ReadLedgerAsync(scope, token).ConfigureAwait(false);
            // Work on a detached value even when a provider/test double shares object instances.
            ContentLedger detached = JsonSerializer.Deserialize<ContentLedger>(JsonSerializer.SerializeToUtf8Bytes(ledger)) ?? throw new ContentUnavailableException();
            ContentLedger next = change(detached);
            if (await SaveAsync(LedgerKey(scope), next, etag, token).ConfigureAwait(false)) return;
        }
        throw new ContentConflictException();
    }

    private async Task<(ContentLedger Ledger, string ETag)> ReadLedgerAsync(ContentScope scope, CancellationToken token)
    {
        (ContentLedger? value, string etag) = await ReadAsync<ContentLedger>(LedgerKey(scope), token).ConfigureAwait(false);
        ContentLedger ledger = value ?? ContentLedger.Empty();
        if (ledger.Schema != 1 || ledger.Uploads is null || ledger.Publications is null || ledger.Bindings is null || ledger.Tombstones is null
            || ledger.Uploads.Count > _options.MaximumEntries || ledger.Publications.Count > _options.MaximumEntries
            || ledger.Bindings.Count > _options.MaximumEntries)
            throw new ContentUnavailableException();
        return (ledger, etag);
    }

    private async Task<(T? Value, string ETag)> ReadAsync<T>(string key, CancellationToken token)
        => await _dapr.GetStateAndETagAsync<T>(_options.StateStoreName, key, ConsistencyMode.Strong, cancellationToken: token).WaitAsync(token).ConfigureAwait(false);

    private async Task<bool> SaveAsync<T>(string key, T value, string etag, CancellationToken token)
    {
        try
        {
            return await _dapr.TrySaveStateAsync(_options.StateStoreName, key, value, etag ?? string.Empty, _cas, cancellationToken: token).WaitAsync(token).ConfigureAwait(false);
        }
        catch (Dapr.DaprException) when (string.IsNullOrEmpty(etag) && !token.IsCancellationRequested)
        {
            // Redis can report a create-only race as Internal; only an observed existing value qualifies it as contention.
            if ((await ReadAsync<T>(key, token).ConfigureAwait(false)).Value is not null) return false;
            throw;
        }
    }

    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.OperationTimeout);
        return await action(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
    }

    private static bool HasPendingReference(ContentLedger ledger, string receiptId)
        => ledger.Publications.Values.Any(publication => publication.Outcome == ContentPublicationOutcome.Pending
            && publication.Request.Changes.Any(change => change.ReceiptId == receiptId
                || ledger.Bindings.Values.Any(binding => binding.ReceiptId == receiptId && binding.FileId == change.FileId && binding.IncarnationId == change.IncarnationId)));

    private static bool IsHex(string value, int length) => value is not null && value.Length == length && value.All(static ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool IsReceiptId(string value) => value.StartsWith("content_", StringComparison.Ordinal) && IsHex(value[8..], 64);
    private static string ScopeKey(ContentScope scope) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(scope)));
    private static string LedgerKey(ContentScope scope) => "authoritative-content:metadata:" + ScopeKey(scope);
    private static string BodyKey(ContentScope scope, string receipt) => "authoritative-content:body:" + ScopeKey(scope) + ":" + receipt;
    private static string IncarnationKey(string file, string incarnation) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { file, incarnation }))));
    private static string BindingKey(string file, string incarnation, long version) => IncarnationKey(file, incarnation) + ":" + version.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
