#!/usr/bin/env python3
"""Shared manifest and NuGet archive contract for EventStore releases."""

from __future__ import annotations

import json
import pathlib
import re
import subprocess
import time
import xml.etree.ElementTree as ET
import zipfile
from collections.abc import Iterable
from dataclasses import dataclass


ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "tools" / "release-packages.json"
EVENTSTORE_PACKAGE_PREFIX = "Hexalith.EventStore."
GATEWAY_PACKAGE_ID = "Hexalith.EventStore.Gateway"
GATEWAY_REQUIRED_DEPENDENCIES = frozenset(
    {
        "Hexalith.EventStore.Admin.Abstractions",
        "Hexalith.EventStore.Contracts",
        "Hexalith.EventStore.Server",
        "Hexalith.EventStore.ServiceDefaults",
    }
)
DOTNET_TOOL_PACKAGE_TYPE = "DotnetTool"
TOOL_PACKAGE_IDS = frozenset({"Hexalith.EventStore.Admin.Cli"})
"""Manifest packages that ship as .NET tools.

Tool packages emit a self-contained closure instead of dependency metadata, so
the dependency contracts are waived for them. Membership is decided here rather
than from the archive's own ``<packageTypes>`` element: a waiver keyed on
attacker- or accident-controlled metadata would let any archive switch off the
proof it is being subjected to.
"""

PROJECT_EVALUATION_TIMEOUT_SECONDS = 300
MANIFEST_EVALUATION_BUDGET_SECONDS = 900
_PACKAGE_ID_PATTERN = re.compile(r"^[A-Za-z0-9_.-]+$")
_DRIVE_QUALIFIED_PATTERN = re.compile(r"^[A-Za-z]:")
_PROJECT_METADATA_PATTERN = re.compile(
    r"(?:ProjectReference|\.(?:cs|fs|vb)proj(?:\b|$)|"
    r"(?:^|[\s;=\"'>])(?:\.\.[\\/]|~[\\/]|(?:src|references|bin|obj|artifacts)[\\/]"
    r"|[A-Za-z]:[\\/]|\\\\[^\\/\s]|/(?![/\s>])))",
    re.IGNORECASE,
)
"""Reject source paths in attribute values and in element text.

The boundary class includes ``>`` because serialized XML always places a
closing angle bracket immediately before element text; without it a checkout
path in element position (``<description>``, ``<icon>``, ``<projectUrl>``)
would never be examined. ``bin/``, ``obj/`` and ``artifacts/`` are the output
directories a real ``dotnet pack`` leak would carry, and none of them is
rooted, so the rooted-path alternatives alone would accept them.
"""


@dataclass(frozen=True)
class ManifestPackage:
    """One normalized release-manifest entry."""

    package_id: str
    project: str
    project_path: pathlib.Path


@dataclass(frozen=True)
class PackageDependency:
    """One dependency declared in embedded NuGet metadata."""

    package_id: str
    version: str | None
    target_framework: str | None


@dataclass(frozen=True)
class PackageMetadata:
    """Validated metadata read from one NuGet archive."""

    path: pathlib.Path
    package_id: str
    version: str
    dependencies: tuple[PackageDependency, ...]
    dependency_groups: tuple[str | None, ...]
    package_types: frozenset[str]


def is_eventstore_package_id(package_id: str) -> bool:
    """Return whether a package ID belongs to the EventStore namespace."""

    return package_id == "Hexalith.EventStore" or package_id.startswith(EVENTSTORE_PACKAGE_PREFIX)


