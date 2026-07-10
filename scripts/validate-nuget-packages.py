#!/usr/bin/env python3
"""Validate packed NuGet packages against tools/release-packages.json."""

from __future__ import annotations

import argparse
import json
import pathlib
import sys
import xml.etree.ElementTree as ET
import zipfile


ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "tools" / "release-packages.json"


def is_eventstore_package_id(package_id: str) -> bool:
    return package_id == "Hexalith.EventStore" or package_id.startswith("Hexalith.EventStore.")


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
        if not is_eventstore_package_id(package_id):
            raise ValueError(f"Manifest package id is outside EventStore scope: {package_id}")
        if package_id in seen:
            raise ValueError(f"Duplicate package id in {MANIFEST}: {package_id}")

        seen.add(package_id)
        ids.append(package_id)

    return ids


def read_nuspec_identity(package_path: pathlib.Path) -> tuple[str, str]:
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

    return package_id, version


def validate_package_directory(package_path: pathlib.Path) -> tuple[set[str], str]:
    if not package_path.is_dir():
        raise FileNotFoundError(f"Package directory does not exist: {package_path}")

    expected_ids = set(load_expected_package_ids())
    actual: dict[str, pathlib.Path] = {}
    versions: set[str] = set()

    for nupkg in sorted(package_path.glob("*.nupkg")):
        package_id, version = read_nuspec_identity(nupkg)
        if package_id in actual:
            raise ValueError(f"Duplicate package output for {package_id}: {actual[package_id].name}, {nupkg.name}")

        expected_name = f"{package_id}.{version}.nupkg"
        if nupkg.name != expected_name:
            raise ValueError(f"{nupkg.name} should be named {expected_name}.")

        actual[package_id] = nupkg
        versions.add(version)

    actual_ids = set(actual)
    missing = sorted(expected_ids - actual_ids)
    extra = sorted(actual_ids - expected_ids)
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

    return actual_ids, next(iter(versions))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", help="Directory containing .nupkg files.")
    args = parser.parse_args()

    package_dir = pathlib.Path(args.package_directory)
    package_path = package_dir if package_dir.is_absolute() else ROOT / package_dir
    actual_ids, version = validate_package_directory(package_path)

    print(f"Validated {len(actual_ids)} EventStore NuGet packages at version {version} in {package_path}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - CI should print the exact release validation failure.
        print(f"validate-nuget-packages: {error}", file=sys.stderr)
        raise SystemExit(1)
