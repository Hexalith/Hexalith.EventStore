#!/usr/bin/env python3
"""Required Release-package proof for the synthetic checkpointed process API."""

from __future__ import annotations

import pathlib
import subprocess
import sys
import tempfile


ROOT = pathlib.Path(__file__).resolve().parents[1]
VERSION = "999.0.0-process-ci-test"


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="eventstore-process-packages-") as folder:
        package_dir = pathlib.Path(folder)
        subprocess.run(
            [sys.executable, "scripts/pack-release-packages.py", str(package_dir), VERSION],
            cwd=ROOT,
            check=True,
        )
        subprocess.run(
            [
                sys.executable,
                "scripts/validate-consumer-package-references.py",
                str(package_dir),
                "--package", "Hexalith.EventStore.Contracts",
                "--package", "Hexalith.EventStore.Client",
                "--package", "Hexalith.EventStore.DomainService",
            ],
            cwd=ROOT,
            check=True,
        )
    print(f"Checkpointed process Release-package proof passed at {VERSION}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
