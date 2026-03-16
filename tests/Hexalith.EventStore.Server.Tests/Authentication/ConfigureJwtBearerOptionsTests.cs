
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Authentication;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

public class ConfigureJwtBearerOptionsTests {
    private const string TestSigningKey = "this-is-a-test-signing-key-at-least-32-chars-long!!";
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

    private static JwtBearerOptions CreateConfiguredOptions() {
        var options = new JwtBearerOptions();
        CreateConfigurer().Configure(JwtBearerDefaults.AuthenticationScheme, options);
        return options;
    }

    private static DefaultHttpContext CreateHttpContextWithBody() {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
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
}
