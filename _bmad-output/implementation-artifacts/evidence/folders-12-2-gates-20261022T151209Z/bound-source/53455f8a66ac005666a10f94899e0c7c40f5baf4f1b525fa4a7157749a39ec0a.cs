using System.Text.Json;

namespace Hexalith.EventStore.IntegrationTests.Helpers;
/// <summary>
/// Acquires real OIDC tokens from Keycloak via Resource Owner Password Grant (D11).
/// Used by E2E security tests to obtain JWT access tokens from a running Keycloak instance.
/// </summary>
public static class KeycloakTokenHelper {
    // Shared HttpClient for token acquisition. Static to avoid socket exhaustion
    // when multiple E2E tests acquire tokens (the well-known .NET HttpClient anti-pattern).
    // Safe for concurrent use since HttpClient is thread-safe for SendAsync/PostAsync.
    private static readonly HttpClient SharedHttpClient = new();

    /// <summary>
    /// Acquires a JWT access token from Keycloak using the Resource Owner Password Grant.
    /// </summary>
    /// <param name="tokenEndpoint">The Keycloak token endpoint URL (e.g., http://localhost:8180/realms/hexalith/protocol/openid-connect/token).</param>
    /// <param name="clientId">The OIDC client ID (e.g., hexalith-eventstore).</param>
    /// <param name="username">The test user's username.</param>
    /// <param name="password">The test user's password.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The JWT access token string.</returns>
    public static async Task<string> AcquireTokenAsync(
        string tokenEndpoint,
        string clientId,
        string username,
        string password,
        CancellationToken cancellationToken = default) {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
        ]);

        using HttpResponseMessage response = await SharedHttpClient
            .PostAsync(tokenEndpoint, content, cancellationToken)
            .ConfigureAwait(false);
        string json = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) {
            throw new HttpRequestException(
                $"Keycloak token acquisition failed ({(int)response.StatusCode} {response.StatusCode}). "
                + $"Endpoint: {tokenEndpoint}. Response: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak token response missing access_token");
    }
}
