"""Fixed SDK-only owner bridge built solely from verified private snapshot bytes.

Hashes detect incomplete copies and changes; source authentication belongs to
the caller. Installed SDK/runtime/OS binaries remain trusted, not attested.
"""
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import sys
import tempfile
import xml.etree.ElementTree as ET

BRIDGE = "tools/ProductionProfileBridge"
PROJECT = BRIDGE + "/ProductionProfileBridge.csproj"
CONFIG = BRIDGE + "/NuGet.Config"
PROGRAM = BRIDGE + "/Program.cs"
TARGETS = BRIDGE + "/BuildProof.targets"
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
CONTROLS = ("tools/profile_bridge_build.py", PROJECT, CONFIG, PROGRAM, TARGETS)
BUILD_INPUTS = ("global.json", *CONTROLS, *SOURCES)
PROOF_UNAVAILABLE = "PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE"
TASK_PROPERTIES = ("MicrosoftNETBuildTasksAssembly", "_BuildTasksDirectory", "_BuildTasksAssemblyName",
    "NetCoreSdkRoot", "RestoreTaskAssemblyFile", "VSTestTaskAssemblyFile", "RoslynTasksAssembly",
    "MicrosoftBuildTasksGitAssemblyFile", "_MicrosoftSourceLinkCommonAssemblyFile", "_SourceLinkGitHubAssemblyFile",
    "_SourceLinkGitLabAssemblyFile", "_SourceLinkAzureReposGitAssemblyFile", "_SourceLinkBitbucketAssemblyFile",
    "CrossGenTasksPath", "DotNetApiCompatTaskAssembly", "NuGetPackTaskAssemblyFile", "ContainerCustomTasksAssembly")
MAX_FILE = 1024 * 1024
MAX_TOTAL = 12 * MAX_FILE
PROPERTIES = {
    "OutputType": "Exe", "TargetFramework": "net11.0", "ImplicitUsings": "enable", "Nullable": "enable",
    "EnableDefaultItems": "false", "UseAppHost": "false", "IsPackable": "false", "NuGetAudit": "false",
}


PROOF_TARGETS = """<Project>
  <Target Name="ProfileProofPlan" DependsOnTargets="ResolveReferences;FindReferenceAssembliesForReferences;GenerateGlobalUsings;GenerateAssemblyInfo;GenerateTargetFrameworkMonikerAttribute;GenerateMSBuildEditorConfigFile;ProfileProofObserve" />
  <Target Name="ProfileProofObserve" BeforeTargets="CoreCompile" DependsOnTargets="AddGlobalAnalyzerConfigForPackage_MicrosoftCodeAnalysisNetAnalyzers">
    <PropertyGroup>
      <_ProofPackages>@(PackageReference)</_ProofPackages>
      <_ProofProjects>@(ProjectReference)</_ProofProjects>
    </PropertyGroup>
    <ItemGroup>
      <_ProfileProof Include="@(Compile->'source|%(FullPath)')" />
      <_ProfileProof Include="@(ReferencePathWithRefAssemblies->'reference|%(FullPath)')" />
      <_ProfileProof Include="@(Analyzer->'analyzer|%(FullPath)')" />
      <_ProfileProof Include="@(AdditionalFiles->'additional|%(FullPath)')" />
      <_ProfileProof Include="@(EditorConfigFiles->'config|%(FullPath)')" />
      <_ProfileProof Include="@(AnalyzerConfigFiles->'config|%(FullPath)')" />
      <_ProfileProofImport Include="$(MSBuildAllProjects)" />
      <_ProfileProof Include="@(_ProfileProofImport->'import|%(FullPath)')" />
      <_ProfileProof Include="response|$(CompilerResponseFile)" />
      <_ProfileProof Include="arguments|$(AdditionalCompilerArguments)" />
      <_ProfileProof Include="toolpath|$(CscToolPath)" />
      <_ProfileProof Include="toolexe|$(CscToolExe)" />
      <_ProfileProof Include="cscenvironment|$(CscEnvironment)" />
      <_ProfileProof Include="package|$(_ProofPackages)" />
      <_ProfileProof Include="project|$(_ProofProjects)" />
      <_ProfileProof Include="complete|profile-bridge-v1" />
    </ItemGroup>
    <WriteLinesToFile File="$(ProfileProofOutput)" Lines="@(_ProfileProof)" Overwrite="true" Encoding="UTF-8" />
  </Target>
</Project>
"""


class Refusal(Exception):
    """Safe diagnostic without caller-controlled values or child diagnostics."""
    def __init__(self, code="PROFILE_BUILD_INPUT_INVALID"):
        super().__init__(code)
        self.code = code


def require(value, code="PROFILE_BUILD_INPUT_INVALID"):
    if not value:
        raise Refusal(code)


def safe_path(path, generated=False):
    require(isinstance(path, str) and len(path) <= 512
            and re.fullmatch(r"[A-Za-z0-9_.@/,=+-]+" if generated else r"[A-Za-z0-9_.@/-]+", path) is not None and not path.startswith("/")
            and all(part not in ("", ".", "..") for part in path.split("/")))


def no_symbolic_ancestors(path):
    require(all(not part.is_symlink() for part in (path.absolute(), *path.absolute().parents)))


def property_path(path):
    """No MSBuild property/list escaping can be introduced through temp/SDK paths."""
    require(path.is_absolute() and re.fullmatch(r"/[A-Za-z0-9_.@/-]+", str(path)) is not None
            and ".." not in path.parts, PROOF_UNAVAILABLE)


def read(root, relative, maximum=MAX_FILE, *, generated=False):
    safe_path(relative, generated)
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
        require((generated or len(content) > 0) and len(content) <= maximum)
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
        require({p.name for p in (root / BRIDGE).iterdir()} == {"Program.cs", "ProductionProfileBridge.csproj", "NuGet.Config", "BuildProof.targets"})
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
        require([node.tag for node in project] == ["PropertyGroup", "ItemGroup", "Import"])
        properties, items, observation = project
        require(observation.attrib == {"Project": "BuildProof.targets"} and len(observation) == 0)
        require(files[TARGETS].decode("utf-8").replace("\r\n", "\n") == PROOF_TARGETS)
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



