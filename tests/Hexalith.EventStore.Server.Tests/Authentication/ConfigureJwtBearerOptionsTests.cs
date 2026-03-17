using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Authentication;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

public class ConfigureJwtBearerOptionsTests {
    private const string TestSigningKey = "this-is-a-test-signing-key-at-least-32-chars-long!!";
    private const string TestAuthority = "https://login.example.com";
    private const string TestIssuer = "test-issuer";
    private const string TestAudience = "test-audience";

    private static ConfigureJwtBearerOptions CreateConfigurer() {
        var authOptions = Options.Create(new EventStoreAuthenticationOptions {
            SigningKey = TestSigningKey,
            Issuer = TestIssuer,
            Audience = TestAudience,
        });
        return new ConfigureJwtBearerOptions(authOptions, NullLoggerFactory.Instance);
    }

    private static ConfigureJwtBearerOptions CreateOidcConfigurer() {
        var authOptions = Options.Create(new EventStoreAuthenticationOptions {
            Authority = TestAuthority,
            Issuer = TestIssuer,
            Audience = TestAudience,
        });
        return new ConfigureJwtBearerOptions(authOptions, NullLoggerFactory.Instance);
    }

    private static ConfigureJwtBearerOptions CreateDualConfigurer() {
        var authOptions = Options.Create(new EventStoreAuthenticationOptions {
            Authority = TestAuthority,
            SigningKey = TestSigningKey,
            Issuer = TestIssuer,
            Audience = TestAudience,
        });
        return new ConfigureJwtBearerOptions(authOptions, NullLoggerFactory.Instance);
    }

    private static JwtBearerOptions CreateConfiguredOptions() {
        var options = new JwtBearerOptions();
        CreateConfigurer().Configure(JwtBearerDefaults.AuthenticationScheme, options);
        return options;
    }

    private static (JwtBearerOptions Options, List<LogEntry> Entries) CreateConfiguredOptionsWithLogs() {
        List<LogEntry> entries = [];
        var options = new JwtBearerOptions();
        CreateConfigurer(new TestLoggerFactory(entries)).Configure(JwtBearerDefaults.AuthenticationScheme, options);
        return (options, entries);
    }

    private static ConfigureJwtBearerOptions CreateConfigurer(ILoggerFactory loggerFactory) {
        var authOptions = Options.Create(new EventStoreAuthenticationOptions {
            SigningKey = TestSigningKey,
            Issuer = TestIssuer,
            Audience = TestAudience,
        });
        return new ConfigureJwtBearerOptions(authOptions, loggerFactory);
    }

    private static string CreateUnsignedToken() {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        string payload = Base64UrlEncoder.Encode(
            JsonSerializer.Serialize(new {
                sub = "user-1",
                iss = TestIssuer,
                aud = TestAudience,
                nbf = now,
                exp = now + 300,
            }));

        return $"{header}.{payload}.";
    }

