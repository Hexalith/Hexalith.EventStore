using Hexalith.EventStore.Contracts.Aggregates;

namespace Hexalith.EventStore.Contracts.Tests.Aggregates;

public class ITerminatableTests {
    [Fact]
    public void ITerminatable_HasIsTerminatedProperty() {
        System.Reflection.PropertyInfo? property = typeof(ITerminatable).GetProperty(nameof(ITerminatable.IsTerminated));

        Assert.NotNull(property);
        Assert.Equal(typeof(bool), property.PropertyType);
        Assert.True(property.CanRead);
    }

    [Fact]
    public void ITerminatable_IsInterface() => Assert.True(typeof(ITerminatable).IsInterface);
}
