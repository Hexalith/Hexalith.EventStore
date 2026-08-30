using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the Story 4.15 OQ8 platform closure and source-only handoff contract.
/// </summary>
public sealed class Oq8PlatformClosureTests
{
    private const string LandedSource = "5e8f175b2ced4715f7c6f765386812cc1001dbb4";
    private const string ReviewedPostgresImage =
        "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636";
    private const string HistoricalPostgresImage = "postgres:18.4";
    private const string SuccessorDirectory = "sdk-10.0.400-xunit4-mtp";
    private static readonly string V2SuccessorRelativeDirectory = Path.Combine(
        "_bmad-output",
        "implementation-artifacts",
        "evidence",
        "story-4-15-successors",
        "v2");
    private static readonly Regex EventStorePlatformCompleteTrue = new(
        "\"eventStorePlatformComplete\"\\s*:\\s*true",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Verifies the complete checked-in packet, review subject, receipts, statuses, and documentation.
    /// </summary>
    [Fact]
    [Trait("OQ8Phase", "FinalOnly")]
    public void ApprovedSourceOnlyHandoffPasses()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            (int exitCode, string output) = RunValidator(root, fixture);

            exitCode.ShouldBe(0, output);
            output.ShouldContain("OQ8 platform evidence validation passed.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies immutable v1 evidence remains independently valid while explicitly non-authorizing.
    /// </summary>
    [Fact]
    public void HistoricalV1EvidencePassesWithoutAuthorizingCurrentSource()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            Directory.Delete(Path.Combine(fixture, V2SuccessorRelativeDirectory), recursive: true);

            (int exitCode, string output) = RunValidator(
                root,
                fixture,
                additionalArguments: ["--historical-v1-only"]);

            exitCode.ShouldBe(0, output);
            output.ShouldContain("v1 historical evidence validation passed; v1 does not authorize current source");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies default current-source closure never falls back to historical v1 evidence.
    /// </summary>
    [Fact]
    public void HistoricalV1AloneCannotAuthorizeChangedCurrentSource()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            Directory.Delete(Path.Combine(fixture, V2SuccessorRelativeDirectory), recursive: true);

            (int exitCode, string output) = RunValidator(root, fixture);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Story 4.15 v2 successor directory is missing or symlinked");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies incomplete, drifted, malformed, or authority-overstating v2 successors fail closed.
    /// </summary>
    /// <param name="mutation">The isolated v2 mutation.</param>
    [Theory]
    [InlineData("missing-receipt", "Story 4.15 v2 successor file set drift")]
    [InlineData("additional-artifact", "Story 4.15 v2 successor file set drift")]
    [InlineData("reordered-manifest", "Story 4.15 v2 closure manifest is not path-sorted")]
    [InlineData("malformed-manifest", "Malformed Story 4.15 v2 closure manifest line")]
    [InlineData("symlinked-artifact", "Story 4.15 v2 artifact limitations.json has a symlinked path component")]
    [InlineData("symlinked-v2-ancestor", "Story 4.15 v2 successor directory has a symlinked path component")]
    [InlineData("symlinked-source-ancestor", "Story 4.15 v2 bound source .github/workflows/integration.yml has a symlinked path component")]
    [InlineData("symlinked-gate-ancestor", "Story 4.15 v2 bound source docs/ci.md has a symlinked path component")]
    [InlineData("oversized-artifact", "Story 4.15 v2 artifact limitations.json exceeds the 65536-byte limit")]
    [InlineData("oversized-source", "Story 4.15 v2 bound source docs/ci.md exceeds the 524288-byte limit")]
    [InlineData("predecessor-mismatch", "Story 4.15 v2 predecessor link drift")]
    [InlineData("source-drift", "Story 4.15 v2 current source identity drift: .github/workflows/integration.yml")]
    [InlineData("semantic-workflow-tag", "Story 4.15 v2 current source PostgreSQL image drift: .github/workflows/integration.yml")]
    [InlineData("semantic-fixture-tag", "Story 4.15 v2 current source PostgreSQL image drift: tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs")]
    [InlineData("gate-input-drift", "Story 4.15 v2 gate-input identity drift: docs/ci.md")]
    [InlineData("reviewed-index-boolean", "Story 4.15 v2 reviewed index authority type drift")]
    [InlineData("pre-review-name", "Story 4.15 v2 pre-review command name drift: postgres-image-governance")]
    [InlineData("pre-review-command", "Story 4.15 v2 pre-review command identity drift: postgres-image-governance")]
    [InlineData("pre-review-count-float", "Story 4.15 v2 pre-review postgres-image-governance:tests must be an exact integer")]
    [InlineData("pre-review-exit-boolean", "Story 4.15 v2 pre-review postgres-image-governance:exitCode must be an exact integer")]
    [InlineData("pre-review-equals-freeze", "Story 4.15 v2 pre-review execution is not strictly before subject freeze")]
    [InlineData("pre-review-future", "Story 4.15 v2 pre-review execution timestamp is later than current UTC")]
    [InlineData("subject-future", "Story 4.15 v2 review-subject freeze timestamp is later than current UTC")]
    [InlineData("receipt-drift", "Story 4.15 v2 security review is not approved")]
    [InlineData("receipt-subject-drift", "Story 4.15 v2 security review subject drift")]
    [InlineData("receipt-equals-freeze", "Story 4.15 v2 security review predates the frozen subject")]
    [InlineData("receipt-future", "Story 4.15 v2 security receipt timestamp is later than current UTC")]
    [InlineData("handoff-equals-receipt", "Story 4.15 v2 handoff predates a review receipt")]
    [InlineData("handoff-future", "Story 4.15 v2 handoff assembly timestamp is later than current UTC")]
    [InlineData("test-verification-missing", "Story 4.15 v2 test review field set drift")]
    [InlineData("test-verification-name", "Story 4.15 v2 test review verification command name drift: oq8-platform-closure")]
    [InlineData("test-verification-command", "Story 4.15 v2 test review verification command identity drift: oq8-platform-closure")]
    [InlineData("test-verification-count", "Story 4.15 v2 test review verification oq8-platform-closure:tests count drift")]
    [InlineData("test-verification-failed", "Story 4.15 v2 test review verification oq8-platform-closure:failed count drift")]
    [InlineData("test-verification-skipped", "Story 4.15 v2 test review verification oq8-platform-closure:skipped count drift")]
    [InlineData("test-verification-float", "Story 4.15 v2 test review verification oq8-platform-closure:tests must be an exact integer")]
    [InlineData("test-verification-boolean", "Story 4.15 v2 test review verification oq8-platform-closure:failed must be an exact integer")]
    [InlineData("overstated-authority", "External authority overstated: deploymentAuthority")]
    public void V2SuccessorMutationsFailClosed(string mutation, string expected)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            ApplyV2Mutation(fixture, mutation);

            (int exitCode, string output) = RunValidator(root, fixture);

            exitCode.ShouldBe(1, output);
            output.ShouldContain(expected);
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies one immutable snapshot drives both v2 hashing and semantic validation.
    /// </summary>
    [Fact]
    public void V2ValidationUsesOneBoundSnapshotAndFreshRunsSeeLaterDrift()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            using Process process = CreatePythonProcess(
                """
                import importlib.util
                import pathlib
                import sys

                specification = importlib.util.spec_from_file_location("oq8_validator", sys.argv[1])
                validator = importlib.util.module_from_spec(specification)
                specification.loader.exec_module(validator)
                validator.configure_roots(pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3]))
                snapshots = validator.capture_v2_snapshots()
                workflow = pathlib.Path(sys.argv[2]) / ".github/workflows/integration.yml"
                workflow.write_text(workflow.read_text(encoding="utf-8") + "\n# post-snapshot drift\n", encoding="utf-8")
                validator.validate_v2_successor(snapshots=snapshots)
                try:
                    validator.validate_v2_successor()
                except validator.EvidenceError as error:
                    if "current source identity drift: .github/workflows/integration.yml" not in str(error):
                        raise
                    print("snapshot-consistent; fresh-drift-rejected")
                else:
                    raise SystemExit("fresh validation unexpectedly accepted post-snapshot drift")
                """);
            process.StartInfo.WorkingDirectory = root;
            process.StartInfo.ArgumentList.Add(Path.Combine(root, "tools", "validate-oq8-platform-evidence.py"));
            process.StartInfo.ArgumentList.Add(fixture);
            process.StartInfo.ArgumentList.Add(root);

            (int exitCode, string output, bool timedOut) = RunProcess(process, 30_000);

            timedOut.ShouldBeFalse("OQ8 v2 snapshot probe timed out.");
            exitCode.ShouldBe(0, output);
            output.ShouldContain("snapshot-consistent; fresh-drift-rejected");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies successor selection, current-source binding, and review receipts all fail closed.
    /// </summary>
    /// <param name="mutation">The successor contract mutation.</param>
    [Theory]
    [InlineData("current-source")]
    [InlineData("prior-selection")]
    [InlineData("review-receipt")]
    [InlineData("reviewed-base-wording")]
    [InlineData("reviewed-resolver-count")]
    [InlineData("review-external-authority")]
    [InlineData("selector-symlink")]
    [Trait("OQ8Phase", "FinalOnly")]
    public void SuccessorSealMutationsFailClosed(string mutation)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            string artifacts = Path.Combine(fixture, "_bmad-output", "implementation-artifacts");
            string expected;
            switch (mutation)
            {
                case "current-source":
                    File.AppendAllText(Path.Combine(fixture, "docs", "ci.md"), "\nSuccessor source drift.\n");
                    expected = "Story 4.15 v2 gate-input identity drift: docs/ci.md";
                    break;
                case "prior-selection":
                {
                    string selectorPath = Path.Combine(artifacts, "4-15-oq8-platform-closure-successor.json");
                    JsonObject selector = LoadObject(selectorPath);
                    selector["prior"]!["packetSha256"] = new string('0', 64);
                    WriteObject(selectorPath, selector);
                    expected = "Story 4.15 successor prior selection drift";
                    break;
                }
                case "review-receipt":
                {
                    string reviewPath = Path.Combine(
                        artifacts,
                        "evidence",
                        "story-4-15",
                        "successors",
                        SuccessorDirectory,
                        "reviews",
                        "security.json");
                    JsonObject review = LoadObject(reviewPath);
                    review["decision"] = "rejected";
                    WriteObject(reviewPath, review);
                    expected = "Story 4.15 successor checksum mismatch: reviews/security.json";
                    break;
                }
                case "reviewed-base-wording":
                {
                    string successor = SuccessorPath(artifacts);
                    string identityPath = Path.Combine(successor, "source-artifact-identity.json");
                    JsonObject identity = LoadObject(identityPath);
                    identity["bindingRule"] =
                        "Every listed current source path must exist as a regular file and match its reviewed SHA-256; the reviewed HEAD must remain an ancestor of Git HEAD.";
                    WriteObject(identityPath, identity);
                    ResealSuccessorArtifacts(artifacts);
                    expected = "Story 4.15 successor source binding rule drift";
                    break;
                }
                case "reviewed-resolver-count":
                {
                    string successor = SuccessorPath(artifacts);
                    string reviewPath = Path.Combine(successor, "reviews", "test.json");
                    JsonObject review = LoadObject(reviewPath);
                    review["findings"]![0] =
                        "Four focused Docker published-port resolver cases and the production OQ8 case passed with no failures or skips.";
                    WriteObject(reviewPath, review);
                    ResealSuccessorArtifacts(artifacts);
                    expected = "Story 4.15 successor test review findings drift";
                    break;
                }
                case "review-external-authority":
                {
                    string successor = SuccessorPath(artifacts);
                    string reviewPath = Path.Combine(successor, "reviews", "security.json");
                    JsonObject review = LoadObject(reviewPath);
                    review["authority"]!["externalRepositoryAuthority"] = true;
                    WriteObject(reviewPath, review);
                    ResealSuccessorArtifacts(artifacts);
                    expected = "External authority overstated: externalRepositoryAuthority";
                    break;
                }
                case "selector-symlink":
                {
                    string selectorPath = Path.Combine(artifacts, "4-15-oq8-platform-closure-successor.json");
                    string targetPath = selectorPath + ".target";
                    File.Move(selectorPath, targetPath);
                    File.CreateSymbolicLink(selectorPath, Path.GetFileName(targetPath));
                    expected = "Story 4.15 successor selector must be a regular non-symlink file";
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown successor mutation.");
            }

            (int exitCode, string output) = RunValidator(root, fixture);

            exitCode.ShouldBe(1, output);
            output.ShouldContain(expected);
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies source-only consumers receive one exact dependency bootstrap before validation.
    /// </summary>
    [Fact]
    public void SourceOnlyConsumerBootstrapInstructionsAreExactAndOrdered()
    {
        string root = FindRepositoryRoot();
        string closure = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-4-15",
            LandedSource);
        JsonObject subject = LoadObject(Path.Combine(closure, "review-subject.json"));
        JsonObject handoff = LoadObject(Path.Combine(closure, "source-only-handoff.json"));
        JsonObject reviewedInstructions = subject["handoff"]!["consumerInstructions"]!.AsObject();
        JsonObject deliveredInstructions = handoff["consumerInstructions"]!.AsObject();
        string[] expectedFields = ["mode", "installCommand", "verifyCommand", "designBytesRequiredFromFolders", "sourcePathRule"];

        reviewedInstructions.Select(item => item.Key).ShouldBe(expectedFields, ignoreOrder: true);
        deliveredInstructions.Select(item => item.Key).ShouldBe(expectedFields, ignoreOrder: true);
        deliveredInstructions.ToJsonString().ShouldBe(reviewedInstructions.ToJsonString());
        deliveredInstructions["installCommand"]!.GetValue<string>()
            .ShouldBe("python3 -m venv .oq8-python && .oq8-python/bin/python -m pip install --requirement requirements-oq8.txt");
        deliveredInstructions["verifyCommand"]!.GetValue<string>()
            .ShouldBe(".oq8-python/bin/python tools/validate-oq8-platform-evidence.py");

        string[] documents =
        [
            "docs/concepts/architecture-overview.md",
            "docs/concepts/command-lifecycle.md",
            "docs/guides/configuration-reference.md",
            "docs/reference/command-api.md",
        ];
        foreach (string relative in documents)
        {
            string text = File.ReadAllText(Path.Combine(root, relative));
            int create = text.IndexOf("python3 -m venv .oq8-python", StringComparison.Ordinal);
            int install = text.IndexOf(".oq8-python/bin/python -m pip install --requirement requirements-oq8.txt", StringComparison.Ordinal);
            int verify = text.IndexOf(".oq8-python/bin/python tools/validate-oq8-platform-evidence.py", StringComparison.Ordinal);
            create.ShouldBeGreaterThanOrEqualTo(0);
            install.ShouldBeGreaterThan(create);
            install.ShouldBeGreaterThanOrEqualTo(0);
            verify.ShouldBeGreaterThan(install);
        }
    }

    /// <summary>
    /// Verifies absent, broken, wrong-version, and shadowed YAML dependencies fail with bounded diagnostics.
    /// </summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("import-failure")]
    [InlineData("wrong-version")]
    [InlineData("shadowed-pinned-version")]
    public void InvalidPyYamlDependenciesFailClosed(string shape)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string shadow = Path.Combine(Path.GetTempPath(), "oq8-yaml-shadow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shadow);
        try
        {
            bool disableSitePackages = shape == "missing";
            if (!disableSitePackages)
            {
                File.WriteAllText(
                    Path.Combine(shadow, "yaml.py"),
                    shape switch
                    {
                        "import-failure" => "raise RuntimeError('hostile import detail must stay hidden')\n",
                        "wrong-version" => "__version__ = '0.0.0'\n",
                        "shadowed-pinned-version" => "__version__ = '6.0.3'\n",
                        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown dependency shape."),
                    });
            }

            (int exitCode, string output) = RunValidator(
                root,
                fixture,
                preReview: true,
                pythonPathPrefix: disableSitePackages ? null : shadow,
                disableSitePackages: disableSitePackages);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Pinned PyYAML 6.0.3 dependency is unavailable or untrusted");
            output.ShouldNotContain("Traceback");
            output.ShouldNotContain("hostile import detail");
            output.Length.ShouldBeLessThan(4096);
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(shadow, recursive: true);
        }
    }

    /// <summary>
    /// Verifies fresh capture validation requires exactly one explicit runtime while
    /// committed Story 4.14 evidence remains pinned to its observed runtime.
    /// </summary>
    [Fact]
    public void FreshAndCommittedRuntimeModesRemainExact()
    {
        string root = FindRepositoryRoot();
        string fixture = Path.Combine(Path.GetTempPath(), "oq8-runtime-mode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        string committed = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-4-14",
            "e60a3777c581d70b62f67173ccc2372b5b64a425",
            "observations.json");
        string fresh = Path.Combine(fixture, "observations.json");
        try
        {
            JsonObject observations = LoadObject(committed);
            observations["runtime"]!["dapr"] = "1.18.2";
            observations["runtime"]!["postgresImage"] = ReviewedPostgresImage;
            WriteObject(fresh, observations);

            (int freshExitCode, string freshOutput) = RunObservationValidator(root, fresh, "1.18.2", ReviewedPostgresImage);
            freshExitCode.ShouldBe(0, freshOutput);

            (int freshCrossModeExitCode, string freshCrossModeOutput) = RunObservationValidator(root, fresh, "1.18.1", ReviewedPostgresImage);
            freshCrossModeExitCode.ShouldBe(1, freshCrossModeOutput);
            freshCrossModeOutput.ShouldContain("Dapr runtime identity drift");

            (int committedExitCode, string committedOutput) = RunObservationValidator(root, committed, "1.18.1", HistoricalPostgresImage);
            committedExitCode.ShouldBe(0, committedOutput);

            (int committedCrossModeExitCode, string committedCrossModeOutput) = RunObservationValidator(root, committed, "1.18.2", HistoricalPostgresImage);
            committedCrossModeExitCode.ShouldBe(1, committedCrossModeOutput);
            committedCrossModeOutput.ShouldContain("Dapr runtime identity drift");

            string[] captureArguments =
            [
                "--capture-directory",
                fixture,
                "--ctrf",
                Path.Combine(Path.GetTempPath(), "oq8-runtime-missing-focused.json"),
                "--support-ctrf",
                Path.Combine(Path.GetTempPath(), "oq8-runtime-missing-support.json"),
            ];
            (int omittedExitCode, string omittedOutput) = RunValidator(
                root,
                root,
                additionalArguments: captureArguments);
            omittedExitCode.ShouldBe(1, omittedOutput);
            omittedOutput.ShouldContain("exactly one --expected-runtime-version");

            (int repeatedExitCode, string repeatedOutput) = RunValidator(
                root,
                root,
                additionalArguments:
                [
                    .. captureArguments,
                    "--expected-runtime-version",
                    "1.18.1",
                    "--expected-runtime-version",
                    "1.18.2",
                ]);
            repeatedExitCode.ShouldBe(1, repeatedOutput);
            repeatedOutput.ShouldContain("exactly one --expected-runtime-version");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies fresh observations fail closed on schema drift and hardened evidence values.
    /// </summary>
    /// <param name="mutation">The fresh observation mutation.</param>
    [Theory]
    [InlineData("top-level-extra")]
    [InlineData("top-level-missing")]
    [InlineData("nested-extra")]
    [InlineData("nested-missing")]
    [InlineData("postgres-image-drift")]
    [InlineData("postgres-image-identity")]
    [InlineData("counter-boolean")]
    [InlineData("counter-float")]
    [InlineData("counter-negative")]
    [InlineData("protected-before")]
    public void FreshObservationSchemaAndSemanticMutationsFailClosed(string mutation)
    {
        string root = FindRepositoryRoot();
        string fixture = Path.Combine(Path.GetTempPath(), "oq8-observation-schema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        string source = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-4-14",
            "e60a3777c581d70b62f67173ccc2372b5b64a425",
            "observations.json");
        string observationsPath = Path.Combine(fixture, "observations.json");
        try
        {
            JsonObject observations = LoadObject(source);
            observations["runtime"]!["dapr"] = "1.18.2";
            observations["runtime"]!["postgresImage"] = ReviewedPostgresImage;
            switch (mutation)
            {
                case "top-level-extra":
                    observations["unexpected"] = true;
                    break;
                case "top-level-missing":
                    observations.Remove("capturedOn");
                    break;
                case "nested-extra":
                    observations["observations"]!["capture"]!["before"]!["unexpected"] = 0;
                    break;
                case "nested-missing":
                    observations["runtime"]!.AsObject().Remove("postgresImage");
                    break;
                case "postgres-image-drift":
                    observations["runtime"]!["postgresImage"] = HistoricalPostgresImage;
                    break;
                case "postgres-image-identity":
                    observations["runtime"]!["postgresImageIdentity"] = "sha256:1234";
                    break;
                case "counter-boolean":
                    observations["topology"]!["eventStoreProcessCount"] = true;
                    break;
                case "counter-float":
                    observations["observations"]!["writers_failover"]!["concurrentRequests"] = 2.5;
                    break;
                case "counter-negative":
                    observations["observations"]!["capture"]!["after"]!["totalRows"] = -1;
                    break;
                case "protected-before":
                    observations["observations"]!["capture"]!["before"]!["protectedSentinelMatches"] = 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown observation mutation.");
            }

            WriteObject(observationsPath, observations);

            (int exitCode, string output) = RunObservationValidator(root, observationsPath, "1.18.2", ReviewedPostgresImage);

            exitCode.ShouldBe(1, output);
            output.ShouldContain(ExpectedObservationFailure(mutation));
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies pre-review validation cannot be combined with capture or support modes.
    /// </summary>
    /// <param name="mode">The conflicting validator mode.</param>
    [Theory]
    [InlineData("capture")]
    [InlineData("support")]
    public void PreReviewModeRejectsCaptureAndSupportArguments(string mode)
    {
        string root = FindRepositoryRoot();
        string[] arguments = mode switch
        {
            "capture" => ["--capture-directory", "unused-capture"],
            "support" => ["--support-output", "unused-support.json"],
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown validator mode."),
        };

        (int exitCode, string output) = RunValidator(
            root,
            root,
            preReview: true,
            additionalArguments: arguments);

        exitCode.ShouldBe(1, output);
        output.ShouldContain("Pre-review mode cannot be combined with capture, support, or lifecycle arguments");
        output.ShouldNotContain("Traceback");
    }

    /// <summary>
    /// Verifies receipt-independent candidate inputs in isolated artifact and Git fixtures.
    /// </summary>
    [Fact]
    public void PreReviewCandidateInputsPassInIsolation()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = CreateGitFixture(root);
        try
        {
            string closure = Path.Combine(
                fixture,
                "_bmad-output",
                "implementation-artifacts",
                "evidence",
                "story-4-15",
                LandedSource);
            File.Exists(Path.Combine(fixture, "_bmad-output", "implementation-artifacts", "4-8-eventstore-oq8-platform-evidence.yaml")).ShouldBeFalse();
            File.Exists(Path.Combine(closure, "closure-sha256.txt")).ShouldBeFalse();
            Directory.Exists(Path.Combine(closure, "reviews")).ShouldBeFalse();
            File.Exists(Path.Combine(closure, "source-only-handoff.json")).ShouldBeFalse();

            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(0, output);
            output.ShouldContain("OQ8 pre-review candidate validation passed.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies later unbound repository commits do not replace the pinned-path identity proof.
    /// </summary>
    [Fact]
    [Trait("OQ8Phase", "FinalOnly")]
    public void LaterUnboundRepositoryWorkPreservesPinnedPathVerification()
    {
        string root = FindRepositoryRoot();
        string head = RunGit(root, "rev-parse", "HEAD");
        head.ShouldNotBe(LandedSource);
        string fixture = CreateFixture(root);
        try
        {
            (int exitCode, string output) = RunValidator(root, fixture);

            exitCode.ShouldBe(0, output);
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies every critical evidence, source, subject, receipt, status, and documentation category fails closed on mutation.
    /// </summary>
    /// <param name="mutation">The critical contract mutation to apply.</param>
    [Theory]
    [InlineData("capture-matrix")]
    [InlineData("capture-artifact")]
    [InlineData("closure-manifest")]
    [InlineData("crosswalk-invariant")]
    [InlineData("crosswalk-invariant-reassignment")]
    [InlineData("crosswalk-evidence-body")]
    [InlineData("crosswalk-command-count")]
    [InlineData("crosswalk-command-count-type")]
    [InlineData("crosswalk-field-extra")]
    [InlineData("source-commit")]
    [InlineData("source-path-hash")]
    [InlineData("source-candidate-path-set")]
    [InlineData("source-current-path-set")]
    [InlineData("source-field-extra")]
    [InlineData("subject-design")]
    [InlineData("subject-binding")]
    [InlineData("subject-limitation")]
    [InlineData("subject-authority")]
    [InlineData("subject-field-extra")]
    [InlineData("review-decision")]
    [InlineData("review-subject")]
    [InlineData("review-reviewer")]
    [InlineData("review-role")]
    [InlineData("review-scope")]
    [InlineData("review-limitations")]
    [InlineData("review-findings")]
    [InlineData("review-findings-blank")]
    [InlineData("review-date")]
    [InlineData("review-authority")]
    [InlineData("review-external-repository-authority")]
    [InlineData("review-field-extra")]
    [InlineData("handoff-mode")]
    [InlineData("handoff-instruction-missing")]
    [InlineData("handoff-instruction-extra")]
    [InlineData("handoff-instruction-changed")]
    [InlineData("handoff-authority")]
    [InlineData("handoff-final-consumer-authority")]
    [InlineData("handoff-field-extra")]
    [InlineData("validator-digest")]
    [InlineData("story-status")]
    [InlineData("sprint-status-duplicate")]
    [InlineData("frontmatter-status-duplicate")]
    [InlineData("document-marker")]
    [InlineData("document-semantics")]
    [InlineData("platform-field-extra")]
    [InlineData("authority-field-extra")]
    [InlineData("duplicate-authority-handoff")]
    [InlineData("duplicate-authority-handoff-minified")]
    [InlineData("duplicate-authority-packet")]
    [InlineData("duplicate-authority-packet-minified")]
    [InlineData("malformed-json-crosswalk")]
    [InlineData("malformed-json-packet")]
    [InlineData("invalid-utf8-packet")]
    [InlineData("closure-manifest-malformed-line")]
    [Trait("OQ8Phase", "FinalOnly")]
    public void EvidenceDriftAndMissingApprovalFailClosed(string mutation)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            ApplyMutation(fixture, mutation);
            (int exitCode, string output) = RunValidator(root, fixture);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("OQ8 evidence validation failed:");
            output.ShouldContain(ExpectedFailure(mutation));
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies duplicate-authority injection is independent of JSON whitespace for both final artifact shapes.
    /// </summary>
    /// <param name="artifact">The final artifact shape represented by the fixture.</param>
    /// <param name="pretty">Whether the fixture uses indented JSON formatting.</param>
    [Theory]
    [InlineData("handoff", false)]
    [InlineData("handoff", true)]
    [InlineData("packet", false)]
    [InlineData("packet", true)]
    public void DuplicateAuthorityInjectorSupportsJsonFormatting(string artifact, bool pretty)
    {
        string json = (artifact, pretty) switch
        {
            ("handoff", false) => "{\"authority\":{\"eventStorePlatformComplete\":true}}",
            ("handoff", true) => "{\n  \"authority\": {\n    \"eventStorePlatformComplete\": true\n  }\n}",
            ("packet", false) => "{\"platformClosure\":{\"authority\":{\"eventStorePlatformComplete\":true}}}",
            ("packet", true) => "{\n  \"platformClosure\": {\n    \"authority\": {\n      \"eventStorePlatformComplete\": true\n    }\n  }\n}",
            _ => throw new ArgumentOutOfRangeException(nameof(artifact), artifact, "Unknown final artifact fixture."),
        };

        string duplicated = InjectDuplicateEventStorePlatformComplete(json);

        duplicated.ShouldNotBe(json);
        EventStorePlatformCompleteTrue.Matches(duplicated).Count.ShouldBe(2);
        string fixture = Path.Combine(Path.GetTempPath(), "oq8-duplicate-authority-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(fixture, duplicated);
            using Process process = CreatePythonProcess(
                """
                import importlib.util
                import pathlib
                import sys

                specification = importlib.util.spec_from_file_location("oq8_validator", sys.argv[1])
                validator = importlib.util.module_from_spec(specification)
                specification.loader.exec_module(validator)
                try:
                    validator.load_candidate_json(pathlib.Path(sys.argv[2]))
                except validator.EvidenceError as error:
                    print(str(error))
                    raise SystemExit(0)
                raise SystemExit(1)
                """);
            process.StartInfo.ArgumentList.Add(Path.Combine(FindRepositoryRoot(), "tools", "validate-oq8-platform-evidence.py"));
            process.StartInfo.ArgumentList.Add(fixture);

            (int exitCode, string output, bool timedOut) = RunProcess(process, 5_000);

            timedOut.ShouldBeFalse("Strict duplicate-JSON validation timed out.");
            exitCode.ShouldBe(0, output);
            output.Trim().ShouldBe("Duplicate JSON field");
        }
        finally
        {
            File.Delete(fixture);
        }
    }

    /// <summary>
    /// Verifies every external authority remains false even when all EventStore reviews approve the subject.
    /// </summary>
    /// <param name="authority">The forbidden authority field to overstate.</param>
    [Theory]
    [InlineData("releaseApproved")]
    [InlineData("foldersFinalClosure")]
    [InlineData("packageAuthority")]
    [InlineData("registryAuthority")]
    [InlineData("deploymentAuthority")]
    [InlineData("runtimePinAuthority")]
    [InlineData("consumerMigrationAuthority")]
    [InlineData("externalRepositoryAuthority")]
    [InlineData("finalConsumerAuthority")]
    [Trait("OQ8Phase", "FinalOnly")]
    public void OverstatedExternalAuthorityFailsClosed(string authority)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            string packetPath = Path.Combine(
                fixture,
                "_bmad-output",
                "implementation-artifacts",
                "4-8-eventstore-oq8-platform-evidence.yaml");
            JsonObject packet = LoadObject(packetPath);
            packet["platformClosure"]!["authority"]![authority] = true;
            WriteObject(packetPath, packet);

            (int exitCode, string output) = RunValidator(root, fixture);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("External authority overstated");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies every reviewed limitation is exact and cannot be replaced under a resealed subject.
    /// </summary>
    /// <param name="index">The limitation index to mutate.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [Trait("OQ8Phase", "FinalOnly")]
    public void EveryLimitationTextIsExact(int index)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            string artifacts = Path.Combine(fixture, "_bmad-output", "implementation-artifacts");
            string closure = Path.Combine(artifacts, "evidence", "story-4-15", LandedSource);
            string packetPath = Path.Combine(artifacts, "4-8-eventstore-oq8-platform-evidence.yaml");
            string limitationsPath = Path.Combine(closure, "limitations.json");
            string subjectPath = Path.Combine(closure, "review-subject.json");
            JsonObject limitations = LoadObject(limitationsPath);
            JsonObject subject = LoadObject(subjectPath);
            string mutated = $"Mutated limitation {index}.";
            limitations["limitations"]![index] = mutated;
            subject["limitations"]![index] = mutated;
            WriteObject(limitationsPath, limitations);
            subject["bindings"]!["limitations"]!["sha256"] = ComputeSha256(limitationsPath);
            WriteObject(subjectPath, subject);
            ResealClosureArtifact(packetPath, closure, "limitations.json");
            ResealClosureArtifact(packetPath, closure, "review-subject.json");

            (int exitCode, string output) = RunValidator(root, fixture);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Closure limitation text or order drift");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies receipt-independent candidate semantics fail closed without any final artifact.
    /// </summary>
    /// <param name="mutation">The candidate-core mutation.</param>
    [Theory]
    [InlineData("candidate-crosswalk-reassignment")]
    [InlineData("candidate-crosswalk-evidence-body")]
    [InlineData("candidate-crosswalk-count-type")]
    [InlineData("candidate-source-path-set")]
    [InlineData("candidate-subject-binding")]
    [InlineData("candidate-subject-test-binding")]
    [InlineData("candidate-subject-dependency-binding")]
    [InlineData("candidate-execution-validator")]
    [InlineData("candidate-execution-test-source")]
    [InlineData("candidate-execution-summary-type")]
    [InlineData("candidate-subject-date")]
    [InlineData("candidate-execution-date")]
    [InlineData("candidate-duplicate-subject")]
    [InlineData("candidate-duplicate-authority")]
    [InlineData("candidate-duplicate-execution")]
    [InlineData("candidate-nan-subject")]
    [InlineData("candidate-infinity-subject")]
    [InlineData("candidate-malformed-subject")]
    [InlineData("candidate-invalid-utf8-subject")]
    [InlineData("candidate-document-binding")]
    [InlineData("candidate-document-semantics")]
    [InlineData("candidate-document-stale-state")]
    [InlineData("candidate-story-status-done")]
    [InlineData("candidate-frontmatter-done")]
    [InlineData("candidate-focused-test-shape")]
    [InlineData("candidate-focused-schema-boolean")]
    [InlineData("candidate-focused-duration-boolean")]
    [InlineData("candidate-focused-duration-negative")]
    [InlineData("candidate-focused-duration-nonfinite")]
    [InlineData("candidate-commands-shape")]
    [InlineData("candidate-commands-duplicate-name")]
    [InlineData("candidate-commands-count-negative")]
    [InlineData("candidate-commands-command-identity")]
    [InlineData("candidate-environment-request-timeout")]
    [InlineData("candidate-environment-postgresql-projection")]
    [InlineData("candidate-environment-raw-postgresql")]
    [InlineData("candidate-execution-command-name-duplicate")]
    [InlineData("candidate-execution-command-count-zero")]
    [InlineData("candidate-execution-command-identity")]
    [InlineData("candidate-authority-capture")]
    [InlineData("candidate-authority-crosswalk")]
    [InlineData("candidate-authority-identity")]
    [InlineData("candidate-authority-limitations")]
    [InlineData("candidate-authority-execution")]
    [InlineData("candidate-authority-subject")]
    [InlineData("candidate-protected-capture")]
    [InlineData("candidate-private-path-capture")]
    [InlineData("candidate-protected-identity")]
    [InlineData("candidate-private-path-identity")]
    public void CandidateSemanticMutationsFailClosed(string mutation)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            ApplyCandidateMutation(fixture, mutation);

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("OQ8 evidence validation failed:");
            output.ShouldContain(ExpectedCandidateFailure(mutation));
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies raw xUnit CTRF sanitization rejects non-object records and boolean counts.
    /// </summary>
    /// <param name="mutation">The raw CTRF shape mutation.</param>
    /// <param name="expected">The stable fail-closed diagnostic.</param>
    [Theory]
    [InlineData("focused-record", "Focused CTRF contains an invalid test record")]
    [InlineData("focused-count", "Focused CTRF summary tests must be an exact integer")]
    [InlineData("support-record", "Deterministic support CTRF contains an invalid test record")]
    [InlineData("support-count", "Deterministic support CTRF summary tests must be an exact integer")]
    public void RawCtrfShapeMutationsFailClosed(string mutation, string expected)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(expected);
        string root = FindRepositoryRoot();
        string fixture = Path.Combine(Path.GetTempPath(), "oq8-ctrf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            bool support = mutation.StartsWith("support-", StringComparison.Ordinal);
            int expectedCount = support ? 33 : 1;
            JsonArray tests = [];
            for (int index = 0; index < expectedCount; index++)
            {
                tests.Add(true);
            }

            JsonObject summary = new()
            {
                ["tests"] = mutation.EndsWith("-count", StringComparison.Ordinal) ? true : expectedCount,
                ["passed"] = expectedCount,
                ["failed"] = 0,
                ["skipped"] = 0,
            };
            JsonObject ctrf = new()
            {
                ["results"] = new JsonObject
                {
                    ["summary"] = summary,
                    ["tests"] = tests,
                },
            };
            string input = Path.Combine(fixture, "input.json");
            string output = Path.Combine(fixture, "output.json");
            WriteObject(input, ctrf);

            (int exitCode, string processOutput) = RunCtrfSanitizer(root, input, output, support);

            exitCode.ShouldBe(1, processOutput);
            processOutput.ShouldContain(expected);
            processOutput.ShouldNotContain("Traceback");
            File.Exists(output).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies hostile duplicate-key names are never reflected in bounded validator output.
    /// </summary>
    [Fact]
    public void HostileDuplicateJsonKeyIsBoundedAndRedacted()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            string subjectPath = Path.Combine(CandidateClosure(fixture), "review-subject.json");
            string hostileKey = "PROTECTED-OQ8-RAW-SENTINEL-/home/private/" + new string('x', 50_000);
            string subject = File.ReadAllText(subjectPath);
            string prefix = $"{{\n  \"{hostileKey}\": false,\n  \"{hostileKey}\": true,\n";
            subject.StartsWith("{\n", StringComparison.Ordinal).ShouldBeTrue();
            File.WriteAllText(subjectPath, prefix + subject[2..]);

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Duplicate JSON field");
            output.Length.ShouldBeLessThan(4096);
            output.ShouldNotContain("PROTECTED-OQ8-RAW-SENTINEL");
            output.ShouldNotContain("/home/private/");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies every reviewed limitation is exact before any receipt exists.
    /// </summary>
    /// <param name="index">The limitation index to mutate.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CandidateLimitationTextIsExact(int index)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            string closure = CandidateClosure(fixture);
            string limitationsPath = Path.Combine(closure, "limitations.json");
            string subjectPath = Path.Combine(closure, "review-subject.json");
            JsonObject limitations = LoadObject(limitationsPath);
            JsonObject subject = LoadObject(subjectPath);
            string mutated = $"Mutated limitation {index}.";
            limitations["limitations"]![index] = mutated;
            subject["limitations"]![index] = mutated;
            WriteObject(limitationsPath, limitations);
            subject["bindings"]!["limitations"]!["sha256"] = ComputeSha256(limitationsPath);
            WriteObject(subjectPath, subject);

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Closure limitation text or order drift");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies every external authority is false in the frozen candidate subject.
    /// </summary>
    /// <param name="authority">The forbidden authority field to overstate.</param>
    [Theory]
    [InlineData("releaseApproved")]
    [InlineData("foldersFinalClosure")]
    [InlineData("packageAuthority")]
    [InlineData("registryAuthority")]
    [InlineData("deploymentAuthority")]
    [InlineData("runtimePinAuthority")]
    [InlineData("consumerMigrationAuthority")]
    [InlineData("externalRepositoryAuthority")]
    [InlineData("finalConsumerAuthority")]
    public void CandidateExternalAuthorityFailsClosed(string authority)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            string subjectPath = Path.Combine(CandidateClosure(fixture), "review-subject.json");
            JsonObject subject = LoadObject(subjectPath);
            subject["authority"]![authority] = true;
            WriteObject(subjectPath, subject);

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain($"External authority overstated: {authority}");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies every executable Epic 4 sprint status is unique and unambiguous and the retired key remains absent.
    /// </summary>
    /// <param name="key">The sprint-status key to duplicate.</param>
    /// <param name="conflictingStatus">A conflicting duplicate value.</param>
    [Theory]
    [InlineData("epic-4", "done")]
    [InlineData("4-8-durable-admission-evidence-ledger", "done")]
    [InlineData("4-9-trusted-admission-contract-and-protected-identity", "in-progress")]
    [InlineData("4-10-digest-directory-rotation-and-key-retirement", "in-progress")]
    [InlineData("4-11-admission-state-machine-and-current-fence-enforcement", "in-progress")]
    [InlineData("4-12-expiry-compaction-and-tombstone-retention", "in-progress")]
    [InlineData("4-13-legacy-admission-migration-and-fail-closed-reconciliation", "in-progress")]
    [InlineData("4-14-oq8-multi-host-production-evidence", "in-progress")]
    [InlineData("4-15-oq8-platform-closure-and-handoff", "in-progress")]
    public void RequiredSprintStatusMustBeUnique(string key, string conflictingStatus)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            if (key == "4-8-durable-admission-evidence-ledger")
            {
                InsertRetiredSprintStatus(fixture, "plain");
            }
            else
            {
                string statusPath = Path.Combine(
                    fixture,
                    "_bmad-output",
                    "implementation-artifacts",
                    "sprint-status.yaml");
                List<string> lines = File.ReadAllLines(statusPath).ToList();
                int mapping = lines.FindIndex(line => line == "development_status:");
                mapping.ShouldBeGreaterThanOrEqualTo(0);
                lines.Insert(mapping + 1, $"  {key}: {conflictingStatus}");
                File.WriteAllLines(statusPath, lines);
            }

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain(
                key == "4-8-durable-admission-evidence-ledger"
                    ? "Retired lifecycle key is forbidden: 4-8-durable-admission-evidence-ledger"
                    : $"Lifecycle status is missing or ambiguous: {key}");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies every supported YAML spelling of the retired Story 4.8 direct entry fails closed.
    /// </summary>
    /// <param name="shape">The retired-entry YAML shape.</param>
    [Theory]
    [InlineData("plain")]
    [InlineData("double-quoted-key")]
    [InlineData("single-quoted-key")]
    [InlineData("colon-spacing")]
    [InlineData("alternate-indentation")]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("comment-only")]
    [InlineData("quoted-value")]
    [InlineData("flow-sequence")]
    [InlineData("flow-mapping")]
    [InlineData("block-sequence")]
    [InlineData("tagged-value")]
    [InlineData("anchored-value")]
    [InlineData("aliased-value")]
    [InlineData("unicode-colon-spacing")]
    [InlineData("escaped-key-x")]
    [InlineData("escaped-key-u")]
    [InlineData("escaped-key-U")]
    public void RetiredSprintStatusYamlShapesFailClosed(string shape)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            InsertRetiredSprintStatus(fixture, shape);

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Retired lifecycle key is forbidden: 4-8-durable-admission-evidence-ledger");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies supported single-quoted and YAML double-quoted active statuses are decoded exactly.
    /// </summary>
    /// <param name="shape">The supported active-entry YAML shape.</param>
    [Theory]
    [InlineData("single-quoted-scalars")]
    [InlineData("single-quoted-escape")]
    [InlineData("double-quoted-escapes")]
    [InlineData("sole-explicit-development-status")]
    public void SupportedActiveSprintStatusYamlPasses(string shape)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = CreateGitFixture(root);
        try
        {
            ReplaceActiveSprintStatus(fixture, shape);

            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(0, output);
            output.ShouldContain("OQ8 pre-review candidate validation passed.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies malformed separators, Unicode whitespace, and unsupported active values fail closed.
    /// </summary>
    /// <param name="shape">The unsupported active-entry YAML shape.</param>
    [Theory]
    [InlineData("missing-separator")]
    [InlineData("tab-separator")]
    [InlineData("unicode-separator")]
    [InlineData("unicode-leading-whitespace")]
    [InlineData("malformed-single-quoted-key")]
    [InlineData("malformed-single-quoted-value")]
    [InlineData("empty-value")]
    [InlineData("null-value")]
    [InlineData("tagged-value")]
    [InlineData("anchored-value")]
    [InlineData("aliased-value")]
    [InlineData("flow-sequence-value")]
    [InlineData("flow-mapping-value")]
    [InlineData("block-scalar-value")]
    [InlineData("unrelated-sequence-token")]
    [InlineData("unrelated-mapping-token")]
    [InlineData("unrelated-directive-token")]
    public void UnsupportedActiveSprintStatusYamlFailsClosed(string shape)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            ReplaceActiveSprintStatus(fixture, shape);

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Unsupported sprint-status mapping structure");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies near-match keys and retired-key text outside development_status do not affect lifecycle validation.
    /// </summary>
    /// <param name="shape">The non-entry text shape.</param>
    [Theory]
    [InlineData("near-match")]
    [InlineData("outside-mapping")]
    [InlineData("comment")]
    [InlineData("document-markers")]
    [InlineData("unrelated-top-level-structure")]
    [InlineData("stream-initial-bom")]
    [InlineData("unrelated-top-level-alias")]
    [InlineData("unrelated-top-level-tag-anchor-alias")]
    [InlineData("unrelated-top-level-anchor-tag-alias")]
    [InlineData("unrelated-top-level-uri-tag-anchor-alias")]
    [InlineData("balanced-top-level-flow-sequence")]
    [InlineData("balanced-top-level-flow-mapping")]
    public void RetiredSprintStatusTextOutsideExactDirectEntryPasses(string shape)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = CreateGitFixture(root);
        try
        {
            InsertNonRetiredSprintStatusText(fixture, shape);

            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(0, output);
            output.ShouldContain("OQ8 pre-review candidate validation passed.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies YAML merge keys, sequence-shaped content, and ambiguous development-status shapes fail closed.
    /// </summary>
    /// <param name="shape">The unsupported mapping shape.</param>
    [Theory]
    [InlineData("merge-key")]
    [InlineData("sequence")]
    [InlineData("tagged-retired-key")]
    [InlineData("anchored-retired-key")]
    [InlineData("tagged-development-status")]
    [InlineData("anchored-development-status")]
    [InlineData("tagged-anchored-development-status")]
    [InlineData("anchored-tagged-development-status")]
    [InlineData("uri-tagged-development-status")]
    [InlineData("tagged-escaped-development-status")]
    [InlineData("anchored-escaped-development-status")]
    [InlineData("uri-tagged-escaped-development-status")]
    [InlineData("tagged-anchored-escaped-development-status")]
    [InlineData("explicit-development-status")]
    [InlineData("explicit-escaped-development-status")]
    [InlineData("explicit-tagged-escaped-development-status")]
    [InlineData("multiline-explicit-development-status")]
    [InlineData("multiline-explicit-escaped-development-status")]
    [InlineData("multiline-explicit-commented-development-status")]
    [InlineData("multiline-explicit-property-development-status")]
    [InlineData("literal-explicit-development-status")]
    [InlineData("folded-explicit-development-status")]
    [InlineData("continued-quoted-explicit-development-status")]
    [InlineData("implicit-aliased-development-status")]
    [InlineData("spaced-implicit-aliased-development-status")]
    [InlineData("punctuated-implicit-aliased-development-status")]
    [InlineData("duplicate-development-status")]
    [InlineData("deeper-indented-retired-key")]
    [InlineData("normalized-duplicate-x")]
    [InlineData("normalized-duplicate-u")]
    [InlineData("normalized-duplicate-U")]
    [InlineData("multiple-documents")]
    [InlineData("inline-document-start")]
    [InlineData("indented-document-start")]
    [InlineData("indented-inline-document-start")]
    [InlineData("inline-document-end")]
    [InlineData("indented-document-end")]
    [InlineData("non-initial-bom")]
    [InlineData("non-printable-source")]
    [InlineData("hostile-duplicate-key")]
    [InlineData("duplicate-unrelated-key")]
    [InlineData("tagged-development-status-inline")]
    [InlineData("tagged-anchored-development-status-inline")]
    [InlineData("anchored-tagged-development-status-inline")]
    [InlineData("aliased-development-status-inline")]
    [InlineData("malformed-top-level-token")]
    [InlineData("malformed-top-level-scalar")]
    [InlineData("indented-shadow-development-status")]
    [InlineData("unclosed-top-level-flow")]
    [InlineData("balanced-top-level-flow-double-comma-sequence")]
    [InlineData("balanced-top-level-flow-leading-comma-sequence")]
    [InlineData("balanced-top-level-flow-double-comma-mapping")]
    [InlineData("nested-aliased-development-status-block")]
    [InlineData("nested-aliased-development-status-inline")]
    [InlineData("nested-flow-development-status-literal")]
    [InlineData("nested-flow-development-status-escaped")]
    [InlineData("nested-flow-development-status-anchor-alias")]
    [InlineData("nested-sequence-development-status")]
    [InlineData("nested-sequence-development-status-escaped")]
    [InlineData("nested-double-sequence-development-status")]
    [InlineData("nested-double-sequence-development-status-escaped")]
    [InlineData("nested-triple-sequence-development-status")]
    [InlineData("nested-triple-sequence-development-status-escaped")]
    [InlineData("nested-explicit-sequence-development-status")]
    [InlineData("nested-explicit-sequence-development-status-escaped")]
    [InlineData("nested-unclosed-quoted-scalar")]
    [InlineData("malformed-explicit-key-unclosed-flow")]
    [InlineData("multiple-tag-properties")]
    [InlineData("anchored-root-mapping")]
    [InlineData("tagged-root-mapping")]
    [InlineData("tagged-anchored-root-mapping")]
    [InlineData("oversized-source")]
    public void UnsupportedSprintStatusYamlFailsClosed(string shape)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            InsertUnsupportedSprintStatus(fixture, shape);

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain(
                shape switch
                {
                    "merge-key" => "Sprint-status merge keys are forbidden",
                    "duplicate-development-status" or "tagged-development-status" or
                        "anchored-development-status" or "tagged-anchored-development-status" or
                        "anchored-tagged-development-status" or "uri-tagged-development-status" or
                        "tagged-escaped-development-status" or "anchored-escaped-development-status" or
                        "uri-tagged-escaped-development-status" or "tagged-anchored-escaped-development-status" or
                        "explicit-development-status" or "explicit-escaped-development-status" or
                        "explicit-tagged-escaped-development-status" or "multiline-explicit-development-status" or
                        "multiline-explicit-escaped-development-status" or
                        "multiline-explicit-commented-development-status" or
                        "multiline-explicit-property-development-status" or
                        "literal-explicit-development-status" or "folded-explicit-development-status" or
                        "continued-quoted-explicit-development-status" or
                        "implicit-aliased-development-status" or "spaced-implicit-aliased-development-status" or
                        "tagged-development-status-inline" or "tagged-anchored-development-status-inline" or
                        "anchored-tagged-development-status-inline" or "aliased-development-status-inline" =>
                        "Lifecycle development_status mapping is missing or ambiguous",
                    "tagged-retired-key" or "anchored-retired-key" =>
                        "Retired lifecycle key is forbidden: 4-8-durable-admission-evidence-ledger",
                    "normalized-duplicate-x" or "normalized-duplicate-u" or "normalized-duplicate-U" =>
                        "Lifecycle status is missing or ambiguous: epic-4",
                    "multiple-documents" =>
                        "Sprint-status YAML stream must contain exactly one document",
                    "inline-document-start" => "Sprint-status YAML stream must contain exactly one document",
                    "non-initial-bom" => "Sprint-status BOM is only permitted at stream start",
                    "non-printable-source" => "Sprint-status YAML source contains forbidden characters",
                    "oversized-source" => "Sprint-status YAML source exceeds the bounded size limit",
                    "duplicate-unrelated-key" => "Lifecycle status mapping contains a duplicate key",
                    _ => "Unsupported sprint-status mapping structure",
                });
            output.ShouldNotContain("Traceback");
            if (shape == "hostile-duplicate-key")
            {
                output.Length.ShouldBeLessThan(4096);
                output.ShouldNotContain(new string('x', 128));
            }
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies removing a required active status preserves the key-specific missing diagnostic.
    /// </summary>
    [Fact]
    public void MissingRequiredSprintStatusFailsClosed()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            const string key = "4-14-oq8-multi-host-production-evidence";
            string statusPath = Path.Combine(fixture, "_bmad-output", "implementation-artifacts", "sprint-status.yaml");
            string[] lines = File.ReadAllLines(statusPath);
            int match = Array.FindIndex(lines, line => line.StartsWith($"  {key}:", StringComparison.Ordinal));
            match.ShouldBeGreaterThanOrEqualTo(0);
            File.WriteAllLines(statusPath, lines.Where((_, index) => index != match));

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain($"Lifecycle status is missing or ambiguous: {key}");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the final BMad lifecycle requires sprint review rather than a terminal or implementation state.
    /// </summary>
    /// <param name="drift">The forbidden final sprint status.</param>
    [Theory]
    [InlineData("done")]
    [InlineData("in-progress")]
    [InlineData("backlog")]
    public void FinalLifecycleRequiresSprintReview(string drift)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            SetFinalLifecycle(fixture);
            string statusPath = Path.Combine(
                fixture,
                "_bmad-output",
                "implementation-artifacts",
                "sprint-status.yaml");
            File.WriteAllText(
                statusPath,
                File.ReadAllText(statusPath).Replace(
                    "  4-15-oq8-platform-closure-and-handoff: review",
                    $"  4-15-oq8-platform-closure-and-handoff: {drift}",
                    StringComparison.Ordinal));

            (int exitCode, string output) = RunValidator(root, fixture, lifecycleMode: "final");

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Lifecycle status drift: 4-15-oq8-platform-closure-and-handoff");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the exact BMad final lifecycle gate accepts sprint review with completed spec metadata.
    /// </summary>
    [Fact]
    public void FinalLifecycleReviewPassesInIsolation()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            SetFinalLifecycle(fixture);

            (int exitCode, string output) = RunValidator(root, fixture, lifecycleMode: "final");

            exitCode.ShouldBe(0, output);
            output.ShouldContain("OQ8 final lifecycle validation passed.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies v1 source bindings resolve from the completed historical snapshot, not later worktree bytes.
    /// </summary>
    /// <param name="mutation">The isolated Git worktree mutation.</param>
    [Theory]
    [InlineData("changed")]
    [InlineData("deleted")]
    public void ChangedOrDeletedLaterWorktreePathDoesNotRewriteHistoricalV1(string mutation)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = CreateGitFixture(root);
        try
        {
            string boundPath = Path.Combine(gitFixture, "deploy", "dapr", "resiliency.yaml");
            if (mutation == "changed")
            {
                File.AppendAllText(boundPath, "\n# semantic drift\n");
                RunGit(gitFixture, "add", "--", "deploy/dapr/resiliency.yaml");
            }
            else
            {
                File.Delete(boundPath);
            }

            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(0, output);
            output.ShouldContain("OQ8 pre-review candidate validation passed.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies current index visibility flags cannot alter historical v1 snapshot resolution.
    /// </summary>
    /// <param name="flag">The forbidden Git index visibility flag.</param>
    [Theory]
    [InlineData("--assume-unchanged")]
    [InlineData("--skip-worktree")]
    public void CurrentIndexVisibilityFlagsDoNotAlterHistoricalV1(string flag)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = CreateGitFixture(root);
        try
        {
            RunGit(gitFixture, "update-index", flag, "--", "deploy/dapr/resiliency.yaml");

            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(0, output);
            output.ShouldContain("OQ8 pre-review candidate validation passed.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies historical v1 validation resolves the named snapshot independently of current HEAD.
    /// </summary>
    [Fact]
    public void NonDescendantCurrentHeadDoesNotReplaceHistoricalV1Snapshot()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = CreateGitFixture(root);
        try
        {
            RunGit(gitFixture, "checkout", "--detach", "--quiet", "e60a3777c581d70b62f67173ccc2372b5b64a425");

            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(0, output);
            output.ShouldContain("OQ8 pre-review candidate validation passed.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies local Git replacement refs cannot substitute bytes in the landed-tree proof.
    /// </summary>
    [Fact]
    public void ReplacementRefCannotAlterLandedIdentityProof()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = CreateGitFixture(root);
        try
        {
            string originalHead = RunGit(gitFixture, "rev-parse", "HEAD");
            RunGit(gitFixture, "checkout", "--detach", "--quiet", LandedSource);
            string boundPath = Path.Combine(gitFixture, "deploy", "dapr", "resiliency.yaml");
            File.AppendAllText(boundPath, "\n# replacement-only drift\n");
            RunGit(gitFixture, "add", "--", "deploy/dapr/resiliency.yaml");
            string replacementTree = RunGit(gitFixture, "write-tree");
            RunGit(gitFixture, "reset", "--hard", "--quiet", "HEAD");
            string landedTree = RunGit(gitFixture, "rev-parse", $"{LandedSource}^{{tree}}");
            RunGit(gitFixture, "replace", landedTree, replacementTree);
            RunGit(gitFixture, "checkout", "--detach", "--quiet", originalHead);

            RunGit(gitFixture, "show", $"{LandedSource}:deploy/dapr/resiliency.yaml")
                .ShouldContain("replacement-only drift");

            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(0, output);
            output.ShouldContain("OQ8 pre-review candidate validation passed.");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies missing status, spec, documentation, configuration, evidence, and closure paths fail without a traceback.
    /// </summary>
    /// <param name="relative">The required fixture path to remove.</param>
    /// <param name="expected">The bounded validation failure fragment.</param>
    [Theory]
    [InlineData("_bmad-output/implementation-artifacts/sprint-status.yaml", "Cannot read evidence path")]
    [InlineData("_bmad-output/implementation-artifacts/spec-4-12-expiry-compaction-and-tombstone-retention.md", "Cannot hash evidence path")]
    [InlineData("docs/concepts/architecture-overview.md", "Cannot read evidence path")]
    [InlineData("deploy/dapr/resiliency.yaml", "Cannot hash evidence path")]
    [InlineData("_bmad-output/implementation-artifacts/evidence/story-4-14/e60a3777c581d70b62f67173ccc2372b5b64a425/observations.json", "Manifest artifact missing: observations.json")]
    [InlineData("_bmad-output/implementation-artifacts/evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4/reviews/security.json", "Closure artifact missing: reviews/security.json")]
    [Trait("OQ8Phase", "FinalOnly")]
    public void MissingRequiredPathFailsSafely(string relative, string expected)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateFixture(root);
        try
        {
            File.Delete(Path.Combine(fixture, relative));

            (int exitCode, string output) = RunValidator(root, fixture);

            exitCode.ShouldBe(1, output);
            output.ShouldContain(expected);
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies missing or unreadable candidate paths are reported without leaking fixture paths.
    /// </summary>
    /// <param name="mode">Whether the required path is missing or unreadable.</param>
    [Theory]
    [InlineData("missing")]
    [InlineData("unreadable")]
    public void CandidatePathFailureIsBoundedAndRedacted(string mode)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        try
        {
            string documentPath = Path.Combine(fixture, "docs", "concepts", "architecture-overview.md");
            File.Delete(documentPath);
            if (mode == "unreadable")
            {
                Directory.CreateDirectory(documentPath);
            }

            (int exitCode, string output) = RunValidator(root, fixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Cannot read evidence path docs/concepts/architecture-overview.md");
            output.ShouldNotContain(fixture);
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a Git subprocess failure is reported as bounded evidence output.
    /// </summary>
    [Fact]
    public void InvalidGitRootFailsSafely()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = Path.Combine(Path.GetTempPath(), "oq8-git-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(gitFixture);
        try
        {
            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Git historical-blob identity proof failed");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a timed-out Git identity subprocess becomes bounded evidence output.
    /// </summary>
    [Fact]
    public void GitSubprocessTimeoutFailsSafely()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string executableFixture = Path.Combine(Path.GetTempPath(), "oq8-git-timeout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executableFixture);
        try
        {
            string gitPath = Path.Combine(executableFixture, "git");
            File.WriteAllText(gitPath, "#!/bin/sh\nwhile :; do :; done\n");
            File.SetUnixFileMode(
                gitPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            (int exitCode, string output) = RunValidator(
                root,
                fixture,
                root,
                preReview: true,
                gitTimeoutSeconds: 0.1,
                executablePathPrefix: executableFixture);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Git historical-blob identity proof");
            output.ShouldContain("timed out");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(executableFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies Git output flooding is bounded before it can exhaust validator memory.
    /// </summary>
    [Fact]
    public void GitSubprocessOutputFloodFailsSafely()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string executableFixture = Path.Combine(Path.GetTempPath(), "oq8-git-flood-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executableFixture);
        try
        {
            string gitPath = Path.Combine(executableFixture, "git");
            File.WriteAllText(
                gitPath,
                "#!/bin/sh\nwhile :; do printf '0123456789abcdef0123456789abcdef'; printf 'fedcba9876543210fedcba9876543210' >&2; done\n");
            File.SetUnixFileMode(
                gitPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            (int exitCode, string output) = RunValidator(
                root,
                fixture,
                root,
                preReview: true,
                executablePathPrefix: executableFixture);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Git historical-blob identity proof");
            output.ShouldContain("exceeded output limit");
            output.ShouldNotContain("Traceback");
            output.Length.ShouldBeLessThan(4096);
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(executableFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the process harness kills a timed-out child while draining both redirected streams.
    /// </summary>
    [Fact]
    public void ProcessHarnessEnforcesTimeoutWithoutRedirectDeadlock()
    {
        using Process process = CreatePythonProcess(
            "import sys,time; print('stdout-start', flush=True); print('stderr-start', file=sys.stderr, flush=True); time.sleep(10)");

        (int _, string output, bool timedOut) = RunProcess(process, 100);

        timedOut.ShouldBeTrue();
        output.ShouldContain("stdout-start");
        output.ShouldContain("stderr-start");
    }

    /// <summary>
    /// Verifies large stdout and stderr payloads are drained concurrently without blocking the child.
    /// </summary>
    [Fact]
    public void ProcessHarnessDrainsLargeStdoutAndStderr()
    {
        using Process process = CreatePythonProcess(
            "import sys; sys.stdout.write('o' * 1048576); sys.stderr.write('e' * 1048576)");

        (int exitCode, string output, bool timedOut) = RunProcess(process, 30_000);

        timedOut.ShouldBeFalse();
        exitCode.ShouldBe(0);
        output.Length.ShouldBe(2 * 1024 * 1024);
        output.Count(character => character == 'o').ShouldBe(1024 * 1024);
        output.Count(character => character == 'e').ShouldBe(1024 * 1024);
    }

    private static string CandidateClosure(string fixture) => Path.Combine(
        fixture,
        "_bmad-output",
        "implementation-artifacts",
        "evidence",
        "story-4-15",
        LandedSource);

    private static void ApplyCandidateMutation(string fixture, string mutation)
    {
        string artifacts = Path.Combine(fixture, "_bmad-output", "implementation-artifacts");
        string closure = CandidateClosure(fixture);
        string subjectPath = Path.Combine(closure, "review-subject.json");
        string executionPath = Path.Combine(closure, "pre-review-execution.json");
        switch (mutation)
        {
            case "candidate-crosswalk-reassignment":
            {
                string path = Path.Combine(closure, "closure-crosswalk.json");
                JsonObject crosswalk = LoadObject(path);
                crosswalk["invariants"]![0]!["stories"]![0] = "4.10";
                WriteObject(path, crosswalk);
                break;
            }
            case "candidate-crosswalk-evidence-body":
            {
                const string relative = "_bmad-output/implementation-artifacts/spec-4-11-admission-state-machine-and-current-fence-enforcement.md";
                string evidencePath = Path.Combine(fixture, relative);
                File.AppendAllText(evidencePath, "\nCandidate evidence body drift.\n");
                string crosswalkPath = Path.Combine(closure, "closure-crosswalk.json");
                JsonObject crosswalk = LoadObject(crosswalkPath);
                crosswalk["evidenceBindings"]![relative] = ComputeSha256(evidencePath);
                WriteObject(crosswalkPath, crosswalk);
                break;
            }
            case "candidate-crosswalk-count-type":
            {
                string path = Path.Combine(closure, "closure-crosswalk.json");
                JsonObject crosswalk = LoadObject(path);
                crosswalk["verification"]!["successfulCommands"] = true;
                WriteObject(path, crosswalk);
                break;
            }
            case "candidate-source-path-set":
            {
                string path = Path.Combine(closure, "source-artifact-identity.json");
                JsonObject identity = LoadObject(path);
                identity["currentVerification"]!["boundPaths"]!.AsArray().RemoveAt(0);
                WriteObject(path, identity);
                break;
            }
            case "candidate-subject-binding":
            {
                JsonObject subject = LoadObject(subjectPath);
                subject["bindings"]!["closureCrosswalk"]!["sha256"] = new string('0', 64);
                WriteObject(subjectPath, subject);
                break;
            }
            case "candidate-subject-test-binding":
            {
                JsonObject subject = LoadObject(subjectPath);
                subject["bindings"]!["closureTests"]!["sha256"] = new string('0', 64);
                WriteObject(subjectPath, subject);
                break;
            }
            case "candidate-subject-dependency-binding":
            {
                JsonObject subject = LoadObject(subjectPath);
                subject["bindings"]!["validatorRequirements"]!["sha256"] = new string('0', 64);
                WriteObject(subjectPath, subject);
                break;
            }
            case "candidate-test-source-body":
                File.AppendAllText(
                    Path.Combine(fixture, "tests", "Hexalith.EventStore.Contracts.Tests", "Packaging", "Oq8PlatformClosureTests.cs"),
                    "\n// Candidate test drift.\n");
                break;
            case "candidate-execution-validator":
            {
                JsonObject execution = LoadObject(executionPath);
                execution["validator"]!["sha256"] = new string('0', 64);
                WriteObject(executionPath, execution);
                break;
            }
            case "candidate-execution-test-source":
            {
                JsonObject execution = LoadObject(executionPath);
                execution["testSource"]!["sha256"] = new string('0', 64);
                WriteObject(executionPath, execution);
                break;
            }
            case "candidate-execution-summary-type":
            {
                JsonObject execution = LoadObject(executionPath);
                execution["summary"]!["tests"] = true;
                WriteObject(executionPath, execution);
                break;
            }
            case "candidate-execution-command-name-duplicate":
            {
                JsonObject execution = LoadObject(executionPath);
                execution["commands"]![1]!["name"] = execution["commands"]![0]!["name"]!.GetValue<string>();
                WriteObject(executionPath, execution);
                break;
            }
            case "candidate-execution-command-count-zero":
            {
                JsonObject execution = LoadObject(executionPath);
                execution["commands"]![3]!["tests"] = 0;
                WriteObject(executionPath, execution);
                break;
            }
            case "candidate-execution-command-identity":
            {
                JsonObject execution = LoadObject(executionPath);
                execution["commands"]![0]!["command"] = "python3 -m py_compile another-validator.py";
                WriteObject(executionPath, execution);
                break;
            }
            case "candidate-subject-date":
            {
                JsonObject subject = LoadObject(subjectPath);
                subject["createdOn"] = PreviousCalendarDay(subject["createdOn"]!.GetValue<string>());
                WriteObject(subjectPath, subject);
                break;
            }
            case "candidate-execution-date":
            {
                JsonObject execution = LoadObject(executionPath);
                execution["executedOn"] = PreviousCalendarDay(execution["executedOn"]!.GetValue<string>());
                WriteObject(executionPath, execution);
                ResealCandidateBinding(fixture, "preReviewExecution");
                break;
            }
            case "candidate-duplicate-subject":
                InsertDuplicateJsonField(subjectPath, "  \"createdOn\": \"2026-08-27\",", "  \"createdOn\": \"2026-08-27\",\n  \"createdOn\": \"2026-08-27\",");
                break;
            case "candidate-duplicate-authority":
                InsertDuplicateJsonField(subjectPath, "    \"releaseApproved\": false,", "    \"releaseApproved\": false,\n    \"releaseApproved\": false,");
                break;
            case "candidate-duplicate-execution":
                InsertDuplicateJsonField(executionPath, "  \"scope\": \"receipt-independent-isolated-candidate\",", "  \"scope\": \"receipt-independent-isolated-candidate\",\n  \"scope\": \"receipt-independent-isolated-candidate\",");
                break;
            case "candidate-nan-subject":
                InsertDuplicateJsonField(subjectPath, "{\n", "{\n  \"nonFinite\": NaN,\n");
                break;
            case "candidate-infinity-subject":
                InsertDuplicateJsonField(subjectPath, "{\n", "{\n  \"nonFinite\": Infinity,\n");
                break;
            case "candidate-malformed-subject":
                File.WriteAllText(subjectPath, "{");
                break;
            case "candidate-invalid-utf8-subject":
                File.WriteAllBytes(subjectPath, [0xff, 0xfe, 0xfd]);
                break;
            case "candidate-document-binding":
                File.AppendAllText(Path.Combine(fixture, "docs", "concepts", "command-lifecycle.md"), "\nBound document drift.\n");
                break;
            case "candidate-document-semantics":
            {
                const string relative = "docs/reference/command-api.md";
                string documentPath = Path.Combine(fixture, relative);
                File.WriteAllText(
                    documentPath,
                    File.ReadAllText(documentPath).Replace("no release approval", "release approval", StringComparison.Ordinal));
                JsonObject subject = LoadObject(subjectPath);
                subject["reviewedPublicDocs"]![relative] = ComputeSha256(documentPath);
                WriteObject(subjectPath, subject);
                break;
            }
            case "candidate-document-stale-state":
            {
                const string relative = "docs/reference/command-api.md";
                string documentPath = Path.Combine(fixture, relative);
                File.AppendAllText(documentPath, "\nLegacy text: source-only handoff candidate.\n");
                JsonObject subject = LoadObject(subjectPath);
                subject["reviewedPublicDocs"]![relative] = ComputeSha256(documentPath);
                WriteObject(subjectPath, subject);
                break;
            }
            case "candidate-story-status-done":
            {
                string path = Path.Combine(artifacts, "sprint-status.yaml");
                File.WriteAllText(
                    path,
                    File.ReadAllText(path).Replace(
                        "  4-15-oq8-platform-closure-and-handoff: in-progress",
                        "  4-15-oq8-platform-closure-and-handoff: done",
                        StringComparison.Ordinal));
                break;
            }
            case "candidate-frontmatter-done":
            {
                string path = Path.Combine(artifacts, "spec-4-15-oq8-platform-closure-and-handoff.md");
                File.WriteAllText(
                    path,
                    File.ReadAllText(path).Replace("status: 'in-review'", "status: 'done'", StringComparison.Ordinal));
                break;
            }
            case "candidate-focused-test-shape":
            {
                string path = Path.Combine(
                    artifacts,
                    "evidence",
                    "story-4-14",
                    "e60a3777c581d70b62f67173ccc2372b5b64a425",
                    "test-results.json");
                JsonObject result = LoadObject(path);
                result["test"] = new JsonArray();
                WriteObject(path, result);
                ResealCandidateCapture(fixture, "test-results.json");
                break;
            }
            case "candidate-focused-schema-boolean":
            {
                string path = Path.Combine(
                    artifacts,
                    "evidence",
                    "story-4-14",
                    "e60a3777c581d70b62f67173ccc2372b5b64a425",
                    "test-results.json");
                JsonObject result = LoadObject(path);
                result["schemaVersion"] = true;
                WriteObject(path, result);
                ResealCandidateCapture(fixture, "test-results.json");
                break;
            }
            case "candidate-focused-duration-boolean":
            case "candidate-focused-duration-negative":
            {
                string path = Path.Combine(
                    artifacts,
                    "evidence",
                    "story-4-14",
                    "e60a3777c581d70b62f67173ccc2372b5b64a425",
                    "test-results.json");
                JsonObject result = LoadObject(path);
                if (mutation == "candidate-focused-duration-boolean")
                {
                    result["test"]!["durationMilliseconds"] = true;
                }
                else
                {
                    result["test"]!["durationMilliseconds"] = -1;
                }

                WriteObject(path, result);
                ResealCandidateCapture(fixture, "test-results.json");
                break;
            }
            case "candidate-focused-duration-nonfinite":
            {
                string path = Path.Combine(
                    artifacts,
                    "evidence",
                    "story-4-14",
                    "e60a3777c581d70b62f67173ccc2372b5b64a425",
                    "test-results.json");
                InsertDuplicateJsonField(
                    path,
                    "    \"durationMilliseconds\": 10843,",
                    "    \"durationMilliseconds\": NaN,");
                ResealCandidateCapture(fixture, "test-results.json");
                break;
            }
            case "candidate-commands-shape":
            {
                string path = Path.Combine(
                    artifacts,
                    "evidence",
                    "story-4-14",
                    "e60a3777c581d70b62f67173ccc2372b5b64a425",
                    "commands.json");
                JsonObject commands = LoadObject(path);
                commands["commands"] = new JsonArray(0);
                WriteObject(path, commands);
                ResealCandidateCapture(fixture, "commands.json");
                break;
            }
            case "candidate-commands-duplicate-name":
            case "candidate-commands-count-negative":
            case "candidate-commands-command-identity":
            {
                string path = Path.Combine(
                    artifacts,
                    "evidence",
                    "story-4-14",
                    "e60a3777c581d70b62f67173ccc2372b5b64a425",
                    "commands.json");
                JsonObject commands = LoadObject(path);
                if (mutation == "candidate-commands-duplicate-name")
                {
                    commands["commands"]!.AsArray().Add(commands["commands"]![0]!.DeepClone());
                }
                else if (mutation == "candidate-commands-count-negative")
                {
                    commands["commands"]![0]!["counts"]!["warnings"] = -1;
                }
                else
                {
                    commands["commands"]![0]!["command"] = "dotnet build unexpected.csproj";
                }

                WriteObject(path, commands);
                ResealCandidateCapture(fixture, "commands.json");
                break;
            }
            case "candidate-environment-request-timeout":
            case "candidate-environment-postgresql-projection":
            case "candidate-environment-raw-postgresql":
            {
                string path = Path.Combine(
                    artifacts,
                    "evidence",
                    "story-4-14",
                    "e60a3777c581d70b62f67173ccc2372b5b64a425",
                    "environment.json");
                JsonObject environment = LoadObject(path);
                if (mutation == "candidate-environment-request-timeout")
                {
                    environment["limits"]!["requestTimeoutSeconds"] = 31;
                }
                else if (mutation == "candidate-environment-postgresql-projection")
                {
                    environment["limits"]!["postgresqlProjection"] = "raw values";
                }
                else
                {
                    environment["limits"]!["rawPostgresqlValuesCommitted"] = true;
                }

                WriteObject(path, environment);
                ResealCandidateCapture(fixture, "environment.json");
                break;
            }
            case "candidate-authority-capture":
            {
                string path = Path.Combine(closure, "capture-packet-v1.json");
                JsonObject capture = LoadObject(path);
                capture["packageAuthority"] = true;
                WriteObject(path, capture);
                ResealCandidateBinding(fixture, "capturePacketV1");
                break;
            }
            case "candidate-authority-crosswalk":
            {
                string path = Path.Combine(closure, "closure-crosswalk.json");
                JsonObject crosswalk = LoadObject(path);
                crosswalk["packageAuthority"] = true;
                WriteObject(path, crosswalk);
                ResealCandidateBinding(fixture, "closureCrosswalk");
                break;
            }
            case "candidate-authority-identity":
            {
                string path = Path.Combine(closure, "source-artifact-identity.json");
                JsonObject identity = LoadObject(path);
                identity["packageAuthority"] = true;
                WriteObject(path, identity);
                ResealCandidateBinding(fixture, "sourceArtifactIdentity");
                break;
            }
            case "candidate-authority-limitations":
            {
                string path = Path.Combine(closure, "limitations.json");
                JsonObject limitations = LoadObject(path);
                limitations["packageAuthority"] = true;
                WriteObject(path, limitations);
                ResealCandidateBinding(fixture, "limitations");
                break;
            }
            case "candidate-authority-execution":
            {
                JsonObject execution = LoadObject(executionPath);
                execution["packageAuthority"] = true;
                WriteObject(executionPath, execution);
                ResealCandidateBinding(fixture, "preReviewExecution");
                break;
            }
            case "candidate-authority-subject":
            {
                JsonObject subject = LoadObject(subjectPath);
                subject["packageAuthority"] = true;
                WriteObject(subjectPath, subject);
                break;
            }
            case "candidate-protected-capture":
            case "candidate-private-path-capture":
            {
                string path = Path.Combine(closure, "capture-packet-v1.json");
                JsonObject capture = LoadObject(path);
                capture["profile"] = mutation == "candidate-protected-capture"
                    ? "PROTECTED-OQ8-RAW-SENTINEL"
                    : "/home/private/capture";
                WriteObject(path, capture);
                ResealCandidateBinding(fixture, "capturePacketV1");
                break;
            }
            case "candidate-protected-identity":
            case "candidate-private-path-identity":
            {
                string path = Path.Combine(closure, "source-artifact-identity.json");
                JsonObject identity = LoadObject(path);
                identity["capture"]!["packetV1Path"] = mutation == "candidate-protected-identity"
                    ? "PROTECTED-OQ8-RAW-SENTINEL"
                    : "/home/private/source-artifact-identity";
                WriteObject(path, identity);
                ResealCandidateBinding(fixture, "sourceArtifactIdentity");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown candidate mutation.");
        }
    }

    private static string ExpectedCandidateFailure(string mutation) => mutation switch
    {
        "candidate-crosswalk-reassignment" => "Closure invariant-to-story/evidence mapping drift",
        "candidate-crosswalk-evidence-body" => "Closure evidence binding set or identity drift",
        "candidate-crosswalk-count-type" => "must be an exact integer",
        "candidate-source-path-set" => "Current bound source path declaration drift",
        "candidate-subject-binding" => "Review subject binding drift: closureCrosswalk",
        "candidate-subject-test-binding" => "Review subject binding drift: closureTests",
        "candidate-subject-dependency-binding" => "Review subject binding drift: validatorRequirements",
        "candidate-test-source-body" => "Story 4.15 successor current source identity drift",
        "candidate-execution-validator" => "Pre-review execution validator identity drift",
        "candidate-execution-test-source" => "Pre-review execution test-source identity drift",
        "candidate-execution-summary-type" => "must be an exact integer",
        "candidate-execution-command-name-duplicate" => "Pre-review execution command names must be exact and unique",
        "candidate-execution-command-count-zero" => "Pre-review execution command 3:tests count drift",
        "candidate-execution-command-identity" => "Pre-review execution command drift",
        "candidate-subject-date" => "Review subject date drift",
        "candidate-execution-date" => "Pre-review execution date drift",
        "candidate-duplicate-subject" => "Duplicate JSON field",
        "candidate-duplicate-authority" => "Duplicate JSON field",
        "candidate-duplicate-execution" => "Duplicate JSON field",
        "candidate-nan-subject" or "candidate-infinity-subject" => "Non-finite JSON constant is forbidden",
        "candidate-malformed-subject" or "candidate-invalid-utf8-subject" => "Cannot load JSON evidence",
        "candidate-document-binding" => "Reviewed public document body drift",
        "candidate-document-semantics" => "OQ8 source-only handoff semantics missing",
        "candidate-document-stale-state" => "Stale OQ8 handoff state remains",
        "candidate-story-status-done" => "Lifecycle status drift: 4-15-oq8-platform-closure-and-handoff",
        "candidate-frontmatter-done" => "Story 4.15 metadata status drift",
        "candidate-focused-test-shape" => "Focused result test must be an object",
        "candidate-focused-schema-boolean" => "Focused result schemaVersion must be an exact integer",
        "candidate-focused-duration-boolean" or "candidate-focused-duration-negative" => "Focused result duration must be a finite non-negative number",
        "candidate-focused-duration-nonfinite" => "Non-finite JSON constant is forbidden",
        "candidate-commands-shape" => "Verification command 0 field set drift",
        "candidate-commands-duplicate-name" => "Verification command names must be exact and unique",
        "candidate-commands-count-negative" => "Verification command live-sidecar-release-build:warnings count drift",
        "candidate-commands-command-identity" => "Verification command identity drift: live-sidecar-release-build",
        "candidate-environment-request-timeout" => "Environment requestTimeoutSeconds count drift",
        "candidate-environment-postgresql-projection" => "Environment PostgreSQL projection disclosure drift",
        "candidate-environment-raw-postgresql" => "Environment permits committed raw PostgreSQL values",
        "candidate-authority-capture" => "Capture packet field set drift",
        "candidate-authority-crosswalk" => "Closure crosswalk field set drift",
        "candidate-authority-identity" => "Source identity field set drift",
        "candidate-authority-limitations" => "Closure limitation text or order drift",
        "candidate-authority-execution" => "Pre-review execution field set drift",
        "candidate-authority-subject" => "Review subject field set drift",
        "candidate-protected-capture" or "candidate-protected-identity" => "Candidate JSON contains forbidden protected content",
        "candidate-private-path-capture" or "candidate-private-path-identity" => "Candidate JSON contains forbidden private-path content",
        _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown candidate mutation."),
    };

    private static string ExpectedObservationFailure(string mutation) => mutation switch
    {
        "top-level-extra" or "top-level-missing" => "Observation field set drift",
        "nested-extra" => "Before capture snapshot field set drift",
        "nested-missing" => "Observation runtime field set drift",
        "postgres-image-drift" => "PostgreSQL image identity drift",
        "postgres-image-identity" => "PostgreSQL immutable identity must be an exact sha256 digest",
        "counter-boolean" => "Observation topology eventStoreProcessCount must be a non-negative integer",
        "counter-float" => "Writer/failover observation concurrentRequests must be a non-negative integer",
        "counter-negative" => "After capture snapshot totalRows must be a non-negative integer",
        "protected-before" => "Protected sentinel leakage detected",
        _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown observation mutation."),
    };

    private static void InsertDuplicateJsonField(string path, string original, string replacement)
    {
        string content = File.ReadAllText(path);
        string mutated = content.Replace(original, replacement, StringComparison.Ordinal);
        mutated.ShouldNotBe(content);
        File.WriteAllText(path, mutated);
    }

    private static void ResealCandidateCapture(string fixture, string relative)
    {
        string artifacts = Path.Combine(fixture, "_bmad-output", "implementation-artifacts");
        string evidence = Path.Combine(
            artifacts,
            "evidence",
            "story-4-14",
            "e60a3777c581d70b62f67173ccc2372b5b64a425");
        string manifestPath = Path.Combine(evidence, "evidence-sha256.txt");
        string[] lines = File.ReadAllLines(manifestPath);
        string suffix = "  " + relative;
        int index = Array.FindIndex(lines, line => line.EndsWith(suffix, StringComparison.Ordinal));
        index.ShouldBeGreaterThanOrEqualTo(0);
        lines[index] = ComputeSha256(Path.Combine(evidence, relative)) + suffix;
        File.WriteAllText(manifestPath, string.Join('\n', lines) + "\n");

        string closure = CandidateClosure(fixture);
        string packetPath = Path.Combine(closure, "capture-packet-v1.json");
        JsonObject packet = LoadObject(packetPath);
        packet["evidenceFiles"]![relative] = ComputeSha256(Path.Combine(evidence, relative));
        packet["manifestSha256"] = ComputeSha256(manifestPath);
        WriteObject(packetPath, packet);

        string identityPath = Path.Combine(closure, "source-artifact-identity.json");
        JsonObject identity = LoadObject(identityPath);
        identity["capture"]!["packetV1Sha256"] = ComputeSha256(packetPath);
        identity["capture"]!["manifestSha256"] = ComputeSha256(manifestPath);
        WriteObject(identityPath, identity);
    }

    private static void ResealCandidateBinding(string fixture, string binding)
    {
        string closure = CandidateClosure(fixture);
        string subjectPath = Path.Combine(closure, "review-subject.json");
        JsonObject subject = LoadObject(subjectPath);
        string artifact = binding switch
        {
            "capturePacketV1" => "capture-packet-v1.json",
            "closureCrosswalk" => "closure-crosswalk.json",
            "sourceArtifactIdentity" => "source-artifact-identity.json",
            "limitations" => "limitations.json",
            "preReviewExecution" => "pre-review-execution.json",
            _ => throw new ArgumentOutOfRangeException(nameof(binding), binding, "Unknown candidate binding."),
        };

        if (binding == "capturePacketV1")
        {
            string identityPath = Path.Combine(closure, "source-artifact-identity.json");
            JsonObject identity = LoadObject(identityPath);
            identity["capture"]!["packetV1Sha256"] = ComputeSha256(Path.Combine(closure, artifact));
            WriteObject(identityPath, identity);
            subject["bindings"]!["sourceArtifactIdentity"]!["sha256"] = ComputeSha256(identityPath);
        }

        subject["bindings"]![binding]!["sha256"] = ComputeSha256(Path.Combine(closure, artifact));
        WriteObject(subjectPath, subject);
    }

    private static void ApplyMutation(string fixture, string mutation)
    {
        string artifacts = Path.Combine(fixture, "_bmad-output", "implementation-artifacts");
        string capture = Path.Combine(
            artifacts,
            "evidence",
            "story-4-14",
            "e60a3777c581d70b62f67173ccc2372b5b64a425");
        string closure = Path.Combine(
            artifacts,
            "evidence",
            "story-4-15",
            LandedSource);
        string packetPath = Path.Combine(artifacts, "4-8-eventstore-oq8-platform-evidence.yaml");
        switch (mutation)
        {
            case "capture-matrix":
            {
                JsonObject packet = LoadObject(packetPath);
                packet["capture"]!["matrix"]!["writersFailover"] = "failed";
                WriteObject(packetPath, packet);
                break;
            }
            case "capture-artifact":
                File.AppendAllText(Path.Combine(capture, "observations.json"), " ");
                break;
            case "closure-manifest":
                File.AppendAllText(Path.Combine(closure, "closure-sha256.txt"), "invalid\n");
                break;
            case "crosswalk-invariant":
            {
                JsonObject crosswalk = LoadObject(Path.Combine(closure, "closure-crosswalk.json"));
                crosswalk["invariants"]!.AsArray().RemoveAt(0);
                WriteObject(Path.Combine(closure, "closure-crosswalk.json"), crosswalk);
                break;
            }
            case "crosswalk-invariant-reassignment":
            {
                JsonObject crosswalk = LoadObject(Path.Combine(closure, "closure-crosswalk.json"));
                crosswalk["invariants"]![0]!["stories"]![0] = "4.10";
                WriteObject(Path.Combine(closure, "closure-crosswalk.json"), crosswalk);
                break;
            }
            case "crosswalk-evidence-body":
            {
                const string relative = "_bmad-output/implementation-artifacts/spec-4-11-admission-state-machine-and-current-fence-enforcement.md";
                string evidencePath = Path.Combine(fixture, relative);
                File.AppendAllText(evidencePath, "\nEvidence body drift.\n");
                JsonObject crosswalk = LoadObject(Path.Combine(closure, "closure-crosswalk.json"));
                crosswalk["evidenceBindings"]![relative] = ComputeSha256(evidencePath);
                WriteObject(Path.Combine(closure, "closure-crosswalk.json"), crosswalk);
                break;
            }
            case "crosswalk-command-count":
            {
                JsonObject crosswalk = LoadObject(Path.Combine(closure, "closure-crosswalk.json"));
                crosswalk["verification"]!["successfulCommands"] = 7;
                WriteObject(Path.Combine(closure, "closure-crosswalk.json"), crosswalk);
                break;
            }
            case "crosswalk-command-count-type":
            {
                JsonObject crosswalk = LoadObject(Path.Combine(closure, "closure-crosswalk.json"));
                crosswalk["verification"]!["focusedProductionCases"] = true;
                WriteObject(Path.Combine(closure, "closure-crosswalk.json"), crosswalk);
                break;
            }
            case "crosswalk-field-extra":
            {
                JsonObject crosswalk = LoadObject(Path.Combine(closure, "closure-crosswalk.json"));
                crosswalk["unreviewed"] = true;
                WriteObject(Path.Combine(closure, "closure-crosswalk.json"), crosswalk);
                break;
            }
            case "source-commit":
            {
                JsonObject identity = LoadObject(Path.Combine(closure, "source-artifact-identity.json"));
                identity["landedSource"]!["commit"] = new string('0', 40);
                WriteObject(Path.Combine(closure, "source-artifact-identity.json"), identity);
                break;
            }
            case "source-path-hash":
            {
                JsonObject identity = LoadObject(Path.Combine(closure, "source-artifact-identity.json"));
                identity["captureWorktreePaths"]!["deploy/dapr/resiliency.yaml"] = new string('0', 64);
                WriteObject(Path.Combine(closure, "source-artifact-identity.json"), identity);
                break;
            }
            case "source-candidate-path-set":
            {
                JsonObject identity = LoadObject(Path.Combine(closure, "source-artifact-identity.json"));
                identity["capturedPathSets"]!["candidateFiles"]!.AsArray().RemoveAt(0);
                WriteObject(Path.Combine(closure, "source-artifact-identity.json"), identity);
                break;
            }
            case "source-current-path-set":
            {
                JsonObject identity = LoadObject(Path.Combine(closure, "source-artifact-identity.json"));
                identity["currentVerification"]!["boundPaths"]!.AsArray().RemoveAt(0);
                WriteObject(Path.Combine(closure, "source-artifact-identity.json"), identity);
                break;
            }
            case "source-field-extra":
            {
                JsonObject identity = LoadObject(Path.Combine(closure, "source-artifact-identity.json"));
                identity["unreviewed"] = true;
                WriteObject(Path.Combine(closure, "source-artifact-identity.json"), identity);
                break;
            }
            case "subject-design":
            {
                JsonObject subject = LoadObject(Path.Combine(closure, "review-subject.json"));
                subject["design"]!["sha256"] = new string('0', 64);
                WriteObject(Path.Combine(closure, "review-subject.json"), subject);
                break;
            }
            case "subject-binding":
            {
                JsonObject subject = LoadObject(Path.Combine(closure, "review-subject.json"));
                subject["bindings"]!["closureCrosswalk"]!["sha256"] = new string('0', 64);
                WriteObject(Path.Combine(closure, "review-subject.json"), subject);
                break;
            }
            case "subject-limitation":
            {
                JsonObject subject = LoadObject(Path.Combine(closure, "review-subject.json"));
                subject["limitations"]!.AsArray().RemoveAt(0);
                WriteObject(Path.Combine(closure, "review-subject.json"), subject);
                break;
            }
            case "subject-authority":
            {
                JsonObject subject = LoadObject(Path.Combine(closure, "review-subject.json"));
                subject["authority"]!["deploymentAuthority"] = true;
                WriteObject(Path.Combine(closure, "review-subject.json"), subject);
                break;
            }
            case "subject-field-extra":
            {
                JsonObject subject = LoadObject(Path.Combine(closure, "review-subject.json"));
                subject["unreviewed"] = true;
                WriteObject(Path.Combine(closure, "review-subject.json"), subject);
                break;
            }
            case "review-decision":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["decision"] = "rejected";
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "review-subject":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "test.json"));
                review["subjectSha256"] = new string('0', 64);
                WriteObject(Path.Combine(closure, "reviews", "test.json"), review);
                break;
            }
            case "review-reviewer":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["reviewer"] = "Unexpected Reviewer";
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "review-role":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["role"] = "architecture";
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "review-scope":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["acceptedScope"] = "unbound scope";
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "review-limitations":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["acceptedLimitationsSha256"] = new string('0', 64);
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "review-findings":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["findings"] = new JsonArray();
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "review-findings-blank":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["findings"] = new JsonArray("   ");
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "review-date":
            {
                string reviewPath = Path.Combine(closure, "reviews", "security.json");
                JsonObject review = LoadObject(reviewPath);
                review["reviewedOn"] = PreviousCalendarDay(review["reviewedOn"]!.GetValue<string>());
                WriteObject(reviewPath, review);
                string handoffPath = Path.Combine(closure, "source-only-handoff.json");
                JsonObject handoff = LoadObject(handoffPath);
                handoff["reviewReceipts"]!["security"] = ComputeSha256(reviewPath);
                WriteObject(handoffPath, handoff);
                ResealClosureArtifact(packetPath, closure, "reviews/security.json");
                ResealClosureArtifact(packetPath, closure, "source-only-handoff.json");
                break;
            }
            case "review-authority":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["authority"]!["releaseApproved"] = true;
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "review-external-repository-authority":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["authority"]!["externalRepositoryAuthority"] = true;
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "review-field-extra":
            {
                JsonObject review = LoadObject(Path.Combine(closure, "reviews", "security.json"));
                review["unreviewed"] = true;
                WriteObject(Path.Combine(closure, "reviews", "security.json"), review);
                break;
            }
            case "handoff-mode":
            {
                JsonObject handoff = LoadObject(Path.Combine(closure, "source-only-handoff.json"));
                handoff["consumerInstructions"]!["mode"] = "package";
                WriteObject(Path.Combine(closure, "source-only-handoff.json"), handoff);
                break;
            }
            case "handoff-instruction-missing":
            {
                JsonObject handoff = LoadObject(Path.Combine(closure, "source-only-handoff.json"));
                handoff["consumerInstructions"]!.AsObject().Remove("sourcePathRule");
                WriteObject(Path.Combine(closure, "source-only-handoff.json"), handoff);
                break;
            }
            case "handoff-instruction-extra":
            {
                JsonObject handoff = LoadObject(Path.Combine(closure, "source-only-handoff.json"));
                handoff["consumerInstructions"]!["packagePath"] = "forbidden";
                WriteObject(Path.Combine(closure, "source-only-handoff.json"), handoff);
                break;
            }
            case "handoff-instruction-changed":
            {
                JsonObject handoff = LoadObject(Path.Combine(closure, "source-only-handoff.json"));
                handoff["consumerInstructions"]!["sourcePathRule"] = "Use any later source.";
                WriteObject(Path.Combine(closure, "source-only-handoff.json"), handoff);
                break;
            }
            case "handoff-authority":
            {
                JsonObject handoff = LoadObject(Path.Combine(closure, "source-only-handoff.json"));
                handoff["authority"]!["consumerMigrationAuthority"] = true;
                WriteObject(Path.Combine(closure, "source-only-handoff.json"), handoff);
                break;
            }
            case "handoff-final-consumer-authority":
            {
                JsonObject handoff = LoadObject(Path.Combine(closure, "source-only-handoff.json"));
                handoff["authority"]!["finalConsumerAuthority"] = true;
                WriteObject(Path.Combine(closure, "source-only-handoff.json"), handoff);
                break;
            }
            case "handoff-field-extra":
            {
                JsonObject handoff = LoadObject(Path.Combine(closure, "source-only-handoff.json"));
                handoff["releaseApproved"] = true;
                WriteObject(Path.Combine(closure, "source-only-handoff.json"), handoff);
                break;
            }
            case "validator-digest":
                File.WriteAllText(
                    Path.Combine(closure, "validator-sha256.txt"),
                    new string('0', 64) + "  tools/validate-oq8-platform-evidence.py\n");
                break;
            case "story-status":
            {
                string statusPath = Path.Combine(artifacts, "sprint-status.yaml");
                string status = File.ReadAllText(statusPath).Replace(
                    "  4-15-oq8-platform-closure-and-handoff: review",
                    "  4-15-oq8-platform-closure-and-handoff: done",
                    StringComparison.Ordinal);
                File.WriteAllText(statusPath, status);
                break;
            }
            case "sprint-status-duplicate":
            {
                string statusPath = Path.Combine(artifacts, "sprint-status.yaml");
                List<string> status = File.ReadAllLines(statusPath).ToList();
                int mapping = status.FindIndex(line => line == "development_status:");
                mapping.ShouldBeGreaterThanOrEqualTo(0);
                status.Insert(mapping + 1, "  4-15-oq8-platform-closure-and-handoff: done");
                File.WriteAllLines(statusPath, status);
                break;
            }
            case "frontmatter-status-duplicate":
            {
                string specPath = Path.Combine(artifacts, "spec-4-15-oq8-platform-closure-and-handoff.md");
                string spec = File.ReadAllText(specPath).Replace(
                    "status: 'done'",
                    "status: 'done'\nstatus: 'in-review'",
                    StringComparison.Ordinal);
                File.WriteAllText(specPath, spec);
                break;
            }
            case "document-marker":
            {
                string documentPath = Path.Combine(fixture, "docs", "reference", "command-api.md");
                string document = File.ReadAllText(documentPath).Replace(
                    "OQ8-SOURCE-ONLY-HANDOFF",
                    "OQ8-HANDOFF-MISSING",
                    StringComparison.Ordinal);
                File.WriteAllText(documentPath, document);
                break;
            }
            case "document-semantics":
            {
                const string relative = "docs/reference/command-api.md";
                string documentPath = Path.Combine(fixture, relative);
                string document = File.ReadAllText(documentPath).Replace(
                    "no release approval",
                    "release approval",
                    StringComparison.Ordinal);
                File.WriteAllText(documentPath, document);
                JsonObject subject = LoadObject(Path.Combine(closure, "review-subject.json"));
                subject["reviewedPublicDocs"]![relative] = ComputeSha256(documentPath);
                WriteObject(Path.Combine(closure, "review-subject.json"), subject);
                break;
            }
            case "platform-field-extra":
            {
                JsonObject packet = LoadObject(packetPath);
                packet["platformClosure"]!["releaseApproved"] = true;
                WriteObject(packetPath, packet);
                break;
            }
            case "authority-field-extra":
            {
                JsonObject packet = LoadObject(packetPath);
                packet["platformClosure"]!["authority"]!["releaseNotesAuthority"] = false;
                WriteObject(packetPath, packet);
                break;
            }
            case "duplicate-authority-handoff":
            case "duplicate-authority-handoff-minified":
            {
                string handoffPath = Path.Combine(closure, "source-only-handoff.json");
                string handoff = File.ReadAllText(handoffPath);
                if (mutation.EndsWith("-minified", StringComparison.Ordinal))
                {
                    handoff = JsonSerializer.Serialize(JsonNode.Parse(handoff));
                }

                string duplicated = InjectDuplicateEventStorePlatformComplete(handoff);
                File.WriteAllText(handoffPath, duplicated);
                break;
            }
            case "duplicate-authority-packet":
            case "duplicate-authority-packet-minified":
            {
                string packet = File.ReadAllText(packetPath);
                if (mutation.EndsWith("-minified", StringComparison.Ordinal))
                {
                    packet = JsonSerializer.Serialize(JsonNode.Parse(packet));
                }

                string duplicated = InjectDuplicateEventStorePlatformComplete(packet);
                File.WriteAllText(packetPath, duplicated);
                break;
            }
            case "malformed-json-crosswalk":
                File.WriteAllText(Path.Combine(closure, "closure-crosswalk.json"), "{");
                break;
            case "malformed-json-packet":
                File.WriteAllText(packetPath, "{");
                break;
            case "invalid-utf8-packet":
                File.WriteAllBytes(packetPath, [0xff, 0xfe, 0xfd]);
                break;
            case "closure-manifest-malformed-line":
            {
                string manifestPath = Path.Combine(closure, "closure-sha256.txt");
                string[] lines = File.ReadAllLines(manifestPath);
                string relative = lines[0].Split("  ", 2, StringSplitOptions.None)[1];
                lines[0] = "not-a-sha256  " + relative;
                File.WriteAllText(manifestPath, string.Join('\n', lines) + "\n");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown OQ8 closure mutation.");
        }

        string? closureArtifact = mutation switch
        {
            "crosswalk-invariant" or "crosswalk-invariant-reassignment" or "crosswalk-evidence-body" or
                "crosswalk-command-count" or "crosswalk-command-count-type" or "crosswalk-field-extra" or
                "malformed-json-crosswalk" => "closure-crosswalk.json",
            "source-commit" or "source-path-hash" or "source-candidate-path-set" or
                "source-current-path-set" or "source-field-extra" => "source-artifact-identity.json",
            "subject-design" or "subject-binding" or "subject-limitation" or "subject-authority" or
                "subject-field-extra" or "document-semantics" => "review-subject.json",
            "review-decision" or "review-reviewer" or "review-role" or "review-scope" or
                "review-limitations" or "review-findings" or "review-findings-blank" or "review-authority" or
                "review-date" or
                "review-external-repository-authority" or
                "review-field-extra" => "reviews/security.json",
            "review-subject" => "reviews/test.json",
            "handoff-mode" or "handoff-instruction-missing" or "handoff-instruction-extra" or
                "handoff-instruction-changed" or "handoff-authority" or "handoff-final-consumer-authority" or "handoff-field-extra" or
                "duplicate-authority-handoff" or "duplicate-authority-handoff-minified" => "source-only-handoff.json",
            "validator-digest" => "validator-sha256.txt",
            _ => null,
        };
        if (closureArtifact is not null)
        {
            ResealClosureArtifact(packetPath, closure, closureArtifact);
        }
    }

    private static string ExpectedFailure(string mutation) => mutation switch
    {
        "capture-matrix" => "Immutable v1 capture packet snapshot drift",
        "capture-artifact" => "Evidence checksum mismatch: observations.json",
        "closure-manifest" => "Closure manifest is not path-sorted",
        "crosswalk-invariant" => "Closure invariant-to-story/evidence mapping drift",
        "crosswalk-invariant-reassignment" => "Closure invariant-to-story/evidence mapping drift",
        "crosswalk-evidence-body" => "Closure evidence binding set or identity drift",
        "crosswalk-command-count" => "Closure verification successfulCommands count drift",
        "crosswalk-command-count-type" => "must be an exact integer",
        "crosswalk-field-extra" => "Closure crosswalk field set drift",
        "source-commit" => "Landed source commit drift",
        "source-path-hash" => "Capture worktree path/hash set drift",
        "source-candidate-path-set" => "Captured candidate/source path sets drift",
        "source-current-path-set" => "Current bound source path declaration drift",
        "source-field-extra" => "Source identity field set drift",
        "subject-design" => "Review subject design binding drift",
        "subject-binding" => "Review subject binding drift: closureCrosswalk",
        "subject-limitation" => "Review subject limitations drift",
        "subject-authority" => "External authority overstated: deploymentAuthority",
        "subject-field-extra" => "Review subject field set drift",
        "review-decision" => "security review is not approved",
        "review-subject" => "test review subject drift",
        "review-reviewer" => "security reviewer identity drift",
        "review-role" => "security review role drift",
        "review-scope" => "security accepted scope drift",
        "review-limitations" => "security limitations acceptance drift",
        "review-findings" or "review-findings-blank" => "security review findings missing or blank",
        "review-date" => "security review date drift",
        "review-authority" => "External authority overstated: releaseApproved",
        "review-external-repository-authority" => "External authority overstated: externalRepositoryAuthority",
        "review-field-extra" => "security review field set drift",
        "handoff-mode" => "Consumer instruction set or value drift",
        "handoff-instruction-missing" => "Consumer instruction set or value drift",
        "handoff-instruction-extra" => "Consumer instruction set or value drift",
        "handoff-instruction-changed" => "Consumer instruction set or value drift",
        "handoff-authority" => "Source-only handoff reviewed authority drift",
        "handoff-final-consumer-authority" => "Source-only handoff reviewed authority drift",
        "handoff-field-extra" => "Source-only handoff field set drift",
        "validator-digest" => "Closure validator identity drift",
        "story-status" => "Lifecycle status drift: 4-15-oq8-platform-closure-and-handoff",
        "sprint-status-duplicate" => "Lifecycle status is missing or ambiguous: 4-15-oq8-platform-closure-and-handoff",
        "frontmatter-status-duplicate" => "Story 4.15 frontmatter status is missing or ambiguous",
        "document-marker" => "OQ8 source-only handoff marker is missing or ambiguous: docs/reference/command-api.md",
        "document-semantics" => "OQ8 source-only handoff semantics missing from docs/reference/command-api.md: no release approval",
        "platform-field-extra" => "Platform closure field set drift",
        "authority-field-extra" => "Closure authority field set drift",
        "duplicate-authority-handoff" => "Duplicate JSON field",
        "duplicate-authority-handoff-minified" => "Duplicate JSON field",
        "duplicate-authority-packet" => "Duplicate JSON field",
        "duplicate-authority-packet-minified" => "Duplicate JSON field",
        "malformed-json-crosswalk" => "Cannot load JSON evidence",
        "malformed-json-packet" => "Cannot load JSON evidence",
        "invalid-utf8-packet" => "Cannot read evidence path _bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml",
        "closure-manifest-malformed-line" => "Malformed closure manifest line",
        _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown OQ8 closure mutation."),
    };

    private static void ResealClosureArtifact(string packetPath, string closure, string relative)
    {
        string manifestPath = Path.Combine(closure, "closure-sha256.txt");
        string[] lines = File.ReadAllLines(manifestPath);
        string suffix = "  " + relative;
        int index = Array.FindIndex(lines, line => line.EndsWith(suffix, StringComparison.Ordinal));
        index.ShouldBeGreaterThanOrEqualTo(0);
        lines[index] = ComputeSha256(Path.Combine(closure, relative)) + suffix;
        File.WriteAllText(manifestPath, string.Join('\n', lines) + "\n");

        JsonObject packet = LoadObject(packetPath);
        packet["platformClosure"]!["closureFiles"]![relative] = ComputeSha256(Path.Combine(closure, relative));
        packet["platformClosure"]!["closureManifestSha256"] = ComputeSha256(manifestPath);
        WriteObject(packetPath, packet);
    }

    private static string SuccessorPath(string artifacts) => Path.Combine(
        artifacts,
        "evidence",
        "story-4-15",
        "successors",
        SuccessorDirectory);

    private static void ResealSuccessorArtifacts(string artifacts)
    {
        string successor = SuccessorPath(artifacts);
        string identityPath = Path.Combine(successor, "source-artifact-identity.json");
        string subjectPath = Path.Combine(successor, "review-subject.json");
        string handoffPath = Path.Combine(successor, "source-only-handoff.json");

        JsonObject subject = LoadObject(subjectPath);
        subject["sourceIdentity"]!["sha256"] = ComputeSha256(identityPath);
        WriteObject(subjectPath, subject);
        string subjectSha256 = ComputeSha256(subjectPath);

        JsonObject handoff = LoadObject(handoffPath);
        handoff["sourceIdentitySha256"] = ComputeSha256(identityPath);
        handoff["reviewSubjectSha256"] = subjectSha256;
        foreach (string role in new[] { "architecture", "security", "test" })
        {
            string reviewPath = Path.Combine(successor, "reviews", role + ".json");
            JsonObject review = LoadObject(reviewPath);
            review["subjectSha256"] = subjectSha256;
            WriteObject(reviewPath, review);
            handoff["reviewReceipts"]![role] = ComputeSha256(reviewPath);
        }

        WriteObject(handoffPath, handoff);

        string[] relativeFiles =
        [
            "review-subject.json",
            "reviews/architecture.json",
            "reviews/security.json",
            "reviews/test.json",
            "source-artifact-identity.json",
            "source-only-handoff.json",
        ];
        string manifestPath = Path.Combine(successor, "successor-sha256.txt");
        File.WriteAllText(
            manifestPath,
            string.Join('\n', relativeFiles.Select(relative => $"{ComputeSha256(Path.Combine(successor, relative))}  {relative}")) + "\n");

        string selectorPath = Path.Combine(artifacts, "4-15-oq8-platform-closure-successor.json");
        JsonObject selector = LoadObject(selectorPath);
        selector["successor"]!["manifestSha256"] = ComputeSha256(manifestPath);
        foreach (string relative in relativeFiles)
        {
            selector["successor"]!["files"]![relative] = ComputeSha256(Path.Combine(successor, relative));
        }

        selector["successor"]!["sourceIdentitySha256"] = ComputeSha256(identityPath);
        selector["successor"]!["reviewSubjectSha256"] = subjectSha256;
        selector["successor"]!["handoffSha256"] = ComputeSha256(handoffPath);
        WriteObject(selectorPath, selector);
    }

    private static string ComputeSha256(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static void ApplyV2Mutation(string fixture, string mutation)
    {
        string successor = Path.Combine(fixture, V2SuccessorRelativeDirectory);
        string manifestPath = Path.Combine(successor, "closure-sha256.txt");
        switch (mutation)
        {
            case "missing-receipt":
                File.Delete(Path.Combine(successor, "reviews", "security.json"));
                break;
            case "additional-artifact":
                File.WriteAllText(Path.Combine(successor, "unreviewed.json"), "{}\n");
                break;
            case "reordered-manifest":
            {
                string[] lines = File.ReadAllLines(manifestPath);
                Array.Reverse(lines);
                File.WriteAllText(manifestPath, string.Join('\n', lines) + "\n");
                break;
            }
            case "malformed-manifest":
            {
                string[] lines = File.ReadAllLines(manifestPath);
                lines[0] = "not-a-sha256  limitations.json";
                File.WriteAllText(manifestPath, string.Join('\n', lines) + "\n");
                break;
            }
            case "symlinked-artifact":
            {
                string limitationsPath = Path.Combine(successor, "limitations.json");
                string target = Path.Combine(fixture, "v2-limitations-target.json");
                File.Move(limitationsPath, target);
                File.CreateSymbolicLink(limitationsPath, target);
                break;
            }
            case "symlinked-v2-ancestor":
            {
                string ancestor = Path.Combine(
                    fixture,
                    "_bmad-output",
                    "implementation-artifacts",
                    "evidence",
                    "story-4-15-successors");
                string target = Path.Combine(fixture, "v2-ancestor-target");
                Directory.Move(ancestor, target);
                Directory.CreateSymbolicLink(ancestor, target);
                break;
            }
            case "symlinked-source-ancestor":
            {
                string ancestor = Path.Combine(fixture, ".github", "workflows");
                string target = Path.Combine(fixture, "source-ancestor-target");
                Directory.Move(ancestor, target);
                Directory.CreateSymbolicLink(ancestor, target);
                break;
            }
            case "symlinked-gate-ancestor":
            {
                string ancestor = Path.Combine(fixture, "docs");
                string target = Path.Combine(fixture, "gate-ancestor-target");
                Directory.Move(ancestor, target);
                Directory.CreateSymbolicLink(ancestor, target);
                break;
            }
            case "oversized-artifact":
                File.AppendAllText(Path.Combine(successor, "limitations.json"), new string('x', 65_537));
                break;
            case "oversized-source":
                File.AppendAllText(Path.Combine(fixture, "docs", "ci.md"), new string('x', 524_288));
                break;
            case "predecessor-mismatch":
            {
                string identityPath = Path.Combine(successor, "source-artifact-identity.json");
                JsonObject identity = LoadObject(identityPath);
                identity["predecessor"]!["landedSourceCommit"] = new string('0', 40);
                WriteObject(identityPath, identity);
                ResealV2AfterIdentityChange(successor);
                break;
            }
            case "source-drift":
                File.AppendAllText(Path.Combine(fixture, ".github", "workflows", "integration.yml"), "\n# v2 source drift\n");
                break;
            case "semantic-workflow-tag":
                MutateV2SemanticSource(
                    fixture,
                    successor,
                    ".github/workflows/integration.yml");
                break;
            case "semantic-fixture-tag":
                MutateV2SemanticSource(
                    fixture,
                    successor,
                    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs");
                break;
            case "gate-input-drift":
                File.AppendAllText(Path.Combine(fixture, "docs", "ci.md"), "\nV2 gate-input drift.\n");
                break;
            case "reviewed-index-boolean":
            {
                string identityPath = Path.Combine(successor, "source-artifact-identity.json");
                JsonObject identity = LoadObject(identityPath);
                identity["reviewedImage"]!["amd64ChildIsAuthority"] = 0;
                WriteObject(identityPath, identity);
                ResealV2AfterIdentityChange(successor);
                break;
            }
            case "pre-review-name":
                MutateV2PreReviewCommand(successor, "postgres-image-governance", command => command["name"] = "governance-renamed");
                break;
            case "pre-review-command":
                MutateV2PreReviewCommand(successor, "postgres-image-governance", command => command["command"] = "dotnet test altered");
                break;
            case "pre-review-count-float":
                MutateV2PreReviewCommand(successor, "postgres-image-governance", command => command["tests"] = JsonNode.Parse("18.0"));
                break;
            case "pre-review-exit-boolean":
                MutateV2PreReviewCommand(successor, "postgres-image-governance", command => command["exitCode"] = false);
                break;
            case "pre-review-equals-freeze":
            {
                string executionPath = Path.Combine(successor, "pre-review-execution.json");
                JsonObject execution = LoadObject(executionPath);
                execution["executedAt"] = LoadObject(Path.Combine(successor, "review-subject.json"))["frozenAt"]!.GetValue<string>();
                WriteObject(executionPath, execution);
                ResealV2AfterPreReviewChange(successor);
                break;
            }
            case "pre-review-future":
                MutateV2Timestamp(successor, "pre-review-execution.json", "executedAt", "2026-08-30T23:59:59Z", ResealV2AfterPreReviewChange);
                break;
            case "subject-future":
                MutateV2Timestamp(successor, "review-subject.json", "frozenAt", "2026-08-30T23:59:59Z", ResealV2AfterSubjectChange);
                break;
            case "receipt-drift":
            {
                string receiptPath = Path.Combine(successor, "reviews", "security.json");
                JsonObject receipt = LoadObject(receiptPath);
                receipt["decision"] = "rejected";
                WriteObject(receiptPath, receipt);
                ResealV2Receipt(successor, "security");
                break;
            }
            case "receipt-subject-drift":
            {
                string receiptPath = Path.Combine(successor, "reviews", "security.json");
                JsonObject receipt = LoadObject(receiptPath);
                receipt["subjectSha256"] = new string('0', 64);
                WriteObject(receiptPath, receipt);
                ResealV2Receipt(successor, "security");
                break;
            }
            case "receipt-equals-freeze":
            {
                string receiptPath = Path.Combine(successor, "reviews", "security.json");
                JsonObject receipt = LoadObject(receiptPath);
                receipt["issuedAt"] = LoadObject(Path.Combine(successor, "review-subject.json"))["frozenAt"]!.GetValue<string>();
                WriteObject(receiptPath, receipt);
                ResealV2Receipt(successor, "security");
                break;
            }
            case "receipt-future":
            {
                string receiptPath = Path.Combine(successor, "reviews", "security.json");
                JsonObject receipt = LoadObject(receiptPath);
                receipt["issuedAt"] = "2026-08-30T23:59:59Z";
                WriteObject(receiptPath, receipt);
                ResealV2Receipt(successor, "security");
                break;
            }
            case "handoff-equals-receipt":
            {
                string handoffPath = Path.Combine(successor, "source-only-handoff.json");
                JsonObject handoff = LoadObject(handoffPath);
                handoff["assembledAt"] = LoadObject(Path.Combine(successor, "reviews", "test.json"))["issuedAt"]!.GetValue<string>();
                WriteObject(handoffPath, handoff);
                ResealV2Manifest(successor);
                break;
            }
            case "handoff-future":
                MutateV2Timestamp(successor, "source-only-handoff.json", "assembledAt", "2026-08-30T23:59:59Z", ResealV2Manifest);
                break;
            case "test-verification-missing":
            {
                string receiptPath = Path.Combine(successor, "reviews", "test.json");
                JsonObject receipt = LoadObject(receiptPath);
                receipt.Remove("verification");
                WriteObject(receiptPath, receipt);
                ResealV2Receipt(successor, "test");
                break;
            }
            case "test-verification-name":
                MutateV2TestVerification(successor, command => command["name"] = "closure-renamed");
                break;
            case "test-verification-command":
                MutateV2TestVerification(successor, command => command["command"] = "dotnet test altered");
                break;
            case "test-verification-count":
                MutateV2TestVerification(successor, command => command["tests"] = command["tests"]!.GetValue<int>() + 1);
                break;
            case "test-verification-failed":
                MutateV2TestVerification(successor, command => command["failed"] = 1);
                break;
            case "test-verification-skipped":
                MutateV2TestVerification(successor, command => command["skipped"] = 1);
                break;
            case "test-verification-float":
                MutateV2TestVerification(successor, command => command["tests"] = JsonNode.Parse("368.0"));
                break;
            case "test-verification-boolean":
                MutateV2TestVerification(successor, command => command["failed"] = false);
                break;
            case "overstated-authority":
            {
                string handoffPath = Path.Combine(successor, "source-only-handoff.json");
                JsonObject handoff = LoadObject(handoffPath);
                handoff["authority"]!["deploymentAuthority"] = true;
                WriteObject(handoffPath, handoff);
                ResealV2Manifest(successor);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown Story 4.15 v2 mutation.");
        }
    }

    private static void ResealV2AfterIdentityChange(string successor)
    {
        string identityPath = Path.Combine(successor, "source-artifact-identity.json");
        JsonObject identity = LoadObject(identityPath);
        string executionPath = Path.Combine(successor, "pre-review-execution.json");
        JsonObject execution = LoadObject(executionPath);
        execution["candidateInputs"]!["sourceTransitions"] = identity["sourceTransitions"]!.DeepClone();
        execution["candidateInputs"]!["gateInputs"] = identity["gateInputs"]!.DeepClone();
        WriteObject(executionPath, execution);

        string subjectPath = Path.Combine(successor, "review-subject.json");
        JsonObject subject = LoadObject(subjectPath);
        subject["sourceTransitions"] = identity["sourceTransitions"]!.DeepClone();
        subject["gateInputs"] = identity["gateInputs"]!.DeepClone();
        subject["bindings"]!["sourceIdentity"]!["sha256"] = ComputeSha256(identityPath);
        subject["bindings"]!["preReviewExecution"]!["sha256"] = ComputeSha256(executionPath);
        WriteObject(subjectPath, subject);
        ResealV2AfterSubjectChange(successor);
    }

    private static void ResealV2AfterPreReviewChange(string successor)
    {
        string subjectPath = Path.Combine(successor, "review-subject.json");
        JsonObject subject = LoadObject(subjectPath);
        subject["bindings"]!["preReviewExecution"]!["sha256"] =
            ComputeSha256(Path.Combine(successor, "pre-review-execution.json"));
        WriteObject(subjectPath, subject);
        ResealV2AfterSubjectChange(successor);
    }

    private static void ResealV2AfterSubjectChange(string successor)
    {
        string subjectPath = Path.Combine(successor, "review-subject.json");
        string subjectSha256 = ComputeSha256(subjectPath);

        string limitationsSha256 = ComputeSha256(Path.Combine(successor, "limitations.json"));
        JsonObject handoff = LoadObject(Path.Combine(successor, "source-only-handoff.json"));
        handoff["reviewSubjectSha256"] = subjectSha256;
        foreach (string role in new[] { "architecture", "security", "test" })
        {
            string receiptPath = Path.Combine(successor, "reviews", role + ".json");
            JsonObject receipt = LoadObject(receiptPath);
            receipt["subjectSha256"] = subjectSha256;
            receipt["limitationsSha256"] = limitationsSha256;
            WriteObject(receiptPath, receipt);
            handoff["reviewReceipts"]![role] = ComputeSha256(receiptPath);
        }

        WriteObject(Path.Combine(successor, "source-only-handoff.json"), handoff);
        ResealV2Manifest(successor);
    }

    private static void MutateV2SemanticSource(string fixture, string successor, string relative)
    {
        string sourcePath = Path.Combine(fixture, relative);
        string source = File.ReadAllText(sourcePath);
        string mutated = source.Replace(ReviewedPostgresImage, HistoricalPostgresImage, StringComparison.Ordinal);
        mutated.ShouldNotBe(source);
        File.WriteAllText(sourcePath, mutated);

        string identityPath = Path.Combine(successor, "source-artifact-identity.json");
        JsonObject identity = LoadObject(identityPath);
        identity["sourceTransitions"]![relative]!["successorSha256"] = ComputeSha256(sourcePath);
        WriteObject(identityPath, identity);
        ResealV2AfterIdentityChange(successor);
    }

    private static void MutateV2PreReviewCommand(
        string successor,
        string name,
        Action<JsonObject> mutation)
    {
        string executionPath = Path.Combine(successor, "pre-review-execution.json");
        JsonObject execution = LoadObject(executionPath);
        JsonObject command = execution["commands"]!
            .AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["name"]!.GetValue<string>() == name);
        mutation(command);
        WriteObject(executionPath, execution);
        ResealV2AfterPreReviewChange(successor);
    }

    private static void MutateV2TestVerification(string successor, Action<JsonObject> mutation)
    {
        string receiptPath = Path.Combine(successor, "reviews", "test.json");
        JsonObject receipt = LoadObject(receiptPath);
        JsonObject command = receipt["verification"]!
            .AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["name"]!.GetValue<string>() == "oq8-platform-closure");
        mutation(command);
        WriteObject(receiptPath, receipt);
        ResealV2Receipt(successor, "test");
    }

    private static void MutateV2Timestamp(
        string successor,
        string relative,
        string field,
        string value,
        Action<string> reseal)
    {
        string path = Path.Combine(successor, relative);
        JsonObject document = LoadObject(path);
        document[field] = value;
        WriteObject(path, document);
        reseal(successor);
    }

    private static void ResealV2Receipt(string successor, string role)
    {
        string receiptPath = Path.Combine(successor, "reviews", role + ".json");
        string handoffPath = Path.Combine(successor, "source-only-handoff.json");
        JsonObject handoff = LoadObject(handoffPath);
        handoff["reviewReceipts"]![role] = ComputeSha256(receiptPath);
        WriteObject(handoffPath, handoff);
        ResealV2Manifest(successor);
    }

    private static void ResealV2Manifest(string successor)
    {
        string[] relativeFiles =
        [
            "limitations.json",
            "pre-review-execution.json",
            "review-subject.json",
            "reviews/architecture.json",
            "reviews/security.json",
            "reviews/test.json",
            "source-artifact-identity.json",
            "source-only-handoff.json",
            "validator-sha256.txt",
        ];
        File.WriteAllText(
            Path.Combine(successor, "closure-sha256.txt"),
            string.Join('\n', relativeFiles.Select(relative => $"{ComputeSha256(Path.Combine(successor, relative))}  {relative}")) + "\n");
    }

    private static string InjectDuplicateEventStorePlatformComplete(string json)
    {
        MatchCollection matches = EventStorePlatformCompleteTrue.Matches(json);
        matches.Count.ShouldBe(1, "Expected exactly one eventStorePlatformComplete=true field before duplicate injection.");
        string duplicated = EventStorePlatformCompleteTrue.Replace(
            json,
            match => $"{match.Value},{match.Value}",
            1);
        duplicated.ShouldNotBe(json, "Duplicate authority injection must change the JSON fixture.");
        return duplicated;
    }

    private static void InsertRetiredSprintStatus(string fixture, string shape)
    {
        string statusPath = Path.Combine(fixture, "_bmad-output", "implementation-artifacts", "sprint-status.yaml");
        List<string> lines = File.ReadAllLines(statusPath).ToList();
        int mapping = lines.FindIndex(line => line == "development_status:");
        mapping.ShouldBeGreaterThanOrEqualTo(0);
        const string key = "4-8-durable-admission-evidence-ledger";
        string[] inserted = shape switch
        {
            "plain" => [$"  {key}: done"],
            "double-quoted-key" => [$"  \"{key}\": done"],
            "single-quoted-key" => [$"  '{key}': done"],
            "colon-spacing" => [$"  {key}   :    done"],
            "alternate-indentation" => [$"    {key}: done"],
            "empty" => [$"  {key}:"],
            "null" => [$"  {key}: null"],
            "comment-only" => [$"  {key}: # retired"],
            "quoted-value" => [$"  {key}: \"done\""],
            "flow-sequence" => [$"  {key}: [done]"],
            "flow-mapping" => [$"  {key}: {{ status: done }}"],
            "block-sequence" => [$"  {key}:", "    - done"],
            "tagged-value" => [$"  {key}: !!str done"],
            "anchored-value" => [$"  {key}: &retired done"],
            "aliased-value" => [$"  {key}: *retired"],
            "unicode-colon-spacing" => [$"  {key} : \u00a0done"],
            "escaped-key-x" => ["  \"4\\x2d8-durable-admission-evidence-ledger\": done"],
            "escaped-key-u" => ["  \"4\\u002d8-durable-admission-evidence-ledger\": done"],
            "escaped-key-U" => ["  \"4\\U0000002d8-durable-admission-evidence-ledger\": done"],
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown retired sprint-status shape."),
        };
        if (shape == "alternate-indentation")
        {
            for (int index = mapping + 1; index < lines.Count; index++)
            {
                if (lines[index].Length > 0 && !lines[index].StartsWith(' '))
                {
                    break;
                }

                if (lines[index].StartsWith("  ", StringComparison.Ordinal))
                {
                    lines[index] = "  " + lines[index];
                }
            }
        }

        if (shape == "aliased-value")
        {
            lines.Insert(0, "retired_value: &retired done");
            mapping++;
        }

        lines.InsertRange(mapping + 1, inserted);
        File.WriteAllLines(statusPath, lines);
    }

    private static void InsertNonRetiredSprintStatusText(string fixture, string shape)
    {
        string statusPath = Path.Combine(fixture, "_bmad-output", "implementation-artifacts", "sprint-status.yaml");
        List<string> lines = File.ReadAllLines(statusPath).ToList();
        int mapping = lines.FindIndex(line => line == "development_status:");
        mapping.ShouldBeGreaterThanOrEqualTo(0);
        const string key = "4-8-durable-admission-evidence-ledger";
        switch (shape)
        {
            case "near-match":
                lines.Insert(mapping + 1, $"  {key}-near-match: backlog");
                break;
            case "outside-mapping":
                lines.Add($"{key}: backlog");
                break;
            case "comment":
                lines.Insert(mapping + 1, $"  # {key}: backlog");
                break;
            case "document-markers":
                lines.Insert(0, "---");
                lines.Add("...");
                break;
            case "unrelated-top-level-structure":
                lines.Insert(0, "top_level_sequence:");
                lines.Insert(1, "  - first");
                lines.Insert(2, "  - second");
                break;
            case "stream-initial-bom":
                lines[0] = "\uFEFF" + lines[0];
                break;
            case "unrelated-top-level-alias":
                lines.Insert(0, "alias_source: &alias_source unrelated");
                lines.Insert(1, "*alias_source:");
                lines.Insert(2, "  nested: true");
                break;
            case "unrelated-top-level-tag-anchor-alias":
                lines.Insert(0, "alias_source: !!str &alias_source unrelated");
                lines.Insert(1, "*alias_source: nested");
                break;
            case "unrelated-top-level-anchor-tag-alias":
                lines.Insert(0, "alias_source: &alias_source !!str unrelated");
                lines.Insert(1, "*alias_source:");
                lines.Insert(2, "  nested: true");
                break;
            case "unrelated-top-level-uri-tag-anchor-alias":
                lines.Insert(0, "alias_source: !<tag:yaml.org,2002:str> &alias_source unrelated");
                lines.Insert(1, "*alias_source: nested");
                break;
            case "balanced-top-level-flow-sequence":
                lines.Add("unrelated-flow: [first, second]");
                break;
            case "balanced-top-level-flow-mapping":
                lines.Add("unrelated-flow: {first: second}");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown non-retired sprint-status text shape.");
        }

        File.WriteAllLines(statusPath, lines);
    }

    private static void InsertUnsupportedSprintStatus(string fixture, string shape)
    {
        string statusPath = Path.Combine(fixture, "_bmad-output", "implementation-artifacts", "sprint-status.yaml");
        List<string> lines = File.ReadAllLines(statusPath).ToList();
        int mapping = lines.FindIndex(line => line == "development_status:");
        mapping.ShouldBeGreaterThanOrEqualTo(0);
        switch (shape)
        {
            case "duplicate-development-status":
                lines.Add("development_status:");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "tagged-development-status":
                lines.Add("!!str development_status:");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "anchored-development-status":
                lines.Add("&duplicate development_status:");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "tagged-anchored-development-status":
                lines.Add("!!str &duplicate development_status:");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "anchored-tagged-development-status":
                lines.Add("&duplicate !!str development_status:");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "uri-tagged-development-status":
                lines.Add("!<tag:yaml.org,2002:str> development_status:");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "tagged-escaped-development-status":
                lines.Add("!!str \"development\\u005fstatus\":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "anchored-escaped-development-status":
                lines.Add("&duplicate \"development\\u005fstatus\":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "uri-tagged-escaped-development-status":
                lines.Add("!<tag:yaml.org,2002:str> \"development\\u005fstatus\":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "tagged-anchored-escaped-development-status":
                lines.Add("!!str &duplicate \"development\\u005fstatus\":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "explicit-development-status":
                lines.Add("? development_status");
                lines.Add(":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "explicit-escaped-development-status":
                lines.Add("? \"development\\u005fstatus\"");
                lines.Add(":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "explicit-tagged-escaped-development-status":
                lines.Add("? !!str \"development\\u005fstatus\"");
                lines.Add(": { 4-8-durable-admission-evidence-ledger: done }");
                break;
            case "multiline-explicit-development-status":
                lines.Add("?");
                lines.Add("  development_status");
                lines.Add(":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "multiline-explicit-escaped-development-status":
                lines.Add("?");
                lines.Add("  \"development\\u005fstatus\"");
                lines.Add(":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "multiline-explicit-commented-development-status":
                lines.Add("? # explicit key");
                lines.Add("  !!str development_status # lifecycle");
                lines.Add(":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "multiline-explicit-property-development-status":
                lines.Add("?");
                lines.Add("  !!str");
                lines.Add("  &duplicate");
                lines.Add("  development_status");
                lines.Add(":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "literal-explicit-development-status":
                lines.Add("? |-");
                lines.Add("  development_status");
                lines.Add(":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "folded-explicit-development-status":
                lines.Add("? >-");
                lines.Add("  development_status");
                lines.Add(":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "continued-quoted-explicit-development-status":
                lines.Add("?");
                lines.Add("  \"development_\\");
                lines.Add("    status\"");
                lines.Add(":");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "implicit-aliased-development-status":
            case "spaced-implicit-aliased-development-status":
            case "punctuated-implicit-aliased-development-status":
                lines.Insert(0, "status_key: &status_key development_status");
                lines.Add(shape switch
                {
                    "implicit-aliased-development-status" => "*status_key:",
                    "spaced-implicit-aliased-development-status" => "*status_key :",
                    _ => "*status.key :",
                });
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "deeper-indented-retired-key":
            {
                int entry = -1;
                for (int index = mapping + 1; index < lines.Count; index++)
                {
                    string line = lines[index];
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                    {
                        continue;
                    }

                    if (!line.StartsWith(' '))
                    {
                        break;
                    }

                    if (line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal))
                    {
                        entry = index;
                        break;
                    }
                }

                entry.ShouldBeGreaterThanOrEqualTo(0);
                lines.Insert(entry + 1, "    4-8-durable-admission-evidence-ledger: done");
                break;
            }
            case "normalized-duplicate-x":
                lines.Insert(mapping + 1, "  \"epic\\x2d4\": in-progress");
                break;
            case "normalized-duplicate-u":
                lines.Insert(mapping + 1, "  \"epic\\u002d4\": in-progress");
                break;
            case "normalized-duplicate-U":
                lines.Insert(mapping + 1, "  \"epic\\U0000002d4\": in-progress");
                break;
            case "multiple-documents":
                lines.Add("...");
                lines.Add("---");
                lines.Add("development_status:");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "inline-document-start":
                lines.Add("--- development_status:");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "indented-document-start":
                lines.Add("  ---");
                lines.Add("development_status:");
                lines.Add("  4-8-durable-admission-evidence-ledger: done");
                break;
            case "indented-inline-document-start":
                lines.Add("  --- development_status:");
                lines.Add("    4-8-durable-admission-evidence-ledger: done");
                break;
            case "inline-document-end":
                lines.Add("... trailing-content");
                break;
            case "indented-document-end":
                lines.Add("  ...");
                break;
            case "non-initial-bom":
                lines.Insert(mapping + 1, "  \uFEFFunrelated: done");
                break;
            case "non-printable-source":
                lines.Insert(mapping + 1, "  # forbidden \u0001 source character");
                break;
            case "hostile-duplicate-key":
            {
                string hostileKey = "hostile-" + new string('x', 50_000);
                lines.Insert(mapping + 1, $"  {hostileKey}: done");
                lines.Insert(mapping + 2, $"  {hostileKey}: done");
                break;
            }
            case "tagged-development-status-inline":
                lines.Add("!!str development_status: { 4-8-durable-admission-evidence-ledger: done }");
                break;
            case "tagged-anchored-development-status-inline":
                lines.Add("!!str &duplicate development_status: [done]");
                break;
            case "anchored-tagged-development-status-inline":
                lines.Add("&duplicate !!str development_status: null");
                break;
            case "aliased-development-status-inline":
                lines.Insert(0, "status_key: !!str &status_key development_status");
                lines.Add("*status_key: { 4-8-durable-admission-evidence-ledger: done }");
                break;
            case "malformed-top-level-token":
                lines.Add("%invalid top-level token");
                break;
            case "malformed-top-level-scalar":
                lines.Add("unmapped-top-level-scalar");
                break;
            case "indented-shadow-development-status":
                lines.Add("  development_status:");
                lines.Add("    4-8-durable-admission-evidence-ledger: done");
                break;
            case "unclosed-top-level-flow":
                lines.Add("unrelated-flow: [first, second");
                break;
            case "balanced-top-level-flow-double-comma-sequence":
                lines.Add("unrelated-flow: [first,,second]");
                break;
            case "balanced-top-level-flow-leading-comma-sequence":
                lines.Add("unrelated-flow: [,first]");
                break;
            case "balanced-top-level-flow-double-comma-mapping":
                lines.Add("unrelated-flow: {first: second,,third: fourth}");
                break;
            case "nested-aliased-development-status-block":
                lines.Add("nested:");
                lines.Add("  status-key: &status-key development_status");
                lines.Add("  *status-key:");
                lines.Add("    4-8-durable-admission-evidence-ledger: done");
                break;
            case "nested-aliased-development-status-inline":
                lines.Add("nested:");
                lines.Add("  status-key: &status-key development_status");
                lines.Add("  *status-key: {4-8-durable-admission-evidence-ledger: done}");
                break;
            case "nested-flow-development-status-literal":
                lines.Add("nested:");
                lines.Add("  shadow: {development_status: {4-8-durable-admission-evidence-ledger: done}}");
                break;
            case "nested-flow-development-status-escaped":
                lines.Add("nested:");
                lines.Add("  shadow: {\"development\\u005fstatus\": {4-8-durable-admission-evidence-ledger: done}}");
                break;
            case "nested-flow-development-status-anchor-alias":
                lines.Add("nested:");
                lines.Add("  shadow-source: &shadow-source {development_status: {4-8-durable-admission-evidence-ledger: done}}");
                lines.Add("  shadow-copy: *shadow-source");
                break;
            case "nested-sequence-development-status":
                lines.Add("nested:");
                lines.Add("  - development_status:");
                lines.Add("      4-8-durable-admission-evidence-ledger: done");
                break;
            case "nested-sequence-development-status-escaped":
                lines.Add("nested:");
                lines.Add("  - \"development\\u005fstatus\":");
                lines.Add("      4-8-durable-admission-evidence-ledger: done");
                break;
            case "nested-double-sequence-development-status":
                lines.Add("nested:");
                lines.Add("  - - development_status:");
                lines.Add("        4-8-durable-admission-evidence-ledger: done");
                break;
            case "nested-double-sequence-development-status-escaped":
                lines.Add("nested:");
                lines.Add("  - - \"development\\u005fstatus\":");
                lines.Add("        4-8-durable-admission-evidence-ledger: done");
                break;
            case "nested-triple-sequence-development-status":
                lines.Add("nested:");
                lines.Add("  - - - development_status:");
                lines.Add("          4-8-durable-admission-evidence-ledger: done");
                break;
            case "nested-triple-sequence-development-status-escaped":
                lines.Add("nested:");
                lines.Add("  - - - \"development\\u005fstatus\":");
                lines.Add("          4-8-durable-admission-evidence-ledger: done");
                break;
            case "nested-explicit-sequence-development-status":
                lines.Add("nested:");
                lines.Add("  - ?");
                lines.Add("      development_status");
                lines.Add("    :");
                lines.Add("      4-8-durable-admission-evidence-ledger: done");
                break;
            case "nested-explicit-sequence-development-status-escaped":
                lines.Add("nested:");
                lines.Add("  - ?");
                lines.Add("      \"development\\u005fstatus\"");
                lines.Add("    :");
                lines.Add("      4-8-durable-admission-evidence-ledger: done");
                break;
            case "nested-unclosed-quoted-scalar":
                lines.Add("unrelated:");
                lines.Add("  nested: \"unclosed");
                break;
            case "malformed-explicit-key-unclosed-flow":
                lines.Add("? unrelated-explicit-key");
                lines.Add(": [first, second");
                break;
            case "multiple-tag-properties":
                lines.Add("unrelated: !!str !!str value");
                break;
            case "anchored-root-mapping":
                lines.Insert(0, "&root");
                break;
            case "tagged-root-mapping":
                lines.Insert(0, "!!map");
                break;
            case "tagged-anchored-root-mapping":
                lines.Insert(0, "!!map &root");
                break;
            case "oversized-source":
                lines.Insert(0, "# " + new string('x', 1_048_576));
                break;
            case "merge-key":
                lines.Insert(0, "shared-statuses: &shared-statuses");
                lines.Insert(1, "  shared: done");
                mapping += 2;
                lines.Insert(mapping + 1, "  <<: *shared-statuses");
                break;
            case "duplicate-unrelated-key":
                lines.Insert(mapping + 1, "  duplicate-unrelated: done");
                lines.Insert(mapping + 2, "  duplicate-unrelated: backlog");
                break;
            case "sequence":
            case "tagged-retired-key":
            case "anchored-retired-key":
                lines.Insert(
                    mapping + 1,
                    shape switch
                    {
                        "sequence" => "  - epic-4: in-progress",
                        "tagged-retired-key" => "  !!str 4-8-durable-admission-evidence-ledger: done",
                        "anchored-retired-key" => "  &retired 4-8-durable-admission-evidence-ledger: done",
                        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown unsupported sprint-status shape."),
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown unsupported sprint-status shape.");
        }

        File.WriteAllLines(statusPath, lines);
    }

    private static void ReplaceActiveSprintStatus(string fixture, string shape)
    {
        string statusPath = Path.Combine(fixture, "_bmad-output", "implementation-artifacts", "sprint-status.yaml");
        List<string> lines = File.ReadAllLines(statusPath).ToList();
        if (shape == "sole-explicit-development-status")
        {
            int mapping = lines.FindIndex(line => line == "development_status:");
            mapping.ShouldBeGreaterThanOrEqualTo(0);
            lines[mapping] = "? development_status";
            lines.Insert(mapping + 1, ":");
            File.WriteAllLines(statusPath, lines);
            return;
        }

        const string key = "epic-4";
        int active = lines.FindIndex(line => line == $"  {key}: in-progress");
        active.ShouldBeGreaterThanOrEqualTo(0);
        string[] replacement = shape switch
        {
            "single-quoted-scalars" => [$"  '{key}': 'in-progress'"],
            "single-quoted-escape" => [$"  {key}: in-progress", "  'owner''s-status': done"],
            "double-quoted-escapes" => ["  \"epic\\u002d4\": \"in\\u002dprogress\""],
            "tab-separator" => [$"  {key}:\tin-progress"],
            "missing-separator" => [$"  {key}:in-progress"],
            "unicode-separator" => [$"  {key}:\u00a0in-progress"],
            "unicode-leading-whitespace" => [$"\u00a0 {key}: in-progress"],
            "malformed-single-quoted-key" => ["  'epic'4': in-progress"],
            "malformed-single-quoted-value" => [$"  {key}: 'in'progress'"],
            "empty-value" => [$"  {key}:"],
            "null-value" => [$"  {key}: null"],
            "tagged-value" => [$"  {key}: !!str in-progress"],
            "anchored-value" => [$"  {key}: &current in-progress"],
            "aliased-value" => [$"  {key}: *current"],
            "flow-sequence-value" => [$"  {key}: [in-progress]"],
            "flow-mapping-value" => [$"  {key}: {{ status: in-progress }}"],
            "block-scalar-value" => [$"  {key}: |", "    in-progress"],
            "unrelated-sequence-token" => [$"  {key}: in-progress", "  unrelated: - item"],
            "unrelated-mapping-token" => [$"  {key}: in-progress", "  unrelated: done: invalid"],
            "unrelated-directive-token" => [$"  {key}: in-progress", "  unrelated: %invalid"],
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown active sprint-status shape."),
        };
        lines.RemoveAt(active);
        lines.InsertRange(active, replacement);
        File.WriteAllLines(statusPath, lines);
    }

    private static string PreviousCalendarDay(string isoDate) =>
        DateOnly.ParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .AddDays(-1)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void SetFinalLifecycle(string fixture)
    {
        SetStory415Lifecycle(fixture, "review", "done");
    }

    private static void SetCandidateLifecycle(string fixture)
    {
        SetStory415Lifecycle(fixture, "in-progress", "in-review");
    }

    private static void SetStory415Lifecycle(string fixture, string sprintStatus, string specStatus)
    {
        string artifacts = Path.Combine(fixture, "_bmad-output", "implementation-artifacts");
        string sprintPath = Path.Combine(artifacts, "sprint-status.yaml");
        string[] sprint = File.ReadAllLines(sprintPath);
        int[] sprintMatches = sprint
            .Select((line, index) => (line, index))
            .Where(item => item.line.StartsWith("  4-15-oq8-platform-closure-and-handoff:", StringComparison.Ordinal))
            .Select(item => item.index)
            .ToArray();
        sprintMatches.Length.ShouldBe(1);
        sprint[sprintMatches[0]] = $"  4-15-oq8-platform-closure-and-handoff: {sprintStatus}";
        File.WriteAllLines(sprintPath, sprint);

        string specPath = Path.Combine(artifacts, "spec-4-15-oq8-platform-closure-and-handoff.md");
        string[] spec = File.ReadAllLines(specPath);
        int closingFrontmatter = Array.FindIndex(spec, 1, line => line == "---");
        closingFrontmatter.ShouldBeGreaterThan(1);
        int[] statusMatches = spec
            .Take(closingFrontmatter)
            .Select((line, index) => (line, index))
            .Where(item => item.line.StartsWith("status:", StringComparison.Ordinal))
            .Select(item => item.index)
            .ToArray();
        statusMatches.Length.ShouldBe(1);
        spec[statusMatches[0]] = $"status: '{specStatus}'";
        File.WriteAllLines(specPath, spec);
    }

    private static string CreateCandidateFixture(string root)
    {
        string fixture = Path.Combine(Path.GetTempPath(), "oq8-candidate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        string[] files =
        [
            "_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md",
            "_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json",
            "_bmad-output/implementation-artifacts/spec-4-11-admission-state-machine-and-current-fence-enforcement.md",
            "_bmad-output/implementation-artifacts/spec-4-12-expiry-compaction-and-tombstone-retention.md",
            "_bmad-output/implementation-artifacts/spec-4-13-legacy-admission-migration-and-fail-closed-reconciliation.md",
            "_bmad-output/implementation-artifacts/spec-4-14-oq8-multi-host-production-evidence.md",
            "_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md",
            "_bmad-output/implementation-artifacts/sprint-status.yaml",
            "deploy/dapr/resiliency.yaml",
            "deploy/dapr/statestore-postgresql.yaml",
            "docs/concepts/architecture-overview.md",
            "docs/concepts/command-lifecycle.md",
            "docs/ci.md",
            "docs/guides/configuration-reference.md",
            "docs/reference/command-api.md",
            ".github/workflows/ci.yml",
            ".github/workflows/integration.yml",
            "global.json",
            "requirements-oq8.txt",
            "tests/Directory.Build.props",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs",
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/AssemblyInfo.cs",
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DockerPublishedPortResolver.cs",
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DockerPublishedPortResolverTests.cs",
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs",
            "tools/validate-oq8-platform-evidence.py",
        ];
        foreach (string relative in files)
        {
            CopyFile(root, fixture, relative);
        }

        CopyDirectory(
            Path.Combine(root, "_bmad-output", "implementation-artifacts", "evidence", "story-4-14"),
            Path.Combine(fixture, "_bmad-output", "implementation-artifacts", "evidence", "story-4-14"));

        string closureRelative = Path.Combine(
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-4-15",
            LandedSource);
        foreach (string relative in new[]
        {
            "capture-packet-v1.json",
            "closure-crosswalk.json",
            "limitations.json",
            "pre-review-execution.json",
            "review-subject.json",
            "source-artifact-identity.json",
            "validator-sha256.txt",
        })
        {
            CopyFile(root, fixture, Path.Combine(closureRelative, relative));
        }

        CopyFile(
            root,
            fixture,
            Path.Combine(
                "_bmad-output",
                "implementation-artifacts",
                "evidence",
                "story-4-15",
                "successors",
                SuccessorDirectory,
                "source-artifact-identity.json"));

        SetCandidateLifecycle(fixture);
        return fixture;
    }

    private static string CreateFixture(string root)
    {
        string fixture = Path.Combine(Path.GetTempPath(), "oq8-closure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        string[] files =
        [
            "_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml",
            "_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md",
            "_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json",
            "_bmad-output/implementation-artifacts/spec-4-11-admission-state-machine-and-current-fence-enforcement.md",
            "_bmad-output/implementation-artifacts/spec-4-12-expiry-compaction-and-tombstone-retention.md",
            "_bmad-output/implementation-artifacts/spec-4-13-legacy-admission-migration-and-fail-closed-reconciliation.md",
            "_bmad-output/implementation-artifacts/spec-4-14-oq8-multi-host-production-evidence.md",
            "_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md",
            "_bmad-output/implementation-artifacts/sprint-status.yaml",
            "deploy/dapr/resiliency.yaml",
            "deploy/dapr/statestore-postgresql.yaml",
            "docs/concepts/architecture-overview.md",
            "docs/concepts/command-lifecycle.md",
            "docs/ci.md",
            "docs/guides/configuration-reference.md",
            "docs/reference/command-api.md",
            ".github/workflows/ci.yml",
            ".github/workflows/integration.yml",
            "global.json",
            "requirements-oq8.txt",
            "tests/Directory.Build.props",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs",
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/AssemblyInfo.cs",
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DockerPublishedPortResolver.cs",
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DockerPublishedPortResolverTests.cs",
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs",
            "tools/validate-oq8-platform-evidence.py",
        ];
        foreach (string relative in files)
        {
            CopyFile(root, fixture, relative);
        }

        CopyDirectory(
            Path.Combine(root, "_bmad-output", "implementation-artifacts", "evidence", "story-4-14"),
            Path.Combine(fixture, "_bmad-output", "implementation-artifacts", "evidence", "story-4-14"));
        CopyDirectory(
            Path.Combine(root, "_bmad-output", "implementation-artifacts", "evidence", "story-4-15"),
            Path.Combine(fixture, "_bmad-output", "implementation-artifacts", "evidence", "story-4-15"));
        CopyDirectory(
            Path.Combine(root, V2SuccessorRelativeDirectory),
            Path.Combine(fixture, V2SuccessorRelativeDirectory));
        SetFinalLifecycle(fixture);
        return fixture;
    }

    private static string CreateGitFixture(string root)
    {
        string fixture = Path.Combine(Path.GetTempPath(), "oq8-git-" + Guid.NewGuid().ToString("N"));
        RunGit(root, "clone", "--shared", "--quiet", root, fixture);
        return fixture;
    }

    private static void CopyFile(string sourceRoot, string destinationRoot, string relative)
    {
        string destination = Path.Combine(destinationRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(Path.Combine(sourceRoot, relative), destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static JsonObject LoadObject(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();

    private static void WriteObject(string path, JsonObject value) =>
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(value));

    private static Process CreatePythonProcess(string code)
    {
        Process process = new()
        {
            StartInfo = new ProcessStartInfo("python3")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add(code);
        return process;
    }

    private static (int ExitCode, string Output) RunObservationValidator(
        string repositoryRoot,
        string observationsPath,
        string expectedRuntimeVersion,
        string expectedPostgresImage)
    {
        using Process process = CreatePythonProcess(
            """
            import importlib.util
            import pathlib
            import sys

            specification = importlib.util.spec_from_file_location("oq8_validator", sys.argv[1])
            validator = importlib.util.module_from_spec(specification)
            specification.loader.exec_module(validator)
            try:
                validator.validate_observations(pathlib.Path(sys.argv[2]), sys.argv[3], sys.argv[4])
            except validator.EvidenceError as error:
                print(str(error))
                raise SystemExit(1)
            """);
        process.StartInfo.WorkingDirectory = repositoryRoot;
        process.StartInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "tools", "validate-oq8-platform-evidence.py"));
        process.StartInfo.ArgumentList.Add(observationsPath);
        process.StartInfo.ArgumentList.Add(expectedRuntimeVersion);
        process.StartInfo.ArgumentList.Add(expectedPostgresImage);

        (int exitCode, string output, bool timedOut) = RunProcess(process, 30_000);
        timedOut.ShouldBeFalse("OQ8 observation validator timed out.");
        return (exitCode, output);
    }

    private static (int ExitCode, string Output) RunCtrfSanitizer(
        string repositoryRoot,
        string inputPath,
        string outputPath,
        bool support)
    {
        using Process process = CreatePythonProcess(
            """
            import importlib.util
            import pathlib
            import sys

            specification = importlib.util.spec_from_file_location("oq8_validator", sys.argv[1])
            validator = importlib.util.module_from_spec(specification)
            specification.loader.exec_module(validator)
            try:
                sanitizer = validator.sanitize_support_ctrf if sys.argv[4] == "support" else validator.sanitize_ctrf
                sanitizer(pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3]))
            except validator.EvidenceError as error:
                print(str(error))
                raise SystemExit(1)
            """);
        process.StartInfo.WorkingDirectory = repositoryRoot;
        process.StartInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "tools", "validate-oq8-platform-evidence.py"));
        process.StartInfo.ArgumentList.Add(inputPath);
        process.StartInfo.ArgumentList.Add(outputPath);
        process.StartInfo.ArgumentList.Add(support ? "support" : "focused");

        (int exitCode, string output, bool timedOut) = RunProcess(process, 30_000);
        timedOut.ShouldBeFalse("OQ8 CTRF sanitizer timed out.");
        return (exitCode, output);
    }

    private static (int ExitCode, string Output, bool TimedOut) RunProcess(Process process, int timeoutMilliseconds)
    {
        process.Start().ShouldBeTrue();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(timeoutMilliseconds);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        Task.WaitAll([standardOutput, standardError], 5_000).ShouldBeTrue("Process output drain timed out.");
        return (process.ExitCode, standardOutput.Result + standardError.Result, !exited);
    }

    private static (int ExitCode, string Output) RunValidator(
        string repositoryRoot,
        string artifactRoot,
        string? gitRoot = null,
        bool preReview = false,
        double gitTimeoutSeconds = 30,
        string? executablePathPrefix = null,
        string? lifecycleMode = null,
        IReadOnlyList<string>? additionalArguments = null,
        string? pythonPathPrefix = null,
        bool disableSitePackages = false)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("python3")
            {
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        if (disableSitePackages)
        {
            process.StartInfo.ArgumentList.Add("-S");
        }

        process.StartInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "tools", "validate-oq8-platform-evidence.py"));
        process.StartInfo.ArgumentList.Add("--root");
        process.StartInfo.ArgumentList.Add(artifactRoot);
        process.StartInfo.ArgumentList.Add("--git-root");
        process.StartInfo.ArgumentList.Add(gitRoot ?? repositoryRoot);
        process.StartInfo.ArgumentList.Add("--git-timeout-seconds");
        process.StartInfo.ArgumentList.Add(gitTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (preReview)
        {
            process.StartInfo.ArgumentList.Add("--pre-review");
        }

        if (lifecycleMode is not null)
        {
            process.StartInfo.ArgumentList.Add("--lifecycle-mode");
            process.StartInfo.ArgumentList.Add(lifecycleMode);
        }

        if (additionalArguments is not null)
        {
            foreach (string argument in additionalArguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
        }

        if (executablePathPrefix is not null)
        {
            process.StartInfo.Environment["PATH"] = executablePathPrefix
                + Path.PathSeparator
                + Environment.GetEnvironmentVariable("PATH");
        }

        if (pythonPathPrefix is not null)
        {
            process.StartInfo.Environment["PYTHONPATH"] = pythonPathPrefix;
        }

        (int exitCode, string output, bool timedOut) = RunProcess(process, 30_000);
        timedOut.ShouldBeFalse("OQ8 closure validator timed out.");
        return (exitCode, output);
    }

    private static string RunGit(string root, params string[] arguments)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        (int exitCode, string output, bool timedOut) = RunProcess(process, 30_000);
        timedOut.ShouldBeFalse("Git identity command timed out.");
        exitCode.ShouldBe(0, output);
        return output.Trim();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Hexalith.EventStore repository root.");
    }
}
