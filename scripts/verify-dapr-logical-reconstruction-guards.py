#!/usr/bin/env python3
"""Run timed compiling controls for dormant canonical logical reconstruction.

Only private source copies are mutated. These checks grant no actor registration,
live Dapr topology, catalog, serving-key, production or activation authority.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib
import shutil
import tempfile


VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/vectors.json")


ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("logical_guards", ROOT / "scripts/verify-dapr-logical-model-guards.py")
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
CLIENT = pathlib.Path("src/Hexalith.EventStore.Client")
SERVER = pathlib.Path("src/Hexalith.EventStore.Server")
TESTS = pathlib.Path("tests/Hexalith.EventStore.Server.Tests")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--timeout", default=60, type=int)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--dependency-mode", choices=("source", "packages"), default="source")
    args = parser.parse_args()
    BASE.CONFIGURATION = args.configuration
    BASE.DEPENDENCY_MODE = args.dependency_mode
    if not 1 <= args.timeout <= 60:
        parser.error("timeout must be between 1 and 60 seconds per lane")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    mutations = [
        ("effective-payload-image", CLIENT / "Aggregates/PrivateLogicalReplayResponseVerifier.cs",
         "|| !SHA256.HashData(payload).AsSpan().SequenceEqual(route.EffectivePayloadHash.Span)", "", 1,
         "TamperedLaterLogicalResponseRefusesEntirePageBeforeCurrentCallbacksAndReleasesCharges"),
        ("within-page-last-good", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "sequence = item.Sequence;", "sequence = 0;", 1,
         "MutatingApplyFailureRetainsPrivateWithinPageLastGoodWithoutAnyTransition"),
        ("fold-read-borrow-lifetime", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "object state;\n                using (var lease = new InvocationPayloadLease(lastGood, token))\n                {\n                    state = _read(lease, token);\n                }",
         "using var lease = new InvocationPayloadLease(lastGood, token);\n                object state = _read(lease, token);", 1,
         "StateCallbackBorrowsExpireBeforeAsyncFenceAndLaterApplyOrWrite"),
        ("roundtrip-read-borrow-lifetime", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "object decoded;\n            using (var lease = new InvocationPayloadLease(canonical, token))\n            {\n                decoded = _read(lease, token);\n            }",
         "using var lease = new InvocationPayloadLease(canonical, token);\n            object decoded = _read(lease, token);", 1,
         "StateCallbackBorrowsExpireBeforeAsyncFenceAndLaterApplyOrWrite"),
        ("synchronous-writer-borrow-lifetime", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "ImmutablePayload canonical;\n        using (BoundedPayloadWriter writer = BoundedPayloadWriter.CreateLegacy(_maximumStateBytes, token, budget))\n        {\n            _write(state, writer, token);\n            canonical = writer.TakeCompletedPayload();\n        }",
         "using BoundedPayloadWriter writer = BoundedPayloadWriter.CreateLegacy(_maximumStateBytes, token, budget);\n        _write(state, writer, token);\n        await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);\n        ImmutablePayload canonical = writer.TakeCompletedPayload();", 1,
         "StateCallbackBorrowsExpireBeforeAsyncFenceAndLaterApplyOrWrite"),
        ("damaged-mutable-last-good", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "var failed = new PrivateLogicalReplayFold(lastGood, sequence, failure, item.Sequence, item.CanonicalType);",
         "using ImmutablePayload damaged = await SerializeAsync(state, budget, sourceFence, token).ConfigureAwait(false);\n                    lastGood.Dispose(); lastGood = ImmutablePayload.CopyFrom(damaged, budget, token);\n                    var failed = new PrivateLogicalReplayFold(lastGood, sequence, failure, item.Sequence, item.CanonicalType);", 1,
         "MutatingApplyFailureRetainsPrivateWithinPageLastGoodWithoutAnyTransition"),
        ("proof-heavy-partition", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "12L * responseBytes", "2L * responseBytes", 1,
         "ProofHeavySingleEventFitsConservativeIntakePartitionBeforeCallbacks"),
        ("complete-capacity-admission", SERVER / "Events/DaprReplayOperationOwner.cs",
         "prepared.Budget.CreatePartition(_reconstruction!.GetPreparationCapacity(prepared.Response.Bytes.Length, previous.Bytes.Length))",
         "new EventBufferBudget(_reconstruction!.GetPreparationCapacity(prepared.Response.Bytes.Length, previous.Bytes.Length))", 1,
         "CompleteReconstructionCapacityShrinksBeforeAnyApplyAndSingleEventFits"),
        ("canonical-roundtrip", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "if (canonical.Length != roundtrip.Length || !canonical.ComputeSha256().AsSpan().SequenceEqual(roundtrip.ComputeSha256()))",
         "if (canonical.Length < 0)", 1, "InvalidCanonicalCodecOutputRefusesBeforeAnySave"),
        ("strict-state-utf8", CLIENT / "Events/ImmutablePayload.cs",
         "_ = new UTF8Encoding(false, true).GetCharCount(owner.AsSpan(0, Length));", "", 1,
         "InvalidUtf8StateRefusesBeforeSuppliedSchemaReadOrSave"),
        ("callback-source-fence", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "await sourceFence(token).ConfigureAwait(false);", "await Task.CompletedTask.ConfigureAwait(false);", 1,
         "CallbackLossStopsBeforeAnyLaterCallbackOrTransition"),
        ("combined-working-admission", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "budget.Reserve(checked(_workingGraphBytes + 2 * _maximumStateBytes))", "budget.Reserve(0)", 2,
         "CombinedWorkingCapacityRefusesBeforeStateCallbacks"),
        ("begin-postsave-current-trust", SERVER / "Events/DaprReplayOperationOwner.cs",
         "try\n                {\n                    _reconstruction?.RequireCurrent(cancellationToken);\n                    await source.RequireCurrentAsync(binding, trust, cancellationToken).ConfigureAwait(false);\n                }",
         "try { await Task.CompletedTask.ConfigureAwait(false); }", 1,
         "BeginPostSaveLossPreservesExactInitialParticipants"),
        ("canonical-successor-save", SERVER / "Events/DaprReplayOperationOwner.cs",
         "await _stateManager.SetStateAsync(StateKey(pageOrdinal), prepared.CanonicalState.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);",
         "await Task.CompletedTask.ConfigureAwait(false);", 1,
         "MultiPageCanonicalStateAndExactRetriesShareCommittedAuthority"),
        ("canonical-final-save", SERVER / "Events/DaprReplayOperationOwner.cs",
         "await _stateManager.SetStateAsync(FinalStateKey, prepared.CanonicalState.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);",
         "await Task.CompletedTask.ConfigureAwait(false);", 1,
         "MultiPageCanonicalStateAndExactRetriesShareCommittedAuthority"),
        ("canonical-retry-readback", SERVER / "Events/DaprReplayOperationOwner.cs",
         "CanonicalState = state", "CanonicalState = null", 1,
         "MultiPageCanonicalStateAndExactRetriesShareCommittedAuthority"),
        ("canonical-state-clearing", SERVER / "Events/DaprLogicalResponseOwner.cs",
         "CryptographicOperations.ZeroMemory(bytes);", "GC.KeepAlive(bytes);", 1,
         "PrivateResponseAndCanonicalBuffersClearAfterDisposal"),
        ("canonical-begin-pending", SERVER / "Events/DaprReplayOperationOwner.cs",
         "if (result.Outcome == DaprReplayCommitOutcome.Indeterminate)\n            {\n                _beginPending = prepared;\n            }",
         "if (result.Outcome == DaprReplayCommitOutcome.Indeterminate) { prepared.Dispose(); }", 1,
         "BeginUncertainReadbackRetainsInitialBytesAndRetriesWithoutCallbacks"),
    ]
    source_files = []
    for directory in (CLIENT, SERVER, BASE.DOMAIN, BASE.CONTRACTS, BASE.DEFAULTS, BASE.UNIQUE_IDS):
        source_files += [path for path in (ROOT / directory).rglob("*") if path.is_file() and "bin" not in path.parts and "obj" not in path.parts]
    source_files += list((ROOT / TESTS / "Events").glob("Dapr*.cs"))
    source_files += [path for path in (ROOT / "references/Hexalith.Builds").rglob("*")
                     if path.is_file() and ".git" not in path.parts and path.suffix in (".props", ".targets", ".json", ".config")]
    source_files += [ROOT / path for path in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json",
        "tests/Directory.Build.props", "tests/Hexalith.EventStore.Client.Tests/Events/Fixtures/EventRegistryV17.json",
        "scripts/verify-dapr-logical-model-guards.py", "scripts/verify-dapr-logical-model-vectors.py",
        "scripts/verify-dapr-logical-reconstruction-vectors.py", ".github/workflows/event-evolution-local-guards.yml")]
    source_files += [ROOT / BASE.VECTOR, ROOT / VECTOR, pathlib.Path(__file__)]
    source_files = sorted(set(source_files))
    def hashes(paths, base):
        return {str(path.relative_to(base)): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
    root_hashes = hashes(source_files, ROOT)
    original_run = BASE.run
    def stable_run(command, work, log, timeout):
        lane_files = [path for path in work.rglob("*") if path.is_file() and "bin" not in path.parts and "obj" not in path.parts]
        before = hashes(lane_files, work)
        if hashes(source_files, ROOT) != root_hashes:
            raise RuntimeError("Root inputs changed before a timed command")
        result = original_run(command, work, log, timeout)
        after = hashes(lane_files, work)
        root_after = hashes(source_files, ROOT)
        BASE.COMMANDS[-1].update({"allLaneInputsSha256Before": before, "allLaneInputsSha256After": after,
                                 "rootInputsUnchanged": root_after == root_hashes, "laneInputsUnchanged": before == after})
        if before != after or root_after != root_hashes:
            raise RuntimeError("Pinned inputs changed during a timed command")
        return result
    BASE.run = stable_run
    rows = []
    with tempfile.TemporaryDirectory(prefix="eventstore-logical-reconstruction-") as temporary:
        work = pathlib.Path(temporary)
        BASE.prepare(work)
        (work / VECTOR).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / VECTOR, work / VECTOR)
        for path in (ROOT / TESTS / "Events").glob("DaprLogicalReconstruction*.cs"):
            shutil.copyfile(path, work / TESTS / "Events" / path.name)
        result, elapsed = BASE.execute(work, output, "control", TESTS, args.timeout)
        if result.returncode or "Failed: 0" not in result.stdout or "Skipped: 0" not in result.stdout:
            raise RuntimeError("Unmutated reconstruction control failed")
        print("control: passed", flush=True)
        for name, relative, before, after, count, killer in mutations:
            path = work / relative
            original = path.read_bytes()
            try:
                source = original.decode().replace("\r\n", "\n")
                if source.count(before) != count:
                    raise RuntimeError(f"Mutation anchor missing/ambiguous: {name}")
                path.write_text(source.replace(before, after), encoding="utf-8")
                result, elapsed = BASE.execute(work, output, name, TESTS, args.timeout)
                lines = result.stdout.splitlines()
                failures = [index for index, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if result.returncode == 0 or not failures:
                    raise RuntimeError(f"Mutation survived or missed named killing test: {name}")
                diagnostic = next(line.strip() for line in lines[failures[0] + 1:] if line.strip())
                rows.append({"mutation": name, "result": "killed", "killingTest": killer,
                             "observedFailure": diagnostic, "failureExcerpt": lines[failures[0]:failures[0] + 14], "elapsedSeconds": elapsed})
                print(f"{name}: killed", flush=True)
            finally:
                path.write_bytes(original)
    receipt = {"scope": "dormant canonical logical reconstruction", "configuration": args.configuration, "dependencyMode": args.dependency_mode,
               "control": "passed", "mutations": rows, "timeoutSecondsPerLane": args.timeout,
               "commands": BASE.COMMANDS, "allRootInputsSha256": root_hashes,
               "actualCrossActorInvocation": False, "productionQualification": False, "activationAuthority": False}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
