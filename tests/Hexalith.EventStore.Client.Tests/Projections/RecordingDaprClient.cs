#pragma warning disable CS0672, DAPR_DISTRIBUTEDLOCK

using System.Text.Json;

using Dapr.Client;

namespace Hexalith.EventStore.Client.Tests.Projections;

#nullable disable

internal sealed class RecordingDaprClient : DaprClient
{
    public override JsonSerializerOptions JsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    public bool TrySaveResult { get; init; }

    public bool TryDeleteResult { get; init; }

    public Exception ExecuteStateTransactionException { get; init; }

    public int ExecuteStateTransactionCallCount { get; private set; }

    public IReadOnlyList<StateTransactionRequest> TransactionOperations { get; private set; }

    public string StoreName { get; private set; }

    public string Key { get; private set; }

    public object Value { get; private set; }

    public string ETag { get; private set; }

    public StateOptions StateOptions { get; private set; }

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

    public override Task DeleteStateAsync(string storeName, string key, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

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
        return ExecuteStateTransactionException is null
            ? Task.CompletedTask
            : Task.FromException(ExecuteStateTransactionException);
    }

    public override Task<Dictionary<string, Dictionary<string, string>>> GetBulkSecretAsync(string storeName, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<IReadOnlyList<BulkStateItem>> GetBulkStateAsync(string storeName, IReadOnlyList<string> keys, int? parallelism, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<IReadOnlyList<BulkStateItem<TValue>>> GetBulkStateAsync<TValue>(string storeName, IReadOnlyList<string> keys, int? parallelism, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<(ReadOnlyMemory<byte>, string)> GetByteStateAndETagAsync(string storeName, string key, ConsistencyMode? consistencyMode, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

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

    public override Task SaveByteStateAsync(string storeName, string key, ReadOnlyMemory<byte> binaryValue, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

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
        return Task.FromResult(TryDeleteResult);
    }

    public override Task<bool> TrySaveByteStateAsync(string storeName, string key, ReadOnlyMemory<byte> binaryValue, string etag, StateOptions stateOptions, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<UnlockResponse> Unlock(string storeName, string resourceId, string lockOwner, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task<UnsubscribeConfigurationResponse> UnsubscribeConfiguration(string storeName, string id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override Task WaitForSidecarAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

#nullable restore

#pragma warning restore CS0672, DAPR_DISTRIBUTEDLOCK
