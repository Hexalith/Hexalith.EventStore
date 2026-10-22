using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Checks private runtime overrides preserve defaults and refuse malformed profile input before startup.</summary>
public sealed class DaprTestRuntimeConfigurationTests
{
    /// <summary>Checks an absent override creates no configuration and preserves Dapr's default features.</summary>
    [Fact]
    public void AbsentOverrideCreatesNoFile()
        => DaprTestRuntimeConfiguration.Create("directory-that-does-not-exist", null).ShouldBeNull();

    /// <summary>Checks invalid overrides refuse before touching a component directory.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData(" false")]
    public void MalformedOverrideRefusesBeforeFileCreation(string configured)
        => Should.Throw<InvalidOperationException>(() => DaprTestRuntimeConfiguration.Create("directory-that-does-not-exist", configured));

    /// <summary>Checks the selected feature override is written only under the fixture-owned directory.</summary>
    [Fact]
    public void DisabledHotReloadIsExplicitInPrivateConfiguration()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dapr-runtime-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = DaprTestRuntimeConfiguration.Create(directory, "FALSE")!;
            Path.GetDirectoryName(path).ShouldBe(directory);
            File.ReadAllText(path).ShouldContain("name: HotReload\n      enabled: false");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
