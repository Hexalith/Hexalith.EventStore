#!/usr/bin/env python3
"""Verify dormant retained managed-image composition in isolated, timed source lanes.

Shared Default imports are supplied object/file claims, not immutable executed
framework evidence. No native/process completeness or activation is established.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import math
import pathlib
import shutil
import tempfile
import time
from xml.sax.saxutils import quoteattr


ROOT = pathlib.Path(__file__).resolve().parents[1]
CLIENT = pathlib.Path("src/Hexalith.EventStore.Client")
TESTS = pathlib.Path("tests/Hexalith.EventStore.Client.Tests")
TEST_CLASS = "Hexalith.EventStore.Client.Tests.Events.EventManagedArtifactSetTests"


def execute(helper, work: pathlib.Path, output: pathlib.Path, label: str, timeout: int,
            configuration: str, dependency_mode: str):
    deadline = time.monotonic() + timeout
    project = work / TESTS / f"{TESTS.name}.csproj"
    project_references = "true" if dependency_mode == "source" else "false"
    build = helper.run(["dotnet", "build", str(project), "--configuration", configuration,
                        f"-p:UseHexalithProjectReferences={project_references}", "-m:1", "--nologo"],
                       work, output / f"{label}-build.log", timeout)
    if build.returncode:
        raise RuntimeError(f"Lane did not compile for {label}; see {output / f'{label}-build.log'}")
    remaining = math.floor(deadline - time.monotonic())
    if remaining < 1:
        raise RuntimeError(f"Lane {label} exhausted its {timeout}s deadline before execution")
    assembly = work / TESTS / f"bin/{configuration}/net10.0/{TESTS.name}.dll"
    return helper.run(["dotnet", str(assembly), "-class", TEST_CLASS], work,
                      output / f"{label}-tests.log", remaining)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--timeout", type=int, default=60)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--dependency-mode", choices=("source", "packages"), default="source")
    args = parser.parse_args()
    if not 1 <= args.timeout <= 60:
        parser.error("timeout must be between 1 and 60 seconds per lane")
    module_spec = importlib.util.spec_from_file_location("router_guards", ROOT / "scripts/verify-event-evolution-replay-routing.py")
    assert module_spec and module_spec.loader
    helper = importlib.util.module_from_spec(module_spec)
    module_spec.loader.exec_module(helper)
    helper.TESTS = TESTS
    helper.TEST_CLASS = TEST_CLASS
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    mutations = [
        ("static-reference-admission", "EventManagedArtifactSet.cs",
         "if (!_images.ContainsKey(identity) && !_imports.ContainsKey(identity))",
         "if (_images.ContainsKey(identity) && _imports.ContainsKey(identity))",
         "MissingStaticManagedReferenceRefusesBeforeContextCreationAndReleasesAllCharges"),
        ("simple-name-ambiguity", "EventManagedArtifactSet.cs",
         "!_simpleNames.Add(new AssemblyName(identity).Name!) || ", "",
         "SameSimpleNameWithDifferentIdentityRefusesBeforePrivateLoading"),
        ("managed-default-fallback", "EventManagedArtifactSet.cs",
         'throw new InvalidOperationException("CapabilityMismatch: undeclared managed resolution cannot use the Default fallback.");',
         "return AssemblyLoadContext.Default.LoadFromAssemblyName(name);",
         "UndeclaredDynamicResolutionThrowsBeforeDefaultFallbackAndFencesSubsequentWork"),
        ("native-default-fallback", "EventManagedArtifactLoadContext.cs",
         'throw new InvalidOperationException("CapabilityMismatch: this private managed composition admits no native load route.");',
         "return 0;", "NativeResolutionRefusesBeforeProbingAndFencesSubsequentWork"),
        ("context-load-observation", "EventManagedArtifactSet.cs",
         "AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;", "",
         "OwnedContextContaminationFencesSubsequentWork"),
        ("foreign-context-provenance", "EventManagedArtifact.cs",
         "context is not null && context.Assemblies.Any",
         "context is not null && context.Assemblies.Where(static assembly => assembly.IsDynamic).Any",
         "ArtifactContextSeamRefusesPreExistingSameIdentityForeignBytes"),
        ("private-image-clearing", "EventManagedArtifact.cs",
         "CryptographicOperations.ZeroMemory(_image);", "",
         "DisposalClearsPrivateImagesAndFencesRetainedBindings"),
    ]
    rows = []
    with tempfile.TemporaryDirectory(prefix="eventstore-managed-artifact-guards-") as temporary:
        work = pathlib.Path(temporary)
        shutil.copytree(ROOT / CLIENT, work / CLIENT, ignore=shutil.ignore_patterns("bin", "obj"))
        for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
            (work / name).write_text(f"<Project><Import Project={quoteattr(str(ROOT / name))} /></Project>\n", encoding="utf-8")
        shutil.copyfile(ROOT / "global.json", work / "global.json")
        project = work / CLIENT / f"{CLIENT.name}.csproj"
        project.write_text(project.read_text(encoding="utf-8").replace(
            '..\\Hexalith.EventStore.Contracts\\Hexalith.EventStore.Contracts.csproj',
            str(ROOT / "src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj")), encoding="utf-8")
        target = work / TESTS
        target.mkdir(parents=True)
        shutil.copyfile(ROOT / TESTS / "Events/EventManagedArtifactSetTests.cs", target / "EventManagedArtifactSetTests.cs")
        (target / f"{TESTS.name}.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup><ItemGroup>'
            '<ProjectReference Include="../../src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj" />'
            '<PackageReference Include="Microsoft.NET.Test.Sdk" /><PackageReference Include="xunit.v3" />'
            '<PackageReference Include="Shouldly" /></ItemGroup><ItemGroup><Using Include="Xunit" /></ItemGroup></Project>\n', encoding="utf-8")
        (work / "tests/Directory.Build.props").write_text(
            f"<Project><Import Project={quoteattr(str(ROOT / 'tests/Directory.Build.props'))} /></Project>\n", encoding="utf-8")
        control = execute(helper, work, output, "control", args.timeout, args.configuration, args.dependency_mode)
        if control.returncode or "Failed: 0" not in control.stdout or "Skipped: 0" not in control.stdout:
            raise RuntimeError(f"Unmutated control failed: {output / 'control-tests.log'}")
        for name, filename, before, after, killer in mutations:
            path = work / CLIENT / "Events" / filename
            original = path.read_bytes()
            try:
                helper.replace_once(path, before, after)
                result = execute(helper, work, output, name, args.timeout, args.configuration, args.dependency_mode)
                lines = result.stdout.splitlines()
                failures = [i for i, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if result.returncode == 0 or not failures:
                    raise RuntimeError(f"Mutation survived or missed its named killing test: {name}")
                diagnostic = next(line.strip() for line in lines[failures[0] + 1:] if line.strip())
                rows.append({"mutation": name, "result": "killed", "killingTest": killer,
                             "observedFailure": diagnostic, "timeoutSecondsPerLane": args.timeout})
            finally:
                path.write_bytes(original)
    receipt = {"scope": "dormant retained-image managed composition with supplied Default imports",
               "configuration": args.configuration, "dependencyMode": args.dependency_mode,
               "control": "passed", "mutations": rows, "immutableFrameworkExecution": False,
               "processObservationQualification": False, "activationAuthority": False}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    main()