def fingerprint(path):
    """Identity of a trusted installed file, not binary authentication."""
    no_symbolic_ancestors(path)
    value = path.stat()
    require(stat.S_ISREG(value.st_mode), PROOF_UNAVAILABLE)
    return (value.st_dev, value.st_ino, value.st_size, value.st_mtime_ns, value.st_ctime_ns)


def directory_identity(path):
    no_symbolic_ancestors(path)
    value = path.stat()
    require(stat.S_ISDIR(value.st_mode), PROOF_UNAVAILABLE)
    return (value.st_dev, value.st_ino, value.st_mtime_ns, value.st_ctime_ns)


def sdk_resolution_directories(sdk):
    """Seal the selected SDK's nested wildcard/import/resolver search locations."""
    result = {sdk: directory_identity(sdk)}
    for path in sdk.rglob("*"):
        if path.is_dir():
            no_symbolic_ancestors(path)
            result[path] = directory_identity(path)
    return result


def execution_metadata(dotnet, sdk):
    """Presence and identity of supported SDK/host configuration, without OS inventory."""
    result = {}
    for root in (sdk, dotnet.parent / "shared", dotnet.parent / "host"):
        for path in root.rglob("*"):
            if path.is_file() and (path.name.endswith((".deps.json", ".runtimeconfig.json", ".runtimeconfig.dev.json",
                    ".props", ".targets", ".tasks", ".overridetasks"))
                    or path.suffix == ".xml" and (sdk / "SdkResolvers") in path.parents):
                result[path] = fingerprint(path)
    for name in ("dotnet.deps.json", "dotnet.runtimeconfig.json"):
        path = dotnet.parent / name
        if path.exists(): result[path] = fingerprint(path)
    for stem in (sdk / "dotnet", sdk / "MSBuild", sdk / "Roslyn/bincore/csc"):
        for suffix in (".deps.json", ".runtimeconfig.json"):
            require(Path(str(stem) + suffix) in result, PROOF_UNAVAILABLE)
    return result


def verify_stage_seals(scratch, settings, contract, captured, *, compiling=False):
    """Repeat these checks at entry and while the actual compiler exec is stopped."""
    verify(captured, scratch / "snapshot")
    require(read(scratch, "global.json") == dict(captured.files)["global.json"], PROOF_UNAVAILABLE)
    if not compiling:
        require(output_members(scratch) == set(contract["produced"]), PROOF_UNAVAILABLE)
    for relative, checksum in contract["produced"].items():
        require(hashlib.sha256(read(scratch, relative, 16 * MAX_FILE, generated=True)).hexdigest()
                == checksum, PROOF_UNAVAILABLE)
    for path, identity in contract["identities"]:
        require(fingerprint(Path(path)) == tuple(identity), PROOF_UNAVAILABLE)
    for path, identity in contract["directories"]:
        require(directory_identity(Path(path)) == tuple(identity), PROOF_UNAVAILABLE)
    if contract["sdk"] is not None:
        sdk = Path(contract["sdk"])
        require({str(path): list(identity) for path, identity in sdk_resolution_directories(sdk).items()}
                == contract["sdk_directories"], PROOF_UNAVAILABLE)
        require({str(path): list(identity) for path, identity in execution_metadata(Path(settings["dotnet"]), sdk).items()}
                == contract["metadata"], PROOF_UNAVAILABLE)


def output_members(scratch):
    """Private generated roots have closed membership between SDK producers."""
    result = set()
    for name in ("obj", "bin", "generated"):
        base = scratch / name
        no_symbolic_ancestors(base)
        if base.exists():
            require(base.is_dir(), PROOF_UNAVAILABLE)
            for path in base.rglob("*"):
                no_symbolic_ancestors(path)
                if not path.is_dir():
                    require(stat.S_ISREG(path.stat().st_mode), PROOF_UNAVAILABLE)
                    result.add(path.relative_to(scratch).as_posix())
    return result


def output_hashes(scratch):
    return {path: hashlib.sha256(read(scratch, path, 16 * MAX_FILE, generated=True)).hexdigest()
            for path in sorted(output_members(scratch))}


def fixed_environment(scratch, dotnet):
    return {"MSBUILDDISABLENODEREUSE": "1", "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
            "DOTNET_ROOT": str(dotnet.parent), "DOTNET_HOST_PATH": str(dotnet),
            "DOTNET_CLI_HOME": str(scratch / "home"), "DOTNET_NOLOGO": "1",
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
            "DOTNET_GENERATE_ASPNET_CERTIFICATE": "false", "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH": "false",
            "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE": "true", "MSBuildEnableWorkloadResolver": "false",
            "DOTNET_MULTILEVEL_LOOKUP": "0", "NUGET_PACKAGES": str(scratch / "packages"),
            "NUGET_HTTP_CACHE_PATH": str(scratch / "http-cache"),
            "MSBuildUserExtensionsPath": str(scratch / "msbuild-user")}


