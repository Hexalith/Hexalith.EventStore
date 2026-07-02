#!/usr/bin/env python3
"""Validate release package output against tools/release-packages.json."""

from __future__ import annotations

import argparse
import json
import pathlib
import sys


ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "tools" / "release-packages.json"


def expected_package_ids() -> set[str]:
    with MANIFEST.open("r", encoding="utf-8") as handle:
        data = json.load(handle)

    packages = data.get("packages")
    if not isinstance(packages, list) or not packages:
        raise ValueError(f"{MANIFEST} must contain a non-empty 'packages' array.")

    ids: set[str] = set()
    for index, package in enumerate(packages, start=1):
        if not isinstance(package, dict):
            raise ValueError(f"Package entry #{index} must be an object.")
        package_id = str(package.get("id", "")).strip()
        if not package_id:
            raise ValueError(f"Package entry #{index} must define 'id'.")
        if package_id in ids:
            raise ValueError(f"Duplicate package id in {MANIFEST}: {package_id}")
        ids.add(package_id)

    return ids


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", help="Directory containing .nupkg files.")
    parser.add_argument("version", help="Expected package version.")
    args = parser.parse_args()

    package_dir = pathlib.Path(args.package_directory)
    package_path = package_dir if package_dir.is_absolute() else ROOT / package_dir
    if not package_path.is_dir():
        raise FileNotFoundError(f"Package directory does not exist: {package_path}")

    expected = expected_package_ids()
    expected_files = {f"{package_id}.{args.version}.nupkg" for package_id in expected}
    actual_files = {path.name for path in package_path.glob("*.nupkg")}

    missing = sorted(expected_files - actual_files)
    extra = sorted(actual_files - expected_files)

    if missing or extra:
        if missing:
            print("Missing release packages:", file=sys.stderr)
            for name in missing:
                print(f"  {name}", file=sys.stderr)
        if extra:
            print("Unexpected release packages:", file=sys.stderr)
            for name in extra:
                print(f"  {name}", file=sys.stderr)
        return 1

    print(f"Validated {len(actual_files)} release packages in {package_path}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - CI should print the exact release validation failure.
        print(f"validate-release-packages: {error}", file=sys.stderr)
        raise SystemExit(1)
