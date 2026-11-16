using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Validates complete v2 infrastructure scope without evaluating authority or qualification.</summary>
public static partial class ProductionProfileV2Validator
{
    /// <summary>Returns safe structural issues and both digests only for a complete valid candidate.</summary>
    /// <param name="profile">The selected v2 candidate.</param>
    /// <param name="context">The independently complete application and instance inventory.</param>
    /// <returns>The structural outcome, with authorization and qualification unevaluated.</returns>
    public static ProductionProfileValidationResult Validate(ProductionProfileV2? profile, ProductionProfileValidationContext? context)
    {
        IReadOnlyList<ProductionProfileIssue> issues = Inspect(profile, context, compareInfrastructureDigest: true);
        if (issues.Count != 0 || profile is null) return new(false, null, null, issues);
        try
        {
            string resolved = ProductionProfileV2Codec.ComputeResolvedDigest(profile, context!);
            return new(true, ProductionProfileV2Codec.InfrastructureDigest(profile), resolved, []);
        }
        catch (ProductionProfileValidationException exception)
        {
            return new(false, null, null, exception.Issues);
        }
    }

    /// <summary>Runs common mandatory bindings and maps their paths into the v2 schema.</summary>
    internal static IReadOnlyList<ProductionProfileIssue> Inspect(ProductionProfileV2? profile, ProductionProfileValidationContext? context,
        bool compareInfrastructureDigest) => ProductionProfileValidator.InspectDocument(profile, context, compareInfrastructureDigest, version: 2)
            .Select(issue => issue with { Path = MapPath(issue.Path) }).Distinct()
            .OrderBy(issue => issue.Path, StringComparer.Ordinal).ThenBy(issue => issue.Code, StringComparer.Ordinal).ToArray();

