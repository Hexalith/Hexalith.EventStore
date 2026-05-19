
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Security;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class ToolHelperTests {
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "unauthorized")]
    [InlineData(HttpStatusCode.NotFound, "not-found")]
    [InlineData(HttpStatusCode.Conflict, "conflict")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "invalid-operation")]
    [InlineData(HttpStatusCode.InternalServerError, "server-error")]
    [InlineData(HttpStatusCode.BadGateway, "server-error")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "service-unavailable")]
    public void HandleHttpException_CategorizesStatusCodes(HttpStatusCode statusCode, string expectedStatus) {
        var ex = new HttpRequestException("Test error", null, statusCode);

        string result = ToolHelper.HandleHttpException(ex);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe(expectedStatus);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void HandleHttpException_NullStatusCode_ReturnsUnreachable() {
        var ex = new HttpRequestException("Connection refused");

        string result = ToolHelper.HandleHttpException(ex);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public void HandleException_TaskCanceledException_ReturnsTimeout() {
        var ex = new TaskCanceledException("The request was canceled");

        string result = ToolHelper.HandleException(ex);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("timeout");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("timed out");
    }

    [Fact]
    public void HandleException_HttpRequestException_DelegatesCorrectly() {
        var ex = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

        string result = ToolHelper.HandleException(ex);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unauthorized");
    }

    [Fact]
    public void SerializeResult_ProducesValidCamelCaseJson() {
        var data = new { TestProperty = "value", NestedObject = new { InnerProp = 42 } };

        string result = ToolHelper.SerializeResult(data);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("testProperty").GetString().ShouldBe("value");
        doc.RootElement.GetProperty("nestedObject").GetProperty("innerProp").GetInt32().ShouldBe(42);
    }

    [Fact]
    public void SerializeResult_RawProtectedFields_AreProjectedToSafeDescriptorNames() {
        var data = new {
            TenantId = "acme",
            PayloadJson = ProtectedDataLeakSentinel.ProtectedPayloadPlaintext,
            StateJson = ProtectedDataLeakSentinel.ProtectedSnapshotPlaintext,
            SafeIdentifier = "stream-1",
        };

        string result = ToolHelper.SerializeResult(data);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        result.ShouldNotContain("payloadJson");
        result.ShouldNotContain("stateJson");
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("safeIdentifier").GetString().ShouldBe("stream-1");
        doc.RootElement.GetProperty("payload").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
        doc.RootElement.GetProperty("state").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
    }

    [Fact]
    public void SerializeError_ProducesStandardErrorShape() {
        string result = ToolHelper.SerializeError("not-found", "Entity not found");

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
        doc.RootElement.GetProperty("message").GetString().ShouldBe("Entity not found");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public void HandleHttpException_5xxStatusCodes_IncludeStatusCodeInMessage(HttpStatusCode statusCode) {
        var ex = new HttpRequestException("Server error", null, statusCode);

        string result = ToolHelper.HandleHttpException(ex);

        using var doc = JsonDocument.Parse(result);
        string message = doc.RootElement.GetProperty("message").GetString()!;
        message.ShouldContain(((int)statusCode).ToString());
    }

    [Fact]
    public void HandleException_GenericException_ReturnsServerError() {
        var ex = new InvalidOperationException("Something unexpected happened");

        string result = ToolHelper.HandleException(ex);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("server-error");
        doc.RootElement.GetProperty("message").GetString().ShouldBe("Unexpected Admin MCP tool error.");
    }

    [Fact]
    public void HandleException_GenericException_DoesNotLeakProtectedSentinel() {
        var ex = new InvalidOperationException(ProtectedDataLeakSentinel.ProtectedProviderExceptionText);

        string result = ToolHelper.HandleException(ex);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("server-error");
        doc.RootElement.GetProperty("message").GetString().ShouldBe("Unexpected Admin MCP tool error.");
    }

    [Fact]
    public void HandleException_JsonException_ReturnsServerErrorWithClearMessage() {
        var ex = new JsonException("'<' is an invalid start of a value.");

        string result = ToolHelper.HandleException(ex);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("server-error");
        doc.RootElement.GetProperty("message").GetString().ShouldBe("Server returned non-JSON response");
    }

    [Fact]
    public void ValidateRequired_ReturnsNull_WhenAllValid() {
        string? result = ToolHelper.ValidateRequired(("value1", "param1"), ("value2", "param2"));

        result.ShouldBeNull();
    }

    [Fact]
    public void ValidateRequired_ReturnsError_WhenEmpty() {
        string? result = ToolHelper.ValidateRequired(("", "tenantId"));

        _ = result.ShouldNotBeNull();
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("tenantId");
    }

    [Fact]
    public void ValidateRequired_ReturnsError_WhenWhitespace() {
        string? result = ToolHelper.ValidateRequired(("valid", "p1"), ("  ", "p2"));

        _ = result.ShouldNotBeNull();
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("p2");
    }

    [Fact]
    public void SerializePreview_ProducesCorrectShape() {
        string result = ToolHelper.SerializePreview(
            "projection-pause",
            "Pause projection 'OrderSummary' for tenant 'acme'",
            "POST /api/v1/admin/projections/acme/OrderSummary/pause",
            new { tenantId = "acme", projectionName = "OrderSummary" },
            "This will stop the projection.");

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("projection-pause");
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("OrderSummary");
        doc.RootElement.GetProperty("endpoint").GetString()!.ShouldContain("POST");
        doc.RootElement.GetProperty("parameters").GetProperty("tenantId").GetString().ShouldBe("acme");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SerializePreview_DoesNotLeakProtectedSentinelFromTextOrParameters() {
        string result = ToolHelper.SerializePreview(
            "backup-restore",
            $"Restore backup containing {ProtectedDataLeakSentinel.ProtectedPayloadPlaintext}",
            $"POST /api/v1/admin/backups/{ProtectedDataLeakSentinel.ProtectedConnectionString}/restore",
            new {
                payloadJson = ProtectedDataLeakSentinel.ProtectedPayloadPlaintext,
                safeIdentifier = "backup-1",
            },
            ProtectedDataLeakSentinel.ProtectedProviderExceptionText);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("parameters").GetProperty("safeIdentifier").GetString().ShouldBe("backup-1");
        // P15 — assert each unsafe-text field collapsed to its fallback replacement, not just sentinel-free.
        doc.RootElement.GetProperty("description").GetString().ShouldBe("Preview description redacted.");
        doc.RootElement.GetProperty("endpoint").GetString().ShouldBe("Preview endpoint redacted.");
        doc.RootElement.GetProperty("warning").GetString().ShouldBe("Preview warning redacted.");
        // P15 — assert the raw-capable key was projected to the descriptor name (not just absent).
        doc.RootElement.GetProperty("parameters").GetProperty("payload").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
    }

    [Fact]
    public void SerializeResult_RestrictsMarkerRedactionToRawCapableKeys_PerD2() {
        // D2 — benign free-text values mentioning marker substrings under safe keys must NOT be replaced.
        var data = new {
            safeGuidance = "Inspect connectionString status via /api/v1/admin/diagnostics.",
            payloadJson = ProtectedDataLeakSentinel.ProtectedPayloadPlaintext,
        };

        string result = ToolHelper.SerializeResult(data);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        using var doc = JsonDocument.Parse(result);
        // Safe text under a non-raw-capable key survives untouched.
        doc.RootElement.GetProperty("safeGuidance").GetString().ShouldBe("Inspect connectionString status via /api/v1/admin/diagnostics.");
        // Raw-capable key is projected to a descriptor.
        doc.RootElement.GetProperty("payload").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
    }

    [Fact]
    public void SerializeResult_NestedAndStringifiedJson_AreSentinelFree_PerP22() {
        // P22 — nested objects under safe keys are recursively sanitized; stringified JSON in non-raw-capable
        // keys remains unsanitized (it's just a string), but its raw-capable key counterparts get descriptorised.
        var data = new {
            outer = new {
                inner = new {
                    payloadJson = ProtectedDataLeakSentinel.ProtectedPayloadPlaintext,
                    stateJson = ProtectedDataLeakSentinel.ProtectedSnapshotPlaintext,
                },
            },
        };

        string result = ToolHelper.SerializeResult(data);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("outer").GetProperty("inner").GetProperty("payload").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
        doc.RootElement.GetProperty("outer").GetProperty("inner").GetProperty("state").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
    }

    [Fact]
    public void SerializeResult_AllRawCapableFieldKinds_AreProjectedToDescriptors_PerP21() {
        // P21 — every key listed in IsRawCapableProperty gets descriptorised, regardless of category.
        var data = new {
            commandPayload = ProtectedDataLeakSentinel.ProtectedPayloadPlaintext,
            providerMetadata = ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob,
            providerPrivateMetadata = ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob,
            stateStoreKey = ProtectedDataLeakSentinel.ProtectedStateStoreKey,
            connectionString = ProtectedDataLeakSentinel.ProtectedConnectionString,
            keyAlias = ProtectedDataLeakSentinel.ProtectedKeyAlias,
            rawResponseBody = "raw body containing PROTECTED_marker",
            stackTrace = "at Provider.Throws() — PROTECTED_marker",
            exceptionText = ProtectedDataLeakSentinel.ProtectedProviderExceptionText,
        };

        string result = ToolHelper.SerializeResult(data);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("payload").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
        doc.RootElement.GetProperty("metadata").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
        doc.RootElement.GetProperty("stateStoreKeyStatus").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
        doc.RootElement.GetProperty("connectionStatus").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
        doc.RootElement.GetProperty("keyStatus").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
        doc.RootElement.GetProperty("responseStatus").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
        doc.RootElement.GetProperty("diagnostic").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
    }

    // P13 — Defense-in-depth recursion bound (MaxSanitizeDepth = 64) is coded inside both
    // ToolHelper.SanitizeNode and JsonOutputFormatter.SanitizeNode. End-to-end verification through
    // SerializeResult is not feasible because System.Text.Json's own SerializeToNode/MaxDepth (64)
    // throws on deeper input before our sanitizer is reached. The bound is therefore covered by
    // code review and remains as belt-and-braces protection if upstream limits are raised.

    [Fact]
    public void HandleHttpException_409_ReturnsConflict() {
        var ex = new HttpRequestException("Projection already paused", null, HttpStatusCode.Conflict);

        string result = ToolHelper.HandleHttpException(ex);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("conflict");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("Operation conflict");
    }

    [Fact]
    public void HandleHttpException_422_ReturnsInvalidOperation() {
        var ex = new HttpRequestException("Cannot pause already-paused projection", null, HttpStatusCode.UnprocessableEntity);

        string result = ToolHelper.HandleHttpException(ex);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-operation");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("Operation rejected");
    }

    [Fact]
    public void HandleHttpException_503_ReturnsTenantServiceUnavailableMessage() {
        var ex = new HttpRequestException("Service unavailable", null, HttpStatusCode.ServiceUnavailable);

        string result = ToolHelper.HandleHttpException(ex);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("service-unavailable");
        doc.RootElement.GetProperty("message").GetString().ShouldBe("Tenant service temporarily unavailable. Retry shortly.");
    }
}
