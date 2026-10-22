using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

/// <summary>
/// Guards <c>CounterCommandForm.razor</c> against regressing to hand-built generic command submission
/// (direct <c>/api/v1/commands</c> POST, anonymous JSON, <c>Guid.NewGuid()</c> message ids, stringly-typed
/// PascalCase command discriminators) and against exposing the tombstoning <c>CloseCounter</c> as a casual
/// demo button.
/// </summary>
public sealed class CounterCommandFormGuardTests
{
    [Fact]
    public void CounterCommandForm_DoesNotHandBuildGenericCommandEnvelope()
    {
        string source = ReadCounterCommandForm();

        source.ShouldNotContain("/api/v1/commands");
        source.ShouldNotContain("Guid.NewGuid");
        source.ShouldNotContain("CreateClient(");
        source.ShouldNotContain("\"IncrementCounter\"");
        source.ShouldNotContain("\"DecrementCounter\"");
        source.ShouldNotContain("\"ResetCounter\"");
    }

    [Fact]
    public void CounterCommandForm_SubmitsThroughTypedGatewayClient()
    {
        string source = ReadCounterCommandForm();

        source.ShouldContain("IEventStoreGatewayClient");
        source.ShouldContain("SubmitCommandAsync");
        source.ShouldContain("UniqueIdHelper.GenerateSortableUniqueStringId()");
    }

    [Fact]
    public void CounterCommandForm_DoesNotExposeCloseButton()
    {
        // CloseCounter tombstones the aggregate; it is proven by generated source/tests/smoke,
        // not surfaced as a casual demo action (story guardrail 5).
        string source = ReadCounterCommandForm();

        source.ShouldNotContain("new CloseCounter(");
    }

    [Fact]
    public void BlazorUiSource_DoesNotSubmitGenericCommands()
    {
        string blazorUiRoot = Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "Hexalith.EventStore.Sample.BlazorUI");

        string source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(blazorUiRoot, "*.*", SearchOption.AllDirectories)
                .Where(static file => file.EndsWith(".cs", StringComparison.Ordinal)
                    || file.EndsWith(".razor", StringComparison.Ordinal))
                .Where(static file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Select(File.ReadAllText));

        source.ShouldNotContain("/api/v1/commands");
        source.ShouldNotContain("Guid.NewGuid");
    }

    private static string ReadCounterCommandForm()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "Hexalith.EventStore.Sample.BlazorUI",
            "Components",
            "CounterCommandForm.razor");

        File.Exists(path).ShouldBeTrue();
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.EventStore.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hexalith.EventStore.slnx from the test output path.");
    }
}
