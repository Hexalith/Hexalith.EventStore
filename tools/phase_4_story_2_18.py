"""Live conditional-query evidence for the exact Story 2.18 supporting slice."""

from contextlib import contextmanager
import json
import os
from pathlib import Path
import re
import shutil
import socket
import stat
import sys
import tempfile
import time
import xml.etree.ElementTree as ET

from dapr_runtime_qualification import CANDIDATE_IDENTITIES, CANDIDATE_PROFILE, REDIS_IMAGE
import phase_4_story_5_3 as Source
import phase_4_transition as C

ADAPTER = "story-2-18-conditional-query-v1"
SCHEMA = "hexalith.eventstore.story-2-18-evidence.v1"
STORY_KEY = "2-18-bind-conditional-queries-to-the-returned-representation"
SPEC = "_bmad-output/implementation-artifacts/spec-" + STORY_KEY + ".md"
CONTROL_FILES = (
    "tools/phase_4_story_2_18.py", "tools/phase_4_story_5_3.py",
    "tools/dapr_runtime_qualification.py",
)
BUILDS = "references/Hexalith.Builds"
PROJECT_NAME = "Hexalith.EventStore.Server.LiveSidecar.Tests"
PROJECT = f"tests/{PROJECT_NAME}/{PROJECT_NAME}.csproj"
ASSEMBLY = f"tests/{PROJECT_NAME}/bin/Release/net11.0/{PROJECT_NAME}.dll"
TEST_SOURCE = f"tests/{PROJECT_NAME}/Integration/NativeActorCatalogLiveTests.cs"
TEST_TYPE = PROJECT_NAME + ".Integration.NativeActorCatalogLiveTests"
METHOD = "NativeQueryRegisteredActorsAndLifecycleKeepActualChannelCatalogAndMethodBoundaries"
SELECTOR = TEST_TYPE + "." + METHOD
CASES = (
    ("in-memory-events", False, False), ("in-memory-events", False, True),
    ("in-memory-events", True, False), ("in-memory-events", True, True),
    ("pubsub", False, False), ("pubsub", False, True),
)
RUNTIME = "1.19.1"
DOCKER_OWNER_LABEL = "hexalith.eventstore.story-2-18-test-owner"


def supported(claim):
    return (claim["story_id"] == "2.18" and claim["story_key"] == STORY_KEY
            and claim["spec_path"] == SPEC and claim["scope"] == "slice"
            and claim["target"] in ("spec", "tracker") and claim["nfr_ids"] == ["NFR8", "NFR12"]
            and claim["high_risk_nfr_ids"] == ["NFR12"])


def subject_at(api, commit, allow_absent=False):
    return subject_digest(Source.authenticated_tree(api, commit, allow_absent))


def subject_digest(tree):
    return C.digest(C.canonical({"adapter": ADAPTER, "root_tree": tree}))


def assert_head(root, path, expected):
    C.require(Source.git(root / path, "rev-parse", "HEAD").decode().strip() == expected,
              "SOURCE_MISMATCH", "checkout revision differs from the bound revision: " + (path or "EventStore"))


def inventory(control):
    root, head = control.root, control.head
    assert_head(root, "", head)
    tree = Source.authenticated_tree(control.api, head)
    entries = Source.authenticated_entries(root, control.api, tree)
    required = {*CONTROL_FILES, SPEC, PROJECT, TEST_SOURCE, CANDIDATE_PROFILE, ".gitmodules", "global.json", "nuget.config",
                "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"}
    C.require(required <= {row["path"] for row in entries if row["mode"] != "160000"},
              "SOURCE_MISMATCH", "bound build or evidence source is absent")
    entries = Source.bind_entries(root, entries)
    submodules = Source.submodule_entries(root, entries, True, paths=(BUILDS,))
    assert_head(root, BUILDS, submodules[0]["commit"])
    return {"head": head, "root_tree": tree, "entries": entries, "submodules": submodules,
            "metadata": Source.metadata_inventory(root, head, submodules, tree, publication_history=False)}


def assert_stable(root, manifest):
    assert_head(root, "", manifest["head"])
    assert_head(root, BUILDS, manifest["submodules"][0]["commit"])
    Source.assert_stable(root, manifest)


