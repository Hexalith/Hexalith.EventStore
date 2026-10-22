using Hexalith.EventStore.Contracts.Results;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class BoundedV1WireResultAdmissionCaptureTests
{
    [Fact]
    public void OptionalStateReservationUsesExactRemainingResultCapacity()
    {
        // The four events share one source array so this exercises the 128 MiB
        // response calculation without retaining four large payload copies.
        byte[] payload = new byte[23 * 1024 * 1024];
        var eventItem = new DomainServiceWireEvent("alias", payload, "json");
        DomainServiceWireResult admitted = BoundedV1WireResultAdmission.Admit(
            new DomainServiceWireResult(false, [eventItem, eventItem, eventItem, eventItem]),
            CancellationToken.None);

        // 128 MiB minus the V1 framing, four 23 MiB Base64 scalars, and the
        // optional member name leaves exactly 5,592,051 escaped scalar bytes.
        BoundedV1WireResultAdmission.CanReserveOptionalState(admitted, 5_592_051, CancellationToken.None)
            .ShouldBeTrue();
        BoundedV1WireResultAdmission.CanReserveOptionalState(admitted, 5_592_052, CancellationToken.None)
            .ShouldBeFalse();
    }
}