def fixed_command(stage, scratch, settings):
    """Only these internally constructed commands can cross the child boundary."""
    dotnet = settings["dotnet"]
    if stage == "sdk":
        return [dotnet, "--version"]
    if stage == "run":
        return [dotnet, str(scratch / "bin/ProductionProfileBridge.dll"), str(scratch / "snapshot"),
                *settings["digests"]]
    flags = ["-m:1", "-nodeReuse:false", "-noAutoResponse",
             "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false",
             "-p:ImportDirectoryPackagesProps=false", "-p:ManagePackageVersionsCentrally=false",
             "-p:ImportProjectExtensionProps=false", "-p:ImportProjectExtensionTargets=false",
             "-p:ImportUserLocationsByWildcardBeforeMicrosoftCommonProps=false",
             "-p:ImportUserLocationsByWildcardAfterMicrosoftCommonProps=false",
             "-p:ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets=false",
             "-p:ImportUserLocationsByWildcardAfterMicrosoftCommonTargets=false",
             "-p:BuildInParallel=false", "-p:UseSharedCompilation=false", "-p:NuGetAudit=false",
             "-p:DisableImplicitLibraryPacksFolder=true", "-p:RestoreSources=",
             "-p:RestoreAdditionalProjectSources=", "-p:RestoreFallbackFolders=",
             "-p:RestoreAdditionalProjectFallbackFolders=", "-p:RestoreIgnoreFailedSources=false",
             "-p:EnableTargetingPackDownload=false", "-p:EnableRuntimePackDownload=false",
             "-p:UseHostCompilerIfAvailable=false", "-p:EmitCompilerGeneratedFiles=true",
             "-p:BaseIntermediateOutputPath=" + str(scratch / "obj") + "/",
             "-p:MSBuildProjectExtensionsPath=" + str(scratch / "obj") + "/",
             "-p:OutputPath=" + str(scratch / "bin") + "/",
             "-p:CompilerGeneratedFilesOutputPath=" + str(scratch / "generated"),
             "-p:Configuration=" + settings["configuration"],
             "-p:ProfileProofOutput=" + str(scratch / "proof" / (stage + ".items"))]
    for name in ("CustomBeforeMicrosoftCommonProps", "CustomAfterMicrosoftCommonProps",
                 "CustomBeforeMicrosoftCommonTargets", "CustomAfterMicrosoftCommonTargets",
                 "CustomBeforeMicrosoftCSharpTargets", "CustomAfterMicrosoftCSharpTargets"):
        flags.append("-p:" + name + "=")
    project = str(scratch / "snapshot" / PROJECT)
    if stage == "preprocess":
        return [dotnet, "msbuild", project, *flags, "-pp:" + str(scratch / "proof/preprocessed.xml")]
    if stage == "evaluate":
        return [dotnet, "msbuild", project, *flags, "-getProperty:" + ",".join(TASK_PROPERTIES)]
    if stage == "restore":
        return [dotnet, "restore", project, "--configfile", str(scratch / "snapshot" / CONFIG),
                "--no-http-cache", "--disable-build-servers", *flags,
                "-flp:logfile=" + str(scratch / "proof/restore.tasks") + ";verbosity=diagnostic"]
    require(stage in ("plan", "build"), PROOF_UNAVAILABLE)
    command = [dotnet, "msbuild", project, *flags, "-target:ProfileProofPlan" if stage == "plan" else "-target:Build"]
    return [*command, "-flp:logfile=" + str(scratch / "proof" / (stage + ".tasks")) + ";verbosity=diagnostic"]


def verify_compiler_response(content, scratch, expected):
    """Check actual csc inputs before exec, including its SDK-produced response file."""
    import shlex
    plan = {}
    for line in expected.decode("utf-8-sig").splitlines():
        kind, _, value = line.partition("|")
        plan.setdefault(kind, set()).add(value)
    observed = {kind: set() for kind in ("source", "reference", "analyzer", "config", "additional")}
    inputs = {"reference": "reference", "analyzer": "analyzer", "analyzerconfig": "config",
              "additionalfile": "additional"}
    scalars = {"unsafe-", "checked-", "nowarn", "fullpaths", "nostdlib+", "errorreport", "warn", "define",
        "highentropyva+", "nullable", "features", "debug+", "debug-", "debug", "filealign", "optimize-",
        "optimize+", "target", "warnaserror-", "utf8output", "deterministic+", "langversion", "warnaserror+"}
    for argument in shlex.split(content.decode("utf-8-sig")):
        name, _, value = argument.lstrip("/").partition(":")
        if name in inputs:
            no_symbolic_ancestors(Path(value))
            value = os.path.abspath(value)
            require(value in plan.get(inputs[name], set()), PROOF_UNAVAILABLE)
            observed[inputs[name]].add(value)
        elif name in ("out", "refout", "generatedfilesout"):
            require(Path(value).is_absolute() and Path(value).is_relative_to(scratch)
                    and ".." not in Path(value).parts, PROOF_UNAVAILABLE)
        elif argument.endswith(".cs"):
            path = Path(argument)
            if not path.is_absolute():
                path = scratch / "snapshot" / BRIDGE / path
            no_symbolic_ancestors(path)
            path = Path(os.path.abspath(path))
            require(str(path) in plan.get("source", set()), PROOF_UNAVAILABLE)
            observed["source"].add(str(path))
        else:
            require(argument.startswith("/") and name in scalars, PROOF_UNAVAILABLE)
    require(all(observed[kind] == plan.get(kind, set()) for kind in observed), PROOF_UNAVAILABLE)


def verify_execution_environment(actual, fixed, sdk, *, compiler=False):
    """Only the fixed environment and these selected-SDK csc derivations are supported."""
    expected = dict(fixed)
    if compiler:
        require(sdk is not None, PROOF_UNAVAILABLE)
        expected.update({"MSBUILDFAILONDRIVEENUMERATINGWILDCARD": "1", "MSBUILDUSESERVER": "0",
                         "MSBuildExtensionsPath": str(sdk), "MSBuildLoadMicrosoftTargetsReadOnly": "true",
                         "MSBuildSDKsPath": str(sdk / "Sdks"), "_MSBUILDTLENABLED": "0"})
    require(actual == expected, PROOF_UNAVAILABLE)


