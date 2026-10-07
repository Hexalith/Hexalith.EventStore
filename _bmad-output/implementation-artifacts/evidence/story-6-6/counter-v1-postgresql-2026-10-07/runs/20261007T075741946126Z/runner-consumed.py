#!/usr/bin/env python3
"""Run source-bound Counter logical readback in an isolated copy of the existing live fixture."""
import datetime
import hashlib
import json
import os
import random
from pathlib import Path
import shutil
import signal
import socket
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = next(parent for parent in HERE.parents if (parent / "Hexalith.EventStore.slnx").is_file())
STAMP = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
OUTPUT = HERE / "runs" / STAMP
OUTPUT.mkdir(parents=True)
RECORDS = []
OWNED = []
IMAGE = "daprio/dapr@sha256:68bb6057abbd3cc1267ad895a73415426ba77f2b451de99693aac54b45ea7d0e"
PROJECT = "Hexalith.EventStore.Server.LiveSidecar.Tests"
FIXTURE = ROOT / "tests" / PROJECT / "Fixtures/Oq8PostgresqlFixture.cs"
HELPER = '''    /// <summary>Reads one logical value through the selected host's public Dapr actor-state API.</summary>
    internal async Task<T> ReadCounterLogicalStateAsync<T>(int nodeIndex, string actorId, string key)
    {
        string path = $"{GetRunningEventStoreNode(nodeIndex).DaprHttpEndpoint}/v1.0/actors/"
            + $"{Uri.EscapeDataString(AggregateActorTypeName)}/{Uri.EscapeDataString(actorId)}/state/{Uri.EscapeDataString(key)}";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        byte[] body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<T>(body, _jsonOptions)
                ?? throw new InvalidOperationException("Dapr returned no logical value.");
        }
        finally { CryptographicOperations.ZeroMemory(body); }
    }

'''


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(command, label, env=None, timeout=120, allow_missing=False):
    record = {"command": command, "label": label, "timeoutSeconds": timeout}
    RECORDS.append(record)
    (OUTPUT / "commands.json").write_text(json.dumps(RECORDS, indent=2) + "\n")
    started = time.monotonic()
    try:
        process = subprocess.Popen(command, cwd=ROOT, env=env, stdin=subprocess.DEVNULL,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
        try:
            stdout, stderr = process.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            record["timedOut"] = True
            os.killpg(process.pid, signal.SIGTERM)
            try:
                stdout, stderr = process.communicate(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                stdout, stderr = process.communicate(timeout=10)
        record.update(exitCode=process.returncode, elapsedSeconds=round(time.monotonic() - started, 3))
        (OUTPUT / (label + ".log")).write_bytes(stdout + stderr)
        if record.get("timedOut"):
            raise TimeoutError(f"{label} exceeded its {timeout}-second limit; owned process group stopped.")
        if process.returncode:
            if allow_missing and process.returncode == 1 and b"No such container" in stderr:
                record["alreadyAbsent"] = True
            else:
                raise RuntimeError(f"{label} failed with exit {process.returncode}; see {OUTPUT / (label + '.log')}")
        return stdout.decode().strip()
    except Exception as failure:
        record["failureType"] = type(failure).__name__
        raise
    finally:
        (OUTPUT / "commands.json").write_text(json.dumps(RECORDS, indent=2) + "\n")


inputs = {
    "scope": "Local Testing PostgreSQL state, Redis pub/sub, loopback sidecars; no AD-26 production authority",
    "runtimeImage": IMAGE,
    "sourceRevision": run(["git", "rev-parse", "HEAD"], "revision"),
    "scriptSha256": digest(Path(__file__).resolve()),
    "probeSha256": digest(HERE / "CounterPostgresqlLogicalReadbackProbe.cs"),
    "sourceHashes": {},
}
(OUTPUT / "runner-consumed.py").write_bytes(Path(__file__).resolve().read_bytes())
for relative in ["samples/Hexalith.EventStore.Sample/Program.cs",
                 "samples/Hexalith.EventStore.Sample/Counter/CounterEventSerialization.cs",
                 "deploy/dapr/statestore-postgresql.yaml", "deploy/dapr/resiliency.yaml",
                 "tests/" + PROJECT + "/Fixtures/Oq8DiscoveryConfiguration.cs",
                 "tests/" + PROJECT + "/Fixtures/Oq8PostgresqlFixture.cs"]:
    inputs["sourceHashes"][relative] = digest(ROOT / relative)
daprd = Path.home() / ".dapr/bin/daprd"
inputs["daprdSha256"] = digest(daprd)
inputs["daprdVersion"] = run([str(daprd), "--version"], "daprd-version")
(OUTPUT / "inputs.json").write_text(json.dumps(inputs, indent=2) + "\n")
print(f"Capture: {OUTPUT}", flush=True)

try:
    with tempfile.TemporaryDirectory(prefix="story66-counter-postgresql-") as scratch:
        workspace = Path(scratch)
        for name in ["src", "samples", "references", "deploy", "Hexalith.EventStore.slnx",
                     "global.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", ".editorconfig"]:
            (workspace / name).symlink_to(ROOT / name)
        (workspace / "tests").mkdir()
        (workspace / "tests/Directory.Build.props").symlink_to(ROOT / "tests/Directory.Build.props")
        project = workspace / "tests" / PROJECT
        shutil.copytree(ROOT / "tests" / PROJECT, project,
                        ignore=shutil.ignore_patterns("bin", "obj", "TestResults"))
        project_file = project / (PROJECT + ".csproj")
        project_xml = ET.parse(project_file)
        for reference in project_xml.findall(".//ProjectReference"):
            included = reference.get("Include")
            if included and not included.startswith("$("):
                reference.set("Include", str((ROOT / "tests" / PROJECT / included.replace("\\", "/")).resolve()))
        project_xml.write(project_file, encoding="unicode")
        original = FIXTURE.read_text()
        marker = "    /// <summary>Sets the shared deterministic clock used by both EventStore processes.</summary>"
        if original.count(marker) != 1:
            raise RuntimeError("The fixture readback insertion boundary changed.")
        instrumented = original.replace(marker, HELPER + marker)
        root_marker = "            RegisterSensitive(_repositoryRoot);"
        if instrumented.count(root_marker) != 1:
            raise RuntimeError("The fixture private-path registration boundary changed.")
        instrumented = instrumented.replace(root_marker, root_marker + "\n            RegisterSensitive("
            + json.dumps(str(ROOT)) + ");")
        runtime_marker = '            _runtimeDirectory = Path.Combine(Path.GetTempPath(), $"eventstore-oq8-runtime-{Guid.NewGuid():N}");'
        if instrumented.count(runtime_marker) != 1:
            raise RuntimeError("The fixture-owned runtime directory boundary changed.")
        runtime_directory = workspace / "counter-probe-runtime"
        instrumented = instrumented.replace(runtime_marker, "            _runtimeDirectory = "
            + json.dumps(str(runtime_directory)) + ";")
        (project / "Fixtures/Oq8PostgresqlFixture.cs").write_text(instrumented)
        (OUTPUT / "instrumented-fixture.cs").write_text(instrumented)
        discovery = project / "Fixtures/Oq8DiscoveryConfiguration.cs"
        discovery_source = discovery.read_text()
        config_marker = "            spec:\n"
        if discovery_source.count(config_marker) != 1:
            raise RuntimeError("The fixture-private discovery configuration boundary changed.")
        discovery.write_text(discovery_source.replace(config_marker, config_marker
            + "              features:\n                - name: HotReload\n                  enabled: false\n"))
        inputs["testProcessHotReload"] = False
        inputs["instrumentedDiscoverySha256"] = digest(discovery)
        (OUTPUT / "instrumented-discovery.cs").write_text(discovery.read_text())
        inputs["instrumentedFixtureSha256"] = digest(project / "Fixtures/Oq8PostgresqlFixture.cs")
        (OUTPUT / "inputs.json").write_text(json.dumps(inputs, indent=2) + "\n")
        shutil.copyfile(HERE / "CounterPostgresqlLogicalReadbackProbe.cs", project / "Events/CounterPostgresqlLogicalReadbackProbe.cs")
        run(["dotnet", "build", str(project_file), "--configuration", "Debug", "-m:1",
             "-warnaserror", "-p:UseHexalithProjectReferences=true",
             "-p:HexalithEventStoreRoot=" + str(ROOT),
             "-p:HexalithCommonsRoot=" + str(ROOT / "references/Hexalith.Commons"),
             "-p:HexalithTenantsBasePath=" + str(ROOT / "references/Hexalith.Tenants/src")], "build", timeout=180)
        for role, port, entry in [("placement", 50005, "./placement"), ("scheduler", 50006, "./scheduler")]:
            name = "g6-oq8-story66-" + role + "-" + STAMP.lower()
            with socket.socket() as listener:
                for attempt in range(20):
                    try:
                        listener.bind(("127.0.0.1", random.randint(18000, 30000)))
                        break
                    except OSError:
                        if attempt == 19:
                            raise
                published_port = listener.getsockname()[1]
            command = ["docker", "run", "--rm", "-d", "--name", name, "--entrypoint", entry,
                       "-p", f"127.0.0.1:{published_port}:{port}", IMAGE]
            if role == "scheduler":
                command += ["--etcd-data-dir=/tmp/story66-scheduler", "--etcd-client-listen-address=0.0.0.0",
                            f"--override-broadcast-host-port=127.0.0.1:{published_port}"]
            OWNED.append((role, name, name))
            identity = run(command, role + "-start")
            OWNED[-1] = (role, name, identity)
        env = os.environ.copy()
        env.update(HEXALITH_OQ8_DAPRD_PATH=str(daprd), HEXALITH_OQ8_CONFIGURATION="Debug",
                   HEXALITH_OQ8_PLACEMENT_CONTAINER=OWNED[0][1], HEXALITH_OQ8_SCHEDULER_CONTAINER=OWNED[1][1],
                   HEXALITH_OQ8_REDIS_ENDPOINT="127.0.0.1:6379", HEXALITH_OQ8_NAMESPACE="g6-oq8-story66-" + STAMP.lower(),
                   HEXALITH_OQ8_EVIDENCE_DIRECTORY=str(OUTPUT / "fixture-evidence"),
                   HEXALITH_OQ8_CLEANUP_PATH=str(OUTPUT / "fixture-cleanup.json"))
        assembly = project / "bin/Debug/net10.0" / (PROJECT + ".dll")
        images = {str(path.relative_to(workspace)): digest(path) for path in assembly.parent.glob("*.dll")}
        runtime_images = {}
        for directory in [ROOT / "src/Hexalith.EventStore/bin/Debug/net10.0",
                          ROOT / "samples/Hexalith.EventStore.Sample/bin/Debug/net10.0"]:
            runtime_images.update({str(path.relative_to(ROOT)): digest(path) for path in directory.iterdir() if path.is_file()})
        inputs["managedImagesBeforeRun"] = images
        inputs["runtimeImagesBeforeRun"] = runtime_images
        (OUTPUT / "inputs.json").write_text(json.dumps(inputs, indent=2) + "\n")
        try:
            run(["dotnet", str(assembly), "-noLogo", "-class",
                 "Hexalith.EventStore.Server.LiveSidecar.Tests.Events.CounterPostgresqlLogicalReadbackProbe",
                 "-result-xml", str(OUTPUT / "test.xml")], "logical-readback", env=env, timeout=240)
        finally:
            identity_file = runtime_directory / "postgresql.cid"
            if identity_file.is_file():
                identity = identity_file.read_text().strip()
                if len(identity) != 64 or any(character not in "0123456789abcdef" for character in identity):
                    raise RuntimeError("The fixture-owned PostgreSQL container identity is invalid.")
                run(["docker", "rm", "-f", identity], "fixture-postgresql-cleanup", allow_missing=True)
        results = ET.parse(OUTPUT / "test.xml").findall(".//assembly")
        if len(results) != 1 or any(results[0].get(key) != value for key, value in
                                   {"total": "1", "passed": "1", "failed": "0", "skipped": "0", "errors": "0"}.items()):
            raise RuntimeError("The logical readback result did not execute exactly one passing test.")
        if images != {str(path.relative_to(workspace)): digest(path) for path in assembly.parent.glob("*.dll")}:
            raise RuntimeError("The built managed images changed during execution.")
        if any(digest(ROOT / path) != sha for path, sha in runtime_images.items()):
            raise RuntimeError("A real host runtime input changed during execution.")
        inputs["managedImagesUnchanged"] = True
        inputs["sourceUnchanged"] = all(digest(ROOT / path) == sha for path, sha in inputs["sourceHashes"].items())
        if not inputs["sourceUnchanged"]:
            raise RuntimeError("An original source input changed during the probe.")
        (OUTPUT / "inputs.json").write_text(json.dumps(inputs, indent=2) + "\n")
        print("Logical readback passed; original source and managed images stayed unchanged.", flush=True)
finally:
    cleanup_failures = []
    for role, name, identity in reversed(OWNED):
        try:
            run(["docker", "rm", "-f", identity], role + "-cleanup", allow_missing=True)
        except Exception as failure:
            cleanup_failures.append({"role": role, "failureType": type(failure).__name__})
    (OUTPUT / "owned-resources.json").write_text(json.dumps(OWNED, indent=2) + "\n")
    if cleanup_failures:
        (OUTPUT / "cleanup-failures.json").write_text(json.dumps(cleanup_failures, indent=2) + "\n")
        raise RuntimeError("Owned control-plane cleanup failed; inspect the captured commands and cleanup failures.")
    print("Owned control-plane containers removed.", flush=True)
