using Shouldly;

using YamlDotNet.Serialization;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Proves discovery configuration binds only the exact fixture-private registry.</summary>
public sealed class Oq8DiscoveryConfigurationTests
{
    /// <summary>Preserves escaped path bytes in the Dapr configuration.</summary>
    [Fact]
    public void PrivateRegistryConfigurationRoundTripsAbsolutePath()
    {
        string registry = Path.Combine(Path.GetTempPath(), "oq8-owned-\"registry", "discovery.sqlite");
        Dictionary<string, object> document = new DeserializerBuilder().Build()
            .Deserialize<Dictionary<string, object>>(Oq8DiscoveryConfiguration.Create(registry));
        var spec = (Dictionary<object, object>)document["spec"];
        var resolver = (Dictionary<object, object>)spec["nameResolution"];
        resolver["component"].ShouldBe("sqlite");
        resolver["version"].ShouldBe("v1");
        var configuration = (Dictionary<object, object>)resolver["configuration"];
        configuration["connectionString"].ShouldBe(registry);
        document.Keys.ShouldNotContain("scopes");
    }

    /// <summary>Rejects a registry that could resolve against a shared working directory.</summary>
    [Fact]
    public void RelativeDiscoveryRegistryFailsClosed()
        => Should.Throw<ArgumentException>(() => Oq8DiscoveryConfiguration.Create("discovery.sqlite"));

    /// <summary>The private HotReload override preserves the isolated discovery resolver.</summary>
    [Fact]
    public void DisabledHotReloadPreservesPrivateDiscovery()
    {
        string registry = Path.Combine(Path.GetTempPath(), "oq8-owned-registry", "discovery.sqlite");
        Dictionary<string, object> document = new DeserializerBuilder().Build()
            .Deserialize<Dictionary<string, object>>(Oq8DiscoveryConfiguration.Create(registry, "FALSE"));
        var spec = (Dictionary<object, object>)document["spec"];
        var resolver = (Dictionary<object, object>)spec["nameResolution"];
        resolver["component"].ShouldBe("sqlite");
        var configuration = (Dictionary<object, object>)resolver["configuration"];
        configuration["connectionString"].ShouldBe(registry);
        var features = (List<object>)spec["features"];
        var hotReload = (Dictionary<object, object>)features.Single();
        hotReload["name"].ShouldBe("HotReload");
        hotReload["enabled"].ShouldBe("false");
    }

    /// <summary>A malformed override fails before producing any discovery configuration.</summary>
    [Fact]
    public void MalformedHotReloadOverrideFailsClosed()
        => Should.Throw<InvalidOperationException>(() => Oq8DiscoveryConfiguration.Create(
            Path.Combine(Path.GetTempPath(), "discovery.sqlite"), " false"));
}
