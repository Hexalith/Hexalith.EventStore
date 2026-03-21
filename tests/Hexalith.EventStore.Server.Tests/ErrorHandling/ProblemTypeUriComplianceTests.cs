
using System.Text.Json;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.ErrorHandling;

/// <summary>
/// Validates UX-DR6 (no event sourcing terminology) and UX-DR7 (hexalith.io type URIs)
/// compliance across all error handlers. Prevents regression.
/// </summary>
public class ProblemTypeUriComplianceTests {
    private static readonly string[] ForbiddenTerms =
    [
        "aggregate",
        "event stream",
        "event store",
        "actor",
        "DAPR",
        "sidecar",
        "pub/sub",
        "state store",
        "projection",
        "snapshot",
        "rehydration",
    ];

    private static DefaultHttpContext CreateHttpContextWithBody() {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "compliance-test";
        httpContext.Request.Path = "/api/v1/commands";
        return httpContext;
    }

    private static async Task<ProblemDetails?> ReadProblemDetails(HttpContext context) {
        _ = context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body).ConfigureAwait(false);
    }

    [Fact]
    public async Task ConcurrencyConflictHandler_UsesHexalithTypeUri() {
        // Arrange
        var handler = new ConcurrencyConflictExceptionHandler(
            new InMemoryCommandStatusStore(),
            NullLogger<ConcurrencyConflictExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new ConcurrencyConflictException("corr-1", "agg-1", "tenant-1");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldStartWith("https://hexalith.io/problems/");
        problem.Type.ShouldNotContain("rfc9457");
        problem.Type.ShouldNotContain("tools.ietf.org");
    }

    [Fact]
    public async Task AuthorizationHandler_UsesHexalithTypeUri() {
        // Arrange
        var handler = new AuthorizationExceptionHandler(
            NullLogger<AuthorizationExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new CommandAuthorizationException("tenant-1", "domain", "cmd", "Not authorized.");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldStartWith("https://hexalith.io/problems/");
    }

    [Fact]
    public async Task AuthorizationServiceUnavailableHandler_UsesHexalithTypeUri() {
        // Arrange
        var handler = new AuthorizationServiceUnavailableHandler(
            NullLogger<AuthorizationServiceUnavailableHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new AuthorizationServiceUnavailableException("Actor", "id", "reason", new Exception());

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldStartWith("https://hexalith.io/problems/");
    }

    [Fact]
    public async Task GlobalExceptionHandler_UsesHexalithTypeUri() {
        // Arrange
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new InvalidOperationException("test error");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldStartWith("https://hexalith.io/problems/");
    }

    [Fact]
    public async Task QueryNotFoundHandler_UsesHexalithTypeUri() {
        // Arrange
        var handler = new QueryNotFoundExceptionHandler(NullLogger<QueryNotFoundExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new QueryNotFoundException("tenant", "domain", "agg-1", "GetStatus");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldStartWith("https://hexalith.io/problems/");
    }

    [Fact]
    public async Task ConcurrencyConflictHandler_DetailHasNoForbiddenTerms() {
        // Arrange
        var handler = new ConcurrencyConflictExceptionHandler(
            new InMemoryCommandStatusStore(),
            NullLogger<ConcurrencyConflictExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new ConcurrencyConflictException("corr-1", "agg-1", "tenant-1");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        AssertNoForbiddenTerms(problem);
    }

    [Fact]
    public async Task AuthorizationHandler_DetailHasNoForbiddenTerms_WhenReasonContainsActorTerminology() {
        // Arrange — actor validators return reasons containing "actor" (UX-DR6 regression guard)
        var handler = new AuthorizationExceptionHandler(
            NullLogger<AuthorizationExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new CommandAuthorizationException("tenant-1", "domain", "cmd", "Tenant access denied by actor.");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        AssertNoForbiddenTerms(problem);
    }

    [Fact]
    public async Task AuthorizationServiceUnavailableHandler_DetailHasNoForbiddenTerms() {
        // Arrange
        var handler = new AuthorizationServiceUnavailableHandler(
            NullLogger<AuthorizationServiceUnavailableHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new AuthorizationServiceUnavailableException("Actor", "id", "reason", new Exception());

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        AssertNoForbiddenTerms(problem);
    }

    [Fact]
    public async Task QueryNotFoundHandler_DetailHasNoForbiddenTerms() {
        // Arrange
        var handler = new QueryNotFoundExceptionHandler(NullLogger<QueryNotFoundExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new QueryNotFoundException("tenant", "domain", "agg-1", "GetStatus");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        AssertNoForbiddenTerms(problem);
    }

    [Fact]
    public async Task GlobalExceptionHandler_DetailHasNoForbiddenTerms() {
        // Arrange
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new InvalidOperationException("test error");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(httpContext);
        _ = problem.ShouldNotBeNull();
        AssertNoForbiddenTerms(problem);
    }

    [Fact]
    public void ProblemTypeUris_AllConstantsUseHexalithDomain() {
        // Assert all type URI constants point to hexalith.io
        ProblemTypeUris.ValidationError.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.AuthenticationRequired.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.TokenExpired.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.BadRequest.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.Forbidden.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.NotFound.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.ConcurrencyConflict.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.RateLimitExceeded.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.ServiceUnavailable.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.CommandStatusNotFound.ShouldStartWith("https://hexalith.io/");
        ProblemTypeUris.InternalServerError.ShouldStartWith("https://hexalith.io/");
    }

    [Fact]
    public void ProblemTypeUris_AllConstantsAreUnique() {
        // Assert all type URIs are unique per error category (UX-DR7)
        string[] allUris =
        [
            ProblemTypeUris.ValidationError,
            ProblemTypeUris.AuthenticationRequired,
            ProblemTypeUris.TokenExpired,
            ProblemTypeUris.BadRequest,
            ProblemTypeUris.Forbidden,
            ProblemTypeUris.NotFound,
            ProblemTypeUris.ConcurrencyConflict,
            ProblemTypeUris.RateLimitExceeded,
            ProblemTypeUris.ServiceUnavailable,
            ProblemTypeUris.CommandStatusNotFound,
            ProblemTypeUris.InternalServerError,
        ];

        allUris.Distinct().Count().ShouldBe(allUris.Length);
    }

    [Fact]
    public void RateLimitExceeded_TypeUri_IsHexalithUri() {
        // Verify 429 rate limit response uses Hexalith URI (Task 1.8 / Task 7.8)
        ProblemTypeUris.RateLimitExceeded.ShouldStartWith("https://hexalith.io/problems/");
        ProblemTypeUris.RateLimitExceeded.ShouldNotContain("rfc6585");
        ProblemTypeUris.RateLimitExceeded.ShouldNotContain("tools.ietf.org");
    }

    [Fact]
    public void RateLimitResponse_ProblemDetailsShape_IsCompliant() {
        // Verify the 429 ProblemDetails shape matches the rate limiter callback pattern
        // This is a contract test for the response format produced by the OnRejected callback
        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Type = ProblemTypeUris.RateLimitExceeded,
            Detail = "Rate limit exceeded for tenant 'test-tenant'. Please retry after the specified interval.",
            Instance = "/api/v1/commands",
            Extensions = {
                ["correlationId"] = "test-correlation",
                ["tenantId"] = "test-tenant",
            },
        };

        problemDetails.Status.ShouldBe(429);
        problemDetails.Type.ShouldStartWith("https://hexalith.io/problems/");
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Extensions.ShouldContainKey("tenantId");
        AssertNoForbiddenTerms(problemDetails);
    }

    [Fact]
    public void SourceCode_RateLimiterCallback_UsesProblemTypeUrisConstant() {
        // Regression test: verify the rate limiter OnRejected callback uses
        // ProblemTypeUris.RateLimitExceeded, not an inline RFC string (Task 7.8)
        string sourceFile = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.CommandApi", "Extensions", "ServiceCollectionExtensions.cs");
        File.Exists(sourceFile).ShouldBeTrue($"Expected to find source file at '{sourceFile}'.");

        string content = File.ReadAllText(sourceFile);
        content.ShouldContain("ProblemTypeUris.RateLimitExceeded");
        content.ShouldNotContain("rfc6585");
        content.ShouldNotContain("JsonSerializer.Serialize(new { status = 429, title = \"Too Many Requests\" })");
    }

    private static void AssertNoForbiddenTerms(ProblemDetails problem) {
        foreach (string term in ForbiddenTerms) {
            problem.Title?.ShouldNotContain(term, Case.Insensitive,
                    $"Title contains forbidden term '{term}'");

            problem.Detail?.ShouldNotContain(term, Case.Insensitive,
                    $"Detail contains forbidden term '{term}'");

            foreach (KeyValuePair<string, object?> kvp in problem.Extensions) {
                kvp.Key.ShouldNotContain(term, Case.Insensitive,
                    $"Extension key '{kvp.Key}' contains forbidden term '{term}'");
            }
        }
    }
}