    private static DefaultHttpContext CreateHttpContextWithBody() {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""{"tenant":"tenant-a","commandType":"submit"}"""));
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        return httpContext;
    }

    private static async Task<ProblemDetails?> ReadProblemDetails(HttpContext context) {
        _ = context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }).ConfigureAwait(false);
    }

    [Fact]
    public async Task OnChallenge_MissingToken_Returns401WithBasicWwwAuthenticate() {
        // Arrange (AC #1)
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties());

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(401);

        // WWW-Authenticate header per RFC 6750
        string wwwAuth = httpContext.Response.Headers.WWWAuthenticate.ToString();
        wwwAuth.ShouldBe("Bearer realm=\"hexalith-eventstore\"");

        // ProblemDetails body
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Status.ShouldBe(401);
        problem.Type.ShouldBe(ProblemTypeUris.AuthenticationRequired);
        problem.Detail.ShouldBe("Authentication is required to access this resource.");
    }

    [Fact]
    public async Task OnChallenge_ExpiredToken_Returns401WithExpiredWwwAuthenticate() {
        // Arrange (AC #2)
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties()) {
            AuthenticateFailure = new SecurityTokenExpiredException("Token expired"),
        };

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(401);

        // WWW-Authenticate with expired-specific error
        string wwwAuth = httpContext.Response.Headers.WWWAuthenticate.ToString();
        wwwAuth.ShouldContain("Bearer realm=\"hexalith-eventstore\"");
        wwwAuth.ShouldContain("error=\"invalid_token\"");
        wwwAuth.ShouldContain("error_description=\"The token has expired\"");

        // ProblemDetails body
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldBe(ProblemTypeUris.TokenExpired);
        problem.Detail.ShouldBe("The provided authentication token has expired.");
    }

    [Fact]
    public async Task OnChallenge_InvalidSignature_Returns401WithInvalidWwwAuthenticate() {
        // Arrange (AC #3)
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties()) {
            AuthenticateFailure = new SecurityTokenInvalidSignatureException("Bad signature"),
        };

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(401);

        // WWW-Authenticate with invalid-token error
        string wwwAuth = httpContext.Response.Headers.WWWAuthenticate.ToString();
        wwwAuth.ShouldContain("Bearer realm=\"hexalith-eventstore\"");
        wwwAuth.ShouldContain("error=\"invalid_token\"");
        wwwAuth.ShouldContain("error_description=\"The token is invalid\"");

        // ProblemDetails body
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldBe(ProblemTypeUris.AuthenticationRequired);
        problem.Detail.ShouldBe("The provided authentication token is invalid.");
    }

    [Fact]
    public async Task OnChallenge_InvalidIssuer_Returns401WithGenericInvalidMessage() {
        // Arrange (AC #3 — invalid issuer consolidated to single "invalid" message)
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties()) {
            AuthenticateFailure = new SecurityTokenInvalidIssuerException("Wrong issuer"),
        };

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Detail.ShouldBe("The provided authentication token is invalid.");
    }

    [Fact]
    public async Task OnChallenge_InvalidTokenErrorWithoutAuthenticateFailure_ReturnsInvalidTokenResponse() {
        // Arrange - some JWT challenge flows only populate Error
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties()) {
            Error = "invalid_token",
        };

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(401);
        httpContext.Response.Headers.WWWAuthenticate.ToString()
            .ShouldBe("Bearer realm=\"hexalith-eventstore\", error=\"invalid_token\", error_description=\"The token is invalid\"");

        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldBe(ProblemTypeUris.AuthenticationRequired);
        problem.Detail.ShouldBe("The provided authentication token is invalid.");
    }

    [Fact]
    public async Task OnChallenge_UnknownErrorWithoutException_ReturnsMissingTokenResponse() {
        // Arrange — unrelated challenge error codes must not be rewritten as invalid-token responses
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties()) {
            Error = "insufficient_scope",
        };

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert — unknown challenge errors fall back to the generic missing-auth contract
        httpContext.Response.StatusCode.ShouldBe(401);
        httpContext.Response.Headers.WWWAuthenticate.ToString()
            .ShouldBe("Bearer realm=\"hexalith-eventstore\"");

        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldBe(ProblemTypeUris.AuthenticationRequired);
        problem.Detail.ShouldBe("Authentication is required to access this resource.");
    }

    [Fact]
    public async Task OnChallenge_NoCorrelationIdInResponse() {
        // Arrange (UX-DR2: No correlationId on 401 — pre-pipeline rejection)
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "should-not-appear";

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties());

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Extensions.ShouldNotContainKey("correlationId");
        problem.Extensions.ShouldNotContainKey("tenantId");
    }

    [Fact]
    public async Task OnChallenge_ContentTypeIsProblemJson() {
        // Arrange
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties());

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert
        _ = httpContext.Response.ContentType.ShouldNotBeNull();
        httpContext.Response.ContentType.ShouldContain("application/problem+json");
    }

    [Fact]
    public async Task OnChallenge_ResponseHasNoForbiddenTerminology() {
        // Arrange (UX-DR6)
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties()) {
            AuthenticateFailure = new SecurityTokenExpiredException("Token expired"),
        };

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        string body = await reader.ReadToEndAsync();

        body.ShouldNotContain("aggregate", Case.Insensitive);
        body.ShouldNotContain("actor", Case.Insensitive);
        body.ShouldNotContain("DAPR", Case.Insensitive);
        body.ShouldNotContain("sidecar", Case.Insensitive);
    }

    // --- Story 5.1 Gap-Closure Tests (5.3.1–5.3.8) ---

    [Fact]
    public void Configure_OidcMode_SetsAuthorityAndNoIssuerSigningKey() {
        // Arrange (5.3.1 — OIDC discovery mode: Authority set, no SigningKey)
        var options = new JwtBearerOptions();

        // Act
        CreateOidcConfigurer().Configure(JwtBearerDefaults.AuthenticationScheme, options);

        // Assert
        options.Authority.ShouldBe(TestAuthority);
        options.TokenValidationParameters.IssuerSigningKey.ShouldBeNull();
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        options.TokenValidationParameters.ValidateIssuerSigningKey.ShouldBeTrue();
        options.TokenValidationParameters.ValidateLifetime.ShouldBeTrue();
    }

    [Fact]
    public void Configure_SymmetricKeyMode_SetsIssuerSigningKeyAndNoAuthority() {
        // Arrange (symmetric key mode: SigningKey set, no Authority)
        var options = new JwtBearerOptions();

        // Act
        CreateConfigurer().Configure(JwtBearerDefaults.AuthenticationScheme, options);

        // Assert
        options.Authority.ShouldBeNull();
        options.TokenValidationParameters.IssuerSigningKey.ShouldNotBeNull();
        options.TokenValidationParameters.IssuerSigningKey.ShouldBeOfType<SymmetricSecurityKey>();
    }

    [Fact]
    public void Configure_MapInboundClaimsDisabled() {
        // Arrange (AC #7 — MapInboundClaims = false)
        JwtBearerOptions options = CreateConfiguredOptions();

        // Assert
        options.MapInboundClaims.ShouldBeFalse();
    }

    [Fact]
    public void Configure_TokenValidationParameters_AllSet() {
        // Arrange — verify all TokenValidationParameters
        JwtBearerOptions options = CreateConfiguredOptions();

        // Assert
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        options.TokenValidationParameters.ValidateIssuerSigningKey.ShouldBeTrue();
        options.TokenValidationParameters.ValidateLifetime.ShouldBeTrue();
        options.TokenValidationParameters.ClockSkew.ShouldBe(TimeSpan.FromMinutes(1));
        options.TokenValidationParameters.ValidIssuer.ShouldBe(TestIssuer);
        options.TokenValidationParameters.ValidAudience.ShouldBe(TestAudience);
    }

    [Fact]
    public async Task OnAuthenticationFailed_ExpiredToken_LogsStructuredWarningWithExpiryReason() {
        // Arrange (5.3.2 — expired token rejection event)
        (JwtBearerOptions options, List<LogEntry> entries) = CreateConfiguredOptionsWithLogs();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var failedContext = new AuthenticationFailedContext(httpContext, scheme, options) {
            Exception = new SecurityTokenExpiredException("Token has expired"),
        };

        // Act — should not throw
        await options.Events.OnAuthenticationFailed(failedContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(200); // not modified by OnAuthenticationFailed
        entries.Count.ShouldBe(1);
        entries[0].Level.ShouldBe(LogLevel.Warning);
        entries[0].Message.ShouldContain("Authentication failed");
        entries[0].Message.ShouldContain("SecurityEvent=AuthenticationFailed");
        entries[0].Message.ShouldContain("CorrelationId=test-correlation-id");
        entries[0].Message.ShouldContain("SourceIp=127.0.0.1");
        entries[0].Message.ShouldContain("Path=/api/v1/commands");
        entries[0].Message.ShouldContain("Tenant=tenant-a");
        entries[0].Message.ShouldContain("CommandType=submit");
        entries[0].Message.ShouldContain("Reason=TokenExpired");
        entries[0].Message.ShouldContain("FailureLayer=JwtValidation");
    }

    [Fact]
    public async Task OnAuthenticationFailed_InvalidIssuer_LogsStructuredWarningWithIssuerMismatch() {
        // Arrange (5.3.3 — wrong issuer rejection event)
        (JwtBearerOptions options, List<LogEntry> entries) = CreateConfiguredOptionsWithLogs();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var failedContext = new AuthenticationFailedContext(httpContext, scheme, options) {
            Exception = new SecurityTokenInvalidIssuerException("Issuer mismatch"),
        };

        // Act — should not throw
        await options.Events.OnAuthenticationFailed(failedContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(200);
        entries.Count.ShouldBe(1);
        entries[0].Level.ShouldBe(LogLevel.Warning);
        entries[0].Message.ShouldContain("Authentication failed");
        entries[0].Message.ShouldContain("SecurityEvent=AuthenticationFailed");
        entries[0].Message.ShouldContain("CorrelationId=test-correlation-id");
        entries[0].Message.ShouldContain("SourceIp=127.0.0.1");
        entries[0].Message.ShouldContain("Path=/api/v1/commands");
        entries[0].Message.ShouldContain("Tenant=tenant-a");
        entries[0].Message.ShouldContain("CommandType=submit");
        entries[0].Message.ShouldContain("Reason=InvalidIssuer");
        entries[0].Message.ShouldContain("FailureLayer=JwtValidation");
    }

    [Fact]
    public void ValidateToken_UnsignedJwt_ThrowsSecurityTokenInvalidSignatureException() {
        // Arrange (5.3.7 — algorithm confusion attack: alg:none must be rejected)
        JwtBearerOptions options = CreateConfiguredOptions();
        string token = CreateUnsignedToken();
        var handler = new JwtSecurityTokenHandler();

        // Act & Assert
        _ = Should.Throw<SecurityTokenInvalidSignatureException>(
            () => handler.ValidateToken(token, options.TokenValidationParameters, out _));
    }

    [Fact]
    public async Task OnChallenge_MissingToken_WritesProblemDetailsWithCorrectStructure() {
        // Arrange (5.3.4 + 5.3.5 — missing token challenge: 401, ProblemDetails, WWW-Authenticate)
        JwtBearerOptions options = CreateConfiguredOptions();
        DefaultHttpContext httpContext = CreateHttpContextWithBody();

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, scheme, options, new AuthenticationProperties());

        // Act
        await options.Events.OnChallenge(challengeContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(401);
        _ = httpContext.Response.ContentType.ShouldNotBeNull();
        httpContext.Response.ContentType.ShouldContain("application/problem+json");
        httpContext.Response.Headers.WWWAuthenticate.ToString().ShouldNotBeNullOrWhiteSpace();

        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldBe(ProblemTypeUris.AuthenticationRequired);
        problem.Status.ShouldBe(401);
        problem.Instance.ShouldBe("/api/v1/commands");
    }

    [Fact]
    public void Configure_DualConfig_AuthorityTakesPrecedenceOverSigningKey() {
        // Arrange (5.3.8 — when BOTH Authority AND SigningKey are set, Authority wins)
        var options = new JwtBearerOptions();

        // Act
        CreateDualConfigurer().Configure(JwtBearerDefaults.AuthenticationScheme, options);

        // Assert — OIDC path taken, symmetric key NOT set
        options.Authority.ShouldBe(TestAuthority);
        options.TokenValidationParameters.IssuerSigningKey.ShouldBeNull();
    }

    [Fact]
    public void Configure_WrongSchemeName_DoesNotModifyOptions() {
        // Arrange — scheme name mismatch should be a no-op
        var options = new JwtBearerOptions();

        // Act
        CreateConfigurer().Configure("WrongScheme", options);

        // Assert — our configurer did not set Authority or IssuerSigningKey
        options.Authority.ShouldBeNull();
        options.TokenValidationParameters.IssuerSigningKey.ShouldBeNull();
        options.TokenValidationParameters.ValidIssuer.ShouldBeNull();
        options.TokenValidationParameters.ValidAudience.ShouldBeNull();
    }

    private sealed class TestLoggerFactory(List<LogEntry> entries) : ILoggerFactory {
        public void AddProvider(ILoggerProvider provider) {
        }

        public ILogger CreateLogger(string categoryName) => new TestLogger(entries);

        public void Dispose() {
        }
    }

    private sealed class TestLogger(List<LogEntry> entries) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
