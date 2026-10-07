#!/usr/bin/env python3
"""Qualify dormant managed process observations in isolated, bounded source lanes.

The supplied framework/file inventory is a fixture, not an authoritative catalog.
No native observation, immutable process image binding or activation is claimed.
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
OBSERVER = CLIENT / "Events/EventEvolutionManagedLoadObserver.cs"
FIXTURE = ROOT / "scripts/fixtures/event-evolution-process-managed-observation"


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


def prepare(work: pathlib.Path) -> None:
    shutil.copytree(ROOT / CLIENT, work / CLIENT, ignore=shutil.ignore_patterns("bin", "obj"))
    for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
        (work / name).write_text(f"<Project><Import Project={quoteattr(str(ROOT / name))} /></Project>\n", encoding="utf-8")
    shutil.copyfile(ROOT / "global.json", work / "global.json")
    project = work / CLIENT / "Hexalith.EventStore.Client.csproj"
    project.write_text(project.read_text(encoding="utf-8").replace(
        '..\\Hexalith.EventStore.Contracts\\Hexalith.EventStore.Contracts.csproj',
        str(ROOT / "src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj")), encoding="utf-8")
    probe = work / "probe"
    shutil.copytree(FIXTURE, probe)
    shutil.copyfile(ROOT / "tests/Hexalith.EventStore.Client.Tests/Events/Fixtures/EventRegistryV17.json",
                    probe / "EventRegistryV17.json")
    (probe / "Probe.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
        '<AssemblyName>Hexalith.EventStore.Client.Tests</AssemblyName><IsPackable>false</IsPackable>'
        '</PropertyGroup><ItemGroup><ProjectReference Include="../src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj" />'
        '<None Update="EventRegistryV17.json" CopyToOutputDirectory="PreserveNewest" />'
        '</ItemGroup></Project>\n', encoding="utf-8")


def build(work: pathlib.Path, output: pathlib.Path, label: str, timeout: int) -> float:
    deadline = time.monotonic() + timeout
    result = run(["dotnet", "build", "probe/Probe.csproj", "--configuration", "Debug",
                  "-p:UseHexalithProjectReferences=true", "-m:1", "--nologo"],
                 work, output / f"{label}-build.log", timeout)
    if result.returncode:
        raise RuntimeError(f"Lane did not compile: {label}; see {output / f'{label}-build.log'}")
    return deadline


def execute(work: pathlib.Path, output: pathlib.Path, label: str, scenario: str, deadline: float) -> subprocess.CompletedProcess[str]:
    remaining = math.floor(deadline - time.monotonic())
    if remaining < 1:
        raise RuntimeError(f"Lane {label} exhausted its deadline before execution")
    return run(["dotnet", "probe/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll", scenario],
               work, output / f"{label}-{scenario}.log", remaining)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--timeout", type=int, default=60)
    args = parser.parse_args()
    if not 1 <= args.timeout <= 60:
        parser.error("--timeout must be between 1 and 60 seconds per lane")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    cases = ["value-equal-unlisted", "value-equal-declared", "value-equal-unloaded", "declared", "startup-unlisted", "late-unlisted", "reflection-unlisted",
             "dynamic-unlisted", "in-flight-unlisted"]
    mutations = [
        ("process-baseline", "? AppDomain.CurrentDomain.GetAssemblies()", "? _contexts.SelectMany(static context => context.Assemblies)", "startup-unlisted"),
        ("unlisted-process-context", "if (_requireAllManagedContexts) { _capabilityLoss.ObserveViolation(); }", "", "late-unlisted"),
        ("assembly-load-subscription", "AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;", "", "reflection-unlisted"),
        ("context-membership-identity", "new HashSet<AssemblyLoadContext>(ReferenceEqualityComparer.Instance)", "new HashSet<AssemblyLoadContext>()", "value-equal-declared"),
        ("context-binding-identity", "= new(new EventManagedAssemblyBindingComparer());", "= [];", "value-equal-declared"),
        ("context-presence-identity", "AssemblyLoadContext.All.Any(candidate => ReferenceEquals(candidate, context))", "AssemblyLoadContext.All.Contains(context)", "value-equal-unloaded"),
    ]
    controls = []
    killed = []
    with tempfile.TemporaryDirectory(prefix="eventstore-process-managed-observation-") as temporary:
        work = pathlib.Path(temporary)
        prepare(work)
        build(work, output, "control", args.timeout)
        for case in cases:
            result = execute(work, output, "control", case, time.monotonic() + args.timeout)
            if result.returncode:
                raise RuntimeError(f"Control failed: {case}; see {output / f'control-{case}.log'}")
            receipt = json.loads(result.stdout.strip().splitlines()[-1])
            if receipt.get("scenario") != case or receipt.get("result") != "passed":
                raise RuntimeError(f"Unexpected control receipt: {case}")
            controls.append(receipt)
        observer = work / OBSERVER
        original = observer.read_bytes()
        for name, before, after, case in mutations:
            try:
                source = original.decode()
                if source.count(before) != 1:
                    raise RuntimeError(f"Mutation anchor is missing or ambiguous: {name}")
                observer.write_text(source.replace(before, after), encoding="utf-8")
                deadline = build(work, output, name, args.timeout)
                result = execute(work, output, name, case, deadline)
                if result.returncode == 0 or "Fixture assertion failed:" not in result.stdout:
                    raise RuntimeError(f"Mutation survived or failed outside its assertion: {name}")
                diagnostic = next(line.strip() for line in result.stdout.splitlines() if "Fixture assertion failed:" in line)
                killed.append({"mutation": name, "result": "killed", "killing_case": case,
                               "observed_failure": diagnostic, "timeout_seconds_per_lane": args.timeout})
            finally:
                observer.write_bytes(original)
    receipt = {"scope": "dormant supplied-inventory managed process observation only",
               "controls": controls, "mutations": killed, "nativeObservation": False,
               "immutableProcessImageBinding": False, "activationAuthority": False}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    main()
