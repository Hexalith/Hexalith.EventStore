using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Seals bounded, durable identity evidence independently of rebuild scratch.</summary>
public sealed class SharedProjectionCapturedIdentityStore
{
    /// <summary>Maximum events in a leaf; leaves and their message index fit one bounded batch.</summary>
    public const int EntriesPerPage = 64;
    /// <summary>Maximum authenticated child references in an index node.</summary>
    public const int ChildrenPerPage = 128;
    private readonly IReadModelStore _store;
    private readonly SharedProjectionEpochCoordinator _coordinator;

    /// <summary>Creates a bounded identity store over the production read-model transport.</summary>
    public SharedProjectionCapturedIdentityStore(IReadModelStore store, IReadModelBatchStore batches)
        : this(store, new SharedProjectionEpochCoordinator(store, batches)) { }

    /// <summary>Uses the exact owning coordinator and its epoch-fenced recovery graph.</summary>
    internal SharedProjectionCapturedIdentityStore(IReadModelStore store, SharedProjectionEpochCoordinator coordinator)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _coordinator = coordinator;
    }

    /// <summary>Reserves an exact owner history before any identity pages are installed.</summary>
    public async Task<SharedProjectionCapturedIdentityReference> BeginAsync(SharedProjectionScope scope,
        string operationId, string sourceStream, long count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        SharedProjectionEpochStatus status = await _coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
        if (!status.IsBuilding || status.OperationId != operationId)
            throw new InvalidOperationException("Captured identity reservation requires the current building epoch.");
        var reference = Describe(scope, operationId, sourceStream, count, status.Epoch);
        string key = InventoryKey(reference);
        for (int attempt = 0; attempt < 64; attempt++)
        {
            var prior = await _store.GetAsync<SharedProjectionCapturedIdentityInventory>(scope.StoreName, key, cancellationToken).ConfigureAwait(false);
            if (prior.Value is { } existing)
            {
                if (existing.Retired || !SameReservation(existing.Reference, reference))
                    throw new InvalidOperationException("Captured identity inventory is retired or conflicts with its owner.");
                return reference;
            }
            if (await _coordinator.TryWriteCapturedIdentityRecordsAsync(scope, operationId, reference.Epoch,
                "identity-begin:" + reference.NamespaceHash,
                [ReadModelBatchOperation.Write(key, new SharedProjectionCapturedIdentityInventory(reference), ReadModelBatchConcurrency.CreateOnly)],
                cancellationToken).ConfigureAwait(false)) return reference;
        }
        throw new InvalidOperationException("Captured identity reservation exhausted its CAS retries.");
    }

    /// <summary>Describes a reserved history so its recovery identity can be retained before installation.</summary>
    public static SharedProjectionCapturedIdentityReference Describe(SharedProjectionScope scope,
        string operationId, string sourceStream, long count, long epoch)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(epoch);
        return new SharedProjectionCapturedIdentityReference(1,
            Hash(JsonSerializer.SerializeToUtf8Bytes(new[] { scope.ComputeHash(), sourceStream, operationId,
                epoch.ToString(System.Globalization.CultureInfo.InvariantCulture) })),
            scope.ComputeHash(), Hash(Encoding.UTF8.GetBytes(sourceStream)), Hash(Encoding.UTF8.GetBytes(operationId)),
            epoch, count, string.Empty, 0, 0);
    }

    /// <summary>Writes a leaf together with its operation-scoped durable message uniqueness index.</summary>
    public async Task AppendAsync(SharedProjectionScope scope, string operationId,
        SharedProjectionCapturedIdentityReference reference, long firstSequence, long[] positions,
        byte[] digests, string[] messageHashes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(digests);
        ArgumentNullException.ThrowIfNull(messageHashes);
        ValidateBinding(scope, reference);
        var page = new SharedProjectionCapturedIdentityPage(0, firstSequence, positions.LongLength,
            positions.Length == 0 ? 0 : positions[^1], positions.ToArray(), digests.ToArray(), messageHashes.ToArray(), [], []);
        ValidatePage(page);
        if ((firstSequence - 1) % EntriesPerPage != 0 || firstSequence + page.Count - 1 > reference.Count
            || page.Count < EntriesPerPage && firstSequence + page.Count - 1 != reference.Count)
            throw new InvalidOperationException("Captured identity leaf does not cover its exact reserved sequence range.");
        await WriteNodeAsync(scope, operationId, reference, page,
            (firstSequence - 1) / EntriesPerPage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Authenticates every leaf and seals its bounded index tree before publishing a reference.</summary>
    public async Task<SharedProjectionCapturedIdentityReference> SealAsync(SharedProjectionScope scope,
        string operationId, SharedProjectionCapturedIdentityReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reference);
        ValidateBinding(scope, reference);
        long nodeCount = DivideUp(reference.Count, EntriesPerPage);
        int level = 0;
        long preceding = 0;
        for (long ordinal = 0; ordinal < nodeCount; ordinal++)
        {
            SharedProjectionCapturedIdentityPage page = await ReadInventoryPageAsync(scope, reference, 0, ordinal, cancellationToken).ConfigureAwait(false);
            if (page.FirstSequence != ordinal * EntriesPerPage + 1 || page.Positions[0] <= preceding
                || page.Count != Math.Min(EntriesPerPage, reference.Count - ordinal * EntriesPerPage))
                throw new InvalidOperationException("Captured identity leaves have a gap or conflicting source position.");
            for (int index = 0; index < page.MessageHashes.Length; index++)
            {
                string message = page.MessageHashes[index];
                if (message.Length == 0) continue;
                var known = await _store.GetAsync<SharedProjectionCapturedMessageIdentity>(scope.StoreName, MessageKey(reference, message), cancellationToken).ConfigureAwait(false);
                if (known.Value?.Sequence != page.FirstSequence + index)
                    throw new InvalidOperationException("Captured identity message index is missing or contradictory.");
            }
            preceding = page.LastPosition;
        }
        while (nodeCount > 1)
        {
            long parents = DivideUp(nodeCount, ChildrenPerPage);
            for (long ordinal = 0; ordinal < parents; ordinal++)
            {
                var children = new List<string>(ChildrenPerPage);
                var counts = new List<long>(ChildrenPerPage);
                long first = 0, last = 0, count = 0;
                for (long child = ordinal * ChildrenPerPage; child < Math.Min(nodeCount, (ordinal + 1) * ChildrenPerPage); child++)
                {
                    SharedProjectionCapturedIdentityPage page = await ReadInventoryPageAsync(scope, reference, level, child, cancellationToken).ConfigureAwait(false);
                    if (first == 0) first = page.FirstSequence;
                    if (page.FirstSequence != first + count)
                        throw new InvalidOperationException("Captured identity index has a sequence gap.");
                    children.Add(Digest(page)); counts.Add(page.Count); count += page.Count; last = page.LastPosition;
                }
                await WriteNodeAsync(scope, operationId, reference,
                    new SharedProjectionCapturedIdentityPage(level + 1, first, count, last, [], [], [], [.. children], [.. counts]),
                    ordinal, cancellationToken).ConfigureAwait(false);
            }
            level++; nodeCount = parents;
        }
        SharedProjectionCapturedIdentityPage root = await ReadInventoryPageAsync(scope, reference, level, 0, cancellationToken).ConfigureAwait(false);
        if (root.FirstSequence != 1 || root.Count != reference.Count)
            throw new InvalidOperationException("Captured identity root is incomplete.");
        var sealedReference = reference with { RootDigest = Digest(root), Height = level, LastPosition = root.LastPosition };
        string key = InventoryKey(reference);
        for (int attempt = 0; attempt < 64; attempt++)
        {
            var entry = await RequireInventoryAsync(scope, reference, cancellationToken).ConfigureAwait(false);
            if (entry.Value!.Reference.RootDigest.Length != 0)
            {
                if (entry.Value.Reference != sealedReference)
                    throw new InvalidOperationException("Captured identity sealed root conflicts with its retained evidence.");
                return sealedReference;
            }
            if (await _coordinator.TryWriteCapturedIdentityRecordsAsync(scope, operationId, reference.Epoch,
                "identity-seal:" + reference.NamespaceHash,
                [ReadModelBatchOperation.Write(key, new SharedProjectionCapturedIdentityInventory(sealedReference),
                    ReadModelBatchConcurrency.Match(entry.ETag!))], cancellationToken).ConfigureAwait(false)) return sealedReference;
        }
        throw new InvalidOperationException("Captured identity sealing exhausted its CAS retries.");
    }

    /// <summary>Reads one authenticated leaf by original owner sequence without loading the owner history.</summary>
    public async Task<SharedProjectionCapturedIdentityPage> ReadPageAsync(SharedProjectionScope scope,
        SharedProjectionCapturedIdentityReference reference, long sequence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reference);
        ValidateBinding(scope, reference);
        if (!IsHash(reference.RootDigest) || reference.Height is < 0 or > 16 || sequence < 1 || sequence > reference.Count)
            throw new InvalidOperationException("Captured identity reference is malformed or the requested sequence is absent.");
        var inventory = await RequireInventoryAsync(scope, reference, cancellationToken).ConfigureAwait(false);
        if (inventory.Value!.Reference != reference)
            throw new InvalidOperationException("Captured identity reference has not been sealed.");
        string digest = reference.RootDigest;
        long first = 1, count = reference.Count;
        for (int level = reference.Height; level >= 0; level--)
        {
            SharedProjectionCapturedIdentityPage page = await ReadNodeAsync(scope, reference, digest, cancellationToken).ConfigureAwait(false);
            if (page.Level != level || page.FirstSequence != first || page.Count != count
                || level == reference.Height && page.LastPosition != reference.LastPosition)
                throw new InvalidOperationException("Captured identity node does not match its authenticated parent.");
            if (level == 0) return page;
            bool found = false;
            for (int child = 0; child < page.Children.Length; child++)
            {
                count = page.ChildCounts[child];
                if (sequence < first + count) { digest = page.Children[child]; found = true; break; }
                first += count;
            }
            if (!found) throw new InvalidOperationException("Captured identity index is incomplete.");
        }
        throw new InvalidOperationException("Captured identity tree is incomplete.");
    }

    /// <summary>Authenticates every original leaf before promotion or checkpoint handoff.</summary>
    public async Task ValidateCompleteAsync(SharedProjectionScope scope,
        SharedProjectionCapturedIdentityReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reference);
        ValidateBinding(scope, reference);
        long preceding = 0;
        for (long sequence = 1; sequence <= reference.Count;)
        {
            var page = await ReadPageAsync(scope, reference, sequence, cancellationToken).ConfigureAwait(false);
            if (page.FirstSequence != sequence || page.Positions[0] <= preceding
                || page.Count != Math.Min(EntriesPerPage, reference.Count - sequence + 1))
                throw new InvalidOperationException("Captured identity tree has an incomplete or discontinuous prefix.");
            preceding = page.LastPosition;
            sequence += page.Count;
        }
        if (preceding != reference.LastPosition) throw new InvalidOperationException("Captured identity root has a conflicting final position.");
    }

    /// <summary>Compares every retained prefix identity, including a partially filled final leaf.</summary>
    public async Task<bool> MatchesPrefixAsync(SharedProjectionScope scope,
        SharedProjectionCapturedIdentityReference candidate, SharedProjectionCheckpoint existing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existing);
        ValidateCheckpoint(existing);
        long retained = existing.CapturedIdentityReference?.Count ?? (existing.CapturedEventDigests?.LongLength / 32 ?? 0);
        if (retained > candidate.Count) return false;
        for (long sequence = 1; sequence <= retained;)
        {
            SharedProjectionCapturedIdentityPage page = await ReadPageAsync(scope, candidate, sequence, cancellationToken).ConfigureAwait(false);
            if (existing.CapturedIdentityReference is { } prior)
            {
                if (candidate.SourceHash != prior.SourceHash) return false;
                SharedProjectionCapturedIdentityPage prefix = await ReadPageAsync(scope, prior, sequence, cancellationToken).ConfigureAwait(false);
                int offset = checked((int)(sequence - page.FirstSequence));
                int priorOffset = checked((int)(sequence - prefix.FirstSequence));
                int take = (int)Math.Min(retained - sequence + 1, Math.Min(page.Count - offset, prefix.Count - priorOffset));
                if (!page.Positions.AsSpan(offset, take).SequenceEqual(prefix.Positions.AsSpan(priorOffset, take))
                    || !page.Digests.AsSpan(offset * 32, take * 32).SequenceEqual(prefix.Digests.AsSpan(priorOffset * 32, take * 32))) return false;
                sequence += take;
            }
            else
            {
                int take = (int)Math.Min(page.Count, retained - sequence + 1);
                if (!page.Digests.AsSpan(0, take * 32).SequenceEqual(existing.CapturedEventDigests!.AsSpan(checked((int)(sequence - 1) * 32), take * 32))) return false;
                sequence += take;
            }
        }
        return true;
    }

    /// <summary>Checks one canonical delivery against paged evidence or a legacy inline prefix.</summary>
    internal async Task<byte[]?> ReadDigestAsync(SharedProjectionScope scope, string sourceStream,
        SharedProjectionCheckpoint checkpoint, long sequence, CancellationToken token)
    {
        ValidateCheckpoint(checkpoint);
        if (checkpoint.CapturedIdentityReference is { } reference)
        {
            ValidateBinding(scope, reference, sourceStream);
            if (sequence < 1 || sequence > reference.Count) return null;
            var page = await ReadPageAsync(scope, reference, sequence, token).ConfigureAwait(false);
            return page.Digests.AsSpan(checked((int)(sequence - page.FirstSequence) * 32), 32).ToArray();
        }
        long offset = (sequence - 1) * 32;
        return checkpoint.CapturedEventDigests is { } inline && sequence > 0 && offset <= inline.LongLength - 32
            ? inline.AsSpan(checked((int)offset), 32).ToArray() : null;
    }

    /// <summary>Irreversibly fences installers and reclaims unselected identity evidence after abort.</summary>
    /// <remarks>Start retirement after abort and before starting another epoch. Retried retirement
    /// remains safe once its reservation is fenced; selected or inherited proof cannot be retired.</remarks>
    public async Task RetireAsync(SharedProjectionScope scope, SharedProjectionCapturedIdentityReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reference);
        ValidateBinding(scope, reference);
        string key = InventoryKey(reference);
        for (int attempt = 0; ; attempt++)
        {
            if (attempt >= 64) throw new InvalidOperationException("Captured identity retirement exhausted its CAS retries.");
            await _coordinator.ReconcileScratchBatchAsync(scope, "identity-begin:" + reference.NamespaceHash, cancellationToken).ConfigureAwait(false);
            await _coordinator.ReconcileScratchBatchAsync(scope, "identity-seal:" + reference.NamespaceHash, cancellationToken).ConfigureAwait(false);
            await _coordinator.ReconcileCapturedIdentityKeyAsync(scope, key, cancellationToken).ConfigureAwait(false);
            var entry = await _store.GetAsync<SharedProjectionCapturedIdentityInventory>(scope.StoreName, key, cancellationToken).ConfigureAwait(false);
            if (entry.Value is null) return;
            if (!SameReservation(entry.Value.Reference, reference)) throw new InvalidOperationException("Captured identity retirement inventory conflicts.");
            if (entry.Value.Retired) break;
            SharedProjectionEpochStatus status = await _coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
            if (status.IsBuilding || status.ActiveGeneration == reference.Epoch || status.Epoch != reference.Epoch)
                throw new InvalidOperationException("Selected captured identity evidence cannot be retired.");
            if (await _store.TrySaveAsync(scope.StoreName, key, entry.Value with { Retired = true }, entry.ETag!, cancellationToken).ConfigureAwait(false)) break;
        }
        long count = DivideUp(reference.Count, EntriesPerPage);
        for (int level = 0; ; level++)
        {
            for (long ordinal = 0; ordinal < count; ordinal++)
            {
                // The original fixed identity also discovers MarkerPrepared batches with no participants.
                await _coordinator.ReconcileScratchBatchAsync(scope, NodeBatchId(reference, level, ordinal), cancellationToken).ConfigureAwait(false);
                var pageEntry = await _store.GetAsync<SharedProjectionCapturedIdentityPage>(scope.StoreName,
                    PageInventoryKey(reference, level, ordinal), cancellationToken).ConfigureAwait(false);
                if (pageEntry.Value is not { } page) continue;
                ValidatePage(page);
                await _coordinator.ReconcileScratchBatchAsync(scope, NodeBatchId(reference, page.Level, ordinal), cancellationToken).ConfigureAwait(false);
                await EraseAsync(scope, NodeKey(reference, Digest(page)), cancellationToken).ConfigureAwait(false);
                if (level == 0)
                    foreach (string message in page.MessageHashes.Where(static item => item.Length != 0))
                        await EraseAsync(scope, MessageKey(reference, message), cancellationToken).ConfigureAwait(false);
                await EraseAsync(scope, PageInventoryKey(reference, level, ordinal), cancellationToken).ConfigureAwait(false);
            }
            if (count <= 1) break;
            count = DivideUp(count, ChildrenPerPage);
        }
    }

    /// <summary>Rejects malformed inline or paged prefixes without acknowledging a delivery.</summary>
    internal static void ValidateCheckpoint(SharedProjectionCheckpoint checkpoint)
    {
        if (checkpoint.CapturedIdentityReference is { } reference
            && (checkpoint.CapturedEventDigests is not null || reference.Count < 1 || reference.Count > checkpoint.OwnerSequenceNumber
                || reference.LastPosition > checkpoint.Position))
            throw new InvalidOperationException("The owner checkpoint has malformed captured identity references.");
        if (checkpoint.CapturedEventDigests is { } inline
            && (inline.Length == 0 || inline.Length % 32 != 0 || inline.LongLength / 32 > checkpoint.OwnerSequenceNumber))
            throw new InvalidOperationException("The owner checkpoint has malformed captured event identities.");
    }

    /// <summary>Checks scope, original source and optional exact operation/epoch ownership.</summary>
    internal static void ValidateBinding(SharedProjectionScope scope, SharedProjectionCapturedIdentityReference reference,
        string? sourceStream = null, string? operationId = null, long? epoch = null)
    {
        if (reference.Version != 1 || reference.Count < 1 || reference.Epoch < 1
            || reference.ScopeHash != scope.ComputeHash() || !IsHash(reference.NamespaceHash)
            || !IsHash(reference.SourceHash) || !IsHash(reference.OperationHash)
            || sourceStream is not null && reference.SourceHash != Hash(Encoding.UTF8.GetBytes(sourceStream))
            || operationId is not null && reference.OperationHash != Hash(Encoding.UTF8.GetBytes(operationId))
            || epoch is not null && reference.Epoch != epoch)
            throw new InvalidOperationException("Captured identity evidence belongs to another scope, source or rebuild epoch.");
    }

    private async Task WriteNodeAsync(SharedProjectionScope scope, string operationId,
        SharedProjectionCapturedIdentityReference reference, SharedProjectionCapturedIdentityPage page,
        long ordinal, CancellationToken token)
    {
        ValidatePage(page);
        string digest = Digest(page), nodeKey = NodeKey(reference, digest), slot = PageInventoryKey(reference, page.Level, ordinal);
        for (int attempt = 0; attempt < 64; attempt++)
        {
            var inventory = await RequireInventoryAsync(scope, reference, token).ConfigureAwait(false);
            var prior = await _store.GetAsync<SharedProjectionCapturedIdentityPage>(scope.StoreName, slot, token).ConfigureAwait(false);
            if (prior.Value is { } known)
            {
                if (Digest(known) != digest) throw new InvalidOperationException("Captured identity page conflicts with its retained ordinal.");
                _ = await ReadNodeAsync(scope, reference, digest, token).ConfigureAwait(false);
                return;
            }
            if (inventory.Value!.Reference.RootDigest.Length != 0)
                throw new InvalidOperationException("Sealed captured identity evidence cannot be extended.");
            var operations = new List<ReadModelBatchOperation>
            {
                ReadModelBatchOperation.Write(nodeKey, page, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(slot, page, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(InventoryKey(reference), inventory.Value, ReadModelBatchConcurrency.Match(inventory.ETag!))
            };
            if (page.Level == 0)
            {
                var messages = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < page.MessageHashes.Length; index++)
                {
                    string message = page.MessageHashes[index];
                    if (message.Length == 0) continue;
                    if (!messages.Add(message)) throw new InvalidOperationException("Captured owner history repeats a message identity.");
                    var knownMessage = await _store.GetAsync<SharedProjectionCapturedMessageIdentity>(scope.StoreName, MessageKey(reference, message), token).ConfigureAwait(false);
                    if (knownMessage.Value is not null && knownMessage.Value.Sequence != page.FirstSequence + index)
                        throw new InvalidOperationException("Captured owner history repeats a distant message identity.");
                    operations.Add(ReadModelBatchOperation.Write(MessageKey(reference, message), new SharedProjectionCapturedMessageIdentity(page.FirstSequence + index),
                        knownMessage.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(knownMessage.ETag!)));
                }
            }
            if (await _coordinator.TryWriteCapturedIdentityRecordsAsync(scope, operationId, reference.Epoch,
                NodeBatchId(reference, page.Level, ordinal), operations, token).ConfigureAwait(false)) return;
        }
        throw new InvalidOperationException("Captured identity page exhausted its CAS retries.");
    }

    private async Task<ReadModelEntry<SharedProjectionCapturedIdentityInventory>> RequireInventoryAsync(
        SharedProjectionScope scope, SharedProjectionCapturedIdentityReference reference, CancellationToken token)
    {
        var entry = await _store.GetAsync<SharedProjectionCapturedIdentityInventory>(scope.StoreName, InventoryKey(reference), token).ConfigureAwait(false);
        if (entry.Value is null || entry.Value.Retired || !SameReservation(entry.Value.Reference, reference))
            throw new InvalidOperationException("Captured identity inventory is missing, retired or conflicts with its reference.");
        return entry;
    }

    private async Task<SharedProjectionCapturedIdentityPage> ReadInventoryPageAsync(SharedProjectionScope scope,
        SharedProjectionCapturedIdentityReference reference, int level, long ordinal, CancellationToken token)
    {
        var page = (await _store.GetAsync<SharedProjectionCapturedIdentityPage>(scope.StoreName,
            PageInventoryKey(reference, level, ordinal), token).ConfigureAwait(false)).Value
            ?? throw new InvalidOperationException("Captured identity inventory has a missing page.");
        ValidatePage(page);
        _ = await ReadNodeAsync(scope, reference, Digest(page), token).ConfigureAwait(false);
        return page;
    }

    private async Task<SharedProjectionCapturedIdentityPage> ReadNodeAsync(SharedProjectionScope scope,
        SharedProjectionCapturedIdentityReference reference, string digest, CancellationToken token)
    {
        if (!IsHash(digest)) throw new InvalidOperationException("Captured identity child digest is malformed.");
        var page = (await _store.GetAsync<SharedProjectionCapturedIdentityPage>(scope.StoreName,
            NodeKey(reference, digest), token).ConfigureAwait(false)).Value
            ?? throw new InvalidOperationException("Captured identity evidence has a missing node.");
        ValidatePage(page);
        if (Digest(page) != digest) throw new InvalidOperationException("Captured identity node failed content authentication.");
        return page;
    }

    private async Task EraseAsync(SharedProjectionScope scope, string key, CancellationToken token)
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            await _coordinator.ReconcileCapturedIdentityKeyAsync(scope, key, token).ConfigureAwait(false);
            var value = await _store.GetAsync<JsonDocument>(scope.StoreName, key, token).ConfigureAwait(false);
            if (value.ETag is null) return;
            if (_store is not IReadModelConditionalEraser eraser)
                throw new InvalidOperationException("Captured identity retirement requires conditional erasure support.");
            if (await eraser.TryEraseAsync(scope.StoreName, key, value.ETag, token).ConfigureAwait(false)) return;
        }
        throw new InvalidOperationException("Captured identity evidence cleanup exhausted its CAS retries.");
    }

    private static void ValidatePage(SharedProjectionCapturedIdentityPage page)
    {
        if (page.Level is < 0 or > 16 || page.FirstSequence < 1 || page.Count < 1 || page.LastPosition < 1
            || page.Positions is null || page.Digests is null || page.MessageHashes is null || page.Children is null || page.ChildCounts is null)
            throw new InvalidOperationException("Captured identity node is malformed.");
        if (page.Level == 0)
        {
            if (page.Count > EntriesPerPage || page.Positions.LongLength != page.Count || page.Digests.LongLength != page.Count * 32
                || page.MessageHashes.LongLength != page.Count || page.Children.Length != 0 || page.ChildCounts.Length != 0
                || page.Positions[^1] != page.LastPosition || page.MessageHashes.Any(static item => item is null || item.Length != 0 && !IsHash(item)))
                throw new InvalidOperationException("Captured identity leaf is malformed.");
            long prior = 0;
            foreach (long position in page.Positions)
            {
                if (position <= prior) throw new InvalidOperationException("Captured identity positions are not strictly increasing.");
                prior = position;
            }
        }
        else if (page.Children.Length is < 1 or > ChildrenPerPage || page.ChildCounts.Length != page.Children.Length
            || page.ChildCounts.Any(static count => count < 1) || page.ChildCounts.Sum() != page.Count
            || page.Children.Any(static child => !IsHash(child)) || page.Positions.Length != 0 || page.Digests.Length != 0 || page.MessageHashes.Length != 0)
            throw new InvalidOperationException("Captured identity index is malformed.");
    }

    private static bool SameReservation(SharedProjectionCapturedIdentityReference left, SharedProjectionCapturedIdentityReference right)
        => left with { RootDigest = string.Empty, Height = 0, LastPosition = 0 } == right with { RootDigest = string.Empty, Height = 0, LastPosition = 0 };
    private static long DivideUp(long count, int size) => (count - 1) / size + 1;
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static bool IsHash(string? hash) => hash is { Length: 64 } && hash.All(static item => item is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static string Digest(SharedProjectionCapturedIdentityPage page) => Hash(JsonSerializer.SerializeToUtf8Bytes(page));
    /// <summary>Returns the independently retained retirement inventory key.</summary>
    internal static string InventoryKey(SharedProjectionCapturedIdentityReference reference) => "shared-identity-v1:" + reference.NamespaceHash;
    /// <summary>Returns the immutable content-addressed node key within one owner namespace.</summary>
    internal static string NodeKey(SharedProjectionCapturedIdentityReference reference, string digest) => InventoryKey(reference) + ":node:" + digest;
    private static string PageInventoryKey(SharedProjectionCapturedIdentityReference reference, int level, long ordinal) => InventoryKey(reference) + ":inventory:" + level + ":" + ordinal;
    private static string MessageKey(SharedProjectionCapturedIdentityReference reference, string hash) => InventoryKey(reference) + ":message:" + hash;
    private static string NodeBatchId(SharedProjectionCapturedIdentityReference reference, int level, long ordinal)
        => "identity-node:" + Hash(Encoding.UTF8.GetBytes(reference.NamespaceHash + ":" + level + ":" + ordinal));
}
