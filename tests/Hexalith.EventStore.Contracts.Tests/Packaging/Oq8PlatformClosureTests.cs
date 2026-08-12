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
    private const string LandedSource = "4b0a7b1d3628a857f131cfbff99030714aefc747";
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
            WriteObject(fresh, observations);

            (int freshExitCode, string freshOutput) = RunObservationValidator(root, fresh, "1.18.2");
            freshExitCode.ShouldBe(0, freshOutput);

            (int freshCrossModeExitCode, string freshCrossModeOutput) = RunObservationValidator(root, fresh, "1.18.1");
            freshCrossModeExitCode.ShouldBe(1, freshCrossModeOutput);
            freshCrossModeOutput.ShouldContain("Dapr runtime identity drift");

            (int committedExitCode, string committedOutput) = RunObservationValidator(root, committed, "1.18.1");
            committedExitCode.ShouldBe(0, committedOutput);

            (int committedCrossModeExitCode, string committedCrossModeOutput) = RunObservationValidator(root, committed, "1.18.2");
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

            (int exitCode, string output) = RunObservationValidator(root, observationsPath, "1.18.2");

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
    [InlineData("candidate-test-source-body")]
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
    /// Verifies near-match keys and retired-key text outside development_status do not affect lifecycle validation.
    /// </summary>
    /// <param name="shape">The non-entry text shape.</param>
    [Theory]
    [InlineData("near-match")]
    [InlineData("outside-mapping")]
    [InlineData("comment")]
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
    [InlineData("duplicate-development-status")]
    [InlineData("deeper-indented-retired-key")]
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
                    "duplicate-development-status" => "Lifecycle development_status mapping is missing or ambiguous",
                    _ => "Unsupported sprint-status mapping structure",
                });
            output.ShouldNotContain("Traceback");
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
    /// Verifies a changed or deleted non-evolved capability path fails the current-bound-source proof.
    /// </summary>
    /// <param name="mutation">The isolated Git worktree mutation.</param>
    [Theory]
    [InlineData("changed")]
    [InlineData("deleted")]
    public void ChangedOrDeletedBoundCapabilityPathFailsClosed(string mutation)
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

            exitCode.ShouldBe(1, output);
            output.ShouldContain(
                mutation == "changed"
                    ? "Current bound source has index changes"
                    : "Current bound source has semantic working-tree changes");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies Git index visibility flags cannot hide a changed bound capability path.
    /// </summary>
    /// <param name="flag">The forbidden Git index visibility flag.</param>
    [Theory]
    [InlineData("--assume-unchanged")]
    [InlineData("--skip-worktree")]
    public void HiddenBoundCapabilityPathFailsClosed(string flag)
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = CreateGitFixture(root);
        try
        {
            RunGit(gitFixture, "update-index", flag, "--", "deploy/dapr/resiliency.yaml");

            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Current bound source index flags are not normal: deploy/dapr/resiliency.yaml");
            output.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            Directory.Delete(gitFixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the current Git HEAD must descend from the landed OQ8 source commit.
    /// </summary>
    [Fact]
    public void NonDescendantHeadFailsClosed()
    {
        string root = FindRepositoryRoot();
        string fixture = CreateCandidateFixture(root);
        string gitFixture = CreateGitFixture(root);
        try
        {
            RunGit(gitFixture, "checkout", "--detach", "--quiet", "e60a3777c581d70b62f67173ccc2372b5b64a425");

            (int exitCode, string output) = RunValidator(root, fixture, gitFixture, preReview: true);

            exitCode.ShouldBe(1, output);
            output.ShouldContain("Git identity proof failed for merge-base --is-ancestor");
            output.ShouldNotContain("Traceback");
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
    [InlineData("_bmad-output/implementation-artifacts/evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/reviews/security.json", "Closure artifact missing: reviews/security.json")]
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
            output.ShouldContain("Git identity proof failed");
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
            output.ShouldContain("Git identity proof for rev-parse");
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
            output.ShouldContain("Git identity proof for rev-parse");
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
                execution["commands"]![2]!["tests"] = 0;
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
                InsertDuplicateJsonField(subjectPath, "  \"createdOn\": \"2026-08-12\",", "  \"createdOn\": \"2026-08-12\",\n  \"createdOn\": \"2026-08-12\",");
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
        "candidate-test-source-body" => "Pre-review execution test-source identity drift",
        "candidate-execution-validator" => "Pre-review execution validator identity drift",
        "candidate-execution-test-source" => "Pre-review execution test-source identity drift",
        "candidate-execution-summary-type" => "must be an exact integer",
        "candidate-execution-command-name-duplicate" => "Pre-review execution command names must be exact and unique",
        "candidate-execution-command-count-zero" => "Pre-review execution command 2:tests count drift",
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

    private static string ComputeSha256(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

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
            "colon-spacing" => [$"  {key} :done"],
            "alternate-indentation" => [$"    {key}: done"],
            "empty" => [$"  {key}:"],
            "null" => [$"  {key}: null"],
            "comment-only" => [$"  {key}: # retired"],
            "quoted-value" => [$"  {key}: \"done\""],
            "flow-sequence" => [$"  {key}: [done]"],
            "flow-mapping" => [$"  {key}: {{ status: done }}"],
            "block-sequence" => [$"  {key}:", "    - done"],
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
            case "merge-key":
            case "sequence":
            case "tagged-retired-key":
            case "anchored-retired-key":
                lines.Insert(
                    mapping + 1,
                    shape switch
                    {
                        "merge-key" => "  <<: *shared-statuses",
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
            "docs/guides/configuration-reference.md",
            "docs/reference/command-api.md",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs",
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
            "docs/guides/configuration-reference.md",
            "docs/reference/command-api.md",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs",
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
        string expectedRuntimeVersion)
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
                validator.validate_observations(pathlib.Path(sys.argv[2]), sys.argv[3])
            except validator.EvidenceError as error:
                print(str(error))
                raise SystemExit(1)
            """);
        process.StartInfo.WorkingDirectory = repositoryRoot;
        process.StartInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "tools", "validate-oq8-platform-evidence.py"));
        process.StartInfo.ArgumentList.Add(observationsPath);
        process.StartInfo.ArgumentList.Add(expectedRuntimeVersion);

        (int exitCode, string output, bool timedOut) = RunProcess(process, 30_000);
        timedOut.ShouldBeFalse("OQ8 observation validator timed out.");
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
        IReadOnlyList<string>? additionalArguments = null)
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
