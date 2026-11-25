using Hexalith.EventStore.Aspire;

using YamlDotNet.RepresentationModel;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Shared assertions for the external API hosts' Dapr access-control policies.</summary>
internal static class DaprAccessControlAssertions
{
    /// <summary>Reads the AppHost's Dapr access-control configuration.</summary>
    internal static string ReadConfiguration() => File.ReadAllText(RepositoryProjectPaths.GetProjectPath(
        "src", "Hexalith.EventStore.AppHost", "DaprComponents", "accesscontrol.yaml"));

    /// <summary>Asserts a default-deny policy allowing only the two gateway POST operations.</summary>
    /// <param name="configuration">The YAML configuration text.</param>
    /// <param name="appId">The calling Dapr application identifier.</param>
    internal static void AssertGatewayPostOperations(string configuration, string appId)
    {
        using var reader = new StringReader(configuration);
        var yaml = new YamlStream();
        yaml.Load(reader);

        var root = yaml.Documents.ShouldHaveSingleItem().RootNode.ShouldBeOfType<YamlMappingNode>();
        var spec = root["spec"].ShouldBeOfType<YamlMappingNode>();
        var accessControl = spec["accessControl"].ShouldBeOfType<YamlMappingNode>();
        YamlMappingNode policy = accessControl["policies"].ShouldBeOfType<YamlSequenceNode>().Children
            .Select(static node => node.ShouldBeOfType<YamlMappingNode>())
            .Where(candidate => Scalar(candidate, "appId") == appId)
            .ShouldHaveSingleItem($"Expected exactly one Dapr access-control policy for {appId}.");

        Scalar(policy, "defaultAction").ShouldBe("deny");
        Scalar(policy, "trustDomain").ShouldBe("public");
        Scalar(policy, "namespace").ShouldBe("default");
        var operationNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (YamlNode entry in policy["operations"].ShouldBeOfType<YamlSequenceNode>().Children)
        {
            var operation = entry.ShouldBeOfType<YamlMappingNode>();
            string name = Scalar(operation, "name");
            operationNames.Add(name).ShouldBeTrue($"Duplicate Dapr ACL operation '{name}'.");
            Scalar(operation, "action").ShouldBe("allow");
            operation["httpVerb"].ShouldBeOfType<YamlSequenceNode>().Children
                .Select(static verb => verb.ShouldBeOfType<YamlScalarNode>().Value)
                .ShouldBe(["POST"], $"{appId} may invoke only POST operations.");
        }

        operationNames.Order(StringComparer.Ordinal).ShouldBe(["/api/v1/commands", "/api/v1/queries"]);
    }

    private static string Scalar(YamlMappingNode mapping, string key)
        => mapping[key].ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
}
