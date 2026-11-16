"""Fixed SDK-only owner bridge built solely from verified private snapshot bytes.

The hashes detect incomplete copies and changes. They authenticate no source or
installed toolchain, and do not supply the publication build-input proof.
"""
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import tempfile
import xml.etree.ElementTree as ET

BRIDGE = "tools/ProductionProfileBridge"
PROJECT = BRIDGE + "/ProductionProfileBridge.csproj"
CONFIG = BRIDGE + "/NuGet.Config"
PROGRAM = BRIDGE + "/Program.cs"
SOURCE_DIRECTORY = "src/Hexalith.EventStore.Contracts/Deployment"
SOURCE_NAMES = (
    "ProductionProfile.cs", "ProductionProfileApplication.cs", "ProductionProfileArtifact.cs",
    "ProductionProfileCanonicalJson.cs", "ProductionProfileCatalogs.cs", "ProductionProfileCodec.cs",
    "ProductionProfileComponent.cs", "ProductionProfileComponentVersion.cs", "ProductionProfileCredentialReference.cs",
    "ProductionProfileEndpoint.cs", "ProductionProfileEnvironment.cs", "ProductionProfileEvidence.cs",
    "ProductionProfileFreshness.cs", "ProductionProfileInfrastructure.cs", "ProductionProfileIssue.cs",
    "ProductionProfileIssuer.cs", "ProductionProfilePhysicalTarget.cs", "ProductionProfileProtection.cs",
    "ProductionProfileProvider.cs", "ProductionProfileRelease.cs", "ProductionProfileRestore.cs",
    "ProductionProfileRestoreUnit.cs", "ProductionProfileTemplate.cs", "ProductionProfileTool.cs",
    "ProductionProfileTopology.cs", "ProductionProfileV2.cs", "ProductionProfileV2Codec.cs",
    "ProductionProfileV2Component.cs", "ProductionProfileV2Dapr.cs", "ProductionProfileV2Scope.cs",
    "ProductionProfileV2Validator.cs", "ProductionProfileValidationComponent.cs",
    "ProductionProfileValidationContext.cs", "ProductionProfileValidationDocument.cs",
    "ProductionProfileValidationException.cs", "ProductionProfileValidationResult.cs",
    "ProductionProfileValidator.cs", "ProductionProfileWriter.cs",
)
SOURCES = tuple(SOURCE_DIRECTORY + "/" + name for name in SOURCE_NAMES)
CONTROLS = ("tools/profile_bridge_build.py", PROJECT, CONFIG, PROGRAM)
BUILD_INPUTS = ("global.json", PROJECT, CONFIG, PROGRAM, *SOURCES)
MAX_FILE = 1024 * 1024
MAX_TOTAL = 12 * MAX_FILE
PROPERTIES = {
    "OutputType": "Exe", "TargetFramework": "net11.0", "ImplicitUsings": "enable", "Nullable": "enable",
    "EnableDefaultItems": "false", "UseAppHost": "false", "IsPackable": "false", "NuGetAudit": "false",
}


class Refusal(Exception):
    """Safe diagnostic without caller-controlled values or child diagnostics."""
    def __init__(self, code="PROFILE_BUILD_INPUT_INVALID"):
        super().__init__(code)
        self.code = code


def require(value, code="PROFILE_BUILD_INPUT_INVALID"):
    if not value:
        raise Refusal(code)


def safe_path(path):
    require(isinstance(path, str) and len(path) <= 512
            and re.fullmatch(r"[A-Za-z0-9_.@/-]+", path) is not None and not path.startswith("/")
            and all(part not in ("", ".", "..") for part in path.split("/")))


def no_symbolic_ancestors(path):
    require(all(not part.is_symlink() for part in (path.absolute(), *path.absolute().parents)))


def read(root, relative, maximum=MAX_FILE):
    safe_path(relative)
    candidate = root / relative
    no_symbolic_ancestors(candidate)
    parent_fd = None
    try:
        # Anchor every ancestor without following links, including during opens.
        parts = candidate.absolute().parts
        require(all(part not in (".", "..") for part in parts))
        parent_fd = os.open(parts[0], os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
        for part in parts[1:-1]:
            next_fd = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent_fd)
            os.close(parent_fd)
            parent_fd = next_fd
        # O_NONBLOCK prevents a substituted FIFO from hanging.
        fd = os.open(parts[-1], os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=parent_fd)
        with os.fdopen(fd, "rb") as stream:
            require(stat.S_ISREG(os.fstat(stream.fileno()).st_mode))
            content = stream.read(maximum + 1)
        require(0 < len(content) <= maximum)
        return content
    except OSError as error:
        raise Refusal() from error
    finally:
        if parent_fd is not None:
            os.close(parent_fd)


