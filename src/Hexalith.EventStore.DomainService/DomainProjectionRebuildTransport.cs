using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Stores large mapped replay requests as bounded, immutable private parts.</summary>
internal static class DomainProjectionRebuildTransport
{
    private const int PartBytes = ProjectionRebuildTransportReference.PartBytes;
    private const int MaximumParts = ProjectionRebuildTransportReference.MaximumParts;
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    internal static async Task StoreAsync(IServiceProvider services,
        ProjectionRebuildTransportPartRequest part, ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(part);
        string storeName = ValidateAndGetStore(services, part.Header, options, identity);
        ProjectionRebuildTransportReference reference = part.Header.RebuildTransport!;
        if (part.Ordinal < 0 || part.Ordinal >= reference.PartCount
            || part.Data is null || part.Data.Length == 0 || part.Data.Length > PartBytes)
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);

        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        string key = Key(part.Header, part.Ordinal);
        var storedPart = new DomainProjectionStoredReplayPart(part.Header, part.Ordinal, part.Data)
            { ReplayContext = ProjectionReplayTransportContext.Capture(part.Header.Request) };
        IReadModelExpiringStore? expiring = services.GetService<IReadModelExpiringStore>();
        bool saved = expiring is null
            ? await store.TrySaveAsync(storeName, key, storedPart, string.Empty, cancellationToken).ConfigureAwait(false)
            : await expiring.TrySaveWithTimeToLiveAsync(storeName, key, storedPart, string.Empty,
                TimeSpan.FromDays(7), cancellationToken).ConfigureAwait(false);
        if (saved)
            return;
        ProjectionRebuildTransportPartRequest? existing = (await store
            .GetAsync<DomainProjectionStoredReplayPart>(storeName, key, cancellationToken)
            .ConfigureAwait(false)).Value?.Restore();
        if (existing is null || existing.Ordinal != part.Ordinal
            || !HeaderMatches(existing.Header, part.Header)
            || !existing.Data.AsSpan().SequenceEqual(part.Data))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
    }

    internal static async Task<ProjectionDispatchRequest> ResolveAsync(IServiceProvider services,
        ProjectionDispatchRequest header, ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identity, CancellationToken cancellationToken)
    {
        if (header.RebuildTransport is null) return header;
        string storeName = ValidateAndGetStore(services, header, options, identity);
        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        using var stream = new PartReadStream(store, storeName, header);
        ProjectionEventDto[] events = await JsonSerializer.DeserializeAsync<ProjectionEventDto[]>(
            stream, s_json, cancellationToken).ConfigureAwait(false)
            ?? throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        await stream.CompleteAsync(cancellationToken).ConfigureAwait(false);
        if (!StringComparer.Ordinal.Equals(stream.Digest, header.RebuildTransport.Digest))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        if (events.Length > options.MaxRebuildEventCount)
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        return header with { Request = header.Request with { Events = events }, RebuildTransport = null };
    }

    internal static async IAsyncEnumerable<ProjectionEventDto> ReadEventsAsync(IServiceProvider services,
        ProjectionDispatchRequest header, ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identity,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string storeName = ValidateAndGetStore(services, header, options, identity);
        using var stream = new PartReadStream(services.GetRequiredService<IReadModelStore>(), storeName, header);
        EventPayloadEvolutionRegistry? evolution = services.GetService<EventPayloadEvolutionRegistry>();
        int count = 0;
        string? headMessageId = null;
        await foreach (ProjectionEventDto? item in JsonSerializer.DeserializeAsyncEnumerable<ProjectionEventDto>(
            stream, s_json, cancellationToken).ConfigureAwait(false))
        {
            if (item is null || ++count > options.MaxRebuildEventCount
                || item.SequenceNumber != count || item.Payload is null
                || string.IsNullOrWhiteSpace(item.EventTypeName)
                || string.IsNullOrWhiteSpace(item.SerializationFormat))
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
            DomainProjectionEvolutionAdmission.RequireLegacyEvents([item]);
            headMessageId = item.MessageId;
            yield return DomainProjectionDispatcher.UpcastEvent(evolution, item);
        }
        await stream.CompleteAsync(cancellationToken).ConfigureAwait(false);
        if (!StringComparer.Ordinal.Equals(stream.Digest, header.RebuildTransport!.Digest))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        if (header.Request.RebuildSourceHeadSequence != count
            || header.Request.RebuildSourceHeadMessageId is { } expectedMessageId && expectedMessageId != headMessageId
            || header.Request.RebuildSourceErased != (count == 0))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
    }

    internal static async Task ReclaimAsync(IServiceProvider services, ProjectionDispatchRequest header,
        ProjectionDispatchOptions options, DomainProjectionIdentityOptions identity,
        CancellationToken cancellationToken)
    {
        if (header.RebuildTransport is null) return;
        string storeName = ValidateAndGetStore(services, header, options, identity);
        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        IReadModelConditionalEraser eraser = services.GetRequiredService<IReadModelConditionalEraser>();
        for (int ordinal = 0; ordinal < header.RebuildTransport.PartCount; ordinal++)
        {
            string key = Key(header, ordinal);
            ReadModelEntry<ProjectionRebuildTransportPartRequest> part = await store
                .GetAsync<ProjectionRebuildTransportPartRequest>(storeName, key, cancellationToken).ConfigureAwait(false);
            if (part.Value is not null && part.ETag is { Length: > 0 }
                && !await eraser.TryEraseAsync(storeName, key, part.ETag, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Mapped replay private transport cleanup is incomplete.");
        }
        // The compact original-concurrency plan is kept for its bounded TTL. If the
        // caller loses the verify response before saving its checkpoint, it can
        // re-upload the immutable source parts and reconcile the same operation.
    }

    internal static async Task<ReadModelBatch> SavePlanAsync(IServiceProvider services,
        ProjectionDispatchRequest header, ReadModelBatch batch, ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identity, CancellationToken cancellationToken)
    {
        string storeName = ValidateAndGetStore(services, header, options, identity);
        ProjectionRebuildTransportPlan plan = new(header.RebuildTransport!.Digest,
            [.. batch.Operations.Select(operation => new ProjectionRebuildTransportPlanEntry(
                operation.Key, operation.Concurrency))]);
        if (JsonSerializer.SerializeToUtf8Bytes(plan, s_json).Length > PartBytes)
            throw new ProjectionDispatchValidationException(
                ProjectionDispatchReasonCodes.MalformedOutcome);
        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        string key = Key(header, -1);
        IReadModelExpiringStore? expiring = services.GetService<IReadModelExpiringStore>();
        bool saved = expiring is null
            ? await store.TrySaveAsync(storeName, key, plan, string.Empty, cancellationToken)
                .ConfigureAwait(false)
            : await expiring.TrySaveWithTimeToLiveAsync(storeName, key, plan, string.Empty,
                TimeSpan.FromDays(7), cancellationToken).ConfigureAwait(false);
        return saved ? batch : await RestorePlanAsync(services, header, batch,
            options, identity, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<ReadModelBatch> RestorePlanAsync(IServiceProvider services,
        ProjectionDispatchRequest header, ReadModelBatch batch, ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identity, CancellationToken cancellationToken)
    {
        string storeName = ValidateAndGetStore(services, header, options, identity);
        ProjectionRebuildTransportPlan? plan = (await services.GetRequiredService<IReadModelStore>()
            .GetAsync<ProjectionRebuildTransportPlan>(storeName, Key(header, -1), cancellationToken)
            .ConfigureAwait(false)).Value;
        if (plan is null || !StringComparer.Ordinal.Equals(plan.Digest, header.RebuildTransport!.Digest)
            || plan.Operations.Length != batch.Operations.Count)
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryGap);
        ReadModelBatchOperation[] restored = new ReadModelBatchOperation[plan.Operations.Length];
        for (int ordinal = 0; ordinal < restored.Length; ordinal++) {
            if (!StringComparer.Ordinal.Equals(plan.Operations[ordinal].Key,
                batch.Operations[ordinal].Key))
                throw new ProjectionDispatchValidationException(
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            restored[ordinal] = batch.Operations[ordinal]
                .WithConcurrency(plan.Operations[ordinal].Concurrency);
        }
        return new ReadModelBatch(batch.Scope, restored) { OwnerSelection = batch.OwnerSelection, OwnerSelections = batch.OwnerSelections };
    }

    internal static string ValidateAndGetStore(IServiceProvider services, ProjectionDispatchRequest header,
        ProjectionDispatchOptions options, DomainProjectionIdentityOptions identity)
    {
        if (header.Request is null || header.Request.Events is not { Length: 0 }
            || header.RebuildTransport is not { PartCount: > 0 and <= MaximumParts } reference
            || reference.Digest is not { Length: 64 }
            || !reference.Digest.All(static character => char.IsAsciiHexDigit(character))
            || string.IsNullOrWhiteSpace(header.DispatchId))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        _ = new AggregateIdentity(header.Request.TenantId, header.Request.Domain, header.Request.AggregateId);
        DomainProjectionEvolutionAdmission.RequireLegacy(header.Request);
        IAsyncDomainProjectionHandler[] allHandlers = [.. DomainProjectionHandlerRouteValidator
            .MaterializeAndValidateNamed(services.GetServices<IAsyncDomainProjectionHandler>(), options)];
        ProjectionDispatchRoute[] routes = [.. allHandlers
            .Where(handler => StringComparer.Ordinal.Equals(handler.Domain, header.Request.Domain))
            .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        if (routes.Length == 0 || !DomainProjectionOwnerProof.MatchesCatalog(services, identity.AppId, identity.ServiceVersion, routes,
                allHandlers.Where(handler => handler.OrdinaryDeliveryEnabled
                    && StringComparer.Ordinal.Equals(handler.Domain, header.Request.Domain))
                    .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType)),
            header.CatalogFingerprint))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        HashSet<string> types = new(header.ProjectionTypes, StringComparer.Ordinal);
        if (types.Count == 0 || types.Count != header.ProjectionTypes.Count)
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
        IAsyncDomainProjectionHandler[] selected = [.. allHandlers.Where(handler =>
            StringComparer.Ordinal.Equals(handler.Domain, header.Request.Domain)
            && types.Contains(handler.ProjectionType))];
        if (selected.Length != types.Count || selected.Any(handler => handler is not IAsyncDomainProjectionRebuildHandler))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
        // A guarded mixed catalog uploads one canonical prefix for one coordinated
        // batch. Full-replay siblings use their already enrolled owning store;
        // mapped siblings retain their explicit streaming transport capability.
        bool guarded = DomainProjectionOwnerProof.UsesGuardedCatalog(services, header, identity);
        string?[] stores = [.. selected.Select(handler => handler is IAsyncDomainProjectionRebuildTransportHandler transport
            ? transport.RebuildTransportStoreName : guarded ? services.GetServices<DomainProjectionOwnerProofRegistration>()
                .SingleOrDefault(item => item.Domain == header.Request.Domain && item.ProjectionType == handler.ProjectionType)?.Binding.StoreName
                : null).Distinct(StringComparer.Ordinal)];
        if (stores.Length != 1 || string.IsNullOrWhiteSpace(stores[0]))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        return stores[0]!;
    }

    internal static string Key(ProjectionDispatchRequest header, int ordinal)
    {
        string identity = string.Join('\0', header.Request.TenantId, header.Request.Domain,
            header.Request.AggregateId, header.DispatchId, header.CatalogFingerprint);
        return "mapped-rebuild-transport:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            + ":" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool HeaderMatches(ProjectionDispatchRequest left, ProjectionDispatchRequest right)
        => left.Request is { Events: { Length: 0 } } && right.Request is { Events: { Length: 0 } }
            && StringComparer.Ordinal.Equals(left.Request.TenantId, right.Request.TenantId)
            && StringComparer.Ordinal.Equals(left.Request.Domain, right.Request.Domain)
            && StringComparer.Ordinal.Equals(left.Request.AggregateId, right.Request.AggregateId)
            && left.Request.RebuildSourceHeadSequence == right.Request.RebuildSourceHeadSequence
            && left.Request.RebuildSourceHeadMessageId == right.Request.RebuildSourceHeadMessageId
            && Equals(ProjectionReplayTransportContext.CaptureCanonical(left.Request),
                ProjectionReplayTransportContext.CaptureCanonical(right.Request))
            && left.Request.RebuildSourceErased == right.Request.RebuildSourceErased
            && StringComparer.Ordinal.Equals(left.DispatchId, right.DispatchId)
            && StringComparer.Ordinal.Equals(left.CatalogFingerprint, right.CatalogFingerprint)
            && Equals(left.RebuildTransport, right.RebuildTransport)
            && left.ProjectionTypes.SequenceEqual(right.ProjectionTypes, StringComparer.Ordinal);

    private sealed record ProjectionRebuildTransportPlan(
        string Digest, ProjectionRebuildTransportPlanEntry[] Operations);

    private sealed record ProjectionRebuildTransportPlanEntry(
        string Key, ReadModelBatchConcurrency Concurrency);

    private sealed class PartReadStream(IReadModelStore store, string storeName,
        ProjectionDispatchRequest header) : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private byte[]? _part;
        private int _partOffset;
        private int _ordinal;

        internal string Digest => Convert.ToHexString(_hash.GetHashAndReset());

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty) return 0;
            if (_part is null || _partOffset == _part.Length)
            {
                if (_ordinal == header.RebuildTransport!.PartCount) return 0;
                ProjectionRebuildTransportPartRequest? stored = (await store
                    .GetAsync<DomainProjectionStoredReplayPart>(storeName, Key(header, _ordinal),
                        cancellationToken).ConfigureAwait(false)).Value?.Restore();
                if (stored is null || stored.Ordinal != _ordinal || !HeaderMatches(stored.Header, header)
                    || stored.Data is null || stored.Data.Length == 0 || stored.Data.Length > PartBytes)
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryGap);
                _part = stored.Data;
                _partOffset = 0;
                _ordinal++;
            }

            int count = Math.Min(buffer.Length, _part.Length - _partOffset);
            ReadOnlySpan<byte> bytes = _part.AsSpan(_partOffset, count);
            bytes.CopyTo(buffer.Span);
            _hash.AppendData(bytes);
            _partOffset += count;
            return count;
        }

        internal async Task CompleteAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[8192];
            while (await ReadAsync(buffer, cancellationToken).ConfigureAwait(false) != 0) { }
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _hash.Dispose();
            base.Dispose(disposing);
        }
    }
}
