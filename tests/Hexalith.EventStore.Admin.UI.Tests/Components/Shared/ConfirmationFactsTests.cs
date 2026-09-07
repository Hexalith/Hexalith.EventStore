using Bunit;

using Hexalith.EventStore.Admin.UI.Components.Shared;

namespace Hexalith.EventStore.Admin.UI.Tests.Components.Shared;

public class ConfirmationFactsTests : AdminUITestContext
{
    [Fact]
    public void ConfirmationFacts_RendersAccessibleResourceBackedContract()
    {
        IRenderedComponent<ConfirmationFacts> component = Render<ConfirmationFacts>(parameters => parameters
            .Add(item => item.Target, "Projection 'orders' in tenant 'tenant-a'")
            .Add(item => item.Impact, "Clear projection state and rebuild it.")
            .Add(item => item.RequiredPermission, "Operator"));

        component.Markup.ShouldContain("role=\"note\"");
        component.Markup.ShouldContain("Confirmation safety facts");
        component.Find("[data-confirmation-fact='target']").TextContent
            .ShouldBe("Projection 'orders' in tenant 'tenant-a'");
        component.Find("[data-confirmation-fact='impact']").TextContent
            .ShouldBe("Clear projection state and rebuild it.");
        component.Find("[data-confirmation-fact='permission']").TextContent.ShouldBe("Operator");
    }
}
