using System.Text;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Exercises the nine approved historical source and probe contexts through actual bytes.</summary>
public sealed partial class SecretsProtectionTests
{
    private const string HistoricalCandidatePath = "_bmad-output/implementation-artifacts/6-5d-simplification/previous-candidate.md";
    private const string HistoricalRunnerPath = "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/run_verification.py";
    private const string HistoricalProbePath = "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/test_run_verification.py";

    /// <summary>Original contexts crossed with supported byte decoders.</summary>
    public static TheoryData<string, string> SemanticEvidenceCases
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach (string scenario in new[] { "tuple", "tuple-mutation", "composition", "interpolation", "receipt-rejection", "bearer-rejection", "authorization-redaction", "bare-redaction", "inspection" })
            {
                foreach (string encoding in new[] { "utf8", "utf8-bom", "utf16-le", "utf16-be", "utf16-le-no-bom", "utf16-be-no-bom", "malformed-utf8", "control-prefix" })
                {
                    cases.Add(scenario, encoding);
                }
            }
            return cases;
        }
    }

    /// <summary>The exact real historical triggers remain benign across the owning decoder lanes.</summary>
    [Theory]
    [MemberData(nameof(SemanticEvidenceCases))]
    public void EvidenceSemanticIngestion_AcceptsHistoricalContext(string scenario, string encoding)
    {
        (string path, string content) = HistoricalSemanticContext(scenario);
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, path, EncodeEvidenceText(content, encoding));
            ReadTrackedText(path, root).ShouldNotBeNull();
            ScanTrackedFile(path, root).ShouldBeEmpty();
        });
    }

    /// <summary>Literal substitutions and changed probe markers are findings in every decoder lane.</summary>
    [Theory]
    [MemberData(nameof(SemanticEvidenceCases))]
    public void EvidenceSemanticIngestion_RejectsCredentialSubstitution(string scenario, string encoding)
    {
        (string path, string content) = HistoricalSemanticContext(scenario);
        string secret = RandomSecret();
        string replacement = scenario switch
        {
            "tuple" or "tuple-mutation" => content.Replace("(account_kind,account)", "('" + secret + "',account)", StringComparison.Ordinal),
            "composition" => content.Replace("+ password +", "+ \"" + secret + "\" +", StringComparison.Ordinal),
            "interpolation" => content.Replace("{self.password}", secret, StringComparison.Ordinal),
            "inspection" => content.Replace(InspectionProbeMarker, secret, StringComparison.Ordinal),
            _ => content.Replace(NegativeProbeMarker, secret, StringComparison.Ordinal),
        };
        replacement.ShouldNotBe(content);
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, path, EncodeEvidenceText(replacement, encoding));
            ScanTrackedFile(path, root).ShouldNotBeEmpty();
        });
    }

    /// <summary>URI credentials and adjacent assignments never inherit an allowed source/probe context.</summary>
    [Theory]
    [MemberData(nameof(SemanticEvidenceCases))]
    public void EvidenceSemanticIngestion_RejectsAdjacentCredentials(string scenario, string encoding)
    {
        (string path, string content) = HistoricalSemanticContext(scenario);
        string suffix = "\n" + Assignment("pass" + "word", "\"" + RandomSecret() + "\"")
            + "\nendpoint = \"ssh://git:" + Uri.EscapeDataString(RandomSecret()) + "@github.com/Hexalith/private.git\"\n";
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, path, EncodeEvidenceText(content + suffix, encoding));
            ScanTrackedFile(path, root).Count().ShouldBeGreaterThanOrEqualTo(2);
        });
    }

    /// <summary>Rejection, redaction and no-retention assertions are required in the same complete method.</summary>
    [Theory]
    [InlineData("receipt-rejection")]
    [InlineData("bearer-rejection")]
    [InlineData("authorization-redaction")]
    [InlineData("bare-redaction")]
    [InlineData("inspection")]
    public void EvidenceSemanticIngestion_RejectsBrokenProofContext(string scenario)
    {
        (string path, string content) = HistoricalSemanticContext(scenario);
        string broken = scenario switch
        {
            "receipt-rejection" or "bearer-rejection" => content.Replace("self.reject()", "verifier.validate(self.directory)", StringComparison.Ordinal),
            "inspection" => content.Replace("self.assertNotIn(\"" + InspectionProbeMarker + "\", retained)", "self.assertIn(\"" + InspectionProbeMarker + "\", retained)", StringComparison.Ordinal),
            _ => content.Replace("self.assertNotIn", "self.assertIn", StringComparison.Ordinal),
        };
        var candidates = new List<string>
        {
            broken,
            content.Replace("unittest.TestCase", "object", StringComparison.Ordinal),
            content.Replace("    def test_", "    def operational_", StringComparison.Ordinal),
        };
        if (scenario is "receipt-rejection" or "bearer-rejection")
        {
            candidates.Add(content.Replace("def reject(self):", "def accept(self):", StringComparison.Ordinal));
            candidates.Add(content.Replace("self.assertRaises", "contextlib.nullcontext", StringComparison.Ordinal));
            candidates.Add(content + "    def reject(self):\n        pass\n\n");
        }
        else if (scenario is "authorization-redaction" or "bare-redaction")
        {
            candidates.Add(content.Replace("verifier.safe(redacted)", "send(redacted)", StringComparison.Ordinal));
        }
        if (scenario is "authorization-redaction" or "bare-redaction")
        {
            candidates.Add(content + "    def setUp(self):\n        self.runner.redact: 'x;y' = lambda *args: None\n\n");
        }
        if (scenario == "inspection")
        {
            candidates.Add(content + "    def setUp(self):\n        self.assertEqual: 'x;y' = lambda *args: None\n\n");
            candidates.Add(content + "    def setUp(self):\n        self.assertTrue: 'x;y' = lambda *args: None\n\n");
        }
        WithEvidenceRepository(root =>
        {
            foreach (string candidate in candidates)
            {
                WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(candidate));
                ScanTrackedFile(path, root).ShouldNotBeEmpty();
            }
        });
    }

    /// <summary>Inert probe markers have no global or operational-use allowance.</summary>
    [Theory]
    [InlineData("receipt-rejection")]
    [InlineData("bearer-rejection")]
    [InlineData("authorization-redaction")]
    [InlineData("bare-redaction")]
    [InlineData("inspection")]
    public void EvidenceSemanticIngestion_RejectsOperationalMarkerUse(string scenario)
    {
        (string path, string content) = HistoricalSemanticContext(scenario);
        string marker = scenario == "inspection" ? InspectionProbeMarker : NegativeProbeMarker;
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(content + "\n" + Assignment("pass" + "word", "\"" + marker + "\"") + "\n"));
            ScanTrackedFile(path, root).ShouldNotBeEmpty();
            string captured = "_bmad-output/implementation-artifacts/evidence/probe/results.ctrf.json";
            WriteEvidenceFile(root, captured, Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["diagnostic"] = content, ["pass" + "word"] = marker })));
            ScanTrackedFile(captured, root).ShouldNotBeEmpty();
        });
    }

    /// <summary>Malformed source and missing, altered, nested or unclosed here-doc boundaries fail closed.</summary>
    [Theory]
    [InlineData("tuple")]
    [InlineData("tuple-mutation")]
    [InlineData("composition")]
    [InlineData("interpolation")]
    public void EvidenceSemanticIngestion_RejectsBrokenSourceOrBoundary(string scenario)
    {
        (string path, string content) = HistoricalSemanticContext(scenario);
        var broken = new List<string>();
        if (scenario is "tuple" or "tuple-mutation")
        {
            broken.Add(content.Replace("(account_kind,account)", "(account_kind,account", StringComparison.Ordinal));
            broken.Add(content.Replace("\nPY\n", "\nWRONG\n", StringComparison.Ordinal));
            broken.Add(content.Replace("\nPY\n", "\nPY \n", StringComparison.Ordinal));
            broken.Add(content.Replace("\nPY\n```", "\nPY", StringComparison.Ordinal));
            broken.Add(content.Replace("<<'PY'", "<<'PY\"", StringComparison.Ordinal));
            broken.Add(content.Replace("\nPY\n", "\npython3 - <<'INNER'\nPY\nINNER\n", StringComparison.Ordinal));
            broken.Add(content.Replace("```bash", "```text", StringComparison.Ordinal));
        }
        else if (scenario == "composition")
        {
            broken.Add(content.Replace("+ password +", "+ (password or \"" + RandomSecret() + "\") +", StringComparison.Ordinal));
            broken.Add(content.TrimEnd().TrimEnd(')'));
            broken.Add("\"\"\"\n" + content);
        }
        else
        {
            broken.Add(content.Replace("{self.password}", "{self.password or '" + RandomSecret() + "'}", StringComparison.Ordinal));
            broken.Add(content.Replace("{self.password}", "{self.password!r}", StringComparison.Ordinal));
            broken.Add(content.Replace("{self.password}", "{self.password", StringComparison.Ordinal));
            broken.Add("\"\"\"\n" + content);
        }
        WithEvidenceRepository(root =>
        {
            foreach (string candidate in broken)
            {
                WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(candidate));
                ScanTrackedFile(path, root).ShouldNotBeEmpty();
            }
        });
    }

    /// <summary>Evidence containing lookalike probe methods inside a Python literal stays scanned.</summary>
    [Theory]
    [InlineData("receipt-rejection")]
    [InlineData("bearer-rejection")]
    [InlineData("authorization-redaction")]
    [InlineData("bare-redaction")]
    [InlineData("inspection")]
    public void EvidenceSemanticIngestion_RejectsQuotedOrExtendedProbeMethod(string scenario)
    {
        (string path, string content) = HistoricalSemanticContext(scenario);
        WithEvidenceRepository(root =>
        {
            foreach (string candidate in new[] { "capture = '''\n" + content + "'''\n", content.TrimEnd() + "\n        send(\"" + NegativeProbeMarker + "\")\n\n", content + "        send(\"" + NegativeProbeMarker + "\")\n\n", content.Replace("(self):", "(self)\n", StringComparison.Ordinal) })
            {
                WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(candidate));
                ScanTrackedFile(path, root).ShouldNotBeEmpty();
            }
        });
    }

    /// <summary>Both LF and CRLF copies preserve the approved recognition and adjacent detection.</summary>
    [Theory]
    [InlineData("receipt-rejection")]
    [InlineData("bearer-rejection")]
    [InlineData("authorization-redaction")]
    [InlineData("bare-redaction")]
    [InlineData("inspection")]
    public void EvidenceSemanticIngestion_AcceptsCrLfProbeContext(string scenario)
    {
        (string path, string content) = HistoricalSemanticContext(scenario);
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(content.Replace("\n", "\r\n", StringComparison.Ordinal)));
            ScanTrackedFile(path, root).ShouldBeEmpty();
        });
    }

    /// <summary>New untracked partials are validated through the same ingestion they will receive in CI.</summary>
    [Theory]
    [InlineData("SecretsProtectionTests.cs")]
    [InlineData("SecretsProtectionTests.EvidenceIngestion.cs")]
    [InlineData("SecretsProtectionTests.EvidenceRecognition.cs")]
    [InlineData("SecretsProtectionTests.EvidenceSemanticIngestion.cs")]
    [InlineData("SecretsProtectionTests.NuGetEvidence.cs")]
    [InlineData("SecretsProtectionTests.XunitEvidence.cs")]
    [InlineData("SecretsProtectionTests.Story64Retirement.cs")]
    [InlineData("SecretsProtectionTests.UnifiedDiffCursor.cs")]
    [InlineData("SecretsProtectionTests.EmptyCalls.cs")]
    public void EvidenceSemanticIngestion_AcceptsOwningScannerAndTestSource(string name)
        => WithEvidenceRepository(root =>
        {
            string path = "tests/Hexalith.EventStore.Server.Tests/Security/" + name;
            WriteEvidenceFile(root, path, File.ReadAllBytes(Path.Combine(RepoRoot, path)));
            ReadTrackedText(path, root).ShouldNotBeNull();
            ScanTrackedFile(path, root).ShouldBeEmpty();
        });

    /// <summary>Review reproductions reject source strings, literal field affixes and incomplete source prefixes.</summary>
    [Theory]
    [InlineData("quoted-tuple")]
    [InlineData("sent-tuple")]
    [InlineData("literal-prefix")]
    [InlineData("literal-suffix")]
    [InlineData("opposite-single-quote")]
    [InlineData("opposite-double-quote")]
    [InlineData("escaped-quote-suffix")]
    [InlineData("tuple-keywords")]
    [InlineData("tuple-attribute-keyword")]
    [InlineData("interpolation-keyword")]
    [InlineData("interpolation-attribute-keyword")]
    [InlineData("composition-keyword")]
    [InlineData("preceding-open-parenthesis")]
    [InlineData("preceding-open-bracket")]
    [InlineData("preceding-close-parenthesis")]
    [InlineData("preceding-continuation")]
    public void EvidenceSemanticIngestion_RejectsReviewedSourceGaps(string scenario)
    {
        string field = "pass" + "word";
        string tuple = Assignment("account_" + "key", "(account_kind,account)");
        string content = scenario switch
        {
            "quoted-tuple" => "value = \"" + tuple + "\"\n",
            "sent-tuple" => "send(\"" + tuple + "\")\n",
            "literal-prefix" => "value = f\"" + Assignment(field, RandomSecret() + "{self.password}") + "\"\n",
            "literal-suffix" => "value = f\"" + Assignment(field, "{self.password}" + RandomSecret()) + "\"\n",
            "opposite-single-quote" => "value = f\"" + Assignment(field, "{self.password}'" + RandomSecret()) + "\"\n",
            "opposite-double-quote" => "value = f'" + Assignment(field, "{self.password}\"" + RandomSecret()) + "'\n",
            "escaped-quote-suffix" => "value = f\"" + Assignment(field, "{self.password}\\\"" + RandomSecret()) + "\"\n",
            "tuple-keywords" => Assignment("account_" + "key", "(yield,await)") + "\n",
            "tuple-attribute-keyword" => Assignment("account_" + "key", "(value.class,account)") + "\n",
            "interpolation-keyword" => "value = f\"" + Assignment(field, "{class}") + "\"\n",
            "interpolation-attribute-keyword" => "value = f\"" + Assignment(field, "{value.class}") + "\"\n",
            "composition-keyword" => HistoricalSemanticContext("composition").Content.Replace("+ password +", "+ class +", StringComparison.Ordinal),
            "preceding-open-parenthesis" => "pending = (\n" + tuple + "\n",
            "preceding-open-bracket" => "pending = [\n" + tuple + "\n",
            "preceding-close-parenthesis" => ")\n" + tuple + "\n",
            "preceding-continuation" => "pending = runtime \\\n" + tuple + "\n",
            _ => throw new ArgumentException("Unknown review reproduction.", nameof(scenario)),
        };
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, HistoricalRunnerPath, Encoding.UTF8.GetBytes(content));
            ScanTrackedFile(HistoricalRunnerPath, root).ShouldNotBeEmpty();
        });
    }

    /// <summary>Actual body termination, whitespace and semicolons are the only approved f-string value boundaries.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" dbname=eventstore")]
    [InlineData(";dbname=eventstore")]
    public void EvidenceSemanticIngestion_AcceptsActualFStringFieldBoundary(string suffix)
        => WithEvidenceRepository(root =>
        {
            string content = "value = f\"" + Assignment("pass" + "word", "{self.password}" + suffix) + "\"\n";
            WriteEvidenceFile(root, HistoricalRunnerPath, Encoding.UTF8.GetBytes(content));
            ScanTrackedFile(HistoricalRunnerPath, root).ShouldBeEmpty();
        });

    /// <summary>The historical mutation row is valid within its complete owning faults list.</summary>
    [Fact]
    public void EvidenceSemanticIngestion_AcceptsCompleteHistoricalMutationList()
    {
        (string path, string content) = HistoricalSemanticContext("tuple-mutation");
        WithEvidenceRepository(root =>
        {
            string complete = content.Replace("<<'PY'\n", "<<'PY'\nfaults = [\n", StringComparison.Ordinal)
                .Replace("\nPY\n", "\n]\nPY\n", StringComparison.Ordinal);
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(complete));
            ScanTrackedFile(path, root).ShouldBeEmpty();
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(complete.Replace("faults = [", "faults = ([", StringComparison.Ordinal)));
            ScanTrackedFile(path, root).ShouldNotBeEmpty();
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(complete.Replace("\n]\n", "\n", StringComparison.Ordinal)));
            ScanTrackedFile(path, root).ShouldNotBeEmpty();
        });
    }

    /// <summary>Here-doc discovery never restarts inside enclosing shell data or Python string contents.</summary>
    [Theory]
    [InlineData("nested-python")]
    [InlineData("cat-data")]
    [InlineData("shell-single-quote")]
    [InlineData("shell-double-quote")]
    [InlineData("python-triple-quote")]
    [InlineData("unsupported-prefix")]
    public void EvidenceSemanticIngestion_RejectsReviewedHereDocumentOwnership(string scenario)
    {
        string source = Assignment("account_" + "key", "(account_kind,account)");
        string inner = "python3 - <<'INNER'\n" + source + "\nINNER\n";
        string content = scenario switch
        {
            "nested-python" => "python3 - <<'OUTER'\n" + inner + "OUTER\n",
            "cat-data" => "cat <<'OUTER'\n" + inner + "OUTER\n",
            "shell-single-quote" => "capture='\n" + inner + "'\n",
            "shell-double-quote" => "capture=\"\n" + inner + "\"\n",
            "python-triple-quote" => "python3 - <<'OUTER'\ncapture = '''\n" + inner + "'''\nOUTER\n",
            "unsupported-prefix" => "send captured-data\n" + inner,
            _ => throw new ArgumentException("Unknown review reproduction.", nameof(scenario)),
        };
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, HistoricalCandidatePath, Encoding.UTF8.GetBytes("```bash\n" + content + "```\n"));
            ScanTrackedFile(HistoricalCandidatePath, root).ShouldNotBeEmpty();
        });
    }

    /// <summary>Helper extensions, decorators, duplicate definitions and shadowed proof symbols revoke recognition.</summary>
    [Theory]
    [InlineData("receipt-rejection")]
    [InlineData("bearer-rejection")]
    [InlineData("authorization-redaction")]
    [InlineData("bare-redaction")]
    [InlineData("inspection")]
    public void EvidenceSemanticIngestion_RejectsReviewedProbeContextOverrides(string scenario)
    {
        (string path, string content) = HistoricalSemanticContext(scenario);
        int method = content.IndexOf("    def test_", StringComparison.Ordinal);
        int nameEnd = content.IndexOf('(', method);
        string name = content[(method + "    def ".Length)..nameEnd];
        var candidates = new List<string>
        {
            content.Insert(method, "    @unittest.skip('disabled')\n"),
            content.Replace("class HistoricalProbeTests", "@replacement\nclass HistoricalProbeTests", StringComparison.Ordinal),
            content + "    def " + name + "(self):\n        pass\n\n",
            content + "    def  " + name + "(self):\n        pass\n\n",
            content + "    def " + name + " (self):\n        pass\n\n",
            content + "    def\t" + name + "\t(self):\n        pass\n\n",
            content + "    def later(self)\n        pass\n\n",
            content + "    def later\n",
            content + "    def later(self, value=Resolve()):\n        pass\n\n",
            content.Replace("class HistoricalProbeTests", "unittest.TestCase = replacement\nclass HistoricalProbeTests", StringComparison.Ordinal),
            content.Replace("class HistoricalProbeTests", "unittest = replacement\nclass HistoricalProbeTests", StringComparison.Ordinal),
            content + "    def setUp(self):\n        self.assertNotIn = lambda *args: None\n\n",
            content + "    def setUp(self):\n        self.assertNotIn: object = lambda *args: None\n\n",
            content + "    def setUp(self):\n        self.assertNotIn: 'x;y' = lambda *args: None\n\n",
            content + "HistoricalProbeTests.assertNotIn: 'x;y' = lambda *args: None\n",
            content + "unittest.TestCase.assertNotIn: 'x;y' = lambda *args: None\n",
            content + "verifier.safe: 'x;y' = lambda *args: None\n",
            content + "    def assertNotIn(self, *args):\n        pass\n\n",
            content + "    assertNotIn = lambda *args: None\n",
            content + "HistoricalProbeTests.assertNotIn = lambda *args: None\n",
            content + "unittest.TestCase.assertNotIn = lambda *args: None\n",
            content + "verifier.safe = lambda *args: None\n",
        };
        if (scenario is "receipt-rejection" or "bearer-rejection")
        {
            candidates.Add(content.Replace("            verifier.validate(self.directory)\n\n", "            verifier.validate(self.directory)\n\n        send(self.commands)\n\n", StringComparison.Ordinal));
            candidates.Add(content.Replace("            verifier.validate(self.directory)\n\n", "            verifier.validate(self.directory)\n\n        self.directory.joinpath('retained.txt').write_text(self.commands[0]['diagnostic'])\n\n", StringComparison.Ordinal));
            candidates.Add(content.Replace("    def reject(self):", "    @replacement\n    def reject(self):", StringComparison.Ordinal));
            candidates.Add(content + "    def setUp(self):\n        self.assertRaises = lambda *args: None\n\n");
            candidates.Add(content + "    def setUp(self):\n        self.assertRaises: 'x;y' = lambda *args: None\n\n");
            candidates.Add(content + "HistoricalProbeTests.assertRaises: 'x;y' = lambda *args: None\n");
            candidates.Add(content + "verifier.validate: 'x;y' = lambda *args: None\n");
            candidates.Add(content + "    def setUp(self):\n        self.reject: 'x;y' = lambda *args: None\n\n");
            candidates.Add(content + "    def  reject(self):\n        pass\n\n");
            candidates.Add(content + "    def reject (self):\n        pass\n\n");
            candidates.Add(content + "    def\treject\t(self):\n        pass\n\n");
        }
        WithEvidenceRepository(root =>
        {
            foreach (string candidate in candidates)
            {
                WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(candidate));
                ScanTrackedFile(path, root).ShouldNotBeEmpty();
            }
        });
    }

    /// <summary>The actual maintained two-line runtime composition is bounded and preserves literal/URI detection.</summary>
    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf16-le-no-bom")]
    [InlineData("utf16-be-no-bom")]
    [InlineData("malformed-utf8")]
    [InlineData("control-prefix")]
    public void EvidenceSemanticIngestion_ValidatesMaintainedRuntimeComposition(string encoding)
    {
        string[] lines = File.ReadAllLines(Path.Combine(RepoRoot, "tools/p1r_published_executor.py"));
        int first = Array.FindIndex(lines, static line => line.Contains("connection = (f", StringComparison.Ordinal));
        first.ShouldBeGreaterThanOrEqualTo(0);
        string content = "# Maintained runtime source\n" + lines[first].TrimStart() + "\n" + lines[first + 1] + "\n";
        WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, HistoricalRunnerPath, EncodeEvidenceText(content, encoding));
            ScanTrackedFile(HistoricalRunnerPath, root).ShouldBeEmpty();
            foreach (string candidate in new[]
            {
                content.Replace("{self.postgres_password}", RandomSecret(), StringComparison.Ordinal),
                content.Replace("{self.postgres_password} ", "{self.postgres_password}", StringComparison.Ordinal),
                content.Replace("{self.postgres_password}", "{self.postgres_password}'" + RandomSecret(), StringComparison.Ordinal),
                content.Replace("connect_timeout=10", "connect_timeout=10" + RandomSecret(), StringComparison.Ordinal),
                content.Replace("host=127.0.0.1", "endpoint=ssh://git:" + Uri.EscapeDataString(RandomSecret()) + "@github.com/Hexalith/private.git", StringComparison.Ordinal),
                content + Assignment("pass" + "word", "\"" + RandomSecret() + "\"") + "\n",
            })
            {
                WriteEvidenceFile(root, HistoricalRunnerPath, EncodeEvidenceText(candidate, encoding));
                ScanTrackedFile(HistoricalRunnerPath, root).ShouldNotBeEmpty();
            }
        });
    }

    /// <summary>Maintained tooling and runtime-generated negative fixtures remain clean under real source ingestion.</summary>
    [Theory]
    [InlineData("tools/p1r_published_executor.py")]
    [InlineData("tools/tests/test_p1r_published_executor.py")]
    [InlineData("tools/tests/test_p1r_published_qualification.py")]
    public void EvidenceSemanticIngestion_AcceptsMaintainedPythonSource(string path)
        => WithEvidenceRepository(root =>
        {
            WriteEvidenceFile(root, path, File.ReadAllBytes(Path.Combine(RepoRoot, path)));
            ScanTrackedFile(path, root).ShouldBeEmpty();
        });

    private static (string Path, string Content) HistoricalSemanticContext(string scenario)
    {
        if (scenario is "tuple" or "tuple-mutation")
        {
            string[] lines = File.ReadAllLines(Path.Combine(RepoRoot, HistoricalCandidatePath));
            return (HistoricalCandidatePath, "Historical source context\n```bash\npython3 - <<'PY'\n" + lines[scenario == "tuple" ? 2592 : 9268].TrimStart() + "\nPY\n```\n");
        }
        if (scenario is "composition" or "interpolation")
        {
            string[] lines = File.ReadAllLines(Path.Combine(RepoRoot, HistoricalRunnerPath));
            return (HistoricalRunnerPath, "# Historical source context\n" + lines[scenario == "composition" ? 1525 : 1570].TrimStart() + "\n");
        }
        string original = File.ReadAllText(Path.Combine(RepoRoot, HistoricalProbePath));
        string method = scenario switch
        {
            "receipt-rejection" => "test_secret_bearing_receipt_is_rejected",
            "bearer-rejection" => "test_retained_bare_bearer_token_is_rejected",
            "authorization-redaction" => "test_authorization_redaction_removes_entire_bearer_token",
            "bare-redaction" => "test_bare_bearer_redaction_removes_token",
            "inspection" => "test_shared_inspection_never_retains_unrelated_environment",
            _ => throw new ArgumentException("Unknown historical context.", nameof(scenario)),
        };
        int start = original.IndexOf("    def " + method + "(self):", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        int end = original.IndexOf("\n    def ", start + 1, StringComparison.Ordinal);
        string body = original[start..end].TrimEnd();
        int rejection = original.IndexOf("    def reject(self):", StringComparison.Ordinal);
        int rejectionEnd = original.IndexOf("\n    def ", rejection + 1, StringComparison.Ordinal);
        string proof = original[rejection..rejectionEnd].TrimEnd();
        return (HistoricalProbePath, "# Historical proof context\nimport unittest\nclass HistoricalProbeTests(unittest.TestCase):\n" + proof + "\n\n" + body + "\n\n");
    }
}
