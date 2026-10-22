using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Exercises the five explicitly approved, content-bound historical retirements.</summary>
public sealed partial class SecretsProtectionTests
{
    private const string Story64RetirementDecision =
        "_bmad-output/implementation-artifacts/evidence/6-4-historical-retirement-20261018/retirement-decision.json";

    private const string Story64RetirementDecisionSha256 =
        "e63af04857d0b4d72e5234f17e9014aae971efcab7c7ee83bbcc45bd56679d7d";

    // Explicit interactive owner selection of the exact proposal on 2026-10-18.
    // A registry edit cannot enroll another path or re-pin any approved bytes.
    private static readonly IReadOnlyDictionary<string, string> ApprovedStory64RetirementHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["_bmad-output/implementation-artifacts/evidence/story-6-4/qualification-20261017-server-debug-full.xml"] =
                "0df8c78dc9689629c6804de235d8a423674eb1847c5143efc61250efa9961501",
            ["_bmad-output/implementation-artifacts/evidence/story-6-4/qualification-20261017-server-release-full.xml"] =
                "ec25a4ad7cd82f1ce095fe7045dbf0636006d275f5817ff422146c39d1a5a2f6",
            ["docs/evidence/story-3-6/20261017t144520z-production-authority-review/pre-review-sources/tests/Hexalith.EventStore.Server.Tests/Authentication/WorkloadBindingAuthorityTests.cs"] =
                "4ca5ee9bdecde7f111221902179dea749019df0e0455deb99edfb089d338286f",
            ["docs/evidence/story-3-6/20261017t144520z-production-authority-review/reviewed-final-sources/tests/Hexalith.EventStore.Server.Tests/Authentication/WorkloadBindingAuthorityTests.cs"] =
                "b9aac41d52cda59e20dc4988941d90ed0b533cd736b489869da8e89bcc9d42e6",
            ["docs/evidence/story-3-6/20261017t144520z-production-authority-review/reviewed-sources/tests/Hexalith.EventStore.Server.Tests/Authentication/WorkloadBindingAuthorityTests.cs"] =
                "f02dae33ff36576bdc9c08e07081010cdd8fa44045c56ac020713e3458929f88",
        };

    /// <summary>The finite owner-approved paths; neighboring files remain ordinary scanned content.</summary>
    public static TheoryData<string> Story64RetiredPaths
    {
        get
        {
            var cases = new TheoryData<string>();
            foreach (string path in ApprovedStory64RetirementHashes.Keys) cases.Add(path);
            return cases;
        }
    }

    /// <summary>Each retirement is consumed by the canonical reader and depends on registry enrollment.</summary>
    [Theory]
    [MemberData(nameof(Story64RetiredPaths))]
    public void Story64Retirement_RequiresExactOriginalAndRegistry(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        WithEvidenceRepository(root =>
        {
            CopyStory64Retirement(root, path);
            ReadTrackedText(path, root).ShouldBeNull();
            ScanTrackedFile(path, root).ShouldBeEmpty();
            JsonObject manifest = ReadStory64Registry(root);
            JsonArray entries = manifest["retired"]!.AsArray();
            entries.Remove(entries.Single(entry => entry!["path"]!.GetValue<string>() == path));
            WriteEvidenceFile(root, RetiredEvidenceManifest, Encoding.UTF8.GetBytes(manifest.ToJsonString()));
            ScanTrackedFile(path, root).ShouldNotBeEmpty("Absent enrollment restores ordinary credential detection.");
        });
    }

    /// <summary>Byte changes cannot be hidden by re-pinning the mutable retirement registry.</summary>
    [Theory]
    [MemberData(nameof(Story64RetiredPaths))]
    public void Story64Retirement_ChangedOrMissingOriginalFailsClosed(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        WithEvidenceRepository(root =>
        {
            CopyStory64Retirement(root, path);
            byte[] changed = [.. File.ReadAllBytes(Path.Combine(root, path)), .. Encoding.UTF8.GetBytes("\n" + Assignment("pass" + "word", JsonSerializer.Serialize(RandomSecret())))];
            WriteEvidenceFile(root, path, changed);
            Should.Throw<ShouldAssertException>(() => ScanTrackedFile(path, root).ToArray());
            JsonObject manifest = ReadStory64Registry(root);
            JsonNode entry = manifest["retired"]!.AsArray().Single(value => value!["path"]!.GetValue<string>() == path)!;
            entry["sha256"] = Convert.ToHexString(SHA256.HashData(changed)).ToLowerInvariant();
            WriteEvidenceFile(root, RetiredEvidenceManifest, Encoding.UTF8.GetBytes(manifest.ToJsonString()));
            Should.Throw<ShouldAssertException>(() => ScanTrackedFile(path, root).ToArray());
            CopyStory64Retirement(root, path);
            File.Delete(Path.Combine(root, path));
            Should.Throw<FileNotFoundException>(() => ScanTrackedFile(path, root).ToArray());
        });
    }

    /// <summary>Neither similar paths nor forged registry entries gain a directory-wide exclusion.</summary>
    [Theory]
    [MemberData(nameof(Story64RetiredPaths))]
    public void Story64Retirement_NeighboringPathsRemainScanned(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        WithEvidenceRepository(root =>
        {
            CopyStory64Retirement(root, path);
            foreach (string neighbor in new[] { path + ".orig", path.Replace("20261017", "20261018", StringComparison.Ordinal) })
            {
                IsGovernedRetirementPath(neighbor).ShouldBeFalse();
                byte[] bytes = Encoding.UTF8.GetBytes(Assignment("pass" + "word", JsonSerializer.Serialize(RandomSecret())));
                WriteEvidenceFile(root, neighbor, bytes);
                ScanTrackedFile(neighbor, root).ShouldBe([neighbor + ":1"]);
                JsonObject manifest = ReadStory64Registry(root);
                JsonNode entry = manifest["retired"]!.AsArray().Single(value => value!["path"]!.GetValue<string>() == path)!.DeepClone();
                entry["path"] = neighbor;
                entry["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                manifest["retired"]!.AsArray().Add(entry);
                WriteEvidenceFile(root, RetiredEvidenceManifest, Encoding.UTF8.GetBytes(manifest.ToJsonString()));
                Should.Throw<ShouldAssertException>(() => ScanTrackedFile(neighbor, root).ToArray());
            }
        });
    }

    /// <summary>Every selected artifact requires the immutable owner decision and all its provenance bindings.</summary>
    [Theory]
    [MemberData(nameof(Story64RetiredPaths))]
    public void Story64Retirement_MissingOrChangedApprovalFailsClosed(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        WithEvidenceRepository(root =>
        {
            foreach (string component in new[] { "decision", "proposal", "authorization", "authorityOverlay" })
            {
                foreach (bool missing in new[] { false, true })
                {
                    CopyStory64Retirement(root, path);
                    using JsonDocument decision = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, Story64RetirementDecision)));
                    string dependency = component == "decision" ? Story64RetirementDecision
                        : decision.RootElement.GetProperty("bindings").EnumerateArray()
                            .Single(value => value.GetProperty("kind").GetString() == component).GetProperty("path").GetString()!;
                    if (missing) File.Delete(Path.Combine(root, dependency));
                    else File.AppendAllText(Path.Combine(root, dependency), "\n");
                    Exception error = Should.Throw<Exception>(() => ScanTrackedFile(path, root).ToArray());
                    (missing ? typeof(FileNotFoundException) : typeof(ShouldAssertException)).IsInstanceOfType(error)
                        .ShouldBeTrue("The named approval dependency must cause the integrity failure.");
                }
            }
        });
    }

    /// <summary>Malformed, duplicate or reclassified registry decisions cannot authorize retirement.</summary>
    [Theory]
    [MemberData(nameof(Story64RetiredPaths))]
    public void Story64Retirement_RegistryDecisionFailsClosed(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        WithEvidenceRepository(root =>
        {
            foreach (string mutation in new[] { "status", "authoritative", "reason", "duplicate", "authorizationRecord", "authorizationSha256", "missing-authorization" })
            {
                CopyStory64Retirement(root, path);
                JsonObject manifest = ReadStory64Registry(root);
                JsonNode entry = manifest["retired"]!.AsArray().Single(value => value!["path"]!.GetValue<string>() == path)!;
                switch (mutation)
                {
                    case "status": entry["status"] = "active"; break;
                    case "authoritative": entry["authoritative"] = true; break;
                    case "reason": entry["reason"] = ""; break;
                    case "duplicate": manifest["retired"]!.AsArray().Add(entry.DeepClone()); break;
                    case "authorizationRecord": entry["authorizationRecord"] = "docs/other.json"; break;
                    case "authorizationSha256": entry["authorizationSha256"] = new string('0', 64); break;
                    case "missing-authorization": entry.AsObject().Remove("authorizationRecord"); break;
                }
                WriteEvidenceFile(root, RetiredEvidenceManifest, Encoding.UTF8.GetBytes(manifest.ToJsonString()));
                Exception error = Should.Throw<Exception>(() => ScanTrackedFile(path, root).ToArray());
                (mutation == "missing-authorization" ? typeof(KeyNotFoundException) : typeof(ShouldAssertException))
                    .IsInstanceOfType(error).ShouldBeTrue();
            }
        });
    }

    /// <summary>The new approval packet remains scanned even while untracked during development.</summary>
    [Fact]
    public void Story64Retirement_DeliveryFilesRemainScanned()
    {
        string directory = Path.GetDirectoryName(Story64RetirementDecision)!;
        foreach (string name in new[] { "retirement-decision.json", "approved-proposal.json", "owner-selection.json",
            "authority-overlay.json", "README.md", ".gitattributes" })
        {
            string path = Path.Combine(directory, name).Replace(Path.DirectorySeparatorChar, '/');
            ReadTrackedText(path).ShouldNotBeNull("Retirement metadata is not itself retired.");
            ScanTrackedFile(path).ShouldBeEmpty();
        }
    }

    /// <summary>Contradictory duplicate properties are rejected rather than resolved by last-value wins.</summary>
    [Theory]
    [MemberData(nameof(Story64RetiredPaths))]
    public void Story64Retirement_DuplicateJsonPropertiesFailClosed(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        WithEvidenceRepository(root =>
        {
            foreach (string property in new[] { "schema", "retired", "path", "sha256", "status", "authoritative", "authorizationRecord", "authorizationSha256" })
            {
                CopyStory64Retirement(root, path);
                ReadTrackedText(path, root).ShouldBeNull();
                JsonObject manifest = ReadStory64Registry(root);
                JsonNode entry = manifest["retired"]!.AsArray().Single(value => value!["path"]!.GetValue<string>() == path)!;
                string duplicate = JsonSerializer.Serialize(property) + ":"
                    + (property == "authoritative" ? "true" : property == "retired" ? "[]" : "\"unapproved\"") + ",";
                string text = manifest.ToJsonString();
                text = property is "schema" or "retired" ? text.Insert(1, duplicate)
                    : text.Replace(entry.ToJsonString(), entry.ToJsonString().Insert(1, duplicate), StringComparison.Ordinal);
                WriteEvidenceFile(root, RetiredEvidenceManifest, Encoding.UTF8.GetBytes(text));
                Should.Throw<ShouldAssertException>(() => ScanTrackedFile(path, root).ToArray()).Message
                    .ShouldContain("duplicate JSON property");
            }
        });
    }

    /// <summary>Original forbidden bytes remain detected after a rename outside the exact approved path.</summary>
    [Theory]
    [MemberData(nameof(Story64RetiredPaths))]
    public void Story64Retirement_RenamedOriginalsRemainDetected(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        WithEvidenceRepository(root =>
        {
            string renamed = "evidence/unapproved-original" + Path.GetExtension(path);
            WriteEvidenceFile(root, renamed, File.ReadAllBytes(Path.Combine(RepoRoot, path)));
            int expected = path.EndsWith(".xml", StringComparison.Ordinal) || path.Contains("pre-review-sources", StringComparison.Ordinal) ? 1 : 2;
            ScanTrackedFile(renamed, root).Count().ShouldBe(expected);
        });
    }

    /// <summary>A generated identifier and the same quoted literal exercise distinct C# source meanings.</summary>
    [Fact]
    public void Story64Retirement_CSharpLiteralFixtureCannotBeAnIdentifier()
    {
        string value = "runtime" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        WithEvidenceRepository(root =>
        {
            const string path = "evidence/unapproved-literal.cs";
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(Assignment("pass" + "word", value)));
            ScanTrackedFile(path, root).ShouldBeEmpty("An identifier names runtime data.");
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(Assignment("pass" + "word", JsonSerializer.Serialize(value))));
            ScanTrackedFile(path, root).ShouldBe([path + ":1"]);
        });
    }

    /// <summary>Historical receipts/indexes and the overlay's canonical JSON locators remain verifiable.</summary>
    [Fact]
    public void Story64Retirement_ReferencedHistoricalBytesAndLocatorsRemainIntact()
    {
        string directory = Path.GetDirectoryName(Story64RetirementDecision)!;
        using JsonDocument overlay = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepoRoot, directory, "authority-overlay.json")));
        var verified = new HashSet<string>(StringComparer.Ordinal);
        foreach (string section in new[] { "retiredArtifacts", "directReferences", "indirectReferences" })
        {
            foreach (JsonElement row in overlay.RootElement.GetProperty(section).EnumerateArray())
            {
                string path = row.GetProperty("path").GetString()!;
                byte[] bytes = File.ReadAllBytes(Path.Combine(RepoRoot, path));
                if (verified.Add(path))
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant().ShouldBe(row.GetProperty("sha256").GetString(),
                        "Historical artifact bytes must remain intact: " + path);
                if (row.TryGetProperty("referenceBindings", out JsonElement bindings) && bindings.GetArrayLength() > 0)
                {
                    using JsonDocument source = JsonDocument.Parse(bytes);
                    foreach (JsonElement binding in bindings.EnumerateArray())
                    {
                        VerifyStory64JsonPointer(source.RootElement, binding.GetProperty("jsonPointer").GetString()!);
                        (binding.GetProperty("targetKind").GetString() is "value" or "object-key").ShouldBeTrue();
                    }
                }
            }
        }
        verified.Count.ShouldBeGreaterThan(ApprovedStory64RetirementHashes.Count);
    }

    private static void VerifyStory64JsonPointer(JsonElement current, string pointer)
    {
        pointer.StartsWith("/", StringComparison.Ordinal).ShouldBeTrue();
        foreach (string encoded in pointer.Split('/').Skip(1))
        {
            string segment = encoded.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            current = current.ValueKind == JsonValueKind.Array
                ? current[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)] : current.GetProperty(segment);
        }
        current.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
    }

    private static void VerifyStory64UniqueProperties(JsonElement value)
    {
        string[] names = value.EnumerateObject().Select(property => property.Name).ToArray();
        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(names.Length,
            "Retirement decisions cannot contain duplicate JSON property names.");
    }

    private static void VerifyStory64RetirementApproval(string root, JsonElement registry, JsonElement retired)
    {
        VerifyStory64UniqueProperties(registry);
        VerifyStory64UniqueProperties(retired);
        retired.GetProperty("authorizationRecord").GetString().ShouldBe(Story64RetirementDecision);
        retired.GetProperty("authorizationSha256").GetString().ShouldBe(Story64RetirementDecisionSha256);
        byte[] bytes = File.ReadAllBytes(Path.Combine(root, Story64RetirementDecision));
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant().ShouldBe(Story64RetirementDecisionSha256,
            "Only the exact recorded owner decision can approve these five retirements.");
        using JsonDocument decision = JsonDocument.Parse(bytes);
        JsonElement approved = decision.RootElement.GetProperty("retired").EnumerateArray()
            .Single(value => value.GetProperty("path").GetString() == retired.GetProperty("path").GetString());
        approved.GetProperty("sha256").GetString().ShouldBe(retired.GetProperty("sha256").GetString(),
            "The specific registry tuple must appear in the pinned owner decision.");
        foreach (JsonElement binding in decision.RootElement.GetProperty("bindings").EnumerateArray())
        {
            string dependency = binding.GetProperty("path").GetString()!;
            string expectedHash = binding.GetProperty("sha256").GetString()!;
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, dependency))))
                .ToLowerInvariant().ShouldBe(expectedHash, "The approved proposal, user selection and authority overlay must remain intact.");
        }
    }

    private static JsonObject ReadStory64Registry(string root)
        => JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, RetiredEvidenceManifest)))!.AsObject();

    private static void CopyStory64Retirement(string root, string path)
    {
        WriteEvidenceFile(root, RetiredEvidenceManifest, File.ReadAllBytes(Path.Combine(RepoRoot, RetiredEvidenceManifest)));
        WriteEvidenceFile(root, path, File.ReadAllBytes(Path.Combine(RepoRoot, path)));
        byte[] bytes = File.ReadAllBytes(Path.Combine(RepoRoot, Story64RetirementDecision));
        WriteEvidenceFile(root, Story64RetirementDecision, bytes);
        using JsonDocument decision = JsonDocument.Parse(bytes);
        foreach (JsonElement binding in decision.RootElement.GetProperty("bindings").EnumerateArray())
        {
            string dependency = binding.GetProperty("path").GetString()!;
            WriteEvidenceFile(root, dependency, File.ReadAllBytes(Path.Combine(RepoRoot, dependency)));
        }
    }
}
