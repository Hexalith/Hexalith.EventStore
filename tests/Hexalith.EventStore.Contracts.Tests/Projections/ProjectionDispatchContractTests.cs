using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Contracts.Tests.Projections;

public class ProjectionDispatchContractTests {
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void LegacyProjectionRequest_WebJsonShape_RemainsStable() {
        var request = new ProjectionRequest("tenant-a", "orders", "order-1", []);

        string json = JsonSerializer.Serialize(request, WebJson);

        json.ShouldBe("""{"tenantId":"tenant-a","domain":"orders","aggregateId":"order-1","events":[]}""");
        ProjectionRequest restored = JsonSerializer.Deserialize<ProjectionRequest>(json, WebJson).ShouldNotBeNull();
        restored.TenantId.ShouldBe(request.TenantId);
        restored.Domain.ShouldBe(request.Domain);
        restored.AggregateId.ShouldBe(request.AggregateId);
        restored.Events.ShouldBeEmpty();
    }

    [Fact]
    public void LegacyProjectionResponse_WebJsonShape_RemainsStable() {
        using JsonDocument document = JsonDocument.Parse("""{"count":7}""");
        var response = new ProjectionResponse("order-detail", document.RootElement.Clone());

        string json = JsonSerializer.Serialize(response, WebJson);

        json.ShouldBe("""{"projectionType":"order-detail","state":{"count":7}}""");
        ProjectionResponse restored = JsonSerializer.Deserialize<ProjectionResponse>(json, WebJson).ShouldNotBeNull();
        restored.ProjectionType.ShouldBe("order-detail");
        restored.State.GetProperty("count").GetInt32().ShouldBe(7);
    }

    [Fact]
    public void ProjectionDispatchRequest_WebJsonShape_IsVersionedAndAdditive() {
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "orders", "order-1", []),
            ["order-detail", "order-index"],
            "01JZC5P8Z6M8AJ6W0KVRJHW5QX",
            "catalog-fingerprint");

        string json = JsonSerializer.Serialize(request, WebJson);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        root.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["request", "projectionTypes", "dispatchId", "catalogFingerprint"]);
        root.GetProperty("projectionTypes").EnumerateArray().Select(value => value.GetString()).ShouldBe(
            ["order-detail", "order-index"]);
    }

    [Fact]
    public void ProjectionDispatchResponse_WebJsonShape_PreservesVersionAndOutcome() {
        using JsonDocument document = JsonDocument.Parse("""{"count":7}""");
        var response = new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome(
                "order-detail",
                ProjectionDispatchStatus.Completed,
                document.RootElement.Clone(),
                null)]);

        string json = JsonSerializer.Serialize(response, WebJson);
        ProjectionDispatchResponse restored = JsonSerializer.Deserialize<ProjectionDispatchResponse>(json, WebJson)
            .ShouldNotBeNull();

        restored.Version.ShouldBe(2);
        restored.Outcomes.Count.ShouldBe(1);
        restored.Outcomes[0].ProjectionType.ShouldBe("order-detail");
        restored.Outcomes[0].Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonElement state = restored.Outcomes[0].State.ShouldNotBeNull();
        state.GetProperty("count").GetInt32().ShouldBe(7);
        restored.Outcomes[0].ReasonCode.ShouldBeNull();
    }

    [Fact]
    public void ProjectionDispatchStatus_NumericValues_AreStable() {
        ((int)ProjectionDispatchStatus.Completed).ShouldBe(0);
        ((int)ProjectionDispatchStatus.AlreadyCompleted).ShouldBe(1);
        ((int)ProjectionDispatchStatus.Retryable).ShouldBe(2);
        ((int)ProjectionDispatchStatus.Indeterminate).ShouldBe(3);
        ((int)ProjectionDispatchStatus.Failed).ShouldBe(4);
    }

    [Fact]
    public void ProjectionDispatchOutcome_OmittedStatusIsRejectedInsteadOfDefaultingToCompleted() {
        const string json = """{"projectionType":"order-detail","state":null,"reasonCode":null}""";

        _ = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ProjectionDispatchOutcome>(json, WebJson));
    }

    [Fact]
    public void ProjectionDispatchProtocol_ExposesFrozenVersionAndCapability() {
        ProjectionDispatchProtocol.Version.ShouldBe(2);
        ProjectionDispatchProtocol.Capability.ShouldBe("named-projection-dispatch-v2");
    }

    [Fact]
    public void ProjectionDispatchReasonCodes_AreStableUniqueAndBoundedAscii() {
        string[] reasonCodes = [
            ProjectionDispatchReasonCodes.DuplicateRoute,
            ProjectionDispatchReasonCodes.UnsupportedRoute,
            ProjectionDispatchReasonCodes.UnsupportedCapability,
            ProjectionDispatchReasonCodes.MalformedOutcome,
            ProjectionDispatchReasonCodes.HandlerFailure,
            ProjectionDispatchReasonCodes.Cancellation,
            ProjectionDispatchReasonCodes.PartialRetry,
            ProjectionDispatchReasonCodes.DeliveryAlreadyCompleted,
            ProjectionDispatchReasonCodes.DeliveryInProgress,
            ProjectionDispatchReasonCodes.DeliveryGap,
            ProjectionDispatchReasonCodes.DeliveryIdentityConflict,
            ProjectionDispatchReasonCodes.DeliveryReconciliationRequired,
            ProjectionDispatchReasonCodes.DeliverySchemaRegression,
            ProjectionDispatchReasonCodes.DeliveryStateUnavailable,
            ProjectionDispatchReasonCodes.DeliveryLeaseReclaimed,
            ProjectionDispatchReasonCodes.DeliveryReconciled,
            ProjectionDispatchReasonCodes.DeliveryRebuildRequired,
        ];

        reasonCodes.Distinct(StringComparer.Ordinal).Count().ShouldBe(reasonCodes.Length);
        reasonCodes.ShouldAllBe(reasonCode => reasonCode.All(character => character <= 0x7f));
        reasonCodes.ShouldAllBe(reasonCode => Encoding.ASCII.GetByteCount(reasonCode) <= ProjectionDispatchReasonCodes.MaxAsciiBytes);
    }
}
