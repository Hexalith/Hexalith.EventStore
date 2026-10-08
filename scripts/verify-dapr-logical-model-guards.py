#!/usr/bin/env python3
"""Exercise dormant logical codecs/source/operation guards in timed compiling lanes.

Only disposable source copies are mutated. These controls establish no actual
cross-actor invocation, source transport, catalog or production qualification.
"""

from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import math
import pathlib
import shutil
import subprocess
import tempfile
import time
from xml.sax.saxutils import quoteattr


ROOT = pathlib.Path(__file__).resolve().parents[1]
CLIENT = pathlib.Path("src/Hexalith.EventStore.Client")
SERVER = pathlib.Path("src/Hexalith.EventStore.Server")
DOMAIN = pathlib.Path("src/Hexalith.EventStore.DomainService")
CONTRACTS = pathlib.Path("src/Hexalith.EventStore.Contracts")
DEFAULTS = pathlib.Path("src/Hexalith.EventStore.ServiceDefaults")
UNIQUE_IDS = pathlib.Path("references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds")
CTESTS = pathlib.Path("tests/Hexalith.EventStore.Client.Tests")
STESTS = pathlib.Path("tests/Hexalith.EventStore.Server.Tests")
VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-model-2026-10-08/vectors.json")
COMMANDS: list[dict] = []
CONFIGURATION = "Debug"
DEPENDENCY_MODE = "source"


def run(command: list[str], work: pathlib.Path, log: pathlib.Path, timeout: int):
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    selected = [path for path in work.rglob("*") if path.is_file() and "bin" not in path.parts and "obj" not in path.parts]
    source_hashes = {str(path.relative_to(work)): hashlib.sha256(path.read_bytes()).hexdigest() for path in selected}
    helper_inputs = [ROOT / path for path in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
                                             "tests/Directory.Build.props", "scripts/verify-dapr-logical-model-guards.py")]
    helper_inputs += [path for path in (ROOT / "references/Hexalith.Builds").rglob("*")
                     if path.is_file() and ".git" not in path.parts and path.suffix in (".props", ".targets", ".json", ".config")]
    helper_hashes = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in helper_inputs}
    assemblies = list(pathlib.Path(command[1]).parent.glob("*.dll")) if command[0] == "dotnet" and command[1].endswith(".dll") else []
    executed_before = {str(path.relative_to(work)): hashlib.sha256(path.read_bytes()).hexdigest() for path in assemblies}
    try:
        result = subprocess.run(command, cwd=work, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=timeout, check=False)
    except subprocess.TimeoutExpired as error:
        output = error.stdout or b""
        log.write_text(output.decode() if isinstance(output, bytes) else output, encoding="utf-8")
        raise RuntimeError(f"Timed out after {timeout}s: {command}; see {log}") from error
    log.write_text(result.stdout, encoding="utf-8")
    after = {str(path.relative_to(work)): hashlib.sha256(path.read_bytes()).hexdigest() for path in selected}
    helpers_after = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in helper_inputs}
    executed_after = {str(path.relative_to(work)): hashlib.sha256(path.read_bytes()).hexdigest() for path in assemblies}
    COMMANDS.append({"argv": command, "cwd": str(work), "startedUtc": started,
                     "endedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "exitCode": result.returncode,
                     "log": log.name, "allLaneInputsSha256Before": source_hashes, "allLaneInputsSha256After": after,
                     "importedHelpersSha256Before": helper_hashes, "importedHelpersSha256After": helpers_after,
                     "laneInputsUnchanged": source_hashes == after, "importedHelpersUnchanged": helper_hashes == helpers_after,
                     "executedDllsSha256Before": executed_before, "executedDllsSha256After": executed_after,
                     "executedDllsUnchanged": executed_before == executed_after})
    if source_hashes != after or helper_hashes != helpers_after or executed_before != executed_after:
        log.with_suffix(".input-drift.json").write_text(json.dumps(COMMANDS[-1], indent=2) + "\n", encoding="utf-8")
        raise RuntimeError("Pinned lane or imported helper inputs changed during the command")
    return result