def trace_fixed_stage(scratch, stage, expected, contract_digest):
    """Linux x86-64 syscall trace. Failure, lost children or network attempts refuse.

    PTRACE_GET_SYSCALL_INFO avoids guessing syscall entry/exit across clone/exec.
    The kernel stops relevant exec/network syscalls before execution and
    follows every new thread and descendant.
    This is an observation of the trusted toolchain, not a sandbox for hostile SDKs.
    """
    import ctypes
    import platform
    import signal
    import struct
    require(sys.platform == "linux" and platform.machine() == "x86_64", PROOF_UNAVAILABLE)
    content = read(scratch, "invocation.json")
    require(hashlib.sha256(content).hexdigest() == expected, PROOF_UNAVAILABLE)
    settings = json.loads(content)
    contract_bytes = read(scratch, "proof/" + stage + ".contract")
    require(hashlib.sha256(contract_bytes).hexdigest() == contract_digest, PROOF_UNAVAILABLE)
    contract = json.loads(contract_bytes)
    require(contract["invocation"] == expected and contract["stage"] == stage
            and contract["environment"] == dict(os.environ), PROOF_UNAVAILABLE)
    snapshot = scratch / "snapshot"
    captured = Capture(tuple((path, read(snapshot, path, 4 * MAX_FILE)) for path, _ in settings["capture"]),
                       tuple(tuple(item) for item in settings["capture"]))
    verify_stage_seals(scratch, settings, contract, captured)
    command = fixed_command(stage, scratch, settings)
    libc = ctypes.CDLL(None, use_errno=True)
    libc.ptrace.restype = ctypes.c_long

    def ptrace(request, pid, address=0, data=0):
        ctypes.set_errno(0)
        value = libc.ptrace(request, pid, ctypes.c_void_p(address), ctypes.c_void_p(data))
        require(value != -1 or ctypes.get_errno() == 0, PROOF_UNAVAILABLE)
        return value

    def memory(pid, address, maximum=4096):
        result = bytearray()
        for offset in range(0, maximum, 8):
            word = ptrace(2, pid, address + offset) & ((1 << 64) - 1)
            chunk = word.to_bytes(8, "little")
            result.extend(chunk)
            if b"\0" in chunk:
                return bytes(result).split(b"\0", 1)[0]
        raise Refusal(PROOF_UNAVAILABLE)

    def install_observation_filter():
        # The kernel stops only exec/network/unsupported syscall entries. This
        # avoids hundreds of thousands of irrelevant read/futex stops while
        # retaining the complete process and Internet-attempt observation.
        class Instruction(ctypes.Structure):
            _fields_ = [("code", ctypes.c_ushort), ("yes", ctypes.c_ubyte),
                        ("no", ctypes.c_ubyte), ("value", ctypes.c_uint)]
        class Program(ctypes.Structure):
            _fields_ = [("length", ctypes.c_ushort), ("instructions", ctypes.POINTER(Instruction))]
        instructions = [(0x20, 0, 0, 4), (0x15, 1, 0, 0xc000003e), (0x06, 0, 0, 0x80000000),
                        (0x20, 0, 0, 0), (0x35, 0, 1, 0x40000000), (0x06, 0, 0, 0x80000000)]
        for number in (41, 42, 44, 46, 59, 307, 322, 425, 426, 427):
            instructions.extend(((0x15, 0, 1, number), (0x06, 0, 0, 0x7ff00000)))
        instructions.append((0x06, 0, 0, 0x7fff0000))
        array = (Instruction * len(instructions))(*(Instruction(*item) for item in instructions))
        program = Program(len(array), array)
        require(libc.prctl(38, 1, 0, 0, 0) == 0
                and libc.prctl(22, 2, ctypes.byref(program), 0, 0) == 0, PROOF_UNAVAILABLE)

    child = os.fork()
    if child == 0:
        try:
            ptrace(0, 0)
            os.kill(os.getpid(), signal.SIGSTOP)
            install_observation_filter()
            os.execve(command[0], command, os.environ)
        finally:
            os._exit(125)
    active, executions, stops, result, runtime_roots = {child}, [], 0, None, set()
    execution_environments = []
    terminated = set()
    mapped_toolchain = set()
    try:
        _, status = os.waitpid(child, 0)
        require(os.WIFSTOPPED(status), PROOF_UNAVAILABLE)
        ptrace(0x4200, child, 0, 1 | 2 | 4 | 8 | 16 | 64 | 128 | 0x100000)
        ptrace(7, child)
        while active:
            pid, status = os.waitpid(-1, 0x40000000)
            if pid not in active:
                # An auto-attached clone can report its initial stop before
                # the parent clone event is delivered by waitpid.
                require(os.WIFSTOPPED(status) and os.WSTOPSIG(status) == signal.SIGSTOP
                        and re.search(r"^TracerPid:\s+" + str(os.getpid()) + r"$",
                                      Path("/proc", str(pid), "status").read_text(), re.M), PROOF_UNAVAILABLE)
                active.add(pid)
            if os.WIFEXITED(status) or os.WIFSIGNALED(status):
                active.remove(pid)
                terminated.add(pid)
                if pid == child:
                    require(os.WIFEXITED(status), PROOF_UNAVAILABLE)
                    result = os.WEXITSTATUS(status)
                continue
            require(os.WIFSTOPPED(status), PROOF_UNAVAILABLE)
            stops += 1
            require(stops <= 2000000, PROOF_UNAVAILABLE)
            stopped, event = os.WSTOPSIG(status), status >> 16
            if event in (1, 2, 3):
                new = ctypes.c_ulonglong()
                ptrace(0x4201, pid, 0, ctypes.addressof(new))
                if new.value not in terminated:
                    active.add(new.value)
            if event == 6 and re.search(r"^Tgid:\s+" + str(pid) + r"$",
                                       Path("/proc", str(pid), "status").read_text(), re.M):
                mapping = Path("/proc", str(pid), "maps").read_text()
                (scratch / "proof" / (stage + ".maps")).write_text(mapping)
                for line in mapping.splitlines():
                    fields = line.split(maxsplit=5)
                    if len(fields) == 6 and fields[5].startswith(settings["dotnet"].rsplit("/", 1)[0] + "/"):
                        mapped_toolchain.add(fields[5])
                    if len(fields) == 6 and fields[5].startswith(settings["dotnet"].rsplit("/", 1)[0] + "/shared/"):
                        relative = Path(fields[5]).relative_to(Path(settings["dotnet"]).parent / "shared")
                        require(len(relative.parts) >= 3, PROOF_UNAVAILABLE)
                        runtime = Path(settings["dotnet"]).parent / "shared" / relative.parts[0] / relative.parts[1]
                        no_symbolic_ancestors(runtime)
                        runtime_roots.add(str(runtime))
            if event == 7:
                info = ctypes.create_string_buffer(88)
                require(ptrace(0x420e, pid, len(info), ctypes.addressof(info)) >= 24, PROOF_UNAVAILABLE)
                require(info.raw[0] == 3, PROOF_UNAVAILABLE)
                if info.raw[0] == 3:
                    arch = struct.unpack_from("I", info.raw, 4)[0]
                    require(arch == 0xc000003e, PROOF_UNAVAILABLE)
                    number, *args = struct.unpack_from("7Q", info.raw, 24)
                    # Internet socket creation is itself a network attempt. Unix
                    # diagnostic IPC stays inside the installed-runtime boundary.
                    if number == 41 and args[0] in (2, 10):
                        (scratch / "proof/refusal.json").write_text(json.dumps({"stage": stage, "kind": "internet-socket"}))
                        raise Refusal(PROOF_UNAVAILABLE)
                    if number in (42, 44, 46):
                        address = args[1] if number == 42 else args[4] if number == 44 else ptrace(2, pid, args[1])
                        if address and ptrace(2, pid, address) & 0xffff in (2, 10):
                            (scratch / "proof/refusal.json").write_text(json.dumps({"stage": stage, "kind": "internet-socket"}))
                            raise Refusal(PROOF_UNAVAILABLE)
                    require(number != 307, PROOF_UNAVAILABLE)  # unsupported sendmmsg envelope
                    require(number not in (322, 425, 426, 427), PROOF_UNAVAILABLE)  # execveat/io_uring
                    if number == 59:
                        executable = os.fsdecode(memory(pid, args[0]))
                        arguments = []
                        for index in range(256):
                            pointer = ptrace(2, pid, args[1] + index * 8) & ((1 << 64) - 1)
                            if not pointer: break
                            arguments.append(os.fsdecode(memory(pid, pointer)))
                        execution_environment = {}
                        for index in range(256):
                            pointer = ptrace(2, pid, args[2] + index * 8) & ((1 << 64) - 1)
                            if not pointer: break
                            name, separator, value = os.fsdecode(memory(pid, pointer)).partition("=")
                            require(separator and name not in execution_environment, PROOF_UNAVAILABLE)
                            execution_environment[name] = value
                        else:
                            raise Refusal(PROOF_UNAVAILABLE)
                        compiler = Path(os.environ.get("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR", "/unavailable")).parent / "Roslyn/bincore/csc"
                        try:
                            verify_execution_environment(execution_environment, contract["environment"],
                                Path(contract["sdk"]) if contract["sdk"] is not None else None,
                                compiler=executable == str(compiler))
                        except Refusal:
                            (scratch / "proof/refusal.json").write_text(json.dumps({"stage": stage, "kind": "execution-environment"}))
                            raise
                        execution_environments.append(execution_environment)
                        if executable == str(compiler):
                            require(stage == "build" and len(arguments) == 3 and arguments[1] == "/noconfig"
                                    and arguments[2].startswith("@"), PROOF_UNAVAILABLE)
                            try:
                                verify_stage_seals(scratch, settings, contract, captured, compiling=True)
                            except Refusal:
                                (scratch / "proof/refusal.json").write_text(json.dumps({"stage": stage, "kind": "compiler-input-seal"}))
                                raise
                            response = Path(arguments[2][1:])
                            require(response.is_relative_to(scratch), PROOF_UNAVAILABLE)
                            response_bytes = read(scratch, response.relative_to(scratch).as_posix())
                            plan_bytes = read(scratch, "proof/plan.items")
                            require(hashlib.sha256(plan_bytes).hexdigest() == contract["plan"], PROOF_UNAVAILABLE)
                            for relative, checksum in contract["produced"].items():
                                require(hashlib.sha256(read(scratch, relative, 16 * MAX_FILE, generated=True)).hexdigest()
                                        == checksum, PROOF_UNAVAILABLE)
                            (scratch / "proof/compiler.response").write_bytes(response_bytes)
                            try:
                                verify_compiler_response(response_bytes, scratch, plan_bytes)
                            except Refusal:
                                (scratch / "proof/refusal.json").write_text(json.dumps({"stage": stage, "kind": "compiler-response"}))
                                raise
                        permitted = (executable == str(compiler) or executable == settings["dotnet"]
                                and arguments == command and not executions or executable == "/bin/sh"
                                and arguments == ["sh", "-c", "umask"]
                                or executable == "/bin/ls" and len(arguments) == 3
                                and arguments[:2] == ["ls", "-ld"]
                                and Path(arguments[2]).is_relative_to(scratch)
                                and ".." not in Path(arguments[2]).parts)
                        if not permitted:
                            kind = "workload-advertising" if "--advertising-manifests-only" in arguments else "unexpected-execution"
                            (scratch / "proof/refusal.json").write_text(json.dumps({"stage": stage, "kind": kind}))
                            raise Refusal(PROOF_UNAVAILABLE)
                        executions.append(arguments)
            forwarded = 0 if stopped in (signal.SIGTRAP, signal.SIGSTOP, signal.SIGTRAP | 0x80) else stopped
            try:
                ptrace(7, pid, 0, forwarded)
            except Refusal:
                # exit_group can terminate a sibling while its syscall stop
                # is queued. Its final wait status is still required below.
                require(ctypes.get_errno() == 3, PROOF_UNAVAILABLE)
        require((result == 0 or stage == "run" and result in (1, 2)) and executions and stops > 0, PROOF_UNAVAILABLE)
        receipt = {"invocation": expected, "stage": stage, "command": command,
                   "contract": contract_digest, "exit_code": result, "executions": executions,
                   "execution_environments": execution_environments,
                   "runtime_roots": sorted(runtime_roots),
                   "toolchain_files": sorted(mapped_toolchain),
                   "outputs": output_hashes(scratch),
                   "compiler_response": hashlib.sha256(read(scratch, "proof/compiler.response")).hexdigest()
                   if stage == "build" else None,
                   "syscall_stops": stops, "network_attempts": 0, "complete": True}
        (scratch / "proof" / (stage + ".trace")).write_text(json.dumps(receipt, sort_keys=True))
    finally:
        for pid in active:
            try:
                os.kill(pid, signal.SIGKILL)
                ptrace(7, pid)
            except (OSError, Refusal):
                pass
        while True:
            try:
                pid, status = os.waitpid(-1, 0x40000000)
                if os.WIFSTOPPED(status):
                    try:
                        os.kill(pid, signal.SIGKILL)
                        ptrace(7, pid, 0, signal.SIGKILL)
                    except (OSError, Refusal):
                        pass
                else:
                    active.discard(pid)
            except ChildProcessError:
                break


