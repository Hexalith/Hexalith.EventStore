namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Owns the private Redis instance used by marker ownership tests.</summary>
public sealed class PrivateAuthenticationRedisFixture : IAsyncLifetime
{
    private readonly OwnedAuthenticationContainer _container = new();

    /// <summary>Gets the private Redis endpoint.</summary>
    public string Endpoint => _container.Endpoint;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        int port = OwnedAuthenticationContainer.CandidatePort();
        try
        {
            await _container.StartAsync("redis:7.4-alpine", null, port,
                ["redis-server", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture)], TestContext.Current.CancellationToken);
        }
        catch
        {
            await _container.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
