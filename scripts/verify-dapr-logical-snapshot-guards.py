#!/usr/bin/env python3
"""Seal sequential disposable snapshot controls; this does not replace the root solution gate."""
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
SPEC = importlib.util.spec_from_file_location("snapshot_base", ROOT / "scripts/verify-dapr-logical-model-guards.py")
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
STESTS = BASE.STESTS
CLASS = "Hexalith.EventStore.Server.Tests.Events.DaprLogicalSnapshotTests"
VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-snapshot-2026-10-09/vectors.json")

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
    retained = {"ExpiredIdentityHistoryCertificate.cs", "IDeletionCapabilityCompromiseRegistrar.cs", "IExpiredIdentityHistoryCustody.cs", "DeletionBatchCapabilityIdentity.cs", "IAnchoredStateTransitionAuthority.cs", "AnchoredStateTransition.cs", "RecoverableAnchoredState.cs", "BoundedPendingStateStream.cs", "DeletionActivationComparison.cs"}
    excluded = [name for name in subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard", "src/Hexalith.EventStore.Server/Security", "src/Hexalith.EventStore.Contracts/Security"], cwd=ROOT, text=True).splitlines()
                if name.endswith(".cs") and pathlib.Path(name).name not in retained]
    def root_hashes():
        roots = [ROOT / directory for directory in (BASE.CLIENT, BASE.DOMAIN, BASE.SERVER, BASE.CONTRACTS, BASE.DEFAULTS, BASE.CTESTS, BASE.STESTS, BASE.UNIQUE_IDS)]
        roots += [ROOT / "scripts", ROOT / ".github/workflows", ROOT / "references/Hexalith.Builds"]
        paths = [p for directory in roots for p in directory.rglob("*") if p.is_file() and not any(x in p.parts for x in ("bin", "obj", ".git", "__pycache__"))]
        paths += [ROOT / name for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "tests/Directory.Build.props", ".editorconfig", ".gitattributes", "nuget.config", str(VECTOR), "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-snapshot-model.md")]
        return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(paths))}
    root_before = root_hashes()
    (output / "attempt.json").write_text(json.dumps({"rootInputsBefore": root_before, "excludedExternalPaths": excluded, "retainedExternalDependencyNames": sorted(retained), "isolationSubstitutions": [], "fullWorkspaceQualified": False}, indent=2) + "\n")
    BASE.prepare(work)
    for name in (".editorconfig", ".gitattributes", "nuget.config"):
        shutil.copyfile(ROOT / name, work / name)
    for name in excluded:
        (work / name).unlink()
    fixture_files = ("DaprLogicalSnapshotFixture.cs", "DaprLogicalSnapshotTests.cs", "DaprLogicalReconstructionFixture.cs", "DaprLogicalReconstructionTestState.cs", "DaprLogicalReconstructionTests.cs")
    for filename in fixture_files:
        shutil.copyfile(ROOT / STESTS / "Events" / filename, work / STESTS / "Events" / filename)
    project = work / STESTS / f"{STESTS.name}.csproj"
    source = project.read_text().replace("</ItemGroup><ItemGroup><Using", '<ProjectReference Include="../../src/Hexalith.EventStore.DomainService/Hexalith.EventStore.DomainService.csproj" /></ItemGroup><ItemGroup><Using')
    project.write_text(source)
    (work / VECTOR).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / VECTOR, work / VECTOR)
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
        result = subprocess.run(argv, cwd=work, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout or args.timeout, check=False)
        (output / f"{name}.log").write_text(result.stdout)
        after = hashes(work)
        helpers_after = helper_hashes()
        dlls_after = {} if assembly_directory is None else {str(p.relative_to(work)): hashlib.sha256(p.read_bytes()).hexdigest() for p in assembly_directory.glob("*.dll")}
        commands.append({"argv": argv, "cwd": str(work), "exitCode": result.returncode, "log": f"{name}.log", "seconds": round(time.monotonic()-start, 3), "startedUtc": started_utc, "endedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "dynamicLaneInputsBefore": before, "dynamicLaneInputsAfter": after, "dynamicLaneSetUnchanged": before == after,
            "importedInputsBefore": helpers, "importedInputsAfter": helpers_after, "dynamicImportedSetUnchanged": helpers == helpers_after,
            "executedDllsBefore": dlls, "executedDllsAfter": dlls_after, "dynamicDllSetUnchanged": dlls == dlls_after})
        (output / "commands.json").write_text(json.dumps(commands, indent=2) + "\n")
        if before != after or helpers != helpers_after or dlls != dlls_after: raise RuntimeError("Disposable/imported/executed input set changed")
        return result
    dll = work / STESTS / f"bin/{args.configuration}/net10.0/{STESTS.name}.dll"
    def execute(name, class_name=CLASS, target=STESTS):
        deadline = time.monotonic() + args.timeout
        lane_project = work / target / f"{target.name}.csproj"
        build = run(["dotnet", "build", str(lane_project), "--configuration", args.configuration, "-p:UseHexalithProjectReferences=" + ("true" if args.dependency_mode == "source" else "false"), "-m:1", "-warnaserror", "--nologo"], name + "-build")
        if build.returncode: raise RuntimeError("Lane did not compile: " + name)
        remaining = math.floor(deadline - time.monotonic())
        if remaining < 1: raise RuntimeError("Lane exhausted deadline before execution: " + name)
        assembly = work / target / f"bin/{args.configuration}/net10.0/{target.name}.dll"
        argv = ["dotnet", str(assembly)] + (["-class", class_name] if class_name else [])
        return run(argv, name + "-tests", remaining)
    def summary(result):
        match = re.search(r"Total:\s*(\d+), Errors:\s*(\d+), Failed:\s*(\d+), Skipped:\s*(\d+), Not Run:\s*(\d+)", result.stdout)
        if not match: raise RuntimeError("Missing exact test summary")
        return dict(zip(("total", "errors", "failed", "skipped", "notRun"), map(int, match.groups())))
    control = execute("control")
    controls = {"snapshot": summary(control)}
    if control.returncode or any(controls["snapshot"][key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Snapshot control failed")
    print("snapshot control: passed", flush=True)
    old = execute("client-model", None, BASE.CTESTS)
    controls["clientModel"] = summary(old)
    if old.returncode or any(controls["clientModel"][key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Existing client model control failed")
    print("client model control: passed", flush=True)
    existing = execute("reconstruction", "Hexalith.EventStore.Server.Tests.Events.DaprLogicalReconstructionTests")
    controls["reconstruction"] = summary(existing)
    if existing.returncode or any(controls["reconstruction"][key] for key in ("errors", "failed", "skipped", "notRun")): raise RuntimeError("Existing reconstruction control failed")
    print("reconstruction control: passed", flush=True)
    mutations = [
        ("explicit-model-selection", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "model != DaprLogicalSnapshotCodec.SnapshotModel", "model.Length < 0", 1, "ModelAndActualManagerSelectionAreExplicit"),
        ("same-actual-owner-manager", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "!source.OwnsStateManager(stateManager)", "stateManager is null", 1, "ModelAndActualManagerSelectionAreExplicit"),
        ("timeline-restarts-one", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "includeTimeline || binding.TargetSequence == 0", "binding.TargetSequence == 0", 1, "BelowHeadZeroTailAboveTargetAndTimelineRestartAtOne"),
        ("below-head-zero-tail-refusal", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "covered == binding.TargetSequence && covered < binding.ActorHead", "covered < 0", 1, "BelowHeadZeroTailAboveTargetAndTimelineRestartAtOne"),
        ("source-binding-before-origin", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "!fields.SourceBindingHash.Span.SequenceEqual(DaprLogicalClaimCodec.ComputeSourceBindingHash(binding with { TargetSequence = covered }, budget))", "fields.SourceBindingHash.Length < 0", 1, "InvalidPairDiscardsEntireCandidateAndPreservesStoredBytes"),
        ("storage-hash-before-origin", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "!fields.StorageHash.Span.SequenceEqual(SHA256.HashData(stored.Bytes.Span))", "fields.StorageHash.Length < 0", 1, "InvalidPairDiscardsEntireCandidateAndPreservesStoredBytes"),
        ("fresh-completed-origin-chain", BASE.SERVER / "Events/DaprReplayOperationOwner.cs", "!await RequireCommittedChainAsync(actual!, binding, trust, budget, token).ConfigureAwait(false)", "actual!.PageOrdinal < 0", 1, "YieldingFinalReadSubstitutionWithholdsCandidate"),
        ("final-exact-paired-witness", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "!witness.Bytes.Span.SequenceEqual(currentWitness.Bytes.Span)", "currentWitness.Bytes.Length < 0", 1, "YieldingFinalReadSubstitutionWithholdsCandidate"),
        ("canonical-mismatch-fallback", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "catch (LogicalReplayCanonicalMismatchException)", "catch (NotSupportedException)", 1, "NoncanonicalRoundtripFallsBackWithoutApplyOrSave"),
        ("retained-witness-decoding-charge", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "budget.Reserve(checked(4 * witness.Bytes.Length + 4096))", "budget.Reserve(0)", 1, "EligibleTailCandidateOwnsPrivateCanonicalBytesAndFreshCompleteOrigin"),
        ("private-state-clearing", BASE.SERVER / "Events/DaprLogicalSnapshotCandidate.cs", "Interlocked.Exchange(ref _state, null)?.Dispose();", "GC.KeepAlive(_state); _state = null;", 1, "EligibleTailCandidateOwnsPrivateCanonicalBytesAndFreshCompleteOrigin"),
        ("retained-actual-owner-fence", BASE.SERVER / "Events/DaprLogicalSnapshotCandidate.cs", "(_fence ?? throw new ObjectDisposedException(nameof(DaprLogicalSnapshotCandidate)))(token)", "Task.FromResult(_fence)", 1, "RetainedCandidateRechecksActualPairAndHistory"),
        ("completed-owner-decision", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "calls != 1 || running is null || !running.IsCompleted", "calls < 0 || running is null || !running.IsCompleted && calls < 0", 1, "SerializedOwnerMustCompleteExactlyOneOriginalDecision"),
        ("snapshot-final-return-cleanup", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "catch { captured?.Dispose(); throw; }", "catch { GC.KeepAlive(captured); throw; }", 1, "CancellationAtFinalOwnerReturnWithholdsAndClearsCandidate"),
        ("origin-final-return-cleanup", BASE.SERVER / "Events/DaprReplayOperationOwner.cs", "catch { captured?.Dispose(); throw; }", "catch { GC.KeepAlive(captured); throw; }", 1, "CancellationAtCompletedOriginFinalFenceRestoresCharge"),
        ("pair-final-return-cleanup", BASE.SERVER / "Events/DaprLogicalSnapshotOwner.cs", "catch { capturedPair.State?.Dispose(); capturedPair.Witness?.Dispose(); throw; }", "catch { GC.KeepAlive(capturedPair); throw; }", 1, "CancellationAtPrivatePairReturnRestoresChargesAndPreservesActorBytes"),
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
                result = execute(name)
                lines = result.stdout.splitlines(); failures = [i for i, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if not result.returncode or not failures: raise RuntimeError("Mutation survived/missed named behavioral killer: " + name)
                counts = summary(result)
                if counts["errors"] or counts["skipped"] or counts["notRun"]: raise RuntimeError("Mutation did not fully execute")
                rows.append({"mutation": name, "result": "killed", "killingTest": killer, "summary": counts, "failureExcerpt": lines[failures[0]:failures[0]+14]})
                (output / "mutations.json").write_text(json.dumps(rows, indent=2) + "\n")
                print(name + ": killed", flush=True)
            finally: path.write_bytes(original)
    root_after = root_hashes()
    (output / "result.json").write_text(json.dumps({"scope": "dormant private snapshot-candidate prerequisite", "configuration": args.configuration, "dependencyMode": args.dependency_mode,
        "controls": controls, "mutations": rows, "timeoutSecondsPerLane": args.timeout, "controlOnly": args.control_only, "excludedExternalPaths": excluded,
        "rootInputsBefore": root_before, "rootInputsAfter": root_after, "rootInputSetUnchanged": root_before == root_after, "commands": commands,
        "copiedFixtureFiles": list(fixture_files), "fullWorkspaceQualified": False, "productionQualification": False, "activationAuthority": False}, indent=2) + "\n")
    print(json.dumps({"control": "passed", "tests": controls["snapshot"]["total"], "mutantsKilled": len(rows), "rootInputSetUnchanged": root_before == root_after}))

if __name__ == "__main__": main()
