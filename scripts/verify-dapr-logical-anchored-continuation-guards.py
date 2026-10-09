#!/usr/bin/env python3
"""Seal sequential disposable anchored continuation controls; this does not replace the root solution gate."""
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
SPEC = importlib.util.spec_from_file_location("continuation_base", ROOT / "scripts/verify-dapr-logical-model-guards.py")
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
STESTS = BASE.STESTS
SNAPSHOT_VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-snapshot-2026-10-09/vectors.json")
CLASS = "Hexalith.EventStore.Server.Tests.Events.DaprLogicalAnchoredContinuationTests"
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
        paths += [ROOT / name for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "tests/Directory.Build.props", ".editorconfig", ".gitattributes", "nuget.config", str(VECTOR), str(COMMAND_VECTOR), str(SNAPSHOT_VECTOR), "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-anchored-replay-2026-10-09/vectors.json", "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/vectors.json", "_bmad-output/implementation-artifacts/story-6-6-dapr-logical-anchored-continuation-model.md")]
        return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(paths))}
    root_before = root_hashes()
    (output / "attempt.json").write_text(json.dumps({"rootInputsBefore": root_before, "excludedExternalPaths": excluded, "retainedExternalDependencyNames": sorted(retained), "isolationSubstitutions": [], "fullWorkspaceQualified": False}, indent=2) + "\n")
    BASE.prepare(work)
    for name in (".editorconfig", ".gitattributes", "nuget.config"):
        shutil.copyfile(ROOT / name, work / name)
    for name in excluded:
        (work / name).unlink()
    fixture_files = ("DaprLogicalSnapshotFixture.cs", "DaprLogicalSnapshotTests.cs", "DaprLogicalReconstructionFixture.cs", "DaprLogicalReconstructionTestState.cs", "DaprLogicalReconstructionTests.cs", "DaprLogicalAnchorFixture.cs", "DaprLogicalAnchoredReplayTests.cs", "DaprLogicalAnchoredContinuationFixture.cs", "DaprLogicalAnchoredContinuationTests.cs", "DaprLogicalAnchoredContinuationCodecTests.cs", "DaprLogicalCommandStateTests.cs", "DaprLogicalCommandStateProcessor.cs", "DaprLogicalCommandStateAdmissionStage.cs", "DaprLogicalCommandStateResult.cs", "DaprLogicalCommandStateTestEvent.cs", "DaprLogicalCommandStateSerializedEvent.cs", "DaprLogicalCommandStateEventList.cs")
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
    controls = {"continuationMethods": {}, "continuationTotal": 0}
    for class_name in (CLASS, (CLASS[:-5] + "CodecTests")):
        methods = re.findall(r"public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\(", (ROOT / STESTS / "Events" / (class_name.split(".")[-1] + ".cs")).read_text())
        for method in methods:
            control = execute("control-" + method, class_name=class_name, method=method)
            counts = summary(control)
            if control.returncode or counts["total"] < 1 or any(counts[key] for key in ("errors", "failed", "skipped", "notRun")):
                raise RuntimeError("Continuation control failed: " + method)
            controls["continuationMethods"][method] = counts
            controls["continuationTotal"] += counts["total"]
    print("continuation controls: passed " + str(controls["continuationTotal"]), flush=True)
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
    mutations = [
        ("distinct-prefix-framing", BASE.CLIENT / "Events/DaprLogicalAnchoredPrefixCodec.cs", "w.WriteRaw(Separator);", 'w.WriteRaw("HX-EV-DAPR-PREFIX-1\\0"u8);', 1, "IndependentContinuationVectorsMatchRuntime"),
        ("canonical-prefix-tags", BASE.CLIENT / "Events/DaprLogicalAnchoredPrefixCodec.cs", "reader.ReadByte() != tag", "reader.ReadByte() == 255", 1, "StrictAnchoredPrefixRefusesUnsupportedShapes"),
        ("exact-selection-scope", BASE.CLIENT / "Events/DaprLogicalAnchoredClaimTrust.cs", "|| prefix.TenantId != selection.TenantId || prefix.Domain != selection.Domain || prefix.AggregateId != selection.AggregateId || prefix.AggregateType != selection.AggregateType || prefix.ActorHead != selection.ActorHead || prefix.TargetSequence != selection.TargetSequence || !prefix.SourceBindingHash.Span.SequenceEqual(selection.SourceBindingHash.Span) || !prefix.RegistryFingerprint.Span.SequenceEqual(selection.RegistryFingerprint.Span)", "", 1, "ExactAnchoredTrustRefusesSelectionSubstitution"),
        ("retained-prefix-decoding-charge", BASE.CLIENT / "Events/DaprLogicalAnchoredClaimTrust.cs", "budget.Reserve(checked(claim.Length * 4 + 4096))", "budget.Reserve(0)", 2, "ExactAnchoredTrustRefusesSelectionSubstitution"),
        ("anchored-request-separator", BASE.CLIENT / "Events/DaprLogicalReplayCommitmentCodec.cs", 'writer.WriteRaw("HX-EV-DAPR-ANCHORED-PAGE-REQUEST-1\\0"u8);', 'writer.WriteRaw("HX-EV-DAPR-PAGE-REQUEST-1\\0"u8);', 1, "IndependentContinuationVectorsMatchRuntime"),
        ("anchored-transcript-separator", BASE.CLIENT / "Events/DaprLogicalReplayCommitmentCodec.cs", 'writer.WriteRaw("HX-EV-DAPR-ANCHORED-TRANSCRIPT-STEP-1\\0"u8);', 'writer.WriteRaw("HX-EV-DAPR-TRANSCRIPT-STEP-1\\0"u8);', 1, "IndependentContinuationVectorsMatchRuntime"),
        ("anchored-effective-separator", BASE.CLIENT / "Events/DaprLogicalReplayCommitmentCodec.cs", '"HX-EV-DAPR-ANCHORED-EFFECTIVE-STEP-1\\0"u8', '"HX-EV-DAPR-EFFECTIVE-STEP-1\\0"u8', 1, "IndependentContinuationVectorsMatchRuntime"),
        ("anchored-ledger-separator", BASE.SERVER / "Events/DaprAnchoredReplayLedgerCodec.cs", 'w.WriteRaw("HX-EV-DAPR-ANCHORED-LEDGER-1\\0"u8);', 'w.WriteRaw("HX-EV-DAPR-REPLAY-LEDGER-2\\0"u8);', 1, "IndependentContinuationVectorsMatchRuntime"),
        ("actual-anchor-participant", BASE.SERVER / "Events/DaprReplayOperationOwner.cs", "if (!await RequireAnchorParticipantAsync(token).ConfigureAwait(false))", "if (token.IsCancellationRequested)", 1, "ActualCommittedHistorySubstitutionRefusesBeforeCallbacks"),
        ("no-below-head-zero", BASE.CLIENT / "Events/DaprLogicalAnchoredPrefixCodec.cs", [("|| claim.CoveredSequence == p.TargetSequence && p.TargetSequence < p.ActorHead", "", 1), ("|| p.TargetSequence != p.ActorHead", "", 1)], None, None, "StrictAnchoredPrefixRefusesUnsupportedShapes"),
        ("callback-predecessor-seal", BASE.SERVER / "Events/DaprReplayOperationOwner.cs", "!freshParticipants.AsSpan().SequenceEqual(admittedPriorParticipants)", "freshParticipants.Length < 0", 1, "LaterCallbackCannotReplaceActualAnchoredPredecessor"),
        ("private-anchor-clearing", BASE.SERVER / "Events/DaprReplayOperationOwner.cs", "_anchorImage?.Dispose();", "GC.KeepAlive(_anchorImage);", 1, "YieldingInitialAliasSubstitutionRefusesAndDropsOwnedCapacity"),
        ("initial-covered-progress", BASE.SERVER / "Events/DaprReplayOperationOwner.cs", "binding.TargetSequence, 0, _anchoredIntake?.Selection.CoveredSequence ?? 0,", "binding.TargetSequence, 0, 0,", 1, "ActualAnchoredTailBeginPageReadbackAndRetry"),
        ("actual-anchored-transcript", BASE.SERVER / "Events/DaprReplayOperationOwner.cs", "DaprLogicalReplayCommitmentCodec.AnchoredTranscriptStep(_tenant, _operation, _anchoredIntake.SelectionHash, prior.TranscriptHash!, entry, budget)", "DaprLogicalReplayCommitmentCodec.TranscriptStep(_tenant, _operation, prior.SourceBindingHash, prior.RegistryFingerprint, Memory(prior.ReconstructionBindingHash), prior.TranscriptHash!, entry, budget)", 1, "ActualAnchoredTailBeginPageReadbackAndRetry"),
        ("exact-route-trust-object", BASE.CLIENT / "Events/DaprLogicalAnchoredClaimTrust.cs", "!ReferenceEquals(routes, _routes)", "routes is null", 1, "EquivalentRouteTrustCannotGrantAnchoredBegin"),
        ("retained-intake-reference-drop", BASE.SERVER / "Events/DaprReplayOperationOwner.cs", "_anchoredIntake = null;", "GC.KeepAlive(_anchoredIntake);", 1, "YieldingInitialAliasSubstitutionRefusesAndDropsOwnedCapacity"),
        ("original-cancellation-before-entry", BASE.SERVER / "Events/DaprReplayOperationOwner.Anchored.cs", "_anchoredOriginatingToken.ThrowIfCancellationRequested();", "GC.KeepAlive(_anchoredOriginatingToken);", 1, "DualCanceledTokensRefuseBeforeGateOrSideEffects"),
        ("exact-entry-token-before-owner-work", BASE.SERVER / "Events/DaprReplayOperationOwner.cs", [("RequireAnchoredEntry(cancellationToken);", "cancellationToken.ThrowIfCancellationRequested();", 2), ("RequireAnchoredEntry(token);", "token.ThrowIfCancellationRequested();", 2)], None, None, "DifferentRequestTokenCannotContinueRetainedOperation"),
        ("ordinary-effective-chain-commitment", BASE.CLIENT / "Events/DaprLogicalReplayCommitmentCodec.cs", "writer.WriteHash(routeHash.Span);", "writer.WriteHash(new byte[32]);", 1, "EveryCommandCommitmentMatchesIndependentPythonVectors"),
        ("ordinary-exact-page-transcript", BASE.CLIENT / "Events/DaprLogicalReplayCommitmentCodec.cs", "writer.WriteHash(entry.RequestHash.Span);", "writer.WriteHash(new byte[32]);", 1, "EveryCommandCommitmentMatchesIndependentPythonVectors"),
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
                result = execute(name, class_name=(CLASS[:-5] + "CodecTests") if killer in ("IndependentContinuationVectorsMatchRuntime", "StrictAnchoredPrefixRefusesUnsupportedShapes", "ExactAnchoredTrustRefusesSelectionSubstitution") else COMMAND_CLASS if killer == "EveryCommandCommitmentMatchesIndependentPythonVectors" else CLASS, method=killer)
                lines = result.stdout.splitlines(); failures = [i for i, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if not result.returncode or not failures: raise RuntimeError("Mutation survived/missed named behavioral killer: " + name)
                counts = summary(result)
                if counts["errors"] or counts["skipped"] or counts["notRun"]: raise RuntimeError("Mutation did not fully execute")
                rows.append({"mutation": name, "result": "killed", "killingTest": killer, "summary": counts, "failureExcerpt": lines[failures[0]:failures[0]+14]})
                (output / "mutations.json").write_text(json.dumps(rows, indent=2) + "\n")
                print(name + ": killed", flush=True)
            finally: path.write_bytes(original)
    root_after = root_hashes()
    (output / "result.json").write_text(json.dumps({"scope": "dormant distinct anchored continuation through actual replay owner; all external runtime inputs preserved", "configuration": args.configuration, "dependencyMode": args.dependency_mode,
        "controls": controls, "mutations": rows, "timeoutSecondsPerLane": args.timeout, "controlOnly": args.control_only, "excludedExternalPaths": excluded,
        "rootInputsBefore": root_before, "rootInputsAfter": root_after, "rootInputSetUnchanged": root_before == root_after, "commands": commands,
        "copiedFixtureFiles": list(fixture_files), "fullWorkspaceQualified": False, "productionQualification": False, "activationAuthority": False}, indent=2) + "\n")
    print(json.dumps({"control": "passed", "tests": controls["snapshot"]["total"], "mutantsKilled": len(rows), "rootInputSetUnchanged": root_before == root_after}))

if __name__ == "__main__": main()