def load_release_manifest(
    manifest_path: pathlib.Path = MANIFEST,
    repository_root: pathlib.Path = ROOT,
) -> list[ManifestPackage]:
    """Load and fail-closed normalize the authoritative package inventory."""

    manifest_path = manifest_path.resolve()
    repository_root = repository_root.resolve()
    source_root = (repository_root / "src").resolve()
    with manifest_path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)

    packages = data.get("packages") if isinstance(data, dict) else None
    if not isinstance(packages, list) or not packages:
        raise ValueError(f"{manifest_path} must contain a non-empty 'packages' array.")

    seen_ids: set[str] = set()
    seen_projects: set[pathlib.Path] = set()
    normalized: list[ManifestPackage] = []
    for index, package in enumerate(packages, start=1):
        if not isinstance(package, dict):
            raise ValueError(f"Package entry #{index} must be an object.")

        raw_id = package.get("id")
        raw_project = package.get("project")
        if not isinstance(raw_id, str) or not isinstance(raw_project, str):
            raise ValueError(f"Package entry #{index} must define string 'id' and 'project' values.")

        package_id = raw_id.strip()
        project = raw_project.strip()
        if not package_id or not project:
            raise ValueError(f"Package entry #{index} must define non-empty 'id' and 'project' values.")
        if not is_eventstore_package_id(package_id) or not _PACKAGE_ID_PATTERN.fullmatch(package_id):
            raise ValueError(f"Manifest package id is outside EventStore scope: {package_id}")

        project_parts = pathlib.PurePosixPath(project.replace("\\", "/"))
        if project_parts.is_absolute() or ".." in project_parts.parts or project_parts.suffix != ".csproj":
            raise ValueError(f"Manifest project path is not a normalized relative .csproj path: {project}")

        project_path = (repository_root / project_parts).resolve()
        try:
            project_path.relative_to(source_root)
        except ValueError as error:
            raise ValueError(f"Manifest project is outside the root-owned src directory: {project}") from error

        normalized_project = project_path.relative_to(repository_root).as_posix()
        if project != normalized_project:
            raise ValueError(f"Manifest project path must be normalized as {normalized_project}: {project}")
        if not project_path.is_file():
            raise FileNotFoundError(f"Release package project does not exist: {project}")

        id_key = package_id.casefold()
        if id_key in seen_ids:
            raise ValueError(f"Duplicate package id in {manifest_path}: {package_id}")
        if project_path in seen_projects:
            raise ValueError(f"Duplicate project in {manifest_path}: {project}")

        seen_ids.add(id_key)
        seen_projects.add(project_path)
        normalized.append(ManifestPackage(package_id, project, project_path))

    return normalized


def evaluate_project_properties(
    package: ManifestPackage,
    properties: Iterable[str],
    timeout_seconds: float = PROJECT_EVALUATION_TIMEOUT_SECONDS,
) -> dict[str, str]:
    """Evaluate project properties using the same safe release-mode inputs as packing."""

    property_names = tuple(properties)
    command = [
        "dotnet",
        "msbuild",
        str(package.project_path),
        f"-getProperty:{','.join(property_names)}",
        "-p:Configuration=Release",
        "-p:GeneratePackageOnBuild=false",
        "-p:UseHexalithProjectReferences=false",
        "--nologo",
    ]
    try:
        completed = subprocess.run(
            command,
            cwd=ROOT,
            capture_output=True,
            check=False,
            text=True,
            timeout=timeout_seconds,
        )
    except subprocess.TimeoutExpired as error:
        raise ValueError(
            f"Evaluating {package.project} timed out after "
            f"{timeout_seconds:.0f} seconds."
        ) from error
    if completed.returncode != 0:
        detail = completed.stderr.strip() or completed.stdout.strip()
        raise ValueError(f"Could not evaluate {package.project}: {detail}")

    try:
        payload = json.loads(completed.stdout)
        values = payload["Properties"]
    except (json.JSONDecodeError, KeyError, TypeError) as error:
        raise ValueError(f"Could not parse evaluated properties for {package.project}.") from error

    return {name: str(values.get(name, "")).strip() for name in property_names}


def validate_manifest_projects(packages: Iterable[ManifestPackage]) -> None:
    """Prove all manifest projects are packable and produce their declared IDs."""

    failures: list[str] = []
    # The per-project timeout alone lets a whole-inventory sweep stall semantic-release
    # prepare for fourteen times that budget, so the sweep carries its own deadline.
    deadline = time.monotonic() + MANIFEST_EVALUATION_BUDGET_SECONDS
    for package in packages:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise ValueError(
                "Release package project evaluation exceeded its whole-inventory budget of "
                f"{MANIFEST_EVALUATION_BUDGET_SECONDS} seconds at {package.project}."
            )
        properties = evaluate_project_properties(
            package,
            ("IsPackable", "PackageId"),
            min(PROJECT_EVALUATION_TIMEOUT_SECONDS, remaining),
        )
        if properties["IsPackable"].casefold() != "true":
            failures.append(f"{package.project} evaluates IsPackable={properties['IsPackable'] or '<empty>'}")
        if properties["PackageId"] != package.package_id:
            failures.append(
                f"{package.project} evaluates PackageId={properties['PackageId'] or '<empty>'}, "
                f"expected {package.package_id}"
            )

    if failures:
        raise ValueError("Invalid release package projects:\n  " + "\n  ".join(failures))


