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
        Dictionary<string, string?> values = CreateConfigValues();
        values["EventStore:Authentication:Authority"] = "https://identity.example.test/realms/hexalith";
        values["EventStore:Authentication:ClientId"] = "admin-ui";
        values["EventStore:Authentication:Username"] = "runtime-admin";
        string expectedPassword = string.Concat("runtime", "-password");
        values["EventStore:Authentication:Password"] = expectedPassword;
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

        first.ShouldBe("issued-token");
        second.ShouldBe(first);
        handler.RequestCount.ShouldBe(1);
        handler.RequestUri.ShouldBe(new Uri("https://identity.example.test/realms/hexalith/protocol/openid-connect/token"));
        handler.Form.ShouldContain("grant_type=password");
        handler.Form.ShouldContain("client_id=admin-ui");
        handler.Form.ShouldContain("username=runtime-admin");
        handler.Form.ShouldContain("password=" + expectedPassword);
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
        public int RequestCount { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string Form { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            Form = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"issued-token\",\"expires_in\":3600}"),
            };
        }
    }
}
