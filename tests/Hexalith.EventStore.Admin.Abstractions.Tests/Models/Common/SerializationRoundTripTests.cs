using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Common;

public class SerializationRoundTripTests
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void StreamSummary_RoundTrips()
    {
        var original = new StreamSummary("acme", "orders", "order-123", 10, DateTimeOffset.UtcNow, 10, true, StreamStatus.Active);
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<StreamSummary>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(original);
    }

    [Fact]
    public void AdminOperationResult_RoundTrips()
    {
        var original = new AdminOperationResult(true, "op-001", "Done", null);
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<AdminOperationResult>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(original);
    }

    [Fact]
    public void ProjectionStatus_RoundTrips()
    {
        var original = new ProjectionStatus("OrderSummary", "acme", ProjectionStatusType.Running, 5, 100.5, 0, 1000, DateTimeOffset.UtcNow);
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<ProjectionStatus>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(original);
    }

    [Fact]
    public void EventTypeInfo_RoundTrips()
    {
        var original = new EventTypeInfo("OrderCreated", "orders", false, 1);
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<EventTypeInfo>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(original);
    }

    [Fact]
    public void DaprComponentHealth_RoundTrips()
    {
        var original = new DaprComponentHealth("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow);
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<DaprComponentHealth>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(original);
    }

    [Fact]
    public void TenantSummary_RoundTrips()
    {
        var original = new TenantSummary("acme", "Acme Corp", TenantStatusType.Active);
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<TenantSummary>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(original);
    }

    [Fact]
    public void DeadLetterEntry_RoundTrips()
    {
        var original = new DeadLetterEntry("msg-001", "acme", "orders", "order-123", "corr-001", "Timeout", DateTimeOffset.UtcNow, 3, "CreateOrder");
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<DeadLetterEntry>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(original);
    }

    [Fact]
    public void SnapshotPolicy_RoundTrips()
    {
        var original = new SnapshotPolicy("acme", "orders", "Order", 100, DateTimeOffset.UtcNow);
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<SnapshotPolicy>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(original);
    }

    [Fact]
    public void FieldChange_RoundTrips()
    {
        var original = new FieldChange("$.status", "\"open\"", "\"closed\"");
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<FieldChange>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(original);
    }

    [Fact]
    public void PagedResult_RoundTrips()
    {
        var original = new PagedResult<string>(["a", "b"], 5, "token");
        string json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<PagedResult<string>>(json, _options);

        deserialized.ShouldNotBeNull();
        deserialized.Items.Count.ShouldBe(2);
        deserialized.TotalCount.ShouldBe(5);
        deserialized.ContinuationToken.ShouldBe("token");
    }
}
