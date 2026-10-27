using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Queries;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Routing;

public class DevelopmentRouteCatalogBuilderTests
{
    [Theory]
    [InlineData("invalid-utf8")]
    [InlineData("unpaired-high")]
    [InlineData("unpaired-low")]
    public void ConstructionInputRejectsMalformedPropertyNamesWithoutParserDisclosure(string scenario)
    {
        byte[] valid = File.ReadAllBytes(FixturePath("CompleteInput.json"));
        DevelopmentRouteCatalogBuilder.Build(DevelopmentRouteCatalogBuilder.ReadInput(valid));
        RouteCatalogValidationException failure = Should.Throw<RouteCatalogValidationException>(() =>
            DevelopmentRouteCatalogBuilder.ReadInput(WithMalformedPropertyName(valid, scenario)));
        failure.Message.ShouldBe("Route catalog rejected: $: invalid-json-or-closed-schema");
        failure.InnerException.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{"EventStore:DomainServices:Registrations":{"*|counter|v1":{"AppId":"never-print-this"}}}""")]
    [InlineData("""{"EventStore":{"DomainServices:Registrations":{"*|counter|v1":{"AppId":"never-print-this"}}}}""")]
    [InlineData("""{"EventStore":{"DomainServices":{"Registrations:*|counter|v1:AppId":"never-print-this"}}}""")]
    [InlineData("""{"EventStore":{"DomainServices":{"Registrations":{"*|counter|v1:AppId":"never-print-this"}}}}""")]
    [InlineData("""{"EventStore:DomainServices:Registrations:*|counter|v1:AppId":"never-print-this"}""")]
    public void InspectionRejectsFlattenedPathsInsteadOfOmittingOrKeepingStaleBindings(string overlay)
    {
        byte[] valid = Encoding.UTF8.GetBytes(ConfigurationWithSelector("*|counter|v1"));
        DevelopmentCatalogInspection control = DevelopmentRouteCatalogBuilder.Inspect([valid]);
        control.Bindings.Single().AppId.ShouldBe("sample");
        control.InventoryLimit.ShouldContain("Flattened colon-delimited configuration property paths are unsupported");
        foreach (ReadOnlyMemory<byte>[] layers in new[]
        {
            new ReadOnlyMemory<byte>[] { Encoding.UTF8.GetBytes(overlay) },
            [valid, Encoding.UTF8.GetBytes(overlay)],
        })
        {
            RouteCatalogValidationException failure = Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.Inspect(layers));
            failure.Diagnostics.Single().ShouldBe(new RouteCatalogDiagnostic($"$.configuration[{layers.Length - 1}]", "unsupported-flattened-configuration-path"));
            failure.Message.ShouldNotContain("never-print-this");
            failure.InnerException.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData("billing--orders", true)]
    [InlineData("-billing", false)]
    [InlineData("billing-", false)]
    [InlineData("Billing", false)]
    [InlineData("billing/orders", false)]
    public void InspectionAppIdGrammarMatchesCatalog(string appId, bool valid)
        => AssertInspectionAppId(appId, valid);

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void InspectionAppIdLengthMatchesCatalog(int length, bool valid)
        => AssertInspectionAppId(new string('a', length), valid);

    [Fact]
    public void ExplicitFixturesBuildTheGoldenCatalogRepeatedly()
    {
        DevelopmentRouteCatalogInput input = ReadFixture();
        byte[] expected = File.ReadAllBytes(FixturePath("CanonicalCatalog.json"));
        RouteCatalogCodec.Encode(DevelopmentRouteCatalogBuilder.Build(input)).ShouldBe(expected);
        RouteCatalogCodec.Encode(DevelopmentRouteCatalogBuilder.Build(input)).ShouldBe(expected);
        RouteCatalogEnvelope result = DevelopmentRouteCatalogBuilder.Build(input);
        result.Routes.Single(r => r.RouteId == "counter-get").Method.ShouldBe(DaprDomainQueryInvoker.QueryMethodName);
        result.Routes.Single(r => r.RouteId == "counter-create").Method.ShouldBe("process");
    }

    [Theory]
    [InlineData("missing-idempotency")]
    [InlineData("missing-binding")]
    [InlineData("missing-declaration")]
    [InlineData("duplicate-declaration")]
    [InlineData("duplicate-binding")]
    [InlineData("missing-version")]
    [InlineData("null-declaration")]
    public void MissingOrAmbiguousInputsCannotProduceAnEnvelope(string scenario)
    {
        DevelopmentRouteCatalogInput valid = ReadFixture();
        DevelopmentRouteCatalogBuilder.Build(valid);
        DevelopmentRouteCatalogInput invalid = scenario switch
        {
            "missing-idempotency" => valid with { Idempotency = [] },
            "missing-binding" => valid with { Bindings = [] },
            "missing-declaration" => valid with { Declarations = [] },
            "duplicate-declaration" => valid with { Declarations = [.. valid.Declarations, valid.Declarations[0]] },
            "duplicate-binding" => valid with { Bindings = [.. valid.Bindings, valid.Bindings[0]] },
            "missing-version" => valid with { Declarations = [valid.Declarations[0] with { MessageIdVersion = null }, .. valid.Declarations.Skip(1)] },
            _ => valid with { Declarations = [null!] },
        };
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.Build(invalid));
    }

    [Fact]
    public void ClosedConstructionInputRejectsSecretFieldsDuplicateFieldsAndMissingBooleans()
    {
        string input = File.ReadAllText(FixturePath("CompleteInput.json"));
        DevelopmentRouteCatalogBuilder.ReadInput(Encoding.UTF8.GetBytes(input));
        foreach (string invalid in new[]
        {
            input.Insert(1, "\"secretValue\":\"never-print-this\","),
            input.Insert(1, "\"generation\":7,"),
            input.Replace("\"allowRuntimeOverrides\": false,", "", StringComparison.Ordinal),
        })
        {
            Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.ReadInput(Encoding.UTF8.GetBytes(invalid)))
                .Message.ShouldNotContain("never-print-this");
        }
    }

    [Theory]
    [InlineData("binding")]
    [InlineData("declaration")]
    [InlineData("descriptor")]
    public void ConstructionInputRejectsNestedDuplicatesBeforeCanonicalEncoding(string location)
    {
        string input = File.ReadAllText(FixturePath("CompleteInput.json"));
        DevelopmentRouteCatalogBuilder.Build(DevelopmentRouteCatalogBuilder.ReadInput(Encoding.UTF8.GetBytes(input)));
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.ReadInput(Encoding.UTF8.GetBytes(WithNestedDuplicate(input, location))))
            .Diagnostics.Single().Code.ShouldBe("duplicate-field");
    }

    [Theory]
    [InlineData("comments")]
    [InlineData("trailing-comma")]
    [InlineData("bom")]
    public void AppsettingsSyntaxIsNotAcceptedForConstructionInputs(string syntax)
    {
        string input = File.ReadAllText(FixturePath("CompleteInput.json"));
        string invalid = syntax switch
        {
            "comments" => input.Insert(1, "/* appsettings comment */"),
            "trailing-comma" => input.Insert(input.LastIndexOf('}'), ","),
            _ => "\uFEFF" + input,
        };
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.ReadInput(Encoding.UTF8.GetBytes(invalid)))
            .Diagnostics.Single().Code.ShouldBe("invalid-json-or-closed-schema");
    }

    [Fact]
    public void RealDevelopmentConfigurationReportsAllFourBindingsAndExplicitInventoryLimits()
    {
        DevelopmentCatalogInspection report = InspectRealConfiguration();
        report.Bindings.Count.ShouldBe(4);
        report.Bindings.Select(b => (b.Domain, b.AppId, b.TenantPattern)).ShouldBe(new[]
        {
            ("counter", "sample", "*"), ("greeting", "sample", "*"),
            ("global-administrators", "tenants", "system"), ("tenants", "tenants", "system"),
        });
        report.Bindings.ShouldAllBe(b => b.CommandMethod == "process" && b.QueryMethod == DaprDomainQueryInvoker.QueryMethodName && b.ContractVersion == "v1");
        report.MissingInputs.Count.ShouldBe(52);
        report.MissingInputs.Count(d => d.Code == "idempotency-adapter-and-operation").ShouldBe(4);
        report.MissingInputs.Count(d => d.Code == "platform-namespace-and-exact-human-permission").ShouldBe(2);
        report.InventoryLimit.ShouldContain("runtime parity remains unverified");
        report.InventoryLimit.ShouldContain("No environment");
        report.InventoryLimit.ShouldContain("adapter registrations");
    }

    [Fact]
    public void InspectionMergesSafeFieldsAndNeverSerializesKeyMaterial()
    {
        string baseLayer = """
            {"EventStore":{"DomainServices":{"Registrations":{"*|counter|v1":{"TenantId":"*","Domain":"counter","AppId":"old","MethodName":"custom-process","Version":"v1"}}},"Idempotency":{"DigestKeys":{"g1":"never-print-this"},"SecretValue":"never-print-this"}}}
            """;
        string overlay = """
            {"EventStore":{"DomainServices":{"Registrations":{"*|counter|v1":{"AppId":"sample"}}}}}
            """;
        DevelopmentCatalogInspection report = DevelopmentRouteCatalogBuilder.Inspect([Encoding.UTF8.GetBytes(baseLayer), Encoding.UTF8.GetBytes(overlay)]);
        report.Bindings.Single().AppId.ShouldBe("sample");
        report.Bindings.Single().CommandMethod.ShouldBe("custom-process");
        report.Bindings.Single().QueryMethod.ShouldBe("query");
        JsonSerializer.Serialize(report).ShouldNotContain("never-print-this");
    }

    [Fact]
    public void InspectionAcceptsAppsettingsSyntaxAndMergesCaseInsensitiveSectionsAndFields()
    {
        string first = """
            { // normal appsettings syntax
              "eventstore":{"domainservices":{"registrations":{"*|counter|v1":{
                "tenantid":"*","domain":"counter","appid":"old","methodname":"process","version":"v1",
              },},},},
            }
            """;
        string second = """
            {"EventSTORE":{"DomainSERVICES":{"RegisTRATIONS":{"*|counter|v1":{"AppID":"sample"}}}}}
            """;
        DevelopmentCatalogInspection report = DevelopmentRouteCatalogBuilder.Inspect([Encoding.UTF8.GetBytes("\uFEFF" + first), Encoding.UTF8.GetBytes(second)]);
        report.Bindings.Single().ShouldBe(new DevelopmentBindingInventory("*", "counter", "sample", "process", "query", "v1"));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("section")]
    [InlineData("selector")]
    [InlineData("field")]
    [InlineData("unrelated-secret")]
    public void InspectionRejectsDuplicateConfigurationPropertiesWithoutDisclosingValues(string location)
    {
        string valid = ConfigurationWithSelector("*|counter|v1");
        DevelopmentRouteCatalogBuilder.Inspect([Encoding.UTF8.GetBytes(valid)]).Bindings.Count.ShouldBe(1);
        string invalid = location switch
        {
            "root" => valid.Insert(1, "\"eventstore\":{},"),
            "section" => valid.Replace("\"DomainServices\":", "\"domainservices\":{},\"DomainServices\":", StringComparison.Ordinal),
            "selector" => valid.Replace("\"*|counter|v1\":", "\"*|COUNTER|V1\":{},\"*|counter|v1\":", StringComparison.Ordinal),
            "field" => valid.Replace("\"AppId\":", "\"appid\":\"never-print-this\",\"AppId\":", StringComparison.Ordinal),
            _ => valid.Insert(1, "\"Secrets\":{\"key\":\"never-print-this\",\"KEY\":\"never-print-this\"},"),
        };
        RouteCatalogValidationException exception = Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.Inspect([Encoding.UTF8.GetBytes(invalid)]));
        exception.Diagnostics.Single().Code.ShouldBe("duplicate-configuration-key");
        exception.Message.ShouldNotContain("never-print-this");
    }

    [Theory]
    [InlineData("*|counter|v1", "*", true)]
    [InlineData("tenant-a|counter|v1", "tenant-a", true)]
    [InlineData("wildcard_counter_v1", "*", true)]
    [InlineData("*|greeting|v1", "*", false)]
    [InlineData("wildcard_greeting_v1", "*", false)]
    [InlineData("*|counter|v2", "*", false)]
    [InlineData("tenant-a|counter|v1", "*", false)]
    [InlineData("wildcard_counter_v1", "tenant-a", false)]
    [InlineData("*|COUNTER|v1", "*", false)]
    [InlineData("WildCard_counter_v1", "*", false)]
    public void InspectionRequiresSupportedSelectorAndValueAgreement(string selector, string tenant, bool valid)
    {
        byte[] configuration = Encoding.UTF8.GetBytes(ConfigurationWithSelector(selector, tenant));
        if (valid)
        {
            DevelopmentRouteCatalogBuilder.Inspect([configuration]).Bindings.Single().Domain.ShouldBe("counter");
        }
        else
        {
            RouteCatalogValidationException exception = Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.Inspect([configuration]));
            exception.Diagnostics.Single().Code.ShouldBe("selector-value-mismatch-or-unsupported-selector");
            exception.Message.ShouldNotContain(selector);
        }
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void InspectionUsesRuntimeDomainLength(int length, bool valid)
    {
        string domain = new('a', length);
        byte[] configuration = Encoding.UTF8.GetBytes(ConfigurationWithSelector($"*|{domain}|v1", domain: domain));
        if (valid)
        {
            DevelopmentRouteCatalogBuilder.Inspect([configuration]).Bindings.Single().Domain.ShouldBe(domain);
        }
        else
        {
            Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.Inspect([configuration]))
                .Diagnostics.Single().Code.ShouldBe("missing-or-invalid-safe-binding");
        }
    }

    [Fact]
    public void InspectionAcceptsConsecutiveHyphensInRuntimeDomains()
    {
        byte[] configuration = Encoding.UTF8.GetBytes(ConfigurationWithSelector("wildcard_counter--domain_v1", domain: "counter--domain"));
        DevelopmentRouteCatalogBuilder.Inspect([configuration]).Bindings.Single().Domain.ShouldBe("counter--domain");
    }

    [Fact]
    public async Task FileToolGeneratesAndValidatesFixturesAndRefusesIncompleteInputs()
    {
        string directory = Path.Combine(Path.GetTempPath(), "route-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string tool = await BuildToolAsync(Path.Combine(directory, "artifacts"));
            string output = Path.Combine(directory, "catalog.json");
            (int exit, string log) = await RunToolAsync(tool, "generate", FixturePath("CompleteInput.json"), output);
            exit.ShouldBe(0, log);
            byte[] expected = File.ReadAllBytes(FixturePath("CanonicalCatalog.json"));
            File.ReadAllBytes(output).ShouldBe(expected);
            string root = RouteCatalogCodec.ComputeRootDigest(RouteCatalogCodec.Decode(expected));
            log.ShouldContain(root);
            (exit, log) = await RunToolAsync(tool, "validate", output, root);
            exit.ShouldBe(0, log);
            (exit, log) = await RunToolAsync(tool, "validate", output, new string('0', 64));
            exit.ShouldBe(2, log);
            log.ShouldContain("root-digest-mismatch");

            string incomplete = Path.Combine(directory, "incomplete.json");
            File.WriteAllText(incomplete, JsonSerializer.Serialize(ReadFixture() with { Idempotency = [] }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            string absent = Path.Combine(directory, "must-not-exist.json");
            (exit, log) = await RunToolAsync(tool, "generate", incomplete, absent);
            exit.ShouldBe(2, log);
            File.Exists(absent).ShouldBeFalse();
            (exit, log) = await RunToolAsync(tool, "generate", incomplete, output);
            exit.ShouldBe(2, log);
            File.ReadAllBytes(output).ShouldBe(expected);

            // Real Development construction binds only its bounded retained configuration bytes.
            string profile = Path.Combine(directory, "development-profile");
            string settings = Path.Combine(profile, "src", "Hexalith.EventStore");
            Directory.CreateDirectory(settings);
            string baseSettings = Path.Combine(settings, "appsettings.json");
            string developmentSettings = Path.Combine(settings, "appsettings.Development.json");
            File.Copy(SettingsPath("appsettings.json"), baseSettings);
            File.Copy(SettingsPath("appsettings.Development.json"), developmentSettings);
            string developmentOutput = Path.Combine(directory, "development.json");
            (exit, log) = await RunToolAsync(tool, "development", profile, developmentOutput);
            exit.ShouldBe(2, log);
            log.Trim().ShouldBe("Route catalog rejected: $.developmentProfile: unrepresented-unbounded-query-fallback");
            File.Exists(developmentOutput).ShouldBeFalse();
            byte[] developmentExpected = File.ReadAllBytes(Path.Combine(FindRoot(), "deploy", "dapr", "eventstore-routing-catalog.json"));
            File.WriteAllBytes(developmentOutput, developmentExpected);
            foreach (string mode in new[] { "development", "check-development" })
            {
                (exit, log) = await RunToolAsync(tool, mode, profile, developmentOutput);
                exit.ShouldBe(2, log);
                log.Trim().ShouldBe("Route catalog rejected: $.developmentProfile: unrepresented-unbounded-query-fallback");
                File.ReadAllBytes(developmentOutput).ShouldBe(developmentExpected);
            }
            foreach (string source in new[] { baseSettings, developmentSettings })
            {
                byte[] retained = File.ReadAllBytes(source);
                foreach (string failure in new[] { "conversion", "oversized" })
                {
                    if (failure == "conversion")
                    {
                        var configuration = System.Text.Json.Nodes.JsonNode.Parse(retained)!;
                        configuration["EventStore"]!["DomainServices"]!["InvocationTimeoutSeconds"] = "never-print-this-conversion-value";
                        File.WriteAllText(source, configuration.ToJsonString());
                    }
                    else
                    {
                        using var oversizedSettings = new FileStream(source, FileMode.Create, FileAccess.Write);
                        oversizedSettings.SetLength(RouteCatalogCodec.MaximumByteLength + 1L);
                    }
                    foreach (string mode in new[] { "development", "check-development" })
                    {
                        (exit, log) = await RunToolAsync(tool, mode, profile, developmentOutput);
                        exit.ShouldBe(2, log);
                        log.Trim().ShouldBe(failure == "conversion"
                            ? "Route catalog rejected: $.developmentProfile.configuration: invalid-configuration"
                            : "Route catalog rejected: $.developmentProfile: size-limit");
                        log.ShouldNotContain("never-print-this-conversion-value");
                        File.ReadAllBytes(developmentOutput).ShouldBe(developmentExpected);
                        Directory.GetFiles(directory, ".routing-catalog-*.tmp").ShouldBeEmpty();
                    }
                    File.WriteAllBytes(source, retained);
                }
            }
            (exit, log) = await RunToolAsync(tool, "check-development", profile, developmentOutput);
            exit.ShouldBe(2, log);
            log.Trim().ShouldBe("Route catalog rejected: $.developmentProfile: unrepresented-unbounded-query-fallback");
            File.ReadAllBytes(developmentOutput).ShouldBe(developmentExpected);

            // Every file argument rejects empty/blank paths with a bounded error and preserves existing output.
            foreach (string invalidPath in new[] { string.Empty, " ", "\t" })
            {
                foreach (string[] arguments in new[]
                {
                    new[] { "generate", invalidPath, absent },
                    ["generate", invalidPath, output],
                    ["generate", FixturePath("CompleteInput.json"), invalidPath],
                    ["validate", invalidPath],
                    ["inspect", invalidPath, SettingsPath("appsettings.Development.json")],
                    ["inspect", SettingsPath("appsettings.json"), invalidPath],
                })
                {
                    (exit, log) = await RunToolAsync(tool, arguments);
                    exit.ShouldBe(3, log);
                    log.Trim().ShouldBe("Route catalog file access failed.");
                    File.Exists(absent).ShouldBeFalse();
                    File.ReadAllBytes(output).ShouldBe(expected);
                    Directory.GetFiles(directory, ".routing-catalog-*.tmp").ShouldBeEmpty();
                }
            }

            string duplicate = Path.Combine(directory, "duplicate.json");
            File.WriteAllText(duplicate, WithNestedDuplicate(File.ReadAllText(FixturePath("CompleteInput.json")), "descriptor"));
            foreach (string destination in new[] { absent, output })
            {
                (exit, log) = await RunToolAsync(tool, "generate", duplicate, destination);
                exit.ShouldBe(2, log);
                log.ShouldContain("duplicate-field");
                File.Exists(absent).ShouldBeFalse();
                File.ReadAllBytes(output).ShouldBe(expected);
            }

            foreach (string scenario in new[] { "invalid-utf8", "unpaired-high", "unpaired-low" })
            {
                string invalidInput = Path.Combine(directory, "invalid-input.json");
                string invalidCatalog = Path.Combine(directory, "invalid-catalog.json");
                File.WriteAllBytes(invalidInput, WithMalformedPropertyName(File.ReadAllBytes(FixturePath("CompleteInput.json")), scenario));
                File.WriteAllBytes(invalidCatalog, WithMalformedPropertyName(expected, scenario));
                foreach (string[] arguments in new[]
                {
                    new[] { "generate", invalidInput, absent },
                    ["generate", invalidInput, output],
                    ["validate", invalidCatalog],
                })
                {
                    (exit, log) = await RunToolAsync(tool, arguments);
                    exit.ShouldBe(2, log);
                    log.Trim().ShouldBe("Route catalog rejected: $: invalid-json-or-closed-schema");
                    File.Exists(absent).ShouldBeFalse();
                    File.ReadAllBytes(output).ShouldBe(expected);
                    Directory.GetFiles(directory, ".routing-catalog-*.tmp").ShouldBeEmpty();
                }
            }

            // The input guard accepts this valid document; only the expanded encoded envelope exceeds 4 MiB.
            byte[] largeInputBytes = JsonSerializer.SerializeToUtf8Bytes(LargeQueryInput(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            largeInputBytes.Length.ShouldBeLessThan(RouteCatalogCodec.MaximumByteLength);
            RouteCatalogEnvelope largeCatalog = DevelopmentRouteCatalogBuilder.Build(DevelopmentRouteCatalogBuilder.ReadInput(largeInputBytes));
            largeCatalog.Routes.Count.ShouldBe(3500);
            RouteCatalogValidator.Validate(largeCatalog);
            JsonSerializer.SerializeToUtf8Bytes(largeCatalog).Length.ShouldBeGreaterThan(RouteCatalogCodec.MaximumByteLength);
            Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Encode(largeCatalog)).Diagnostics.Single()
                .ShouldBe(new RouteCatalogDiagnostic("$", "size-limit"));
            string largeInput = Path.Combine(directory, "large-input.json");
            File.WriteAllBytes(largeInput, largeInputBytes);
            foreach (string destination in new[] { absent, output })
            {
                (exit, log) = await RunToolAsync(tool, "generate", largeInput, destination);
                exit.ShouldBe(2, log);
                log.Trim().ShouldBe("Route catalog rejected: $: size-limit");
                File.Exists(absent).ShouldBeFalse();
                File.ReadAllBytes(output).ShouldBe(expected);
                Directory.GetFiles(directory, ".routing-catalog-*.tmp").ShouldBeEmpty();
            }

            string oversized = Path.Combine(directory, "oversized.json");
            using (FileStream file = File.Create(oversized))
            {
                file.SetLength(RouteCatalogCodec.MaximumByteLength + 1L);
            }

            foreach (string[] arguments in new[]
            {
                new[] { "generate", oversized, absent },
                ["generate", oversized, output],
                ["validate", oversized],
                ["inspect", oversized, SettingsPath("appsettings.Development.json")],
                ["inspect", SettingsPath("appsettings.json"), oversized],
            })
            {
                (exit, log) = await RunToolAsync(tool, arguments);
                exit.ShouldBe(2, log);
                log.ShouldContain("size-limit");
                File.Exists(absent).ShouldBeFalse();
                File.ReadAllBytes(output).ShouldBe(expected);
            }

            // Failed replacement also cleans up the completed temporary sibling.
            string blocked = Path.Combine(directory, "blocked");
            Directory.CreateDirectory(blocked);
            File.WriteAllBytes(Path.Combine(blocked, "catalog.json"), expected);
            (exit, log) = await RunToolAsync(tool, "generate", FixturePath("CompleteInput.json"), blocked);
            exit.ShouldBe(3, log);
            File.ReadAllBytes(Path.Combine(blocked, "catalog.json")).ShouldBe(expected);
            Directory.GetFiles(directory, ".routing-catalog-*.tmp").ShouldBeEmpty();

            // Existing readers retain their original bytes while the path acquires the completed catalog.
            byte[] sentinel = Encoding.UTF8.GetBytes("previous-catalog-sentinel");
            File.WriteAllBytes(output, sentinel);
            using (var previous = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                (exit, log) = await RunToolAsync(tool, "generate", FixturePath("CompleteInput.json"), output);
                exit.ShouldBe(0, log);
                File.ReadAllBytes(output).ShouldBe(expected);
                using var retained = new MemoryStream();
                previous.CopyTo(retained);
                retained.ToArray().ShouldBe(sentinel);
            }

            Directory.GetFiles(directory, ".routing-catalog-*.tmp").ShouldBeEmpty();

            (exit, log) = await RunToolAsync(tool, "inspect", SettingsPath("appsettings.json"), SettingsPath("appsettings.Development.json"));
            exit.ShouldBe(2, log);
            log.ShouldContain("idempotency-adapter-and-operation");
            log.ShouldContain("runtime parity remains unverified");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DevelopmentRouteCatalogInput ReadFixture() => DevelopmentRouteCatalogBuilder.ReadInput(File.ReadAllBytes(FixturePath("CompleteInput.json")));

    private static byte[] WithMalformedPropertyName(byte[] document, string scenario)
    {
        byte[] name = scenario == "invalid-utf8" ? [0xff] : Encoding.UTF8.GetBytes(scenario == "unpaired-high" ? "\\uD800" : "\\uDC00");
        return [.. "{\"never-print-this"u8, .. name, .. "\":null,"u8, .. document[1..]];
    }

    private static DevelopmentRouteCatalogInput LargeQueryInput()
    {
        RouteContractDeclaration template = ReadFixture().Declarations.Single(d => d.RouteId == "counter-get");
        RouteContractDeclaration[] declarations = Enumerable.Range(0, 3500).Select(index =>
        {
            string name = new string('a', 121) + index.ToString("D7", System.Globalization.CultureInfo.InvariantCulture);
            return template with { RouteId = name, MessageType = name, Domain = new string('d', 64), ProjectionType = new string('p', 128) };
        }).ToArray();
        RouteCompositionBinding[] bindings = declarations.Select(d => new RouteCompositionBinding(
            d.RouteId, new string('a', 80), new string('m', 64), d.ContractVersion, false)).ToArray();
        return new(7, false, declarations, bindings, []);
    }

    private static void AssertInspectionAppId(string appId, bool valid)
    {
        string control = ConfigurationWithSelector("*|counter|v1");
        DevelopmentRouteCatalogBuilder.Inspect([Encoding.UTF8.GetBytes(control)]);
        byte[] configuration = Encoding.UTF8.GetBytes(control.Replace("\"AppId\":\"sample\"", $"\"AppId\":\"{appId}\"", StringComparison.Ordinal));
        if (valid)
        {
            DevelopmentRouteCatalogBuilder.Inspect([configuration]).Bindings.Single().AppId.ShouldBe(appId);
        }
        else
        {
            Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.Inspect([configuration])).Diagnostics.Single()
                .ShouldBe(new RouteCatalogDiagnostic("$.bindings[0].AppId", "missing-or-invalid-safe-binding"));
        }
    }

    private static string WithNestedDuplicate(string input, string location)
    {
        using JsonDocument document = JsonDocument.Parse(input);
        (JsonElement target, string property) = location switch
        {
            "binding" => (document.RootElement.GetProperty("bindings")[0], "appId"),
            "declaration" => (document.RootElement.GetProperty("declarations")[0], "domain"),
            _ => (document.RootElement.GetProperty("idempotency")[0].GetProperty("descriptorSchema"), "schemaId"),
        };
        string original = target.GetRawText();
        string duplicate = original.Insert(1, $"\"{property}\":{target.GetProperty(property).GetRawText()},");
        return input.Replace(original, duplicate, StringComparison.Ordinal);
    }

    private static string ConfigurationWithSelector(string selector, string tenant = "*", string domain = "counter") =>
        "{\"EventStore\":{\"DomainServices\":{\"Registrations\":{\"" + selector
        + "\":{\"TenantId\":\"" + tenant + "\",\"Domain\":\"" + domain + "\",\"AppId\":\"sample\",\"MethodName\":\"process\",\"Version\":\"v1\"}}}}}";

    private static DevelopmentCatalogInspection InspectRealConfiguration() => DevelopmentRouteCatalogBuilder.Inspect(
        [File.ReadAllBytes(SettingsPath("appsettings.json")), File.ReadAllBytes(SettingsPath("appsettings.Development.json"))]);

    private static string FixturePath(string name) => Path.Combine(FindRoot(), "tests", "Hexalith.EventStore.Contracts.Tests", "Routing", "Fixtures", name);

    private static string SettingsPath(string name) => Path.Combine(FindRoot(), "src", "Hexalith.EventStore", name);

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    private static async Task<string> BuildToolAsync(string artifacts)
    {
        var arguments = new List<string> { "build", "tools/routing-catalog.cs", "--artifacts-path", artifacts, "-v", "quiet" };
#if DEBUG
        arguments.Add("-c=Debug");
        arguments.Add("-p:UseHexalithProjectReferences=true");
#else
        arguments.Add("-c=Release");
        arguments.Add("-p:UseHexalithProjectReferences=false");
#endif
        (int exit, string log) = await RunProcessAsync("dotnet", arguments);
        exit.ShouldBe(0, log);
        return Directory.GetFiles(Path.Combine(artifacts, "bin"), "routing-catalog.dll", SearchOption.AllDirectories).Single();
    }

    private static Task<(int Exit, string Log)> RunToolAsync(string tool, params string[] arguments)
        => RunProcessAsync("dotnet", [tool, .. arguments]);

    private static async Task<(int Exit, string Log)> RunProcessAsync(string executable, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = FindRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await stdout + await stderr);
    }
}
