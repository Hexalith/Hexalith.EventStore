using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Deployment;

namespace Hexalith.EventStore.Contracts.Tests.Deployment;

public class ProductionProfileV2Tests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "Deployment", "Fixtures");
    private static readonly ProductionProfileValidationContext Context = new(
        ["app:eventstore", "app:tenants"],
        ["component:actor-eventstore", "component:actor-tenants", "component:readmodels", "component:broker", "component:openbao",
            "component:scheduler", "component:placement", "component:issuer", "component:backup", "component:distribution",
            "component:containerd", "component:os", "component:crd", "component:environment-service", "component:management",
            "component:workload", "component:rancher"]);

    [Fact]
    public void IndependentVectorsBindTheExactVersionedPreimagesAndEveryScope()
    {
        byte[] bytes = Bytes();
        using JsonDocument digests = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Directory, "SyntheticDigests.v2.json")));
        string infrastructure = digests.RootElement.GetProperty("infrastructure").GetString()!;
        string resolved = digests.RootElement.GetProperty("resolved").GetString()!;
        ProductionProfileV2 profile = ProductionProfileV2Codec.Decode(bytes, Context, resolved, infrastructure);
        ProductionProfileV2Codec.Encode(profile, Context).ShouldBe(bytes);
        ProductionProfileV2Codec.ComputeInfrastructureDigest(profile, Context).ShouldBe(infrastructure);
        ProductionProfileV2Codec.ComputeResolvedDigest(profile, Context).ShouldBe(resolved);
        Convert.ToHexStringLower(SHA256.HashData(bytes)).ShouldBe(resolved);
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Directory, "SyntheticInfrastructurePreimage.v2.json"))))
            .ShouldBe(infrastructure);
        profile.Components.Select(c => c.Scope.Kind).Distinct().Order().ShouldBe(["cluster", "environment", "namespace", "node"]);
        foreach (ProductionProfileV2Component component in profile.Components.Where(c => c.Kind is "container-runtime" or "node-os" or "crd"))
        {
            component.Scope.Namespace.ShouldBeNull();
            component.Dapr.ShouldBeNull();
            component.SoftwareVersion.ShouldNotBe("0.0.9-synthetic");
            component.BundleParentId.ShouldBe("component:distribution");
        }
        ProductionProfileV2Component actor = profile.Components.Single(c => c.Id == "component:actor-eventstore");
        actor.SoftwareVersion.ShouldBe("17.0.0");
        actor.Dapr!.ApiVersion.ShouldBe("v1");
        ProductionProfileValidationResult result = ProductionProfileV2Validator.Validate(profile, Context);
        result.IsStructurallyValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
        result.Authorization.ShouldBe("unevaluated");
        result.Qualification.ShouldBe("unevaluated");
    }

    [Theory]
    [InlineData("namespace", "scope", "invalid-scope-identifiers")]
    [InlineData("node", "scope", "invalid-scope-identifiers")]
    [InlineData("cluster", "scope", "invalid-scope-identifiers")]
    [InlineData("environment", "scope.environmentId", "environment-binding-mismatch")]
    [InlineData("kind", "kind", "kind-scope-mismatch")]
    [InlineData("unknown-scope", "scope", "invalid-scope-identifiers")]
    [InlineData("dapr", "dapr", "dapr-applicability-mismatch")]
    [InlineData("software", "softwareVersion", "software-provider-binding-mismatch")]
    [InlineData("cluster-reference", "scope.clusterId", "dangling-cluster-reference")]
    [InlineData("bundle-dangling", "bundleParentId", "dangling-reference")]
    [InlineData("bundle-self", "bundleParentId", "self-bundle-parent")]
    [InlineData("bundle-cycle", "bundleParentId", "cyclic-bundle-parent")]
    [InlineData("bundle-scope", "bundleParentId", "bundle-scope-mismatch")]
    [InlineData("bundle-kind", "bundleParentId", "bundle-parent-kind-mismatch")]
    [InlineData("stateful", "id", "missing-restore-component")]
    [InlineData("sources", "supportSources", "missing-currency-inputs")]
    [InlineData("owner", "currencyOwner", "missing-selection")]
    [InlineData("evidence", "evidenceIds", "evidence-subject-mismatch")]
    public void InvalidInfrastructureFactsRefuseAtSafeSchemaPaths(string change, string field, string code)
    {
        JsonNode node = Node();
        JsonNode instance = Component(node, "component:containerd");
        switch (change)
        {
            case "namespace": instance["scope"]!["namespace"] = "fabricated"; break;
            case "node": instance["scope"]!["nodeId"] = null; break;
            case "cluster": instance["scope"]!["clusterId"] = null; break;
            case "environment": instance["scope"]!["environmentId"] = "environment:other"; break;
            case "kind": instance["kind"] = "unknown-kind"; break;
            case "unknown-scope": instance["scope"]!["kind"] = "unknown-scope"; break;
            case "dapr": instance["dapr"] = Component(node, "component:actor-eventstore")["dapr"]!.DeepClone(); break;
            case "software": instance["softwareVersion"] = "0.0.9-synthetic"; break;
            case "cluster-reference": instance["scope"]!["clusterId"] = "cluster:absent"; break;
            case "bundle-dangling": instance["bundleParentId"] = "component:absent"; break;
            case "bundle-self": instance["bundleParentId"] = "component:containerd"; break;
            case "bundle-cycle": Component(node, "component:distribution")["bundleParentId"] = "component:containerd"; break;
            case "bundle-scope": Component(node, "component:distribution")["scope"]!["clusterId"] = "cluster:management"; break;
            case "bundle-kind": instance["bundleParentId"] = "component:os"; break;
            case "stateful": instance["stateful"] = true; break;
            case "sources": instance["supportSources"] = new JsonArray(); break;
            case "owner": instance["currencyOwner"] = null; break;
            case "evidence": RemoveSubject(node, instance["loadedIdentityDigest"]!.GetValue<string>()); break;
        }
        Refuses(node, PathOf(node, instance) + "." + field, code);
    }

    [Theory]
    [InlineData("configuration", "dapr.aclDigest", "missing-selection")]
    [InlineData("resiliency", "dapr.resiliencyDigest", "missing-selection")]
    [InlineData("migration", "dapr.dataMigrationDigest", "missing-selection")]
    [InlineData("api-version", "dapr.type", "actor-contract-mismatch")]
    [InlineData("dapr", "dapr", "dapr-applicability-mismatch")]
    [InlineData("namespace", "scope", "invalid-scope-identifiers")]
    [InlineData("isolation", "dapr.actorStateStore", "actor-isolation-required")]
    [InlineData("physical-target", "physicalTargetIds", "actor-physical-target-reused")]
    [InlineData("credential", "credentialReferenceIds", "actor-credential-isolation-required")]
    [InlineData("hidden-state", "stateful", "state-store-must-be-stateful")]
    public void ApplicableDaprRequirementsRemainMandatory(string change, string field, string code)
    {
        JsonNode node = Node();
        JsonNode actor = Component(node, "component:actor-eventstore");
        switch (change)
        {
            case "configuration": actor["dapr"]!["aclDigest"] = null; break;
            case "resiliency": actor["dapr"]!["resiliencyDigest"] = null; break;
            case "migration": actor["dapr"]!["dataMigrationDigest"] = null; break;
            case "api-version": actor["dapr"]!["apiVersion"] = "17.0.0"; break;
            case "dapr": actor["dapr"] = null; break;
            case "namespace": actor["scope"]!["namespace"] = null; break;
            case "isolation": actor["dapr"]!["appScopeIds"]!.AsArray().Add("app:tenants"); break;
            case "physical-target": Component(node, "component:readmodels")["physicalTargetIds"] = actor["physicalTargetIds"]!.DeepClone(); break;
            case "credential": node["environment"]!["credentials"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == "credential:eventstore-database")!["consumerAppIds"]!.AsArray().Add("app:tenants"); break;
            case "hidden-state": actor["stateful"] = false; break;
        }
        Refuses(node, PathOf(node, actor) + "." + field, code);
    }

    [Theory]
    [InlineData("tokens", "$.applications[0].daprApiTokenEnabled", "required-api-tokens")]
    [InlineData("management", "$.infrastructure.topology.managementProviderId", "topology-instance-binding-mismatch")]
    [InlineData("workload", "$.infrastructure.topology.workloadProviderId", "topology-instance-binding-mismatch")]
    [InlineData("rancher", "$.infrastructure.topology.rancherProviderId", "topology-instance-binding-mismatch")]
    [InlineData("placement-restore", "$.components[11].id", "missing-restore-component")]
    public void ExactPairTokensAndRestoreBindingsArePreserved(string change, string path, string code)
    {
        JsonNode node = Node();
        switch (change)
        {
            case "tokens": node["applications"]![0]!["daprApiTokenEnabled"] = false; break;
            case "management" or "workload": Component(node, "component:" + change)["providerId"] = "provider:rancher"; break;
            case "rancher": Component(node, "component:rancher")["scope"]!["clusterId"] = "cluster:workload"; break;
            case "placement-restore":
                JsonNode placement = Component(node, "component:placement");
                placement["stateful"] = false;
                JsonArray ids = node["restore"]!["units"]![0]!["componentIds"]!.AsArray();
                ids.RemoveAt(Enumerable.Range(0, ids.Count).Single(i => ids[i]!.GetValue<string>() == "component:placement"));
                path = PathOf(node, placement) + ".id";
                break;
        }
        Refuses(node, path, code);
    }

    [Fact]
    public void ExpectedInventoryMustBeIndependentCompleteNonemptyAndUnique()
    {
        ProductionProfileV2 profile = Profile();
        foreach (ProductionProfileValidationContext? context in new ProductionProfileValidationContext?[]
        {
            null, new([], Context.ExpectedComponentIds), new(Context.ExpectedApplicationIds, []),
            Context with { ExpectedComponentIds = Context.ExpectedComponentIds.Skip(1).ToArray() },
            Context with { ExpectedComponentIds = [.. Context.ExpectedComponentIds, "component:missing"] },
            Context with { ExpectedComponentIds = [.. Context.ExpectedComponentIds, Context.ExpectedComponentIds[0]] },
            Context with { ExpectedApplicationIds = ["app:eventstore"] },
        })
        {
            ProductionProfileV2Validator.Validate(profile, context).IsStructurallyValid.ShouldBeFalse();
            Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Decode(Bytes(), context!));
        }
        JsonNode node = Node();
        node["components"]!.AsArray().Remove(Component(node, "component:os"));
        Refuses(node, "$.components", "inventory-coverage-mismatch");
        node = Node();
        node["components"]!.AsArray().Add(Component(node, "component:containerd").DeepClone());
        Refuses(node, "$.components[17].id", "duplicate-identity");
    }

    [Fact]
    public void NonDaprProviderEvidenceAndScopedUniquenessCannotBeOmitted()
    {
        JsonNode node = Node();
        JsonNode containerd = Component(node, "component:containerd");
        string pin = node["infrastructure"]!["providers"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == "provider:containerd")!["immutableDigest"]!.GetValue<string>();
        RemoveSubject(node, pin);
        Refuses(node, PathOf(node, containerd) + ".evidenceIds", "evidence-subject-mismatch");
        node = Node();
        JsonNode alias = Component(node, "component:containerd").DeepClone();
        alias["id"] = "component:alias";
        node["components"]!.AsArray().Add(alias);
        Refuses(node, PathOf(node, alias), "duplicate-scoped-identity");
    }

    [Fact]
    public void DistinctInfrastructureKindsMayTruthfullyShareAScopedName()
    {
        JsonNode node = Node();
        Component(node, "component:os")["name"] = Component(node, "component:containerd")["name"]!.DeepClone();
        ProductionProfileV2 profile = ProductionProfileV2Codec.Seal(FromNode(node), Context);
        ProductionProfileV2Validator.Validate(profile, Context).IsStructurallyValid.ShouldBeTrue();
    }

    [Fact]
    public void CanonicalByteLimitAlsoAppliesToDirectV2Validation()
    {
        ProductionProfileV2 profile = Profile();
        ProductionProfileV2 oversized = profile with
        {
            Components = [profile.Components[0] with
            {
                SupportSources = Enumerable.Range(0, 2500).Select(i => new string('x', 2000) + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            }, .. profile.Components.Skip(1)],
        };
        ProductionProfileValidationResult result = ProductionProfileV2Validator.Validate(oversized, Context);
        result.IsStructurallyValid.ShouldBeFalse();
        result.Issues.ShouldBe([new ProductionProfileIssue("$", "size-limit")]);
        result.ResolvedDigest.ShouldBeNull();
        result.InfrastructureDigest.ShouldBeNull();
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Encode(oversized, Context)).Issues.ShouldBe(result.Issues);
    }

    [Theory]
    [InlineData("scope", false)]
    [InlineData("cluster", false)]
    [InlineData("namespace", false)]
    [InlineData("bundle", false)]
    [InlineData("loaded", false)]
    [InlineData("owner", false)]
    [InlineData("release", false)]
    [InlineData("provider-pin", true)]
    [InlineData("provider-version", true)]
    [InlineData("tool-pin", true)]
    [InlineData("topology-pin", true)]
    public void ActualBindingsAndObservationsNeverExpandTheInfrastructureDomain(string change, bool infrastructureChanges)
    {
        ProductionProfileV2 original = Profile();
        JsonNode node = Node();
        JsonNode containerd = Component(node, "component:containerd");
        switch (change)
        {
            case "scope": containerd["scope"]!["nodeId"] = "node:synthetic-2"; break;
            case "cluster":
                JsonNode scope = Component(node, "component:environment-service")["scope"]!;
                scope["kind"] = "cluster";
                scope["clusterId"] = "cluster:management";
                break;
            case "namespace": Component(node, "component:rancher")["scope"]!["namespace"] = "other-management"; break;
            case "bundle": containerd["bundleParentId"] = null; break;
            case "loaded": ChangeSubject(node, containerd, "loadedIdentityDigest"); break;
            case "owner": containerd["currencyOwner"] = "owner:other"; break;
            case "release": node["release"]!["id"] = "release:other"; break;
            case "provider-pin": ChangeSubject(node, node["infrastructure"]!["providers"]![0]!, "immutableDigest"); break;
            case "provider-version":
                node["infrastructure"]!["providers"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == "provider:containerd")!["version"] = "0.0.5-synthetic";
                containerd["softwareVersion"] = "0.0.5-synthetic";
                break;
            case "tool-pin": node["infrastructure"]!["tools"]![0]!["immutableDigest"] = new string('b', 64); break;
            case "topology-pin": node["infrastructure"]!["topology"]!["capacity"] = "synthetic-five-nodes"; break;
        }
        ProductionProfileV2 changed = ProductionProfileV2Codec.Seal(FromNode(node), Context);
        (changed.InfrastructureDigest != original.InfrastructureDigest).ShouldBe(infrastructureChanges);
        ProductionProfileV2Codec.ComputeResolvedDigest(changed, Context).ShouldNotBe(ProductionProfileV2Codec.ComputeResolvedDigest(original, Context));
    }

    [Fact]
    public void StrictVersionSelectionAndCanonicalEncodingHaveNoFallback()
    {
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileCodec.Decode(Bytes(), Context));
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Decode(File.ReadAllBytes(Path.Combine(Directory, "SyntheticProfile.v1.json")), Context));
        ProductionProfileV2Validator.Validate(Profile() with { SchemaVersion = 1 }, Context).Issues.ShouldContain(new ProductionProfileIssue("$.schemaVersion", "unsupported-version"));
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Decode((byte[])[.. Bytes(), (byte)'\n'], Context))
            .Issues.ShouldContain(new ProductionProfileIssue("$", "noncanonical-encoding"));
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Decode(Bytes(), Context, new string('0', 64)))
            .Issues.ShouldContain(new ProductionProfileIssue("$", "expected-resolved-digest-mismatch"));
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Decode(Bytes(), Context, expectedInfrastructureDigest: new string('0', 64)))
            .Issues.ShouldContain(new ProductionProfileIssue("$.infrastructureDigest", "expected-infrastructure-digest-mismatch"));
        byte[] owned = Bytes();
        ProductionProfileV2 profile = ProductionProfileV2Codec.Decode(owned, Context);
        Array.Fill(owned, (byte)' ');
        ProductionProfileV2Codec.Encode(profile, Context).ShouldBe(Bytes());
        ProductionProfileV2Codec.Encode(profile with { Components = profile.Components.Reverse().ToArray() }, Context).ShouldBe(Bytes());
    }

    [Fact]
    public void ClosedSchemaNeverEchoesUntrustedFieldsAndDoesNotMakeOtherFieldsNullable()
    {
        string canary = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        JsonNode node = Node();
        Component(node, "component:containerd")[canary] = canary;
        ProductionProfileValidationException failure = Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Decode(Encoding.UTF8.GetBytes(node.ToJsonString()), Context));
        failure.Issues.ShouldContain(new ProductionProfileIssue("$.components[4]", "unknown-field"));
        failure.Message.ShouldNotContain(canary);
        failure.Message.ShouldNotContain("credential-value-must-not-appear");
        node = Node();
        Component(node, "component:containerd").AsObject().Remove("bundleParentId");
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Decode(Encoding.UTF8.GetBytes(node.ToJsonString()), Context))
            .Issues.ShouldContain(new ProductionProfileIssue("$.components[4].bundleParentId", "missing-field"));
        node = Node();
        Component(node, "component:containerd")["roles"] = null;
        Refuses(node, "$.components[4].roles", "missing-selection");
        string duplicate = Encoding.UTF8.GetString(Bytes()).Replace("\"schemaVersion\":2", "\"schemaVersion\":2,\"schemaVersion\":2", StringComparison.Ordinal);
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Decode(Encoding.UTF8.GetBytes(duplicate), Context))
            .Issues.ShouldContain(new ProductionProfileIssue("$", "duplicate-field"));
    }

    [Theory]
    [InlineData("management")]
    [InlineData("workload")]
    [InlineData("rancher")]
    public void AdditionalDifferentlyPinnedTopologyInstancesRefuse(string selection)
    {
        JsonNode node = Node();
        JsonNode original = Component(node, "component:" + selection);
        JsonNode provider = node["infrastructure"]!["providers"]!.AsArray()
            .Single(p => p!["id"]!.GetValue<string>() == original["providerId"]!.GetValue<string>())!.DeepClone();
        provider["id"] = "provider:extra-" + selection;
        provider["version"] = "0.0.1-synthetic-extra";
        provider["immutableDigest"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("extra-pin:" + selection)));
        node["infrastructure"]!["providers"]!.AsArray().Add(provider);
        node["evidence"]![0]!["subjectDigests"]!.AsArray().Add(provider["immutableDigest"]!.DeepClone());
        // First bind a valid additional pin, so the refusal cannot come from a stale infrastructure digest.
        node["infrastructureDigest"] = ProductionProfileV2Codec.Seal(FromNode(node), Context).InfrastructureDigest;
        JsonNode extra = original.DeepClone();
        extra["id"] = "component:extra-" + selection;
        extra["name"] = "synthetic-extra-" + selection;
        extra["providerId"] = provider["id"]!.DeepClone();
        extra["softwareVersion"] = provider["version"]!.DeepClone();
        extra["loadedIdentityDigest"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("extra-loaded:" + selection)));
        node["components"]!.AsArray().Add(extra);
        node["evidence"]![0]!["componentIds"]!.AsArray().Add(extra["id"]!.DeepClone());
        node["evidence"]![0]!["subjectDigests"]!.AsArray().Add(extra["loadedIdentityDigest"]!.DeepClone());
        ProductionProfileValidationContext context = Context with { ExpectedComponentIds = [.. Context.ExpectedComponentIds, extra["id"]!.GetValue<string>()] };
        Refuses(node, "$.infrastructure.topology." + selection + "ProviderId", "topology-instance-binding-mismatch", context);
    }

    [Theory]
    [InlineData("team name")]
    [InlineData("team/name")]
    [InlineData("team.name")]
    [InlineData("team_name")]
    [InlineData("Team")]
    [InlineData("-team")]
    [InlineData("team-")]
    [InlineData("équipe")]
    public void V2ActualNamespacesRequireKubernetesDnsLabels(string value) => CheckNamespaces(value, valid: false);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public void V2ActualNamespaceLengthBoundariesAreEnforced(int length, bool valid) =>
        CheckNamespaces(new string('a', length), valid, length == 0 ? "missing-or-invalid-text" : "invalid-namespace");

    [Theory]
    [InlineData("0")]
    [InlineData("a-0")]
    public void V2NamespaceDigitsAndInternalHyphensAreApplicable(string value) => CheckNamespaces(value, valid: true);

    [Theory]
    [InlineData("team name")]
    [InlineData("team/name")]
    public void V1NamespaceAcceptanceRemainsUnchanged(string value)
    {
        JsonNode node = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Directory, "SyntheticProfile.v1.json")))!;
        foreach (JsonNode? application in node["applications"]!.AsArray()) application!["namespace"] = value;
        foreach (JsonNode? component in node["components"]!.AsArray()) component!["namespace"] = value;
        ProductionProfile profile = node.Deserialize<ProductionProfile>(Options)!;
        ProductionProfileValidationContext context = Context with { ExpectedComponentIds = Context.ExpectedComponentIds.Take(9).ToArray() };
        ProductionProfileValidator.Validate(profile, context).IsStructurallyValid.ShouldBeTrue();
        ProductionProfileCodec.Encode(profile, context).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("aclDigest")]
    [InlineData("resiliencyDigest")]
    [InlineData("dataMigrationDigest")]
    public void EachPopulatedDaprConfigurationDigestRequiresItsOwnEvidenceSubject(string field)
    {
        JsonNode node = Node();
        JsonNode actor = Component(node, "component:actor-eventstore");
        RemoveSubject(node, actor["dapr"]![field]!.GetValue<string>());
        Refuses(node, PathOf(node, actor) + ".evidenceIds", "evidence-subject-mismatch");
    }

    [Theory]
    [InlineData("environment")]
    [InlineData("node")]
    [InlineData("nested")]
    public void EnvironmentSameNodeAndNestedDistributionsContainActualConstituents(string scenario)
    {
        JsonNode node = Node();
        JsonNode root = Component(node, "component:distribution");
        JsonNode constituent = Component(node, "component:containerd");
        if (scenario == "environment") SetScope(root, "environment");
        else
        {
            JsonNode parent = Distribution(node, "component:environment-service", scenario == "nested" ? "environment" : "node");
            if (scenario == "nested")
            {
                root["bundleParentId"] = parent["id"]!.DeepClone();
                parent = Distribution(node, "component:os", "node");
            }
            constituent["bundleParentId"] = parent["id"]!.DeepClone();
        }
        Accepts(node);
    }

    [Theory]
    [InlineData("nodeId", "node:synthetic-2")]
    [InlineData("clusterId", "cluster:management")]
    public void NodeDistributionsRefuseOtherNodesOrClusters(string field, string value)
    {
        JsonNode node = Node();
        JsonNode parent = Distribution(node, "component:environment-service", "node");
        JsonNode constituent = Component(node, "component:containerd");
        constituent["bundleParentId"] = parent["id"]!.DeepClone();
        constituent["scope"]![field] = value;
        Refuses(node, PathOf(node, constituent) + ".bundleParentId", "bundle-scope-mismatch");
    }

    [Theory]
    [InlineData("dapr-component", "namespace", true)]
    [InlineData("container-runtime", "node", true)]
    [InlineData("node-os", "node", true)]
    [InlineData("crd", "cluster", true)]
    [InlineData("kubernetes", "cluster", true)]
    [InlineData("rancher", "namespace", true)]
    [InlineData("rancher", "cluster", true)]
    [InlineData("operator", "namespace", true)]
    [InlineData("operator", "cluster", true)]
    [InlineData("service", "environment", true)]
    [InlineData("service", "namespace", true)]
    [InlineData("service", "cluster", true)]
    [InlineData("distribution", "environment", true)]
    [InlineData("distribution", "cluster", true)]
    [InlineData("distribution", "node", true)]
    [InlineData("dapr-component", "cluster", false)]
    [InlineData("container-runtime", "cluster", false)]
    [InlineData("node-os", "cluster", false)]
    [InlineData("crd", "namespace", false)]
    [InlineData("kubernetes", "namespace", false)]
    [InlineData("rancher", "environment", false)]
    [InlineData("rancher", "node", false)]
    [InlineData("operator", "environment", false)]
    [InlineData("operator", "node", false)]
    [InlineData("service", "node", false)]
    [InlineData("distribution", "namespace", false)]
    public void ClosedKindAndScopeApplicabilityIsExecutable(string kind, string scope, bool valid)
    {
        JsonNode node = Node();
        string id = kind switch
        {
            "dapr-component" => "component:actor-eventstore",
            "container-runtime" => "component:containerd",
            "node-os" => "component:os",
            "crd" => "component:crd",
            "kubernetes" => "component:workload",
            "rancher" => "component:rancher",
            _ => "component:environment-service",
        };
        JsonNode instance = Component(node, id);
        instance["kind"] = kind;
        SetScope(instance, scope);
        if (valid) Accepts(node);
        else Refuses(node, PathOf(node, instance) + ".kind", "kind-scope-mismatch");
    }

    [Fact]
    public void NonDaprInfrastructureCannotAliasAnActorPhysicalTarget()
    {
        JsonNode node = Node();
        JsonNode actor = Component(node, "component:actor-eventstore");
        JsonNode infrastructure = Component(node, "component:environment-service");
        infrastructure["dapr"].ShouldBeNull();
        JsonNode target = node["environment"]!["physicalTargets"]!.AsArray()
            .Single(t => t!["id"]!.GetValue<string>() == actor["physicalTargetIds"]![0]!.GetValue<string>())!.DeepClone();
        target["id"] = "target:infrastructure-alias";
        target["host"] = target["host"]!.GetValue<string>().ToUpperInvariant() + ".";
        node["environment"]!["physicalTargets"]!.AsArray().Add(target);
        node["evidence"]![0]!["physicalTargetIds"]!.AsArray().Add(target["id"]!.DeepClone());
        infrastructure["providerId"] = actor["providerId"]!.DeepClone();
        infrastructure["softwareVersion"] = actor["softwareVersion"]!.DeepClone();
        infrastructure["physicalTargetIds"] = new JsonArray(target["id"]!.GetValue<string>());
        Refuses(node, PathOf(node, actor) + ".physicalTargetIds", "actor-physical-target-reused");
    }

    private static byte[] Bytes() => File.ReadAllBytes(Path.Combine(Directory, "SyntheticProfile.v2.json"));
    private static ProductionProfileV2 Profile() => ProductionProfileV2Codec.Decode(Bytes(), Context);
    private static JsonNode Node() => JsonNode.Parse(Bytes())!;
    private static ProductionProfileV2 FromNode(JsonNode node) => node.Deserialize<ProductionProfileV2>(Options)!;
    private static JsonNode Component(JsonNode node, string id) => node["components"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == id)!;
    private static string PathOf(JsonNode node, JsonNode component) => $"$.components[{node["components"]!.AsArray().IndexOf(component)}]";

    private static void Refuses(JsonNode node, string path, string code, ProductionProfileValidationContext? context = null)
    {
        context ??= Context;
        ProductionProfileValidationResult result = ProductionProfileV2Validator.Validate(FromNode(node), context);
        result.IsStructurallyValid.ShouldBeFalse();
        result.Issues.ShouldContain(new ProductionProfileIssue(path, code));
        result.InfrastructureDigest.ShouldBeNull();
        result.ResolvedDigest.ShouldBeNull();
        result.Authorization.ShouldBe("unevaluated");
        result.Qualification.ShouldBe("unevaluated");
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileV2Codec.Encode(FromNode(node), context))
            .Issues.ShouldContain(new ProductionProfileIssue(path, code));
    }

    private static void Accepts(JsonNode node)
    {
        ProductionProfileV2 profile = FromNode(node);
        ProductionProfileV2Validator.Validate(profile, Context).IsStructurallyValid.ShouldBeTrue();
        ProductionProfileV2Codec.Encode(profile, Context).ShouldNotBeEmpty();
    }

    private static void CheckNamespaces(string value, bool valid, string code = "invalid-namespace")
    {
        foreach (bool application in new[] { false, true })
        {
            JsonNode node = Node();
            string path;
            if (application)
            {
                foreach (JsonNode? app in node["applications"]!.AsArray()) app!["namespace"] = value;
                foreach (JsonNode? component in node["components"]!.AsArray())
                    if (component!["scope"]!["namespace"]?.GetValue<string>() == "synthetic") component["scope"]!["namespace"] = value;
                path = "$.applications[0].namespace";
            }
            else
            {
                JsonNode component = Component(node, "component:rancher");
                component["scope"]!["namespace"] = value;
                path = PathOf(node, component) + ".scope.namespace";
            }
            if (valid) Accepts(node);
            else Refuses(node, path, code);
        }
    }

    private static void SetScope(JsonNode component, string kind)
    {
        JsonNode scope = component["scope"]!;
        scope["kind"] = kind;
        scope["clusterId"] = kind == "environment" ? null : scope["clusterId"]?.GetValue<string>() ?? "cluster:workload";
        scope["nodeId"] = kind == "node" ? "node:synthetic-1" : null;
        scope["namespace"] = kind == "namespace" ? scope["namespace"]?.GetValue<string>() ?? "synthetic-scope" : null;
    }

    private static JsonNode Distribution(JsonNode node, string id, string scope)
    {
        JsonNode component = Component(node, id);
        component["kind"] = "distribution";
        component["providerId"] = "provider:distribution";
        component["softwareVersion"] = node["infrastructure"]!["providers"]!.AsArray()
            .Single(p => p!["id"]!.GetValue<string>() == "provider:distribution")!["version"]!.DeepClone();
        SetScope(component, scope);
        return component;
    }

    private static void RemoveSubject(JsonNode node, string value)
    {
        JsonArray subjects = node["evidence"]![0]!["subjectDigests"]!.AsArray();
        subjects.RemoveAt(Enumerable.Range(0, subjects.Count).Single(i => subjects[i]!.GetValue<string>() == value));
    }

    private static void ChangeSubject(JsonNode node, JsonNode section, string field)
    {
        string old = section[field]!.GetValue<string>();
        section[field] = new string('b', 64);
        JsonArray subjects = node["evidence"]![0]!["subjectDigests"]!.AsArray();
        for (int i = 0; i < subjects.Count; i++)
            if (subjects[i]!.GetValue<string>() == old) subjects[i] = new string('b', 64);
    }
}
