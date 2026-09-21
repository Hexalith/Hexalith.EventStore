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

    [Fact]
    public void ConfirmationFacts_ClampsEachDisplayedFactToSupportSafeBound()
    {
        const int supportSafeBound = 240;
        string oversized = new string('a', supportSafeBound + 40);

        IRenderedComponent<ConfirmationFacts> component = Render<ConfirmationFacts>(parameters => parameters
            .Add(item => item.Target, oversized)
            .Add(item => item.Impact, oversized)
            .Add(item => item.RequiredPermission, oversized));

        foreach (string fact in new[] { "target", "impact", "permission" })
        {
            string text = component.Find($"[data-confirmation-fact='{fact}']").TextContent;
            text.Length.ShouldBe(supportSafeBound);
            text.ShouldEndWith("...");
            text.ShouldNotBe(oversized);
        }
    }

    [Fact]
    public void ConfirmationFacts_RedactsCredentialShapedValues()
    {
        IRenderedComponent<ConfirmationFacts> component = Render<ConfirmationFacts>(parameters => parameters
            .Add(item => item.Target, "Bearer secret-token")
            .Add(item => item.Impact, "https://user:password@example.test/resource")
            .Add(item => item.RequiredPermission, "client_secret=private-value"));

        foreach (string fact in new[] { "target", "impact", "permission" })
        {
            component.Find($"[data-confirmation-fact='{fact}']").TextContent.ShouldBe("[redacted]");
        }

        component.Markup.ShouldNotContain("secret-token");
        component.Markup.ShouldNotContain("password");
        component.Markup.ShouldNotContain("private-value");
    }

    [Theory]
    [InlineData("tenant\u0007")]
    [InlineData("tenant\u202Ehidden")]
    [InlineData("tenant\U000E0001hidden")]
    public void ConfirmationFacts_RedactsControlAndUnicodeFormatCharactersAndRejectsExactConfirmation(string unsafeText)
    {
        IRenderedComponent<ConfirmationFacts> component = Render<ConfirmationFacts>(parameters => parameters
            .Add(item => item.Target, unsafeText)
            .Add(item => item.Impact, "Safe impact")
            .Add(item => item.RequiredPermission, "Admin"));

        component.Find("[data-confirmation-fact='target']").TextContent.ShouldBe("[redacted]");
        ConfirmationFacts.IsExactAndSupportSafe(unsafeText).ShouldBeFalse();
    }
}
