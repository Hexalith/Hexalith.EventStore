using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Deployment;

namespace Hexalith.EventStore.Contracts.Tests.Deployment;

public class ProductionProfileTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Deployment", "Fixtures");
    private static readonly ProductionProfileValidationContext Context = new(
        ["app:eventstore", "app:tenants"],
        ["component:actor-eventstore", "component:actor-tenants", "component:readmodels", "component:broker", "component:openbao",
            "component:scheduler", "component:placement", "component:issuer", "component:backup"]);

    [Fact]
    public void IndependentGoldenBytesAndBothDigestDomainsRoundTripEveryField()
    {
        byte[] bytes = FixtureBytes();
        using JsonDocument expected = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureDirectory, "SyntheticDigests.v1.json")));
        string infrastructure = expected.RootElement.GetProperty("infrastructure").GetString()!;
        string resolved = expected.RootElement.GetProperty("resolved").GetString()!;
        ProductionProfile profile = ProductionProfileCodec.Decode(bytes, Context, resolved, infrastructure);
        ProductionProfileCodec.Encode(profile, Context).ShouldBe(bytes);
        ProductionProfileCodec.ComputeInfrastructureDigest(profile, Context).ShouldBe(infrastructure);
        ProductionProfileCodec.ComputeResolvedDigest(profile, Context).ShouldBe(resolved);
        Convert.ToHexStringLower(SHA256.HashData(bytes)).ShouldBe(resolved);
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(FixtureDirectory, "SyntheticInfrastructurePreimage.v1.json"))))
            .ShouldBe(infrastructure);
        ProductionProfileValidationResult result = ProductionProfileValidator.Validate(profile, Context);
        result.IsStructurallyValid.ShouldBeTrue();
        result.ResolvedDigest.ShouldBe(resolved);
        result.InfrastructureDigest.ShouldBe(infrastructure);
        result.Issues.ShouldBeEmpty();
        result.Authorization.ShouldBe("unevaluated");
        result.Qualification.ShouldBe("unevaluated");
        profile.Evidence.Single().QualificationId.ShouldBe("qualification:synthetic-unverified");
        profile.Applications.Count.ShouldBe(2);
        profile.Environment.PhysicalTargets.Count.ShouldBe(3);
        profile.Evidence.Single().SubjectDigests.Distinct(StringComparer.Ordinal).Count().ShouldBeGreaterThan(80);
    }

    [Fact]
    public void ExternalComposingCoverageRejectsAnUnderDeclaredInventory()
    {
        ProductionProfile profile = ProductionProfileCodec.Decode(FixtureBytes(), Context);
        ProductionProfileValidationContext incomplete = Context with
        {
            ExpectedApplicationIds = ["app:eventstore"],
            ExpectedComponentIds = Context.ExpectedComponentIds.Skip(1).ToArray(),
        };
        ProductionProfileValidationResult result = ProductionProfileValidator.Validate(profile, incomplete);
        result.IsStructurallyValid.ShouldBeFalse();
        result.Issues.ShouldContain(new ProductionProfileIssue("$.applications", "inventory-coverage-mismatch"));
        result.Issues.ShouldContain(new ProductionProfileIssue("$.components", "inventory-coverage-mismatch"));
        result.ResolvedDigest.ShouldBeNull();
        result.Authorization.ShouldBe("unevaluated");
        result.Qualification.ShouldBe("unevaluated");
    }

    [Theory]
    [InlineData("release")]
    [InlineData("environment")]
    [InlineData("loaded-identity")]
    [InlineData("credential-generation")]
    [InlineData("currency-owner")]
    [InlineData("provider-pin")]
    public void DigestDomainsIncludeOnlyTheirDeclaredInputs(string change)
    {
        ProductionProfile original = Profile();
        JsonNode node = Node();
        switch (change)
        {
            case "release": node["release"]!["id"] = "release:changed"; break;
            case "environment": node["environment"]!["endpoints"]![0]!["address"] = "amqps://changed.invalid:5671"; break;
            case "loaded-identity": ChangeSubject(node, node["components"]![0]!, "loadedIdentityDigest", new string('b', 64)); break;
            case "credential-generation": node["environment"]!["credentials"]![0]!["generation"] = "generation:2"; break;
            case "currency-owner": node["components"]![0]!["currencyOwner"] = "owner:changed"; break;
            case "provider-pin": ChangeSubject(node, node["infrastructure"]!["providers"]![0]!, "immutableDigest", new string('b', 64)); break;
        }
        ProductionProfile changed = ProductionProfileCodec.Seal(FromNode(node), Context);
        (changed.InfrastructureDigest == original.InfrastructureDigest).ShouldBe(change != "provider-pin");
        ProductionProfileCodec.ComputeResolvedDigest(changed, Context).ShouldNotBe(ProductionProfileCodec.ComputeResolvedDigest(original, Context));
    }

    [Fact]
    public void CanonicalSetsSortOrdinallyWhileRestoreOrderRemainsDeclared()
    {
        ProductionProfile profile = Profile();
        ProductionProfile permuted = profile with
        {
            Applications = profile.Applications.Reverse().ToArray(),
            Components = profile.Components.Reverse().Select(c => c with { Roles = c.Roles.Reverse().ToArray() }).ToArray(),
            Evidence = profile.Evidence.Select(e => e with { ComponentIds = e.ComponentIds.Reverse().ToArray() }).ToArray(),
        };
        ProductionProfileCodec.Encode(permuted, Context).ShouldBe(FixtureBytes());
        ProductionProfileRestoreUnit unit = profile.Restore.Units.Single();
        ProductionProfile ordered = ProductionProfileCodec.Seal(profile with
        {
            Restore = profile.Restore with
            {
                Units = [unit with { Id = "restore:z" }, unit with { Id = "restore:a" }],
                Order = ["restore:z", "restore:a"],
                ForbiddenIndependentRestores = ["restore:a", "restore:z"],
            },
        }, Context);
        ProductionProfileCodec.Decode(ProductionProfileCodec.Encode(ordered, Context), Context).Restore.Order.ShouldBe(["restore:z", "restore:a"]);
        ProductionProfile reversed = ordered with { Restore = ordered.Restore with { Order = ["restore:a", "restore:z"] } };
        reversed.InfrastructureDigest.ShouldBe(ordered.InfrastructureDigest);
        ProductionProfileCodec.ComputeResolvedDigest(reversed, Context).ShouldNotBe(ProductionProfileCodec.ComputeResolvedDigest(ordered, Context));
    }

    [Theory]
    [InlineData("null-target", "missing-selection")]
    [InlineData("missing-component", "inventory-coverage-mismatch")]
    [InlineData("duplicate-app", "duplicate-identity")]
    [InlineData("duplicate-component", "duplicate-identity")]
    [InlineData("duplicate-scope", "duplicate-scoped-identity")]
    [InlineData("duplicate-set", "duplicate-set-member")]
    [InlineData("version", "unsupported-version")]
    [InlineData("purpose", "unsupported-purpose")]
    [InlineData("digest", "invalid-digest")]
    [InlineData("infrastructure-digest", "infrastructure-digest-mismatch")]
    [InlineData("app-reference", "dangling-reference")]
    [InlineData("component-reference", "dangling-reference")]
    [InlineData("target-reference", "dangling-reference")]
    [InlineData("provider-reference", "dangling-reference")]
    [InlineData("app-scope", "app-component-scope-mismatch")]
    [InlineData("actor-scope", "actor-isolation-required")]
    [InlineData("actor-target", "actor-physical-target-reused")]
    [InlineData("target-alias", "actor-physical-target-reused")]
    [InlineData("actor-table-alias", "actor-physical-target-reused")]
    [InlineData("actor-missing", "actor-component-coverage-mismatch")]
    [InlineData("actor-credential", "actor-credential-isolation-required")]
    [InlineData("inline-credential", "credential-reference-required")]
    [InlineData("endpoint-credential", "credential-free-endpoint-required")]
    [InlineData("endpoint-query", "credential-free-endpoint-required")]
    [InlineData("runtime-floor", "runtime-floor-mismatch")]
    [InlineData("runtime-minor", "runtime-minor-mismatch")]
    [InlineData("runtime-image", "runtime-binding-mismatch")]
    [InlineData("application-image", "application-artifact-binding-mismatch")]
    [InlineData("composed-host", "composed-host-artifact-mismatch")]
    [InlineData("broker-component", "broker-component-binding-mismatch")]
    [InlineData("secret-component", "secret-component-binding-mismatch")]
    [InlineData("scheduler-component", "scheduler-component-binding-mismatch")]
    [InlineData("placement-component", "placement-component-binding-mismatch")]
    [InlineData("tokens", "required-api-tokens")]
    [InlineData("openbao-floor", "server-floor-mismatch")]
    [InlineData("secret-binding", "secret-contract-binding-mismatch")]
    [InlineData("catalog-binding", "catalog-binding-mismatch")]
    [InlineData("guard", "mandatory-protection-required")]
    [InlineData("append-semantics", "inventory-coverage-mismatch")]
    [InlineData("protection-tuple", "protection-tuple-mismatch")]
    [InlineData("writers", "missing-writer-inventory")]
    [InlineData("wrong-writer", "writer-binding-mismatch")]
    [InlineData("erasure", "erasure-binding-mismatch")]
    [InlineData("replicas", "unsupported-replica-freshness-claim")]
    [InlineData("restore-coverage", "inventory-coverage-mismatch")]
    [InlineData("restore-component", "missing-restore-component")]
    [InlineData("restore-order", "inventory-coverage-mismatch")]
    [InlineData("restore-independent", "inventory-coverage-mismatch")]
    [InlineData("evidence-reference", "dangling-reference")]
    [InlineData("evidence-coverage", "evidence-coverage-mismatch")]
    [InlineData("evidence-missing", "missing-evidence-references")]
    [InlineData("currency-source", "missing-currency-inputs")]
    public void StructuralMatrixRefusesWithStableSafeReasonsBesidePositiveControl(string scenario, string expectedCode)
    {
        ProductionProfileValidator.Validate(Profile(), Context).IsStructurallyValid.ShouldBeTrue();
        JsonNode node = Node();
        JsonNode actor = Component(node, "component:actor-eventstore");
        JsonNode app = Application(node, "app:eventstore");
        switch (scenario)
        {
            case "null-target": node["environment"]!["physicalTargets"]![0] = null; break;
            case "missing-component": node["components"]!.AsArray().RemoveAt(0); break;
            case "duplicate-app": node["applications"]!.AsArray().Add(app.DeepClone()); break;
            case "duplicate-component": node["components"]!.AsArray().Add(actor.DeepClone()); break;
            case "duplicate-scope": Application(node, "app:tenants")["appId"] = "eventstore"; break;
            case "duplicate-set": app["actorTypes"]!.AsArray().Add(app["actorTypes"]![0]!.DeepClone()); break;
            case "version": node["schemaVersion"] = 2; break;
            case "purpose": node["purpose"] = "other"; break;
            case "digest": node["release"]!["composedHostDigest"] = "never-print-credential"; break;
            case "infrastructure-digest": node["infrastructureDigest"] = new string('b', 64); break;
            case "app-reference": actor["appScopeIds"]![0] = "app:absent"; break;
            case "component-reference": app["componentIds"]![0] = "component:absent"; break;
            case "target-reference": actor["physicalTargetIds"]![0] = "target:absent"; break;
            case "provider-reference": actor["providerId"] = "provider:absent"; break;
            case "app-scope": actor["appScopeIds"] = new JsonArray("app:tenants"); break;
            case "actor-scope": actor["appScopeIds"]!.AsArray().Add("app:tenants"); break;
            case "actor-target": Component(node, "component:readmodels")["physicalTargetIds"] = actor["physicalTargetIds"]!.DeepClone(); break;
            case "target-alias":
                JsonNode alias = node["environment"]!["physicalTargets"]![0]!.DeepClone();
                alias["id"] = "target:alias";
                node["environment"]!["physicalTargets"]!.AsArray().Add(alias);
                Component(node, "component:readmodels")["physicalTargetIds"] = new JsonArray("target:alias");
                break;
            case "actor-table-alias": node["environment"]!["physicalTargets"]![2]!["metadataTableName"] = node["environment"]!["physicalTargets"]![0]!["tableName"]!.DeepClone(); break;
            case "actor-missing": actor["actorStateStore"] = false; break;
            case "actor-credential": node["environment"]!["credentials"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == "credential:eventstore-database")!["consumerAppIds"]!.AsArray().Add("app:tenants"); break;
            case "inline-credential": node["environment"]!["credentials"]![0]!["store"] = "never-print-credential"; break;
            case "endpoint-credential": node["environment"]!["endpoints"]![0]!["address"] = "amqps://user:never-print-credential@broker.invalid"; break;
            case "endpoint-query": node["environment"]!["endpoints"]![0]!["address"] = "amqps://broker.invalid?password=never-print-credential"; break;
            case "runtime-floor": node["release"]!["runtimeVersion"] = "1.18.2"; break;
            case "runtime-minor": node["template"]!["runtimeMinor"] = "1.19"; break;
            case "runtime-image": app["sidecarImageDigest"] = new string('b', 64); break;
            case "application-image": app["imageDigest"] = new string('b', 64); break;
            case "composed-host": node["release"]!["composedHostDigest"] = new string('b', 64); break;
            case "broker-component": node["environment"]!["brokerComponentId"] = "component:readmodels"; break;
            case "secret-component": node["environment"]!["openBaoComponentId"] = "component:readmodels"; break;
            case "scheduler-component": node["environment"]!["schedulerComponentIds"] = new JsonArray("component:readmodels"); break;
            case "placement-component": node["environment"]!["placementComponentIds"] = new JsonArray("component:readmodels"); break;
            case "tokens": app["daprApiTokenEnabled"] = false; break;
            case "openbao-floor": node["environment"]!["openBaoServerVersion"] = "1.0.0"; break;
            case "secret-binding": node["environment"]!["secretContractDigest"] = new string('b', 64); break;
            case "catalog-binding": node["catalogs"]!["contentDigest"] = new string('b', 64); break;
            case "guard": node["protection"]!["mandatoryGuard"] = false; break;
            case "append-semantics": node["protection"]!["semantics"]!.AsArray().RemoveAt(0); break;
            case "protection-tuple": node["protection"]!["runtimeVersion"] = "1.19.0"; break;
            case "writers": node["protection"]!["writers"] = new JsonArray(); break;
            case "wrong-writer": node["protection"]!["writers"]![0]!["appId"] = "app:tenants"; break;
            case "erasure": node["protection"]!["writers"]![0]!["erasureContractDigest"] = new string('b', 64); break;
            case "replicas": node["freshness"]!["replicas"] = 2; node["freshness"]!["multiReplicaFreshnessClaim"] = true; break;
            case "restore-coverage": node["restore"]!["units"]![0]!["coverage"]!.AsArray().RemoveAt(0); break;
            case "restore-component": node["restore"]!["units"]![0]!["componentIds"]!.AsArray().RemoveAt(0); break;
            case "restore-order": node["restore"]!["order"] = new JsonArray(); break;
            case "restore-independent": node["restore"]!["forbiddenIndependentRestores"] = new JsonArray(); break;
            case "evidence-reference": app["evidenceIds"]![0] = "evidence:absent"; break;
            case "evidence-coverage": node["evidence"]![0]!["appIds"] = new JsonArray("app:tenants"); break;
            case "evidence-missing": app["evidenceIds"] = new JsonArray(); break;
            case "currency-source": actor["supportSources"] = new JsonArray(); break;
        }
        ProductionProfile invalid = FromNode(node);
        ProductionProfileValidationResult result = ProductionProfileValidator.Validate(invalid, Context);
        result.IsStructurallyValid.ShouldBeFalse();
        result.InfrastructureDigest.ShouldBeNull();
        result.ResolvedDigest.ShouldBeNull();
        result.Authorization.ShouldBe("unevaluated");
        result.Qualification.ShouldBe("unevaluated");
        result.Issues.ShouldContain(i => i.Code == expectedCode);
        result.Issues.ShouldBe(ProductionProfileValidator.Validate(invalid, Context).Issues);
        result.Issues.ShouldBe(result.Issues.OrderBy(i => i.Path, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal));
        ProductionProfileValidationException failure = Should.Throw<ProductionProfileValidationException>(() => ProductionProfileCodec.Encode(invalid, Context));
        failure.Message.ShouldNotContain("never-print-credential");
        failure.InnerException.ShouldBeNull();
    }

    [Theory]
    [InlineData("unknown-field", "unknown-field")]
    [InlineData("duplicate-field", "duplicate-field")]
    [InlineData("nested-duplicate", "duplicate-field")]
    [InlineData("missing-section", "missing-field")]
    [InlineData("missing-field", "missing-field")]
    [InlineData("wrong-type", "wrong-type")]
    [InlineData("float", "wrong-type")]
    [InlineData("comment", "invalid-json-or-closed-schema")]
    [InlineData("trailing-data", "invalid-json-or-closed-schema")]
    [InlineData("depth", "invalid-json-or-closed-schema")]
    [InlineData("whitespace", "noncanonical-encoding")]
    [InlineData("property-order", "noncanonical-encoding")]
    [InlineData("set-order", "noncanonical-encoding")]
    [InlineData("alternate-escaping", "noncanonical-encoding")]
    [InlineData("bom", "invalid-json-or-closed-schema")]
    [InlineData("utf16", "invalid-json-or-closed-schema")]
    [InlineData("size", "size-limit")]
    [InlineData("empty", "size-limit")]
    public void ClosedDecodeMatrixRefusesWithoutEchoingInput(string scenario, string expectedCode)
    {
        ProductionProfileCodec.Decode(FixtureBytes(), Context);
        string json = Encoding.UTF8.GetString(FixtureBytes());
        byte[] bytes;
        JsonNode node = Node();
        switch (scenario)
        {
            case "unknown-field": node["never-print-credential"] = "never-print-credential"; json = node.ToJsonString(); break;
            case "duplicate-field": json = "{\"purpose\":\"never-print-credential\"," + json[1..]; break;
            case "nested-duplicate": json = json.Replace("\"mandatoryGuard\":true", "\"mandatoryGuard\":true,\"mandatoryGuard\":false", StringComparison.Ordinal); break;
            case "missing-section": node.AsObject().Remove("issuer"); json = node.ToJsonString(); break;
            case "missing-field": node["issuer"]!.AsObject().Remove("contractDigest"); json = node.ToJsonString(); break;
            case "wrong-type": node["schemaVersion"] = "never-print-credential"; json = node.ToJsonString(); break;
            case "float": json = json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1.0", StringComparison.Ordinal); break;
            case "comment": json = "/*never-print-credential*/" + json; break;
            case "trailing-data": json += "{}"; break;
            case "depth": json = new string('[', 33) + "0" + new string(']', 33); break;
            case "whitespace": json += "\n"; break;
            case "property-order": json = "{\"schemaVersion\":1," + json[1..].Replace("\"schemaVersion\":1,", "", StringComparison.Ordinal); break;
            case "set-order": node["applications"] = new JsonArray(node["applications"]!.AsArray().Reverse().Select(a => a!.DeepClone()).ToArray()); json = node.ToJsonString(); break;
            case "alternate-escaping": json = json.Replace("\\u002B", "+", StringComparison.Ordinal); break;
        }
        bytes = scenario switch
        {
            "bom" => [0xef, 0xbb, 0xbf, .. FixtureBytes()],
            "utf16" => Encoding.Unicode.GetBytes(json),
            "size" => new byte[ProductionProfileCodec.MaximumByteLength + 1],
            "empty" => [],
            _ => Encoding.UTF8.GetBytes(json),
        };
        ProductionProfileValidationException failure = Should.Throw<ProductionProfileValidationException>(() => ProductionProfileCodec.Decode(bytes, Context));
        failure.Issues.ShouldContain(i => i.Code == expectedCode);
        failure.Message.ShouldNotContain("never-print-credential");
        failure.InnerException.ShouldBeNull();
    }

    [Fact]
    public void DirectValidationEnforcesTheCodecCanonicalByteLimit()
    {
        ProductionProfile profile = Profile();
        ProductionProfileComponent first = profile.Components[0];
        ProductionProfile oversized = profile with
        {
            Components = [first with
            {
                SupportSources = Enumerable.Range(0, 2500).Select(i => new string('x', 2000) + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            }, .. profile.Components.Skip(1)],
        };
        ProductionProfileValidationResult result = ProductionProfileValidator.Validate(oversized, Context);
        result.IsStructurallyValid.ShouldBeFalse();
        result.Issues.ShouldBe([new ProductionProfileIssue("$", "size-limit")]);
        result.ResolvedDigest.ShouldBeNull();
        result.InfrastructureDigest.ShouldBeNull();
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileCodec.Encode(oversized, Context))
            .Issues.ShouldBe(result.Issues);
    }

    [Theory]
    [InlineData("2.6.4", "2.6.4")]
    [InlineData("2.6.4", "2.6.7")]
    [InlineData("2.6.4", "2.7.1")]
    [InlineData("2.7.1", "2.7.1")]
    [InlineData("2.7.1", "2.8.0")]
    public void OpenBaoPatchedBranchFloorsAcceptMatchingProviderPins(string floor, string server)
    {
        JsonNode node = Node();
        node["template"]!["openBaoServerFloor"] = floor;
        node["environment"]!["openBaoServerVersion"] = server;
        Provider(node, "provider:openbao")["version"] = server;
        ProductionProfileValidator.Validate(ProductionProfileCodec.Seal(FromNode(node), Context), Context).IsStructurallyValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("bao-low-self-floor", "$.template.openBaoServerFloor", "server-floor-mismatch")]
    [InlineData("bao-unpatched-branch", "$.environment.openBaoServerVersion", "server-floor-mismatch")]
    [InlineData("bao-pin-version", "$.environment.openBaoServerVersion", "secret-provider-binding-mismatch")]
    [InlineData("bao-pin-kind", "$.environment.openBaoServerVersion", "secret-provider-binding-mismatch")]
    [InlineData("management-kind", "$.infrastructure.topology.managementProviderId", "topology-provider-binding-mismatch")]
    [InlineData("workload-kind", "$.infrastructure.topology.workloadProviderId", "topology-provider-binding-mismatch")]
    [InlineData("rancher-kind", "$.infrastructure.topology.rancherProviderId", "topology-provider-binding-mismatch")]
    [InlineData("management-role", "$.infrastructure.topology.managementProviderId", "topology-provider-binding-mismatch")]
    [InlineData("package-as-image", "$.applications[1].imageDigest", "application-artifact-binding-mismatch")]
    [InlineData("credential-alias", "$.environment.credentials[3]", "actor-credential-alias")]
    [InlineData("credential-reverse-only", "$.environment.credentials[7].componentIds[0]", "credential-component-mismatch")]
    [InlineData("credential-reverse-shared", "$.environment.credentials[7].componentIds[0]", "actor-credential-isolation-required")]
    [InlineData("unrelated-bootstrap", "$.issuer.bootstrapCredentialReferenceId", "issuer-bootstrap-binding-mismatch")]
    [InlineData("dns-alias", "$.components[0].physicalTargetIds", "actor-physical-target-reused")]
    [InlineData("hidden-state", "$.components[7].stateful", "state-store-must-be-stateful")]
    [InlineData("hidden-state-restore", "$.components[7].id", "missing-restore-component")]
    [InlineData("placement-restore", "$.components[6].id", "missing-restore-component")]
    [InlineData("component-evidence-association", "$.components[7].evidenceIds[0]", "evidence-coverage-mismatch")]
    [InlineData("nested-null", "$.environment.physicalTargets[0].host", "missing-selection")]
    [InlineData("nested-reference", "$.components[0].physicalTargetIds[0]", "dangling-reference")]
    [InlineData("nested-binding", "$.applications[0].imageDigest", "application-artifact-binding-mismatch")]
    public void ReviewedBindingsRefuseAtExactSafePaths(string scenario, string path, string code)
    {
        ProductionProfileValidator.Validate(Profile(), Context).IsStructurallyValid.ShouldBeTrue();
        JsonNode node = Node();
        switch (scenario)
        {
            case "bao-low-self-floor":
                node["template"]!["openBaoServerFloor"] = "2.0.0";
                node["environment"]!["openBaoServerVersion"] = "2.4.0";
                Provider(node, "provider:openbao")["version"] = "2.4.0";
                break;
            case "bao-unpatched-branch":
                node["template"]!["openBaoServerFloor"] = "2.6.4";
                node["environment"]!["openBaoServerVersion"] = "2.7.0";
                Provider(node, "provider:openbao")["version"] = "2.7.0";
                break;
            case "bao-pin-version": Provider(node, "provider:openbao")["version"] = "2.7.2"; break;
            case "bao-pin-kind": Component(node, "component:openbao")["providerId"] = "provider:postgresql"; break;
            case "management-kind": node["infrastructure"]!["topology"]!["managementProviderId"] = "provider:postgresql"; break;
            case "workload-kind": node["infrastructure"]!["topology"]!["workloadProviderId"] = "provider:postgresql"; break;
            case "rancher-kind": node["infrastructure"]!["topology"]!["rancherProviderId"] = "provider:postgresql"; break;
            case "management-role": RemoveValue(Provider(node, "provider:cluster")["roles"]!, "management"); break;
            case "package-as-image":
                Application(node, "app:tenants")["artifactId"] = "artifact:tenants";
                Application(node, "app:tenants")["imageDigest"] = node["release"]!["artifacts"]!.AsArray()
                    .Single(a => a!["id"]!.GetValue<string>() == "artifact:tenants")!["digest"]!.DeepClone();
                break;
            case "credential-alias":
                foreach (string field in new[] { "store", "logicalName", "mapKey", "generation" })
                    Credential(node, "credential:tenants-database")[field] = Credential(node, "credential:eventstore-database")[field]!.DeepClone();
                break;
            case "credential-reverse-only":
            case "credential-reverse-shared":
                JsonNode reverse = Credential(node, "credential:eventstore-database").DeepClone();
                reverse["id"] = "credential:reverse";
                reverse["logicalName"] = "isolated/reverse";
                if (scenario == "credential-reverse-shared") reverse["componentIds"] = new JsonArray("component:actor-tenants");
                node["environment"]!["credentials"]!.AsArray().Add(reverse);
                break;
            case "unrelated-bootstrap": node["issuer"]!["bootstrapCredentialReferenceId"] = "credential:eventstore-app-token"; break;
            case "dns-alias":
                node["environment"]!["physicalTargets"]![2]!["host"] = node["environment"]!["physicalTargets"]![0]!["host"]!.GetValue<string>() + ".";
                node["environment"]!["physicalTargets"]![2]!["tableName"] = node["environment"]!["physicalTargets"]![0]!["tableName"]!.DeepClone();
                break;
            case "hidden-state":
            case "hidden-state-restore":
                Component(node, "component:readmodels")["stateful"] = false;
                RemoveValue(node["restore"]!["units"]![0]!["componentIds"]!, "component:readmodels");
                break;
            case "placement-restore": RemoveValue(node["restore"]!["units"]![0]!["componentIds"]!, "component:placement"); break;
            case "component-evidence-association": RemoveValue(node["evidence"]![0]!["componentIds"]!, "component:readmodels"); break;
            case "nested-null": node["environment"]!["physicalTargets"]![0]!["host"] = null; break;
            case "nested-reference": Component(node, "component:actor-eventstore")["physicalTargetIds"]![0] = "target:missing"; break;
            case "nested-binding": Application(node, "app:eventstore")["imageDigest"] = new string('b', 64); break;
        }
        ProductionProfileValidationResult result = ProductionProfileValidator.Validate(FromNode(node), Context);
        result.IsStructurallyValid.ShouldBeFalse();
        result.Issues.ShouldContain(new ProductionProfileIssue(path, code));
        result.Authorization.ShouldBe("unevaluated");
        result.Qualification.ShouldBe("unevaluated");
    }

    [Theory]
    [InlineData("application", "imageDigest", "$.applications[0].evidenceIds")]
    [InlineData("component", "loadedIdentityDigest", "$.components[7].evidenceIds")]
    [InlineData("release", "helmPackageDigest", "$.release.evidenceIds")]
    [InlineData("cli", "runtimeImageDigest", "$.release.cliCompatibilityEvidenceId")]
    [InlineData("broker", "brokerContractDigest", "$.environment.brokerEvidenceIds")]
    [InlineData("scheduler", "schedulerConfigurationDigest", "$.environment.schedulerPlacementEvidenceIds")]
    [InlineData("catalogs", "rootDigest", "$.catalogs.evidenceIds")]
    [InlineData("protection", "transactionSemanticsDigest", "$.protection.evidenceIds")]
    [InlineData("issuer", "assertionConfigurationDigest", "$.issuer.evidenceIds")]
    [InlineData("freshness", "postureDigest", "$.freshness.evidenceIds")]
    [InlineData("restore", "acceptedPostureDigest", "$.restore.evidenceIds")]
    public void EvidenceSubjectsMustBindEachReferencingSection(string section, string field, string path)
    {
        JsonNode node = Node();
        JsonNode subject = section switch
        {
            "application" => Application(node, "app:eventstore"),
            "component" => Component(node, "component:readmodels"),
            "cli" => node["release"]!,
            "broker" or "scheduler" => node["environment"]!,
            _ => node[section]!,
        };
        RemoveValue(node["evidence"]![0]!["subjectDigests"]!, subject[field]!.GetValue<string>());
        ProductionProfileValidationResult result = ProductionProfileValidator.Validate(FromNode(node), Context);
        result.Issues.ShouldContain(new ProductionProfileIssue(path, "evidence-subject-mismatch"));
    }

    [Theory]
    [InlineData("application", "$.applications[0].evidenceIds")]
    [InlineData("component", "$.components[7].evidenceIds")]
    [InlineData("release", "$.release.evidenceIds")]
    [InlineData("cli-compatibility", "$.release.cliCompatibilityEvidenceId")]
    [InlineData("broker", "$.environment.brokerEvidenceIds")]
    [InlineData("scheduler-reminders-and-jobs", "$.environment.schedulerPlacementEvidenceIds")]
    [InlineData("ad34", "$.catalogs.evidenceIds")]
    [InlineData("append-erasure", "$.protection.evidenceIds")]
    [InlineData("issuer", "$.issuer.evidenceIds")]
    [InlineData("freshness", "$.freshness.evidenceIds")]
    [InlineData("restore", "$.restore.evidenceIds")]
    public void EvidenceSemanticCoverageCannotBeReplacedByUnrelatedNonemptyCoverage(string category, string path)
    {
        JsonNode node = Node();
        RemoveValue(node["evidence"]![0]!["coverage"]!, category);
        ProductionProfileValidator.Validate(FromNode(node), Context).Issues.ShouldContain(new ProductionProfileIssue(path, "evidence-coverage-mismatch"));
    }

    [Fact]
    public void CallerInventoryCannotBeMissingEmptyDuplicateOrUnderDeclared()
    {
        ProductionProfile profile = Profile();
        foreach (ProductionProfileValidationContext? context in new ProductionProfileValidationContext?[]
        {
            null, new([], Context.ExpectedComponentIds), new(Context.ExpectedApplicationIds, []),
            new([.. Context.ExpectedApplicationIds, Context.ExpectedApplicationIds[0]], Context.ExpectedComponentIds),
            new([.. Context.ExpectedApplicationIds, "app:omitted"], Context.ExpectedComponentIds),
            new(Context.ExpectedApplicationIds, [.. Context.ExpectedComponentIds, "component:omitted"]),
        })
        {
            ProductionProfileValidator.Validate(profile, context).IsStructurallyValid.ShouldBeFalse();
            Should.Throw<ProductionProfileValidationException>(() => ProductionProfileCodec.Decode(FixtureBytes(), context!));
        }
    }

    [Fact]
    public void ExpectedDigestsAreComparedAndDecodedModelsDoNotRetainMutableCallerBuffers()
    {
        byte[] bytes = FixtureBytes();
        ProductionProfile profile = ProductionProfileCodec.Decode(bytes, Context);
        Array.Fill(bytes, (byte)' ');
        ProductionProfileCodec.Encode(profile, Context).ShouldBe(FixtureBytes());
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileCodec.Decode(bytes, Context));
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileCodec.Decode(FixtureBytes(), Context, new string('0', 64)))
            .Issues.Single().Code.ShouldBe("expected-resolved-digest-mismatch");
        Should.Throw<ProductionProfileValidationException>(() => ProductionProfileCodec.Decode(FixtureBytes(), Context, expectedInfrastructureDigest: new string('0', 64)))
            .Issues.Single().Code.ShouldBe("expected-infrastructure-digest-mismatch");
        ProductionProfile fresh = ProductionProfileCodec.Decode(FixtureBytes(), Context);
        ReferenceEquals(profile, fresh).ShouldBeFalse();
        ReferenceEquals(profile.Applications, fresh.Applications).ShouldBeFalse();
    }

    private static byte[] FixtureBytes() => File.ReadAllBytes(Path.Combine(FixtureDirectory, "SyntheticProfile.v1.json"));
    private static ProductionProfile Profile() => ProductionProfileCodec.Decode(FixtureBytes(), Context);
    private static JsonNode Node() => JsonNode.Parse(FixtureBytes())!;
    private static ProductionProfile FromNode(JsonNode node) => node.Deserialize<ProductionProfile>(Options)!;
    private static JsonNode Component(JsonNode node, string id) => node["components"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == id)!;
    private static JsonNode Application(JsonNode node, string id) => node["applications"]!.AsArray().Single(a => a!["id"]!.GetValue<string>() == id)!;
    private static JsonNode Credential(JsonNode node, string id) => node["environment"]!["credentials"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == id)!;
    private static JsonNode Provider(JsonNode node, string id) => node["infrastructure"]!["providers"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == id)!;

    private static void RemoveValue(JsonNode node, string value)
    {
        JsonArray values = node.AsArray();
        values.RemoveAt(Enumerable.Range(0, values.Count).Single(i => values[i]!.GetValue<string>() == value));
    }

    private static void ChangeSubject(JsonNode profile, JsonNode section, string field, string replacement)
    {
        string previous = section[field]!.GetValue<string>();
        section[field] = replacement;
        foreach (JsonNode? evidence in profile["evidence"]!.AsArray())
        {
            JsonArray subjects = evidence!["subjectDigests"]!.AsArray();
            for (int i = 0; i < subjects.Count; i++)
                if (subjects[i]!.GetValue<string>() == previous) subjects[i] = replacement;
        }
    }
}
