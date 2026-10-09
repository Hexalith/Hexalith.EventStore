#!/usr/bin/env python3
"""Seal sequential disposable anchored preparation controls; this does not replace the root solution gate."""
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
SPEC = importlib.util.spec_from_file_location("anchor_base", ROOT / "scripts/verify-dapr-logical-model-guards.py")
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
STESTS = BASE.STESTS
SNAPSHOT_VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-snapshot-2026-10-09/vectors.json")
CLASS = "Hexalith.EventStore.Server.Tests.Events.DaprLogicalAnchoredReplayTests"
VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-anchored-replay-2026-10-09/vectors.json")

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
        paths += [ROOT / name for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "tests/Directory.Build.props", ".editorconfig", ".gitattributes", "nuget.config", str(VECTOR), str(SNAPSHOT_VECTOR), "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-anchored-replay-model.md")]
        return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(paths))}
    root_before = root_hashes()
    (output / "attempt.json").write_text(json.dumps({"rootInputsBefore": root_before, "excludedExternalPaths": excluded, "retainedExternalDependencyNames": sorted(retained), "isolationSubstitutions": [], "fullWorkspaceQualified": False}, indent=2) + "\n")
    BASE.prepare(work)
    for name in (".editorconfig", ".gitattributes", "nuget.config"):
        shutil.copyfile(ROOT / name, work / name)
    for name in excluded:
        (work / name).unlink()
    fixture_files = ("DaprLogicalSnapshotFixture.cs", "DaprLogicalSnapshotTests.cs", "DaprLogicalReconstructionFixture.cs", "DaprLogicalReconstructionTestState.cs", "DaprLogicalReconstructionTests.cs", "DaprLogicalAnchorFixture.cs", "DaprLogicalAnchoredReplayTests.cs")
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
        argv = ["dotnet", str(assembly)] + (["-method", CLASS + "." + method] if method else ["-class", class_name] if class_name else [])
        return run(argv, name + "-tests", remaining)
    def summary(result):
        match = re.search(r"Total:\s*(\d+), Errors:\s*(\d+), Failed:\s*(\d+), Skipped:\s*(\d+), Not Run:\s*(\d+)", result.stdout)
        if not match: raise RuntimeError("Missing exact test summary")
        return dict(zip(("total", "errors", "failed", "skipped", "notRun"), map(int, match.groups())))
    methods = re.findall(r"public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\(", (ROOT / STESTS / "Events/DaprLogicalAnchoredReplayTests.cs").read_text())
    controls = {"anchorMethods": {}, "anchorTotal": 0}
    for method in methods:
        control = execute("control-" + method, method=method)
        counts = summary(control)
        if control.returncode or counts["total"] < 1 or any(counts[key] for key in ("errors", "failed", "skipped", "notRun")):
            raise RuntimeError("Anchor control failed: " + method)
        controls["anchorMethods"][method] = counts
        controls["anchorTotal"] += counts["total"]
    print("anchor controls: passed " + str(controls["anchorTotal"]), flush=True)
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
    mutations = [
        ("distinct-anchor-model", BASE.SERVER / "Events/DaprLogicalReplayInitialAnchor.cs", "model != DaprLogicalReplayAnchorCodec.ModelId", "model.Length < 0", 1, "PrivateInitialSelectionRequiresExactModelParentAndSource"),
        ("exact-composed-parent", BASE.SERVER / "Events/DaprLogicalReplayInitialAnchor.cs", "!ReferenceEquals(candidate.Budget, budget)", "budget.LiveBytes < 0", 1, "PrivateInitialSelectionRequiresExactModelParentAndSource"),
        ("exact-requested-source", BASE.SERVER / "Events/DaprLogicalReplayInitialAnchor.cs", "candidate.RequireSourceBinding(source, token);", "GC.KeepAlive(source);", 1, "PrivateInitialSelectionRequiresExactModelParentAndSource"),
        ("canonical-selection-order", BASE.CLIENT / "Events/DaprLogicalReplayAnchorCodec.cs", "reader.ReadByte() != tag", "reader.ReadByte() == 255", 1, "CanonicalSelectionRefusesUnsupportedShapes"),
        ("covered-seed-preimage", BASE.CLIENT / "Events/DaprLogicalReplayAnchorCodec.cs", "writer.WriteHash(covered.Span);", "writer.WriteHash(selectionHash.Span);", 1, "IndependentAnchorVectorsMatchStrictMeasuredImages"),
        ("retained-anchor-decoding-charge", BASE.SERVER / "Events/DaprLogicalReplayInitialAnchor.cs", "budget.Reserve(checked(4 * DaprLogicalReplayAnchorCodec.MaximumBytes + 4096))", "budget.Reserve(0)", 1, "PrivateInitialAnchorOwnsCanonicalStateAndDistinctSeedsWithoutAdvancingReplay"),
        ("post-await-initial-private-pin", BASE.SERVER / "Events/DaprLogicalReplayInitialAnchor.cs", "RequirePins(); } private void RequirePins()", "GC.KeepAlive(_candidate); } private void RequirePins()", 1, "PrivateInitialYieldingFenceSubstitutionWithholdsOwnership"),
        ("initial-refusal-clearing", BASE.SERVER / "Events/DaprLogicalReplayInitialAnchor.cs", "captured?.Dispose();", "GC.KeepAlive(captured);", 2, "PrivateInitialReadWriteCancellationClearsOwnedCandidate"),
        ("prior-pair-hold", BASE.SERVER / "Actors/AggregateActor.LogicalSnapshot.cs", "state is not null || priorWitness is not null", "state is not null && state.Bytes.Length < 0", 1, "PriorNonidenticalPairHoldsWithoutOverwrite"),
        ("exact-issuer-origin-scope", BASE.SERVER / "Actors/AggregateActor.LogicalSnapshot.cs", "RequireOriginScope(origin);", "GC.KeepAlive(origin);", 1, "ActualIssuerRequiresExactCompletedOriginScope"),
        ("paired-witness-stage", BASE.SERVER / "Actors/AggregateActor.LogicalSnapshot.cs", "await StateManager.SetStateAsync(write.WitnessKey, write.WitnessArray, token).ConfigureAwait(false);", "await Task.CompletedTask.ConfigureAwait(false);", 1, "ActualActorPairSaveUsesFreshExactClassification"),
        ("actual-pair-readback", BASE.SERVER / "Actors/AggregateActor.LogicalSnapshot.cs", "outcome = await InspectLogicalSnapshotAsync(write).ConfigureAwait(false);", "outcome = DaprReplayCommitOutcome.Proven;", 3, "ActualActorPairSaveUsesFreshExactClassification"),
        ("staging-private-pin", BASE.SERVER / "Events/DaprLogicalSnapshotWrite.cs", "HasDesired && (!SHA256.HashData(_state!.Bytes.Span).AsSpan().SequenceEqual(_statePin) || !SHA256.HashData(_witness!.Bytes.Span).AsSpan().SequenceEqual(_witnessPin))", "HasDesired && _state!.Bytes.Length < 0", 1, "ActualPairStagingAliasSubstitutionStopsBeforeLaterStage"),
        ("retained-materialization-charge", BASE.SERVER / "Events/DaprLogicalSnapshotWrite.cs", "budget.Reserve(checked(4 * maximumStateBytes + 6 * 64 * 1024 + 4096))", "budget.Reserve(0)", 1, "ActualPairYieldingReadbackRetainsUntilExactRecovery"),
        ("sdk-private-capacity-clearing", BASE.SERVER / "Events/DaprLogicalSnapshotWrite.cs", "Interlocked.Exchange(ref _state, null)?.Dispose();", "GC.KeepAlive(_state); _state = null;", 1, "ActualActorPairSaveUsesFreshExactClassification"),
        ("pending-never-repeat-save", BASE.SERVER / "Actors/AggregateActor.LogicalSnapshot.cs", "return; } RequireDecisionPins(); var write", "GC.KeepAlive(pending); } RequireDecisionPins(); var write", 1, "ActualPendingNoCommitRecoveryNeverRepeatsSave"),
        ("ordinary-cache-barrier-after-failed-release", BASE.SERVER / "Actors/AggregateActor.LogicalSnapshot.cs", "_stateCacheUnsafe = true;", "GC.KeepAlive(write);", 2, "ActualFailedCacheReleaseHoldsLegacyActorBoundaryUntilRecovery"),
        ("complete-owner-decision", BASE.SERVER / "Actors/AggregateActor.LogicalSnapshot.cs", "calls != 1 || running is null || !running.IsCompleted", "calls != 1 || running is null", 1, "ActualSerializedOwnerMustCompleteOneOriginalDecision"),
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
                tokens = re.findall(r'"(?:\\.|[^"\\])*"|\w+|[^\w\s]', before)
                pattern = re.compile(r"\s*".join(re.escape(token) for token in tokens))
                source = original.decode().replace("\r\n", "\n")
                if len(list(pattern.finditer(source))) != count: raise RuntimeError("Missing/ambiguous mutation anchor: " + name)
                path.write_text(pattern.sub(lambda _: after, source))
                result = execute(name, method=killer)
                lines = result.stdout.splitlines(); failures = [i for i, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if not result.returncode or not failures: raise RuntimeError("Mutation survived/missed named behavioral killer: " + name)
                counts = summary(result)
                if counts["errors"] or counts["skipped"] or counts["notRun"]: raise RuntimeError("Mutation did not fully execute")
                rows.append({"mutation": name, "result": "killed", "killingTest": killer, "summary": counts, "failureExcerpt": lines[failures[0]:failures[0]+14]})
                (output / "mutations.json").write_text(json.dumps(rows, indent=2) + "\n")
                print(name + ": killed", flush=True)
            finally: path.write_bytes(original)
    root_after = root_hashes()
    (output / "result.json").write_text(json.dumps({"scope": "dormant distinct anchor preparation and actual actor pair issuer; all external runtime inputs preserved", "configuration": args.configuration, "dependencyMode": args.dependency_mode,
        "controls": controls, "mutations": rows, "timeoutSecondsPerLane": args.timeout, "controlOnly": args.control_only, "excludedExternalPaths": excluded,
        "rootInputsBefore": root_before, "rootInputsAfter": root_after, "rootInputSetUnchanged": root_before == root_after, "commands": commands,
        "copiedFixtureFiles": list(fixture_files), "fullWorkspaceQualified": False, "productionQualification": False, "activationAuthority": False}, indent=2) + "\n")
    print(json.dumps({"control": "passed", "tests": controls["snapshot"]["total"], "mutantsKilled": len(rows), "rootInputSetUnchanged": root_before == root_after}))

if __name__ == "__main__": main()
