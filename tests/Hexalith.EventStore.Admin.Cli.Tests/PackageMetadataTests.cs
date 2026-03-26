namespace Hexalith.EventStore.Admin.Cli.Tests;

using System.Reflection;

public class PackageMetadataTests
{
    private static readonly Assembly _cliAssembly = typeof(ExitCodes).Assembly;

    [Fact]
    public void Assembly_HasInformationalVersion()
    {
        string? version = _cliAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Assembly_NameIsCorrect()
    {
        _cliAssembly.GetName().Name.ShouldBe("Hexalith.EventStore.Admin.Cli");
    }

    [Fact]
    public void Assembly_HasEntryPoint()
    {
        // Verify this is an executable assembly (has top-level statements or Main)
        _cliAssembly.EntryPoint.ShouldNotBeNull();
    }
}
