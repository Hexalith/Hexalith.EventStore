// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base section 10; amendment sections 1-4 and 7.
using Dapr.Client;
using Grpc.Core;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Issues strong byte reads and qualified ETag transactions without scanning keys or retrying mutations.</summary>
internal sealed class DaprPayloadProtectionStateStore(DaprClient client, string storeName = "statestore") : IPayloadProtectionStateStore
{
    private bool _qualified;

    /// <summary>Gets the selected component name; no partition override is silently introduced.</summary>
    internal string StoreName { get; } = !string.IsNullOrWhiteSpace(storeName) ? storeName : throw new PayloadProtectionFormatException();

    /// <summary>Reads one closed value under strong consistency after qualification.</summary>
    public async ValueTask<PayloadProtectionStateValue<T>?> ReadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        RequireQualification();
        return await ReadQualifiedAsync<T>(key, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Submits the exact transaction once and conservatively distinguishes an unobserved result.</summary>
    public async ValueTask<PayloadStateCommitResult> CommitAsync(IReadOnlyList<PayloadProtectionStateMutation> mutations, CancellationToken cancellationToken = default)
    {
        RequireQualification();
        foreach (PayloadProtectionStateMutation mutation in mutations)
        {
            ValidateMutation(mutation);
        }

        return await CommitQualifiedAsync(mutations, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads through the same production primitive during isolated capability qualification.</summary>
    internal async ValueTask<PayloadProtectionStateValue<T>?> ReadQualifiedAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            (ReadOnlyMemory<byte> bytes, string etag) = await client.GetByteStateAndETagAsync(
                StoreName, key, ConsistencyMode.Strong, cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes.IsEmpty && string.IsNullOrEmpty(etag)) { return null; }
            if (bytes.IsEmpty || string.IsNullOrWhiteSpace(etag)) { throw new PayloadProtectionFormatException(); }
            T record = PayloadProtectionStateCodec.Decode<T>(bytes);
            if (PayloadProtectionStateCodec.KeyFor(record) != key) { throw new PayloadProtectionFormatException(); }
            return new(record, etag, bytes.ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (PayloadProtectionFormatException) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch); }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new PayloadProtectionStateException(IsDenied(exception)
                ? UnreadableProtectedDataReason.ProviderDenied : UnreadableProtectedDataReason.ProviderUnavailable);
        }
    }

    /// <summary>Submits qualified create-only, ETag or unchanged-value CAS requests during startup qualification.</summary>
    internal async ValueTask<PayloadStateCommitResult> CommitQualifiedAsync(IReadOnlyList<PayloadProtectionStateMutation> mutations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mutations.Count == 0 || mutations.Select(m => m.Key).Distinct(StringComparer.Ordinal).Count() != mutations.Count)
        {
            throw new PayloadProtectionFormatException();
        }

