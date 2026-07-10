using Hexalith.EventStore.Admin.UI.Services;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public sealed class DaprRoutingHeaderOwnershipTests {
    [Fact]
    public void AdminUiAssembly_DeclaresNoLocalDaprRoutingHandler() {
        Type[] localDaprHandlers = typeof(AdminApiAuthorizationHandler).Assembly
            .GetTypes()
            .Where(static type => typeof(DelegatingHandler).IsAssignableFrom(type)
                && type.Name.Contains("Dapr", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        localDaprHandlers.ShouldBeEmpty(
            "AD-18 requires Admin.UI to register the platform DAPR routing handler instead of declaring a host-local DelegatingHandler.");
    }
}
