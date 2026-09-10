using System.Text.Json;
using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminApiAccessTokenProviderRoleTests {
    [Theory]
    [InlineData(AdminRole.ReadOnly)]
    [InlineData(AdminRole.Operator)]
    [InlineData(AdminRole.Admin)]
    public async Task GetAccessTokenAsync_UsesSelectedDevelopmentRoleAndPreservesClaims(AdminRole selectedRole) {
        IConfiguration config = CreateDevelopmentConfig();
        var roleState = new DevelopmentAdminRoleState(config, new TestHostEnvironment("Development"));
        var provider = new AdminApiAccessTokenProvider(
            config,
            new TestHostEnvironment(Environments.Development),
            CreateHttpClientFactory(),
            roleState);

        roleState.SetRole(selectedRole);

        string token = await provider.GetAccessTokenAsync();
        ClaimsPrincipal principal = ValidateDevelopmentToken(config, token, out JwtSecurityToken validatedToken);
        JsonElement payload = DecodePayload(token);

        validatedToken.Header.Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
        principal.FindFirst("sub")!.Value.ShouldBe("test-user");
        payload.GetProperty(AdminClaimTypes.Role).GetString().ShouldBe(selectedRole.ToString());
        payload.GetProperty("sub").GetString().ShouldBe("test-user");
        payload.GetProperty("iss").GetString().ShouldBe("hexalith-dev");
        payload.GetProperty("aud").GetString().ShouldBe("hexalith-eventstore");
        payload.GetProperty("tenants").GetString().ShouldNotBeNull().ShouldContain("tenant-a");
        payload.GetProperty("domains").GetString().ShouldNotBeNull().ShouldContain("counter");
        payload.GetProperty("permissions").GetString().ShouldNotBeNull().ShouldContain("admin:read");
        payload.EnumerateObject().Count(p => p.Name == AdminClaimTypes.Role).ShouldBe(1);

        if (selectedRole is AdminRole.Admin) {
            payload.GetProperty("global_admin").GetBoolean().ShouldBeTrue();
        }
        else {
            payload.TryGetProperty("global_admin", out _).ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData("Subject", "Subject")]
    [InlineData("Tenants:0", "Tenants")]
    [InlineData("Domains:0", "Domains")]
    [InlineData("Permissions:0", "Permissions")]
    public async Task GetAccessTokenAsync_WhenRequiredLocalIdentitySettingIsMissingOrBlank_FailsClosed(
        string settingPath,
        string expectedName)
    {
        ArgumentNullException.ThrowIfNull(settingPath);

        foreach (string? invalidValue in new string?[] { null, "   " })
        {
            Dictionary<string, string?> values = CreateConfigValues();
            string key = $"EventStore:Authentication:{settingPath}";
            if (settingPath.StartsWith("Permissions:", StringComparison.Ordinal))
            {
                foreach (string permissionKey in values.Keys
                    .Where(candidate => candidate.StartsWith("EventStore:Authentication:Permissions:", StringComparison.Ordinal))
                    .ToArray())
                {
                    values.Remove(permissionKey);
                }
            }

            if (invalidValue is null)
            {
                values.Remove(key);
            }
            else
            {
                values[key] = invalidValue;
            }

            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var provider = new AdminApiAccessTokenProvider(
                config,
                new TestHostEnvironment(Environments.Development),
                CreateHttpClientFactory());

            InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
                () => provider.GetAccessTokenAsync());

            exception.Message.ShouldContain(expectedName);
        }
    }

    [Fact]
    public async Task GetAccessTokenAsync_InvalidatesCachedTokenAfterRoleChange() {
        IConfiguration config = CreateDevelopmentConfig();
        var roleState = new DevelopmentAdminRoleState(config, new TestHostEnvironment("Development"));
        var provider = new AdminApiAccessTokenProvider(
            config,
            new TestHostEnvironment(Environments.Development),
            CreateHttpClientFactory(),
            roleState);

        string adminToken = await provider.GetAccessTokenAsync();
        roleState.SetRole(AdminRole.ReadOnly);
        string readOnlyToken = await provider.GetAccessTokenAsync();

        readOnlyToken.ShouldNotBe(adminToken);
        DecodePayload(readOnlyToken).GetProperty(AdminClaimTypes.Role).GetString().ShouldBe("ReadOnly");
    }

    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Development", "https://security/realms/test", false)]
    [InlineData("Development", null, true)]
    public void IsRoleSwitcherAvailable_RequiresDevelopmentWithoutAuthority(string environmentName, string? authority, bool expected) {
        Dictionary<string, string?> values = CreateConfigValues();
        values["EventStore:Authentication:Authority"] = authority;
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var roleState = new DevelopmentAdminRoleState(config, new TestHostEnvironment(environmentName));

        roleState.IsRoleSwitcherAvailable.ShouldBe(expected);
    }

    [Fact]
    public async Task GetAccessTokenAsync_OutsideDevelopmentWithoutAuthority_RejectsLocalMinting() {
        IConfiguration config = CreateDevelopmentConfig();
        var provider = new AdminApiAccessTokenProvider(
            config,
            new TestHostEnvironment(Environments.Production),
            CreateHttpClientFactory());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("only in the Development environment");
    }

    [Theory]
    [InlineData("identity.example.com")]
    [InlineData("http://identity.example.com")]
    [InlineData("https://user" + "@identity.example.com")]
    [InlineData("https://identity.example.com#realm")]
    public void BuildTokenEndpoint_UnsafeProductionAuthority_Fails(string authority) {
        _ = Should.Throw<InvalidOperationException>(
            () => AdminApiAccessTokenProvider.BuildTokenEndpoint(authority, allowHttp: false));
    }

    [Fact]
    public void BuildTokenEndpoint_WithFixedHttpsQuery_PreservesTheQuery() {
        Uri endpoint = AdminApiAccessTokenProvider.BuildTokenEndpoint(
            "https://identity.example.com/oauth/token?tenant=stable",
            allowHttp: false);

        endpoint.AbsoluteUri.ShouldBe("https://identity.example.com/oauth/token?tenant=stable");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithProductionAuthority_PostsExpectedFormAndCachesResponse()
    {
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
        values["EventStore:Authentication:ClientId"] = "admin-ui";
        values["EventStore:Authentication:Username"] = "runtime-admin";
        string expectedPassword = " " + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + " ";
        values["EventStore:Authentication:Password"] = expectedPassword;
        values["EventStore:Authentication:AudienceParameterName"] = "resource";
        values["EventStore:Authentication:AudienceParameterValue"] = "https://api.example.test";
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var handler = new RecordingTokenHandler();
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        var provider = new AdminApiAccessTokenProvider(
            config,
            new TestHostEnvironment(Environments.Production),
            factory);

        string first = await provider.GetAccessTokenAsync();
        string second = await provider.GetAccessTokenAsync();

        first.ShouldBe(handler.AccessToken);
        second.ShouldBe(first);
        handler.RequestCount.ShouldBe(1);
        handler.RequestUri.ShouldBe(new Uri("https://tokens.example.test/oauth/token"));
        handler.FormValues.ShouldBe(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-ui",
            ["scope"] = "api.read",
            ["username"] = "runtime-admin",
            ["password"] = expectedPassword,
            ["resource"] = "https://api.example.test",
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithClientCredentials_SendsOnlyProfileFieldsAndCachesResponse()
    {
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
        string clientSecret = " " + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + " ";
        values["EventStore:Authentication:GrantType"] = "client_credentials";
        values["EventStore:Authentication:ClientSecret"] = clientSecret;
        values["EventStore:Authentication:Username"] = "PROTECTED_UNUSED_USERNAME_MARKER";
        values["EventStore:Authentication:Password"] = "PROTECTED_UNUSED_PASSWORD_MARKER";
        var handler = new RecordingTokenHandler();
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        var provider = new AdminApiAccessTokenProvider(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new TestHostEnvironment(Environments.Production),
            factory);

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
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
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
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
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
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
        values["EventStore:Authentication:TokenEndpoint"] =
            "https://tokens.example.test/oauth/token?tenant=stable";
        var handler = new RecordingTokenHandler();
        var provider = CreateAuthorityProvider(values, handler);

        _ = await provider.GetAccessTokenAsync();

        handler.RequestUri.ShouldBe(
            new Uri("https://tokens.example.test/oauth/token?tenant=stable"));
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithDevelopmentHttpTokenEndpoint_PostsTheGrant()
    {
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
        values["EventStore:Authentication:Authority"] = "http://identity.example.test/tenant";
        values["EventStore:Authentication:TokenEndpoint"] = "http://tokens.example.test/oauth/token";
        var handler = new RecordingTokenHandler();
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        var provider = new AdminApiAccessTokenProvider(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new TestHostEnvironment(Environments.Development),
            factory);

        string token = await provider.GetAccessTokenAsync();

        token.ShouldBe(handler.AccessToken);
        handler.RequestCount.ShouldBe(1);
        handler.RequestUri.ShouldBe(new Uri("http://tokens.example.test/oauth/token"));
        handler.FormValues["grant_type"].ShouldBe("password");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithoutExplicitEndpoint_UsesMatchingIssuerDiscoveryAndCachesResponse()
    {
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
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
            new Uri("https://identity.example.test/tenant/oauth/token"),
        ]);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenDiscoveryTokenEndpointEscapesAuthorityOrigin_RejectsBeforeSendingCredentials()
    {
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
        values.Remove("EventStore:Authentication:TokenEndpoint");
        var handler = new RecordingTokenHandler(discoveryTokenEndpoint: "https://tokens.example.test/oauth/token");
        var provider = CreateAuthorityProvider(values, handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("Authority origin");
        handler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenDiscoveryIssuerDoesNotMatchAuthority_RejectsBeforeSendingCredentials()
    {
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
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
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
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
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
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
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
        values["EventStore:Authentication:AudienceParameterName"] = "audience";
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
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
        var provider = CreateAuthorityProvider(values, new RecordingTokenHandler(tokenResponseJson: responseJson));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("non-blank access_token");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAccessTokenAsync_WhenOidcResponseExceedsBound_FailsClosed(bool discovery)
    {
        Dictionary<string, string?> values = CreateAuthorityConfigValues();
        string oversizedResponse = new('x', (64 * 1024) + 1);
        RecordingTokenHandler handler;
        if (discovery)
        {
            values.Remove("EventStore:Authentication:TokenEndpoint");
            handler = new RecordingTokenHandler(discoveryResponseJson: oversizedResponse);
        }
        else
        {
            handler = new RecordingTokenHandler(tokenResponseJson: oversizedResponse);
        }

        var provider = CreateAuthorityProvider(values, handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        exception.Message.ShouldContain("exceeded the permitted size");
    }

    [Fact]
    public async Task AddHttpClient_ProductionClient_DoesNotFollowCredentialPostRedirect()
    {
        int sourcePort = GetAvailablePort();
        int untrustedPort = GetAvailablePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{sourcePort}/");
        listener.Start();
        Task responder = Task.Run(async () =>
        {
            HttpListenerContext context = await listener.GetContextAsync();
            context.Response.StatusCode = (int)HttpStatusCode.TemporaryRedirect;
            context.Response.RedirectLocation = $"http://127.0.0.1:{untrustedPort}/relay";
            context.Response.Close();
        });
        var services = new ServiceCollection();
        AdminApiAccessTokenProvider.AddHttpClient(services);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using HttpClient client = serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(AdminApiAccessTokenProvider));
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_secret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
        });

        using HttpResponseMessage response = await client.PostAsync(
            $"http://127.0.0.1:{sourcePort}/token",
            form);
        await responder;

        response.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect);
    }

    private static IConfiguration CreateDevelopmentConfig()
        => new ConfigurationBuilder().AddInMemoryCollection(CreateConfigValues()).Build();

    private static Dictionary<string, string?> CreateConfigValues()
        => new() {
            ["EventStore:Authentication:Issuer"] = "hexalith-dev",
            ["EventStore:Authentication:Audience"] = "hexalith-eventstore",
            ["EventStore:Authentication:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["EventStore:Authentication:Subject"] = "test-user",
            ["EventStore:Authentication:GlobalAdmin"] = "true",
            ["EventStore:Authentication:Tenants:0"] = "tenant-a",
            ["EventStore:Authentication:Domains:0"] = "counter",
            ["EventStore:Authentication:Permissions:0"] = "admin:read",
            ["EventStore:Authentication:Permissions:1"] = "admin:write",
        };

    private static Dictionary<string, string?> CreateAuthorityConfigValues()
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

    private static AdminApiAccessTokenProvider CreateAuthorityProvider(
        Dictionary<string, string?> values,
        RecordingTokenHandler handler)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        return new AdminApiAccessTokenProvider(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new TestHostEnvironment(Environments.Production),
            factory);
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static IHttpClientFactory CreateHttpClientFactory() {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());
        return factory;
    }

    private static JsonElement DecodePayload(string token) {
        string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.Clone();
    }

    private static ClaimsPrincipal ValidateDevelopmentToken(
        IConfiguration configuration,
        string token,
        out JwtSecurityToken validatedToken)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        ClaimsPrincipal principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["EventStore:Authentication:Issuer"],
            ValidateAudience = true,
            ValidAudience = configuration["EventStore:Authentication:Audience"],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                configuration["EventStore:Authentication:SigningKey"]!)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
        }, out SecurityToken securityToken);
        validatedToken = securityToken.ShouldBeOfType<JwtSecurityToken>();
        return principal;
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Hexalith.EventStore.Admin.UI.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class RecordingTokenHandler : HttpMessageHandler
    {
        private readonly string _discoveryIssuer;
        private readonly string? _discoveryResponseJson;
        private readonly string _discoveryTokenEndpoint;
        private readonly string _tokenResponseJson;

        public RecordingTokenHandler(
            string? discoveryIssuer = null,
            string? discoveryTokenEndpoint = null,
            string? discoveryResponseJson = null,
            string? tokenResponseJson = null)
        {
            AccessToken = Guid.NewGuid().ToString("N");
            _discoveryIssuer = discoveryIssuer ?? "https://identity.example.test/tenant";
            _discoveryTokenEndpoint = discoveryTokenEndpoint
                ?? "https://identity.example.test/tenant/oauth/token";
            _discoveryResponseJson = discoveryResponseJson;
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
                    Content = new StringContent(_discoveryResponseJson ?? JsonSerializer.Serialize(new
                    {
                        issuer = _discoveryIssuer,
                        token_endpoint = _discoveryTokenEndpoint,
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
