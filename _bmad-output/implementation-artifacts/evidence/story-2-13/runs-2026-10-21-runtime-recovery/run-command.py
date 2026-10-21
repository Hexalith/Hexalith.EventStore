#!/usr/bin/env python3
"""Retain local commands and exact qualification inputs, without asserting owner authority."""
import datetime
import fnmatch
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

packet = Path(__file__).resolve().parent
repository = next(path for path in packet.parents if (path / "Hexalith.EventStore.slnx").exists())
label, *command = sys.argv[1:]
if command and command[0] == "--":
    command.pop(0)
assert re.fullmatch(r"[a-z0-9-]+", label) and command
receipt_path = packet / f"{label}.json"
assert not receipt_path.exists(), "Receipts are immutable; use a distinct label for a retry."
fixture = repository / "tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionFixture.cs"
sources = sorted(set(re.findall(r'"((?:src|tests|samples|deploy|_bmad-output|references)/[^"\n]+)"', fixture.read_text())))
sources += ["global.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "src/Hexalith.EventStore.Server/Actors/ETagActor.cs", "src/Hexalith.EventStore.Server/Actors/IETagActor.cs", "tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj", "src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj", ".editorconfig", ".gitattributes"]
class_filters = [command[index + 1] for index, value in enumerate(command[:-1]) if value == "-class"]
if class_filters:
    tracked_tests = subprocess.check_output(["rg", "--files", "tests"], cwd=repository, text=True).splitlines()
    sources += [relative for relative in tracked_tests if relative.endswith(".cs")
                and any(fnmatch.fnmatchcase(Path(relative).name.split(".")[0], pattern) for pattern in class_filters)]
sources = sorted(set(sources))
# Component evidence lists also contain binary paths; retain their identities separately, never copy build output as source.
sources = [relative for relative in sources if "/bin/" not in relative]
snapshot = packet / "snapshots" / label
retained_source_paths = {relative: relative + ".source" if Path(relative).suffix in (".props", ".targets", ".csproj", ".slnx") else relative for relative in sources}
for relative in sources:
    target = snapshot / retained_source_paths[relative]
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(repository / relative, target)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def identities():
    return {relative: digest(repository / relative) for relative in sources}

binaries = [
    "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.dll",
    "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.deps.json",
    "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.Gateway.dll",
    "tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests.dll",
]
for argument in command:
    path = (repository / argument).resolve()
    if path.is_relative_to(repository) and path.is_file() and path.suffix in (".dll", ".exe"):
        binaries.append(str(path.relative_to(repository)))
        for suffix in (".deps.json", ".runtimeconfig.json"):
            companion = path.with_suffix(suffix)
            if companion.exists():
                binaries.append(str(companion.relative_to(repository)))
        binaries += [str(dependency.relative_to(repository)) for dependency in path.parent.glob("Hexalith.EventStore*.dll")]
binaries += [str(dependency) for dependency in Path("src/Hexalith.EventStore/bin/Debug/net10.0").glob("*.dll") if dependency.name.startswith(("Hexalith.", "Dapr."))]
binaries = sorted(set(binaries))
def binary_identities():
    return {relative: digest(repository / relative) for relative in binaries if (repository / relative).exists()}

runtime_path = os.environ.get("HEXALITH_STORY_2_13_DAPRD_PATH", str(Path.home() / ".dapr/bin/daprd"))
for argument in command:
    if argument.startswith("HEXALITH_STORY_2_13_DAPRD_PATH="):
        runtime_path = argument.split("=", 1)[1]
runtime = Path(runtime_path)
acquisition = json.loads((packet / "runtime-acquisition.json").read_text()) if (packet / "runtime-acquisition.json").exists() else None
external = {}
if acquisition:
    work = Path(acquisition["workDirectory"])
    source = work / "dapr-b32a815fa2260983e181b95272ea555fe17d3895"
    external_paths = [source / "go.mod", source / "go.sum", work / "go/bin/go"] + sorted((source / "pkg/actors/router").glob("*.go")) + sorted((source / "pkg/api/grpc/manager").glob("*.go"))
    external = {str(path): digest(path) for path in external_paths if path.exists()}
    for original in external:
        target = snapshot / "external" / (Path(original).name + ".source")
        target.parent.mkdir(parents=True, exist_ok=True)
        if Path(original).name != "go":
            shutil.copyfile(original, target)
inotify_limit_before = Path("/proc/sys/fs/inotify/max_user_watches").read_text().strip()
runtime_before = digest(runtime) if runtime.exists() else None
before = identities()
binary_before = binary_identities()
revision_before = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip()
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
environment = os.environ.copy()
environment["HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY"] = str(packet / "scenarios")
result = subprocess.run(command, cwd=repository, env=environment, capture_output=True, text=True)
ended = datetime.datetime.now(datetime.timezone.utc).isoformat()
output = result.stdout + result.stderr
output = re.sub(r"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b", "[redacted-jwt]", output)
output = re.sub(r"(?i)([?&]t=|x-otlp-api-key=)[^\s&\"']+", r"\1[redacted]", output)
log = packet / f"{label}.log"
log.write_text(output)
receipt = {
    "schemaVersion": 1, "label": label, "command": command,
    "inotifyLimitBefore": inotify_limit_before, "inotifyLimitAfter": Path("/proc/sys/fs/inotify/max_user_watches").read_text().strip(),
    "fixtureRuntimePath": runtime_path, "fixtureRuntimeSha256Before": runtime_before,
    "fixtureRuntimeSha256After": digest(runtime) if runtime.exists() else None,
    "externalInputsBefore": external,
    "externalInputsAfter": {path: digest(Path(path)) for path in external},
    "startedUtc": started, "completedUtc": ended, "exitCode": result.returncode,
    "runStartRevision": revision_before,
    "revisionAfter": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip(),
    "sourcesBefore": before, "sourcesAfter": identities(),
    "binariesBefore": binary_before, "binariesAfter": binary_identities(),
    "outputSha256": digest(log), "snapshot": str(snapshot.relative_to(packet)),
    "retainedSourcePaths": retained_source_paths,
    "authority": "local qualification only; no owner approval, CI seal or production authority",
}
receipt_path.write_text(json.dumps(receipt, indent=2) + "\n")
print(json.dumps({"label": label, "exitCode": result.returncode, "startedUtc": started, "completedUtc": ended}))
print(output[-3500:])
sys.exit(result.returncode)
