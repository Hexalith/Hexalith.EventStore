using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Contracts.Tests.Routing;

public class RouteCatalogValidatorTests
{
    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void DomainLengthMatchesRuntimeDeclarationsAndIdempotencyMetadata(int length, bool valid)
        => AssertDomain(new string('a', length), valid);

    [Theory]
    [InlineData("billing--orders", true)]
    [InlineData("-billing", false)]
    [InlineData("billing-", false)]
    [InlineData("Billing", false)]
    public void DomainSyntaxMatchesRuntimeDeclarationsAndIdempotencyMetadata(string domain, bool valid)
        => AssertDomain(domain, valid);

    [Fact]
    public void DomainGrammarDoesNotRelaxOtherIdentifiers()
    {
        RouteContractDeclaration declaration = RouteCatalogCodec.Decode(RouteCatalogCodecTests.FixtureBytes()).Declarations[0];
        RouteCatalogValidator.ValidateDeclaration(declaration with { Domain = "billing--orders" });
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogValidator.ValidateDeclaration(declaration with { RouteId = "billing--orders" }))
            .Diagnostics.Single().Code.ShouldBe("missing-or-invalid-name");
    }

    [Theory]
    [InlineData("duplicate-id", "duplicate-route-id")]
    [InlineData("duplicate-key", "duplicate-route-key")]
    [InlineData("missing-declaration", "missing-declaration")]
    [InlineData("missing-binding", "missing-binding")]
    [InlineData("missing-idempotency", "missing-command-idempotency")]
    [InlineData("wrong-command", "command-join-mismatch")]
    [InlineData("query-idempotency", "invalid-command-join")]
    [InlineData("duplicate-idempotency", "duplicate-idempotency-entry")]
    [InlineData("route-generation", "generation-mismatch")]
    [InlineData("policy-generation", "generation-mismatch")]
    [InlineData("declaration-digest", "declaration-digest-mismatch")]
    [InlineData("descriptor-digest", "descriptor-digest-mismatch")]
    [InlineData("entry-content-digest", "idempotency-content-digest-mismatch")]
    [InlineData("entry-policy-tamper", "idempotency-content-digest-mismatch")]
    [InlineData("route-facet", "digest-mismatch")]
    [InlineData("idempotency-facet", "digest-mismatch")]
    [InlineData("missing-message-version", "invalid-message-id-version")]
    [InlineData("unknown-message-version", "invalid-message-id-version")]
    [InlineData("target-credential", "invalid-target-credential")]
    [InlineData("target-operation", "invalid-target-operation")]
    [InlineData("entry-operation", "declaration-mismatch")]
    [InlineData("target-version", "declaration-mismatch")]
    [InlineData("platform-tenant-grammar", "invalid-platform-namespace")]
    [InlineData("missing-human-permission", "missing-or-invalid-human-permission")]
    [InlineData("wildcard-human-permission", "missing-or-invalid-human-permission")]
    [InlineData("workload-human-permission", "workload-permission-is-not-human-admission")]
    [InlineData("missing-adapter", "missing-or-invalid-token")]
    [InlineData("missing-policy", "missing-or-invalid-token")]
    [InlineData("missing-retention", "invalid-retention-tier")]
    [InlineData("missing-readers", "missing-or-invalid-list")]
    [InlineData("active-not-reader", "active-generation-not-readable")]
    [InlineData("duplicate-generation", "duplicate-generation")]
    [InlineData("missing-key-map", "reader-generation-map-mismatch")]
    [InlineData("duplicate-key-map", "duplicate-key-reference")]
    [InlineData("missing-consumer", "missing-or-invalid-name")]
    [InlineData("missing-map-generation", "missing-or-invalid-token")]
    [InlineData("unbounded-fallback", "undeclared-or-unbounded-fallback")]
    [InlineData("ambiguous-fallback", "ambiguous-fallback")]
    [InlineData("production-override", "override-outside-development")]
    [InlineData("undeclared-override", "undeclared-override")]
    [InlineData("missing-target", "missing-or-invalid-name")]
    [InlineData("invalid-method", "invalid-method")]
    [InlineData("missing-schema", "missing-descriptor-schema")]
    [InlineData("duplicate-schema-field", "duplicate-descriptor-field")]
    [InlineData("unknown-schema-type", "unsupported-descriptor-type")]
    [InlineData("missing-secret-store", "missing-or-invalid-name")]
    [InlineData("command-fallback", "command-requires-exact-declaration")]
    [InlineData("zero-generation", "invalid-generation")]
    [InlineData("null-declarations", "missing-or-invalid-list")]
    public void InvalidSemanticsFailBesidePassingControl(string scenario, string code)
    {
        RouteCatalogEnvelope valid = RouteCatalogCodec.Decode(RouteCatalogCodecTests.FixtureBytes());
        RouteCatalogValidator.Validate(valid);
        var declarations = valid.Declarations.ToArray();
        var routes = valid.Routes.ToArray();
        var policies = valid.Idempotency.ToArray();
        RouteCatalogEnvelope candidate = valid;
        switch (scenario)
        {
            case "duplicate-id": declarations[1] = declarations[0]; break;
            case "duplicate-key": declarations[1] = declarations[0] with { RouteId = "duplicate" }; break;
            case "missing-declaration": routes[0] = routes[0] with { RouteId = "unknown" }; break;
            case "missing-binding": routes = routes[1..]; break;
            case "missing-idempotency": policies = []; break;
            case "wrong-command": policies[0] = policies[0] with { CommandType = "wrong-command" }; break;
            case "query-idempotency": policies[0] = policies[0] with { RouteId = "counter-get" }; break;
            case "duplicate-idempotency": policies = [policies[0], policies[0]]; break;
            case "route-generation": routes[0] = routes[0] with { Generation = 8 }; break;
            case "policy-generation": policies[0] = policies[0] with { Generation = 8 }; break;
            case "declaration-digest": routes[0] = routes[0] with { DeclarationDigest = new string('0', 64) }; break;
            case "entry-content-digest": policies[0] = policies[0] with { ContentDigest = new string('0', 64) }; break;
            case "entry-policy-tamper": policies[0] = policies[0] with { PolicyVersion = "changed-owner-policy" }; break;
            case "descriptor-digest": policies[0] = policies[0] with { DescriptorDigest = new string('0', 64) }; break;
            case "route-facet": candidate = candidate with { RouteFacetDigest = new string('0', 64) }; break;
            case "idempotency-facet": candidate = candidate with { IdempotencyFacetDigest = new string('0', 64) }; break;
            case "missing-message-version": declarations[0] = declarations[0] with { MessageIdVersion = null }; break;
            case "unknown-message-version": declarations[0] = declarations[0] with { MessageIdVersion = 3 }; break;
            case "target-credential": declarations[0] = declarations[0] with { CredentialKind = "delegated-user" }; break;
            case "target-operation": declarations[0] = declarations[0] with { Operation = "eventstore:command-submit" }; break;
            case "entry-operation": routes[0] = routes[0] with { Operation = "domain-service:query" }; break;
            case "target-version": routes[0] = routes[0] with { ContractVersion = "v2" }; break;
            case "platform-tenant-grammar": declarations[3] = declarations[3] with { PlatformAdmission = new("system", "tenants:create") }; break;
            case "missing-human-permission": declarations[3] = declarations[3] with { PlatformAdmission = new("platform:tenants", "") }; break;
            case "wildcard-human-permission": declarations[3] = declarations[3] with { PlatformAdmission = new("platform:tenants", "tenants:*") }; break;
            case "workload-human-permission": declarations[3] = declarations[3] with { PlatformAdmission = new("platform:tenants", "domain-service:process") }; break;
            case "missing-adapter": policies[0] = policies[0] with { AdapterId = "" }; break;
            case "missing-policy": policies[0] = policies[0] with { PolicyVersion = "" }; break;
            case "missing-retention": policies[0] = policies[0] with { RetentionTier = "" }; break;
            case "missing-readers": policies[0] = policies[0] with { ReaderDigestKeyGenerations = [] }; break;
            case "active-not-reader": policies[0] = policies[0] with { ActiveDigestKeyGeneration = "g3" }; break;
            case "duplicate-generation": policies[0] = policies[0] with { ReaderDigestKeyGenerations = ["g2", "g2"] }; break;
            case "missing-key-map": policies[0] = policies[0] with { KeyMap = [policies[0].KeyMap[0]] }; break;
            case "duplicate-key-map": policies[0] = policies[0] with { KeyMap = [policies[0].KeyMap[0], policies[0].KeyMap[0]] }; break;
            case "missing-consumer": policies[0] = policies[0] with { ConsumerAppId = "" }; break;
            case "missing-map-generation": policies[0] = policies[0] with { SecretMapGeneration = "" }; break;
            case "unbounded-fallback": declarations[4] = declarations[4] with { FallbackTypes = [] }; break;
            case "ambiguous-fallback": declarations = [.. declarations, declarations[4] with { RouteId = "overlap" }]; break;
            case "production-override": candidate = candidate with { Profile = "Production", AllowRuntimeOverrides = true }; break;
            case "undeclared-override": routes[0] = routes[0] with { IsRuntimeOverride = true }; break;
            case "missing-target": routes[0] = routes[0] with { AppId = "" }; break;
            case "invalid-method": routes[0] = routes[0] with { Method = "../query" }; break;
            case "missing-schema": policies[0] = policies[0] with { DescriptorSchema = null! }; break;
            case "duplicate-schema-field": policies[0] = policies[0] with { DescriptorSchema = policies[0].DescriptorSchema with { Fields = [policies[0].DescriptorSchema.Fields[0], policies[0].DescriptorSchema.Fields[0]] } }; break;
            case "unknown-schema-type": policies[0] = policies[0] with { DescriptorSchema = policies[0].DescriptorSchema with { Fields = [new("amount", "unknown")] } }; break;
            case "missing-secret-store": policies[0] = policies[0] with { SecretStore = "" }; break;
            case "command-fallback": declarations[0] = declarations[0] with { MessageType = "*", FallbackTypes = ["create-counter"] }; break;
            case "zero-generation": candidate = candidate with { Generation = 0 }; break;
            case "null-declarations": declarations = null!; break;
        }

        candidate = candidate with { Declarations = declarations, Routes = routes, Idempotency = policies };
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogValidator.Validate(candidate)).Diagnostics.Single().Code.ShouldBe(code);
    }

    [Fact]
    public void ExplicitDevelopmentOverrideAndLegacyMessageVersionAreAccepted()
    {
        RouteCatalogEnvelope valid = RouteCatalogCodec.Decode(RouteCatalogCodecTests.FixtureBytes());
        var declarations = valid.Declarations.ToArray();
        declarations[0] = declarations[0] with { MessageIdVersion = 1 };
        var routes = valid.Routes.ToArray();
        routes[0] = routes[0] with { DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(declarations[0]), IsRuntimeOverride = true };
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Seal(valid with { AllowRuntimeOverrides = true, Declarations = declarations, Routes = routes });
        RouteCatalogValidator.Validate(catalog);
    }

    private static void AssertDomain(string domain, bool valid)
    {
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Decode(RouteCatalogCodecTests.FixtureBytes());
        RouteContractDeclaration declaration = catalog.Declarations[0] with { Domain = domain };
        IdempotencyCatalogEntry entry = catalog.Idempotency[0] with { Domain = domain };
        if (valid)
        {
            RouteCatalogCodec.ComputeDeclarationDigest(declaration).Length.ShouldBe(64);
            RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry).Length.ShouldBe(64);
        }
        else
        {
            Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.ComputeDeclarationDigest(declaration))
                .Diagnostics.Single().Code.ShouldBe("missing-or-invalid-domain");
            Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry))
                .Diagnostics.Single().Code.ShouldBe("missing-or-invalid-domain");
        }
    }
}
