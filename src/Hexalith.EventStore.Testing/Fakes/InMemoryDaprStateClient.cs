#pragma warning disable CS0672, DAPR_DISTRIBUTEDLOCK
#nullable disable

using System.Text.Json;
using Dapr.Client;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>Isolated byte-state transport for production SDK persistence tests; never an installation authority.</summary>
public sealed class InMemoryDaprStateClient : DaprClient
{
    private readonly Dictionary<(string Store, string Key), (byte[] Value, string ETag)> _state = [];
    private readonly object _sync = new();
    private long _revision;

    /// <inheritdoc/>
    public override JsonSerializerOptions JsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>Gets the observed raw read keys.</summary>
    public List<string> ReadKeys { get; } = [];

    /// <summary>Gets successful mutation keys.</summary>
    public List<string> WrittenKeys { get; } = [];

    /// <summary>Gets or sets a raw-read spy or fault injection hook.</summary>
    public Action<string> BeforeRead { get; set; }

    /// <summary>Gets or sets a pre-mutation spy or fault injection hook.</summary>
    public Action<string> BeforeWrite { get; set; }

    /// <inheritdoc/>
    public override Task<bool> TrySaveStateAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        StateOptions stateOptions,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken) => TrySaveByteStateAsync(storeName, key, JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptions), etag, stateOptions, metadata, cancellationToken);

    /// <inheritdoc/>
    public override Task<BulkPublishResponse<TValue>> BulkPublishEventAsync<TValue>(string pubsubName, string topicName, IReadOnlyList<TValue> events, Dictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<bool> CheckHealthAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<bool> CheckOutboundHealthAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override HttpClient CreateInvokableHttpClient(string appId) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override HttpRequestMessage CreateInvokeMethodRequest(HttpMethod httpMethod, string appId, string methodName) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override HttpRequestMessage CreateInvokeMethodRequest(HttpMethod httpMethod, string appId, string methodName, IReadOnlyCollection<KeyValuePair<string, string>> queryStringParameters) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override HttpRequestMessage CreateInvokeMethodRequest<TRequest>(HttpMethod httpMethod, string appId, string methodName, IReadOnlyCollection<KeyValuePair<string, string>> queryStringParameters, TRequest data) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, ReadOnlyMemory<byte> ciphertextBytes, string keyName, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override IAsyncEnumerable<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, Stream ciphertextStream, string keyName, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, ReadOnlyMemory<byte> ciphertextBytes, string keyName, DecryptionOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override IAsyncEnumerable<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, Stream ciphertextStream, string keyName, DecryptionOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task DeleteBulkStateAsync(string storeName, IReadOnlyList<BulkDeleteStateItem> items, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task DeleteStateAsync(string storeName, string key, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BeforeWrite?.Invoke(key);
        lock (_sync)
        {
            _state.Remove((storeName, key));
            WrittenKeys.Add(key);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc/>
    public override Task<ReadOnlyMemory<byte>> EncryptAsync(string vaultResourceName, ReadOnlyMemory<byte> plaintextBytes, string keyName, EncryptionOptions encryptionOptions, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override IAsyncEnumerable<ReadOnlyMemory<byte>> EncryptAsync(string vaultResourceName, Stream plaintextStream, string keyName, EncryptionOptions encryptionOptions, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task ExecuteStateTransactionAsync(
        string storeName,
        IReadOnlyList<StateTransactionRequest> operations,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<Dictionary<string, Dictionary<string, string>>> GetBulkSecretAsync(string storeName, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<IReadOnlyList<BulkStateItem>> GetBulkStateAsync(string storeName, IReadOnlyList<string> keys, int? parallelism, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<IReadOnlyList<BulkStateItem<TValue>>> GetBulkStateAsync<TValue>(string storeName, IReadOnlyList<string> keys, int? parallelism, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<BulkStateItem<TValue>>>([.. keys.Select(key =>
        {
            BeforeRead?.Invoke(key);
            lock (_sync)
            {
                ReadKeys.Add(key);
                return _state.TryGetValue((storeName, key), out var entry)
                    ? new BulkStateItem<TValue>(key, JsonSerializer.Deserialize<TValue>(entry.Value, JsonSerializerOptions), entry.ETag)
                    : new BulkStateItem<TValue>(key, default, string.Empty);
            }
        })]);
    }

    /// <inheritdoc/>
    public override Task<(ReadOnlyMemory<byte>, string)> GetByteStateAndETagAsync(string storeName, string key, ConsistencyMode? consistencyMode, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BeforeRead?.Invoke(key);
        lock (_sync)
        {
            ReadKeys.Add(key);
            return Task.FromResult(_state.TryGetValue((storeName, key), out var entry)
                ? ((ReadOnlyMemory<byte>)entry.Value.ToArray(), entry.ETag) : (ReadOnlyMemory<byte>.Empty, string.Empty));
        }
    }

    /// <inheritdoc/>
    public override Task<ReadOnlyMemory<byte>> GetByteStateAsync(string storeName, string key, ConsistencyMode? consistencyMode, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<GetConfigurationResponse> GetConfiguration(string storeName, IReadOnlyList<string> keys, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<DaprMetadata> GetMetadataAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<Dictionary<string, string>> GetSecretAsync(string storeName, string key, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<(TValue, string)> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BeforeRead?.Invoke(key);
        lock (_sync)
        {
            ReadKeys.Add(key);
            return Task.FromResult(_state.TryGetValue((storeName, key), out var entry)
                ? (JsonSerializer.Deserialize<TValue>(entry.Value, JsonSerializerOptions), entry.ETag) : (default(TValue), string.Empty));
        }
    }

    /// <inheritdoc/>
    public override Task<TValue> GetStateAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BeforeRead?.Invoke(key);
        lock (_sync)
        {
            ReadKeys.Add(key);
            return Task.FromResult(_state.TryGetValue((storeName, key), out var entry)
                ? JsonSerializer.Deserialize<TValue>(entry.Value, JsonSerializerOptions) : default);
        }
    }

    /// <inheritdoc/>
    public override Task<BindingResponse> InvokeBindingAsync(BindingRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task InvokeBindingAsync<TRequest>(string bindingName, string operation, TRequest data, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<TResponse> InvokeBindingAsync<TRequest, TResponse>(string bindingName, string operation, TRequest data, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task InvokeMethodAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<TResponse> InvokeMethodAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task InvokeMethodGrpcAsync(string appId, string methodName, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<TResponse> InvokeMethodGrpcAsync<TResponse>(string appId, string methodName, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task InvokeMethodGrpcAsync<TRequest>(string appId, string methodName, TRequest data, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<TResponse> InvokeMethodGrpcAsync<TRequest, TResponse>(string appId, string methodName, TRequest data, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<HttpResponseMessage> InvokeMethodWithResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<TryLockResponse> Lock(string storeName, string resourceId, string lockOwner, int expiryInSeconds, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task PublishByteEventAsync(string pubsubName, string topicName, ReadOnlyMemory<byte> data, string dataContentType, Dictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task PublishEventAsync(string pubsubName, string topicName, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task PublishEventAsync<TData>(string pubsubName, string topicName, TData data, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task PublishEventAsync(string pubsubName, string topicName, Dictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task PublishEventAsync<TData>(string pubsubName, string topicName, TData data, Dictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<StateQueryResponse<TValue>> QueryStateAsync<TValue>(string storeName, string jsonQuery, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task SaveBulkStateAsync<TValue>(string storeName, IReadOnlyList<SaveStateItem<TValue>> items, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task SaveByteStateAsync(string storeName, string key, ReadOnlyMemory<byte> binaryValue, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BeforeWrite?.Invoke(key);
        lock (_sync)
        {
            _state[(storeName, key)] = (binaryValue.ToArray(), (++_revision).ToString(System.Globalization.CultureInfo.InvariantCulture));
            WrittenKeys.Add(key);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc/>
    public override Task SaveStateAsync<TValue>(string storeName, string key, TValue value, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => SaveByteStateAsync(storeName, key, JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptions), stateOptions, metadata, cancellationToken);

    /// <inheritdoc/>
    public override Task SetMetadataAsync(string attributeName, string attributeValue, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task ShutdownSidecarAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<SubscribeConfigurationResponse> SubscribeConfiguration(string storeName, IReadOnlyList<string> keys, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<bool> TryDeleteStateAsync(
        string storeName,
        string key,
        string etag,
        StateOptions stateOptions,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BeforeWrite?.Invoke(key);
        lock (_sync)
        {
            if (!_state.TryGetValue((storeName, key), out var current) || current.ETag != etag) return Task.FromResult(false);
            _state.Remove((storeName, key));
            WrittenKeys.Add(key);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public override Task<bool> TrySaveByteStateAsync(string storeName, string key, ReadOnlyMemory<byte> binaryValue, string etag, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BeforeWrite?.Invoke(key);
        lock (_sync)
        {
            bool exists = _state.TryGetValue((storeName, key), out var current);
            if (exists ? current.ETag != etag : !string.IsNullOrEmpty(etag)) return Task.FromResult(false);
            _state[(storeName, key)] = (binaryValue.ToArray(), (++_revision).ToString(System.Globalization.CultureInfo.InvariantCulture));
            WrittenKeys.Add(key);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public override Task<UnlockResponse> Unlock(string storeName, string resourceId, string lockOwner, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<UnsubscribeConfigurationResponse> UnsubscribeConfiguration(string storeName, string id, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task WaitForSidecarAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

}

#nullable restore
#pragma warning restore CS0672, DAPR_DISTRIBUTEDLOCK
