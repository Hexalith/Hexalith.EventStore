using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using NSubstitute;

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

    [Theory]
    [InlineData("js")]
    [InlineData("disconnected")]
    [InlineData("timeout")]
    public async Task RestoreAsync_WhenInteropFails_DoesNotFaultTheCaller(string failure)
    {
        Exception exception = failure switch
        {
            "js" => new JSException("focus failed"),
            "disconnected" => new JSDisconnectedException("circuit gone"),
            _ => new TaskCanceledException("interop timed out"),
        };
        IJSRuntime jsRuntime = Substitute.For<IJSRuntime>();
        _ = jsRuntime.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "hexalithAdmin.waitForRender", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromException<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(exception));
        InitiatorFocusService service = new(jsRuntime);

        Exception? fault = await Record.ExceptionAsync(async () => await service.RestoreAsync("initiating-control"));

        fault.ShouldBeNull();
    }

    [Fact]
    public void WaitForRender_DoesNotDependOnlyOnAnimationFrames()
    {
        string interop = File.ReadAllText(Path.Combine(
            Dw5TestPaths.RepoRoot(),
            "src",
            "Hexalith.EventStore.Admin.UI",
            "wwwroot",
            "js",
            "interop.js"));
        int start = interop.IndexOf("waitForRender:", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        int end = interop.IndexOf("focusElementById:", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start);

        string body = interop[start..end];
        body.ShouldContain("requestAnimationFrame(resolve)");
        body.ShouldContain("setTimeout(resolve,");
    }
}