def prepare(work: pathlib.Path) -> None:
    for directory in (CLIENT, DOMAIN, SERVER, CONTRACTS, DEFAULTS):
        shutil.copytree(ROOT / directory, work / directory, ignore=shutil.ignore_patterns("bin", "obj"))
    shutil.copytree(ROOT / UNIQUE_IDS, work / UNIQUE_IDS, ignore=shutil.ignore_patterns("bin", "obj"))
    for filename in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
        override = f'<PropertyGroup><HexalithCommonsRoot>{work / "references/Hexalith.Commons"}</HexalithCommonsRoot></PropertyGroup>' if filename == "Directory.Build.props" else ""
        (work / filename).write_text(f"<Project><Import Project={quoteattr(str(ROOT / filename))} />{override}</Project>\n", encoding="utf-8")
    shutil.copyfile(ROOT / "global.json", work / "global.json")
    (work / "Hexalith.EventStore.slnx").write_text("<Solution />\n", encoding="utf-8")
    (work / VECTOR).parent.mkdir(parents=True)
    shutil.copyfile(ROOT / VECTOR, work / VECTOR)
    for target, files, dependency in (
        (CTESTS, ("DaprLogicalClaimCodecTests.cs", "DaprLogicalClaimTimeProvider.cs", "DaprLogicalStreamingExtensions.cs"), CLIENT),
        (STESTS, ("DaprLogicalReplayFixture.cs", "DaprLogicalReplayTestValue.cs", "DaprLogicalReplayTests.cs",
                  "DaprLogicalReplayTimeProvider.cs", "DaprReplayTestStore.cs", "DaprLogicalReconstructionUpcaster.cs"), SERVER),
    ):
        (work / target / "Events").mkdir(parents=True)
        for filename in files:
            shutil.copyfile(ROOT / target / "Events" / filename, work / target / "Events" / filename)
        (work / target / f"{target.name}.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup><ItemGroup>'
            f'<ProjectReference Include="../../{dependency}/{dependency.name}.csproj" />'
            '<FrameworkReference Include="Microsoft.AspNetCore.App" />'
            '<PackageReference Include="Microsoft.NET.Test.Sdk" /><PackageReference Include="xunit.v3" />'
            '<PackageReference Include="Shouldly" /><PackageReference Include="NSubstitute" />'
            '</ItemGroup><ItemGroup><Using Include="Xunit" /></ItemGroup></Project>\n', encoding="utf-8")
    (work / CTESTS / "Events/Fixtures").mkdir()
    shutil.copyfile(ROOT / CTESTS / "Events/Fixtures/EventRegistryV17.json", work / CTESTS / "Events/Fixtures/EventRegistryV17.json")
    (work / "tests/Directory.Build.props").write_text(
        f"<Project><Import Project={quoteattr(str(ROOT / 'tests/Directory.Build.props'))} /></Project>\n", encoding="utf-8")


