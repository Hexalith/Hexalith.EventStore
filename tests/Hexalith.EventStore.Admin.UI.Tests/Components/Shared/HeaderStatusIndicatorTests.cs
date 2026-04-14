using Bunit;

using Hexalith.EventStore.Admin.UI.Components.Shared;

namespace Hexalith.EventStore.Admin.UI.Tests.Components.Shared;

public class HeaderStatusIndicatorTests : AdminUITestContext
{
    [Fact]
    public void HeaderStatusIndicator_RendersGhostAppearanceWhenDisconnected()
    {
        IRenderedComponent<HeaderStatusIndicator> cut = Render<HeaderStatusIndicator>(
            parameters => parameters
                .Add(p => p.ConnectionStatus, HeaderStatusIndicator.ConnectionStatusType.Disconnected));

        string markup = cut.Markup;
        markup.ShouldContain("appearance=\"ghost\"");
        markup.ShouldContain("color=\"danger\"");
    }

    [Fact]
    public void HeaderStatusIndicator_RendersFilledAppearanceWhenConnected()
    {
        IRenderedComponent<HeaderStatusIndicator> cut = Render<HeaderStatusIndicator>(
            parameters => parameters
                .Add(p => p.ConnectionStatus, HeaderStatusIndicator.ConnectionStatusType.Connected));

        string markup = cut.Markup;
        markup.ShouldContain("appearance=\"filled\"");
        markup.ShouldContain("color=\"success\"");
    }
}
