#!/usr/bin/env python3
"""Exercise legacy replay guards in isolated source lanes with bounded mutations.

This local control proves only router admission and dispatch guards. It grants no
authenticated replay, catalog, production, or activation authority.
"""

from __future__ import annotations

import argparse
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
DOMAIN = pathlib.Path("src/Hexalith.EventStore.DomainService")
TESTS = pathlib.Path("tests/Hexalith.EventStore.DomainService.Tests")
TEST_CLASS = "Hexalith.EventStore.DomainService.Tests.LegacyAggregateReplayRoutingTests"


def run(command: list[str], work: pathlib.Path, log: pathlib.Path, timeout: int) -> subprocess.CompletedProcess[str]:
    try:
        result = subprocess.run(command, cwd=work, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=timeout, check=False)
    except subprocess.TimeoutExpired as error:
        output = error.stdout or b""
        log.write_text(output.decode() if isinstance(output, bytes) else output, encoding="utf-8")
        raise RuntimeError(f"Timed out after {timeout}s: {command}; see {log}") from error
    log.write_text(result.stdout, encoding="utf-8")
    return result


def replace_once(path: pathlib.Path, before: str, after: str) -> None:
    source = path.read_text(encoding="utf-8")
    if source.count(before) != 1:
        raise RuntimeError(f"Mutation anchor is missing or ambiguous in {path}: {before}")
    path.write_text(source.replace(before, after), encoding="utf-8")


def prepare(work: pathlib.Path) -> None:
    for directory in (CLIENT, DOMAIN):
        shutil.copytree(ROOT / directory, work / directory, ignore=shutil.ignore_patterns("bin", "obj"))
    (work / "Directory.Build.props").write_text(
        f"<Project><Import Project={quoteattr(str(ROOT / 'Directory.Build.props'))} /></Project>\n", encoding="utf-8")
    (work / "Directory.Build.targets").write_text(
        f"<Project><Import Project={quoteattr(str(ROOT / 'Directory.Build.targets'))} /></Project>\n", encoding="utf-8")
    (work / "Directory.Packages.props").write_text(
        f"<Project><Import Project={quoteattr(str(ROOT / 'Directory.Packages.props'))} /></Project>\n", encoding="utf-8")
    shutil.copyfile(ROOT / "global.json", work / "global.json")
    for directory in (CLIENT, DOMAIN):
        project = work / directory / f"{directory.name}.csproj"
        source = project.read_text(encoding="utf-8")
        for dependency in ("Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults"):
            source = source.replace(f'..\\{dependency}\\{dependency}.csproj',
                                    str(ROOT / "src" / dependency / f"{dependency}.csproj"))
        project.write_text(source, encoding="utf-8")
    target = work / TESTS
    target.mkdir(parents=True)
    for name in ("LegacyAggregateReplayRoutingTests.cs", "ReplayAdmissionProbeCollection.cs",
                 "CustomAsyncReplayAggregate.cs", "CustomSyncReplayAggregate.cs", "Fixtures/WidgetDomain.cs"):
        destination = target / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / TESTS / name, destination)
    project = (ROOT / TESTS / f"{TESTS.name}.csproj").read_text(encoding="utf-8")
    project = project.replace('    <ProjectReference Include="..\\..\\src\\Hexalith.EventStore.Testing\\Hexalith.EventStore.Testing.csproj" />\n', "")
    (target / f"{TESTS.name}.csproj").write_text(project, encoding="utf-8")
    (work / "tests/Directory.Build.props").write_text(
        f"<Project><Import Project={quoteattr(str(ROOT / 'tests/Directory.Build.props'))} /></Project>\n", encoding="utf-8")