class OwnerOutput(bytes):
    """An in-memory invocation result; JSON cannot manufacture this capability."""


_ISSUED = None


@dataclass(frozen=True, eq=False)
class InvocationProof:
    capture: str
    invocation: str
    sdk: str
    stages: tuple
    toolchain: tuple


def proved(output, *, captured=None):
    if type(output) is not OwnerOutput or _ISSUED is None:
        return False
    result, proof, checksum, capture_digest, invocation_digest, context, issued_capture = _ISSUED
    return (output is result and getattr(output, "proof", None) is proof
            and hashlib.sha256(output).hexdigest() == checksum
            and proof.capture == capture_digest and proof.invocation == invocation_digest
            and getattr(output, "_context", None) is context
            and (captured is None or captured is issued_capture))


def _execute(captured, inventory_digest, resolved_digest, infrastructure_digest):
    """Prove this execution's closure before each fixed affected stage launches."""
    import phase_4_transition as T
    digests = (inventory_digest, resolved_digest, infrastructure_digest)
    for index, checksum in enumerate(digests):
        require(index == 2 and checksum == "-" or isinstance(checksum, str)
                and re.fullmatch(r"[0-9a-f]{64}", checksum) is not None)
    executable = shutil.which("dotnet")
    require(executable is not None, PROOF_UNAVAILABLE)
    dotnet = Path(executable).resolve()
    property_path(dotnet)
    fingerprint(dotnet)
    with tempfile.TemporaryDirectory(prefix="publication-profile-") as directory:
        scratch = Path(directory)
        property_path(scratch)
        snapshot = scratch / "snapshot"
        materialize(captured, snapshot)
        (scratch / "global.json").write_bytes(dict(captured.files)["global.json"])
        (scratch / "proof").mkdir()
        settings = {"dotnet": str(dotnet), "configuration": "Release" if os.environ.get("CI") else "Debug",
                    "digests": digests, "nonce": os.urandom(32).hex(), "capture": captured.hashes}
        invocation = json.dumps(settings, sort_keys=True).encode()
        (scratch / "invocation.json").write_bytes(invocation)
        invocation_digest = hashlib.sha256(invocation).hexdigest()
        environment = fixed_environment(scratch, dotnet)
        require(environment.get("DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE") == "true"
                and environment.get("DOTNET_CLI_TELEMETRY_OPTOUT") == "1"
                and environment.get("MSBuildEnableWorkloadResolver") == "false", PROOF_UNAVAILABLE)
        identities, produced, stages = {dotnet: fingerprint(dotnet)}, {}, []
        selected_sdk, sdk_directories, metadata, known_imports = None, {}, {}, set()
        directories = {}
        # latestPatch remains the owner policy. Changes to its candidate SDK
        # inventory (or pack/runtime inventories) invalidate this invocation
        # before another CLI command can select a different installation.
        for name in ("sdk", "packs", "shared", "host"):
            base = dotnet.parent / name
            directories[base] = directory_identity(base)
            for path in base.iterdir():
                no_symbolic_ancestors(path)
                if path.is_dir():
                    directories[path] = directory_identity(path)

        def unchanged(allow_producer_outputs=False):
            verify(captured, snapshot)
            require(read(scratch, "global.json") == dict(captured.files)["global.json"], PROOF_UNAVAILABLE)
            require(read(scratch, "invocation.json") == invocation, PROOF_UNAVAILABLE)
            if not allow_producer_outputs:
                require(output_members(scratch) == set(produced), PROOF_UNAVAILABLE)
            for path, identity in identities.items():
                require(fingerprint(path) == identity, PROOF_UNAVAILABLE)
            for path, identity in directories.items():
                require(directory_identity(path) == identity, PROOF_UNAVAILABLE)
            if selected_sdk is not None:
                require(sdk_resolution_directories(selected_sdk) == sdk_directories, PROOF_UNAVAILABLE)
                require(execution_metadata(dotnet, selected_sdk) == metadata, PROOF_UNAVAILABLE)
            for path, checksum in produced.items():
                require(hashlib.sha256(read(scratch, path, 16 * MAX_FILE, generated=True)).hexdigest() == checksum, PROOF_UNAVAILABLE)

        def child(stage):
            unchanged()
            base_environment = {"PATH": os.defpath, "HOME": str(scratch), "TMPDIR": str(scratch),
                                "TMP": str(scratch), "TEMP": str(scratch), "LANG": "C.UTF-8"}
            base_environment.update(environment)
            contract = {"invocation": invocation_digest, "stage": stage, "environment": base_environment,
                        "identities": [(str(path), identity) for path, identity in identities.items()],
                        "directories": [(str(path), identity) for path, identity in directories.items()],
                        "sdk": str(selected_sdk) if selected_sdk is not None else None,
                        "sdk_directories": {str(path): identity for path, identity in sdk_directories.items()},
                        "metadata": {str(path): identity for path, identity in metadata.items()},
                        "produced": produced, "plan": hashlib.sha256(read(scratch, "proof/plan.items")).hexdigest()
                        if stage == "build" else None}
            contract_bytes = json.dumps(contract, sort_keys=True).encode()
            (scratch / "proof" / (stage + ".contract")).write_bytes(contract_bytes)
            contract_digest = hashlib.sha256(contract_bytes).hexdigest()
            command = [sys.executable, "-I", "-B", str(snapshot / "tools/profile_bridge_build.py"),
                       "--trace-private", str(scratch), stage, invocation_digest, contract_digest]
            try:
                output = T.Control.bounded_process(command, scratch, stage + ".log", timeout_seconds=60,
                    environment_extra=environment, failure_code=PROOF_UNAVAILABLE, limit_code="PROFILE_BRIDGE_UNAVAILABLE")
            except T.Refusal as error:
                raise Refusal(PROOF_UNAVAILABLE) from error
            unchanged(allow_producer_outputs=stage in ("restore", "plan", "build"))
            try:
                receipt = json.loads(read(scratch, "proof/" + stage + ".trace"))
                require(receipt == {"invocation": invocation_digest, "stage": stage,
                    "command": fixed_command(stage, scratch, settings), "executions": receipt.get("executions"),
                    "contract": contract_digest, "exit_code": receipt.get("exit_code"),
                    "execution_environments": receipt.get("execution_environments"),
                    "runtime_roots": receipt.get("runtime_roots"), "compiler_response": receipt.get("compiler_response"),
                    "toolchain_files": receipt.get("toolchain_files"),
                    "outputs": receipt.get("outputs"),
                    "syscall_stops": receipt.get("syscall_stops"), "network_attempts": 0, "complete": True}, PROOF_UNAVAILABLE)
                require(receipt["executions"] and receipt["executions"][0] == fixed_command(stage, scratch, settings)
                        and type(receipt["syscall_stops"]) is int and receipt["syscall_stops"] > 0, PROOF_UNAVAILABLE)
                require(receipt["outputs"] == output_hashes(scratch), PROOF_UNAVAILABLE)
                require(len(receipt["execution_environments"]) == len(receipt["executions"]), PROOF_UNAVAILABLE)
                for execution, actual_environment in zip(receipt["executions"], receipt["execution_environments"]):
                    verify_execution_environment(actual_environment, base_environment, selected_sdk,
                        compiler=selected_sdk is not None and execution[0] == str(selected_sdk / "Roslyn/bincore/csc"))
                require((stage == "sdk" or receipt["runtime_roots"]) and all(Path(root).is_relative_to(dotnet.parent / "shared")
                        and ".." not in Path(root).parts for root in receipt["runtime_roots"]), PROOF_UNAVAILABLE)
                for root in receipt["runtime_roots"]:
                    for name in ("libcoreclr.so", "System.Private.CoreLib.dll"):
                        path = Path(root) / name
                        identities[path] = fingerprint(path)
                require(receipt["toolchain_files"], PROOF_UNAVAILABLE)
                for item in receipt["toolchain_files"]:
                    path = Path(item)
                    require(path.is_relative_to(dotnet.parent) and ".." not in path.parts, PROOF_UNAVAILABLE)
                    if stage != "sdk":
                        require(path == dotnet or any(path.is_relative_to(root) for root in roots), PROOF_UNAVAILABLE)
                    identities[path] = fingerprint(path)
                if stage == "build":
                    require(receipt["compiler_response"] == hashlib.sha256(read(scratch, "proof/compiler.response")).hexdigest(),
                            PROOF_UNAVAILABLE)
                    compilers = [execution for execution in receipt["executions"]
                                 if execution and execution[0] == str(sdk / "Roslyn/bincore/csc")]
                    require(len(compilers) == 1 and len(compilers[0]) == 3
                            and compilers[0][1] == "/noconfig" and compilers[0][2].startswith("@"), PROOF_UNAVAILABLE)
            except (ValueError, TypeError, KeyError, AttributeError, Refusal) as error:
                raise Refusal(PROOF_UNAVAILABLE) from error
            stages.append(stage)
            require(receipt["exit_code"] == 0, "PROFILE_OWNER_VALIDATION_FAILED" if stage == "run" else PROOF_UNAVAILABLE)
            return output

        def classify(path):
            require(path.is_absolute(), PROOF_UNAVAILABLE)
            no_symbolic_ancestors(path)
            path = Path(os.path.abspath(path))
            if path.is_relative_to(snapshot):
                relative = path.relative_to(snapshot).as_posix()
                require(relative in dict(captured.hashes), PROOF_UNAVAILABLE)
                return "snapshot"
            if any(path.is_relative_to(root) for root in roots):
                identity = fingerprint(path)
                require(path not in identities or identities[path] == identity, PROOF_UNAVAILABLE)
                identities[path] = identity
                return "toolchain"
            if path.is_relative_to(scratch):
                relative = path.relative_to(scratch).as_posix()
                require(relative in produced, PROOF_UNAVAILABLE)
                return "generated"
            raise Refusal(PROOF_UNAVAILABLE)

        def inventory_outputs():
            # Only a completed fixed SDK producer can introduce private outputs.
            for name in ("obj", "bin", "generated"):
                base = scratch / name
                if base.exists():
                    for path in base.rglob("*"):
                        no_symbolic_ancestors(path)
                        if path.is_file():
                            relative = path.relative_to(scratch).as_posix()
                            produced[relative] = hashlib.sha256(read(scratch, relative, 16 * MAX_FILE, generated=True)).hexdigest()

        def tasks(stage):
            log = read(scratch, "proof/" + stage + ".tasks", 16 * MAX_FILE).decode("utf-8-sig")
            assemblies = re.findall(r'Using "[^"\n]+" task from assembly "([^"\n]+)"', log)
            require(assemblies, PROOF_UNAVAILABLE)
            for assembly in assemblies:
                if assembly == "Microsoft.Build.Tasks.Core, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a":
                    assembly = str(sdk / "Microsoft.Build.Tasks.Core.dll")
                require(Path(os.path.abspath(assembly)) in declared
                        and classify(Path(assembly)) == "toolchain", PROOF_UNAVAILABLE)
            return log

        def observations(stage):
            content = read(scratch, "proof/" + stage + ".items").decode("utf-8-sig")
            result = {}
            for line in content.splitlines():
                kind, separator, value = line.partition("|")
                require(separator and kind in ("source", "reference", "analyzer", "additional", "config", "import",
                        "response", "arguments", "toolpath", "toolexe", "cscenvironment", "package", "project", "complete"), PROOF_UNAVAILABLE)
                result.setdefault(kind, []).append(value)
                if kind in ("source", "reference", "analyzer", "additional", "config", "import"):
                    if kind == "import":
                        require(Path(os.path.abspath(value)) in known_imports, PROOF_UNAVAILABLE)
                    classify(Path(value))
                elif kind != "complete":
                    require(value == "", PROOF_UNAVAILABLE)
            require(result.get("complete") == ["profile-bridge-v1"] and result.get("reference")
                    and result.get("analyzer") and result.get("import"), PROOF_UNAVAILABLE)
            require(all(result.get(kind) == [""] for kind in ("response", "arguments", "toolpath", "toolexe", "cscenvironment", "package", "project")),
                    PROOF_UNAVAILABLE)
            require({str(snapshot / path) for path in (*SOURCES, PROGRAM)} <= set(result.get("source", [])), PROOF_UNAVAILABLE)
            return {key: sorted(value) for key, value in result.items()}

        version = child("sdk").decode("utf-8").strip()
        require(re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?", version) is not None, PROOF_UNAVAILABLE)
        sdk = dotnet.parent / "sdk" / version
        selected_sdk = sdk
        sdk_directories = sdk_resolution_directories(sdk)
        metadata = execution_metadata(dotnet, sdk)
        identities.update(metadata)
        # All SDK tasks/imports must resolve in this selected installation. Packs
        # and runtimes are trusted only below the same dotnet installation.
        roots = (sdk, dotnet.parent / "packs", dotnet.parent / "shared", dotnet.parent / "host")
        for root in roots:
            no_symbolic_ancestors(root)
            require(root.is_dir(), PROOF_UNAVAILABLE)
        require(all(path == dotnet or any(path.is_relative_to(root) for root in roots)
                    for path in identities), PROOF_UNAVAILABLE)
        environment["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"] = str(dotnet.parent)
        environment["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"] = str(sdk / "Sdks")
        child("preprocess")
        xml = read(scratch, "proof/preprocessed.xml", 16 * MAX_FILE).decode("utf-8-sig")
        imports = re.findall(r'^(/[^/\r\n][^\r\n]+)\r?$', xml, re.M)
        require(imports, PROOF_UNAVAILABLE)
        for path in imports:
            known_imports.add(Path(os.path.abspath(path.strip())))
            classify(Path(path.strip()))
        # Preflight evaluation performs no build target. Resolve every declared
        # task assembly before restore can invoke one; diagnostics later must be
        # a subset of these identities. Contextual SDK task declarations have
        # one fixed SDK location in this supported envelope.
        properties = json.loads(child("evaluate"))["Properties"]
        declared = {sdk / "Microsoft.Build.Tasks.Core.dll"}
        for node in ET.fromstring(xml).iter():
            if node.tag.rsplit("}", 1)[-1] != "UsingTask":
                continue
            require("TaskFactory" not in node.attrib and "AssemblyFile" in node.attrib, PROOF_UNAVAILABLE)
            assembly = node.attrib["AssemblyFile"]
            if assembly == "$(MSBuildThisFileDirectory)Microsoft.NET.ProjectData.Tasks.dll":
                assembly = str(sdk / "ProjectData/Microsoft.NET.ProjectData.Tasks.dll")
            else:
                assembly = re.sub(r"\$\(([A-Za-z_][A-Za-z0-9_]*)\)",
                                  lambda match: properties.get(match[1], "!unknown!"), assembly)
            # Crossgen's conditionally replaced declaration is inactive in this
            # non-publishing project; its active replacement is checked below.
            if node.attrib["AssemblyFile"] == "$(CrossGenTasksPath)" and assembly == "":
                continue
            require("$" not in assembly and "!unknown!" not in assembly, PROOF_UNAVAILABLE)
            candidate = Path(assembly.replace("\\", "/"))
            if not candidate.is_absolute():
                require(candidate.name in ("NuGet.Build.Tasks.dll", "NuGet.Build.Tasks.Pack.dll",
                                           "Microsoft.TestPlatform.Build.dll") and len(candidate.parts) == 1, PROOF_UNAVAILABLE)
                candidate = sdk / candidate
            path = Path(os.path.abspath(candidate))
            require(path.is_relative_to(sdk) and classify(path) == "toolchain", PROOF_UNAVAILABLE)
            declared.add(path)
        identities[sdk / "Roslyn/bincore/csc"] = fingerprint(sdk / "Roslyn/bincore/csc")
        child("restore")
        tasks("restore")
        inventory_outputs()
        child("plan")
        tasks("plan")
        inventory_outputs()
        plan = observations("plan")
        # Generated source/configuration and restore assets are now sealed to
        # their observed producer; a mutation refuses before compilation.
        child("build")
        tasks("build")
        inventory_outputs()
        require(observations("build") == plan, PROOF_UNAVAILABLE)
        assembly = "bin/ProductionProfileBridge.dll"
        require(assembly in produced, "PROFILE_OWNER_RESULT_INVALID")
        output = child("run")
        proof = InvocationProof(hashlib.sha256(repr(captured.hashes).encode()).hexdigest(),
                                invocation_digest, version, tuple(stages),
                                tuple(sorted([(str(path), identity) for path, identity in identities.items()]
                                    + [("directory:" + str(path), identity) for path, identity in directories.items()])))
        result = OwnerOutput(output)
        result.proof = proof
        result._context = object()
        global _ISSUED
        _ISSUED = (result, proof, hashlib.sha256(result).hexdigest(), proof.capture, proof.invocation, result._context, captured)
        return result


def execute(captured, inventory_digest, resolved_digest, infrastructure_digest):
    """Incomplete or unsupported observation always retains the proof refusal."""
    global _ISSUED
    _ISSUED = None
    try:
        return _execute(captured, inventory_digest, resolved_digest, infrastructure_digest)
    except Refusal as error:
        if error.code in ("PROFILE_OWNER_VALIDATION_FAILED", "PROFILE_OWNER_RESULT_INVALID"):
            raise
        raise Refusal(PROOF_UNAVAILABLE) from error
    except (OSError, ValueError, TypeError, KeyError, AttributeError, ET.ParseError) as error:
        raise Refusal(PROOF_UNAVAILABLE) from error


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 6 and sys.argv[1] == "--trace-private", PROOF_UNAVAILABLE)
        private = Path(sys.argv[2])
        require(private.is_absolute() and Path(__file__).absolute() == private / "snapshot/tools/profile_bridge_build.py", PROOF_UNAVAILABLE)
        trace_fixed_stage(private, sys.argv[3], sys.argv[4], sys.argv[5])
    except (Refusal, OSError, ValueError, KeyError, TypeError, AttributeError):
        print(PROOF_UNAVAILABLE)
        sys.exit(2)