def execute_bounded(work, command, step, timeout, environment=None):
    try:
        return C.Control.bounded_process(command, work, step + ".log", timeout_seconds=timeout,
                                         environment_extra=environment,
                                         failure_code="STORY_EVIDENCE_FAILED", limit_code="STORY_EVIDENCE_LIMIT")
    except C.Refusal as error:
        raise C.Refusal(error.code, "Story 2.18 " + step + ": " + str(error)) from error


def require_executable(path, name):
    C.require(path is not None and path.is_absolute() and path.is_file() and os.access(path, os.X_OK),
              "STORY_EVIDENCE_FAILED", "Story 2.18 prerequisite unavailable: " + name)
    return path.resolve()


def dotnet_executable():
    installed = os.environ.get("DOTNET_ROOT")
    if installed:
        return require_executable(Path(installed) / "dotnet", "dotnet SDK")
    lookup = os.pathsep.join((str(Path.home() / ".dotnet"), "/usr/local/bin", os.defpath))
    found = shutil.which("dotnet", path=lookup)
    return require_executable(Path(found) if found else None, "dotnet SDK")


def runtime_inputs(work):
    profile = json.loads((work / CANDIDATE_PROFILE).read_bytes())
    runtime = profile["runtime"]
    images = {name: profile["images"][name]["reference"] for name in ("placement", "scheduler")}
    C.require(runtime["version"] == RUNTIME and runtime["binarySha256"] == CANDIDATE_IDENTITIES["binarySha256"]
              and all(reference == CANDIDATE_IDENTITIES["images"][name] for name, reference in images.items()),
              "STORY_EVIDENCE_FAILED", "Story 2.18 requires the published Dapr " + RUNTIME + " runtime identities")
    installed = os.environ.get("QUALIFICATION_DAPRD") or os.environ.get("EVENTSTORE_ROUTE_CATALOG_DAPRD")
    path = require_executable(Path(installed) if installed else Path.home() / ".dapr/bin/daprd", "daprd " + RUNTIME)
    selected = work / ".story-2-18-daprd"
    C.require(path.stat().st_size <= 512 * C.MAX_BYTES, "STORY_EVIDENCE_LIMIT", "daprd exceeds size limit")
    shutil.copyfile(path, selected)
    C.require(Source.file_digest(selected) == runtime["binarySha256"],
              "STORY_EVIDENCE_FAILED", "Story 2.18 daprd differs from the pinned runtime")
    selected.chmod(0o700)
    images["redis"] = REDIS_IMAGE
    return selected, {"version": RUNTIME, "daprd_sha256": runtime["binarySha256"], "images": images}


def loopback_port():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def wait_for_control_ports(ports):
    deadline = time.monotonic() + 30
    for port in ports:
        while True:
            try:
                with socket.create_connection(("127.0.0.1", port), timeout=0.5):
                    break
            except OSError as error:
                if time.monotonic() >= deadline:
                    raise C.Refusal("STORY_EVIDENCE_FAILED", "Story 2.18 private Dapr control plane is unavailable") from error
                time.sleep(0.1)


def write_fixture_docker(work, docker, host, owner):
    """Label Redis started by the native test so forced shutdown can reap it."""
    directory = work / ".story-2-18-tools"
    directory.mkdir()
    wrapper = directory / "docker"
    wrapper.write_text(
        f"#!{sys.executable} -I\n"
        "import os, sys\n"
        "args = sys.argv[1:]\n"
        f"if args[:2] != ['--host', {host!r}] or len(args) < 3:\n"
        "    raise SystemExit('Story 2.18 fixture refuses a different Docker endpoint')\n"
        "if args[2] == 'run':\n"
        f"    args[3:3] = ['--label', {DOCKER_OWNER_LABEL + '=' + owner!r}]\n"
        f"os.execv({str(docker)!r}, [{str(docker)!r}, *args])\n",
        encoding="utf-8",
    )
    wrapper.chmod(0o700)
    return directory


