using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Exercises evidence through the owning file reader and secret scanner.</summary>
public sealed partial class SecretsProtectionTests
{
    private static readonly string[] RestoredEvidencePaths =
    [
        "_bmad-output/implementation-artifacts/evidence/probe/results.ctrf.json",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/probe.json",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/probe.md",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/probe.cs",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/probe.py",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/NuGet.Config",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/receipt",
        "_bmad-output/implementation-artifacts/6-5d-simplification/previous-candidate.md",
    ];

    /// <summary>Former exclusions crossed with every supported text decoder and credential form.</summary>
    public static TheoryData<string, string, bool> EvidenceIngestionCases
    {
        get
        {
            var cases = new TheoryData<string, string, bool>();
            foreach (string path in RestoredEvidencePaths)
            {
                foreach (string encoding in new[] { "utf8", "utf8-bom", "utf16-le", "utf16-be", "utf16-le-no-bom", "utf16-be-no-bom", "malformed-utf8", "control-prefix" })
                {
                    cases.Add(path, encoding, false);
                    cases.Add(path, encoding, true);
                }
            }
            return cases;
        }
    }

    /// <summary>Injected assignments and credential URIs must be detected after real file ingestion.</summary>
    [Theory]
    [MemberData(nameof(EvidenceIngestionCases))]
    public void EvidenceFileIngestion_DetectsInjectedCredentials(string path, string encoding, bool uri)
    {
        ArgumentNullException.ThrowIfNull(path);
        string credential = uri
            ? "ssh://git:" + Uri.EscapeDataString(RandomSecret()) + "@github.com/Hexalith/private.git"
            : RandomSecret();
        string document = CreateEvidenceDocument(path, credential, uri);
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, path, EncodeEvidenceText(document, encoding));
            ReadTrackedText(path, root).ShouldNotBeNull("Evidence must reach the decoder.");
            ScanTrackedFile(path, root).ShouldBe([path + ":1"]);
        });
    }

    /// <summary>Benign evidence and the exact public SSH identity remain valid after ingestion.</summary>
    [Theory]
    [MemberData(nameof(EvidenceIngestionCases))]
    public void EvidenceFileIngestion_AcceptsBenignText(string path, string encoding, bool uri)
    {
        string content = uri
            ? "ssh://git@github.com/Hexalith/Hexalith.EventStore.git"
            : "[redacted]";
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, path, EncodeEvidenceText(CreateEvidenceDocument(path, content, uri), encoding));
            ReadTrackedText(path, root).ShouldNotBeNull();
            ScanTrackedFile(path, root).ShouldBeEmpty();
        });
    }

    /// <summary>Sibling paths and prefix, extension and retirement-boundary lookalikes remain scanned.</summary>
    [Theory]
    [InlineData("_bmad-output/implementation-artifacts/evidence-other/results.ctrf.json")]
    [InlineData("_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification-2/probe.json")]
    [InlineData("_bmad-output/implementation-artifacts/6-5d-simplification/previous-candidate.md.orig")]
    [InlineData("docs/previous-candidate.md")]
    [InlineData("docs/results.ctrf.json")]
    [InlineData("_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/source-candidate.diff.orig")]
    [InlineData("_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation-2/source-candidate.diff")]
    [InlineData("_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md.orig")]
    public void EvidenceFileIngestion_PathLookalikesDoNotGrantExemptions(string path)
        => WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(Assignment("Pass" + "word", RandomSecret())));
            ScanTrackedFile(path, root).ShouldBe([path + ":1"]);
        });

    /// <summary>Binary signature handling and the established exact UI generated path are preserved.</summary>
    [Fact]
    public void EvidenceFileIngestion_PreservesBinaryAndUiGeneratedHandling()
        => WithEvidenceRepository(root =>
        {
            string path = RestoredEvidencePaths[0];
            byte[] injectedBytes = Encoding.UTF8.GetBytes(Assignment("Pass" + "word", RandomSecret()));
            WriteEvidenceFile(root, path, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, .. injectedBytes]);
            ReadTrackedText(path, root).ShouldBeNull();
            const string generated = "src/Hexalith.EventStore.Admin.UI/.artifacts/ui-test-obj/project.assets.json";
            WriteEvidenceFile(root, generated, injectedBytes);
            ReadTrackedText(generated, root).ShouldBeNull();
        });

    /// <summary>The established retirement is valid only for its approved content, even if re-pinned.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EvidenceFileIngestion_ChangedRetiredContentFailsClosed(bool repin, bool proofPacket)
        => WithEvidenceRepository(root =>
        {
            (string path, byte[] original) = CopyApprovedRetirement(root, proofPacket);
            ReadTrackedText(path, root).ShouldBeNull();
            byte[] changed = [.. original, .. Encoding.UTF8.GetBytes("\n" + Assignment("Pass" + "word", RandomSecret()))];
            WriteEvidenceFile(root, path, changed);
            if (repin)
            {
                JsonObject manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(root, RetiredEvidenceManifest)))!.AsObject();
                manifest["retired"]![proofPacket ? 1 : 0]!["sha256"] = Convert.ToHexString(SHA256.HashData(changed)).ToLowerInvariant();
                WriteEvidenceFile(root, RetiredEvidenceManifest, Encoding.UTF8.GetBytes(manifest.ToJsonString()));
            }
            Should.Throw<ShouldAssertException>(() => ScanTrackedFile(path, root).ToArray());
        });

    /// <summary>Unverifiable registry disposition or approval never silently retires content.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("schema")]
    [InlineData("status")]
    [InlineData("authoritative")]
    [InlineData("reason")]
    [InlineData("hash")]
    [InlineData("missing-hash")]
    [InlineData("missing-schema")]
    [InlineData("missing-retired")]
    [InlineData("retired-kind")]
    [InlineData("duplicate")]
    [InlineData("unapproved-path")]
    [InlineData("ungoverned-path")]
    public void EvidenceFileIngestion_InvalidRetirementEvidenceFailsClosed(string mutation)
        => WithEvidenceRepository(root =>
        {
            (string path, byte[] original) = CopyApprovedRetirement(root);
            string manifestPath = Path.Combine(root, RetiredEvidenceManifest);
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
            JsonNode entry = manifest["retired"]![0]!;
            switch (mutation)
            {
                case "missing": File.Delete(manifestPath); break;
                case "malformed": File.WriteAllText(manifestPath, "{"); break;
                default:
                    switch (mutation)
                    {
                        case "schema": manifest["schema"] = "unrecognized"; break;
                        case "status": entry["status"] = "active"; break;
                        case "authoritative": entry["authoritative"] = true; break;
                        case "reason": entry["reason"] = ""; break;
                        case "hash": entry["sha256"] = "invalid"; break;
                        case "missing-hash": entry.AsObject().Remove("sha256"); break;
                        case "missing-schema": manifest.Remove("schema"); break;
                        case "missing-retired": manifest.Remove("retired"); break;
                        case "retired-kind": manifest["retired"] = true; break;
                        case "duplicate":
                            JsonNode contradictory = entry.DeepClone();
                            contradictory["authoritative"] = true;
                            manifest["retired"]!.AsArray().Add(contradictory);
                            break;
                        case "unapproved-path": path = path.Replace("story-3-14/", "story-3-15/", StringComparison.Ordinal); entry["path"] = path; break;
                        case "ungoverned-path": path = "docs/capture.py"; entry["path"] = path; break;
                    }
                    File.WriteAllText(manifestPath, manifest.ToJsonString());
                    WriteEvidenceFile(root, path, original);
                    break;
            }
            // Every failure is consumed through the same ingestion call as the tracked guard.
            Type expectedType = mutation switch
            {
                "missing" => typeof(FileNotFoundException),
                "malformed" => typeof(JsonException),
                "missing-hash" or "missing-schema" or "missing-retired" => typeof(KeyNotFoundException),
                "retired-kind" => typeof(InvalidOperationException),
                _ => typeof(ShouldAssertException),
            };
            Exception error = Should.Throw<Exception>(() => ScanTrackedFile(path, root).ToArray());
            expectedType.IsInstanceOfType(error).ShouldBeTrue("The failure must come from the expected integrity check.");
        });

    /// <summary>A missing retired artifact cannot be treated as a successful exclusion.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvidenceFileIngestion_MissingRetiredArtifactFailsClosed(bool proofPacket)
        => WithEvidenceRepository(root =>
        {
            (string path, _) = CopyApprovedRetirement(root, proofPacket);
            File.Delete(Path.Combine(root, path));
            Should.Throw<FileNotFoundException>(() => ScanTrackedFile(path, root).ToArray());
        });

    /// <summary>Absent retirement approval restores scanning of the preserved original evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvidenceFileIngestion_MissingRetirementRecordRestoresScanning(bool proofPacket)
        => WithEvidenceRepository(root =>
        {
            (string path, _) = CopyApprovedRetirement(root, proofPacket);
            File.WriteAllText(Path.Combine(root, RetiredEvidenceManifest),
                "{\"schema\":\"hexalith.eventstore.retired-evidence.v1\",\"retired\":[]}");
            ScanTrackedFile(path, root).ShouldNotBeEmpty();
        });

    /// <summary>Inherited records with unavailable authorization lineage cannot grant an exclusion.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void EvidenceFileIngestion_UnverifiableInheritedRetirementFailsClosed(int entryIndex)
        => WithEvidenceRepository(root =>
        {
            CopyApprovedRetirement(root);
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, RetiredEvidenceManifest)));
            string path = manifest.RootElement.GetProperty("retired")[entryIndex].GetProperty("path").GetString()!;
            Should.Throw<ShouldAssertException>(() => ScanTrackedFile(path, root).ToArray());
        });

    /// <summary>The existing seal is verified inside actual file ingestion, including altered bytes.</summary>
    [Fact]
    public void EvidenceFileIngestion_SealedCaptureRequiresOriginalContent()
        => WithEvidenceRepository(root =>
        {
            byte[] original = File.ReadAllBytes(Path.Combine(RepoRoot, SealedP1RRemediationSourceCapture));
            WriteEvidenceFile(root, SealedP1RRemediationSourceCapture, original);
            ReadTrackedText(SealedP1RRemediationSourceCapture, root).ShouldBeNull();
            WriteEvidenceFile(root, SealedP1RRemediationSourceCapture, [.. original, (byte)'\n']);
            Should.Throw<ShouldAssertException>(() => ScanTrackedFile(SealedP1RRemediationSourceCapture, root).ToArray());
            File.Delete(Path.Combine(root, SealedP1RRemediationSourceCapture));
            Should.Throw<FileNotFoundException>(() => ScanTrackedFile(SealedP1RRemediationSourceCapture, root).ToArray());
        });

    private static (string Path, byte[] Bytes) CopyApprovedRetirement(string root, bool proofPacket = false)
    {
        WriteEvidenceFile(root, RetiredEvidenceManifest, File.ReadAllBytes(Path.Combine(RepoRoot, RetiredEvidenceManifest)));
        string path = ApprovedRetirementHashes.Keys.ElementAt(proofPacket ? 1 : 0);
        byte[] original = File.ReadAllBytes(Path.Combine(RepoRoot, path));
        WriteEvidenceFile(root, path, original);
        return (path, original);
    }

    private static string CreateEvidenceDocument(string path, string value, bool uri)
    {
        string field = uri ? "endpoint" : "Pass" + "word";
        if (path.EndsWith(".ctrf.json", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(new
            {
                results = new
                {
                    tool = new { name = "xunit" },
                    summary = new { tests = 1, passed = 1, failed = 0, pending = 0, skipped = 0, other = 0, start = 0, stop = 1 },
                    tests = new[] { new { name = "BenignTest", status = "passed", duration = 1, extra = new Dictionary<string, string> { [field] = value } } },
                },
            });
        }
        if (path.EndsWith(".json", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(new Dictionary<string, string> { [field] = value });
        }
        if (path.EndsWith(".Config", StringComparison.Ordinal))
        {
            return "<configuration><" + field + ">" + System.Security.SecurityElement.Escape(value) + "</" + field + "></configuration>";
        }
        if (path.EndsWith(".cs", StringComparison.Ordinal))
        {
            return "string " + field + " = " + JsonSerializer.Serialize(value) + ";";
        }
        return Assignment(field, JsonSerializer.Serialize(value));
    }

    private static void WithEvidenceRepository(Action<string> exercise)
    {
        string root = Directory.CreateTempSubdirectory("dw517-evidence-").FullName;
        try
        {
            WriteEvidenceFile(root, RetiredEvidenceManifest, Encoding.UTF8.GetBytes(
                "{\"schema\":\"hexalith.eventstore.retired-evidence.v1\",\"retired\":[]}"));
            exercise(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteEvidenceFile(string root, string path, byte[] bytes)
    {
        string fullPath = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes);
    }

    private static byte[] EncodeEvidenceText(string content, string encoding)
    {
        Encoding codec = encoding switch
        {
            "utf8-bom" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            "utf16-le" => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
            "utf16-be" => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
            "utf16-le-no-bom" => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
            "utf16-be-no-bom" => new UnicodeEncoding(bigEndian: true, byteOrderMark: false),
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        byte[] bytes = [.. codec.GetPreamble(), .. codec.GetBytes(content)];
        return encoding switch
        {
            "malformed-utf8" => [0xff, .. bytes],
            "control-prefix" => [0x00, 0x01, 0x02, .. bytes],
            _ => bytes,
        };
    }
}
