#pragma warning disable CS0672, DAPR_DISTRIBUTEDLOCK

using System.Text.Json;

using Dapr.Client;

namespace Hexalith.EventStore.Client.Tests.Projections;

#nullable disable

internal sealed class RecordingDaprClient : DaprClient
{
    public override JsonSerializerOptions JsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    public bool TrySaveResult { get; init; }

    public bool? TryDeleteResult { get; init; }

    public Exception ExecuteStateTransactionException { get; init; }

    public int ExecuteStateTransactionCallCount { get; private set; }

    public int SaveByteStateCallCount { get; private set; }

    public int DeleteStateCallCount { get; private set; }

    public Action<string, ReadOnlyMemory<byte>> BeforeTrySaveByteState { get; set; }

    public IReadOnlyList<(string Key, byte[] Value, string ETag, ConcurrencyMode Concurrency)> TrySaveByteOperations => _trySaveByteOperations;

    public IReadOnlyList<(string Key, string ETag, ConcurrencyMode Concurrency)> TryDeleteByteOperations => _tryDeleteByteOperations;

    public IReadOnlyList<StateTransactionRequest> TransactionOperations { get; private set; }

    public string StoreName { get; private set; }

    public string Key { get; private set; }

    public object Value { get; private set; }

    public string ETag { get; private set; }

    public StateOptions StateOptions { get; private set; }

    private readonly Dictionary<string, (byte[] Value, string ETag)> _byteStore = new(StringComparer.Ordinal);
    private readonly List<(string Key, byte[] Value, string ETag, ConcurrencyMode Concurrency)> _trySaveByteOperations = [];
    private readonly List<(string Key, string ETag, ConcurrencyMode Concurrency)> _tryDeleteByteOperations = [];
    private long _byteEtag;

    // When set, ExecuteStateTransactionAsync throws before applying, simulating an ambiguous dispatch.
    public bool ThrowOnNextByteWrite { get; set; }

    // When set, a transaction operation is applied to the byte store only if the predicate returns true.
    // Lets a test simulate a mis-qualified store that commits the terminal receipt but partially applies the
    // logical data operations, so read-back verification (not the void response) must catch the gap.
    public Func<StateTransactionRequest, bool> TransactionOperationApplyFilter { get; set; }

    private string NextByteETag() => (++_byteEtag).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool ByteStoreContains(string key) => _byteStore.ContainsKey(key);

    public byte[] ByteStoreValue(string key) => _byteStore.TryGetValue(key, out (byte[] Value, string ETag) e) ? e.Value : null;

    public void SeedByteStore(string key, ReadOnlyMemory<byte> value) =>
        _byteStore[key] = (value.ToArray(), NextByteETag());

