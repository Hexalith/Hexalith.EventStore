#!/usr/bin/env python3
"""Seal sequential disposable current-prefix snapshot re-witness controls; this does not replace the root solution gate."""
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
SPEC = importlib.util.spec_from_file_location("rewitness_base", ROOT / "scripts/verify-dapr-logical-model-guards.py")
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
STESTS = BASE.STESTS
SNAPSHOT_VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-snapshot-2026-10-09/vectors.json")
CLASS = "Hexalith.EventStore.Server.Tests.Events.DaprLogicalSnapshotRewitnessTests"
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
        paths += [ROOT / name for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "tests/Directory.Build.props", ".editorconfig", ".gitattributes", "nuget.config", str(VECTOR), str(COMMAND_VECTOR), str(SNAPSHOT_VECTOR), "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-anchored-replay-2026-10-09/vectors.json", "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/vectors.json", "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-anchored-continuation-model.md", "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-snapshot-replacement-model.md", "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-snapshot-rewitness-model.md")]
        return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(paths))}
    root_before = root_hashes()
    (output / "attempt.json").write_text(json.dumps({"rootInputsBefore": root_before, "excludedExternalPaths": excluded, "retainedExternalDependencyNames": sorted(retained), "isolationSubstitutions": [], "fullWorkspaceQualified": False}, indent=2) + "\n")
    BASE.prepare(work)
    for name in (".editorconfig", ".gitattributes", "nuget.config"):
        shutil.copyfile(ROOT / name, work / name)
    for name in excluded:
        (work / name).unlink()
    fixture_files = ("DaprLogicalSnapshotRewitnessFixture.cs", "DaprLogicalSnapshotRewitnessTests.cs", "DaprLogicalSnapshotReplacementFixture.cs", "DaprLogicalSnapshotReplacementTests.cs", "DaprLogicalSnapshotFixture.cs", "DaprLogicalSnapshotTests.cs", "DaprLogicalReconstructionFixture.cs", "DaprLogicalReconstructionTestState.cs", "DaprLogicalReconstructionTests.cs", "DaprLogicalAnchorFixture.cs", "DaprLogicalAnchoredReplayTests.cs", "DaprLogicalAnchoredContinuationFixture.cs", "DaprLogicalAnchoredContinuationTests.cs", "DaprLogicalAnchoredContinuationCodecTests.cs", "DaprLogicalCommandStateTests.cs", "DaprLogicalCommandStateProcessor.cs", "DaprLogicalCommandStateAdmissionStage.cs", "DaprLogicalCommandStateResult.cs", "DaprLogicalCommandStateTestEvent.cs", "DaprLogicalCommandStateSerializedEvent.cs", "DaprLogicalCommandStateEventList.cs")
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
    controls = {"rewitnessMethods": {}, "rewitnessTotal": 0, "replacementMethods": {}, "replacementTotal": 0}
    for class_name in (CLASS, REPLACEMENT_CLASS):
        prefix = "rewitness" if class_name == CLASS else "replacement"
        methods = re.findall(r"public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\(", (ROOT / STESTS / "Events" / (class_name.split(".")[-1] + ".cs")).read_text())
        for method in methods:
            control = execute(prefix + "-control-" + method, class_name=class_name, method=method)
            counts = summary(control)
            if control.returncode or counts["total"] < 1 or any(counts[key] for key in ("errors", "failed", "skipped", "notRun")):
                raise RuntimeError("Scoped control failed: " + method)
            controls[prefix + "Methods"][method] = counts
            controls[prefix + "Total"] += counts["total"]
    print("re-witness controls: passed " + str(controls["rewitnessTotal"]) + "; established replacement: " + str(controls["replacementTotal"]), flush=True)
    old = execute("client-model", None, BASE.CTESTS)
    controls["clientModel"] = summary(old)
    if old.returncode or any(controls["clientModel"][key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Existing client model control failed")
    print("client model control: passed", flush=True)
    snapshots = execute("snapshot", "Hexalith.EventStore.Server.Tests.Events.DaprLogicalSnapshotTests")
    controls["snapshot"] = summary(snapshots)
    if snapshots.returncode or any(controls["snapshot"][key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Existing snapshot control failed")
    print("snapshot control: passed", flush=True)
    existing = execute("reconstruction", "Hexalith.EventStore.Server.Tests.Events.DaprLogicalReconstructionTests")
    controls["reconstruction"] = summary(existing)
    if existing.returncode or any(controls["reconstruction"][key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Existing reconstruction control failed")
    print("reconstruction control: passed", flush=True)
    source = execute("ordinary-source-operation", "Hexalith.EventStore.Server.Tests.Events.DaprLogicalReplayTests")
    controls["ordinarySourceOperation"] = summary(source)
    if source.returncode or any(controls["ordinarySourceOperation"][key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Existing ordinary source-operation control failed")
    preparation = execute("anchored-preparation", "Hexalith.EventStore.Server.Tests.Events.DaprLogicalAnchoredReplayTests")
    controls["anchoredPreparation"] = summary(preparation)
    if preparation.returncode or any(controls["anchoredPreparation"][key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Existing anchored preparation control failed")
    command = execute("ordinary-command", COMMAND_CLASS)
    controls["ordinaryCommand"] = summary(command)
    if command.returncode or controls["ordinaryCommand"]["total"] < 1 or any(controls["ordinaryCommand"][key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Existing ordinary command control failed")
    print("ordinary command control: passed", flush=True)
    controls["anchoredContinuation"] = {}
    for method in ("ActualAnchoredTailBeginPageReadbackAndRetry", "ActualHeadSnapshotZeroTailCommitsTerminalReadbackAndExactRetry"):
        result = execute("continuation-" + method, "Hexalith.EventStore.Server.Tests.Events.DaprLogicalAnchoredContinuationTests", method=method)
        counts = summary(result)
        if result.returncode or counts["total"] < 1 or any(counts[key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Existing continuation failed")
        controls["anchoredContinuation"][method] = counts
    mutations = [
        ('older-entry-policy-identity', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshot.cs", 'pending.PolicyId != model', 'false', 1, 'RewitnessPendingCannotCrossIntoOlderPolicy'),
        ('rewitness-entry-policy-identity', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'pending.PolicyId != model', 'false', 1, 'InitialPendingCannotCrossIntoRewitness'),
        ('exact-target-before-owner', BASE.SERVER / "Events/DaprLogicalSnapshotRewitnessPolicy.cs", 'prior.TargetSequence != current.TargetSequence ||', '', 1, 'UnsupportedBindingRefusesBeforeOwner'),
        ('earlier-head-before-owner', BASE.SERVER / "Events/DaprLogicalSnapshotRewitnessPolicy.cs", 'prior.ActorHead >= current.ActorHead ||', '', 1, 'UnsupportedBindingRefusesBeforeOwner'),
        ('complete-prefix-floor-before-owner', BASE.SERVER / "Events/DaprLogicalSnapshotRewitnessPolicy.cs", 'prior.RetainedFloor != 1 || current.RetainedFloor != 1 ||', '', 1, 'UnsupportedBindingRefusesBeforeOwner'),
        ('same-namespace-before-owner', BASE.SERVER / "Events/DaprLogicalSnapshotRewitnessPolicy.cs", 'prior.Namespace != current.Namespace ||', '', 1, 'UnsupportedBindingRefusesBeforeOwner'),
        ('exact-observed-source-hint', BASE.SERVER / "Events/DaprLogicalSnapshotRewitnessPolicy.cs", '!fields.SourceBindingHash.Span.SequenceEqual(DaprLogicalClaimCodec.ComputeSourceBindingHash(prior, write.Budget)) ||', '', 1, 'IncompatibleHintNeverStagesOrSaves'),
        ('exact-hint-registry', BASE.SERVER / "Events/DaprLogicalSnapshotRewitnessPolicy.cs", '!fields.RegistryFingerprint.Span.SequenceEqual(trust.RegistryFingerprint.Span) ||', '', 1, 'IncompatibleHintNeverStagesOrSaves'),
        ('exact-hint-reconstruction', BASE.SERVER / "Events/DaprLogicalSnapshotRewitnessPolicy.cs", '!fields.ReconstructionBindingHash.Span.SequenceEqual(reconstruction.Fingerprint.Span) ||', '', 1, 'IncompatibleHintNeverStagesOrSaves'),
        ('exact-current-prefix-state', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", '!exactDesired && !write.PriorState.Bytes.Span.SequenceEqual(origin.State.Span)', 'false', 1, 'CurrentPrefixStateMustEqualObservedHint'),
        ('fresh-current-canonical-admission', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'await DaprLogicalSnapshotCanonical.RequireAsync(origin.State, write.WitnessArray.Length, reconstruction, budget, CurrentFenceAsync, token).ConfigureAwait(false);', 'await CurrentFenceAsync(token).ConfigureAwait(false);', 1, 'ExactDesiredRetryIsCanonicalAndIdempotent'),
        ('pending-current-canonical-admission', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'await DaprLogicalSnapshotCanonical.RequireAsync(origin.State, pending.WitnessArray.Length, reconstruction, budget, CurrentFenceAsync, token).ConfigureAwait(false);', 'await CurrentFenceAsync(token).ConfigureAwait(false);', 1, 'PendingProvenRecoveryRequiresFreshCurrentCanonicalProof'),
        ('actual-current-origin-fences', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'await origin.RequireCurrentAsync(token).ConfigureAwait(false);', 'await Task.CompletedTask.ConfigureAwait(false);', 1, 'CodecCallbackCannotSubstitutePinsOrCurrentHistory'),
        ('actual-prestage-prior-pair', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", '!write.MatchesPrior(freshState, freshWitness) || exactDesired && write.Classify(freshState, freshWitness) != DaprReplayCommitOutcome.Proven', 'false', 1, 'CodecCallbackCannotSubstitutePinsOrCurrentHistory'),
        ('private-complete-prior-witness-pin', BASE.SERVER / "Events/DaprLogicalSnapshotWrite.cs", 'if ((_priorState is not null && !SHA256.HashData(_priorState.Bytes.Span).AsSpan().SequenceEqual(_priorStatePin)) || (_priorWitness is not null && !SHA256.HashData(_priorWitness.Bytes.Span).AsSpan().SequenceEqual(_priorWitnessPin)))', 'if (_priorState is not null && !SHA256.HashData(_priorState.Bytes.Span).AsSpan().SequenceEqual(_priorStatePin))', 1, 'CodecCallbackCannotSubstitutePinsOrCurrentHistory'),
        ('current-origin-after-last-readback', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'if (outcome == DaprReplayCommitOutcome.Proven) { RequireOrigin(origin, write); await FenceAsync(origin, token).ConfigureAwait(false); }', 'if (outcome == DaprReplayCommitOutcome.Proven) { RequireOrigin(origin, write); await Task.CompletedTask.ConfigureAwait(false); }', 1, 'LastReadbackCannotSubstituteCurrentCompletedHistory'),
        ('exact-desired-after-last-readback', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'write.ReleaseReads(); await FenceAsync(origin, token).ConfigureAwait(false); RequireOrigin(origin, write);', 'write.ReleaseReads(); await Task.CompletedTask.ConfigureAwait(false); RequireOrigin(origin, write);', 1, 'ExactDesiredLastReadbackCannotSubstituteCurrentHistory'),
        ('current-origin-after-state-stage', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'await StateManager.SetStateAsync(write.StorageKey, write.StateArray, token).ConfigureAwait(false); await FenceAsync(origin, token).ConfigureAwait(false); RequireOrigin(origin, write);', 'await StateManager.SetStateAsync(write.StorageKey, write.StateArray, token).ConfigureAwait(false); await Task.CompletedTask.ConfigureAwait(false); RequireOrigin(origin, write);', 1, 'YieldingStageKeepsCapacityAndExpiresFacadesBeforeRefusal'),
        ('retained-materialization-capacity', BASE.SERVER / "Events/DaprLogicalSnapshotWrite.cs", 'budget.Reserve(checked(4 * maximumStateBytes + 6 * 64 * 1024 + 4096))', 'budget.Reserve(0)', 1, 'CapacityRefusesBeforeOriginAndStage'),
        ('retained-origin-state-clearing', BASE.SERVER / "Events/DaprLogicalReplayAnchorOrigin.cs", 'Interlocked.Exchange(ref _state, null)?.Dispose();', 'GC.KeepAlive(_state); _state = null;', 1, 'ActualCurrentPrefixRewitnessesWithoutHistoricalAuthority'),
        ('paired-witness-stage', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'await StateManager.SetStateAsync(write.WitnessKey, write.WitnessArray, token).ConfigureAwait(false);', 'await Task.CompletedTask.ConfigureAwait(false);', 1, 'IndependentReadbackClassifiesAndPendingNeverRepeatsSave'),
        ('independent-paired-readback', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", [('outcome = await InspectLogicalSnapshotAsync(pending).ConfigureAwait(false);', 'outcome = DaprReplayCommitOutcome.Proven;', 2), ('outcome = await InspectLogicalSnapshotAsync(write).ConfigureAwait(false);', 'outcome = DaprReplayCommitOutcome.Proven;', 1)], None, 0, 'IndependentReadbackClassifiesAndPendingNeverRepeatsSave'),
        ('pending-no-repeat-save', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'return; } await RequireLogicalSnapshotSourceAsync(current, source, trust, reconstruction, token).ConfigureAwait(false);', 'GC.KeepAlive(pending); } await RequireLogicalSnapshotSourceAsync(current, source, trust, reconstruction, token).ConfigureAwait(false);', 1, 'IndependentReadbackClassifiesAndPendingNeverRepeatsSave'),
        ('complete-original-owner-decision', BASE.SERVER / "Actors/AggregateActor.LogicalSnapshotRewitness.cs", 'calls != 1 || running is null || !running.IsCompleted', 'calls != 1 || running is null', 1, 'CommonOwnerMustCompleteExactlyOneOriginalDecision'),
    ]
    if args.mutation:
        unknown = set(args.mutation) - {row[0] for row in mutations}
        if unknown: raise RuntimeError("Unknown mutation selection: " + ", ".join(sorted(unknown)))
        mutations = [row for row in mutations if row[0] in args.mutation]
    rows = []
    if not args.control_only:
        for name, relative, before, after, count, killer in mutations:
            path = work / relative; original = path.read_bytes()
            try:
                source = original.decode().replace("\r\n", "\n")
                edits = before if isinstance(before, list) else [(before, after, count)]
                for current_before, current_after, expected_count in edits:
                    tokens = re.findall(r'"(?:\\.|[^"\\])*"|\w+|[^\w\s]', current_before)
                    pattern = re.compile(r"\s*".join(re.escape(token) for token in tokens))
                    if len(list(pattern.finditer(source))) != expected_count: raise RuntimeError("Missing/ambiguous mutation anchor: " + name)
                    source = pattern.sub(lambda _: current_after, source)
                path.write_text(source)
                result = execute(name, class_name=CLASS, method=killer)
                lines = result.stdout.splitlines(); failures = [i for i, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if not result.returncode or not failures: raise RuntimeError("Mutation survived/missed named behavioral killer: " + name)
                counts = summary(result)
                if counts["errors"] or counts["skipped"] or counts["notRun"]: raise RuntimeError("Mutation did not fully execute")
                rows.append({"mutation": name, "result": "killed", "killingTest": killer, "summary": counts, "failureExcerpt": lines[failures[0]:failures[0]+14]})
                (output / "mutations.json").write_text(json.dumps(rows, indent=2) + "\n")
                print(name + ": killed", flush=True)
            finally:
                path.write_bytes(original)
    root_after = root_hashes()
    (output / "result.json").write_text(json.dumps({"scope": "dormant current-prefix snapshot re-witnessing through actual aggregate and completed owners; earlier-head hint remains unauthenticated; all external runtime inputs preserved", "configuration": args.configuration, "dependencyMode": args.dependency_mode,
        "controls": controls, "mutations": rows, "timeoutSecondsPerLane": args.timeout, "controlOnly": args.control_only, "excludedExternalPaths": excluded,
        "rootInputsBefore": root_before, "rootInputsAfter": root_after, "rootInputSetUnchanged": root_before == root_after, "commands": commands,
        "copiedFixtureFiles": list(fixture_files), "fullWorkspaceQualified": False, "productionQualification": False, "activationAuthority": False}, indent=2) + "\n")
    print(json.dumps({"control": "passed", "tests": controls["snapshot"]["total"], "mutantsKilled": len(rows), "rootInputSetUnchanged": root_before == root_after}))

if __name__ == "__main__": main()
