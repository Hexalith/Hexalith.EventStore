using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Testing.Integration.Tests.Benchmarking;

internal sealed class RecordingActorStateHttpMessageHandler : HttpMessageHandler {
    private readonly Lock _lock = new();
    private readonly Dictionary<string, string> _state = new(StringComparer.Ordinal);
    private readonly List<int> _transactionBytes = [];
    private readonly List<IReadOnlyList<string>> _transactionKeys = [];
    private int _getCount;

    internal int GetCount {
        get {
            lock (_lock) {
                return _getCount;
            }
        }
    }

    internal int PostCount {
        get {
            lock (_lock) {
                return _transactionKeys.Count;
            }
        }
    }

    internal IReadOnlyList<int> TransactionBytes {
        get {
            lock (_lock) {
                return [.. _transactionBytes];
            }
        }
    }

    internal IReadOnlyList<IReadOnlyList<string>> TransactionKeys {
        get {
            lock (_lock) {
                return [.. _transactionKeys.Select(static keys => (IReadOnlyList<string>)[.. keys])];
            }
        }
    }

    internal bool ContainsState(AggregateIdentity identity, string key) {
        lock (_lock) {
            return _state.ContainsKey(CreateStateKey(identity.ActorId, key));
        }
    }

    internal void SeedState(AggregateIdentity identity, string key, object value) {
        lock (_lock) {
            _state[CreateStateKey(identity.ActorId, key)] = JsonSerializer.Serialize(value);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request.RequestUri);
        string[] segments = request.RequestUri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length < 5
            || !string.Equals(segments[0], "v1.0", StringComparison.Ordinal)
            || !string.Equals(segments[1], "actors", StringComparison.Ordinal)
            || !string.Equals(segments[4], "state", StringComparison.Ordinal)) {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        string actorId = segments[3];
        if (request.Method == HttpMethod.Get && segments.Length == 6) {
            string? json;
            lock (_lock) {
                _getCount++;
                _state.TryGetValue(CreateStateKey(actorId, segments[5]), out json);
            }

            return json is null
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
        }

        if (request.Method != HttpMethod.Post || segments.Length != 5 || request.Content is null) {
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        byte[] body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(body);
        var transactionKeys = new List<string>();
        lock (_lock) {
            foreach (JsonElement item in document.RootElement.EnumerateArray()) {
                string operation = item.GetProperty("operation").GetString()
                    ?? throw new JsonException("Actor-state operation is missing its operation name.");
                JsonElement stateRequest = item.GetProperty("request");
                string key = stateRequest.GetProperty("key").GetString()
                    ?? throw new JsonException("Actor-state operation is missing its key.");
                transactionKeys.Add(key);
                string stateKey = CreateStateKey(actorId, key);
                if (string.Equals(operation, "upsert", StringComparison.Ordinal)) {
                    _state[stateKey] = stateRequest.GetProperty("value").GetRawText();
                }
                else if (string.Equals(operation, "delete", StringComparison.Ordinal)) {
                    _state.Remove(stateKey);
                }
                else {
                    throw new JsonException($"Unsupported actor-state operation '{operation}'.");
                }
            }

            _transactionKeys.Add(transactionKeys);
            _transactionBytes.Add(body.Length);
        }

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static string CreateStateKey(string actorId, string key) => string.Concat(actorId, "\0", key);
}
