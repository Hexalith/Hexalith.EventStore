using System.Security.Claims;
using System.Text.Encodings.Web;

using Hexalith.EventStore.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        result.Principal.ShouldNotBeNull();
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

    private static async Task<AuthenticateResult> AuthenticateAsync(string? header, IList<string> allowedCallers) {
        var options = new DaprInternalAuthenticationOptions {
            AllowedCallers = allowedCallers,
        };

        var optionsMonitor = new TestOptionsMonitor<DaprInternalAuthenticationOptions>(options);
        var handler = new DaprInternalAuthenticationHandler(optionsMonitor, NullLoggerFactory.Instance, UrlEncoder.Default);

        var scheme = new AuthenticationScheme(
            DaprInternalAuthenticationOptions.SchemeName,
            null,
            typeof(DaprInternalAuthenticationHandler));

        var httpContext = new DefaultHttpContext();
        if (header is not null) {
            httpContext.Request.Headers[DaprInternalAuthenticationOptions.CallerHeaderName] = header;
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
