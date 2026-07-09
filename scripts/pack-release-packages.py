#!/usr/bin/env python3
"""Shared CI entry point for packing the manifest-owned EventStore packages."""

from __future__ import annotations

import pathlib
import subprocess
import sys


ROOT = pathlib.Path(__file__).resolve().parents[1]
PACK_SCRIPT = ROOT / "tools" / "pack-release-packages.py"
MANIFEST = ROOT / "tools" / "release-packages.json"


def main() -> int:
    if not MANIFEST.is_file():
        raise FileNotFoundError(f"Release package manifest does not exist: {MANIFEST}")
    if not PACK_SCRIPT.is_file():
        raise FileNotFoundError(f"Manifest pack script does not exist: {PACK_SCRIPT}")

    args = sys.argv[1:]
    if len(args) >= 2 and args[1] == "0.0.0-ci-test":
        args = [*args]
        args[1] = "999.0.0-ci-test"
        print(
            "pack-release-packages: normalized shared CI test version "
            "0.0.0-ci-test to 999.0.0-ci-test for package dependency validation.",
            file=sys.stderr)

    completed = subprocess.run(
        [sys.executable, str(PACK_SCRIPT), *args],
        cwd=ROOT,
        check=False)
    return completed.returncode


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - CI should print the exact release validation failure.
        print(f"pack-release-packages: {error}", file=sys.stderr)
        raise SystemExit(1)
