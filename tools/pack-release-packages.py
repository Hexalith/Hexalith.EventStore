#!/usr/bin/env python3
"""Pack the EventStore NuGet release package inventory."""

from __future__ import annotations

import argparse
import pathlib
import subprocess
import sys
from collections.abc import Iterable

from release_package_contract import load_release_manifest, validate_manifest_projects


ROOT = pathlib.Path(__file__).resolve().parents[1]


def run(command: Iterable[str]) -> None:
    completed = subprocess.run(list(command), cwd=ROOT, check=False)
    if completed.returncode != 0:
        raise subprocess.CalledProcessError(completed.returncode, completed.args)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", help="Directory that receives packed .nupkg files.")
    parser.add_argument("version", help="Semantic-release version to stamp into packages.")
    parser.add_argument("--dry-run", action="store_true", help="Validate and print package commands without packing.")
    args = parser.parse_args()

    packages = load_release_manifest()
    validate_manifest_projects(packages)
    output = pathlib.Path(args.output)
    output_path = output if output.is_absolute() else ROOT / output
    if not args.dry_run:
        output_path.mkdir(parents=True, exist_ok=True)

    for package in packages:
        command = [
            "dotnet",
            "pack",
            package.project,
            "--configuration",
            "Release",
            "--output",
            str(output_path),
            f"-p:Version={args.version}",
            "-p:GeneratePackageOnBuild=false",
            "-p:UseHexalithProjectReferences=false",
        ]
        print(" ".join(command), flush=True)
        if not args.dry_run:
            run(command)

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - CI should print the exact release validation failure.
        print(f"pack-release-packages: {error}", file=sys.stderr)
        raise SystemExit(1)
