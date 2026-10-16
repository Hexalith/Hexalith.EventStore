using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.DomainService;

/// <summary>Acquires bounded, cached tokens from an explicitly configured trusted authority endpoint.</summary>
internal sealed class GatewayAuthorityAccessTokenProvider(IConfiguration configuration, IHttpClientFactory clients, IHostEnvironment? environment) : IDisposable
{
    /// <summary>The isolated credential acquisition HTTP client.</summary>
    internal const string TokenClientName = "EventStoreGatewayAuthorityToken";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expires;

    /// <summary>Gets a live authority token; credentials and response contents never enter diagnostics.</summary>
    internal async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is not null && _expires > DateTimeOffset.UtcNow) return _token;
            IConfigurationSection settings = configuration.GetSection("EventStore:Authentication");
            if (!Uri.TryCreate(settings["Authority"], UriKind.Absolute, out Uri? authority)
                || !Uri.TryCreate(settings["TokenEndpoint"], UriKind.Absolute, out Uri? endpoint)
                || endpoint.UserInfo.Length != 0 || authority.UserInfo.Length != 0
                || endpoint.Scheme != Uri.UriSchemeHttps && !(environment?.IsDevelopment() == true && endpoint.Scheme == Uri.UriSchemeHttp)
                || endpoint.GetLeftPart(UriPartial.Authority) != authority.GetLeftPart(UriPartial.Authority)
                || !endpoint.AbsolutePath.StartsWith(authority.AbsolutePath.TrimEnd('/') + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("The gateway authority token endpoint must be explicitly configured within its trusted authority; HTTPS is required outside Development.");
            string grant = settings["GrantType"] ?? "client_credentials";
            var form = new Dictionary<string, string> { ["grant_type"] = grant, ["client_id"] = Required(settings, "ClientId") };
            if (!string.IsNullOrWhiteSpace(settings["Scope"])) form["scope"] = settings["Scope"]!;
            if (!string.IsNullOrWhiteSpace(settings["ClientSecret"])) form["client_secret"] = Required(settings, "ClientSecret");
            if (grant == "password")
            {
                form["username"] = Required(settings, "Username");
                form["password"] = Required(settings, "Password");
            }
            else if (grant != "client_credentials" || !form.ContainsKey("client_secret"))
                throw new InvalidOperationException("The gateway authority grant requires configured password or client credentials.");
            using HttpClient client = clients.CreateClient(TokenClientName);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (client.Timeout != Timeout.InfiniteTimeSpan) deadline.CancelAfter(client.Timeout);
            CancellationToken acquisitionToken = deadline.Token;
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new FormUrlEncodedContent(form) };
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, acquisitionToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("The gateway authority rejected access-token acquisition.");
            using Stream stream = await response.Content.ReadAsStreamAsync(acquisitionToken).ConfigureAwait(false);
            using var bytes = new MemoryStream();
            byte[] buffer = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(buffer, acquisitionToken).ConfigureAwait(false)) != 0)
            {
                if (bytes.Length + count > 64 * 1024) throw new InvalidOperationException("The gateway authority token response exceeds its bound.");
                bytes.Write(buffer, 0, count);
            }
            using JsonDocument document = JsonDocument.Parse(bytes.ToArray());
            JsonElement root = document.RootElement;
            string? value = root.TryGetProperty("access_token", out JsonElement token) && token.ValueKind == JsonValueKind.String
                ? token.GetString() : null;
            if (string.IsNullOrWhiteSpace(value)
                || !root.TryGetProperty("token_type", out JsonElement type) || !string.Equals(type.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase)
                || !root.TryGetProperty("expires_in", out JsonElement expires) || !expires.TryGetInt32(out int seconds) || seconds < 1)
                throw new InvalidOperationException("The gateway authority returned an invalid token response.");
            _token = value;
            _expires = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, Math.Min(seconds, 300) - 15));
            return _token;
        }
        finally { _gate.Release(); }
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    /// <summary>Requires explicit credential configuration without echoing its value.</summary>
    private static string Required(IConfigurationSection settings, string key)
        => !string.IsNullOrWhiteSpace(settings[key]) ? settings[key]! : throw new InvalidOperationException("The gateway authority credentials are incomplete.");
}
