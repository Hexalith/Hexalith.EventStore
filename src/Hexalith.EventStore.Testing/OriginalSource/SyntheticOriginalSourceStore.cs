using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ByteAether.Ulid;

using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.CatalogInvocation;

namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Durable synthetic primary authority: immutable original pages, atomic head and owner source-v1 codec.</summary>
internal sealed class SyntheticOriginalSourceStore : INativeSourceScanSource, IDisposable
{
    /// <summary>The fixed native page and append ceiling.</summary>
    internal const int MaximumEvents = 255;
    /// <summary>The complete serialized page, append and response ceiling.</summary>
    internal const int MaximumBytes = 2_097_152;
    private readonly SyntheticOriginalSourceOptions _selection;
    private readonly CatalogInvocationFiles _files;
    private readonly IDisposable _ownership;
    private readonly Action? _beforeHeadCommit;
    private readonly SemaphoreSlim _appendGate = new(1, 1);

    /// <summary>Owns the private directory exclusively and retires only verified uncommitted pages left by an interrupted owner.</summary>
    internal SyntheticOriginalSourceStore(SyntheticOriginalSourceOptions selection, Action? beforeHeadCommit = null)
    {
        _selection = selection;
        _beforeHeadCommit = beforeHeadCommit;
        _files = new(selection.Directory, MaximumBytes);
        _ownership = _files.AcquireExclusiveDirectoryOwnership();
        try { RetireUncommittedPagesAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        catch { _ownership.Dispose(); _appendGate.Dispose(); throw; }
    }

    /// <inheritdoc/>
    public bool IsSelected(AggregateIdentity identity) => identity.TenantId == _selection.Tenant
        && identity.Domain == _selection.Domain && identity.AggregateId.StartsWith(_selection.AggregatePrefix, StringComparison.Ordinal);

    /// <summary>Validates and commits only contiguous original appends. Existing bytes are compared, never replaced.</summary>
    internal async Task<bool> AppendAsync(SyntheticOriginalSourceAppend request, CancellationToken token)
    {
        var identity = new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId);
        if (!IsSelected(identity) || request.Tenant != _selection.Tenant || request.Domain != _selection.Domain
            || request.RunId != _selection.RunId || request.ExpiresAt <= DateTimeOffset.UtcNow
            || request.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(31)
            || request.Events is not { Length: > 0 and <= MaximumEvents }
            || JsonSerializer.SerializeToUtf8Bytes(request, CatalogInvocationFiles.Json).Length > MaximumBytes)
            throw new InvalidOperationException("Original-primary fixture append selection refused.");
        _ = _files.PathFor(request.Id, ".working");
        EventEnvelope[] incoming = request.Events.Select(source => Original(identity, source)).ToArray();
        for (int i = 1; i < incoming.Length; i++)
            if (incoming[i].SequenceNumber != incoming[i - 1].SequenceNumber + 1)
                throw new InvalidOperationException("Original source append is not contiguous.");
        await _appendGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            RequireCurrent(request, token);
            string resource = ResourceId(identity);
            SyntheticOriginalSourceHead? head = File.Exists(_files.PathFor(resource, ".head"))
                ? await HeadAsync(identity, token).ConfigureAwait(false) : null;
            if (head is null && Directory.GetFiles(_selection.Directory, "*.head").Length >= 8)
                throw new InvalidOperationException("Original-primary fixture resource enrollment is full.");
            NativeSourceProof before = head?.Proof ?? NativeSourceProofCodec.CreateSeed(identity, Ulid.New().ToString());
            if (incoming[0].SequenceNumber > before.HeadSequence + 1) return false;
            EventEnvelope[] existing = head is null ? [] : await RangeAsync(identity, head,
                incoming[0].SequenceNumber - 1, Math.Min(before.HeadSequence, incoming[^1].SequenceNumber), MaximumEvents, token).ConfigureAwait(false);
            int existingOrdinal = 0;
            foreach (EventEnvelope envelope in incoming.Where(item => item.SequenceNumber <= before.HeadSequence))
            {
                if (existingOrdinal >= existing.Length
                    || SourceProofCodec.ComputeEventHash(envelope) != SourceProofCodec.ComputeEventHash(existing[existingOrdinal++])) return false;
            }
            EventEnvelope[] appended = incoming.Where(item => item.SequenceNumber > before.HeadSequence).ToArray();
            if (appended.Length == 0) return true;
            NativeSourceProof after = before;
            foreach (EventEnvelope envelope in appended)
                after = NativeSourceProofCodec.ExtendDigest(after, identity, envelope.SequenceNumber, envelope.MessageId, SourceProofCodec.ComputeEventHash(envelope));
            string pageId = Guid.NewGuid().ToString("N");
            var page = new SyntheticOriginalSourcePage(_selection.RunId, head?.PageId, before, appended, after);
            SyntheticOriginalSourcePageAddress[] pages = [.. head?.Pages ?? [], new(appended[0].SequenceNumber, pageId)];
            if (pages.Length > 4096) throw new InvalidOperationException("Original-primary fixture page inventory is full.");
            bool committed = false;
            try
            {
                await _files.WriteAsync(pageId, ".page", page, token).ConfigureAwait(false);
                _beforeHeadCommit?.Invoke();
                RequireCurrent(request, token);
                await _files.WriteAsync(resource, ".head", new SyntheticOriginalSourceHead(_selection.RunId, pageId, after, pages), token,
                    replace: true, beforePublish: () => RequireCurrent(request, token)).ConfigureAwait(false);
                committed = true;
                return true;
            }
            finally
            {
                // Actual page/head work has ended. Never retire a page referenced by a committed head.
                if (!committed) await RetireUncommittedPagesAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally { _appendGate.Release(); }
    }

    /// <inheritdoc/>
    public async Task<StreamReadPage> ReadAsync(StreamReadRequest request, CancellationToken cancellationToken)
    {
        var identity = new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId!);
        if (!IsSelected(identity) || request.Tenant != _selection.Tenant || request.Domain != _selection.Domain
            || !request.RequireNativeProof || request.PageSize is < 1 or > MaximumEvents || request.FromSequence < 0
            || request.Checkpoint is not null || request.ContinuationToken is not null || request.ProjectionName is not null
            || request.ToSequence is < 0 || request.ToSequence < request.FromSequence)
            throw new InvalidOperationException("Original-primary fixture scan selection refused.");
        SyntheticOriginalSourceHead head = await HeadAsync(identity, cancellationToken).ConfigureAwait(false);
        EventEnvelope[] originals = request.FromSequence == 0 && request.ToSequence == 0 ? []
            : await RangeAsync(identity, head, request.FromSequence, Math.Min(request.ToSequence ?? head.Proof.HeadSequence,
                head.Proof.HeadSequence), request.PageSize, cancellationToken).ConfigureAwait(false);
        StreamReadEvent[] events = originals.Select(envelope => new StreamReadEvent(envelope.SequenceNumber,
            envelope.EventTypeName, envelope.Payload, envelope.SerializationFormat, envelope.MetadataVersion,
            envelope.MessageId, envelope.CorrelationId, envelope.CausationId, envelope.Timestamp, envelope.UserId)
            { GlobalPosition = envelope.GlobalPosition, PayloadVersion = envelope.PayloadVersion,
                NativeEventDigest = SourceProofCodec.ComputeEventHash(envelope) }).ToArray();
        long? last = events.Length == 0 ? null : events[^1].SequenceNumber;
        var result = new StreamReadPage(identity.TenantId, identity.Domain, identity.AggregateId, events,
            new(request.FromSequence, request.ToSequence, last, head.Proof.HeadSequence, events.Length,
                (last ?? request.FromSequence) < Math.Min(request.ToSequence ?? head.Proof.HeadSequence, head.Proof.HeadSequence), null)
            { NativeProof = head.Proof });
        var final = await HeadAsync(identity, cancellationToken).ConfigureAwait(false);
        if (JsonSerializer.SerializeToUtf8Bytes(result, CatalogInvocationFiles.Json).Length > MaximumBytes
            || final.Proof != head.Proof || final.PageId != head.PageId)
            throw new InvalidOperationException("Original-primary fixture observation changed or exceeded its bound.");
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<SyntheticOriginalSourceHead> HeadAsync(AggregateIdentity identity, CancellationToken token)
    {
        var head = await _files.ReadAsync<SyntheticOriginalSourceHead>(ResourceId(identity), ".head", token).ConfigureAwait(false);
        if (head.RunId != _selection.RunId || !NativeSourceProofCodec.IsValid(head.Proof, identity, head.Proof.HeadSequence, 1)
            || head.Proof.Erased || head.Proof.HeadSequence == 0 || head.Pages is not { Length: > 0 and <= 4096 }
            || head.Pages[0].FirstSequence != 1 || head.Pages[^1].PageId != head.PageId)
            throw new IOException("Original-primary fixture committed head is unavailable.");
        for (int index = 1; index < head.Pages.Length; index++)
            if (head.Pages[index].FirstSequence <= head.Pages[index - 1].FirstSequence
                || head.Pages[index].FirstSequence - head.Pages[index - 1].FirstSequence > MaximumEvents)
                throw new IOException("Original-primary fixture page index is invalid.");
        var page = await PageAsync(identity, head.PageId, token).ConfigureAwait(false);
        if (page.After != head.Proof) throw new IOException("Original-primary fixture head is incomplete.");
        return head;
    }

    private async Task<SyntheticOriginalSourcePage> PageAsync(AggregateIdentity identity, string pageId, CancellationToken token)
    {
        var page = await _files.ReadAsync<SyntheticOriginalSourcePage>(pageId, ".page", token).ConfigureAwait(false);
        if (page.RunId != _selection.RunId || page.Events is not { Length: > 0 and <= MaximumEvents }
            || !NativeSourceProofCodec.IsValid(page.Before, identity, page.Before.HeadSequence, 1)
            || page.Before.Erased || (page.PreviousPageId is null) != (page.Before.HeadSequence == 0))
            throw new IOException("Original-primary fixture page is invalid.");
        NativeSourceProof proof = page.Before;
        foreach (EventEnvelope envelope in page.Events)
        {
            if (envelope.Identity != identity) throw new IOException("Original-primary fixture page scope differs.");
            proof = NativeSourceProofCodec.ExtendDigest(proof, identity, envelope.SequenceNumber, envelope.MessageId, SourceProofCodec.ComputeEventHash(envelope));
        }
        if (proof != page.After) throw new IOException("Original-primary fixture original bytes differ.");
        return page;
    }

    private async Task<EventEnvelope[]> RangeAsync(AggregateIdentity identity, SyntheticOriginalSourceHead head,
        long from, long to, int maximum, CancellationToken token)
    {
        if (to <= from) return [];
        long upper = Math.Min(to, checked(from + maximum));
        List<EventEnvelope> result = [];
        int ordinal = 0;
        while (ordinal + 1 < head.Pages.Length && head.Pages[ordinal + 1].FirstSequence <= from + 1) ordinal++;
        NativeSourceProof? previous = null;
        while (ordinal < head.Pages.Length && result.Count < upper - from)
        {
            var address = head.Pages[ordinal];
            var page = await PageAsync(identity, address.PageId, token).ConfigureAwait(false);
            if (page.Events[0].SequenceNumber != address.FirstSequence
                || page.PreviousPageId != (ordinal == 0 ? null : head.Pages[ordinal - 1].PageId)
                || previous is not null && page.Before != previous
                || ordinal + 1 == head.Pages.Length && page.After != head.Proof)
                throw new IOException("Original-primary fixture page chain is incomplete.");
            result.AddRange(page.Events.Where(item => item.SequenceNumber > from && item.SequenceNumber <= upper));
            previous = page.After;
            ordinal++;
        }
        EventEnvelope[] ordered = result.OrderBy(item => item.SequenceNumber).ToArray();
        if (ordered.Length != upper - from) throw new IOException("Original-primary fixture source has a gap.");
        return ordered;
    }

    private static EventEnvelope Original(AggregateIdentity identity, ProjectionEventDto source)
    {
        if (source is null || source.Payload is null || source.SequenceNumber < 1 || string.IsNullOrWhiteSpace(source.MessageId)
            || string.IsNullOrWhiteSpace(source.EventTypeName) || source.SerializationFormat != "json"
            || source.MetadataVersion != 1 || source.StoredPayloadVersion is not null
            || source.StoredEventContractType is not null || source.StoredEventTypeName is not null
            || source.StoredSerializationFormat is not null || source.StoredDigest is not null
            || source.EffectiveEventContractType is not null || source.EffectivePayloadVersion is not null
            || source.EffectiveSerializationFormat is not null || source.EffectivePayload is not null
            || source.RegistryFingerprint is not null || source.IsAdapted is not null)
            throw new InvalidOperationException("Only original legacy synthetic envelopes are admitted by this fixture.");
        return new(source.MessageId, identity.AggregateId, identity.Domain, identity.TenantId, identity.Domain,
            source.SequenceNumber, source.GlobalPosition, source.Timestamp, source.CorrelationId, null!, source.UserId!,
            "synthetic-original-primary-v1", source.EventTypeName, source.MetadataVersion ?? 1, source.SerializationFormat,
            source.Payload, null);
    }

    private static string ResourceId(AggregateIdentity identity) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ActorId)))[..32];

    private static void RequireCurrent(SyntheticOriginalSourceAppend request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (DateTimeOffset.UtcNow >= request.ExpiresAt) throw new OperationCanceledException("Original-primary fixture append expired.");
    }

    private async Task RetireUncommittedPagesAsync(CancellationToken token)
    {
        HashSet<string> committed = new(StringComparer.Ordinal);
        string[] heads = _files.Existing(".head");
        if (heads.Length > 8) throw new IOException("Original-primary fixture resource inventory is invalid.");
        foreach (string path in heads)
        {
            string id = Path.GetFileNameWithoutExtension(path);
            var observed = await _files.ReadAsync<SyntheticOriginalSourceHead>(id, ".head", token).ConfigureAwait(false);
            var identity = new AggregateIdentity(observed.Proof.TenantId, observed.Proof.Domain, observed.Proof.AggregateId);
            if (!IsSelected(identity) || id != ResourceId(identity)) throw new IOException("Original-primary fixture head scope differs.");
            var head = await HeadAsync(identity, token).ConfigureAwait(false);
            NativeSourceProof expected = NativeSourceProofCodec.CreateSeed(identity, head.Proof.SourceIncarnation);
            string? previous = null;
            foreach (var address in head.Pages)
            {
                var page = await PageAsync(identity, address.PageId, token).ConfigureAwait(false);
                if (page.Events[0].SequenceNumber != address.FirstSequence || page.PreviousPageId != previous || page.Before != expected)
                    throw new IOException("Original-primary fixture committed page index is incomplete.");
                committed.Add(address.PageId);
                expected = page.After;
                previous = address.PageId;
            }
            if (expected != head.Proof || previous != head.PageId)
                throw new IOException("Original-primary fixture committed page chain differs from its head.");
        }
        foreach (string path in _files.Existing(".page"))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            if (committed.Contains(id)) continue;
            var observed = await _files.ReadAsync<SyntheticOriginalSourcePage>(id, ".page", token).ConfigureAwait(false);
            var identity = new AggregateIdentity(observed.After.TenantId, observed.After.Domain, observed.After.AggregateId);
            if (!IsSelected(identity)) throw new IOException("Uncommitted original-primary fixture page scope differs.");
            _ = await PageAsync(identity, id, token).ConfigureAwait(false);
            _files.Delete(id, ".page");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _appendGate.Dispose();
        _ownership.Dispose();
    }
}
