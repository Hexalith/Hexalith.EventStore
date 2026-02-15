namespace Hexalith.EventStore.IntegrationTests.Security;

using System.Net.Http;

using global::Aspire.Hosting;

/// <summary>
/// Base class for E2E security tests that use the shared <see cref="AspireTopologyFixture"/>.
/// Provides convenient accessors for the running Aspire topology, CommandApi client,
/// and Keycloak token acquisition. Tests derived from this base should be tagged
/// with [Trait("Category", "E2E")] and [Collection("AspireTopology")].
/// </summary>
public abstract class KeycloakE2ETestBase
{
    private readonly AspireTopologyFixture _fixture;

    protected KeycloakE2ETestBase(AspireTopologyFixture fixture)
    {
        _fixture = fixture;
    }

    protected HttpClient CommandApiClient => _fixture.CommandApiClient;

    protected string KeycloakBaseUrl => _fixture.KeycloakBaseUrl;

    /// <summary>
    /// Gets the running Aspire distributed application instance.
    /// </summary>
    protected DistributedApplication App => _fixture.App;

    /// <summary>
    /// Acquires a real OIDC token from Keycloak for the specified test user (D11, Rule #16).
    /// </summary>
    protected Task<string> GetTokenAsync(string username, string password)
        => _fixture.GetTokenAsync(username, password);
}
