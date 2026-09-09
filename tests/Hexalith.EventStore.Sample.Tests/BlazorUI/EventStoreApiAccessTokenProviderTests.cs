extern alias BlazorUI;

using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using Shouldly;
using Microsoft.IdentityModel.Tokens;

using EventStoreApiAccessTokenProvider = BlazorUI::Hexalith.EventStore.Sample.BlazorUI.Services.EventStoreApiAccessTokenProvider;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

public sealed class EventStoreApiAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_OutsideDevelopmentWithoutAuthority_RejectsLocalMinting()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(CreateLocalConfiguration())
            .Build();
        var provider = new EventStoreApiAccessTokenProvider(
            configuration,
            new TestHostEnvironment(Environments.Production),
            new TestHttpClientFactory());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("only in the Development environment");
    }

    [Fact]
    public async Task GetAccessTokenAsync_DevelopmentWithExplicitClaims_MintsHs256Token()
    {
        Dictionary<string, string?> values = CreateLocalConfiguration();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var provider = new EventStoreApiAccessTokenProvider(
            configuration,
            new TestHostEnvironment(Environments.Development),
            new TestHttpClientFactory());

        string token = await provider.GetAccessTokenAsync();

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        ClaimsPrincipal principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "hexalith-dev",
            ValidateAudience = true,
            ValidAudience = "hexalith-eventstore",
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                values["EventStore:Authentication:SigningKey"]!)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
        }, out SecurityToken validatedToken);

        validatedToken.ShouldBeOfType<JwtSecurityToken>().Header.Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
        principal.FindFirst("sub")!.Value.ShouldBe("sample-user");
        principal.FindFirst("tenants")!.Value.ShouldContain("tenant-a");
        principal.FindFirst("domains")!.Value.ShouldContain("counter");
        principal.FindFirst("permissions")!.Value.ShouldContain("query:read");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithProductionAuthority_PostsExpectedFormAndCachesResponse()
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        string password = " " + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + " ";
        values["EventStore:Authentication:ClientId"] = "sample-ui";
        values["EventStore:Authentication:Username"] = "tenant-a-user";
        values["EventStore:Authentication:Password"] = password;
        values["EventStore:Authentication:AudienceParameterName"] = "audience";
        values["EventStore:Authentication:AudienceParameterValue"] = "hexalith-eventstore";
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var handler = new RecordingTokenHandler();
        var provider = new EventStoreApiAccessTokenProvider(
            configuration,
            new TestHostEnvironment(Environments.Production),
            new TestHttpClientFactory(handler));

        string first = await provider.GetAccessTokenAsync();
        string second = await provider.GetAccessTokenAsync();

        first.ShouldBe(handler.AccessToken);
        second.ShouldBe(first);
        handler.RequestCount.ShouldBe(1);
        handler.RequestUri.ShouldBe(new Uri("https://tokens.example.test/oauth/token"));
        handler.FormValues.ShouldBe(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "password",
            ["client_id"] = "sample-ui",
            ["scope"] = "api.read",
            ["username"] = "tenant-a-user",
            ["password"] = password,
            ["audience"] = "hexalith-eventstore",
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithClientCredentials_SendsOnlyProfileFieldsAndCachesResponse()
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        string clientSecret = " " + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + " ";
        values["EventStore:Authentication:GrantType"] = "client_credentials";
        values["EventStore:Authentication:ClientSecret"] = clientSecret;
        values["EventStore:Authentication:Username"] = "PROTECTED_UNUSED_USERNAME_MARKER";
        values["EventStore:Authentication:Password"] = "PROTECTED_UNUSED_PASSWORD_MARKER";
        var handler = new RecordingTokenHandler();
        var provider = CreateAuthorityProvider(values, handler);

        string first = await provider.GetAccessTokenAsync();
        string second = await provider.GetAccessTokenAsync();

        first.ShouldBe(handler.AccessToken);
        second.ShouldBe(first);
        handler.RequestCount.ShouldBe(1);
        handler.FormValues.ShouldBe(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "ui-client",
            ["scope"] = "api.read",
            ["client_secret"] = clientSecret,
        }, ignoreOrder: true);
    }

    [Theory]
    [InlineData("identity.example.test")]
    [InlineData("http://identity.example.test")]
    [InlineData("https://user" + "@identity.example.test")]
    [InlineData("https://identity.example.test?tenant=x")]
    [InlineData("https://identity.example.test#tenant")]
    public async Task GetAccessTokenAsync_WithExplicitEndpoint_StillRejectsUnsafeAuthority(string authority)
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        values["EventStore:Authentication:Authority"] = authority;
        var handler = new RecordingTokenHandler();
        var provider = CreateAuthorityProvider(values, handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("Authority");
        handler.RequestCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("tokens.example.test/oauth/token")]
    [InlineData("http://tokens.example.test/oauth/token")]
    [InlineData("https://user" + "@tokens.example.test/oauth/token")]
    [InlineData("https://tokens.example.test/oauth/token#tenant")]
    public async Task GetAccessTokenAsync_WithUnsafeExplicitTokenEndpoint_FailsBeforeSendingCredentials(string endpoint)
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        values["EventStore:Authentication:TokenEndpoint"] = endpoint;
        var handler = new RecordingTokenHandler();
        var provider = CreateAuthorityProvider(values, handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("TokenEndpoint");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithFixedQueryTokenEndpoint_PreservesTheConfiguredQuery()
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        values["EventStore:Authentication:TokenEndpoint"] =
            "https://tokens.example.test/oauth/token?tenant=stable";
        var handler = new RecordingTokenHandler();
        var provider = CreateAuthorityProvider(values, handler);

        _ = await provider.GetAccessTokenAsync();

        handler.RequestUri.ShouldBe(
            new Uri("https://tokens.example.test/oauth/token?tenant=stable"));
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithoutExplicitEndpoint_UsesMatchingIssuerDiscoveryAndCachesResponse()
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        values.Remove("EventStore:Authentication:TokenEndpoint");
        values["EventStore:Authentication:Authority"] = "https://identity.example.test/tenant/";
        var handler = new RecordingTokenHandler(discoveryIssuer: "https://identity.example.test/tenant");
        var provider = CreateAuthorityProvider(values, handler);

        string first = await provider.GetAccessTokenAsync();
        string second = await provider.GetAccessTokenAsync();

        first.ShouldBe(handler.AccessToken);
        second.ShouldBe(first);
        handler.RequestUris.ShouldBe(
        [
            new Uri("https://identity.example.test/tenant/.well-known/openid-configuration"),
            new Uri("https://tokens.example.test/oauth/token"),
        ]);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenDiscoveryIssuerDoesNotMatchAuthority_RejectsBeforeSendingCredentials()
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        values.Remove("EventStore:Authentication:TokenEndpoint");
        var handler = new RecordingTokenHandler(discoveryIssuer: "https://other.example.test/tenant");
        var provider = CreateAuthorityProvider(values, handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("issuer");
        exception.Message.ShouldContain("Authority");
        handler.RequestUris.ShouldBe(
        [
            new Uri("https://identity.example.test/tenant/.well-known/openid-configuration"),
        ]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("authorization_code")]
    [InlineData("PASSWORD")]
    public async Task GetAccessTokenAsync_WhenGrantProfileIsNotExplicitlySupported_Fails(string? grantType)
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        values["EventStore:Authentication:GrantType"] = grantType;
        var handler = new RecordingTokenHandler();
        var provider = CreateAuthorityProvider(values, handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("GrantType");
        handler.RequestCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("password", "Password")]
    [InlineData("client_credentials", "ClientSecret")]
    public async Task GetAccessTokenAsync_WhenProfileCredentialIsMissing_FailsBeforeSendingCredentials(
        string grantType,
        string missingSetting)
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        values["EventStore:Authentication:GrantType"] = grantType;
        values.Remove($"EventStore:Authentication:{missingSetting}");
        var handler = new RecordingTokenHandler();
        var provider = CreateAuthorityProvider(values, handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain(missingSetting);
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenAudienceParameterIsOnlyPartiallyConfigured_Fails()
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        values["EventStore:Authentication:AudienceParameterName"] = "resource";
        var handler = new RecordingTokenHandler();
        var provider = CreateAuthorityProvider(values, handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("configured together");
        handler.RequestCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"access_token\":\"\"}")]
    [InlineData("{\"access_token\":\"   \"}")]
    public async Task GetAccessTokenAsync_WhenProviderReturnsBlankToken_FailsClosed(string responseJson)
    {
        Dictionary<string, string?> values = CreateAuthorityConfiguration();
        var provider = CreateAuthorityProvider(values, new RecordingTokenHandler(tokenResponseJson: responseJson));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("non-blank access_token");
    }

    private static Dictionary<string, string?> CreateLocalConfiguration()
        => new()
        {
            ["EventStore:Authentication:Issuer"] = "hexalith-dev",
            ["EventStore:Authentication:Audience"] = "hexalith-eventstore",
            ["EventStore:Authentication:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["EventStore:Authentication:Subject"] = "sample-user",
            ["EventStore:Authentication:Tenants:0"] = "tenant-a",
            ["EventStore:Authentication:Domains:0"] = "counter",
            ["EventStore:Authentication:Permissions:0"] = "query:read",
        };

    private static Dictionary<string, string?> CreateAuthorityConfiguration()
        => new()
        {
            ["EventStore:Authentication:Authority"] = "https://identity.example.test/tenant",
            ["EventStore:Authentication:TokenEndpoint"] = "https://tokens.example.test/oauth/token",
            ["EventStore:Authentication:GrantType"] = "password",
            ["EventStore:Authentication:Scope"] = "api.read",
            ["EventStore:Authentication:ClientId"] = "ui-client",
            ["EventStore:Authentication:Username"] = "runtime-user",
            ["EventStore:Authentication:Password"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
        };

    private static EventStoreApiAccessTokenProvider CreateAuthorityProvider(
        Dictionary<string, string?> values,
        RecordingTokenHandler handler)
        => new(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new TestHostEnvironment(Environments.Production),
            new TestHttpClientFactory(handler));

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = nameof(EventStoreApiAccessTokenProviderTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler? handler = null) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => handler is null ? new() : new(handler);
    }

    private sealed class RecordingTokenHandler : HttpMessageHandler
    {
        private readonly string _discoveryIssuer;
        private readonly string _tokenResponseJson;

        public RecordingTokenHandler(
            string? discoveryIssuer = null,
            string? tokenResponseJson = null)
        {
            AccessToken = Guid.NewGuid().ToString("N");
            _discoveryIssuer = discoveryIssuer ?? "https://identity.example.test/tenant";
            _tokenResponseJson = tokenResponseJson
                ?? JsonSerializer.Serialize(new { access_token = AccessToken, expires_in = 3600 });
        }

        public string AccessToken { get; }

        public int RequestCount { get; private set; }

        public Uri? RequestUri { get; private set; }

        public List<Uri> RequestUris { get; } = [];

        public string Form { get; private set; } = string.Empty;

        public IReadOnlyDictionary<string, string> FormValues { get; private set; }
            = new Dictionary<string, string>(StringComparer.Ordinal);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            RequestUris.Add(request.RequestUri!);
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        issuer = _discoveryIssuer,
                        token_endpoint = "https://tokens.example.test/oauth/token",
                    })),
                };
            }

            Form = await request.Content!.ReadAsStringAsync(cancellationToken);
            FormValues = Form.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(static component => component.Split('=', 2))
                .ToDictionary(
                    static pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                    static pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')),
                    StringComparer.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_tokenResponseJson),
            };
        }
    }
}
