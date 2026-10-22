using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

public sealed class CounterQueryWrapperGuardTests
{
    [Fact]
    public void Source_DoesNotContainHandWrittenCounterQueryWrapper()
    {
        string blazorUiRoot = Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "Hexalith.EventStore.Sample.BlazorUI");

        File.Exists(Path.Combine(blazorUiRoot, "Services", "CounterQueryService.cs")).ShouldBeFalse();

        string source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(blazorUiRoot, "*.*", SearchOption.AllDirectories)
                .Where(static file => file.EndsWith(".cs", StringComparison.Ordinal)
                    || file.EndsWith(".razor", StringComparison.Ordinal))
                .Where(static file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Select(File.ReadAllText));

        source.ShouldNotContain("CounterQueryService");
        source.ShouldNotContain("/api/v1/queries");
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
