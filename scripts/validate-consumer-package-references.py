#!/usr/bin/env python3
"""Restore and build a package-only consumer for the EventStore release inventory."""

from __future__ import annotations

import argparse
import json
import pathlib
import subprocess
import sys
import tempfile
import textwrap
import xml.etree.ElementTree as ET
import zipfile


ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "tools" / "release-packages.json"


def load_expected_package_ids() -> list[str]:
    with MANIFEST.open("r", encoding="utf-8") as handle:
        data = json.load(handle)

    packages = data.get("packages")
    if not isinstance(packages, list) or not packages:
        raise ValueError(f"{MANIFEST} must contain a non-empty 'packages' array.")

    ids: list[str] = []
    seen: set[str] = set()
    for index, package in enumerate(packages, start=1):
        if not isinstance(package, dict):
            raise ValueError(f"Package entry #{index} must be an object.")

        package_id = str(package.get("id", "")).strip()
        if not package_id:
            raise ValueError(f"Package entry #{index} must define 'id'.")
        if not package_id.startswith("Hexalith.EventStore"):
            raise ValueError(f"Manifest package id is outside EventStore scope: {package_id}")
        if package_id in seen:
            raise ValueError(f"Duplicate package id in {MANIFEST}: {package_id}")

        seen.add(package_id)
        ids.append(package_id)

    return ids


def read_nuspec_identity(package_path: pathlib.Path) -> tuple[str, str, set[str]]:
    with zipfile.ZipFile(package_path, "r") as archive:
        nuspec_names = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise ValueError(f"{package_path.name} must contain exactly one .nuspec file.")

        with archive.open(nuspec_names[0]) as nuspec:
            root = ET.parse(nuspec).getroot()

    namespace = ""
    if root.tag.startswith("{"):
        namespace = root.tag[1:].split("}", 1)[0]

    metadata = root.find(f"{{{namespace}}}metadata") if namespace else root.find("metadata")
    if metadata is None:
        raise ValueError(f"{package_path.name} nuspec is missing metadata.")

    package_id = metadata.findtext(f"{{{namespace}}}id" if namespace else "id")
    version = metadata.findtext(f"{{{namespace}}}version" if namespace else "version")
    if not package_id or not version:
        raise ValueError(f"{package_path.name} nuspec must define id and version.")

    package_types: set[str] = set()
    package_types_element = metadata.find(f"{{{namespace}}}packageTypes" if namespace else "packageTypes")
    if package_types_element is not None:
        for package_type in package_types_element.findall(f"{{{namespace}}}packageType" if namespace else "packageType"):
            name = package_type.attrib.get("name")
            if name:
                package_types.add(name)

    return package_id, version, package_types


def validate_package_directory(package_path: pathlib.Path) -> tuple[list[str], str, dict[str, set[str]]]:
    if not package_path.is_dir():
        raise FileNotFoundError(f"Package directory does not exist: {package_path}")

    expected_ids = load_expected_package_ids()
    actual: dict[str, pathlib.Path] = {}
    package_types: dict[str, set[str]] = {}
    versions: set[str] = set()
    for nupkg in sorted(package_path.glob("*.nupkg")):
        package_id, version, types = read_nuspec_identity(nupkg)
        if package_id in actual:
            raise ValueError(f"Duplicate package output for {package_id}: {actual[package_id].name}, {nupkg.name}")
        actual[package_id] = nupkg
        package_types[package_id] = types
        versions.add(version)

    missing = sorted(set(expected_ids) - set(actual))
    extra = sorted(set(actual) - set(expected_ids))
    if missing or extra:
        if missing:
            print("Missing release packages:", file=sys.stderr)
            for package_id in missing:
                print(f"  {package_id}", file=sys.stderr)
        if extra:
            print("Unexpected release packages:", file=sys.stderr)
            for package_id in extra:
                print(f"  {package_id}", file=sys.stderr)
        raise ValueError("NuGet package output does not match tools/release-packages.json.")

    if len(versions) != 1:
        raise ValueError(f"Release packages must share one version. Found: {', '.join(sorted(versions))}")

    return expected_ids, next(iter(versions)), package_types