def execute(work: pathlib.Path, output: pathlib.Path, label: str, timeout: int) -> subprocess.CompletedProcess[str]:
    deadline = time.monotonic() + timeout
    project = work / TESTS / f"{TESTS.name}.csproj"
    build = run(["dotnet", "build", str(project), "--configuration", "Debug",
                 "-p:UseHexalithProjectReferences=true", "-m:1", "--nologo"],
                work, output / f"{label}-build.log", timeout)
    if build.returncode:
        raise RuntimeError(f"Lane did not compile for {label}; see {output / f'{label}-build.log'}")
    assembly = work / TESTS / "bin/Debug/net10.0" / f"{TESTS.name}.dll"
    remaining = math.floor(deadline - time.monotonic())
    if remaining < 1:
        raise RuntimeError(f"Lane {label} exhausted its {timeout}s mutation timeout before execution")
    return run(["dotnet", str(assembly), "-class", TEST_CLASS], work,
               output / f"{label}-tests.log", remaining)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--timeout", type=int, default=60)
    args = parser.parse_args()
    if not 1 <= args.timeout <= 60:
        parser.error("--timeout must be between 1 and 60 seconds per mutation lane")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    mutations = [
        ("async-private-admission", DOMAIN / "DomainServiceRequestRouter.cs",
         "using LegacyReplayInput input = LegacyReplayInput.Capture(request, cancellationToken);",
         "using LegacyReplayInput input = LegacyReplayInput.Capture(request with { Events = [] }, cancellationToken);",
         "OversizedSourceRefusesBeforeReadsOrServiceResolution(asynchronous: True"),
        ("async-custom-dispatch", DOMAIN / "DomainServiceRequestRouter.cs",
         "&& admitted.CanReplayAdmitted(asynchronous: true)", "", "ExplicitDerivedAsyncReplayKeepsCustomInterfaceDispatch"),
        ("sync-custom-dispatch", DOMAIN / "DomainServiceRequestRouter.cs",
         "&& admitted.CanReplayAdmitted(asynchronous: false)", "", "ExplicitDerivedSynchronousReplayKeepsCustomInterfaceDispatch"),
        ("async-complete-prefix", DOMAIN / "DomainServiceRequestRouter.cs",
         " ?? input.ValidatePrefix(cancellationToken)", "", "IncompleteOrDuplicatePrefixRefusesBeforeOwnershipCallbacks(asynchronous: True"),
        ("immutable-input-slots", CLIENT / "Aggregates/LegacyReplayInput.cs",
         "internal IReadOnlyList<ReplayEventEnvelope> Events => _eventView;",
         "internal IReadOnlyList<ReplayEventEnvelope> Events => _events;", "OwnershipMutationCannotReplacePrivateInputAndCompletionClearsCopies"),
    ]
    results = []
    with tempfile.TemporaryDirectory(prefix="eventstore-replay-router-guards-") as temporary:
        work = pathlib.Path(temporary)
        prepare(work)
        control = execute(work, output, "control", args.timeout)
        if control.returncode or "Failed: 0" not in control.stdout:
            raise RuntimeError(f"Unmutated control failed; see {output / 'control-tests.log'}")
        for name, relative, before, after, killer in mutations:
            path = work / relative
            original = path.read_bytes()
            try:
                replace_once(path, before, after)
                result = execute(work, output, name, args.timeout)
                failures = [line for line in result.stdout.splitlines() if "[FAIL]" in line]
                if result.returncode == 0 or not any(killer in line for line in failures):
                    raise RuntimeError(f"Mutation survived or failed outside its named killing vector: {name}")
                lines = result.stdout.splitlines()
                failure_index = next(index for index, line in enumerate(lines) if killer in line and "[FAIL]" in line)
                diagnostic = next(line.strip() for line in lines[failure_index + 1:] if line.strip())
                results.append({"mutation": name, "result": "killed", "killing_test": killer,
                                "observed_failure": diagnostic,
                                "timeout_seconds_per_lane": args.timeout})
            finally:
                path.write_bytes(original)
    receipt = {"scope": "local legacy replay admission and dispatch; no activation authority",
               "control": "passed", "mutations": results}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    main()