@contextmanager
def native_topology(work, runtime):
    docker = require_executable(Path("/usr/bin/docker"), "Docker")
    endpoint = Path("/var/run/docker.sock").resolve()
    C.require(endpoint.exists() and stat.S_ISSOCK(endpoint.stat().st_mode), "STORY_EVIDENCE_FAILED",
              "Story 2.18 prerequisite unavailable: local Docker socket")
    docker_host = "unix://" + str(endpoint)
    socket_identity = (endpoint.stat().st_dev, endpoint.stat().st_ino)
    environment = {"DOCKER_HOST": docker_host}

    def run(argv, step, timeout=30):
        current = endpoint.stat()
        C.require((current.st_dev, current.st_ino) == socket_identity, "STORY_EVIDENCE_FAILED",
                  "Story 2.18 Docker socket changed")
        return execute_bounded(work, [str(docker), "--host", docker_host, *argv[1:]], step, timeout, environment)

    images = runtime["images"]
    run(["docker", "image", "inspect", "--format", "{{.Id}}", *images.values()], "native-images")
    owner = os.urandom(16).hex()
    ports = {}
    try:
        for name, internal_port in (("placement", 50005), ("scheduler", 50006)):
            port = loopback_port()
            arguments = ["--port", str(internal_port)]
            if name == "scheduler":
                arguments.extend(["--etcd-data-dir=/tmp/etcd", "--etcd-client-listen-address=0.0.0.0",
                                  f"--override-broadcast-host-port=127.0.0.1:{port}"])
            identifier = run(["docker", "run", "--detach", "--rm", "--pull=never",
                              "--name", f"story-2-18-{owner}-{name}", "--label", DOCKER_OWNER_LABEL + "=" + owner,
                              "--publish", f"127.0.0.1:{port}:{internal_port}", "--tmpfs", "/tmp:rw",
                              "--entrypoint", "/" + name, images[name], *arguments], name + "-start").decode().strip()
            C.require(C.sha(identifier, 64), "STORY_EVIDENCE_FAILED", "invalid private Dapr container identity")
            ports["EVENTSTORE_TEST_" + name.upper() + "_PORT"] = str(port)
        wait_for_control_ports([int(port) for port in ports.values()])
        path = write_fixture_docker(work, docker, docker_host, owner)
        yield {**environment, **ports, "PATH": os.pathsep.join((str(path), os.defpath))}
    finally:
        Source.cleanup_owned_containers(owner, run, DOCKER_OWNER_LABEL)


def passing_cases(xml, assembly):
    try:
        C.require(xml.is_file() and not xml.is_symlink(), "STORY_EVIDENCE_FAILED", "Story 2.18 xUnit report is absent")
        with xml.open("rb") as stream:
            content = stream.read(C.MAX_BYTES + 1)
        C.require(len(content) <= C.MAX_BYTES, "STORY_EVIDENCE_LIMIT", "Story 2.18 xUnit report exceeds limit")
        root = ET.fromstring(content)
        assemblies = root.findall("assembly")
        expected_counts = {"total": "6", "passed": "6", "failed": "0", "skipped": "0", "not-run": "0"}
        C.require(root.tag == "assemblies" and len(assemblies) == 1, "STORY_EVIDENCE_FAILED",
                  "Story 2.18 requires one xUnit assembly")
        report = assemblies[0]
        C.require(report.get("name") == str(assembly) and report.get("errors") == "0"
                  and all(report.get(name) == count for name, count in expected_counts.items()),
                  "STORY_EVIDENCE_FAILED", "Story 2.18 requires all six live cases to pass with no skips or errors")
        expected = {f'{SELECTOR}(component: \\"{component}\\", declarative: {declarative}, legacyFinite: {legacy})'
                    for component, declarative, legacy in CASES}
        collections = report.findall("collection")
        tests = report.findall(".//test")
        C.require(len(collections) == 1 and len(tests) == len(expected)
                  and tests == collections[0].findall("test") and not root.findall(".//error")
                  and all(test.get("type") == TEST_TYPE and test.get("method") == METHOD and test.get("result") == "Pass"
                          and test.find("failure") is None and test.find("reason") is None for test in tests)
                  and {test.get("name") for test in tests} == expected,
                  "STORY_EVIDENCE_FAILED", "Story 2.18 xUnit case membership differs from the fixed theory")
    except (ET.ParseError, OSError) as error:
        raise C.Refusal("STORY_EVIDENCE_FAILED", "Story 2.18 cannot read its fresh xUnit report") from error
    return [{"component": component, "declarative": declarative, "legacy_finite": legacy, "result": "PASS"}
            for component, declarative, legacy in CASES]


