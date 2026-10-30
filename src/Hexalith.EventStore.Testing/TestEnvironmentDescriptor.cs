using System.Net.Http.Headers;
using System.Text.Json;

namespace Hexalith.EventStore.Testing;

/// <summary>A validated ready owner environment. It owns only its requests and clients.</summary>
public sealed class TestEnvironmentDescriptor : IDisposable
{
    private readonly JsonElement _document;
    private readonly List<HttpClient> _clients = [];
    private readonly CancellationTokenSource _requests = new();
    private string? _accessToken;
    private Dictionary<(string Resource, string Module, string Endpoint), string> _endpointCredentials = [];
    private bool _disposed;

    /// <summary>Requires complete readiness and synthetic isolation after strict contract validation.</summary>
    /// <param name="document">The detached validated v1 document.</param>
    internal TestEnvironmentDescriptor(JsonElement document)
    {
        _document = document;
        JsonElement environment = EnvironmentBinding;
        JsonElement readiness = environment.GetProperty("readiness");
        JsonElement resources = environment.GetProperty("resources");
        JsonElement tasks = environment.GetProperty("startupTasks");
        if (RunBinding.GetProperty("role").GetString() != "owner"
            || environment.GetProperty("isolation").GetProperty("state").GetString() != "populated"
            || !environment.GetProperty("composition").TryGetProperty("id", out _)
            || !environment.GetProperty("catalog").TryGetProperty("commit", out _)
            || resources.ValueKind != JsonValueKind.Array || resources.GetArrayLength() == 0
            || readiness.GetProperty("state").GetString() != "populated"
            || readiness.GetProperty("status").GetString() != "ready"
            || resources.EnumerateArray().Any(resource => !readiness.GetProperty("resources").EnumerateArray().Any(
                ready => ready.GetProperty("resourceId").GetString() == resource.GetProperty("resourceId").GetString()
                    && ready.GetProperty("status").GetString() == "ready"))
            || tasks.GetProperty("state").GetString() != "populated"
            || tasks.GetProperty("tasks").EnumerateArray().Any(task => task.GetProperty("required").GetBoolean()
                && task.GetProperty("status").GetString() != "passed"))
        {
            throw new TestEnvironmentDescriptorException("HXT108", "Require a ready isolated owner environment and successful required startup tasks. Attached execution remains unqualified.");
        }
    }

    /// <summary>Gets the producing run identity.</summary>
    public string RunId => RunBinding.GetProperty("runId").GetString()!;

    /// <summary>Gets the synthetic tenant supplied by the runner.</summary>
    public string SyntheticTenantId => EnvironmentBinding.GetProperty("isolation").GetProperty("syntheticTenantId").GetString()!;

    /// <summary>Gets the run-scoped data isolation identity.</summary>
    public string DataIsolationId => EnvironmentBinding.GetProperty("isolation").GetProperty("dataIsolationId").GetString()!;

