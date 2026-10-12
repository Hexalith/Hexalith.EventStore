using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Sends one SDK shared-rebuild transition to the admitted domain service.</summary>
public interface ISharedProjectionRebuildGateway
{
    /// <summary>Returns the durable domain-service outcome or throws on an unknown transport outcome.</summary>
    Task<DomainSharedProjectionRebuildResponse> SendAsync(DomainSharedProjectionRebuildRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Authenticated Dapr invocation of the existing DomainService shared rebuild endpoint.</summary>
public sealed class DaprSharedProjectionRebuildGateway(DaprClient dapr, IHttpClientFactory clients,
    IDomainServiceResolver resolver) : ISharedProjectionRebuildGateway
{
    internal const int MaximumOwnerRequestBytes = 2 * 1_048_576;

    /// <inheritdoc/>
    public async Task<DomainSharedProjectionRebuildResponse> SendAsync(
        DomainSharedProjectionRebuildRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DomainServiceRegistration registration = await resolver.ResolveAsync(
            request.Identity.TenantId, request.Identity.Domain, "v1", cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Shared rebuild domain service is unavailable.");
        if (!string.Equals(registration.TenantId, request.Identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(registration.Domain, request.Identity.Domain, StringComparison.Ordinal)
            || !string.Equals(registration.Version, "v1", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(registration.AppId))
            throw new InvalidOperationException("Shared rebuild domain service registration does not match its exact route.");
        using HttpRequestMessage message = dapr.CreateInvokeMethodRequest(
            registration.AppId, "project/rebuild/shared/v1", request);
        if (request.Action is DomainSharedProjectionRebuildAction.Accumulate
            or DomainSharedProjectionRebuildAction.AccumulatePart)
        {
            if (message.Content is null)
                throw new InvalidOperationException("Shared rebuild owner request has no HTTP body.");
            // Check the bytes emitted by the Dapr serializer, including the request envelope.
            await BufferBoundedOwnerRequestAsync(message.Content, cancellationToken).ConfigureAwait(false);
        }
        using HttpResponseMessage response = await clients.CreateClient(DaprDomainServiceInvoker.HttpClientName)
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        DomainSharedProjectionRebuildResponse result = await ReadBoundedResponseAsync(
            response.Content, cancellationToken).ConfigureAwait(false);
        if (result.Version != DomainSharedProjectionRebuildProtocol.Version)
            throw new InvalidOperationException("Shared rebuild response protocol mismatch.");
        return result;
    }

    internal static async Task<DomainSharedProjectionRebuildResponse> ReadBoundedResponseAsync(
        HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 1_048_576)
            throw new InvalidOperationException("Shared rebuild response exceeds its envelope limit.");
        using Stream body = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var bounded = new MemoryStream();
        byte[] buffer = new byte[16_384];
        while (true)
        {
            int count = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (bounded.Length + count > 1_048_576)
                throw new InvalidOperationException("Shared rebuild response exceeds its envelope limit.");
            await bounded.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }

        bounded.Position = 0;
        DomainSharedProjectionRebuildResponse result = await JsonSerializer.DeserializeAsync<DomainSharedProjectionRebuildResponse>(
            bounded, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Shared rebuild response is empty.");
        return result;
    }

    internal static Task BufferBoundedOwnerRequestAsync(HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumOwnerRequestBytes)
            throw new InvalidOperationException("Shared rebuild owner request exceeds its HTTP envelope limit.");
        return content.LoadIntoBufferAsync(MaximumOwnerRequestBytes, cancellationToken);
    }
}

/// <summary>Operator contract for an actor-grounded shared projection rebuild.</summary>
public interface ISharedProjectionSourceRebuildOperator
{
    /// <summary>Resumes or starts an exact tenant/domain shared rebuild operation.</summary>
    Task<DomainSharedProjectionRebuildResponse> RebuildAsync(DomainSharedProjectionRebuildIdentity identity,
        CancellationToken cancellationToken = default);
}

/// <summary>Produces a source-actor-grounded Begin and completes the partitioned SDK lifecycle.</summary>
public sealed class SharedProjectionSourceBeginProducer(
    IActorProxyFactory proxies,
    IProjectionDeliveryHistoryReader histories,
    ISharedProjectionRebuildGateway gateway,
    IOptions<EventStoreActorOptions> actorOptions,
    IOptions<SharedProjectionSourceCaptureOptions> captureOptions) : ISharedProjectionSourceRebuildOperator
{
    private const int MaxOwners = 10_000;
    private const int MaxPartRequestBytes = DaprSharedProjectionRebuildGateway.MaximumOwnerRequestBytes;
    private const int BeginPageOwners = 200;

    // Includes the current durable actor page and the bounded sorted selection.
    internal int PeakRetainedOwnerMetadata { get; private set; }

    /// <summary>Captures all installed owners under a held write fence and drives a normal shared rebuild.</summary>
    public async Task<DomainSharedProjectionRebuildResponse> RebuildAsync(
        DomainSharedProjectionRebuildIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!captureOptions.Value.RequiresRegistration(identity.Domain))
            throw new InvalidOperationException("Shared source capture requires mandatory writer enforcement for this domain.");
        SharedProjectionSourceScope scope = new(identity.TenantId, identity.Domain);
        ISharedProjectionSourceRosterActor roster = proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(
            new ActorId(scope.ActorId), SharedProjectionSourceRosterActor.ActorTypeName);
        SharedProjectionSourceCaptureProgress? progress = await roster.ReadCaptureProgressAsync(scope,
            identity.OperationId).ConfigureAwait(false);
        if (progress?.Completed == true)
        {
            DomainSharedProjectionRebuildResponse completed = await gateway.SendAsync(new(
                DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Commit, identity),
                cancellationToken).ConfigureAwait(false);
            RequireSuccess(completed, DomainSharedProjectionRebuildPhase.Committed);
            return completed;
        }
        long? revision = await roster.BeginCaptureAsync(scope, identity.OperationId).ConfigureAwait(false);
        if (revision is null)
        {
            IReadOnlyList<SharedProjectionSourcePendingWrite>? pending = await roster.ReadPendingWritesAsync(
                scope, identity.OperationId).ConfigureAwait(false);
            if (pending is { Count: > 0 })
            {
                foreach (SharedProjectionSourcePendingWrite write in pending)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IAggregateActor aggregate = proxies.CreateActorProxy<IAggregateActor>(
                        new ActorId(write.Identity.ActorId), actorOptions.Value.AggregateActorTypeName);
                    AggregateStreamMetadata head = await aggregate.GetStreamMetadataAsync().ConfigureAwait(false);
                    if (head.CurrentSequence is < 0 or > 10_000 || head.Erased && head.Exists
                        || !head.Exists && head.CurrentSequence != 0)
                        throw new InvalidOperationException("Pending source write has no valid durable actor head.");
                    if (!await roster.CompleteWriteAsync(write.Identity, write.IntentId).ConfigureAwait(false))
                        throw new InvalidOperationException("Pending source write could not be reconciled.");
                }

                revision = await roster.BeginCaptureAsync(scope, identity.OperationId).ConfigureAwait(false);
            }
        }
        if (revision is null) throw new InvalidOperationException("A certified source capture is unavailable.");
        progress ??= await roster.ReadCaptureProgressAsync(scope, identity.OperationId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Source capture progress is unavailable.");
        if (progress.CaptureRevision != revision) throw new InvalidOperationException("Source capture revision changed.");
        if (progress.PageCount is < 64 or > 74)
            throw new InvalidOperationException("Source capture page count is invalid.");

        if (!progress.Sealed)
        {
            try
            {
                int capturedCount = 0;
                for (int partition = 0; partition < progress.PageCount; partition++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SharedProjectionSourceRosterPage page = await roster.ReadPageAsync(scope, identity.OperationId, partition)
                        .ConfigureAwait(false) ?? throw new InvalidOperationException("Source capture was lost.");
                    if (page.Revision != revision || page.Owners is null || page.Owners.Count > 1000)
                        throw new InvalidOperationException("Source roster partition conflicts with the held cut.");
                    ValidateOwners(page.Owners, scope);
                    capturedCount = checked(capturedCount + page.Owners.Count);
                    if (capturedCount > MaxOwners) throw new InvalidOperationException("Source roster exceeds the shared rebuild limit.");
                    var captured = new List<SharedProjectionCapturedSourceOwner>(page.Owners.Count);
                    PeakRetainedOwnerMetadata = Math.Max(PeakRetainedOwnerMetadata, page.Owners.Count + captured.Count);
                    foreach (AggregateIdentity owner in page.Owners)
                    {
                        IAggregateActor aggregate = proxies.CreateActorProxy<IAggregateActor>(
                            new ActorId(owner.ActorId), actorOptions.Value.AggregateActorTypeName);
                        AggregateStreamMetadata metadata = await aggregate.GetStreamMetadataAsync().ConfigureAwait(false);
                        if (metadata.CurrentSequence is < 0 or > 10_000 || metadata.Erased && metadata.Exists
                            || !metadata.Exists && metadata.CurrentSequence != 0)
                            throw new InvalidOperationException("Authoritative owner metadata is invalid.");
                        (string historyFingerprint, long lastPosition) = await ScanHistoryAsync(
                            owner, metadata.Erased, metadata.CurrentSequence, cancellationToken).ConfigureAwait(false);
                        if (metadata.ErasedPosition < 0 || !metadata.Erased && metadata.ErasedPosition != 0)
                            throw new InvalidOperationException("Authoritative erasure position is invalid.");
                        captured.Add(new(owner, metadata.CurrentSequence,
                            metadata.Erased ? metadata.ErasedPosition : lastPosition,
                            metadata.Erased, historyFingerprint));
                    }
                    PeakRetainedOwnerMetadata = Math.Max(PeakRetainedOwnerMetadata, page.Owners.Count + captured.Count);
                    if (!await roster.WriteCapturedPageAsync(scope, identity.OperationId, partition, captured)
                        .ConfigureAwait(false))
                        throw new InvalidOperationException("Source capture partition could not be persisted.");
                }
                if (capturedCount != progress.OwnerCount)
                    throw new InvalidOperationException("Source roster count conflicts with the held cut.");
                string initialFingerprint = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
                int initialOrdinal = 0;
                string? after = null;
                while (initialOrdinal < capturedCount)
                {
                    IReadOnlyList<SharedProjectionCapturedSourceOwner> sorted = await ReadSortedCapturedPageAsync(
                        roster, scope, identity.OperationId, revision.Value, progress.PageCount,
                        after, BeginPageOwners, cancellationToken).ConfigureAwait(false);
                    if (sorted.Count == 0) throw new InvalidOperationException("Source capture omitted an owner.");
                    foreach (SharedProjectionCapturedSourceOwner item in sorted)
                    {
                        initialFingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(initialFingerprint,
                            initialOrdinal++, item.HistoryFingerprint!);
                        after = item.Owner.AggregateId;
                    }
                }
                if (!await roster.SealCaptureAsync(scope, identity.OperationId, initialFingerprint,
                    identity.ProjectionType, identity.CatalogFingerprint).ConfigureAwait(false))
                    throw new InvalidOperationException("Source capture could not be sealed.");
                progress = await roster.ReadCaptureProgressAsync(scope, identity.OperationId).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Sealed source capture is unavailable.");
            }
            catch
            {
                // No SDK Begin can exist before seal. The actor keeps a failed abort
                // recoverable under the same operation ID after a process restart.
                _ = await roster.AbortUnsealedCaptureAsync(scope, identity.OperationId).ConfigureAwait(false);
                throw;
            }
        }

        if (string.IsNullOrWhiteSpace(progress.Fingerprint))
            throw new InvalidOperationException("Sealed source capture has no fingerprint.");
        string fingerprint = progress.Fingerprint;
        int expectedCount = progress.OwnerCount;
        if (!progress.OwnersAccumulated)
        {
            if (expectedCount < 0 || expectedCount > MaxOwners)
                throw new InvalidOperationException("Sealed source capture owner count conflicts.");
            int partCount = expectedCount == 0 ? 0 : checked((expectedCount + BeginPageOwners - 1) / BeginPageOwners);
            int sentCount = 0;
            string? after = null;
            Dictionary<string, long>? singleHeads = null;
            List<string>? singleErased = null;
            for (int partOrdinal = 0; partOrdinal < partCount; partOrdinal++)
            {
                IReadOnlyList<SharedProjectionCapturedSourceOwner> sorted = await ReadSortedCapturedPageAsync(
                    roster, scope, identity.OperationId, revision.Value, progress.PageCount,
                    after, BeginPageOwners, cancellationToken).ConfigureAwait(false);
                int expectedPageCount = Math.Min(BeginPageOwners, expectedCount - sentCount);
                if (sorted.Count != expectedPageCount)
                    throw new InvalidOperationException("Sealed source capture owner count conflicts.");
                var heads = new Dictionary<string, long>(sorted.Count, StringComparer.Ordinal);
                var erased = new List<string>();
                foreach (SharedProjectionCapturedSourceOwner item in sorted)
                {
                    heads.Add(item.Owner.AggregateId, item.Position);
                    if (item.IsErased) erased.Add(item.Owner.AggregateId);
                    after = item.Owner.AggregateId;
                }
                if (partCount == 1)
                {
                    singleHeads = heads;
                    singleErased = erased;
                    sentCount += sorted.Count;
                    continue;
                }
                var part = new DomainSharedProjectionRebuildRequest(
                    DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.BeginPart,
                    identity, SourceHighWatermarks: heads, ErasedOwnerIds: erased, PartOrdinal: partOrdinal);
                if (JsonSerializer.SerializeToUtf8Bytes(part).Length > 524_288)
                    throw new InvalidOperationException("A captured Begin inventory page exceeds the transport bound.");
                DomainSharedProjectionRebuildResponse accepted = await gateway.SendAsync(part,
                    cancellationToken).ConfigureAwait(false);
                if (accepted.Status == ProjectionDispatchStatus.Failed)
                    _ = await roster.AbortRejectedCaptureAsync(scope, identity.OperationId).ConfigureAwait(false);
                RequireSuccess(accepted, DomainSharedProjectionRebuildPhase.Accumulating);
                sentCount += sorted.Count;
            }
            DomainSharedProjectionRebuildResponse begun = await gateway.SendAsync(partCount <= 1
                ? new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
                    identity, CaptureInventoryFingerprint: fingerprint,
                    SourceHighWatermarks: singleHeads ?? new Dictionary<string, long>(),
                    ErasedOwnerIds: singleErased ?? [])
                : new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
                    identity, ExpectedAggregateCount: expectedCount,
                    CaptureInventoryFingerprint: fingerprint, PartCount: partCount), cancellationToken)
                .ConfigureAwait(false);
            if (begun.Status == ProjectionDispatchStatus.Failed)
                _ = await roster.AbortRejectedCaptureAsync(scope, identity.OperationId).ConfigureAwait(false);
            RequireSuccess(begun, DomainSharedProjectionRebuildPhase.Accumulating);
            if (!await roster.ReleaseAppendFenceAsync(scope, identity.OperationId).ConfigureAwait(false))
                throw new InvalidOperationException("Sealed source capture could not release ordinary appends.");
            string recalculated = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
            int ordinal = 0;
            after = null;
            while (ordinal < expectedCount)
            {
                IReadOnlyList<SharedProjectionCapturedSourceOwner> sorted = await ReadSortedCapturedPageAsync(
                    roster, scope, identity.OperationId, revision.Value, progress.PageCount,
                    after, BeginPageOwners, cancellationToken).ConfigureAwait(false);
                if (sorted.Count == 0) throw new InvalidOperationException("Sealed source capture omitted an owner.");
                foreach (SharedProjectionCapturedSourceOwner owner in sorted)
                {
                    string historyFingerprint = await SendOwnerPartsAsync(identity, ordinal, owner,
                        cancellationToken).ConfigureAwait(false);
                    recalculated = DomainSharedProjectionRebuildFingerprint.AppendInventory(recalculated, ordinal++,
                        historyFingerprint);
                    after = owner.Owner.AggregateId;
                }
            }
            if (recalculated != fingerprint)
                throw new InvalidOperationException("Source prefix conflicts with the sealed inventory.");
            if (!await roster.MarkOwnersAccumulatedAsync(scope, identity.OperationId).ConfigureAwait(false))
                throw new InvalidOperationException("Accumulated owner inventory could not release the erasure hold.");
        }
        DomainSharedProjectionRebuildResponse current = await gateway.SendAsync(new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize, identity,
            ExpectedAggregateCount: expectedCount, ExpectedInventoryFingerprint: fingerprint),
            cancellationToken).ConfigureAwait(false);
        RequireAtLeast(current, DomainSharedProjectionRebuildPhase.Finalized);
        current = await gateway.SendAsync(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Stage, identity), cancellationToken).ConfigureAwait(false);
        RequireAtLeast(current, DomainSharedProjectionRebuildPhase.Prepared);
        current = await gateway.SendAsync(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Commit, identity), cancellationToken).ConfigureAwait(false);
        RequireSuccess(current, DomainSharedProjectionRebuildPhase.Committed);
        if (!await roster.CompleteCaptureAsync(scope, identity.OperationId).ConfigureAwait(false))
            throw new InvalidOperationException("Committed source capture could not close its recovery state.");
        return current;
    }

    private static void RequireSuccess(DomainSharedProjectionRebuildResponse response,
        DomainSharedProjectionRebuildPhase phase)
    {
        if (response.Phase != phase || response.Status is not
            (ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted))
            throw new InvalidOperationException("Shared rebuild did not reach its required durable phase.");
    }

    private static void RequireAtLeast(DomainSharedProjectionRebuildResponse response,
        DomainSharedProjectionRebuildPhase phase)
    {
        if (response.Phase < phase || response.Phase == DomainSharedProjectionRebuildPhase.Aborted
            || response.Status is not (ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted))
            throw new InvalidOperationException("Shared rebuild did not reach its required durable phase.");
    }

    private static void ValidateOwners(IReadOnlyList<AggregateIdentity> owners, SharedProjectionSourceScope scope)
    {
        if (owners.Distinct().Count() != owners.Count
            || owners.Any(owner => owner is null || owner.TenantId != scope.TenantId || owner.Domain != scope.Domain))
            throw new InvalidOperationException("Source roster is malformed or incomplete.");
    }

    private async Task<IReadOnlyList<SharedProjectionCapturedSourceOwner>> ReadSortedCapturedPageAsync(
        ISharedProjectionSourceRosterActor roster, SharedProjectionSourceScope scope, string operationId,
        long revision, int pageCount, string? after, int limit, CancellationToken cancellationToken)
    {
        var selected = new List<SharedProjectionCapturedSourceOwner>(limit);
        for (int partition = 0; partition < pageCount; partition++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SharedProjectionCapturedSourcePage page = await roster.ReadCapturedPageAsync(
                scope, operationId, partition).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Source capture page is unavailable.");
            if (page.CaptureRevision != revision || page.Owners.Count > 1000)
                throw new InvalidOperationException("Source capture page conflicts with its revision.");
            PeakRetainedOwnerMetadata = Math.Max(PeakRetainedOwnerMetadata, page.Owners.Count + selected.Count);
            foreach (SharedProjectionCapturedSourceOwner item in page.Owners)
            {
                if (item.Owner is null || item.Owner.TenantId != scope.TenantId || item.Owner.Domain != scope.Domain
                    || string.IsNullOrWhiteSpace(item.HistoryFingerprint))
                    throw new InvalidOperationException("Source capture page is malformed.");
                string id = item.Owner.AggregateId;
                if (after is not null && StringComparer.Ordinal.Compare(id, after) <= 0) continue;
                int index = selected.BinarySearch(item, CapturedOwnerComparer.Instance);
                if (index >= 0) throw new InvalidOperationException("Source capture has a duplicate owner.");
                index = ~index;
                if (index >= limit) continue;
                selected.Insert(index, item);
                if (selected.Count > limit) selected.RemoveAt(limit);
            }
        }

        PeakRetainedOwnerMetadata = Math.Max(PeakRetainedOwnerMetadata, 1000 + selected.Count);
        return selected;
    }

    private sealed class CapturedOwnerComparer : IComparer<SharedProjectionCapturedSourceOwner>
    {
        public static readonly CapturedOwnerComparer Instance = new();

        public int Compare(SharedProjectionCapturedSourceOwner? left, SharedProjectionCapturedSourceOwner? right)
            => StringComparer.Ordinal.Compare(left?.Owner.AggregateId, right?.Owner.AggregateId);
    }

    private async Task<(string Fingerprint, long LastPosition)> ScanHistoryAsync(
        AggregateIdentity owner, bool isErased, long sequence, CancellationToken cancellationToken)
    {
        using var fingerprint = new DomainSharedProjectionHistoryFingerprintBuilder(
            owner.AggregateId, isErased, isErased ? 0 : sequence);
        if (isErased) return (fingerprint.Finish(), 0);
        long expectedSequence = 1;
        long priorPosition = 0;
        await foreach (ProjectionEventDto[] page in ReadHistoryPagesAsync(owner, sequence, cancellationToken)
            .ConfigureAwait(false))
        {
            foreach (ProjectionEventDto item in page)
            {
                if (item.SequenceNumber != expectedSequence || item.GlobalPosition <= priorPosition)
                    throw new InvalidOperationException("Source history has an invalid authoritative position.");
                fingerprint.Append(item);
                expectedSequence++;
                priorPosition = item.GlobalPosition;
            }
        }

        if (expectedSequence - 1 != sequence)
            throw new InvalidOperationException("Source history does not reach its captured head.");
        return (fingerprint.Finish(), priorPosition);
    }

    private async Task<string> SendOwnerPartsAsync(DomainSharedProjectionRebuildIdentity identity,
        int ownerOrdinal, SharedProjectionCapturedSourceOwner owner, CancellationToken cancellationToken)
    {
        using var fingerprint = new DomainSharedProjectionHistoryFingerprintBuilder(
            owner.Owner.AggregateId, owner.IsErased, owner.IsErased ? 0 : owner.Sequence,
            owner.HistoryFingerprint);
        long expectedSequence = 1;
        long priorPosition = 0;
        var buffered = new List<ProjectionEventDto>(256);
        int bufferedEventBytes = 0;
        int partOrdinal = 0;
        bool chunked = false;

        DomainSharedProjectionRebuildRequest PartRequest(ProjectionEventDto[] events) => new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
            identity, ownerOrdinal, owner.Owner.AggregateId, Events: events, PartOrdinal: partOrdinal);

        async Task SendPartAsync()
        {
            if (buffered.Count == 0) return;
            DomainSharedProjectionRebuildRequest part = PartRequest([.. buffered]);
            if (JsonSerializer.SerializeToUtf8Bytes(part).Length > MaxPartRequestBytes)
                throw new InvalidOperationException("Owner transport part exceeds its HTTP envelope limit.");
            DomainSharedProjectionRebuildResponse sent = await gateway.SendAsync(part, cancellationToken)
                .ConfigureAwait(false);
            RequireSuccess(sent, DomainSharedProjectionRebuildPhase.Accumulating);
            partOrdinal++;
            buffered.Clear();
            bufferedEventBytes = 0;
            chunked = true;
        }

        if (!owner.IsErased)
        {
            await foreach (ProjectionEventDto[] page in ReadHistoryPagesAsync(
                owner.Owner, owner.Sequence, cancellationToken).ConfigureAwait(false))
            {
                foreach (ProjectionEventDto item in page)
                {
                    if (item.SequenceNumber != expectedSequence || item.GlobalPosition <= priorPosition)
                        throw new InvalidOperationException("Source history has an invalid authoritative position.");
                    int eventBytes = JsonSerializer.SerializeToUtf8Bytes(item).Length;
                    int envelopeBytes = JsonSerializer.SerializeToUtf8Bytes(PartRequest([])).Length - 2;
                    if (buffered.Count == 256 || envelopeBytes + bufferedEventBytes
                        + buffered.Count + eventBytes > MaxPartRequestBytes)
                    {
                        await SendPartAsync().ConfigureAwait(false);
                        envelopeBytes = JsonSerializer.SerializeToUtf8Bytes(PartRequest([])).Length - 2;
                    }
                    if (envelopeBytes + eventBytes > MaxPartRequestBytes)
                        throw new InvalidOperationException("One source event exceeds a bounded transport part.");
                    buffered.Add(item);
                    bufferedEventBytes += eventBytes;
                    fingerprint.Append(item);
                    expectedSequence++;
                    priorPosition = item.GlobalPosition;
                }
            }
        }

        if (expectedSequence - 1 != (owner.IsErased ? 0 : owner.Sequence)
            || !owner.IsErased && priorPosition != owner.Position)
            throw new InvalidOperationException("Source prefix changed after the sealed cut.");
        string historyFingerprint = fingerprint.Finish();
        if (owner.HistoryFingerprint is not null
            && !string.Equals(owner.HistoryFingerprint, historyFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("Source history changed after its captured fingerprint.");
        if (chunked)
        {
            await SendPartAsync().ConfigureAwait(false);
            DomainSharedProjectionRebuildResponse completed = await gateway.SendAsync(new(
                DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.AccumulateComplete, identity,
                ownerOrdinal, owner.Owner.AggregateId, owner.IsErased,
                PartCount: partOrdinal, ExpectedHistoryFingerprint: historyFingerprint),
                cancellationToken).ConfigureAwait(false);
            RequireSuccess(completed, DomainSharedProjectionRebuildPhase.Accumulating);
        }
        else
        {
            var request = new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity,
                ownerOrdinal, owner.Owner.AggregateId, owner.IsErased, [.. buffered]);
            if (JsonSerializer.SerializeToUtf8Bytes(request).Length > MaxPartRequestBytes)
            {
                await SendPartAsync().ConfigureAwait(false);
                DomainSharedProjectionRebuildResponse completed = await gateway.SendAsync(new(
                    DomainSharedProjectionRebuildProtocol.Version,
                    DomainSharedProjectionRebuildAction.AccumulateComplete, identity,
                    ownerOrdinal, owner.Owner.AggregateId, owner.IsErased,
                    PartCount: partOrdinal, ExpectedHistoryFingerprint: historyFingerprint),
                    cancellationToken).ConfigureAwait(false);
                RequireSuccess(completed, DomainSharedProjectionRebuildPhase.Accumulating);
                return historyFingerprint;
            }
            DomainSharedProjectionRebuildResponse accumulated = await gateway.SendAsync(request,
                cancellationToken).ConfigureAwait(false);
            RequireSuccess(accumulated, DomainSharedProjectionRebuildPhase.Accumulating);
        }

        return historyFingerprint;
    }

    private async IAsyncEnumerable<ProjectionEventDto[]> ReadHistoryPagesAsync(
        AggregateIdentity owner, long sequence,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IAsyncEnumerable<ProjectionEventDto[]>? pages = histories.ReadPagesAsync(owner, sequence, cancellationToken);
        if (pages is null)
        {
            yield return [.. await histories.ReadAsync(owner, sequence, cancellationToken).ConfigureAwait(false)];
            yield break;
        }

        bool yielded = false;
        await foreach (ProjectionEventDto[] page in pages.ConfigureAwait(false))
        {
            yielded = true;
            yield return page ?? throw new InvalidOperationException("Source history page is null.");
        }
        if (!yielded && sequence > 0)
            yield return [.. await histories.ReadAsync(owner, sequence, cancellationToken).ConfigureAwait(false)];
    }
}
