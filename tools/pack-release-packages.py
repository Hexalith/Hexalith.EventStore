#!/usr/bin/env python3
"""Pack the EventStore NuGet release package inventory."""

from __future__ import annotations

import argparse
import json
import pathlib
import subprocess
import sys
from collections.abc import Iterable


ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "tools" / "release-packages.json"


def load_packages() -> list[dict[str, str]]:
    with MANIFEST.open("r", encoding="utf-8") as handle:
        data = json.load(handle)

    packages = data.get("packages")
    if not isinstance(packages, list) or not packages:
        raise ValueError(f"{MANIFEST} must contain a non-empty 'packages' array.")

    seen_ids: set[str] = set()
    seen_projects: set[str] = set()
    normalized: list[dict[str, str]] = []
    for index, package in enumerate(packages, start=1):
        if not isinstance(package, dict):
            raise ValueError(f"Package entry #{index} must be an object.")

        package_id = str(package.get("id", "")).strip()
        project = str(package.get("project", "")).strip()
        if not package_id or not project:
            raise ValueError(f"Package entry #{index} must define 'id' and 'project'.")

        if package_id in seen_ids:
            raise ValueError(f"Duplicate package id in {MANIFEST}: {package_id}")
        if project in seen_projects:
            raise ValueError(f"Duplicate project in {MANIFEST}: {project}")

        project_path = ROOT / project
        if not project_path.is_file():
            raise FileNotFoundError(f"Release package project does not exist: {project}")

        seen_ids.add(package_id)
        seen_projects.add(project)
        normalized.append({"id": package_id, "project": project})

    return normalized


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

    packages = load_packages()
    output = pathlib.Path(args.output)
    output_path = output if output.is_absolute() else ROOT / output
    if not args.dry_run:
        output_path.mkdir(parents=True, exist_ok=True)

    for package in packages:
        command = [
            "dotnet",
            "pack",
            package["project"],
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