    /// <summary>Gets the cancellation token for all fixture-owned requests, including declared non-HTTP clients.</summary>
    public CancellationToken RequestCancellation
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _requests.Token;
        }
    }

    private JsonElement RunBinding => _document.GetProperty("bindings").GetProperty("run");

    private JsonElement EnvironmentBinding => _document.GetProperty("bindings").GetProperty("environment");

    /// <summary>Selects exactly the declared resource, owning module and endpoint name.</summary>
    /// <param name="resourceId">The declared resource identity.</param>
    /// <param name="moduleId">The resource's declaring module.</param>
    /// <param name="endpointName">The declared endpoint name.</param>
    /// <returns>The supplied local endpoint; no fallback is inferred.</returns>
    public Uri GetEndpoint(string resourceId, string moduleId, string endpointName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (JsonElement resource in EnvironmentBinding.GetProperty("resources").EnumerateArray())
        {
            if (resource.GetProperty("resourceId").GetString() != resourceId || resource.GetProperty("moduleId").GetString() != moduleId)
            {
                continue;
            }

            foreach (JsonElement endpoint in resource.GetProperty("endpoints").EnumerateArray())
            {
                if (endpoint.GetProperty("name").GetString() == endpointName)
                {
                    Uri uri = new(endpoint.GetProperty("uri").GetString()!, UriKind.Absolute);
                    if (!uri.IsLoopback)
                    {
                        throw new TestEnvironmentDescriptorException("HXT109", "Hosted endpoints cannot be consumed by this fixture.");
                    }

                    return uri;
                }
            }
        }

        throw new TestEnvironmentDescriptorException("HXT109", "The exact declared resource, module and endpoint binding is required.");
    }

    /// <summary>Creates a bounded HTTP client using only runner-supplied credentials.</summary>
    /// <param name="resourceId">The declared resource identity.</param>
    /// <param name="moduleId">The declaring module.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <param name="timeout">A finite positive request bound, at most five minutes.</param>
    /// <param name="authenticated">Whether to send the separate runner bearer credential.</param>
    /// <returns>A fixture-owned client cancelled and released on disposal.</returns>
    public HttpClient CreateHttpClient(string resourceId, string moduleId, string endpointName, TimeSpan timeout, bool authenticated = true)
    {
        Uri endpoint = GetHttpEndpoint(resourceId, moduleId, endpointName);
        if (endpoint.Scheme is not ("http" or "https") || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(5)
            || (authenticated && _accessToken is null))
        {
            throw new TestEnvironmentDescriptorException("HXT110", "Require an HTTP endpoint, bounded requests and the private runner credential channel.");
        }

        return CreateClient(endpoint, timeout, authenticated, null);
    }

    /// <summary>Creates an explicitly requested Dapr invocation client from its exact supplied URI and private endpoint credential.</summary>
    /// <param name="resourceId">The declared resource identity.</param>
    /// <param name="moduleId">The declaring module.</param>
    /// <param name="endpointName">The explicit invocation endpoint name.</param>
    /// <param name="timeout">The finite request bound, at most five minutes.</param>
    /// <returns>A fixture-owned client confined to the supplied invocation prefix.</returns>
    public HttpClient CreateDaprInvocationClient(string resourceId, string moduleId, string endpointName, TimeSpan timeout)
    {
        Uri endpoint = GetDaprInvocationEndpoint(resourceId, moduleId, endpointName);
        if (_accessToken is null
            || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(5)
            || !_endpointCredentials.TryGetValue((resourceId, moduleId, endpointName), out string? credential))
            throw new TestEnvironmentDescriptorException("HXT110", "Require the explicit supplied invocation endpoint and its matching private Dapr API credential.");
        return CreateClient(endpoint, timeout, authenticated: true, credential);
    }

    /// <summary>Validates an explicitly required invocation binding before any fixture clients are allocated.</summary>
    /// <param name="resourceId">The declared resource identity.</param>
    /// <param name="moduleId">The declaring module identity.</param>
    /// <param name="endpointName">The explicit invocation endpoint name.</param>
    /// <returns>The exact supplied URI after its private binding is validated.</returns>
    public Uri GetDaprInvocationEndpoint(string resourceId, string moduleId, string endpointName)
    {
        Uri endpoint = GetHttpEndpoint(resourceId, moduleId, endpointName);
        if (!TestEnvironmentEndpointCredentials.IsInvocationEndpoint(endpoint) || !_endpointCredentials.ContainsKey((resourceId, moduleId, endpointName)))
            throw new TestEnvironmentDescriptorException("HXT110", "Require the explicit supplied invocation endpoint and its matching private Dapr API credential.");
        return endpoint;
    }

    /// <summary>Binds only the explicit separate private endpoint channel before clients are exposed.</summary>
    /// <param name="path">The explicit private endpoint credential path.</param>
    /// <param name="descriptorPath">The supplied descriptor path that anchors the fixed sibling file.</param>
    internal void BindEndpointCredentials(string path, string descriptorPath)
    {
        try { _endpointCredentials = TestEnvironmentEndpointCredentials.Read(path, descriptorPath, this); }
        catch { Dispose(); throw; }
    }

    private Uri GetHttpEndpoint(string resourceId, string moduleId, string endpointName)
    {
        Uri endpoint = GetEndpoint(resourceId, moduleId, endpointName);
        JsonElement resource = EnvironmentBinding.GetProperty("resources").EnumerateArray().Single(value =>
            value.GetProperty("resourceId").GetString() == resourceId && value.GetProperty("moduleId").GetString() == moduleId);
        string? protocol = resource.GetProperty("endpoints").EnumerateArray().Single(value =>
            value.GetProperty("name").GetString() == endpointName).GetProperty("protocol").GetString();
        if (protocol is not ("http" or "https") || endpoint.Scheme is not ("http" or "https"))
            throw new TestEnvironmentDescriptorException("HXT110", "Require an explicitly declared HTTP transport and HTTP endpoint URI.");
        return endpoint;
    }

    private HttpClient CreateClient(Uri endpoint, TimeSpan timeout, bool authenticated, string? endpointCredential)
    {
        HttpClient client = new(new TestEnvironmentRequestHandler(endpoint, _requests.Token, timeout, endpointCredential)) { BaseAddress = endpoint, Timeout = timeout };
        if (authenticated)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        _clients.Add(client);
        return client;
    }

    /// <summary>Stops this fixture's requests and releases clients while leaving runner resources owned by the runner.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        List<Exception> failures = [];
        try { _requests.Cancel(); }
        catch (Exception) { failures.Add(new InvalidOperationException("Fixture request cancellation failed.")); }
        foreach (HttpClient client in _clients)
        {
            try { client.Dispose(); }
            catch (Exception) { failures.Add(new InvalidOperationException("Fixture client disposal failed.")); }
        }

        _clients.Clear();
        _accessToken = null;
        _endpointCredentials.Clear();
        _requests.Dispose();
        if (failures.Count > 0)
        {
            throw new AggregateException("Fixture disposal failed; runner lifecycle ownership is unchanged.", failures);
        }
    }

    /// <summary>Binds only matching identities from the separate private runner channel.</summary>
    /// <param name="runId">The producing run identity.</param>
    /// <param name="tenantId">The supplied synthetic tenant.</param>
    /// <param name="dataIsolationId">The supplied run-scoped data isolation.</param>
    /// <param name="accessToken">The private runner bearer credential.</param>
    internal void BindCredentials(string? runId, string? tenantId, string? dataIsolationId, string? accessToken)
    {
        if (runId != RunId || tenantId != SyntheticTenantId || dataIsolationId != DataIsolationId
            || string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 8192 || accessToken.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '.' or '_' or '~' or '+' or '/' or '=')))
        {
            Dispose();
            throw new TestEnvironmentDescriptorException("HXT110", "Require matching run, synthetic tenant and data isolation identities and a private bearer credential.");
        }

        _accessToken = accessToken;
    }
}
