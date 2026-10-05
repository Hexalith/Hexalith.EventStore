#!/usr/bin/env python3
"""Check the current Dapr-only preparation boundary, without granting activation.

The approved 6.5 verifier remains a separate historical-design check. This gate
checks application dependency and mutation ownership at the current source, and
tests each negative policy input in a fresh process with an explicit timeout.
Functional, compatibility and live-component evidence remain separate gates.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess
import sys
import xml.etree.ElementTree as ET


ROOT = pathlib.Path(__file__).resolve().parents[1]
SERVER = pathlib.Path("src/Hexalith.EventStore.Server")
SERVER_PROJECT = SERVER / "Hexalith.EventStore.Server.csproj"
BUILD_CATALOG = pathlib.Path("references/Hexalith.Builds/Props/Directory.Packages.props")
PERSISTER = SERVER / "Events/EventPersister.cs"
LOGICAL_READER = SERVER / "Events/DaprLogicalEventReader.cs"
AMENDMENT = pathlib.Path("_bmad-output/implementation-artifacts/story-6-6-dapr-only-amendment.md")
MUTATIONS = {
    "application-sql": "application-storage-boundary",
    "server-npgsql": "server-dependencies",
    "catalog-npgsql": "story-dependency-footprint",
    "persister-save": "actor-save-ownership",
    "v2-admission": "dormant-v2-writer",
}


class BoundaryFailure(Exception):
    """One current-boundary policy violation, carrying its owning check."""

    def __init__(self, check: str, detail: str):
        super().__init__(detail)
        self.check = check


def read(path: pathlib.Path, mutation: str | None) -> str:
    """Load real source; mutation probes alter only a process-private string."""
    value = (ROOT / path).read_text(encoding="utf-8")
    if mutation == "application-sql" and path == LOGICAL_READER:
        value += "\nusing Npgsql;\n"
    elif mutation == "server-npgsql" and path == SERVER_PROJECT:
        value = value.replace("</Project>", '<ItemGroup><PackageReference Include="Npgsql" /></ItemGroup></Project>')
    elif mutation == "catalog-npgsql" and path == BUILD_CATALOG:
        value = value.replace("</Project>", '<ItemGroup><PackageVersion Include="Npgsql" Version="10.0.3" /></ItemGroup></Project>')
    elif mutation == "persister-save" and path == PERSISTER:
        value += "\nawait stateManager.SaveStateAsync(cancellationToken);\n"
    elif mutation == "v2-admission" and path == PERSISTER:
        value = value.replace("if (metadataVersion == 2)", "if (metadataVersion == 3)")
    return value


def require_no_dependency(path: pathlib.Path, element_name: str, check: str, mutation: str | None) -> None:
    """Reject the withdrawn direct database package from its two owning inventories."""
    tree = ET.fromstring(read(path, mutation))
    for element in tree.iter(element_name):
        if element.get("Include", "").casefold() == "npgsql":
            raise BoundaryFailure(check, f"{path}: withdrawn Npgsql dependency remains")


def verify(mutation: str | None = None) -> dict:
    """Validate only current local policy, leaving unproven obligations open."""
    require_no_dependency(SERVER_PROJECT, "PackageReference", "server-dependencies", mutation)
    require_no_dependency(BUILD_CATALOG, "PackageVersion", "story-dependency-footprint", mutation)
    forbidden = re.compile(r"\b(?:using\s+Npgsql|NpgsqlConnection|NpgsqlDataSource|SqlConnection|DbConnection)\b")
    source_count = 0
    for path in sorted((ROOT / SERVER).rglob("*.cs")):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        relative = path.relative_to(ROOT)
        source_count += 1
        if forbidden.search(read(relative, mutation)):
            raise BoundaryFailure("application-storage-boundary", f"{relative}: application database access is forbidden")

    persister = read(PERSISTER, mutation)
    if re.search(r"\bSaveStateAsync\s*\(", persister):
        raise BoundaryFailure("actor-save-ownership", "EventPersister must stage state; AggregateActor owns saving")
    fence = re.search(r"if\s*\(metadataVersion\s*==\s*2\)\s*\{\s*throw\s+new\s+InvalidOperationException\(", persister)
    first_read = persister.index(".TryGetStateAsync<AggregateMetadata>")
    if fence is None or fence.start() > first_read:
        raise BoundaryFailure("dormant-v2-writer", "V2 admission must refuse before actor metadata read and mutation")

    amendment = (ROOT / AMENDMENT).read_bytes()
    return {
        "result": "passed",
        "scope": "current Dapr-only source boundary; no activation or completion authority",
        "amendment_sha256": hashlib.sha256(amendment).hexdigest(),
        "server_source_files": source_count,
        "checks": sorted(set(MUTATIONS.values())),
        "v2_writes": "fenced",
        "production_qualification": "not established by this preflight",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mutations", action="store_true", help="run each policy mutation with a 10 second timeout")
    parser.add_argument("--mutation", choices=sorted(MUTATIONS), help=argparse.SUPPRESS)
    args = parser.parse_args()
    try:
        result = verify(args.mutation)
        if args.mutations:
            outcomes = []
            for mutation, expected_check in MUTATIONS.items():
                try:
                    child = subprocess.run(
                        [sys.executable, str(pathlib.Path(__file__).resolve()), "--mutation", mutation],
                        capture_output=True, text=True, timeout=10, check=False,
                    )
                except subprocess.TimeoutExpired as error:
                    raise BoundaryFailure("mutation-timeout", f"{mutation}: exceeded 10 seconds") from error
                observed = json.loads(child.stdout)
                if child.returncode != 2 or observed.get("check") != expected_check:
                    raise BoundaryFailure("mutation-rejection", f"{mutation}: missing rejection from {expected_check}")
                outcomes.append({"mutation": mutation, "check": expected_check, "result": "rejected", "timeout_seconds": 10})
            result["mutations"] = outcomes
        print(json.dumps(result, sort_keys=True))
        return 0
    except BoundaryFailure as error:
        print(json.dumps({"result": "failed", "check": error.check, "detail": str(error)}, sort_keys=True))
        return 2
    except (OSError, ET.ParseError, ValueError, KeyError) as error:
        print(json.dumps({"result": "failed", "check": "preflight-input", "detail": str(error)}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
