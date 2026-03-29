using Hexalith.EventStore.Contracts.Aggregates;

namespace Hexalith.EventStore.Contracts.Tests.Aggregates;

public class ITerminatableTests {
    [Fact]
    public void ITerminatable_HasIsTerminatedProperty() {
        System.Reflection.PropertyInfo? property = typeof(ITerminatable).GetProperty(nameof(ITerminatable.IsTerminated));

        property.ShouldNotBeNull();
        property.PropertyType.ShouldBe(typeof(bool));
        property.CanRead.ShouldBeTrue();
    }

    [Fact]
    public void ITerminatable_IsInterface() => typeof(ITerminatable).IsInterface.ShouldBeTrue();
}
