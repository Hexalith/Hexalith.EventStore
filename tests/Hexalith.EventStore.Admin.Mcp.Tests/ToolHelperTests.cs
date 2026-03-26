namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;

public class ToolHelperTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "unauthorized")]
    [InlineData(HttpStatusCode.NotFound, "not-found")]
    [InlineData(HttpStatusCode.InternalServerError, "server-error")]
    [InlineData(HttpStatusCode.BadGateway, "server-error")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "server-error")]
    public void HandleHttpException_CategorizesStatusCodes(HttpStatusCode statusCode, string expectedStatus)
    {
        var ex = new HttpRequestException("Test error", null, statusCode);

        string result = ToolHelper.HandleHttpException(ex);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe(expectedStatus);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void HandleHttpException_NullStatusCode_ReturnsUnreachable()
    {
        var ex = new HttpRequestException("Connection refused");

        string result = ToolHelper.HandleHttpException(ex);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public void HandleException_TaskCanceledException_ReturnsTimeout()
    {
        var ex = new TaskCanceledException("The request was canceled");

        string result = ToolHelper.HandleException(ex);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("timeout");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("timed out");
    }

    [Fact]
    public void HandleException_HttpRequestException_DelegatesCorrectly()
    {
        var ex = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

        string result = ToolHelper.HandleException(ex);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unauthorized");
    }

    [Fact]
    public void SerializeResult_ProducesValidCamelCaseJson()
    {
        var data = new { TestProperty = "value", NestedObject = new { InnerProp = 42 } };

        string result = ToolHelper.SerializeResult(data);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("testProperty").GetString().ShouldBe("value");
        doc.RootElement.GetProperty("nestedObject").GetProperty("innerProp").GetInt32().ShouldBe(42);
    }

    [Fact]
    public void SerializeError_ProducesStandardErrorShape()
    {
        string result = ToolHelper.SerializeError("not-found", "Entity not found");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
        doc.RootElement.GetProperty("message").GetString().ShouldBe("Entity not found");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void HandleHttpException_5xxStatusCodes_IncludeStatusCodeInMessage(HttpStatusCode statusCode)
    {
        var ex = new HttpRequestException("Server error", null, statusCode);

        string result = ToolHelper.HandleHttpException(ex);

        using JsonDocument doc = JsonDocument.Parse(result);
        string message = doc.RootElement.GetProperty("message").GetString()!;
        message.ShouldContain(((int)statusCode).ToString());
    }

    [Fact]
    public void HandleException_GenericException_ReturnsServerError()
    {
        var ex = new InvalidOperationException("Something unexpected happened");

        string result = ToolHelper.HandleException(ex);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("server-error");
        doc.RootElement.GetProperty("message").GetString().ShouldBe("Something unexpected happened");
    }

    [Fact]
    public void HandleException_JsonException_ReturnsServerErrorWithClearMessage()
    {
        var ex = new JsonException("'<' is an invalid start of a value.");

        string result = ToolHelper.HandleException(ex);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("server-error");
        doc.RootElement.GetProperty("message").GetString().ShouldBe("Server returned non-JSON response");
    }

    [Fact]
    public void ValidateRequired_ReturnsNull_WhenAllValid()
    {
        string? result = ToolHelper.ValidateRequired(("value1", "param1"), ("value2", "param2"));

        result.ShouldBeNull();
    }

    [Fact]
    public void ValidateRequired_ReturnsError_WhenEmpty()
    {
        string? result = ToolHelper.ValidateRequired(("", "tenantId"));

        result.ShouldNotBeNull();
        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("tenantId");
    }

    [Fact]
    public void ValidateRequired_ReturnsError_WhenWhitespace()
    {
        string? result = ToolHelper.ValidateRequired(("valid", "p1"), ("  ", "p2"));

        result.ShouldNotBeNull();
        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("p2");
    }
}
