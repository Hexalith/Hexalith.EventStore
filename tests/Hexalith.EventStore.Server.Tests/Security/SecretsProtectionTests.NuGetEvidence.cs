using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

public sealed partial class SecretsProtectionTests
{
    /// <summary>Crosses the actual metadata shapes with supported text encodings.</summary>
    public static TheoryData<string, string, string> NuGetEvidenceMetadataCases
    {
        get
        {
            var cases = new TheoryData<string, string, string>();
            foreach (string package in new[]
            {
                "Azure.Security.KeyVault.Secrets", "Microsoft.Extensions.Configuration.UserSecrets",
                "Microsoft.AspNetCore.Authentication.BearerToken", "Microsoft.IdentityModel.JsonWebTokens",
                "Microsoft.IdentityModel.Tokens",
            })
            {
                foreach (string shape in new[] { "central", "package", "reference" })
                {
                    if (shape == "reference" && package is not ("Microsoft.Extensions.Configuration.UserSecrets"
                            or "Microsoft.AspNetCore.Authentication.BearerToken"))
                    {
                        continue;
                    }

                    foreach (string encoding in new[] { "utf8", "utf8-bom", "utf16-le", "utf16-be",
                        "utf16-le-no-bom", "utf16-be-no-bom", "malformed-utf8", "control-prefix" })
                    {
                        cases.Add(package, shape, encoding);
                    }
                }
            }

            return cases;
        }
    }

    /// <summary>Only exact package metadata survives canonical file ingestion; surrounding credentials do not.</summary>
    [Theory]
    [MemberData(nameof(NuGetEvidenceMetadataCases))]
    public void NuGetEvidenceMetadata_RequiresSchemaAndPreservesDetection(string package, string shape, string encoding)
    {
        const string path = "evidence/asset-bytes/metadata";
        string content = CreateNuGetEvidenceMetadata(package, shape).ToJsonString();
        WithEvidenceRepository(root =>
        {
            void Scan(string text, bool rejected, int? credentialLine = null)
            {
                WriteEvidenceFile(root, path, EncodeEvidenceText(text, encoding));
                ReadTrackedText(path, root).ShouldNotBeNull();
                string[] violations = ScanTrackedFile(path, root).ToArray();
                violations.Any().ShouldBe(rejected);
                if (credentialLine is not null)
                {
                    violations.ShouldContain(path + ":" + credentialLine);
                }
            }

            // Malformed/control-prefixed JSON cannot establish schema recognition.
            Scan(content, encoding is "malformed-utf8" or "control-prefix");
            Scan(content + " trailing-text", true);
            Scan(content.Replace("10.0.0.0", RandomSecret(), StringComparison.Ordinal), true);
            Scan(content.Replace(package, "database.password", StringComparison.Ordinal), true);
            Scan(new JsonObject { [package] = "10.0.0.0" }.ToJsonString(), true);
            if (shape == "central")
            {
                Scan(new JsonObject { ["wrapper"] = JsonNode.Parse(content) }.ToJsonString(), true);
            }
            Scan(content.Insert(1, "\n\"pass" + "word\":\"" + RandomSecret() + "\",\n"), true, 2);
            Scan(content.Insert(1, "\n\"endpoint\":\"https://user:" + Uri.EscapeDataString(RandomSecret()) + "@example.test/\",\n"), true, 2);

            JsonObject changed = CreateNuGetEvidenceMetadata(package, shape);
            if (shape == "central")
            {
                changed["project"]!["frameworks"]!["net10.0"]!["centralPackageVersion"] =
                    changed["project"]!["frameworks"]!["net10.0"]!["centralPackageVersions"]!.DeepClone();
                changed["project"]!["frameworks"]!["net10.0"]!.AsObject().Remove("centralPackageVersions");
                foreach (string mutation in new[] { "version", "targets", "libraries" })
                {
                    JsonObject invalidAsset = CreateNuGetEvidenceMetadata(package, shape);
                    invalidAsset[mutation] = "invalid";
                    Scan(invalidAsset.ToJsonString(), true);
                }
                JsonObject missingFramework = CreateNuGetEvidenceMetadata(package, shape);
                missingFramework["targets"] = new JsonObject { ["other"] = new JsonObject() };
                Scan(missingFramework.ToJsonString(), true);
            }
            else
            {
                changed["libraries"]!.AsObject().Remove(package + "/10.0.0.0");
            }

            Scan(changed.ToJsonString(), true);
            if (shape == "reference")
            {
                foreach (string mutation in new[] { "compile", "compileOnly", "sha512", "serviceable", "runtimeTarget" })
                {
                    changed = CreateNuGetEvidenceMetadata(package, shape);
                    if (mutation is "compile" or "compileOnly")
                    {
                        changed["targets"]!["net10.0"]![package + "/10.0.0.0"]!.AsObject().Remove(mutation);
                    }
                    else if (mutation == "runtimeTarget")
                    {
                        changed["runtimeTarget"]!["name"] = "other";
                    }
                    else
                    {
                        changed["libraries"]![package + "/10.0.0.0"]![mutation] = JsonValue.Create("invalid");
                    }

                    Scan(changed.ToJsonString(), true);
                }
            }
        });
    }