def _metadata_element(root: ET.Element, archive_name: str) -> tuple[ET.Element, str]:
    namespace = ""
    if root.tag.startswith("{"):
        namespace = root.tag[1:].split("}", 1)[0]
    metadata = root.find(f"{{{namespace}}}metadata") if namespace else root.find("metadata")
    if metadata is None:
        raise ValueError(f"{archive_name} nuspec is missing metadata.")
    return metadata, namespace


def _qualified(name: str, namespace: str) -> str:
    return f"{{{namespace}}}{name}" if namespace else name


def _validate_archive_paths(archive: zipfile.ZipFile, package_path: pathlib.Path) -> None:
    for name in archive.namelist():
        normalized = name.replace("\\", "/")
        parts = pathlib.PurePosixPath(normalized).parts
        if (
            name != normalized
            or pathlib.PurePosixPath(normalized).is_absolute()
            or pathlib.PureWindowsPath(normalized).is_absolute()
            # A drive-relative entry such as `C:leak.txt` is absolute under neither
            # flavour, yet it still escapes the package root when extracted on Windows.
            or _DRIVE_QUALIFIED_PATTERN.match(normalized) is not None
            or ".." in parts
            or normalized.casefold().endswith((".csproj", ".fsproj", ".vbproj"))
        ):
            raise ValueError(f"{package_path.name} contains a local or unsafe archive path: {name}")


