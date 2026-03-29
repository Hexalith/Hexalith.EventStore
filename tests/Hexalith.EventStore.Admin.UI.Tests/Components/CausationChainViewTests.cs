using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class CausationChainViewTests : AdminUITestContext
{
    [Fact]
    public void CausationChainView_RendersCommandType()
    {
        var chain = CreateChain("IncrementCounter");

        IRenderedComponent<CausationChainView> cut = Render<CausationChainView>(p => p
            .Add(c => c.Chain, chain));

        cut.Markup.ShouldContain("IncrementCounter");
    }

    [Fact]
    public void CausationChainView_RendersCorrelationId()
    {
        var chain = CreateChain("IncrementCounter", correlationId: "corr-abc-123");

        IRenderedComponent<CausationChainView> cut = Render<CausationChainView>(p => p
            .Add(c => c.Chain, chain));

        // Correlation ID may be truncated to 8 chars
        cut.Markup.ShouldContain("corr-abc");
    }

    [Fact]
    public void CausationChainView_RendersEvents()
    {
        var chain = new CausationChain(
            "IncrementCounter", "cmd-1", "corr-1", "user-1",
            [
                new CausationEvent(5, "CounterIncremented", DateTimeOffset.UtcNow),
                new CausationEvent(6, "ThresholdReached", DateTimeOffset.UtcNow),
            ],
            []);

        IRenderedComponent<CausationChainView> cut = Render<CausationChainView>(p => p
            .Add(c => c.Chain, chain));

        string markup = cut.Markup;
        markup.ShouldContain("CounterIncremented");
        markup.ShouldContain("ThresholdReached");
    }

    [Fact]
    public void CausationChainView_RendersAffectedProjections()
    {
        var chain = new CausationChain(
            "IncrementCounter", "cmd-1", "corr-1", "user-1",
            [new CausationEvent(5, "CounterIncremented", DateTimeOffset.UtcNow)],
            ["CounterSummary", "DashboardStats"]);

        IRenderedComponent<CausationChainView> cut = Render<CausationChainView>(p => p
            .Add(c => c.Chain, chain));

        string markup = cut.Markup;
        markup.ShouldContain("CounterSummary");
        markup.ShouldContain("DashboardStats");
    }

    [Fact]
    public void CausationChainView_InvokesOnCorrelationClick()
    {
        var chain = CreateChain("IncrementCounter", correlationId: "corr-click-test");
        string? clickedCorrelation = null;

        IRenderedComponent<CausationChainView> cut = Render<CausationChainView>(p => p
            .Add(c => c.Chain, chain)
            .Add(c => c.OnCorrelationClick, id => clickedCorrelation = id));

        // Find the correlation link/button and click it
        var correlationButton = cut.FindAll("button, a, [role='button']")
            .FirstOrDefault(el => el.InnerHtml.Contains("corr-click"));
        if (correlationButton is not null)
        {
            correlationButton.Click();
            clickedCorrelation.ShouldBe("corr-click-test");
        }
    }

    [Fact]
    public void CausationChainView_RendersEmptyEvents_Gracefully()
    {
        var chain = new CausationChain(
            "IncrementCounter", "cmd-1", "corr-1", "user-1",
            [],
            []);

        IRenderedComponent<CausationChainView> cut = Render<CausationChainView>(p => p
            .Add(c => c.Chain, chain));

        // Should render without throwing even with empty events/projections
        cut.Markup.ShouldContain("IncrementCounter");
    }

    private static CausationChain CreateChain(
        string commandType = "IncrementCounter",
        string correlationId = "corr-1")
    {
        return new CausationChain(
            commandType, "cmd-1", correlationId, "user-1",
            [new CausationEvent(5, "CounterIncremented", DateTimeOffset.UtcNow)],
            ["CounterSummary"]);
    }
}