def source_paths(root):
    """Require the complete fixed owner source and bridge membership."""
    try:
        no_symbolic_ancestors(root / SOURCE_DIRECTORY)
        actual = {p.name for p in (root / SOURCE_DIRECTORY).iterdir()
                  if p.name.startswith("ProductionProfile") and p.name.endswith(".cs")}
        require(actual == set(SOURCE_NAMES))
        no_symbolic_ancestors(root / BRIDGE)
        require({p.name for p in (root / BRIDGE).iterdir()} == {"Program.cs", "ProductionProfileBridge.csproj", "NuGet.Config"})
    except OSError as error:
        raise Refusal() from error
    return SOURCES


def validate_configuration(files):
    """Closed project/configuration syntax: no extensible imports, items or properties."""
    try:
        def unique(pairs):
            value = {}
            for key, item in pairs:
                require(key not in value)
                value[key] = item
            return value
        sdk = json.loads(files["global.json"], object_pairs_hook=unique)
        require(isinstance(sdk, dict) and set(sdk) <= {"sdk", "test"} and "sdk" in sdk)
        require(isinstance(sdk["sdk"], dict) and set(sdk["sdk"]) <= {"version", "rollForward", "allowPrerelease"})
        require(isinstance(sdk["sdk"].get("version"), str)
                and re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?", sdk["sdk"]["version"]) is not None)
        require("allowPrerelease" not in sdk["sdk"] or type(sdk["sdk"]["allowPrerelease"]) is bool)
        require("rollForward" not in sdk["sdk"] or sdk["sdk"]["rollForward"] in
                ("patch", "feature", "minor", "major", "latestPatch", "latestFeature", "latestMinor", "latestMajor", "disable"))
        if "test" in sdk:
            require(sdk["test"] == {"runner": "Microsoft.Testing.Platform"})
        project = ET.fromstring(files[PROJECT])
        require(project.tag == "Project" and project.attrib == {"Sdk": "Microsoft.NET.Sdk"})
        require([node.tag for node in project] == ["PropertyGroup", "ItemGroup"])
        properties, items = project
        require(not properties.attrib and not items.attrib and len(properties) == len(PROPERTIES))
        require({node.tag: node.text for node in properties} == PROPERTIES)
        require(all(not node.attrib and len(node) == 0 for node in properties))
        expected = ["Program.cs", *("../../" + path for path in SOURCES)]
        require(len(items) == len(expected))
        require(all(node.tag == "Compile" and node.attrib == {"Include": path}
                    and len(node) == 0 and not (node.text or "").strip() for node, path in zip(items, expected)))
        config = ET.fromstring(files[CONFIG])
        require(config.tag == "configuration" and not config.attrib)
        require([node.tag for node in config] == ["packageSources", "fallbackPackageFolders"])
        require(all(not node.attrib and len(node) == 1 and node[0].tag == "clear"
                    and not node[0].attrib and len(node[0]) == 0 for node in config))
    except (ET.ParseError, KeyError, ValueError, TypeError, RecursionError) as error:
        raise Refusal() from error


@dataclass(frozen=True)
class Capture:
    """Exact captured bytes and SHA-256 membership; no source authority is implied."""
    files: tuple
    hashes: tuple


def capture(root, data):
    """Capture fixed controls/sources plus separately validated owner data."""
    source_paths(root)
    require(set(data).isdisjoint(BUILD_INPUTS) and 3 <= len(data) <= 4)
    files = {path: read(root, path) for path in BUILD_INPUTS}
    for path, expected in data.items():
        require(isinstance(expected, bytes))
        files[path] = read(root, path, 4 * MAX_FILE)
        require(files[path] == expected)
    require(sum(map(len, files.values())) <= MAX_TOTAL)
    validate_configuration(files)
    # Detect membership changing during capture as well as missing fixed files.
    source_paths(root)
    return Capture(tuple(sorted(files.items())), tuple(sorted((path, hashlib.sha256(content).hexdigest())
                                                            for path, content in files.items())))


def materialize(captured, snapshot):
    require(not snapshot.exists())
    snapshot.mkdir(mode=0o700)
    for path, content in captured.files:
        safe_path(path)
        destination = snapshot / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open("xb") as stream:
            stream.write(content)
    verify(captured, snapshot)


def verify(captured, snapshot):
    """Reject missing, extra, changed, symbolic or nonregular snapshot members."""
    no_symbolic_ancestors(snapshot)
    expected = dict(captured.hashes)
    require(len(expected) == len(captured.files) and set(expected) == {path for path, _ in captured.files})
    directories = {parent.as_posix() for path in expected for parent in Path(path).parents if parent != Path(".")}
    actual, actual_directories = set(), set()
    try:
        for base, children, filenames in os.walk(snapshot, followlinks=False):
            for name in children:
                path = Path(base) / name
                no_symbolic_ancestors(path)
                actual_directories.add(path.relative_to(snapshot).as_posix())
            for name in filenames:
                actual.add((Path(base) / name).relative_to(snapshot).as_posix())
        require(actual == set(expected) and actual_directories == directories)
        for path, checksum in expected.items():
            require(hashlib.sha256(read(snapshot, path, 4 * MAX_FILE)).hexdigest() == checksum)
        validate_configuration(dict(captured.files))
    except OSError as error:
        raise Refusal() from error


def execute(captured, inventory_digest, resolved_digest, infrastructure_digest):
    """Restore/build/run fixed arguments under the existing bounded child control."""
    import phase_4_transition as T
    for index, checksum in enumerate((inventory_digest, resolved_digest, infrastructure_digest)):
        require(index == 2 and checksum == "-"
                or isinstance(checksum, str) and re.fullmatch(r"[0-9a-f]{64}", checksum) is not None)
    executable = shutil.which("dotnet")
    require(executable is not None, "PROFILE_BRIDGE_UNAVAILABLE")
    dotnet = Path(executable).resolve()
    configuration = "Release" if os.environ.get("CI") else "Debug"
    with tempfile.TemporaryDirectory(prefix="publication-profile-") as directory:
        scratch = Path(directory)
        snapshot = scratch / "snapshot"
        materialize(captured, snapshot)
        # SDK resolution starts at cwd; keep the exact owner SDK pin there too.
        (scratch / "global.json").write_bytes(dict(captured.files)["global.json"])
        environment = {"MSBUILDDISABLENODEREUSE": "1", "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
                       "DOTNET_ROOT": str(dotnet.parent), "DOTNET_CLI_HOME": str(scratch / "home"),
                       "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1", "DOTNET_NOLOGO": "1",
                       "NUGET_PACKAGES": str(scratch / "packages"),
                       "NUGET_HTTP_CACHE_PATH": str(scratch / "http-cache"),
                       "MSBuildUserExtensionsPath": str(scratch / "msbuild-user")}
        flags = ["--disable-build-servers", "-m:1", "-nodeReuse:false", "-noAutoResponse",
                 "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false",
                 "-p:ImportDirectoryPackagesProps=false", "-p:ManagePackageVersionsCentrally=false",
                 "-p:BuildInParallel=false", "-p:UseSharedCompilation=false", "-p:NuGetAudit=false",
                 "-p:DisableImplicitLibraryPacksFolder=true",
                 "-p:RestoreSources=", "-p:RestoreAdditionalProjectSources=", "-p:RestoreFallbackFolders=",
                 "-p:RestoreAdditionalProjectFallbackFolders=", "-p:RestoreIgnoreFailedSources=false",
                 "-p:EnableTargetingPackDownload=false", "-p:EnableRuntimePackDownload=false",
                 "-p:BaseIntermediateOutputPath=" + str(scratch / "obj") + "/",
                 "-p:MSBuildProjectExtensionsPath=" + str(scratch / "obj") + "/",
                 "-p:OutputPath=" + str(scratch / "bin") + "/", "-p:Configuration=" + configuration]

        def child(command, filename):
            verify(captured, snapshot)
            require(read(scratch, "global.json") == dict(captured.files)["global.json"])
            try:
                output = T.Control.bounded_process(command, scratch, filename, timeout_seconds=60,
                    environment_extra=environment, failure_code="PROFILE_OWNER_VALIDATION_FAILED",
                    limit_code="PROFILE_BRIDGE_UNAVAILABLE")
            except T.Refusal as error:
                raise Refusal(error.code) from error
            verify(captured, snapshot)
            require(read(scratch, "global.json") == dict(captured.files)["global.json"])
            return output

        child([str(dotnet), "restore", str(snapshot / PROJECT), "--configfile", str(snapshot / CONFIG),
               "--no-http-cache", *flags], "restore.log")
        child([str(dotnet), "build", str(snapshot / PROJECT), "--no-restore", *flags], "build.log")
        # Exit zero cannot substitute for producing a fresh assembly in private outputs.
        require((scratch / "bin/ProductionProfileBridge.dll").is_file(), "PROFILE_OWNER_RESULT_INVALID")
        read(scratch, "bin/ProductionProfileBridge.dll", 4 * MAX_FILE)
        return child([str(dotnet), str(scratch / "bin/ProductionProfileBridge.dll"), str(snapshot),
                      inventory_digest, resolved_digest, infrastructure_digest], "profile-result.txt")
