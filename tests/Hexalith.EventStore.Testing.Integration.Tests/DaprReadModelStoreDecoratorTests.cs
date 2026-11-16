using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Integration;

using Shouldly;

namespace Hexalith.EventStore.Testing.Integration.Tests;

public class DaprReadModelStoreDecoratorTests {
    [Fact]
    public void DecoratorImplementsEveryProductionAdapterCapability() {
        // Callers probe optional capabilities with `is IReadModel*` checks, so a capability the adapter gains
        // but the decorator misses would silently disable that behavior behind a fixture's injected failure.
        Type[] adapterCapabilities = typeof(DaprReadModelStore).GetInterfaces();
        Type[] decoratorCapabilities = typeof(DaprReadModelStoreDecorator).GetInterfaces();

        adapterCapabilities.ShouldContain(typeof(IReadModelStore));
        adapterCapabilities.Except(decoratorCapabilities).Select(type => type.FullName).ShouldBeEmpty(
            "DaprReadModelStoreDecorator must forward every interface DaprReadModelStore implements");
    }
}
