#!/usr/bin/env python3
"""Verify dormant exact-image options admission with compiling, timed mutations.

Supplied retained images do not establish complete catalogs, framework execution,
serving-peer pins, production qualification or activation authority.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib
import shutil
import tempfile
from xml.sax.saxutils import quoteattr


ROOT = pathlib.Path(__file__).resolve().parents[1]
CLIENT = pathlib.Path("src/Hexalith.EventStore.Client")
TESTS = pathlib.Path("tests/Hexalith.EventStore.Client.Tests")
TEST_CLASS = "Hexalith.EventStore.Client.Tests.Events.EventRuntimeOptionsBindingTests"


def module(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / filename)
    assert spec and spec.loader
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--timeout", type=int, default=60)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--dependency-mode", choices=("source", "packages"), default="source")
    args = parser.parse_args()
    if not 1 <= args.timeout <= 60:
        parser.error("timeout must be between 1 and 60 seconds per lane")
    helper = module("options_guard_helper", "verify-event-evolution-replay-routing.py")
    runner = module("options_guard_runner", "verify-event-evolution-managed-artifacts.py")
    runner.TEST_CLASS = TEST_CLASS
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    mutations = [
        ("single-options-callable", "EventImplementationBinding.cs",
         "runtimeOptions is not null && runtimeOptions.GetInvocationList().Length != 1", "false",
         "MulticastOptionsRefuseBeforeEitherCallback"),
        ("exact-options-object", "EventImplementationBinding.cs",
         "_runtimeOptionsExecutionBinding?.RequireBoundAssembly(_runtimeOptionsAssembly);", "",
         "SameIdentityDefaultOptionsRefuseBeforeAnyCallback"),
        ("implicit-options-image", "EventImplementationBinding.cs",
         "runtimeOptionsExecutionBinding ?? executionBinding", "runtimeOptionsExecutionBinding",
         "SameIdentityDefaultOptionsRefuseBeforeAnyCallback"),
        ("options-capability-scope", "EventImplementationBinding.cs",
         "_runtimeOptionsExecutionBinding?.RequireCapabilityScope(capabilityLoss);", "",
         "ForeignOptionsCapabilityScopeRefusesBeforeCallback"),
        ("post-options-boundary", "EventImplementationBinding.cs",
         "cancellationToken.ThrowIfCancellationRequested();\n        RequireCallableBindings();\n        RequireOptionsHash(options, cancellationToken);",
         "RequireOptionsHash(options, cancellationToken);", "OptionsCancellationOrObservedLossPrecedesReturnedOptionsParsing"),
        ("serializer-options-forwarding", "RegisteredCurrentEventDeserializer.cs",
         "serializerExecutionBinding, runtimeOptionsExecutionBinding);", "serializerExecutionBinding);",
         "RegisteredWrappersUseSeparatelyAdmittedOptionsAndRefuseDefaultSubstitution"),
        ("schema-options-forwarding", "RegisteredEventVersionValidation.cs",
         "schemaExecutionBinding, schemaRuntimeOptionsExecutionBinding);", "schemaExecutionBinding);",
         "RegisteredWrappersUseSeparatelyAdmittedOptionsAndRefuseDefaultSubstitution"),
        ("identity-options-forwarding", "RegisteredEventVersionValidation.cs",
         "identityExecutionBinding, identityRuntimeOptionsExecutionBinding);", "identityExecutionBinding);",
         "RegisteredWrappersUseSeparatelyAdmittedOptionsAndRefuseDefaultSubstitution"),
    ]
    rows = []
    with tempfile.TemporaryDirectory(prefix="eventstore-runtime-options-guards-") as temporary:
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
        (target / "Events/Fixtures").mkdir(parents=True)
        shutil.copyfile(ROOT / TESTS / "Events/EventRuntimeOptionsBindingTests.cs", target / "EventRuntimeOptionsBindingTests.cs")
        shutil.copyfile(ROOT / TESTS / "Events/Fixtures/EventRegistryV17.json", target / "Events/Fixtures/EventRegistryV17.json")
        (target / f"{TESTS.name}.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup><ItemGroup>'
            '<ProjectReference Include="../../src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj" />'
            '<PackageReference Include="Microsoft.NET.Test.Sdk" /><PackageReference Include="xunit.v3" />'
            '<PackageReference Include="Shouldly" /></ItemGroup><ItemGroup><Using Include="Xunit" />'
            '<None Update="Events/Fixtures/EventRegistryV17.json" CopyToOutputDirectory="PreserveNewest" />'
            '</ItemGroup></Project>\n', encoding="utf-8")
        (work / "tests/Directory.Build.props").write_text(
            f"<Project><Import Project={quoteattr(str(ROOT / 'tests/Directory.Build.props'))} /></Project>\n", encoding="utf-8")
        sources = {str(CLIENT / "Events" / name): hashlib.sha256((work / CLIENT / "Events" / name).read_bytes()).hexdigest()
                   for name in ("EventImplementationBinding.cs", "RegisteredCurrentEventDeserializer.cs", "RegisteredEventVersionValidation.cs")}
        sources[str(TESTS / "Events/EventRuntimeOptionsBindingTests.cs")] = hashlib.sha256((target / "EventRuntimeOptionsBindingTests.cs").read_bytes()).hexdigest()
        sources[str(TESTS / "Events/Fixtures/EventRegistryV17.json")] = hashlib.sha256((target / "Events/Fixtures/EventRegistryV17.json").read_bytes()).hexdigest()
        sources[str(pathlib.Path(__file__).relative_to(ROOT))] = hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()
        control = runner.execute(helper, work, output, "control", args.timeout, args.configuration, args.dependency_mode)
        if control.returncode or "Failed: 0" not in control.stdout or "Skipped: 0" not in control.stdout:
            raise RuntimeError(f"Unmutated control failed: {output / 'control-tests.log'}")
        for name, filename, before, after, killer in mutations:
            path = work / CLIENT / "Events" / filename
            original = path.read_bytes()
            try:
                helper.replace_once(path, before, after)
                result = runner.execute(helper, work, output, name, args.timeout, args.configuration, args.dependency_mode)
                lines = result.stdout.splitlines()
                failures = [index for index, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if result.returncode == 0 or not failures:
                    raise RuntimeError(f"Mutation survived or missed its named killing test: {name}")
                diagnostic = next(line.strip() for line in lines[failures[0] + 1:] if line.strip())
                rows.append({"mutation": name, "result": "killed", "killingTest": killer,
                             "observedFailure": diagnostic, "timeoutSecondsPerLane": args.timeout})
            finally:
                path.write_bytes(original)
    receipt = {"scope": "dormant exact managed-image runtime options admission",
               "configuration": args.configuration, "dependencyMode": args.dependency_mode,
               "control": "passed", "mutations": rows, "sourceSha256": sources, "catalogQualification": False,
               "immutableFrameworkExecution": False, "activationAuthority": False}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    main()
