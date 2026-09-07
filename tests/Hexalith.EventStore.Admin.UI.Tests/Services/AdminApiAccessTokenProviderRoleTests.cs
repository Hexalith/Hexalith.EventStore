using System.Text.Json;
using System.Security.Cryptography;
using System.Net;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
        JsonElement payload = DecodePayload(token);

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
    [InlineData("https://user@identity.example.com")]
    [InlineData("https://identity.example.com?realm=x")]
    [InlineData("https://identity.example.com#realm")]
    public void BuildTokenEndpoint_UnsafeProductionAuthority_Fails(string authority) {
        _ = Should.Throw<InvalidOperationException>(
            () => AdminApiAccessTokenProvider.BuildTokenEndpoint(authority, allowHttp: false));
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
        values["EventStore:Authentication:Username"] = "must-not-be-sent";
        values["EventStore:Authentication:Password"] = "must-not-be-sent";
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
    [InlineData("https://user@identity.example.test")]
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
    [InlineData("https://user@tokens.example.test/oauth/token")]
    [InlineData("https://tokens.example.test/oauth/token?tenant=x")]
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
            new Uri("https://tokens.example.test/oauth/token"),
        ]);
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
