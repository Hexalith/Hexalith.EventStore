#!/usr/bin/env python3
"""Build and execute the isolated loader candidate with bounded owned effects."""

from __future__ import annotations

import hashlib
import argparse
import json
import os
from pathlib import Path
import platform
import shutil
import signal
import subprocess
import tempfile
import time
import zipfile


def sha256(path: Path) -> str:
    """Hash the exact bytes at one consumed or retained fixture path."""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bounded_run(command: list[str], cwd: Path, output: Path,
                timeout: int, environment: dict[str, str] | None = None) -> dict:
    """Bound one process group, retain output and kill its owned children on timeout."""
    started = time.monotonic()
    process = subprocess.Popen(command, cwd=cwd, env=environment, stdout=subprocess.PIPE,
                               stderr=subprocess.STDOUT, text=True, start_new_session=True)
    timed_out = False
    try:
        stdout, _ = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        timed_out = True
        os.killpg(process.pid, signal.SIGKILL)
        stdout, _ = process.communicate(timeout=5)
    output.write_text(stdout)
    result = {"command": command, "working_directory": str(cwd),
              "timeout_seconds": timeout, "timed_out": timed_out,
              "exit_code": process.returncode, "elapsed_seconds": round(time.monotonic() - started, 3),
              "log": output.name, "log_sha256": sha256(output)}
    if timed_out or process.returncode != 0:
        raise RuntimeError(json.dumps(result))
    return result