    /// <summary>Checks closed kinds, genuine scope, separately pinned software and explicit bundle bindings.</summary>
    internal static IReadOnlyList<ProductionProfileIssue> InspectInstances(ProductionProfileV2 profile)
    {
        var issues = new List<ProductionProfileIssue>();
        void Require(bool condition, string path, string code) { if (!condition) issues.Add(new(path, code)); }
        var components = profile.Components.GroupBy(c => c.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var providers = profile.Infrastructure.Providers.GroupBy(p => p.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var scopes = new HashSet<(string, string, string, string?, string?, string?, string)>();
        for (int i = 0; i < profile.Components.Count; i++)
        {
            ProductionProfileV2Component component = profile.Components[i];
            ProductionProfileV2Scope scope = component.Scope;
            string path = $"$.components[{i}]";
            bool applicable = component.Kind switch
            {
                "dapr-component" => scope.Kind == "namespace",
                "container-runtime" or "node-os" => scope.Kind == "node",
                "crd" or "kubernetes" => scope.Kind == "cluster",
                "rancher" or "operator" => scope.Kind is "cluster" or "namespace",
                "service" => scope.Kind is "environment" or "cluster" or "namespace",
                "distribution" => scope.Kind is "environment" or "cluster" or "node",
                _ => false,
            };
            Require(applicable, path + ".kind", "kind-scope-mismatch");
            Require((component.Kind == "dapr-component") == (component.Dapr is not null), path + ".dapr", "dapr-applicability-mismatch");
            Require(scope.EnvironmentId == profile.Environment.Id, path + ".scope.environmentId", "environment-binding-mismatch");
            bool validScope = scope.Kind switch
            {
                "environment" => scope.ClusterId is null && scope.NodeId is null && scope.Namespace is null,
                "cluster" => scope.ClusterId is not null && scope.NodeId is null && scope.Namespace is null,
                "node" => scope.ClusterId is not null && scope.NodeId is not null && scope.Namespace is null,
                "namespace" => scope.ClusterId is not null && scope.NodeId is null && scope.Namespace is not null,
                _ => false,
            };
            Require(validScope, path + ".scope", "invalid-scope-identifiers");
            Require(scope.Namespace is null || NamespaceName().IsMatch(scope.Namespace), path + ".scope.namespace", "invalid-namespace");
            Require(scopes.Add((component.Kind, scope.Kind, scope.EnvironmentId, scope.ClusterId, scope.NodeId, scope.Namespace, component.Name)),
                path, "duplicate-scoped-identity");
            Require(providers.TryGetValue(component.ProviderId, out ProductionProfileProvider? provider) && provider.Version == component.SoftwareVersion,
                path + ".softwareVersion", "software-provider-binding-mismatch");
            if (component.BundleParentId is not null)
            {
                Require(component.BundleParentId != component.Id, path + ".bundleParentId", "self-bundle-parent");
                Require(components.TryGetValue(component.BundleParentId, out ProductionProfileV2Component? parent), path + ".bundleParentId", "dangling-reference");
                if (parent is not null)
                {
                    Require(parent.Kind == "distribution", path + ".bundleParentId", "bundle-parent-kind-mismatch");
                    Require(Contains(parent.Scope, scope), path + ".bundleParentId", "bundle-scope-mismatch");
                }
                var visited = new HashSet<string>(StringComparer.Ordinal) { component.Id };
                string? current = component.BundleParentId;
                while (current is not null && components.TryGetValue(current, out ProductionProfileV2Component? ancestor))
                {
                    if (!visited.Add(current))
                    {
                        Require(false, path + ".bundleParentId", "cyclic-bundle-parent");
                        break;
                    }
                    current = ancestor.BundleParentId;
                }
            }
        }
        for (int i = 0; i < profile.Applications.Count; i++)
            Require(NamespaceName().IsMatch(profile.Applications[i].Namespace), $"$.applications[{i}].namespace", "invalid-namespace");
        ProductionProfileTopology topology = profile.Infrastructure.Topology;
        TopologyInstance("kubernetes", topology.ManagementClusterId, topology.ManagementProviderId,
            "$.infrastructure.topology.managementProviderId");
        TopologyInstance("kubernetes", topology.WorkloadClusterId, topology.WorkloadProviderId,
            "$.infrastructure.topology.workloadProviderId");
        TopologyInstance("rancher", topology.ManagementClusterId, topology.RancherProviderId,
            "$.infrastructure.topology.rancherProviderId");
        return issues;

        void TopologyInstance(string kind, string clusterId, string providerId, string path)
        {
            ProductionProfileV2Component[] actual = profile.Components.Where(c => c.Kind == kind && c.Scope.ClusterId == clusterId).ToArray();
            Require(actual.Length == 1 && actual[0].ProviderId == providerId, path, "topology-instance-binding-mismatch");
        }
    }

    private static bool Contains(ProductionProfileV2Scope parent, ProductionProfileV2Scope child) =>
        parent.EnvironmentId == child.EnvironmentId && (parent.Kind switch
        {
            "environment" => true,
            "cluster" => parent.ClusterId == child.ClusterId,
            "node" => child.Kind == "node" && parent.ClusterId == child.ClusterId && parent.NodeId == child.NodeId,
            _ => false,
        });

    private static string MapPath(string path) => ComponentField().Replace(path, match => match.Groups[1].Value + (match.Groups[2].Value switch
    {
        "clusterId" => ".scope.clusterId",
        "namespace" => ".scope.namespace",
        "type" => ".dapr.type",
        "version" => ".dapr.apiVersion",
        var field => ".dapr." + field,
    }));

    [GeneratedRegex(@"\A[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamespaceName();

    [GeneratedRegex(@"\A(\$\.components\[[0-9]+\])\.(clusterId|namespace|type|version|actorStateStore|appScopeIds|aclDigest|resiliencyDigest|dataMigrationDigest)(?=\.|\[|\z)", RegexOptions.CultureInvariant)]
    private static partial Regex ComponentField();
}
