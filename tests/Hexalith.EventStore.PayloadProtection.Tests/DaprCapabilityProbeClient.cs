using System.Globalization;
using Dapr.Client;
using Grpc.Core;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Substitutes the Dapr transport to expose incompatible capability and transaction behavior; never PF-02 evidence.</summary>
internal sealed class DaprCapabilityProbeClient
{
    private readonly Dictionary<string, (byte[] Bytes, string ETag, DateTimeOffset? Expires)> _values = new(StringComparer.Ordinal);
    private readonly string _fault;
    private int _version;

    /// <summary>Configures the substituted client with exactly one incompatible behavior.</summary>
    internal DaprCapabilityProbeClient(string fault)
    {
        _fault = fault;
        Client = Substitute.For<DaprClient>();
        string[] capabilities = ["ETAG", "TRANSACTIONAL", "TTL"];
        if (fault.StartsWith("missing-", StringComparison.Ordinal)) { capabilities = capabilities.Where(capability => capability != fault[8..]).ToArray(); }
        var component = new DaprComponentsMetadata("statestore", "state.postgresql", "v2", capabilities);
        Client.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(new DaprMetadata("probe", [], new Dictionary<string, string>(),
            fault == "duplicate-component" ? [component, component] : [component]));
        Client.GetByteStateAndETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                string key = call.ArgAt<string>(1);
                if (!_values.TryGetValue(key, out var value)) { return (ReadOnlyMemory<byte>.Empty, ""); }
                if (fault != "ttl-never-expires" && value.Expires <= DateTimeOffset.UtcNow)
                {
                    _values.Remove(key);
                    return (ReadOnlyMemory<byte>.Empty, "");
                }
                return (new ReadOnlyMemory<byte>(value.Bytes), value.ETag);
            });
        Client.ExecuteStateTransactionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<StateTransactionRequest>>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                IReadOnlyList<StateTransactionRequest> requests = call.ArgAt<IReadOnlyList<StateTransactionRequest>>(1);
                if (fault == "partial-transaction")
                {
                    foreach (StateTransactionRequest request in requests)
                    {
                        if (!Matches(request)) { return Conflict(); }
                        Apply(request);
                    }
                    return Task.CompletedTask;
                }
                if (requests.Any(request => !Matches(request))) { return Conflict(); }
                foreach (StateTransactionRequest request in requests) { Apply(request); }
                return Task.CompletedTask;
            });
    }

    /// <summary>Gets the substituted transport consumed by the production state-store boundary.</summary>
    internal DaprClient Client { get; }

    private bool Matches(StateTransactionRequest request)
    {
        bool exists = _values.TryGetValue(request.Key, out var value);
        if (request.ETag is null) { return !exists; }
        if (_fault == "ignored-etag" || (_fault == "ignored-delete-etag" && request.OperationType == StateOperationType.Delete)) { return exists; }
        return exists && value.ETag == request.ETag;
    }

    private void Apply(StateTransactionRequest request)
    {
        if (request.OperationType == StateOperationType.Delete)
        {
            if (_fault != "ignored-delete") { _values.Remove(request.Key); }
            return;
        }
        DateTimeOffset? expires = request.Metadata is not null && request.Metadata.TryGetValue("ttlInSeconds", out string? ttl)
            ? DateTimeOffset.UtcNow.AddSeconds(int.Parse(ttl, CultureInfo.InvariantCulture)) : null;
        _values[request.Key] = (request.Value!.ToArray(), (++_version).ToString(CultureInfo.InvariantCulture), expires);
    }

    private static Task Conflict() => Task.FromException(new RpcException(new(StatusCode.Aborted, "fixture-rejected-condition")));
}
