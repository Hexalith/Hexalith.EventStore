"""Rebuild exact local scratch controls and remove only the producer's scratch guard.

Temporary compilations use the repository's pinned build/package configuration,
actual Contracts project and isolated copies. No production source is edited.
This demonstrates one local guard's coverage, not application serializer readiness.
"""

from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
RUN = HERE / "runs" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
CLASS = "Hexalith.EventStore.DomainService.Tests.BoundedV1DomainResultProducerScratchTests"
METHOD = "SerializedSourceScratchBoundaryPrecedesFirstSerializer"
PRODUCER = "src/Hexalith.EventStore.DomainService/BoundedV1DomainResultProducer.cs"
SOURCES = [
    PRODUCER,
    "src/Hexalith.EventStore.DomainService/BoundedV1EventSerialization.cs",
    "src/Hexalith.EventStore.DomainService/BoundedV1PayloadStream.cs",
    "src/Hexalith.EventStore.DomainService/BoundedV1WireResultAdmission.cs",
    "tests/Hexalith.EventStore.DomainService.Tests/BoundedProducerTestEvent.cs",
    "tests/Hexalith.EventStore.DomainService.Tests/BoundedProducerSerializedTestEvent.cs",
    "tests/Hexalith.EventStore.DomainService.Tests/BoundedV1DomainResultProducerScratchTests.cs",
]
GUARD = " || scratch > MaximumScratchBytes"


def require(condition, message):
    """Keep verification and its subprocess calls active under Python optimization."""
    if not condition:
        raise RuntimeError(message)


def run(command, label):
    """Retain each exact command, timeout, exit and complete output."""
    record = {"command": command, "exit_code": None, "timeout_seconds": 60, "status": "started"}
    path = RUN / (label + ".json")
    path.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    try:
        with (RUN / (label + ".log")).open("w", encoding="utf-8") as log:
            result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=60)
        record.update({"exit_code": result.returncode, "status": "completed"})
    except (subprocess.TimeoutExpired, OSError) as error:
        record.update({"status": "failed", "exception_type": type(error).__name__, "detail": str(error)})
        raise
    finally:
        path.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    return result.returncode


def image_hashes(folder):
    """Pin every managed image and runtime descriptor consumed by the isolated process."""
    return {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
            for path in sorted(folder.iterdir())
            if path.suffix == ".dll" or path.name.endswith((".deps.json", ".runtimeconfig.json"))}


def cases(path):
    """Require both real theory cases, exact class/method and no assembly errors."""
    tree = ET.parse(path)
    assemblies = tree.findall("assembly")
    require(len(assemblies) == 1 and assemblies[0].get("errors") == "0", "test assembly error")
    tests = tree.findall(".//test")
    require(len(tests) == 2 and all(t.get("type") == CLASS and t.get("method") == METHOD for t in tests),
            "missing or substituted test class/method")
    names = {t.get("name"): t.get("result") for t in tests}
    expected = {f"{CLASS}.{METHOD}(serializedCount: 42, admitted: True)",
                f"{CLASS}.{METHOD}(serializedCount: 43, admitted: False)"}
    require(set(names) == expected, "missing or substituted theory case")
    return names, tests


