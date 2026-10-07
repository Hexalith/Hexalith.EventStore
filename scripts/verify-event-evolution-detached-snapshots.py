#!/usr/bin/env python3
"""Verify optional snapshot or proof-framing guards in isolated, bounded source lanes."""

from __future__ import annotations

import argparse
import importlib.util
import json
import pathlib
import shutil
import tempfile
from xml.sax.saxutils import quoteattr


ROOT = pathlib.Path(__file__).resolve().parents[1]
CLIENT = pathlib.Path("src/Hexalith.EventStore.Client")
TESTS = pathlib.Path("tests/Hexalith.EventStore.Client.Tests")
TEST_CLASS = "Hexalith.EventStore.Client.Tests.Handlers.DetachedStateCaptureTests"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--timeout", type=int, default=60)
    parser.add_argument("--proof-framing", action="store_true", help="Run the separate opaque outer-proof framing controls.")
    args = parser.parse_args()
    if not 1 <= args.timeout <= 60:
        parser.error("timeout must be between 1 and 60 seconds")
    module_spec = importlib.util.spec_from_file_location("router_guards", ROOT / "scripts/verify-event-evolution-replay-routing.py")
    assert module_spec and module_spec.loader
    helper = importlib.util.module_from_spec(module_spec)
    module_spec.loader.exec_module(helper)
    helper.TESTS = TESTS
    helper.TEST_CLASS = "Hexalith.EventStore.Client.Tests.Events.EventEvolutionProofFramingTests" if args.proof_framing else TEST_CLASS
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    rows = []
    mutations = [
        ("typed-detachment", [("Handlers/DomainProcessorStateRehydrator.cs",
          "return snapshotCapture is null ? typed : owner.CaptureState(typed, snapshotCapture);", "return typed;")],
         "ApplyFailureOrCancellationMutatesOnlyDetachedState"),
        ("pre-tail-capture", [("Handlers/DomainProcessorStateRehydrator.cs",
          "object? snapshot = snapshotCapture is null ? null\n                    : CaptureReplayInput(current.SnapshotState, cancellationToken, owner, snapshotCapture, depth + 1);",
          "object? snapshot = null;"), ("Handlers/DomainProcessorStateRehydrator.cs", "if (snapshotCapture is null) {", "if (true) {")],
         "SnapshotIsCapturedBeforeCountAndEnumerationMutateSource"),
        ("graph-admission", [("Handlers/LegacyCommandReplayInput.cs", "Account(declaration.MaximumAccountedBytes);", "")],
         "WholeGraphChargeIsAdmittedBeforeCaptureCallback"),
        ("root-identity", [("Handlers/DetachedStateCapture.cs", "ReferenceEquals(source, copy) || ", "")],
         "AliasedCaptureRefusesBeforeTailAndHandle"),
        ("getter-cancellation", [("Handlers/DomainProcessorBase.cs",
          "catch (Exception) when (cancellationToken.IsCancellationRequested)", "catch (Exception) when (!cancellationToken.IsCancellationRequested)")],
         "DeclarationGetterCancellationAndFailurePreservesOriginatingToken(aggregate: False)"),
    ]
    if args.proof_framing:
        mutations = [
            ("route-claim-ceiling", [("Events/EventEvolutionProofFraming.cs",
             "private const int MaximumRouteClaimBytes = 1024 * 1024;", "private const int MaximumRouteClaimBytes = 2 * 1024 * 1024;")],
             "ExactRouteClaimLimitAndNextByteAreIndependent(oversized: True)"),
            ("command-proof-ceiling", [("Events/EventEvolutionProofFraming.cs",
             "if (bytes.Length > maximum)", "if (bytes.Length > MaximumPageProofBytes)")],
             "CommandWholeProofCeilingRefusesBeforeNestedAllocation"),
            ("proof-scratch-charge", [("Events/EventEvolutionProofFraming.cs", "budget.Reserve(charge)", "budget.Reserve(0)")],
             "ScratchChargeIsReservedBeforeCopyOrEntryBuffers"),
            ("command-checkpoint-fence", [("Events/EventEvolutionProofFraming.cs", "allowCheckpoint &= !commandPage;", "")],
             "CheckpointIsRejectedOutsideExplicitNonCommandPreparation(commandPage: True, allowCheckpoint: True)"),
            ("proof-private-clearing", [("Events/UnverifiedEventEvolutionProofFrame.cs", "CryptographicOperations.ZeroMemory(_bytes);", "")],
             "ExactOuterBytesRemainPrivateAndOpaqueUntilSeparateClaimVerification"),
        ]
    with tempfile.TemporaryDirectory(prefix="eventstore-detached-snapshot-guards-") as temporary:
        work = pathlib.Path(temporary)
        shutil.copytree(ROOT / CLIENT, work / CLIENT, ignore=shutil.ignore_patterns("bin", "obj"))
        for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
            (work / name).write_text(f"<Project><Import Project={quoteattr(str(ROOT / name))} /></Project>\n", encoding="utf-8")
        shutil.copyfile(ROOT / "global.json", work / "global.json")
        project = work / CLIENT / f"{CLIENT.name}.csproj"
        project.write_text(project.read_text().replace('..\\Hexalith.EventStore.Contracts\\Hexalith.EventStore.Contracts.csproj',
                           str(ROOT / "src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj")), encoding="utf-8")
        target = work / TESTS
        target.mkdir(parents=True)
        for source in (ROOT / TESTS / "Handlers").glob("Detached*.cs"):
            shutil.copyfile(source, target / source.name)
        if args.proof_framing:
            shutil.copyfile(ROOT / TESTS / "Events/EventEvolutionProofFramingTests.cs", target / "EventEvolutionProofFramingTests.cs")
        (target / f"{TESTS.name}.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup><ItemGroup>'
            '<ProjectReference Include="../../src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj" />'
            '<PackageReference Include="Microsoft.NET.Test.Sdk" /><PackageReference Include="xunit.v3" />'
            '<PackageReference Include="Shouldly" /></ItemGroup><ItemGroup><Using Include="Xunit" /></ItemGroup></Project>\n', encoding="utf-8")
        (work / "tests/Directory.Build.props").write_text(
            f"<Project><Import Project={quoteattr(str(ROOT / 'tests/Directory.Build.props'))} /></Project>\n", encoding="utf-8")
        control = helper.execute(work, output, "control", args.timeout)
        if control.returncode or "Failed: 0" not in control.stdout:
            raise RuntimeError(f"Unmutated control failed: {output / 'control-tests.log'}")
        for name, replacements, killer in mutations:
            originals = {work / CLIENT / relative: (work / CLIENT / relative).read_bytes() for relative, _, _ in replacements}
            try:
                for relative, before, after in replacements:
                    helper.replace_once(work / CLIENT / relative, before, after)
                result = helper.execute(work, output, name, args.timeout)
                lines = result.stdout.splitlines()
                failures = [i for i, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if result.returncode == 0 or not failures:
                    raise RuntimeError(f"Mutation survived or missed its named killing test: {name}")
                diagnostic = next(line.strip() for line in lines[failures[0] + 1:] if line.strip())
                rows.append({"mutation": name, "result": "killed", "killingTest": killer,
                             "observedFailure": diagnostic, "timeoutSecondsPerLane": args.timeout})
            finally:
                for path, source in originals.items():
                    path.write_bytes(source)
    scope = "opaque outer proof framing; no semantic claim/signature/source verification or activation authority" if args.proof_framing else \
        "optional owner-declared legacy typed snapshot capture; no generic isolation or activation authority"
    receipt = {"scope": scope,
               "control": "passed", "mutations": rows}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    main()
