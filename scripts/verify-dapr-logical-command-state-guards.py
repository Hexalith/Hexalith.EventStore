#!/usr/bin/env python3
"""Run isolated timed compiling command-state controls; grant no deployment authority."""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib
import re
import shutil
import subprocess
import tempfile
import time
from xml.sax.saxutils import quoteattr

ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("command_base", ROOT / "scripts/verify-dapr-logical-model-guards.py")
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
CLIENT = pathlib.Path("src/Hexalith.EventStore.Client")
SERVER = pathlib.Path("src/Hexalith.EventStore.Server")
DOMAIN = pathlib.Path("src/Hexalith.EventStore.DomainService")
TESTS = pathlib.Path("tests/Hexalith.EventStore.Server.Tests")
DTESTS = pathlib.Path("tests/Hexalith.EventStore.DomainService.Tests")
VECTOR = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-command-state-2026-10-08/vectors.json")
CLASS = "Hexalith.EventStore.Server.Tests.Events.DaprLogicalCommandStateTests"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--timeout", type=int, default=60)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--dependency-mode", choices=("source", "packages"), default="packages")
    parser.add_argument("--control-only", action="store_true")
    parser.add_argument("--mutation", action="append", default=[], help="Execute only named mutations after all unmutated controls")
    args = parser.parse_args()
    if not 1 <= args.timeout <= 60:
        parser.error("timeout must be 1..60 seconds per lane")
    BASE.CONFIGURATION = args.configuration
    BASE.DEPENDENCY_MODE = args.dependency_mode
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    def root_hashes():
        files = [path for directory in (BASE.CLIENT, BASE.SERVER, BASE.DOMAIN, BASE.CONTRACTS, BASE.DEFAULTS, BASE.UNIQUE_IDS)
                 for path in (ROOT / directory).rglob("*") if path.is_file() and "bin" not in path.parts and "obj" not in path.parts]
        files += list((ROOT / TESTS / "Events").glob("Dapr*.cs"))
        files += [path for path in (ROOT / DTESTS).glob("*.cs") if path.name.startswith(("Bounded", "MutatingBounded", "AsyncDomainProcessor"))]
        files += [ROOT / VECTOR, pathlib.Path(__file__), ROOT / "scripts/verify-dapr-logical-model-guards.py",
                  ROOT / "scripts/verify-dapr-logical-command-state-vectors.py", ROOT / ".github/workflows/event-evolution-local-guards.yml"]
        return {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in files}
    root_before = root_hashes()
    # Preserve concurrent external work. CI's clean tracked checkout excludes none.
    retained_external = ("ExpiredIdentityHistoryCertificate.cs", "IDeletionCapabilityCompromiseRegistrar.cs", "IExpiredIdentityHistoryCustody.cs", "DeletionBatchCapabilityIdentity.cs")
    excluded = [path for path in subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard", "src/Hexalith.EventStore.Server/Security", "src/Hexalith.EventStore.Contracts/Security"], cwd=ROOT, text=True).splitlines() if path.endswith(".cs") and pathlib.Path(path).name not in retained_external]
    original_run = BASE.run
    def all_lane_hashes(work):
        return {str(path.relative_to(work)): hashlib.sha256(path.read_bytes()).hexdigest()
                for path in work.rglob("*") if path.is_file() and "bin" not in path.parts and "obj" not in path.parts}
    def helper_hashes():
        files = [ROOT / path for path in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "tests/Directory.Build.props", ".editorconfig", ".gitattributes", "nuget.config")]
        files += [path for path in (ROOT / "references/Hexalith.Builds").rglob("*") if path.is_file() and ".git" not in path.parts and path.suffix in (".props", ".targets", ".json", ".config")]
        return {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in files}
    def stable_run(command, work, log, timeout):
        before = all_lane_hashes(work)
        helpers_before = helper_hashes()
        assembly_directory = pathlib.Path(command[1]).parent if command[0] == "dotnet" and command[1].endswith(".dll") else None
        def dlls():
            return {} if assembly_directory is None else {str(path.relative_to(work)): hashlib.sha256(path.read_bytes()).hexdigest() for path in assembly_directory.glob("*.dll")}
        dlls_before = dlls()
        result = original_run(command, work, log, timeout)
        after = all_lane_hashes(work)
        helpers_after = helper_hashes()
        dlls_after = dlls()
        BASE.COMMANDS[-1].update({"allLaneInputsSha256Before": before, "allLaneInputsSha256After": after,
            "dynamicLaneInputSetUnchanged": before == after, "allImportedHelpersSha256Before": helpers_before,
            "allImportedHelpersSha256After": helpers_after, "dynamicImportedHelperSetUnchanged": helpers_before == helpers_after,
            "executedDllsSha256Before": dlls_before, "executedDllsSha256After": dlls_after,
            "dynamicExecutedDllSetUnchanged": dlls_before == dlls_after})
        if before != after or helpers_before != helpers_after or dlls_before != dlls_after:
            raise RuntimeError("Dynamic copied/imported/executed input set drifted during command")
        return result
    BASE.run = stable_run
    rows = []
    mutations = [
        ("terminal-proof-atomic-save", SERVER / "Events/DaprReplayOperationOwner.cs",
         "await _stateManager.SetStateAsync(CommandProofKey, prepared.CommandProof.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);",
         "await Task.CompletedTask.ConfigureAwait(false);", 1, "CompletedOwnerRoutesOnlyPrivateCanonicalStateAndRetainsExactRetry"),
        ("all-prior-save-readback", SERVER / "Events/DaprReplayOperationOwner.cs",
         "participants.AsSpan().SequenceEqual(prepared.ExpectedParticipantDigest) && ", "", 1,
         "TerminalSaveRequiresEveryPriorParticipantAndExactAtomicProof"),
        ("actual-owner-command-fence", CLIENT / "Events/PrivateLogicalCommandState.cs",
         "await _actualOwnerFence(token).ConfigureAwait(false);", "await Task.CompletedTask.ConfigureAwait(false);", 1,
         "ActualParticipantAndCallerSubstitutionsRefuseBeforeProcessor"),
        ("private-command-substitution", CLIENT / "Events/PrivateLogicalCommandState.cs",
         "|| !DaprLogicalCommandStateCodec.CommandHash(request.Command, _budget, token).AsSpan().SequenceEqual(_claim.Value.CommandHash.Span)", "", 1,
         "ActualParticipantAndCallerSubstitutionsRefuseBeforeProcessor"),
        ("caller-state-substitution", CLIENT / "Events/PrivateLogicalCommandState.cs",
         "|| request.CurrentState is not null", "", 1,
         "ActualParticipantAndCallerSubstitutionsRefuseBeforeProcessor"),
        ("command-decoder-cancellation", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "finally { token.ThrowIfCancellationRequested(); }", "finally { _ = token; }", 8,
         "ThrowingCancelledDecoderRefusesBeforeProcessorAndClearsCapturedBytes"),
        ("pre-callback-predecessor-seal", SERVER / "Events/DaprReplayOperationOwner.cs",
         "!freshParticipants.AsSpan().SequenceEqual(admittedPriorParticipants)", "freshParticipants.Length < 0", 1,
         "LaterPageCallbackCannotReplaceAdmittedPredecessorHistory"),
        ("completed-next-ledger-absence", SERVER / "Events/DaprReplayOperationOwner.cs",
         "if (next.HasValue) { return false; }", "if (!record.IsComplete && next.HasValue) { return false; }", 1,
         "ActualParticipantAndCallerSubstitutionsRefuseBeforeProcessor"),
        ("command-proof-expiry", CLIENT / "Events/DaprLogicalClaimTrust.cs",
         "|| now >= claim.ExpiresAt", "", 1,
         "ExpiryAndStrictPurposeSevenSchemaRefuseWithoutCommandAuthority"),
        ("produced-output-charge", DOMAIN / "BoundedV1DomainResultProducer.cs",
         "new PrivateProducedDomainResult(new DomainServiceWireResult(isRejection, events, resultPayload), resultCharge)",
         "new PrivateProducedDomainResult(new DomainServiceWireResult(isRejection, events, resultPayload), null)", 1,
         "ProducedResultRemainsChargedThroughYieldingFinalOwnerFenceAndClearsOnRefusal"),
        ("produced-output-clearing", DOMAIN / "PrivateProducedDomainResult.cs",
         "CryptographicOperations.ZeroMemory(item.Payload);", "GC.KeepAlive(item.Payload);", 1,
         "ProducedResultRemainsChargedThroughYieldingFinalOwnerFenceAndClearsOnRefusal"),
        ("admission-stage-state-seal", DOMAIN / "DomainServiceRequestRouter.cs",
         "if (stateFence is not null) { await stateFence(request.CurrentState!, cancellationToken).ConfigureAwait(false); }",
         "await Task.CompletedTask.ConfigureAwait(false);", 1,
         "StageStateMutationRefusesBeforeLaterStageOrProcessor"),
        ("bounded-noop-result", DOMAIN / "DomainServiceRequestRouter.cs",
         "if (commandFence is not null) {\n            throw new InvalidOperationException(\"CapabilityMismatch: logical command results require the explicit bounded producer.\");",
         "if (commandFence is not null && result.Events.Count != 0) {\n            throw new InvalidOperationException(\"CapabilityMismatch: logical command results require the explicit bounded producer.\");", 1,
         "NoOpCannotBypassBoundedResultPayloadAdmission"),
        ("individual-result-getter-fences", DOMAIN / "BoundedV1DomainResultProducer.cs",
         '        async Task<T> ReadCallbackAsync<T>(Func<T> read) {\n            cancellationToken.ThrowIfCancellationRequested();\n            if (commandFence is not null) { await commandFence(cancellationToken).ConfigureAwait(false); }\n            try { return read(); }\n            finally {\n                cancellationToken.ThrowIfCancellationRequested();\n                if (commandFence is not null) { await commandFence(cancellationToken).ConfigureAwait(false); }\n                cancellationToken.ThrowIfCancellationRequested();\n            }\n        }',
         "Task<T> ReadCallbackAsync<T>(Func<T> read) => Task.FromResult(read());", 1, "ResultGetterLossStopsBeforeEveryLaterGetter"),
        ("purpose-seven-decoding-charge", CLIENT / "Events/DaprLogicalClaimTrust.cs",
         "budget.Reserve(checked(Math.Min(claim.Length, DaprLogicalClaimCodec.MaximumClaimBytes) * 4 + 4096))",
         "budget.Reserve(0)", 3, "ExpiryAndStrictPurposeSevenSchemaRefuseWithoutCommandAuthority"),
        ("post-await-private-command-pin", CLIENT / "Events/PrivateLogicalCommandState.cs",
         "if (!DaprLogicalCommandStateCodec.CommandHash(_command!, _budget, token).AsSpan().SequenceEqual(_claim.Value.CommandHash.Span)) { throw new InvalidOperationException(\"ProofMismatch: private command changed during the actual owner await.\"); }",
         "await Task.CompletedTask.ConfigureAwait(false);", 1,
         "YieldingOwnerFenceCommandSubstitutionRefusesBeforeProcessor"),
        ("command-read-borrow-lifetime", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "object state; try { using var lease = new InvocationPayloadLease(canonical, token); state = _read(lease, token); } finally { token.ThrowIfCancellationRequested(); }",
         "using var lease = new InvocationPayloadLease(canonical, token); object state; try { state = _read(lease, token); } finally { token.ThrowIfCancellationRequested(); }", 1,
         "YieldingActualOwnerFenceSeesExpiredCommandAndProducerBorrowedFacades"),
        ("effective-chain-commitment", CLIENT / "Events/DaprLogicalReplayCommitmentCodec.cs",
         "writer.WriteHash(routeHash.Span);", "writer.WriteHash(new byte[32]);", 1,
         "EveryCommandCommitmentMatchesIndependentPythonVectors"),
        ("exact-page-transcript", CLIENT / "Events/DaprLogicalReplayCommitmentCodec.cs",
         "writer.WriteHash(entry.RequestHash.Span);", "writer.WriteHash(new byte[32]);", 1,
         "EveryCommandCommitmentMatchesIndependentPythonVectors"),
        ("actual-command-payload-capacity", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "+ commandBytes + 12 * 1024 * 1024", "+ 12 * 1024 * 1024", 1,
         "MaximumCommandPayloadUsesActualPartitionCapacity"),
        ("private-command-metadata-charge", CLIENT / "Events/PrivateLogicalCommandState.cs",
         "budget.Reserve(checked(command.Payload.Length + 4 * 1024 * 1024))", "budget.Reserve(command.Payload.Length)", 1,
         "CommandExtensionCeilingKeepsConservativeDictionaryCharge"),
        ("owner-command-metadata-charge", SERVER / "Events/DaprReplayOperationOwner.cs",
         "_bufferBudget.Reserve(checked(command.Payload.Length + 4 * 1024 * 1024))", "_bufferBudget.Reserve(command.Payload.Length)", 1,
         "CommandExtensionCeilingKeepsConservativeDictionaryCharge"),
        ("private-command-owner-drop", CLIENT / "Events/PrivateLogicalCommandState.cs",
         "_command = null;", "GC.KeepAlive(_command);", 1, "DisposedCommandOwnersClearAndDropEveryPrivateCommandReference"),
        ("replay-command-owner-drop", SERVER / "Events/DaprReplayOperationOwner.cs",
         "_command = null;", "GC.KeepAlive(_command);", 1, "DisposedCommandOwnersClearAndDropEveryPrivateCommandReference"),
        ("reconstruction-callback-cancellation", CLIENT / "Aggregates/RegisteredLogicalReplayBinding.cs",
         "finally { token.ThrowIfCancellationRequested(); }", "finally { _ = token; }", 8,
         "SynchronousReconstructionCallbacksPreserveOriginalCancellationAndDurableBytes"),
    ]
    selected = set(args.mutation)
    if selected - {row[0] for row in mutations}:
        parser.error("Unknown mutation: " + ", ".join(sorted(selected - {row[0] for row in mutations})))
    with tempfile.TemporaryDirectory(prefix="eventstore-command-state-") as temporary:
        work = pathlib.Path(temporary)
        BASE.prepare(work)
        for filename in (".editorconfig", ".gitattributes", "nuget.config"):
            shutil.copyfile(ROOT / filename, work / filename)
        for relative in excluded:
            (work / relative).unlink()
        for pattern in ("DaprLogicalReconstruction*.cs", "DaprLogicalCommandState*.cs", "DaprLogicalCallbackFenceTests.cs"):
            for path in (ROOT / TESTS / "Events").glob(pattern):
                shutil.copyfile(path, work / TESTS / "Events" / path.name)
        (work / VECTOR).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / VECTOR, work / VECTOR)
        reconstruction = pathlib.Path("_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/vectors.json")
        (work / reconstruction).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / reconstruction, work / reconstruction)
        (work / DTESTS).mkdir(parents=True)
        for filename in ("BoundedProducerTestEvent.cs", "BoundedProducerSerializedTestEvent.cs", "BoundedProducerUnknownTestEvent.cs",
                         "MutatingBoundedProducerResult.cs", "BoundedV1DomainResultProducerTests.cs", "BoundedV1DomainResultProducerScratchTests.cs",
                         "AsyncDomainProcessorRoutingTests.cs", "ChangingCountWireEvents.cs"):
            shutil.copyfile(ROOT / DTESTS / filename, work / DTESTS / filename)
        (work / DTESTS / f"{DTESTS.name}.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup><ItemGroup>'
            f'<ProjectReference Include="../../{DOMAIN}/{DOMAIN.name}.csproj" />'
            '<FrameworkReference Include="Microsoft.AspNetCore.App" />'
            '<PackageReference Include="Microsoft.NET.Test.Sdk" /><PackageReference Include="xunit.v3" />'
            '<PackageReference Include="Shouldly" /><PackageReference Include="NSubstitute" />'
            '</ItemGroup><ItemGroup><Using Include="Xunit" /></ItemGroup></Project>\n', encoding="utf-8")
        def execute(label, all_controls=False):
            start = time.monotonic()
            project = work / TESTS / f"{TESTS.name}.csproj"
            build = BASE.run(["dotnet", "build", str(project), "--configuration", args.configuration,
                "-p:UseHexalithProjectReferences=" + ("true" if args.dependency_mode == "source" else "false"), "-m:1", "--nologo"], work, output / f"{label}-build.log", args.timeout)
            if build.returncode:
                raise RuntimeError(f"Lane did not compile: {label}; see {output / f'{label}-build.log'}")
            remaining = int(args.timeout - (time.monotonic() - start))
            if remaining < 1:
                raise RuntimeError("Lane deadline exhausted before execution")
            command = ["dotnet", str(work / TESTS / f"bin/{args.configuration}/net10.0/{TESTS.name}.dll")]
            if not all_controls:
                command += ["-class", CLASS]
            result = BASE.run(command, work, output / f"{label}-tests.log", remaining)
            return result, round(time.monotonic() - start, 3)
        control, elapsed = execute("control", True)
        if control.returncode or "Failed: 0" not in control.stdout or "Skipped: 0" not in control.stdout or "Not Run: 0" not in control.stdout:
            raise RuntimeError("Unmutated control failed")
        def summary(result):
            match = re.search(r"Total:\s*(\d+), Errors:\s*(\d+), Failed:\s*(\d+), Skipped:\s*(\d+), Not Run:\s*(\d+)", result.stdout)
            if not match: raise RuntimeError("Missing exact executed test summary")
            return dict(zip(("total", "errors", "failed", "skipped", "notRun"), map(int, match.groups())))
        controls = {"serverSourceReconstructionCallbacksAndCommand": summary(control)}
        print("control: passed", flush=True)
        for label, target in (("client-model-control", BASE.CTESTS), ("domain-compatibility-control", DTESTS)):
            result, duration = BASE.execute(work, output, label, target, args.timeout)
            if result.returncode or "Failed: 0" not in result.stdout or "Skipped: 0" not in result.stdout or "Not Run: 0" not in result.stdout:
                raise RuntimeError(f"Existing {label} control failed")
            controls[label] = summary(result)
            print(f"{label}: passed", flush=True)
        if not args.control_only:
            for name, relative, before, after, count, killer in mutations:
                if selected and name not in selected:
                    continue
                path = work / relative
                original = path.read_bytes()
                try:
                    source = original.decode().replace("\r\n", "\n")
                    tokens = re.findall(r'"(?:\\.|[^"\\])*"|\w+|[^\w\s]', before)
                    pattern = re.compile(r"\s*".join(re.escape(token) for token in tokens))
                    if len(list(pattern.finditer(source))) != count:
                        raise RuntimeError(f"Mutation anchor missing/ambiguous: {name}")
                    path.write_text(pattern.sub(lambda _: after, source), encoding="utf-8")
                    result, duration = execute(name)
                    lines = result.stdout.splitlines()
                    failures = [i for i, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                    if not result.returncode or not failures:
                        raise RuntimeError(f"Mutation survived or missed named killer: {name}")
                    rows.append({"mutation": name, "result": "killed", "killingTest": killer,
                                 "failureExcerpt": lines[failures[0]:failures[0] + 14], "elapsedSeconds": duration})
                    print(f"{name}: killed", flush=True)
                finally:
                    path.write_bytes(original)
    root_after = root_hashes()
    receipt = {"scope": "dormant actual-owner completed logical command intake", "configuration": args.configuration,
               "dependencyMode": args.dependency_mode, "control": "passed", "controlSeconds": elapsed, "controlSummaries": controls, "mutations": rows,
               "timeoutSecondsPerLane": args.timeout, "controlOnly": args.control_only, "selectedMutations": sorted(selected),
               "retainedExternalDependencyNames": list(retained_external), "excludedExternalPaths": excluded, "allRootInputsSha256Before": root_before,
               "allRootInputsSha256After": root_after, "rootInputSetUnchanged": root_before == root_after, "commands": BASE.COMMANDS,
               "actualCrossActorInvocation": False, "productionQualification": False, "activationAuthority": False}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
