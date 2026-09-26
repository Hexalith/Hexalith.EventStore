using System.Security.Claims;
using System.Text.Encodings.Web;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.HealthChecks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

public class DaprInternalAuthenticationHandlerTests {
    [Fact]
    public async Task HandleAuthenticate_NoHeader_ReturnsNoResult() {
        AuthenticateResult result = await AuthenticateAsync(header: null, allowedCallers: ["tenants"]);

        result.Succeeded.ShouldBeFalse();
        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticate_CallerNotInAllowList_ReturnsNoResult() {
        AuthenticateResult result = await AuthenticateAsync(header: "eventstore-admin", allowedCallers: ["tenants"]);

        result.Succeeded.ShouldBeFalse();
        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticate_AllowedCaller_IssuesSystemPrincipalWithGlobalAdmin() {
        AuthenticateResult result = await AuthenticateAsync(header: "tenants", allowedCallers: ["tenants"]);

        result.Succeeded.ShouldBeTrue();
        _ = result.Principal.ShouldNotBeNull();
        result.Principal!.Identity?.IsAuthenticated.ShouldBeTrue();
        result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.ShouldBe("system:tenants");
        result.Principal.FindFirst("sub")?.Value.ShouldBe("system:tenants");
        result.Principal.FindFirst("global_admin")?.Value.ShouldBe("true");
        result.Principal.FindFirst("dapr_caller_app_id")?.Value.ShouldBe("tenants");
    }

    [Fact]
    public async Task HandleAuthenticate_EmptyAllowList_ReturnsNoResultForAnyCaller() {
        AuthenticateResult result = await AuthenticateAsync(header: "tenants", allowedCallers: []);

        result.Succeeded.ShouldBeFalse();
        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticate_HeaderIsCaseSensitiveMatch() {
        // Ordinal string comparison: "Tenants" (uppercase T) does NOT match "tenants".
        AuthenticateResult result = await AuthenticateAsync(header: "Tenants", allowedCallers: ["tenants"]);

        result.Succeeded.ShouldBeFalse();
        result.None.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("configured-token", null, false)]
    [InlineData("configured-token", "wrong-token", false)]
    [InlineData("configured-token", "configured-token", true)]
    public async Task HandleAuthenticate_ProductionRequiresDaprAppChannelToken(
        string? configuredToken,
        string? presentedToken,
        bool expectedSuccess) {
        AuthenticateResult result = await AuthenticateAsync(
            "tenants", ["tenants"], Environments.Production, configuredToken, presentedToken);

        result.Succeeded.ShouldBe(expectedSuccess);
    }

    [Theory]
    [InlineData(null, HealthStatus.Unhealthy)]
    [InlineData("configured-token", HealthStatus.Healthy)]
    public async Task AppChannelReadiness_ProductionRequiresConfiguredSecret(
        string? configuredToken,
        HealthStatus expectedStatus) {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["APP_API_TOKEN"] = configuredToken })
            .Build();
        var check = new DaprAppChannelTokenHealthCheck(
            new DaprAppChannelTokenValidator(environment, configuration));

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(expectedStatus);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        string? header,
        IList<string> allowedCallers,
        string environmentName = "Development",
        string? configuredToken = null,
        string? presentedToken = null) {
        var options = new DaprInternalAuthenticationOptions {
            AllowedCallers = allowedCallers,
        };

        var optionsMonitor = new TestOptionsMonitor<DaprInternalAuthenticationOptions>(options);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["APP_API_TOKEN"] = configuredToken })
            .Build();
        var validator = new DaprAppChannelTokenValidator(environment, configuration);
        var handler = new DaprInternalAuthenticationHandler(
            optionsMonitor, NullLoggerFactory.Instance, UrlEncoder.Default, validator);

        var scheme = new AuthenticationScheme(
            DaprInternalAuthenticationOptions.SchemeName,
            null,
            typeof(DaprInternalAuthenticationHandler));

        var httpContext = new DefaultHttpContext();
        if (header is not null) {
            httpContext.Request.Headers[DaprInternalAuthenticationOptions.CallerHeaderName] = header;
        }
        if (presentedToken is not null) {
            httpContext.Request.Headers[DaprAppChannelTokenValidator.HeaderName] = presentedToken;
        }

        await handler.InitializeAsync(scheme, httpContext);
        return await handler.AuthenticateAsync();
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T> {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