def write_consumer_project(consumer_dir: pathlib.Path, package_ids: list[str], version: str, package_source: pathlib.Path) -> pathlib.Path:
    package_versions = "\n".join(
        f'    <PackageVersion Include="{package_id}" Version="{version}" />'
        for package_id in package_ids)
    package_references = "\n".join(
        f'    <PackageReference Include="{package_id}" />'
        for package_id in package_ids)

    (consumer_dir / "Directory.Packages.props").write_text(
        textwrap.dedent(f"""\
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
        {package_versions}
          </ItemGroup>
        </Project>
        """),
        encoding="utf-8")

    (consumer_dir / "PackageConsumer.csproj").write_text(
        textwrap.dedent(f"""\
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
        {package_references}
          </ItemGroup>
        </Project>
        """),
        encoding="utf-8")

    (consumer_dir / "ConsumerProbe.cs").write_text(
        "namespace Hexalith.EventStore.PackageConsumer;\n\npublic sealed class ConsumerProbe;\n",
        encoding="utf-8")

    (consumer_dir / "nuget.config").write_text(
        textwrap.dedent(f"""\
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="local-release-packages" value="{package_source}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
        </configuration>
        """),
        encoding="utf-8")

    return consumer_dir / "PackageConsumer.csproj"


def run(command: list[str], cwd: pathlib.Path = ROOT) -> None:
    completed = subprocess.run(command, cwd=cwd, check=False)
    if completed.returncode != 0:
        raise subprocess.CalledProcessError(completed.returncode, completed.args)


def assert_assets_use_packages(project_path: pathlib.Path, package_ids: list[str], version: str) -> None:
    assets_path = project_path.parent / "obj" / "project.assets.json"
    with assets_path.open("r", encoding="utf-8") as handle:
        assets = json.load(handle)

    libraries = assets.get("libraries")
    if not isinstance(libraries, dict):
        raise ValueError(f"Consumer assets file is missing libraries: {assets_path}")

    project_libraries = [name for name, value in libraries.items() if isinstance(value, dict) and value.get("type") == "project"]
    if project_libraries:
        raise ValueError(f"Consumer restore resolved project references instead of packages: {', '.join(sorted(project_libraries))}")

    library_keys = {key.lower() for key in libraries}
    missing = [
        package_id
        for package_id in package_ids
        if f"{package_id}/{version}".lower() not in library_keys
    ]
    if missing:
        raise ValueError(f"Consumer restore did not resolve manifest packages: {', '.join(missing)}")


def validate_dotnet_tool_packages(consumer_dir: pathlib.Path, package_path: pathlib.Path, package_ids: list[str], version: str) -> None:
    if not package_ids:
        return

    run(["dotnet", "new", "tool-manifest", "--force"], cwd=consumer_dir)

    for package_id in package_ids:
        run([
            "dotnet",
            "tool",
            "install",
            package_id,
            "--version",
            version,
            "--add-source",
            str(package_path),
            "--add-source",
            "https://api.nuget.org/v3/index.json",
        ], cwd=consumer_dir)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", help="Directory containing manifest-built .nupkg files.")
    args = parser.parse_args()

    package_dir = pathlib.Path(args.package_directory)
    package_path = package_dir if package_dir.is_absolute() else ROOT / package_dir
    package_ids, version, package_types = validate_package_directory(package_path)
    tool_package_ids = [
        package_id
        for package_id in package_ids
        if "DotnetTool" in package_types.get(package_id, set())
    ]
    library_package_ids = [
        package_id
        for package_id in package_ids
        if package_id not in tool_package_ids
    ]

    with tempfile.TemporaryDirectory(prefix="eventstore-package-consumer-") as temp_dir_name:
        consumer_dir = pathlib.Path(temp_dir_name)
        project_path = write_consumer_project(consumer_dir, library_package_ids, version, package_path)

        run([
            "dotnet",
            "restore",
            str(project_path),
            "--configfile",
            str(consumer_dir / "nuget.config"),
            "--packages",
            str(consumer_dir / "packages"),
            "-p:UseHexalithProjectReferences=false",
        ])
        run([
            "dotnet",
            "build",
            str(project_path),
            "--no-restore",
            "--configuration",
            "Release",
            "-p:UseHexalithProjectReferences=false",
        ])

        assert_assets_use_packages(project_path, library_package_ids, version)

    with tempfile.TemporaryDirectory(prefix="eventstore-package-tool-consumer-") as temp_dir_name:
        consumer_dir = pathlib.Path(temp_dir_name)
        validate_dotnet_tool_packages(consumer_dir, package_path, tool_package_ids, version)

    print(f"Validated package-only consumer for {len(library_package_ids)} EventStore packages and {len(tool_package_ids)} tool packages at version {version}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - CI should print the exact release validation failure.
        print(f"validate-consumer-package-references: {error}", file=sys.stderr)
        raise SystemExit(1)