    public override Task<bool> TrySaveStateAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        StateOptions stateOptions,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        StoreName = storeName;
        Key = key;
        Value = value;
        ETag = etag;
        StateOptions = stateOptions;
        return Task.FromResult(TrySaveResult);
    }

    public override Task<BulkPublishResponse<TValue>> BulkPublishEventAsync<TValue>(string pubsubName, string topicName, IReadOnlyList<TValue> events, Dictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<bool> CheckHealthAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<bool> CheckOutboundHealthAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public override HttpClient CreateInvokableHttpClient(string appId) => throw new NotSupportedException();

    public override HttpRequestMessage CreateInvokeMethodRequest(HttpMethod httpMethod, string appId, string methodName) => throw new NotSupportedException();

    public override HttpRequestMessage CreateInvokeMethodRequest(HttpMethod httpMethod, string appId, string methodName, IReadOnlyCollection<KeyValuePair<string, string>> queryStringParameters) => throw new NotSupportedException();

    public override HttpRequestMessage CreateInvokeMethodRequest<TRequest>(HttpMethod httpMethod, string appId, string methodName, IReadOnlyCollection<KeyValuePair<string, string>> queryStringParameters, TRequest data) => throw new NotSupportedException();

    public override Task<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, ReadOnlyMemory<byte> ciphertextBytes, string keyName, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override IAsyncEnumerable<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, Stream ciphertextStream, string keyName, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, ReadOnlyMemory<byte> ciphertextBytes, string keyName, DecryptionOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override IAsyncEnumerable<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, Stream ciphertextStream, string keyName, DecryptionOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task DeleteBulkStateAsync(string storeName, IReadOnlyList<BulkDeleteStateItem> items, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task DeleteStateAsync(string storeName, string key, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteStateCallCount++;
        _ = _byteStore.Remove(key);
        return Task.CompletedTask;
    }

    public override Task<ReadOnlyMemory<byte>> EncryptAsync(string vaultResourceName, ReadOnlyMemory<byte> plaintextBytes, string keyName, EncryptionOptions encryptionOptions, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override IAsyncEnumerable<ReadOnlyMemory<byte>> EncryptAsync(string vaultResourceName, Stream plaintextStream, string keyName, EncryptionOptions encryptionOptions, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task ExecuteStateTransactionAsync(
        string storeName,
        IReadOnlyList<StateTransactionRequest> operations,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StoreName = storeName;
        TransactionOperations = operations.ToArray();
        ExecuteStateTransactionCallCount++;
        if (ExecuteStateTransactionException is not null)
        {
            return Task.FromException(ExecuteStateTransactionException);
        }

        // Apply the transaction to the stateful byte store so read-back verification can prove the persisted
        // end state. Normally all-or-nothing (transaction-qualified profile); a test may install
        // TransactionOperationApplyFilter to simulate a mis-qualified store that partially commits.
        foreach (StateTransactionRequest operation in operations)
        {
            if (TransactionOperationApplyFilter is not null && !TransactionOperationApplyFilter(operation))
            {
                continue;
            }

            if (operation.OperationType == StateOperationType.Delete)
            {
                _ = _byteStore.Remove(operation.Key);
            }
            else
            {
                _byteStore[operation.Key] = (operation.Value.ToArray(), NextByteETag());
            }
        }

        return Task.CompletedTask;
    }

    public override Task<Dictionary<string, Dictionary<string, string>>> GetBulkSecretAsync(string storeName, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<IReadOnlyList<BulkStateItem>> GetBulkStateAsync(string storeName, IReadOnlyList<string> keys, int? parallelism, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<IReadOnlyList<BulkStateItem<TValue>>> GetBulkStateAsync<TValue>(string storeName, IReadOnlyList<string> keys, int? parallelism, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<(ReadOnlyMemory<byte>, string)> GetByteStateAndETagAsync(string storeName, string key, ConsistencyMode? consistencyMode, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _byteStore.TryGetValue(key, out (byte[] Value, string ETag) entry)
            ? Task.FromResult<(ReadOnlyMemory<byte>, string)>((entry.Value, entry.ETag))
            : Task.FromResult<(ReadOnlyMemory<byte>, string)>((ReadOnlyMemory<byte>.Empty, string.Empty));
    }

    public override Task<ReadOnlyMemory<byte>> GetByteStateAsync(string storeName, string key, ConsistencyMode? consistencyMode, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<GetConfigurationResponse> GetConfiguration(string storeName, IReadOnlyList<string> keys, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<DaprMetadata> GetMetadataAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<Dictionary<string, string>> GetSecretAsync(string storeName, string key, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<(TValue, string)> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<TValue> GetStateAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<BindingResponse> InvokeBindingAsync(BindingRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task InvokeBindingAsync<TRequest>(string bindingName, string operation, TRequest data, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<TResponse> InvokeBindingAsync<TRequest, TResponse>(string bindingName, string operation, TRequest data, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task InvokeMethodAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<TResponse> InvokeMethodAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task InvokeMethodGrpcAsync(string appId, string methodName, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<TResponse> InvokeMethodGrpcAsync<TResponse>(string appId, string methodName, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task InvokeMethodGrpcAsync<TRequest>(string appId, string methodName, TRequest data, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<TResponse> InvokeMethodGrpcAsync<TRequest, TResponse>(string appId, string methodName, TRequest data, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<HttpResponseMessage> InvokeMethodWithResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<TryLockResponse> Lock(string storeName, string resourceId, string lockOwner, int expiryInSeconds, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task PublishByteEventAsync(string pubsubName, string topicName, ReadOnlyMemory<byte> data, string dataContentType, Dictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task PublishEventAsync(string pubsubName, string topicName, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task PublishEventAsync<TData>(string pubsubName, string topicName, TData data, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task PublishEventAsync(string pubsubName, string topicName, Dictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task PublishEventAsync<TData>(string pubsubName, string topicName, TData data, Dictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<StateQueryResponse<TValue>> QueryStateAsync<TValue>(string storeName, string jsonQuery, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task SaveBulkStateAsync<TValue>(string storeName, IReadOnlyList<SaveStateItem<TValue>> items, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task SaveByteStateAsync(string storeName, string key, ReadOnlyMemory<byte> binaryValue, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveByteStateCallCount++;
        _byteStore[key] = (binaryValue.ToArray(), NextByteETag());
        return Task.CompletedTask;
    }

    public override Task SaveStateAsync<TValue>(string storeName, string key, TValue value, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task SetMetadataAsync(string attributeName, string attributeValue, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task ShutdownSidecarAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<SubscribeConfigurationResponse> SubscribeConfiguration(string storeName, IReadOnlyList<string> keys, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<bool> TryDeleteStateAsync(
        string storeName,
        string key,
        string etag,
        StateOptions stateOptions,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StoreName = storeName;
        Key = key;
        ETag = etag;
        StateOptions = stateOptions;
        _tryDeleteByteOperations.Add((
            key,
            etag,
            stateOptions.Concurrency ?? throw new InvalidOperationException("Conditional delete requires a concurrency mode.")));
        if (TryDeleteResult is not null)
        {
            return Task.FromResult(TryDeleteResult.Value);
        }

        if (!_byteStore.TryGetValue(key, out (byte[] Value, string ETag) current)
            || !string.Equals(current.ETag, etag, StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        _ = _byteStore.Remove(key);
        return Task.FromResult(true);
    }

    public override Task<bool> TrySaveByteStateAsync(string storeName, string key, ReadOnlyMemory<byte> binaryValue, string etag, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _trySaveByteOperations.Add((
            key,
            binaryValue.ToArray(),
            etag,
            stateOptions.Concurrency ?? throw new InvalidOperationException("Conditional write requires a concurrency mode.")));
        if (ThrowOnNextByteWrite)
        {
            ThrowOnNextByteWrite = false;
            throw new Dapr.DaprException("Injected byte-write transport failure.");
        }

        BeforeTrySaveByteState?.Invoke(key, binaryValue);

        bool exists = _byteStore.TryGetValue(key, out (byte[] Value, string ETag) current);
        bool matches = exists
            ? string.Equals(current.ETag, etag, StringComparison.Ordinal)
            : string.IsNullOrEmpty(etag);
        if (!matches)
        {
            return Task.FromResult(false);
        }

        _byteStore[key] = (binaryValue.ToArray(), NextByteETag());
        return Task.FromResult(true);
    }

    public override Task<UnlockResponse> Unlock(string storeName, string resourceId, string lockOwner, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<UnsubscribeConfigurationResponse> UnsubscribeConfiguration(string storeName, string id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task WaitForSidecarAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

#nullable restore

#pragma warning restore CS0672, DAPR_DISTRIBUTEDLOCK
