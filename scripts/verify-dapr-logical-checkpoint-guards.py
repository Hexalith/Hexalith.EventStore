#!/usr/bin/env python3
"""Seal sequential disposable logical checkpoint issue/readback controls; this does not replace the root solution gate."""
import argparse
import datetime
import hashlib
import math
import importlib.util
import json
import pathlib
import re
import shutil
import subprocess
import time

ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("checkpoint_base", ROOT / "scripts/verify-dapr-logical-model-guards.py")
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
STESTS = BASE.STESTS
CHECKPOINT_VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-checkpoint-owner-2026-10-09/vectors.json")
SNAPSHOT_VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-snapshot-2026-10-09/vectors.json")
CLASS = "Hexalith.EventStore.Server.Tests.Events.DaprLogicalCheckpointTests"
REPLACEMENT_CLASS = "Hexalith.EventStore.Server.Tests.Events.DaprLogicalSnapshotReplacementTests"
VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-anchored-continuation-2026-10-09/vectors.json")
COMMAND_VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-command-state-2026-10-08/vectors.json")
COMMAND_CLASS = "Hexalith.EventStore.Server.Tests.Events.DaprLogicalCommandStateTests"

def hashes(directory):
    return {str(p.relative_to(directory)): hashlib.sha256(p.read_bytes()).hexdigest() for p in directory.rglob("*")
            if p.is_file() and "bin" not in p.parts and "obj" not in p.parts and ".git" not in p.parts}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--work", type=pathlib.Path, default=None)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--dependency-mode", choices=("source", "packages"), default="source")
    parser.add_argument("--timeout", type=int, default=60)
    parser.add_argument("--control-only", action="store_true")
    parser.add_argument("--mutation", action="append", default=[], help="Run only these named mutations after positive controls")
    args = parser.parse_args()
    if not 1 <= args.timeout <= 60: parser.error("timeout must be 1..60 seconds per compiling lane")
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    work = args.work.resolve() if args.work else output / "work"
    if work.exists(): raise SystemExit("Disposable work directory must be new")
    retained = set()
    excluded = []  # Preserve every arriving external runtime dependency; no source substitutions.
    def root_hashes():
        roots = [ROOT / directory for directory in (BASE.CLIENT, BASE.DOMAIN, BASE.SERVER, BASE.CONTRACTS, BASE.DEFAULTS, BASE.CTESTS, BASE.STESTS, BASE.UNIQUE_IDS)]
        roots += [ROOT / "scripts", ROOT / ".github/workflows", ROOT / "references/Hexalith.Builds"]
        paths = [p for directory in roots for p in directory.rglob("*") if p.is_file() and not any(x in p.parts for x in ("bin", "obj", ".git", "__pycache__"))]
        paths += [ROOT / name for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "tests/Directory.Build.props", ".editorconfig", ".gitattributes", "nuget.config", str(VECTOR), str(COMMAND_VECTOR), str(SNAPSHOT_VECTOR), str(CHECKPOINT_VECTOR), "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-anchored-replay-2026-10-09/vectors.json", "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/vectors.json", "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-anchored-continuation-model.md", "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-snapshot-replacement-model.md", "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-snapshot-rewitness-model.md", "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-checkpoint-owner-model.md")]
        return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(paths))}
    root_before = root_hashes()
    (output / "attempt.json").write_text(json.dumps({"rootInputsBefore": root_before, "excludedExternalPaths": excluded, "retainedExternalDependencyNames": sorted(retained), "isolationSubstitutions": [], "fullWorkspaceQualified": False}, indent=2) + "\n")
    BASE.prepare(work)
    for name in (".editorconfig", ".gitattributes", "nuget.config"):
        shutil.copyfile(ROOT / name, work / name)
    for name in excluded:
        (work / name).unlink()
    fixture_files = ("DaprLogicalCheckpointFixture.cs", "DaprLogicalCheckpointTests.cs", "DaprLogicalSnapshotRewitnessFixture.cs", "DaprLogicalSnapshotRewitnessTests.cs", "DaprLogicalSnapshotReplacementFixture.cs", "DaprLogicalSnapshotReplacementTests.cs", "DaprLogicalSnapshotFixture.cs", "DaprLogicalSnapshotTests.cs", "DaprLogicalReconstructionFixture.cs", "DaprLogicalReconstructionTestState.cs", "DaprLogicalReconstructionTests.cs", "DaprLogicalAnchorFixture.cs", "DaprLogicalAnchoredReplayTests.cs", "DaprLogicalAnchoredContinuationFixture.cs", "DaprLogicalAnchoredContinuationTests.cs", "DaprLogicalAnchoredContinuationCodecTests.cs", "DaprLogicalCommandStateTests.cs", "DaprLogicalCommandStateProcessor.cs", "DaprLogicalCommandStateAdmissionStage.cs", "DaprLogicalCommandStateResult.cs", "DaprLogicalCommandStateTestEvent.cs", "DaprLogicalCommandStateSerializedEvent.cs", "DaprLogicalCommandStateEventList.cs")
    for filename in fixture_files:
        shutil.copyfile(ROOT / STESTS / "Events" / filename, work / STESTS / "Events" / filename)
    (work / STESTS / "TestUtilities").mkdir()
    shutil.copyfile(ROOT / STESTS / "TestUtilities/ActorStateManagerTestHelper.cs", work / STESTS / "TestUtilities/ActorStateManagerTestHelper.cs")
    project = work / STESTS / f"{STESTS.name}.csproj"
    source = project.read_text().replace("</ItemGroup><ItemGroup><Using", '<ProjectReference Include="../../src/Hexalith.EventStore.DomainService/Hexalith.EventStore.DomainService.csproj" /></ItemGroup><ItemGroup><Using')
    project.write_text(source)
    (work / VECTOR).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / VECTOR, work / VECTOR)
    (work / SNAPSHOT_VECTOR).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / SNAPSHOT_VECTOR, work / SNAPSHOT_VECTOR)
    old_anchor_vector = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-anchored-replay-2026-10-09/vectors.json")
    (work / old_anchor_vector).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / old_anchor_vector, work / old_anchor_vector)
    reconstruction_vector = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/vectors.json")
    (work / reconstruction_vector).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / reconstruction_vector, work / reconstruction_vector)
    (work / COMMAND_VECTOR).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / COMMAND_VECTOR, work / COMMAND_VECTOR)
    (work / CHECKPOINT_VECTOR).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / CHECKPOINT_VECTOR, work / CHECKPOINT_VECTOR)
    commands = []
    def run(argv, name, timeout=None):
        before = hashes(work)
        def helper_hashes():
            files = [ROOT / name for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "tests/Directory.Build.props", ".editorconfig", ".gitattributes", "nuget.config")]
            files += [p for directory in (ROOT / "references/Hexalith.Builds", ROOT / "scripts") for p in directory.rglob("*")
                      if p.is_file() and not any(x in p.parts for x in (".git", "bin", "obj", "__pycache__")) and p.suffix in (".props", ".targets", ".json", ".config", ".py")]
            return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(files))}
        helpers = helper_hashes()
        assembly_directory = pathlib.Path(argv[1]).parent if argv[0] == "dotnet" and argv[1].endswith(".dll") else None
        dlls = {} if assembly_directory is None else {str(p.relative_to(work)): hashlib.sha256(p.read_bytes()).hexdigest() for p in assembly_directory.glob("*.dll")}
        start = time.monotonic()
        started_utc = datetime.datetime.now(datetime.timezone.utc).isoformat()
        timed_out = False
        try:
            result = subprocess.run(argv, cwd=work, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout or args.timeout, check=False)
        except subprocess.TimeoutExpired as error:
            timed_out = True
            raw = error.stdout or b""
            transcript = raw.decode(errors="replace") if isinstance(raw, bytes) else raw
            result = subprocess.CompletedProcess(argv, 124, transcript + "\nTimed out: lane is unqualified; timeout is not a mutation kill.\n")
        (output / f"{name}.log").write_text(result.stdout)
        after = hashes(work)
        helpers_after = helper_hashes()
        dlls_after = {} if assembly_directory is None else {str(p.relative_to(work)): hashlib.sha256(p.read_bytes()).hexdigest() for p in assembly_directory.glob("*.dll")}
        commands.append({"argv": argv, "cwd": str(work), "exitCode": result.returncode, "timedOut": timed_out, "timeoutSeconds": timeout or args.timeout, "log": f"{name}.log", "seconds": round(time.monotonic()-start, 3), "startedUtc": started_utc, "endedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "dynamicLaneInputsBefore": before, "dynamicLaneInputsAfter": after, "dynamicLaneSetUnchanged": before == after,
            "importedInputsBefore": helpers, "importedInputsAfter": helpers_after, "dynamicImportedSetUnchanged": helpers == helpers_after,
            "executedDllsBefore": dlls, "executedDllsAfter": dlls_after, "dynamicDllSetUnchanged": dlls == dlls_after})
        (output / "commands.json").write_text(json.dumps(commands, indent=2) + "\n")
        if before != after or helpers != helpers_after or dlls != dlls_after: raise RuntimeError("Disposable/imported/executed input set changed")
        if timed_out: raise RuntimeError("Timed out without a behavioral kill: " + name)
        return result
    dll = work / STESTS / f"bin/{args.configuration}/net10.0/{STESTS.name}.dll"
    def execute(name, class_name=CLASS, target=STESTS, method=None):
        deadline = time.monotonic() + args.timeout
        lane_project = work / target / f"{target.name}.csproj"
        build = run(["dotnet", "build", str(lane_project), "--configuration", args.configuration, "-p:UseHexalithProjectReferences=" + ("true" if args.dependency_mode == "source" else "false"), "-m:1", "-warnaserror", "--nologo"], name + "-build")
        if build.returncode: raise RuntimeError("Lane did not compile: " + name)
        remaining = math.floor(deadline - time.monotonic())
        if remaining < 1: raise RuntimeError("Lane exhausted deadline before execution: " + name)
        assembly = work / target / f"bin/{args.configuration}/net10.0/{target.name}.dll"
        argv = ["dotnet", str(assembly)] + (["-method", class_name + "." + method] if method else ["-class", class_name] if class_name else [])
        return run(argv, name + "-tests", remaining)
    def summary(result):
        match = re.search(r"Total:\s*(\d+), Errors:\s*(\d+), Failed:\s*(\d+), Skipped:\s*(\d+), Not Run:\s*(\d+)", result.stdout)
        if not match: raise RuntimeError("Missing exact test summary")
        return dict(zip(("total", "errors", "failed", "skipped", "notRun"), map(int, match.groups())))
    vector = run(["python3", str(ROOT / "scripts/verify-dapr-logical-checkpoint-vectors.py"), "--verify", str(work / CHECKPOINT_VECTOR)], "checkpoint-independent-vectors")
    if vector.returncode: raise RuntimeError("Independent checkpoint vectors failed")
    controls = {}
    for label, class_name, target in (("checkpoint", CLASS, STESTS), ("client-model", None, BASE.CTESTS),
        ("reconstruction", "Hexalith.EventStore.Server.Tests.Events.DaprLogicalReconstructionTests", STESTS),
        ("source-operation", "Hexalith.EventStore.Server.Tests.Events.DaprLogicalReplayTests", STESTS)):
        result = execute(label, class_name, target)
        counts = summary(result)
        if result.returncode or counts["total"] < 1 or any(counts[k] for k in ("errors", "failed", "skipped", "notRun")):
            raise RuntimeError("Control failed: " + label)
        controls[label] = counts
        print(label + ": " + str(counts), flush=True)
    mutations = [('exact-completed-fold-object', 'src/Hexalith.EventStore.Server/Events/DaprReplayOperationOwner.Checkpoint.cs', ' || !ReferenceEquals(reconstruction, _reconstruction)', '', 'DeclaredProjectionFoldMustBeTheActualCompletedBindingObject'), ('separate-proof-owner-manager', 'src/Hexalith.EventStore.Server/Events/DaprReplayOperationOwner.Checkpoint.cs', 'ReferenceEquals(projectionManager, _stateManager) || ', '', 'SourceOrCompletedManagerCannotOwnCheckpointStaging'), ('separate-source-owner-manager', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', ' || source.OwnsStateManager(StateManager)', '', 'SourceOrCompletedManagerCannotOwnCheckpointStaging'), ('pending-exact-declaration', 'src/Hexalith.EventStore.Server/Events/DaprLogicalCheckpointWrite.cs', '!ReferenceEquals(fold, Declaration) || ', '', 'PendingAdmissionRefusesDifferentDeclarationBeforeActualReadback'), ('pending-exact-fixed-source', 'src/Hexalith.EventStore.Server/Events/DaprLogicalCheckpointWrite.cs', ' || !source.SequenceEqual(_sourcePin)', '', 'PendingFixedSourceRefusesBeforeReadback'), ('retained-materialization-workspace', 'src/Hexalith.EventStore.Server/Events/DaprLogicalCheckpointWrite.cs', '_workspace = Budget.Reserve(checked(4 * maximumStateBytes + 8 * 64 * 1024 + 8192));', '_workspace = Budget.Reserve(0);', 'WorkspaceReservationSurvivesActuallyYieldingStageAndClearsOnRefusal'), ('private-participant-pin', 'src/Hexalith.EventStore.Server/Events/DaprLogicalCheckpointWrite.cs', '!SHA256.HashData(_desired[index].Bytes.Span).AsSpan().SequenceEqual(_pins[index])', '_pins[index].Length != 32', 'CallbackSubstitutionStopsLaterStagesAndRestoresBudget'), ('private-participant-key-pin', 'src/Hexalith.EventStore.Server/Events/DaprLogicalCheckpointWrite.cs', '!current.AsSpan().SequenceEqual(_keysPin)', 'current.Length != 32', 'CallbackSubstitutionStopsLaterStagesAndRestoresBudget'), ('all-three-participant-stages', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', 'for (int index = 0; index < 3; index++)\n                        {\n                            try', 'for (int index = 0; index < 2; index++)\n                        {\n                            try', 'ActualPureFoldIssuesThreeRowsAndReadbackRetryDoesNotSave'), ('actual-completed-origin-fences', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', 'await origin.RequireCurrentAsync(boundary).ConfigureAwait(false);', 'await Task.CompletedTask.ConfigureAwait(false);', 'CallbackSubstitutionStopsLaterStagesAndRestoresBudget'), ('fresh-canonical-admission', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', 'await DaprLogicalSnapshotCanonical.RequireAsync(origin.State, write.Array(1).Length + write.Array(2).Length, fold.Reconstruction, fold.Budget, boundary => FinalFenceAsync(origin, boundary), token).ConfigureAwait(false);', 'await Task.CompletedTask.ConfigureAwait(false);', 'PendingProvenRequiresFreshCurrentCanonicalCodecAndDoesNotRepeatSave'), ('final-origin-after-readback', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', 'await FinalFenceAsync(origin, token).ConfigureAwait(false);\n                    PrepareDesired(write, origin);', 'await Task.CompletedTask.ConfigureAwait(false);\n                    PrepareDesired(write, origin);', 'FinalReadbackCallbackHistoryLossWithholdsProven'), ('independent-durable-classification', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', 'DaprReplayCommitOutcome result = write.Classify(rows);', 'DaprReplayCommitOutcome result = DaprReplayCommitOutcome.Proven;', 'IndependentActualReadbackClassifiesSaveAndNeverRepeatsPendingSave'), ('uncertain-no-repeat-save', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', 'if (outcome != DaprReplayCommitOutcome.Proven)\n                    {\n                        return;', 'if (outcome != DaprReplayCommitOutcome.Proven)\n                    {\n                        await StateManager.SaveStateAsync(token).ConfigureAwait(false);\n                        return;', 'UncertainPendingAttemptCannotEnterAnotherSaveBoundary'), ('serialized-original-decision', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', ' || boundary != token', '', 'SerializedOwnerRequiresExactlyOneOriginalDecision'), ('serialized-once-decision', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', 'Interlocked.Increment(ref calls) != 1', 'Interlocked.Increment(ref calls) > 2', 'SerializedOwnerRequiresExactlyOneOriginalDecision'), ('serialized-completed-decision', 'src/Hexalith.EventStore.Server/Actors/DaprLogicalCheckpointActor.cs', ' || !running.IsCompleted', '', 'ActuallyYieldingEarlyOwnerRefusesSaveAndEventuallyClearsStaging'), ('owned-staging-clearing', 'src/Hexalith.EventStore.Server/Events/DaprLogicalCheckpointWrite.cs', 'foreach (DaprLogicalResponseOwner image in _desired)\n        {\n            image.Dispose();\n        }', 'foreach (DaprLogicalResponseOwner image in _desired)\n        {\n            _ = image;\n        }', 'ActualPureFoldIssuesThreeRowsAndReadbackRetryDoesNotSave'), ('namespace-distinct-framing', 'src/Hexalith.EventStore.Server/Events/DaprLogicalCheckpointCodec.cs', 'HX-EV-DAPR-CHECKPOINT-NAMESPACE-1', 'HX-EV-DAPR-CHECKPOINT-NAMESPACE-2', 'IndependentCheckpointOwnerVectorsMatchRuntimeFramingAndMeasuredBytes'), ('root-exact-tag-order', 'src/Hexalith.EventStore.Server/Events/DaprLogicalCheckpointCodec.cs', 'writer.WriteByte(9);', 'writer.WriteByte(8);', 'IndependentCheckpointOwnerVectorsMatchRuntimeFramingAndMeasuredBytes')]
    kills = []
    if not args.control_only:
        for name, relative, old, new, killer in mutations:
            if args.mutation and name not in args.mutation: continue
            path = work / relative
            original = path.read_text()
            if original.count(old) != 1: raise RuntimeError("Mutation anchor missing/ambiguous: " + name)
            path.write_text(original.replace(old, new))
            try:
                result = execute("mutation-" + name, CLASS, method=killer)
                counts = summary(result)
                if result.returncode == 0 or counts["failed"] < 1 or counts["errors"] or counts["skipped"] or counts["notRun"]:
                    raise RuntimeError("Compiling mutation survived/missed behavioral killer: " + name)
                kills.append({"name": name, "killer": killer, "counts": counts})
                print(name + ": killed", flush=True)
            finally:
                path.write_text(original)
    root_after = root_hashes()
    result = {"configuration": args.configuration, "dependencyMode": args.dependency_mode, "controls": controls, "kills": kills,
        "commandCount": len(commands), "commands": commands, "rootInputsBefore": root_before, "rootInputsAfter": root_after,
        "rootInputSetUnchanged": root_before == root_after, "excludedExternalPaths": excluded, "isolationSubstitutions": [],
        "qualifiedScope": "Unchanged dynamically sealed disposable runtime/test/helper/DLL inputs only; not full workspace or production authority"}
    (output / "result.json").write_text(json.dumps(result, indent=2) + "\n")
    print("completed; root input set unchanged=" + str(root_before == root_after), flush=True)

if __name__ == "__main__":
    main()