        var requests = new List<StateTransactionRequest>(mutations.Count);
        foreach (PayloadProtectionStateMutation mutation in mutations)
        {
            if (!mutation.Key.StartsWith("eventstore:pp:v2:", StringComparison.Ordinal)
                || mutation.Bytes is { Length: > PayloadProtectionStateCodec.MaximumBytes }
                || mutation.ExpectedETag is "" || (mutation.Bytes is null && mutation.ExpectedETag is null)
                || mutation.TtlSeconds is < 1 or > 60)
            {
                throw new PayloadProtectionFormatException();
            }

            IReadOnlyDictionary<string, string>? metadata = mutation.TtlSeconds is { } ttl
                ? new Dictionary<string, string> { ["ttlInSeconds"] = ttl.ToString(System.Globalization.CultureInfo.InvariantCulture) }
                : null;
            requests.Add(new(mutation.Key, mutation.Bytes?.ToArray(),
                mutation.Bytes is null ? StateOperationType.Delete : StateOperationType.Upsert, mutation.ExpectedETag, metadata,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }));
        }

        try
        {
            await client.ExecuteStateTransactionAsync(StoreName, requests, cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return PayloadStateCommitResult.Committed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsDenied(exception)) { return PayloadStateCommitResult.Denied; }
            RpcException? rpc = exception as RpcException ?? exception.InnerException as RpcException;
            return rpc?.StatusCode is StatusCode.Aborted or StatusCode.FailedPrecondition or StatusCode.AlreadyExists
                ? PayloadStateCommitResult.Conflict : PayloadStateCommitResult.Unknown;
        }
    }

    /// <summary>Confirms metadata capabilities for the exact selected component without granting readiness.</summary>
    internal async ValueTask<string> InspectCapabilitiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            DaprMetadata metadata = await client.GetMetadataAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            DaprComponentsMetadata[] components = metadata.Components.Where(c => c.Name == StoreName).ToArray();
            if (components.Length != 1 || !new[] { "ETAG", "TRANSACTIONAL", "TTL" }.All(required => components[0].Capabilities.Contains(required, StringComparer.Ordinal)))
            {
                throw new PayloadProtectionFormatException();
            }

            return components[0].Type + ":" + components[0].Version;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { cancellationToken.ThrowIfCancellationRequested(); throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
    }

    /// <summary>Publishes readiness only after the full isolated probe confirms all persisted assertions.</summary>
    internal void ConfirmQualification() => Volatile.Write(ref _qualified, true);

    /// <summary>Withholds readiness before a fresh capability qualification attempt.</summary>
    internal void RevokeQualification() => Volatile.Write(ref _qualified, false);

    private void RequireQualification()
    {
        if (!Volatile.Read(ref _qualified)) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
    }

    private static void ValidateMutation(PayloadProtectionStateMutation mutation)
    {
        // Raw mutations exist for unchanged bytes and delete CAS. They must not bypass the closed schemas.
        if (!mutation.Key.StartsWith("eventstore:pp:v2:", StringComparison.Ordinal)) { throw new PayloadProtectionFormatException(); }
        string[] parts = mutation.Key["eventstore:pp:v2:".Length..].Split(':');
        if (parts.Length < 2) { throw new PayloadProtectionFormatException(); }
        if (mutation.Bytes is null)
        {
            // Deletes require an explicit ETag and the exact closed key grammar.
            if (mutation.ExpectedETag is null) { throw new PayloadProtectionFormatException(); }
            string canonicalKey = parts[0] switch
            {
                "lease" when parts.Length == 3 => PayloadProtectionStateKeys.Lease(parts[1], parts[2]),
                "completion" when parts.Length == 5 => PayloadProtectionStateKeys.Completion(parts[1], parts[2], ParseVersion(parts[3]), parts[4]),
                "capability" when parts.Length == 4 => PayloadProtectionStateKeys.Capability(parts[1], ParseGeneration(parts[2]), parts[3]),
                "key" when parts.Length == 4 => PayloadProtectionStateKeys.Key(parts[1], parts[2], ParseVersion(parts[3])),
                _ => throw new PayloadProtectionFormatException(),
            };
            if (canonicalKey != mutation.Key) { throw new PayloadProtectionFormatException(); }
            if (mutation.TtlSeconds is not null) { throw new PayloadProtectionFormatException(); }
            return;
        }

        object value = parts[0] switch
        {
            "key" => PayloadProtectionStateCodec.Decode<WrappedDekV1>(mutation.Bytes),
            "index-head" => PayloadProtectionStateCodec.Decode<ScopeIndexHeadV1>(mutation.Bytes),
            "index" => PayloadProtectionStateCodec.Decode<ScopeIndexPageV1>(mutation.Bytes),
            "kek-index-head" => PayloadProtectionStateCodec.Decode<KekInventoryHeadV1>(mutation.Bytes),
            "kek-index" => PayloadProtectionStateCodec.Decode<KekInventoryPageV1>(mutation.Bytes),
            "lifecycle" => PayloadProtectionStateCodec.Decode<LifecycleV1>(mutation.Bytes),
            "fence" => PayloadProtectionStateCodec.Decode<FenceV1>(mutation.Bytes),
            "deployment" => PayloadProtectionStateCodec.Decode<DeploymentGenerationV1>(mutation.Bytes),
            "capability" => PayloadProtectionStateCodec.Decode<CapabilityLeaseV1>(mutation.Bytes),
            "operation" => PayloadProtectionStateCodec.Decode<OperationV1>(mutation.Bytes),
            "lease" => PayloadProtectionStateCodec.Decode<ReadLeaseV1>(mutation.Bytes),
            "completion" => PayloadProtectionStateCodec.Decode<CompletionLeaseV1>(mutation.Bytes),
            "reconcile" => PayloadProtectionStateCodec.Decode<ReconciliationV1>(mutation.Bytes),
            "audit" => PayloadProtectionStateCodec.Decode<PayloadProtectionAuditRecord>(mutation.Bytes),
            "member-head" => PayloadProtectionStateCodec.Decode<MemberHeadV1>(mutation.Bytes),
            "members" => PayloadProtectionStateCodec.Decode<MemberPageV1>(mutation.Bytes),
            "work-head" => PayloadProtectionStateCodec.Decode<WorkHeadV1>(mutation.Bytes),
            "work" => PayloadProtectionStateCodec.Decode<WorkPageV1>(mutation.Bytes),
            "drain" => PayloadProtectionStateCodec.Decode<InvalidationDrainV1>(mutation.Bytes),
            "clear-ack" => PayloadProtectionStateCodec.Decode<CacheClearAckV1>(mutation.Bytes),
            _ => throw new PayloadProtectionFormatException(),
        };
        if (PayloadProtectionStateCodec.KeyFor(value) != mutation.Key) { throw new PayloadProtectionFormatException(); }
        int? expectedTtl = value switch
        {
            ReadLeaseV1 v => checked((int)Math.Ceiling((PayloadProtectionStateCodec.ParseTimestamp(v.ExpiresAtUtc) - PayloadProtectionStateCodec.ParseTimestamp(v.IssuedAtUtc)).TotalSeconds)),
            CapabilityLeaseV1 or CompletionLeaseV1 => 60,
            _ => null,
        };
        if (mutation.TtlSeconds != expectedTtl) { throw new PayloadProtectionFormatException(); }
    }

    private static uint ParseVersion(string value) => uint.TryParse(value, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out uint version) ? version : throw new PayloadProtectionFormatException();

    private static ulong ParseGeneration(string value) => ulong.TryParse(value, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out ulong generation) ? generation : throw new PayloadProtectionFormatException();

    private static bool IsDenied(Exception exception)
    {
        RpcException? rpc = exception as RpcException ?? exception.InnerException as RpcException;
        return rpc?.StatusCode is StatusCode.PermissionDenied or StatusCode.Unauthenticated;
    }
}