def execute(work: pathlib.Path, output: pathlib.Path, label: str, target: pathlib.Path, timeout: int):
    started = time.monotonic()
    deadline = started + timeout
    project = work / target / f"{target.name}.csproj"
    build = run(["dotnet", "build", str(project), "--configuration", CONFIGURATION,
                 "-p:UseHexalithProjectReferences=" + ("true" if DEPENDENCY_MODE == "source" else "false"), "-m:1", "--nologo"],
                work, output / f"{label}-build.log", timeout)
    if build.returncode:
        raise RuntimeError(f"Lane did not compile for {label}; see {output / f'{label}-build.log'}")
    remaining = math.floor(deadline - time.monotonic())
    if remaining < 1:
        raise RuntimeError(f"Lane {label} exhausted its {timeout}s deadline before execution")
    assembly = work / target / f"bin/{CONFIGURATION}/net10.0/{target.name}.dll"
    result = run(["dotnet", str(assembly)], work, output / f"{label}-tests.log", remaining)
    return result, round(time.monotonic() - started, 3)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--timeout", type=int, default=60)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--dependency-mode", choices=("source", "packages"), default="source")
    args = parser.parse_args()
    global CONFIGURATION, DEPENDENCY_MODE
    CONFIGURATION = args.configuration
    DEPENDENCY_MODE = args.dependency_mode
    if not 1 <= args.timeout <= 60:
        parser.error("timeout must be between 1 and 60 seconds per lane")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    mutations = [
        ("registry-instance-disposal", CLIENT / "Events/EventDomainRegistry.cs",
         "Volatile.Read(ref _disposed) != 0", "Volatile.Read(ref _disposed) < 0", 1, STESTS,
         "DisposedRegistryEmptyTargetRefusesBeforeSigningOrSavingWithoutPoisoningSharedLoss"),
        ("initial-atomic-participant-admission", SERVER / "Events/DaprReplayOperationOwner.cs",
         "if (!await RequireInitialAbsenceAsync(cancellationToken).ConfigureAwait(false))",
         "if (ownerId.Length < 0)", 1, STESTS,
         "BeginWithoutOperationRefusesOrphanParticipantsBeforeSavingAnyPointer"),
        ("legacy-evidence-opt-in", SERVER / "Events/DaprLogicalEventReader.cs",
         "bool computeApplicationLogicalDigest = false", "bool computeApplicationLogicalDigest = true", 1, STESTS,
         "LegacyReaderLeavesApplicationEvidenceOptInWhilePreservingPayloadBytes"),
        ("source-metadata-presence", SERVER / "Events/DaprLogicalEventReader.cs",
         "binding.MetadataPresent != metadata.HasValue || ", "", 1, STESTS,
         "ZeroValuedActorMetadataPresenceToggleRefusesBeforeEmptyProof"),
        ("source-trust-before-callback", SERVER / "Events/DaprLogicalReplaySource.cs",
         "if (trust.Domain != _identity.Domain || !ReferenceEquals(trust.CapabilityLoss, _evolution.CapabilityLoss)\n            || !trust.RegistryFingerprint.Span.SequenceEqual(Convert.FromHexString(_evolution.RegistryFingerprint)))",
         "if (trust.Domain.Length < 0)", 1, STESTS, "SourceTrustOrBindingMismatchRefusesBeforeAnyCatalogCallback"),
        ("v2-exact-canonical-tuple", CLIENT / "Events/DaprLogicalClaimCodec.cs",
         "claim.StoredEventType != claim.StoredCanonicalType", "claim.StoredEventType.Length < 0", 1, CTESTS,
         "V2StoredEventTypeMustMatchTheExactCanonicalTupleOnEncodeAndDecode"),
        ("trust-disposal-after-time", CLIENT / "Events/DaprLogicalClaimTrust.cs",
         "cancellationToken.ThrowIfCancellationRequested();\n        ObjectDisposedException.ThrowIf(_disposed, this);",
         "cancellationToken.ThrowIfCancellationRequested();", 1, CTESTS, "FinalDecodeBoundaryTrustDisposalRefusesAndReleasesCapacity"),
        ("verified-decoding-retained-charge", CLIENT / "Events/DaprLogicalClaimTrust.cs",
         "budget.Reserve(checked(Math.Min(claim.Length, DaprLogicalClaimCodec.MaximumClaimBytes) * 4 + 4096))",
         "budget.Reserve(0)", 2, CTESTS, "CurrentScopedKeySignsAndVerifiesBothClaimsThenClearsItsRetainedImage"),
        ("response-private-clearing", SERVER / "Events/DaprLogicalResponseOwner.cs",
         "CryptographicOperations.ZeroMemory(bytes);", "GC.KeepAlive(bytes);", 1, STESTS,
         "AddressedSourceMatchesIndependentApplicationConsumedMetadataAndProofFields"),
        ("pending-capacity-retention", SERVER / "Events/DaprReplayOperationOwner.cs",
         "if (_pending?.Prepared != prepared)", "if (prepared is not null)", 1, STESTS,
         "PendingOwnerDeactivationRetainsChargesUntilCacheReleaseAndNeverClearsActorOwnedCopies"),
        ("post-save-response-authorization", SERVER / "Events/DaprReplayOperationOwner.cs",
         "return await AuthorizeCommittedResponseAsync(inspected, source, binding, trust, cancellationToken).ConfigureAwait(false);",
         "return inspected;", 1, STESTS, "PostSaveLossPreservesCommittedTruthAndWithholdsStaleResponse"),
        ("committed-pointer-admission", SERVER / "Events/DaprReplayOperationOwner.cs",
         "existing.Value is null || pageOrdinal > prior.PageOrdinal", "existing.Value is null", 1, STESTS,
         "OrphanAtomicParticipantsRefuseBeforeCallbacksSaveOrOverwrite"),
        ("orphan-blob-admission", SERVER / "Events/DaprReplayOperationOwner.cs",
         "if (orphanResponse.HasValue || orphanFinal.HasValue || orphanState.HasValue || orphanFinalState.HasValue)",
         "if (!orphanResponse.HasValue && orphanFinal.HasValue)", 1, STESTS,
         "OrphanAtomicParticipantsRefuseBeforeCallbacksSaveOrOverwrite"),
        ("begin-pointer-only-completion", SERVER / "Events/DaprReplayOperationOwner.cs",
         "RequireOperation(prior, sourceHash, trust.RegistryFingerprint.Span, binding.TargetSequence);\n                if (!await RequireCommittedChainAsync",
         "RequireOperation(prior, sourceHash, trust.RegistryFingerprint.Span, binding.TargetSequence);\n                if (prior.OwnerId == ownerId) { return new(DaprReplayCommitOutcome.Proven, prior.Generation, null, prior.IsComplete); }\n                if (!await RequireCommittedChainAsync", 1, STESTS,
         "EmptyOperationCommitsFinalCountZeroPageAndBeginCannotInventCompletionAfterFinalDeletion"),
        ("deterministic-smaller-page", SERVER / "Events/DaprReplayOperationOwner.cs",
         "count = Math.Max(1, count / 2);", 'throw new InvalidOperationException("ProofLimit: mutation refused smaller page.");', 1, STESTS,
         "ComposedAdmissionShrinksContiguousPagesAndDisposalLeavesDurableImagesUnchanged"),
    ]
    extra_edits = {name: [("\n            if (!await RequireCommittedChainAsync(prior, binding, trust, budget, cancellationToken).ConfigureAwait(false))",
                           "\n            if (prior.Generation < 0)", 1)]
                   for name in ("committed-pointer-admission", "orphan-blob-admission")}
    rows = []
    controls = []
    owned = list((ROOT / CLIENT / "Events").glob("DaprLogical*.cs")) + list((ROOT / SERVER / "Events").glob("DaprReplay*.cs"))
    owned += list((ROOT / SERVER / "Events").glob("DaprLogical*.cs")) + [ROOT / SERVER / "Actors/DaprReplayOperationActor.cs"]
    owned += list((ROOT / CTESTS / "Events").glob("DaprLogical*.cs")) + list((ROOT / STESTS / "Events").glob("DaprLogical*.cs"))
    owned += [ROOT / CLIENT / "Events/EventEvolutionService.cs", ROOT / CLIENT / "Events/EventDomainRegistry.cs", ROOT / STESTS / "Events/DaprReplayTestStore.cs", ROOT / VECTOR, pathlib.Path(__file__)]
    owned += [ROOT / ".github/workflows/event-evolution-local-guards.yml"]
    owned += list((ROOT / CLIENT / "Aggregates").glob("*LogicalReplay*.cs"))
    owned += [ROOT / CLIENT / "Events/EventBufferBudget.cs", ROOT / CLIENT / "Events/ImmutablePayload.cs",
              ROOT / SERVER / "DomainServices/DaprAggregateStateReconstructor.cs", ROOT / "scripts/verify-dapr-logical-model-vectors.py"]
    source_hashes = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in owned}
    dependencies = list((ROOT / UNIQUE_IDS).rglob("*.cs")) + [ROOT / UNIQUE_IDS / "Hexalith.Commons.UniqueIds.csproj"]
    dependencies = [path for path in dependencies if "bin" not in path.parts and "obj" not in path.parts]
    dependency_hashes = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in dependencies}
    with tempfile.TemporaryDirectory(prefix="eventstore-dapr-logical-guards-") as temporary:
        work = pathlib.Path(temporary)
        prepare(work)
        for target in (CTESTS, STESTS):
            label = "control-client" if target == CTESTS else "control-server"
            result, elapsed = execute(work, output, label, target, args.timeout)
            if result.returncode or "Failed: 0" not in result.stdout or "Skipped: 0" not in result.stdout:
                raise RuntimeError(f"Unmutated control failed: {output / f'{label}-tests.log'}")
            controls.append({"lane": label, "result": "passed", "elapsedSeconds": elapsed})
            print(f"{label}: passed", flush=True)
        for name, relative, before, after, count, target, killer in mutations:
            path = work / relative
            original = path.read_bytes()
            try:
                text = original.decode().replace("\r\n", "\n")
                if text.count(before) != count:
                    raise RuntimeError(f"Mutation anchor missing/ambiguous: {name}")
                text = text.replace(before, after)
                for extra_before, extra_after, extra_count in extra_edits.get(name, []):
                    if text.count(extra_before) != extra_count:
                        raise RuntimeError(f"Additional admission anchor missing/ambiguous: {name}")
                    text = text.replace(extra_before, extra_after)
                path.write_text(text, encoding="utf-8")
                result, elapsed = execute(work, output, name, target, args.timeout)
                lines = result.stdout.splitlines()
                failures = [index for index, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if result.returncode == 0 or not failures:
                    raise RuntimeError(f"Mutation survived or missed named killing test: {name}")
                diagnostic = next(line.strip() for line in lines[failures[0] + 1:] if line.strip())
                rows.append({"mutation": name, "result": "killed", "killingTest": killer,
                             "observedFailure": diagnostic, "failureExcerpt": lines[failures[0]:failures[0] + 12],
                             "additionalEdits": extra_edits.get(name, []),
                             "elapsedSeconds": elapsed, "timeoutSecondsPerLane": args.timeout})
                print(f"{name}: killed", flush=True)
            finally:
                path.write_bytes(original)
    receipt = {"scope": "dormant logical source/codecs/local actor state-manager protocol composition",
               "configuration": CONFIGURATION, "dependencyMode": DEPENDENCY_MODE, "controls": controls, "mutations": rows,
               "commands": COMMANDS,
               "ownedSourceSha256": source_hashes, "copiedDependencyInputsSha256": dependency_hashes, "actualCrossActorInvocation": False,
               "transportOrProductionQualification": False, "activationAuthority": False}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
