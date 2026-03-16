
using System.Text.Json;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.CommandApi.ErrorHandling;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.ErrorHandling;

public class ValidationExceptionHandlerTests
{
    private readonly ILogger<ValidationExceptionHandler> _logger;
    private readonly ValidationExceptionHandler _handler;

    public ValidationExceptionHandlerTests()
    {
        _logger = Substitute.For<ILogger<ValidationExceptionHandler>>();
        _handler = new ValidationExceptionHandler(_logger);
    }

    private static DefaultHttpContext CreateHttpContextWithBody()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    [Fact]
    public async Task TryHandleAsync_SingleValidationError_Returns400WithCorrectTypeAndTitle()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        var failures = new List<ValidationFailure>
        {
            new("MessageId", "MessageId is required"),
        };
        var exception = new ValidationException(failures);

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(400);

        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
        problemDetails.Type.ShouldBe("https://hexalith.io/problems/validation-error");
        problemDetails.Title.ShouldBe("Command Validation Failed");
        problemDetails.Instance.ShouldBe("/api/v1/commands");
    }

    [Fact]
    public async Task TryHandleAsync_SingleValidationError_ErrorsKeyIsCamelCase()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        var failures = new List<ValidationFailure>
        {
            new("MessageId", "MessageId is required"),
        };
        var exception = new ValidationException(failures);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("errors");

        // Deserialize the errors dictionary to verify camelCase keys
        JsonElement errorsElement = (JsonElement)problemDetails.Extensions["errors"]!;
        errorsElement.TryGetProperty("messageId", out JsonElement messageIdError).ShouldBeTrue();
        messageIdError.GetString().ShouldBe("MessageId is required");
    }

    [Fact]
    public async Task TryHandleAsync_MultipleErrorsSameProperty_JoinedWithSemicolon()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        var failures = new List<ValidationFailure>
        {
            new("Tenant", "Tenant is required"),
            new("Tenant", "Tenant cannot be empty"),
        };
        var exception = new ValidationException(failures);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();

        JsonElement errorsElement = (JsonElement)problemDetails.Extensions["errors"]!;
        errorsElement.TryGetProperty("tenant", out JsonElement tenantError).ShouldBeTrue();
        tenantError.GetString().ShouldBe("Tenant is required; Tenant cannot be empty");
    }

    [Fact]
    public async Task TryHandleAsync_NonValidationException_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var exception = new InvalidOperationException("some other error");

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_IncludesCorrelationIdAndTenantId()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "my-correlation-id";
        httpContext.Items["RequestTenantId"] = "acme-tenant";
        httpContext.Request.Path = "/api/v1/commands";

        var failures = new List<ValidationFailure>
        {
            new("Domain", "Domain is required"),
        };
        var exception = new ValidationException(failures);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Extensions["correlationId"]!.ToString().ShouldBe("my-correlation-id");
        problemDetails.Extensions.ShouldContainKey("tenantId");
        problemDetails.Extensions["tenantId"]!.ToString().ShouldBe("acme-tenant");
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_DoesNotContainLegacyExtensions()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        var failures = new List<ValidationFailure>
        {
            new("MessageId", "MessageId is required"),
        };
        var exception = new ValidationException(failures);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();

        // Legacy extensions must NOT be present
        problemDetails.Extensions.ShouldNotContainKey("validationErrors");
        problemDetails.Extensions.ShouldNotContainKey("errorsDictionary");
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_ContentTypeIsProblemJson()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        var failures = new List<ValidationFailure>
        {
            new("CommandType", "CommandType is required"),
        };
        var exception = new ValidationException(failures);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        httpContext.Response.ContentType.ShouldNotBeNull();
        httpContext.Response.ContentType!.ShouldContain("application/problem+json");
    }

    [Fact]
    public async Task TryHandleAsync_NullTenantId_TenantIdExtensionPresentButNull()
    {
        // Arrange - no RequestTenantId in HttpContext
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        var failures = new List<ValidationFailure>
        {
            new("MessageId", "MessageId is required"),
        };
        var exception = new ValidationException(failures);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert - tenantId should be present in extensions (even if null)
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("tenantId");
    }
}
