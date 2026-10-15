#!/usr/bin/env python3
"""Validate release package output against tools/release-packages.json."""

from __future__ import annotations

import argparse
import pathlib
import sys

from release_package_contract import validate_package_directory


ROOT = pathlib.Path(__file__).resolve().parents[1]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", help="Directory containing .nupkg files.")
    parser.add_argument("version", help="Expected package version.")
    args = parser.parse_args()

    package_dir = pathlib.Path(args.package_directory)
    package_path = package_dir if package_dir.is_absolute() else ROOT / package_dir
    packages, version = validate_package_directory(package_path, args.version)
    print(
        f"Validated {len(packages)} release packages at version {version} in {package_path}."
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - CI should print the exact release validation failure.
        print(f"validate-release-packages: {error}", file=sys.stderr)
        raise SystemExit(1)
