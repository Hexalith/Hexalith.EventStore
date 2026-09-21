using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class InitiatorFocusServiceTests : AdminUITestContext
{
    [Fact]
    public async Task RestoreAsync_WaitsForRenderedDomBeforeFocusingInitiator()
    {
        InitiatorFocusService service = Services.GetRequiredService<InitiatorFocusService>();

        await service.RestoreAsync("initiating-control");

        string[] identifiers = JSInterop.Invocations
            .Select(invocation => invocation.Identifier)
            .ToArray();
        identifiers.ShouldBe(
        [
            "hexalithAdmin.waitForRender",
            "hexalithAdmin.focusElementById",
        ]);
        JSInterop.Invocations.Last().Arguments[0].ShouldBe("initiating-control");
    }
}
