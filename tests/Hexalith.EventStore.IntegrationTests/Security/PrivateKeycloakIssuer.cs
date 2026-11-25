using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Helpers;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Runs an isolated real OIDC issuer for the bearer claims contract.</summary>
internal sealed class PrivateKeycloakIssuer : IAsyncDisposable
{
    /// <summary>The plain client and audience expected by the bearer configuration.</summary>
    internal const string DirectClient = "eventstore-claim-test";

    /// <summary>The client that adds the test delegation claims.</summary>
    internal const string DelegatedClient = DirectClient + "-delegated";

    private const string Realm = "eventstore-claims";
    private const string Username = "fixture-user";
    private readonly OwnedAuthenticationContainer _container = new();
    private readonly DirectoryInfo _import = Directory.CreateTempSubdirectory("eventstore-keycloak-claims-");
    private readonly string _password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    /// <summary>Gets the generated subject of both tokens.</summary>
    internal string Subject { get; } = Guid.NewGuid().ToString("D");

    /// <summary>Gets the run-private loopback issuer and discovery base URI.</summary>
    internal string Authority => $"http://{_container.Endpoint}/realms/{Realm}";

    /// <summary>Imports the fixture realm and waits for its discovery endpoint.</summary>
    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        string path = Path.Combine(_import.FullName, Realm + "-realm.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            realm = Realm,
            enabled = true,
            sslRequired = "none",
            attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["CreateDefaultClientScopes"] = "true" },
            clients = new[] { Client(DirectClient, []), Client(DelegatedClient,
                [Claim("act", """{"sub":"fixture-delegate"}""", "JSON"), Claim("client_id", "fixture-caller", "String")]) },
            users = new[]
            {
                new
                {
                    id = Subject, username = Username, enabled = true, firstName = "Test", lastName = "User",
                    email = "fixture@example.test", emailVerified = true,
                    credentials = new[] { new { type = "password", value = _password, temporary = false } },
                },
            },
        }), cancellationToken).ConfigureAwait(false);

        if (OperatingSystem.IsLinux())
        {
            // Let the container's non-root user read the mounted fixture. The wrapper's parent directory stays private.
            File.SetUnixFileMode(_import.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        int port = OwnedAuthenticationContainer.CandidatePort();
        await _container.StartAsync("quay.io/keycloak/keycloak:26.8", null, port,
            ["start-dev", "--import-realm", "--http-port=" + port.ToString(CultureInfo.InvariantCulture)], cancellationToken,
            ["--mount", $"type=bind,source={_import.FullName},target=/opt/keycloak/data/import,readonly"])
            .ConfigureAwait(false);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var ready = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ready.CancelAfter(TimeSpan.FromSeconds(90));
        while (true)
        {
            ready.Token.ThrowIfCancellationRequested();
            try
            {
                using HttpResponseMessage response = await client.GetAsync(Authority + "/.well-known/openid-configuration", ready.Token)
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!ready.IsCancellationRequested) { }
            await Task.Delay(TimeSpan.FromMilliseconds(250), ready.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Requests an issuer-signed access token for the generated subject.</summary>
    internal Task<string> IssueAsync(string clientId, CancellationToken cancellationToken)
        => KeycloakTokenHelper.AcquireTokenAsync(Authority + "/protocol/openid-connect/token",
            clientId, Username, _password, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try { await _container.DisposeAsync().ConfigureAwait(false); }
        finally { _import.Delete(recursive: true); }
    }

    private static object Client(string clientId, object[] additionalMappers)
        => new
        {
            clientId, protocol = "openid-connect", enabled = true, publicClient = true,
            directAccessGrantsEnabled = true, defaultClientScopes = new[] { "basic", "profile" },
            protocolMappers = new[] { Mapper("eventstore-audience", "oidc-audience-mapper",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["included.client.audience"] = DirectClient,
                    ["access.token.claim"] = "true",
                }) }.Concat(additionalMappers),
        };

    private static object Claim(string name, string value, string type)
        => Mapper(name, "oidc-hardcoded-claim-mapper", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["claim.name"] = name, ["claim.value"] = value, ["jsonType.label"] = type,
            ["access.token.claim"] = "true",
        });

    private static object Mapper(string name, string protocolMapper, Dictionary<string, string> config)
        => new { name, protocol = "openid-connect", protocolMapper, config };
}
