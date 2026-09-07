extern alias BlazorUI;

using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

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
        Dictionary<string, string?> values = CreateLocalConfiguration();
        string password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        values["EventStore:Authentication:Authority"] = "https://identity.example.test/realms/hexalith";
        values["EventStore:Authentication:ClientId"] = "sample-ui";
        values["EventStore:Authentication:Username"] = "tenant-a-user";
        values["EventStore:Authentication:Password"] = password;
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var handler = new RecordingTokenHandler();
        var provider = new EventStoreApiAccessTokenProvider(
            configuration,
            new TestHostEnvironment(Environments.Production),
            new TestHttpClientFactory(handler));

        string first = await provider.GetAccessTokenAsync();
        string second = await provider.GetAccessTokenAsync();

        first.ShouldBe("issued-token");
        second.ShouldBe(first);
        handler.RequestCount.ShouldBe(1);
        handler.RequestUri.ShouldBe(new Uri("https://identity.example.test/realms/hexalith/protocol/openid-connect/token"));
        handler.FormValues["grant_type"].ShouldBe("password");
        handler.FormValues["client_id"].ShouldBe("sample-ui");
        handler.FormValues["username"].ShouldBe("tenant-a-user");
        handler.FormValues["password"].ShouldBe(password);
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
        public int RequestCount { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string Form { get; private set; } = string.Empty;

        public IReadOnlyDictionary<string, string> FormValues { get; private set; }
            = new Dictionary<string, string>(StringComparer.Ordinal);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            Form = await request.Content!.ReadAsStringAsync(cancellationToken);
            FormValues = Form.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(static component => component.Split('=', 2))
                .ToDictionary(
                    static pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                    static pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')),
                    StringComparer.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"issued-token\",\"expires_in\":3600}"),
            };
        }
    }
}