def main():
    RUN.mkdir(parents=True, exist_ok=False)
    source_bytes = {name: (ROOT / name).read_bytes() for name in SOURCES}
    producer = source_bytes[PRODUCER].decode("utf-8")
    require(producer.count(GUARD) == 1, "mutation must have one exact owning target")
    require("if (encoded > MaximumResultBytes" + GUARD + ")" in producer, "owning admission guard changed")
    summary = {"scope": "isolated local scratch-guard control; no readiness or parent completion",
               "mutation": "remove only complete-result scratch refusal; retain encoded-size guard",
               "source_sha256": {name: hashlib.sha256(raw).hexdigest() for name, raw in source_bytes.items()},
               "verifier_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
               "invocation": {"executable": sys.executable, "argv": sys.orig_argv,
                              "optimization": sys.flags.optimize}}
    configuration_paths = ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
                           "tests/Directory.Build.props", ".editorconfig", "global.json",
                           "references/Hexalith.Builds/Hexalith.Build.props",
                           "references/Hexalith.Builds/Props/Directory.Packages.props"]
    configuration = {name: (ROOT / name).read_bytes() for name in configuration_paths}
    summary["configuration_sha256"] = {name: hashlib.sha256(raw).hexdigest() for name, raw in configuration.items()}
    with tempfile.TemporaryDirectory(prefix="eventstore-v1-scratch-") as temporary:
        folder = Path(temporary)
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        ET.SubElement(project, "Import", Project=str(ROOT / "tests/Directory.Build.props"))
        properties = ET.SubElement(project, "PropertyGroup")
        for key, value in {"OutputType": "Exe", "IsTestProject": "true", "IsPackable": "false",
                           "EnableDefaultCompileItems": "false"}.items():
            ET.SubElement(properties, key).text = value
        items = ET.SubElement(project, "ItemGroup")
        ET.SubElement(items, "ProjectReference", Include=str(ROOT / "src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj"))
        ET.SubElement(items, "PackageReference", Include="xunit.v3")
        ET.SubElement(items, "PackageReference", Include="Shouldly")
        ET.SubElement(items, "Using", Include="Xunit")
        for name, raw in source_bytes.items():
            target = folder / Path(name).name
            target.write_bytes(raw)
            ET.SubElement(items, "Compile", Include=str(target))
        project_path = folder / "ScratchAdmissionControl.csproj"
        ET.SubElement(project, "Import", Project=str(ROOT / "Directory.Build.targets"))
        ET.ElementTree(project).write(project_path, encoding="unicode")
        (folder / ".editorconfig").write_bytes(configuration[".editorconfig"])
        packages = ET.Element("Project")
        ET.SubElement(packages, "Import", Project=str(ROOT / "Directory.Packages.props"))
        ET.ElementTree(packages).write(folder / "Directory.Packages.props", encoding="unicode")
        build = ["dotnet", "build", str(project_path), "--configuration", "Debug", "-m:1",
                 "-warnaserror", "-p:UseHexalithProjectReferences=true"]
        assembly = folder / "bin/Debug/net10.0/ScratchAdmissionControl.dll"
        require(run(build, "control-build") == 0, "isolated control build failed")
        control_images = image_hashes(assembly.parent)
        control_command = ["dotnet", str(assembly), "-noLogo", "-class", CLASS,
                           "-result-xml", str(RUN / "control-tests.xml")]
        require(run(control_command, "control-tests") == 0, "unmodified isolated controls failed")
        control, _ = cases(RUN / "control-tests.xml")
        require(all(result == "Pass" for result in control.values()), "unmodified theory cases did not pass")
        require(image_hashes(assembly.parent) == control_images, "control images changed during execution")
        mutated = producer.replace(GUARD, "")
        (folder / Path(PRODUCER).name).write_text(mutated, encoding="utf-8")
        require(run(build, "mutant-build") == 0, "mutation must compile")
        mutant_images = image_hashes(assembly.parent)
        dependencies = lambda images: {name: sha for name, sha in images.items() if name != assembly.name}
        require(dependencies(control_images) == dependencies(mutant_images), "consumed dependency images changed between builds")
        mutant_command = ["dotnet", str(assembly), "-noLogo", "-class", CLASS,
                          "-result-xml", str(RUN / "mutant-tests.xml")]
        require(run(mutant_command, "mutant-tests") == 1, "mutation was not rejected as a test failure")
        require(image_hashes(assembly.parent) == mutant_images, "mutant images changed during execution")
        mutant, tests = cases(RUN / "mutant-tests.xml")
        require(mutant[f"{CLASS}.{METHOD}(serializedCount: 42, admitted: True)"] == "Pass",
                "mutation must retain the admitted boundary control")
        refused = f"{CLASS}.{METHOD}(serializedCount: 43, admitted: False)"
        require(mutant[refused] == "Fail", "mutation must die in the 43-event refusal control")
        failure = next(test for test in tests if test.get("name") == refused).find("failure")
        require(failure is not None and failure.get("exception-type") == "Shouldly.ShouldAssertException",
                "unexpected failure type")
        message = failure.findtext("message", "")
        require("InvalidOperationException" in message and "did not" in message, "unexpected owning failure")
        summary.update({"control_cases": control, "mutant_cases": mutant,
                        "control_images_sha256": control_images, "mutant_images_sha256": mutant_images,
                        "owning_failure": message,
                        "mutant_producer_sha256": hashlib.sha256(mutated.encode()).hexdigest()})
    require(all((ROOT / name).read_bytes() == raw for name, raw in source_bytes.items()), "source changed during verification")
    require(all((ROOT / name).read_bytes() == raw for name, raw in configuration.items()), "consumed configuration changed during verification")
    summary.update({"result": "passed", "production_source_edited": False,
                    "temporary_project_removed": True})
    (RUN / "mutation-result.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print("Isolated controls: 2 passed; scratch-guard mutation: 42 passes, 43 fails in its owning assertion.")
    print("Preserved run:", RUN.relative_to(ROOT))


if __name__ == "__main__":
    main()