    /// <summary>Scans the preserved real asset bytes, with no filename or content-hash exemption.</summary>
    [Theory]
    [InlineData("oq8-correction-20261017", "01e4ea21f085f56721f17b47f8718c9037a1c0ed4c81b7a7e2fbca11cf35770a")]
    [InlineData("oq8-correction-20261017", "de9ac6ba4361a2de5d82ef00565764530cdbe39314bed45b076f2cceb93d5c30")]
    [InlineData("runtime-corrections-20261017", "01e4ea21f085f56721f17b47f8718c9037a1c0ed4c81b7a7e2fbca11cf35770a.json")]
    [InlineData("runtime-corrections-20261017", "0a566ae5b421fb192ebdad20b2ccdafcb55c601847e01cac8b0ddfe0bfcba1be.json")]
    public void NuGetEvidenceMetadata_ActualHistoricalAssetsRemainScanned(string directory, string asset)
    {
        string original = "_bmad-output/implementation-artifacts/evidence/story-6-4/" + directory + "/asset-bytes/" + asset;
        string content = ReadTrackedText(original).ShouldNotBeNull();
        WithEvidenceRepository(root =>
        {
            const string path = "docs/renamed-capture";
            WriteEvidenceFile(root, path, System.Text.Encoding.UTF8.GetBytes(content));
            ScanTrackedFile(path, root).ShouldBeEmpty();
            content = content.Insert(1, "\"pass" + "word\":\"" + RandomSecret() + "\",\n");
            WriteEvidenceFile(root, path, System.Text.Encoding.UTF8.GetBytes(content));
            ScanTrackedFile(path, root).ShouldBe([path + ":1"]);
        });
    }

    private static JsonObject CreateNuGetEvidenceMetadata(string package, string shape)
    {
        const string version = "10.0.0.0";
        var document = new JsonObject
        {
            ["version"] = 3,
            ["targets"] = new JsonObject { ["net10.0"] = new JsonObject() },
            ["libraries"] = new JsonObject(),
        };
        if (shape == "central")
        {
            document["project"] = new JsonObject
            {
                ["frameworks"] = new JsonObject
                {
                    ["net10.0"] = new JsonObject { ["centralPackageVersions"] = new JsonObject { [package] = version } },
                },
            };
            return document;
        }

        document["targets"]!["net10.0"] = new JsonObject
        {
            ["Sample.Package/1.0.0"] = new JsonObject { ["dependencies"] = new JsonObject { [package] = version } },
        };
        document["libraries"]!["Sample.Package/1.0.0"] = new JsonObject { ["type"] = "project" };
        document["libraries"]![package + "/" + version] = new JsonObject
        {
            ["type"] = "package",
            ["sha512"] = Convert.ToBase64String(new byte[64]),
        };
        if (shape == "reference")
        {
            document["runtimeTarget"] = new JsonObject { ["name"] = "net10.0" };
            document["libraries"]![package + "/" + version] = new JsonObject
            {
                ["type"] = "referenceassembly", ["sha512"] = string.Empty, ["serviceable"] = false,
            };
            document["targets"]!["net10.0"]![package + "/" + version] = new JsonObject
            {
                ["compileOnly"] = true,
                ["compile"] = new JsonObject { [package + ".dll"] = new JsonObject() },
            };
        }

        return document;
    }
}