def main() -> None:
    """Copy sources outside the product build, run independent controls and seal new evidence."""
    evidence = Path(__file__).resolve().parent
    repository = evidence.parents[4]
    source = evidence / "fixture"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-name", default="results")
    options = parser.parse_args()
    if Path(options.results_name).name != options.results_name:
        raise ValueError("Results name must be a single owned child-directory name.")
    output = evidence / options.results_name
    if output.exists():
        raise RuntimeError("Choose a fresh result directory; retained evidence is never overwritten.")
    output.mkdir()
    work = Path(tempfile.mkdtemp(prefix="story-6-6-loader-probe-"))
    shutil.copytree(source, work / "fixture")
    shutil.copy2(repository / "global.json", work / "global.json")
    sources = [{"path": str(path.relative_to(evidence)), "sha256": sha256(path),
                "bytes": path.stat().st_size}
               for path in sorted(source.rglob("*")) if path.is_file()]
    sources.append({"path": "run_probe.py", "sha256": sha256(Path(__file__)),
                    "bytes": Path(__file__).stat().st_size})
    sources.append({"path": "repository:global.json", "sha256": sha256(repository / "global.json"),
                    "bytes": (repository / "global.json").stat().st_size})
    (output / "source-inventory.json").write_text(json.dumps(sources, indent=2) + "\n")
    commands = []
    commands.append(bounded_run(["dotnet", "--info"], work, output / "dotnet-info.log", 20))
    commands.append(bounded_run(["cc", "--version"], work, output / "cc-version.log", 20))
    for project in ["Host", "Plugin", "Undeclared"]:
        commands.append(bounded_run(["dotnet", "build", f"fixture/{project}/{project}.csproj",
                                     "--configuration", "Release", "-m:1", "-warnaserror"],
                                    work, output / f"build-{project.lower()}.log", 60))
    native = work / "libprobe_unmanifested.so"
    commands.append(bounded_run(["cc", "-shared", "-fPIC", "-O2", "-Wall", "-Wextra", "-Werror",
                                 "-o", str(native), str(work / "fixture/native/owned_fixture.c")],
                                work, output / "build-native.log", 20))
    content = work / "content-addressed"
    content.mkdir()
    pins = []
    binaries = []
    binary_directory = output / "binaries"
    binary_directory.mkdir()
    for project in ["Host", "Plugin", "Declared", "Undeclared"]:
        directory = work / f"fixture/{project}/bin/Release/net10.0"
        for path in sorted(directory.glob("*")):
            if path.is_file() and path.suffix in [".dll", ".pdb", ".json"]:
                target = binary_directory / path.name
                if not target.exists():
                    shutil.copy2(path, target)
                    binaries.append({"path": str(target.relative_to(evidence)),
                                     "sha256": sha256(target), "bytes": target.stat().st_size})
        assembly = directory / f"Probe.{project}.dll"
        digest = sha256(assembly)
        addressed = content / digest / assembly.name
        addressed.parent.mkdir()
        shutil.copy2(assembly, addressed)
        if project in ["Plugin", "Declared"]:
            pins.append({"Name": f"Probe.{project}", "Path": str(addressed), "Sha256": digest})
        if project == "Undeclared":
            unmanifested_managed = addressed
    shutil.copy2(native, binary_directory / native.name)
    binaries.append({"path": str((binary_directory / native.name).relative_to(evidence)),
                     "sha256": sha256(native), "bytes": native.stat().st_size})
    manifest = work / "managed-pins.json"
    manifest.write_text(json.dumps(pins, indent=2) + "\n")
    shutil.copy2(manifest, output / "consumed-managed-pins.json")
    (output / "binary-inventory.json").write_text(json.dumps(binaries, indent=2) + "\n")
    host = work / "fixture/Host/bin/Release/net10.0/Probe.Host.dll"
    scenarios = ["pinned-managed", "ordinary-undeclared-managed", "explicit-default-managed",
                 "explicit-other-managed", "ordinary-undeclared-native", "explicit-native"]
    observations = []
    for mode in scenarios:
        effects = work / f"{mode}.effects.txt"
        environment = dict(os.environ)
        environment["STORY_6_6_OWNED_EFFECTS"] = str(effects)
        consumed = [{"path": str(path), "sha256": sha256(path)}
                    for path in [host, manifest, unmanifested_managed, native]
                    + [Path(pin["Path"]) for pin in pins]]
        command = ["dotnet", str(host), mode, str(manifest), str(unmanifested_managed),
                   str(native), str(effects)]
        execution = bounded_run(command, work, output / f"run-{mode}.log", 20, environment)
        after = [{"path": item["path"], "sha256": sha256(Path(item["path"]))} for item in consumed]
        if consumed != after:
            raise RuntimeError("Consumed bytes changed during the isolated scenario.")
        raw = (output / f"run-{mode}.log").read_text()
        result = json.loads(raw.splitlines()[-1])
        effect_bytes = effects.read_bytes() if effects.exists() else b""
        (output / f"effects-{mode}.txt").write_bytes(effect_bytes)
        observations.append({"mode": mode, "execution": execution, "consumed": consumed,
                             "consumed_unchanged_after_execution": True, "result": result,
                             "effect_file_sha256": hashlib.sha256(effect_bytes).hexdigest()})
    by_mode = {item["mode"]: item["result"] for item in observations}
    (output / "raw-observations.json").write_text(json.dumps(observations, indent=2) + "\n")
    positive = by_mode["pinned-managed"]
    assert positive["effects"] == ["managed:declared"] and positive["callbackReturned"]
    assert not positive["capabilityRejected"] and positive["candidateReadyAtEnd"]
    normal = by_mode["ordinary-undeclared-managed"]
    assert not normal["effects"] and not normal["callbackReturned"] and normal["capabilityRejected"]
    for mode in ["explicit-default-managed", "explicit-other-managed"]:
        result = by_mode[mode]
        assert result["effects"] == ["managed:undeclared"] and result["callbackReturned"]
        assert result["capabilityRejected"]
        invalidated = next(event["sequence"] for event in result["events"]
                           if event["kind"] == "capability-invalidated")
        returned = next(event["sequence"] for event in result["events"]
                        if event["kind"] == "foreign-effect-returned")
        rejected = next(event["sequence"] for event in result["events"]
                        if event["kind"] == "host-capability-rejected")
        assert invalidated < returned < rejected
    native_normal = by_mode["ordinary-undeclared-native"]
    assert not native_normal["effects"] and not native_normal["callbackReturned"]
    assert native_normal["capabilityRejected"] and native_normal["nativeResolutionCalls"] == 1
    native_escape = by_mode["explicit-native"]
    assert native_escape["effects"] == ["native:constructor", "native:export"]
    assert native_escape["callbackReturned"] and native_escape["capabilityRejected"]
    assert native_escape["nativeResolutionCalls"] == 0
    assert native_escape["perAssemblyNativeResolverCalls"] == 0
    result = {"result": "negative-candidate-qualification", "controls": "passed",
              "scope": "dedicated AssemblyLoadContext plus observation cannot prevent these explicit fixture escapes",
              "product_policy_adopted": False, "readiness_granted": False,
              "owned_temporary_directory": str(work), "platform": platform.platform(),
              "commands": commands, "observations": observations}
    (output / "result.json").write_text(json.dumps(result, indent=2) + "\n")
    with zipfile.ZipFile(output / "consumed-binaries.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(binary_directory.iterdir()):
            archive.write(path, path.name)
    inventory = [{"path": str(path.relative_to(evidence)), "sha256": sha256(path),
                  "bytes": path.stat().st_size}
                 for path in sorted(evidence.rglob("*")) if path.is_file()]
    (output / "sealed-inventory.json").write_text(json.dumps(inventory, indent=2) + "\n")
    print(json.dumps({"result": result["result"], "controls": result["controls"],
                      "scenarios": len(observations), "owned_temporary_directory": str(work)}))


if __name__ == "__main__":
    main()
