using Aspire.Hosting.Testing;

using Hexalith.EventStore.IntegrationTests.Fixtures;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Shares one private, real Keycloak topology across authentication selectors.</summary>
public class AspireTopologyFixture : KeycloakAuthFixture
{
    /// <summary>Gets the Keycloak HTTP endpoint.</summary>
    public string KeycloakBaseUrl => App.GetEndpoint("security", "http").ToString().TrimEnd('/');

    /// <summary>Acquires a real OIDC token for the named fixture identity.</summary>
    public Task<string> GetTokenAsync(string username) => AcquireTokenAsync(username, TestContext.Current.CancellationToken);
}
