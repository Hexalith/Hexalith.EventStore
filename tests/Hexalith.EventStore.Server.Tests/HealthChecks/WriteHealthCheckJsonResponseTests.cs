
using System.Text.Json;

using Hexalith.EventStore.ServiceDefaults;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public class WriteHealthCheckJsonResponseTests {
    [Fact]
    public async Task WriteHealthCheckJsonResponse_WritesCorrectJsonStructure() {
        // Arrange
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        var entries = new Dictionary<string, HealthReportEntry> {
            ["dapr-sidecar"] = new(
                HealthStatus.Healthy,
                "Dapr sidecar is responsive.",
                TimeSpan.FromMilliseconds(42),
                exception: null,
                data: new Dictionary<string, object>()),
            ["dapr-statestore"] = new(
                HealthStatus.Unhealthy,
                "Dapr state store 'statestore' is not accessible: DaprException",
                TimeSpan.FromMilliseconds(150),
                exception: null,
                data: new Dictionary<string, object>()),
        };

        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(200));

        // Act
        await Extensions.WriteHealthCheckJsonResponse(httpContext, report);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        string json = await reader.ReadToEndAsync();

        httpContext.Response.ContentType.ShouldBe("application/json; charset=utf-8");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("status").GetString().ShouldBe("Unhealthy");
        root.GetProperty("results").GetProperty("dapr-sidecar").GetProperty("status").GetString().ShouldBe("Healthy");
        root.GetProperty("results").GetProperty("dapr-sidecar").GetProperty("description").GetString().ShouldBe("Dapr sidecar is responsive.");
        root.GetProperty("results").GetProperty("dapr-sidecar").GetProperty("duration").GetString().ShouldNotBeNullOrEmpty();

        root.GetProperty("results").GetProperty("dapr-statestore").GetProperty("status").GetString().ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task WriteHealthCheckJsonResponse_NonSerializableData_DoesNotCrash() {
        // Arrange
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        // Create a non-serializable object (circular reference)
        var circularData = new Dictionary<string, object>();
        var selfRef = new SelfReferencing();
        selfRef.Self = selfRef;
        circularData["bad"] = selfRef;

        var entries = new Dictionary<string, HealthReportEntry> {
            ["test-check"] = new(
                HealthStatus.Healthy,
                "Test check healthy.",
                TimeSpan.FromMilliseconds(10),
                exception: null,
                data: circularData),
        };

        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(10));

        // Act -- should NOT throw
        await Extensions.WriteHealthCheckJsonResponse(httpContext, report);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        string json = await reader.ReadToEndAsync();

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        string? badValue = root.GetProperty("results").GetProperty("test-check").GetProperty("data").GetProperty("bad")
            .GetString();
        badValue.ShouldNotBeNull();
        badValue.ShouldContain("non-serializable");
    }

    [Fact]
    public async Task WriteHealthCheckJsonResponse_ThrowsInvalidOperation_DoesNotCrash() {
        // Arrange
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        var data = new Dictionary<string, object> {
            ["bad"] = new ThrowsInvalidOperation(),
        };

        var entries = new Dictionary<string, HealthReportEntry> {
            ["test-check"] = new(
                HealthStatus.Healthy,
                "Test check healthy.",
                TimeSpan.FromMilliseconds(10),
                exception: null,
                data: data),
        };

        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(10));

        // Act -- should NOT throw
        await Extensions.WriteHealthCheckJsonResponse(httpContext, report);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        string json = await reader.ReadToEndAsync();

        using JsonDocument doc = JsonDocument.Parse(json);
        string? badValue = doc.RootElement.GetProperty("results").GetProperty("test-check").GetProperty("data").GetProperty("bad")
            .GetString();
        badValue.ShouldNotBeNull();
        badValue.ShouldContain("non-serializable");
    }

    private sealed class SelfReferencing {
        public SelfReferencing? Self { get; set; }
    }

    private sealed class ThrowsInvalidOperation {
        private readonly int _instanceMarker = 1;

        public string Value => _instanceMarker > 0
            ? throw new InvalidOperationException("boom")
            : string.Empty;
    }
}
