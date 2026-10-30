using Xunit;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>Consumes an existing Platform topology. This fixture never creates or stops infrastructure.</summary>
public abstract class AspireTopologyFixtureBase : IAsyncLifetime
{
    private readonly Dictionary<(string ResourceId, string ModuleId, string EndpointName), HttpClient> _clients = new();
    private TestEnvironmentDescriptor? _environment;
    private bool _disposed;

    /// <summary>Gets the exact declared HTTP resource bindings required by this suite.</summary>
    protected abstract IReadOnlyList<TestEnvironmentEndpoint> Resources { get; }

    /// <summary>Gets the validated ready environment after initialization.</summary>
    public TestEnvironmentDescriptor Environment => _environment ?? throw new InvalidOperationException("The fixture has not consumed a valid handoff.");

    /// <summary>Gets the client for an explicitly declared resource.</summary>
    /// <param name="resourceId">The declared resource identity.</param>
    /// <returns>The bounded fixture-owned client.</returns>
    protected HttpClient Client(string resourceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        HttpClient[] matches = _clients.Where(pair => pair.Key.ResourceId == resourceId).Select(pair => pair.Value).ToArray();
        return matches.Length == 1 ? matches[0]
            : throw new InvalidOperationException("The requested fixture resource is unavailable or requires an exact endpoint binding.");
    }

    /// <summary>Gets the client for an exact resource, module and endpoint tuple.</summary>
    /// <param name="resourceId">The declared resource identity.</param>
    /// <param name="moduleId">The declaring module identity.</param>
    /// <param name="endpointName">The declared endpoint name.</param>
    /// <returns>The bounded fixture-owned client.</returns>
    protected HttpClient Client(string resourceId, string moduleId, string endpointName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _clients.TryGetValue((resourceId, moduleId, endpointName), out HttpClient? client)
            ? client : throw new InvalidOperationException("The requested fixture endpoint was not declared or initialized.");
    }

    /// <inheritdoc/>
    public ValueTask InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_environment is not null)
        {
            throw new InvalidOperationException("The fixture has already initialized.");
        }

        TestEnvironmentEndpoint[] required = Resources.ToArray();
        TestEnvironmentDescriptor environment = TestEnvironmentDescriptorReader.ReadFromEnvironment();
        try
        {
            // Validate every binding before allocating any clients.
            foreach (TestEnvironmentEndpoint resource in required)
            {
                _ = environment.GetEndpoint(resource.ResourceId, resource.ModuleId, resource.EndpointName);
            }

            foreach (TestEnvironmentEndpoint resource in required)
            {
                _clients.Add((resource.ResourceId, resource.ModuleId, resource.EndpointName), environment.CreateHttpClient(resource.ResourceId, resource.ModuleId, resource.EndpointName, resource.Timeout, resource.Authenticated));
            }

            _environment = environment;
            return ValueTask.CompletedTask;
        }
        catch
        {
            environment.Dispose();
            _clients.Clear();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _environment?.Dispose();
        _clients.Clear();
        return ValueTask.CompletedTask;
    }
}
