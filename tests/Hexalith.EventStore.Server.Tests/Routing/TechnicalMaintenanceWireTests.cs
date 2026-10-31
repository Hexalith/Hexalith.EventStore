using System.Text.Json;

using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Proves the typed success payloads preserve the actual MVC Web serializer's existing anonymous wire bytes.</summary>
public sealed class TechnicalMaintenanceWireTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceProofBootstrapPreservesExistingWireBytes(bool incarnation)
    {
        string? value = incarnation ? "owned-source-incarnation" : null;
        var original = new { Status = SourceProofInspectionStatus.Available, CurrentSequence = 7L, Erased = false, SourceIncarnation = value };
        var typed = new ProjectionSourceProofBootstrapResponse(original.Status, original.CurrentSequence, original.Erased, original.SourceIncarnation);
        JsonSerializer.SerializeToUtf8Bytes(typed).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original));
        JsonSerializer.SerializeToUtf8Bytes(typed, JsonSerializerOptions.Web).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original, JsonSerializerOptions.Web));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceGuardMaintenancePreservesExistingWireBytes(bool activated)
    {
        var original = new { Qualified = true, Activated = activated, ReasonCode = "qualified", PreservedSequence = 7L };
        var typed = new ProjectionSourceGuardQualificationResponse(original.Qualified, original.Activated, original.ReasonCode, original.PreservedSequence);
        JsonSerializer.SerializeToUtf8Bytes(typed).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original));
        JsonSerializer.SerializeToUtf8Bytes(typed, JsonSerializerOptions.Web).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original, JsonSerializerOptions.Web));
    }

    [Fact]
    public void WriterProtocolActivationPreservesExistingWireBytes()
    {
        var original = new { status = "Activated" };
        JsonSerializer.SerializeToUtf8Bytes(new ProjectionDeliveryWriterProtocolResponse(original.status)).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original));
        JsonSerializer.SerializeToUtf8Bytes(new ProjectionDeliveryWriterProtocolResponse(original.status), JsonSerializerOptions.Web)
            .ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original, JsonSerializerOptions.Web));
    }

    [Fact]
    public void DeliveryReconciliationPreservesExistingWireBytes()
    {
        var original = new { status = "Completed", ReasonCode = "completed", PreservedSequence = 7L };
        JsonSerializer.SerializeToUtf8Bytes(new ProjectionDeliveryReconciliationResponse(original.status, original.ReasonCode, original.PreservedSequence)).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original));
        JsonSerializer.SerializeToUtf8Bytes(new ProjectionDeliveryReconciliationResponse(original.status, original.ReasonCode, original.PreservedSequence), JsonSerializerOptions.Web)
            .ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original, JsonSerializerOptions.Web));
    }
}
