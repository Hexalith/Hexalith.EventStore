#!/usr/bin/env python3
"""Qualify dormant managed-loader controls in an isolated source copy.

The baseline and each killing mutation have explicit build/test timeouts. Source
references remain pinned and shared read-only inputs; no repository source, Git
state, registration, qualification claim or activation fence is mutated.
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import re
import shutil
import subprocess
import tempfile


ROOT = pathlib.Path(__file__).resolve().parents[5]
OUTPUT = pathlib.Path(__file__).resolve().parent
PROJECT = pathlib.Path("tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj")
ASSEMBLY = pathlib.Path("tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll")
OBSERVER = pathlib.Path("src/Hexalith.EventStore.Client/Events/EventEvolutionManagedLoadObserver.cs")
ARTIFACT = pathlib.Path("src/Hexalith.EventStore.Client/Events/EventManagedArtifact.cs")
MUTATIONS = (
    ("assembly-load-subscription", OBSERVER,
     "AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;", "// isolated mutation: no late-load observation",
     "Hexalith.EventStore.Client.Tests.Events.EventEvolutionManagedLoadObserverTests.UndeclaredLateLoadIsDetectedWithoutPreventingTheLoad", 2),
    ("private-image-pin", ARTIFACT,
     "!actual.AsSpan().SequenceEqual(_hash)", "!actual.AsSpan().SequenceEqual(actual)",
     "Hexalith.EventStore.Client.Tests.Events.EventManagedArtifactTests.InvalidPinsAndMetadataRefuseBeforeLoadingAndReleasePrivateImage", 1),
    ("loaded-object-origin", ARTIFACT,
     "if (!ReferenceEquals(assembly, _assembly))", "if (false && !ReferenceEquals(assembly, _assembly))",
     "Hexalith.EventStore.Client.Tests.Events.EventManagedArtifactTests.ForeignAssemblyObjectAndForgedBindingRefuseBeforeCallbackUse", 1),
    ("original-dependency-declaration", ARTIFACT,
     "if (!_declaration!.Encoded.SequenceEqual(declaration.Encoded))", "if (false && !_declaration!.Encoded.SequenceEqual(declaration.Encoded))",
     "Hexalith.EventStore.Client.Tests.Events.EventPrivateImageRegistryCallbackTests.ComposedObserverRefusesSameHashUnderDifferentOriginalDependencyDeclaration", 2),
    ("private-image-preload-recheck", ARTIFACT,
     "RequirePrivateImageHash(cancellationToken);\n            if (_assembly is null)", "// isolated mutation: skip pre-load private-image check\n            if (_assembly is null)",
     "Hexalith.EventStore.Client.Tests.Events.EventManagedArtifactTests.ChangedRetainedPrivateImageRefusesBeforeLoadAndClearsItsCapacity", 1),
)


def run(command: list[str], workspace: pathlib.Path, label: str, timeout: int) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(command, cwd=workspace, text=True, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, timeout=timeout, check=False)
    (OUTPUT / f"{label}.log").write_text(
        "$ " + " ".join(command) + f"\nTimeout: {timeout}s\nExit: {result.returncode}\n" + result.stdout,
        encoding="utf-8")
    return result


def build(workspace: pathlib.Path, label: str) -> None:
    command = ["dotnet", "build", str(PROJECT), "--configuration", "Debug",
               "-p:UseHexalithProjectReferences=true",
               "-p:HexalithCommonsRoot=" + str((ROOT / "references/Hexalith.Commons").resolve()), "-m:1"]
    result = run(command, workspace, label, 180)
    if result.returncode != 0:
        raise RuntimeError(f"{label}: isolated build failed; see {label}.log")


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sources = {str(path): hashlib.sha256((ROOT / path).read_bytes()).hexdigest()
               for path in (OBSERVER, ARTIFACT)}
    outcomes = []
    with tempfile.TemporaryDirectory(prefix="event-managed-mutations-") as temporary:
        workspace = pathlib.Path(temporary)
        for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
                     "global.json", "nuget.config", "README.md", ".editorconfig", ".gitattributes"):
            shutil.copy2(ROOT / name, workspace / name)
        shutil.copytree(ROOT / "src", workspace / "src", ignore=shutil.ignore_patterns("bin", "obj"))
        shutil.copytree(ROOT / "tests/Hexalith.EventStore.Client.Tests", workspace / "tests/Hexalith.EventStore.Client.Tests",
                        ignore=shutil.ignore_patterns("bin", "obj"))
        shutil.copy2(ROOT / "tests/Directory.Build.props", workspace / "tests/Directory.Build.props")
        (workspace / "references").symlink_to(ROOT / "references", target_is_directory=True)
        build(workspace, "baseline-build")
        baseline = run(["dotnet", str(ASSEMBLY), "-class",
                        "Hexalith.EventStore.Client.Tests.Events.Event*", "-class",
                        "Hexalith.EventStore.Client.Tests.Events.EventPrivateImageRegistryCallbackTests"],
                       workspace, "baseline-tests", 60)
        if baseline.returncode != 0 or "Total: 0" in baseline.stdout:
            raise RuntimeError("isolated baseline tests failed or selected no tests")
        for label, path, original, replacement, method, expected_failures in MUTATIONS:
            source = workspace / path
            pristine = source.read_text(encoding="utf-8")
            if pristine.count(original) != 1:
                raise RuntimeError(f"{label}: mutation must replace exactly one source control")
            source.write_text(pristine.replace(original, replacement, 1), encoding="utf-8")
            try:
                build(workspace, label + "-build")
                result = run(["dotnet", str(ASSEMBLY), "-method", method], workspace, label + "-tests", 60)
                match = re.search(r"Failed:\s*(\d+)", result.stdout)
                if result.returncode == 0 or match is None or int(match.group(1)) != expected_failures:
                    raise RuntimeError(f"{label}: mutation was not killed by its owning controls")
                outcomes.append({"mutation": label, "result": "killed", "failed_controls": expected_failures,
                                 "build_timeout_seconds": 180, "test_timeout_seconds": 60})
            finally:
                source.write_text(pristine, encoding="utf-8")
    result = {"result": "passed", "scope": "isolated local managed-loader preparation; no readiness or activation authority",
              "source_sha256": sources, "mutations": outcomes}
    (OUTPUT / "mutation-result.json").write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    main()
