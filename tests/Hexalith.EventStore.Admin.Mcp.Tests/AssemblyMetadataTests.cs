namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Reflection;

public class AssemblyMetadataTests
{
    private static readonly Assembly _mcpAssembly = typeof(AdminApiClient).Assembly;

    [Fact]
    public void AssemblyName_IsCorrect()
    {
        _mcpAssembly.GetName().Name.ShouldBe("Hexalith.EventStore.Admin.Mcp");
    }

    [Fact]
    public void Assembly_HasEntryPoint()
    {
        // An Exe project has an entry point
        _mcpAssembly.EntryPoint.ShouldNotBeNull();
    }

    [Fact]
    public void Assembly_HasInformationalVersionAttribute()
    {
        AssemblyInformationalVersionAttribute? attr = _mcpAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        attr.ShouldNotBeNull();
        attr.InformationalVersion.ShouldNotBeNullOrWhiteSpace();
    }
}