def run(root, manifest):
    with tempfile.TemporaryDirectory(prefix="story-2-18-") as directory:
        work = Path(directory)
        Source.materialize_sources(root, work, manifest)
        daprd, runtime = runtime_inputs(work)
        dotnet = dotnet_executable()
        packages = work / ".story-2-18-packages"
        environment = {"DOTNET_ROOT": str(dotnet.parent), "DOTNET_CLI_HOME": str(work),
                       "DOTNET_NOLOGO": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
                       "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE": "true", "NUGET_PACKAGES": str(packages),
                       "MSBUILDDISABLENODEREUSE": "1", "GIT_CONFIG_NOSYSTEM": "1", "GIT_CONFIG_GLOBAL": "/dev/null",
                       "GIT_NO_REPLACE_OBJECTS": "1"}
        sdk = json.loads((work / "global.json").read_bytes())["sdk"]["version"]
        version = execute_bounded(work, [str(dotnet), "--version"], "sdk-version", 30, environment).decode().strip()
        C.require(version == sdk, "STORY_EVIDENCE_FAILED", "Story 2.18 dotnet SDK differs from global.json")
        build_properties = ["-p:UseNuGetDeps=true", "-p:UseHexalithProjectReferences=false",
                            "-p:RestoreFallbackFolders=", "-p:MinVerVersionOverride=1.0.0",
                            "-p:SourceRevisionId=" + manifest["head"]]
        execute_bounded(work, [str(dotnet), "restore", PROJECT, "--configfile", "nuget.config",
                               "-p:Configuration=Release", "-warnaserror", *build_properties], "restore", 480, environment)
        dependencies = Source.dependency_inventory(packages)
        C.require(dependencies, "STORY_EVIDENCE_FAILED", "Story 2.18 restored package inventory is empty")
        execute_bounded(work, [str(dotnet), "build", PROJECT, "--configuration", "Release", "--no-restore",
                               "--disable-build-servers", "-warnaserror", "-m:1", "-p:UseSharedCompilation=false",
                               *build_properties], "release-build", 720, environment)
        assembly = work / ASSEMBLY
        xml = work / ".story-2-18-xunit.xml"
        C.require(assembly.is_file() and not assembly.is_symlink() and not xml.exists(),
                  "STORY_EVIDENCE_FAILED", "Story 2.18 requires a fresh private Release build and test report")
        with native_topology(work, runtime) as topology:
            execute_bounded(work, [str(dotnet), str(assembly), "-method", SELECTOR, "-xml", str(xml)],
                            "native-conditional-query", 420,
                            {**environment, **topology, "EVENTSTORE_DAPR_RUNTIME_PROFILE": RUNTIME,
                             "EVENTSTORE_ROUTE_CATALOG_DAPRD": str(daprd), "Logging__LogLevel__Default": "Warning"})
        cases = passing_cases(xml, assembly)
        C.require(dependencies == Source.dependency_inventory(packages),
                  "SOURCE_MISMATCH", "Story 2.18 restored dependency bytes changed during execution")
        return {"dotnet_sdk": sdk, "packages_sha256": C.digest(C.canonical(dependencies)), "runtime": runtime,
                "cases": cases}


def evaluate(control):
    C.require(supported(control.story), "ADAPTER_UNAVAILABLE", "Story 2.18 requires its exact supporting slice")
    try:
        manifest = inventory(control)
        C.require(subject_digest(manifest["root_tree"]) == control.subject, "SUBJECT_MISMATCH",
                  "Story 2.18 build tree differs from the authenticated approval subject")
        binding = {"adapter": ADAPTER, "head_sha": manifest["head"], "root_tree": manifest["root_tree"],
                   "builds_gitlink": manifest["submodules"][0]["commit"],
                   "inventory_sha256": C.digest(C.canonical(manifest))}
        control.story_inventory = manifest
        control.binding["story_adapter"] = binding
        execution = run(control.root, manifest)
        assert_stable(control.root, manifest)
        control.story_evidence = {"schema": SCHEMA, "adapter": ADAPTER, "result": "PASS", "binding": binding,
                                  "selector": SELECTOR, "test_source": TEST_SOURCE,
                                  "http_status_sequence": [200, 304, 200], **execution}
    except (OSError, ValueError, KeyError) as error:
        raise C.Refusal("STORY_EVIDENCE_FAILED", "Story 2.18 cannot build or execute its bound live proof") from error