def read_package_metadata(package_path: pathlib.Path) -> PackageMetadata:
    """Read and validate identity, dependency, and source-leak metadata from an archive."""

    with zipfile.ZipFile(package_path, "r") as archive:
        _validate_archive_paths(archive, package_path)
        nuspec_names = [name for name in archive.namelist() if name.casefold().endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise ValueError(f"{package_path.name} must contain exactly one .nuspec file.")
        nuspec_bytes = archive.read(nuspec_names[0])

    try:
        root = ET.fromstring(nuspec_bytes)
    except ET.ParseError as error:
        raise ValueError(f"{package_path.name} contains invalid nuspec XML: {error}") from error

    metadata, namespace = _metadata_element(root, package_path.name)
    package_id = (metadata.findtext(_qualified("id", namespace)) or "").strip()
    version = (metadata.findtext(_qualified("version", namespace)) or "").strip()
    if not package_id or not version:
        raise ValueError(f"{package_path.name} nuspec must define id and version.")
    if not _PACKAGE_ID_PATTERN.fullmatch(package_id):
        raise ValueError(f"{package_path.name} contains an invalid embedded package id: {package_id}")

    # Scan the whole document, not just <metadata>: a sibling element such as
    # <files> is equally part of the shipped nuspec.
    nuspec_text = ET.tostring(root, encoding="unicode")
    leak = _PROJECT_METADATA_PATTERN.search(nuspec_text)
    if leak is not None:
        raise ValueError(
            f"{package_path.name} nuspec contains local project or source-path metadata: "
            f"{leak.group(0).strip()!r}"
        )

    dependencies: list[PackageDependency] = []
    dependency_groups: list[str | None] = []
    dependencies_element = metadata.find(_qualified("dependencies", namespace))
    if dependencies_element is not None:
        has_direct_dependencies = any(
            child.tag == _qualified("dependency", namespace) for child in dependencies_element
        )
        has_blank_target_group = any(
            child.tag == _qualified("group", namespace)
            and not (child.attrib.get("targetFramework") or "").strip()
            for child in dependencies_element
        )
        if has_direct_dependencies and has_blank_target_group:
            raise ValueError(
                f"{package_path.name} mixes direct ungrouped dependencies with a dependency "
                "group whose target framework is blank."
            )

        seen_group_frameworks: set[str] = set()
        # Keyed by target framework so duplicate dependency IDs are caught across
        # ungrouped <dependency> children. Repeated groups are rejected separately
        # before their dependency sets can be unioned into a false contract pass.
        seen_ids_by_framework: dict[str | None, set[str]] = {}
        for child in dependencies_element:
            if child.tag == _qualified("dependency", namespace):
                dependency_elements = [child]
                target_framework = None
            elif child.tag == _qualified("group", namespace):
                dependency_elements = child.findall(_qualified("dependency", namespace))
                target_framework = (child.attrib.get("targetFramework") or "").strip() or None
                framework_key = (target_framework or "").casefold()
                if framework_key in seen_group_frameworks:
                    framework = target_framework or "ungrouped"
                    raise ValueError(
                        f"{package_path.name} declares repeated dependency group for "
                        f"target framework {framework}."
                    )
                seen_group_frameworks.add(framework_key)
            else:
                continue

            if target_framework not in dependency_groups:
                dependency_groups.append(target_framework)

            seen_group_ids = seen_ids_by_framework.setdefault(target_framework, set())
            for dependency in dependency_elements:
                dependency_id = (dependency.attrib.get("id") or "").strip()
                dependency_version = (dependency.attrib.get("version") or "").strip() or None
                if not dependency_id or not _PACKAGE_ID_PATTERN.fullmatch(dependency_id):
                    raise ValueError(
                        f"{package_path.name} contains an invalid dependency id: "
                        f"{dependency_id or '<empty>'}"
                    )
                dependency_key = dependency_id.casefold()
                if dependency_key in seen_group_ids:
                    framework = target_framework or "ungrouped"
                    raise ValueError(
                        f"{package_path.name} declares duplicate dependency {dependency_id} "
                        f"in {framework} metadata."
                    )
                seen_group_ids.add(dependency_key)
                dependencies.append(PackageDependency(dependency_id, dependency_version, target_framework))

    package_types: set[str] = set()
    package_types_element = metadata.find(_qualified("packageTypes", namespace))
    if package_types_element is not None:
        for package_type in package_types_element.findall(_qualified("packageType", namespace)):
            name = (package_type.attrib.get("name") or "").strip()
            if name:
                package_types.add(name)

    return PackageMetadata(
        package_path,
        package_id,
        version,
        tuple(dependencies),
        tuple(dependency_groups),
        frozenset(package_types),
    )


def _manifest_project_dependencies(
    manifest: Iterable[ManifestPackage],
) -> dict[str, frozenset[str]]:
    """Map each manifest project to its root-owned manifest ProjectReference edges."""

    packages = tuple(manifest)
    ids_by_project = {package.project_path: package.package_id for package in packages}
    expected: dict[str, frozenset[str]] = {}
    for package in packages:
        try:
            project = ET.parse(package.project_path)
        except ET.ParseError as error:
            raise ValueError(f"Could not parse release project {package.project}: {error}") from error

        dependency_ids: set[str] = set()
        for reference in project.getroot().iter("ProjectReference"):
            include = (reference.attrib.get("Include") or "").strip()
            if not include or "$" in include:
                continue
            reference_path = (package.project_path.parent / include.replace("\\", "/")).resolve()
            dependency_id = ids_by_project.get(reference_path)
            if dependency_id is not None:
                dependency_ids.add(dependency_id)
        expected[package.package_id] = frozenset(dependency_ids)

    return expected


def _manifest_external_hexalith_dependencies(
    manifest: Iterable[ManifestPackage],
) -> dict[str, frozenset[str]]:
    """Map direct package-mode references to Hexalith packages outside this inventory."""

    expected: dict[str, frozenset[str]] = {}
    for package in manifest:
        try:
            project = ET.parse(package.project_path)
        except ET.ParseError as error:
            raise ValueError(f"Could not parse release project {package.project}: {error}") from error

        dependency_ids = {
            include
            for reference in project.getroot().iter("PackageReference")
            if (include := (reference.attrib.get("Include") or "").strip()).startswith("Hexalith.")
            and not is_eventstore_package_id(include)
        }
        expected[package.package_id] = frozenset(dependency_ids)

    return expected


def _validate_package_type_contract(package: PackageMetadata) -> None:
    """Bind the tool-package waiver to the manifest instead of to archive metadata."""

    declares_tool = DOTNET_TOOL_PACKAGE_TYPE in package.package_types
    is_tool_package = package.package_id in TOOL_PACKAGE_IDS
    if declares_tool and not is_tool_package:
        raise ValueError(
            f"{package.package_id} declares the {DOTNET_TOOL_PACKAGE_TYPE} package type but is not a "
            "manifest tool package; dependency proof must not be waived by archive metadata."
        )
    if is_tool_package and not declares_tool:
        raise ValueError(
            f"{package.package_id} is a manifest tool package but does not declare the "
            f"{DOTNET_TOOL_PACKAGE_TYPE} package type."
        )


def _validate_internal_dependencies(
    package: PackageMetadata,
    expected_dependencies: frozenset[str],
    manifest_ids_by_key: dict[str, str],
    release_version: str,
) -> None:
    """Require canonical, same-release internal edges in every dependency group."""

    if package.package_id in TOOL_PACKAGE_IDS:
        expected_dependencies = frozenset()

    dependencies_by_group: dict[str | None, list[PackageDependency]] = {
        group: [] for group in package.dependency_groups
    }
    for dependency in package.dependencies:
        canonical_id = manifest_ids_by_key.get(dependency.package_id.casefold())
        if canonical_id is None:
            continue
        if dependency.package_id != canonical_id:
            raise ValueError(
                f"{package.package_id} dependency id must use canonical casing {canonical_id}: "
                f"{dependency.package_id}"
            )
        dependencies_by_group.setdefault(dependency.target_framework, []).append(dependency)

    if expected_dependencies and not dependencies_by_group:
        raise ValueError(
            f"{package.package_id} internal dependency contract failed: missing "
            + ", ".join(sorted(expected_dependencies))
        )

    groups = dependencies_by_group.items() if dependencies_by_group else [(None, [])]
    for target_framework, dependencies in groups:
        actual_dependencies = {dependency.package_id for dependency in dependencies}
        missing = sorted(expected_dependencies - actual_dependencies)
        extra = sorted(actual_dependencies - expected_dependencies)
        version_drift = sorted(
            f"{dependency.package_id}={dependency.version or '<empty>'}"
            for dependency in dependencies
            if dependency.version != release_version
        )
        if missing or extra or version_drift:
            details: list[str] = []
            if missing:
                details.append("missing " + ", ".join(missing))
            if extra:
                details.append("unexpected " + ", ".join(extra))
            if version_drift:
                details.append(
                    f"expected version {release_version}, found " + ", ".join(version_drift)
                )
            framework = target_framework or "ungrouped"
            raise ValueError(
                f"{package.package_id} internal dependency contract failed in {framework}: "
                + "; ".join(details)
            )


def _validate_external_hexalith_dependencies(
    package: PackageMetadata,
    expected_dependencies: frozenset[str],
) -> None:
    """Require direct external Hexalith package-mode references in every dependency group."""

    if package.package_id in TOOL_PACKAGE_IDS:
        # Tool packages ship a self-contained closure and emit no dependency
        # metadata, exactly as the internal-edge contract already allows.
        return
    if not expected_dependencies:
        return

    dependencies_by_group: dict[str | None, list[PackageDependency]] = {
        group: [] for group in package.dependency_groups
    }
    for dependency in package.dependencies:
        if dependency.package_id in expected_dependencies:
            dependencies_by_group.setdefault(dependency.target_framework, []).append(dependency)

    if not dependencies_by_group:
        raise ValueError(
            f"{package.package_id} external Hexalith dependency contract failed: missing "
            + ", ".join(sorted(expected_dependencies))
        )

    for target_framework, dependencies in dependencies_by_group.items():
        actual_dependencies = {dependency.package_id for dependency in dependencies}
        missing = sorted(expected_dependencies - actual_dependencies)
        missing_versions = sorted(
            dependency.package_id for dependency in dependencies if dependency.version is None
        )
        if missing or missing_versions:
            details: list[str] = []
            if missing:
                details.append("missing " + ", ".join(missing))
            if missing_versions:
                details.append("missing versions for " + ", ".join(missing_versions))
            framework = target_framework or "ungrouped"
            raise ValueError(
                f"{package.package_id} external Hexalith dependency contract failed in {framework}: "
                + "; ".join(details)
            )


def validate_package_directory(
    package_path: pathlib.Path,
    expected_version: str | None = None,
) -> tuple[list[PackageMetadata], str]:
    """Validate exact archive output against the manifest and embedded metadata."""

    package_path = package_path.resolve()
    if not package_path.is_dir():
        raise FileNotFoundError(f"Package directory does not exist: {package_path}")
    if expected_version is not None and not expected_version.strip():
        raise ValueError("Expected package version must not be blank.")

    manifest = load_release_manifest()
    expected_by_key = {package.package_id.casefold(): package.package_id for package in manifest}
    actual_by_key: dict[str, PackageMetadata] = {}
    versions: set[str] = set()
    nupkgs = sorted(
        path
        for path in package_path.iterdir()
        if path.is_file() and path.suffix.casefold() == ".nupkg"
    )
    for nupkg in nupkgs:
        metadata = read_package_metadata(nupkg)
        key = metadata.package_id.casefold()
        if key in actual_by_key:
            first = actual_by_key[key].path.name
            raise ValueError(f"Duplicate package output for {metadata.package_id}: {first}, {nupkg.name}")

        canonical_id = expected_by_key.get(key)
        if canonical_id is not None and metadata.package_id != canonical_id:
            raise ValueError(
                f"{nupkg.name} embedded package id must use canonical manifest casing "
                f"{canonical_id}: {metadata.package_id}"
            )

        _validate_package_type_contract(metadata)

        expected_name = f"{metadata.package_id}.{metadata.version}.nupkg"
        if nupkg.name != expected_name:
            raise ValueError(f"Renamed package archive {nupkg.name}; embedded metadata requires {expected_name}.")
        if expected_version is not None and metadata.version != expected_version:
            raise ValueError(
                f"{nupkg.name} has embedded version {metadata.version}; expected {expected_version}."
            )

        actual_by_key[key] = metadata
        versions.add(metadata.version)

    missing = sorted(expected_by_key[key] for key in expected_by_key.keys() - actual_by_key.keys())
    extra = sorted(metadata.package_id for key, metadata in actual_by_key.items() if key not in expected_by_key)
    if missing or extra:
        details: list[str] = []
        if missing:
            details.append("Missing release packages: " + ", ".join(missing))
        if extra:
            details.append("Unexpected release packages: " + ", ".join(extra))
        raise ValueError("NuGet package output does not match tools/release-packages.json. " + " ".join(details))

    if len(versions) != 1:
        found = ", ".join(sorted(versions)) or "<none>"
        raise ValueError(f"Release packages must share one version. Found: {found}")
    version = next(iter(versions))

    expected_dependencies = _manifest_project_dependencies(manifest)
    expected_external_dependencies = _manifest_external_hexalith_dependencies(manifest)
    for metadata in actual_by_key.values():
        _validate_internal_dependencies(
            metadata,
            expected_dependencies[metadata.package_id],
            expected_by_key,
            version,
        )
        _validate_external_hexalith_dependencies(
            metadata,
            expected_external_dependencies[metadata.package_id],
        )

    if expected_dependencies.get(GATEWAY_PACKAGE_ID) != GATEWAY_REQUIRED_DEPENDENCIES:
        raise ValueError(
            f"{GATEWAY_PACKAGE_ID} project graph does not match its explicit four-edge release contract."
        )

    unknown_tool_packages = sorted(TOOL_PACKAGE_IDS - {package.package_id for package in manifest})
    if unknown_tool_packages:
        raise ValueError(
            "Tool package contract names packages outside the manifest: "
            + ", ".join(unknown_tool_packages)
        )

    ordered = [actual_by_key[package.package_id.casefold()] for package in manifest]
    return ordered, version
