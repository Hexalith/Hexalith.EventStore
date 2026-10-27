#!/usr/bin/env python3
"""Prepare the temporary Story 5.12 package chain without publishing anything."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[1]
EVENTSTORE_PACKAGES = ("Contracts", "Client", "ServiceDefaults", "DomainService", "Server")
TENANTS_PACKAGES = ("Contracts", "Server")
CONSUMERS = (
    "Hexalith.EventStore.CatalogConstruction",
    "Hexalith.EventStore.Server.Tests",
    "Hexalith.EventStore.QueryRouting.Tests",
    "Hexalith.EventStore.Server.LiveSidecar.Tests",
    "routing-catalog.cs",
)


def capture(repository: Path, *command: str) -> str:
    return subprocess.check_output(command, cwd=repository, text=True).strip()


def revision(repository: Path) -> str:
    if Path(capture(repository, "git", "rev-parse", "--show-toplevel")).resolve() != repository.resolve():
        raise ValueError("Every candidate owner must own its initialized Git worktree.")
    value = capture(repository, "git", "rev-parse", "HEAD")
    if not re.fullmatch(r"[0-9a-f]{40}", value):
        raise ValueError("Package preparation requires full Git source revisions.")
    return value


def tracked_diff_digest(repository: Path) -> str:
    return hashlib.sha256(subprocess.check_output(
        ["git", "diff", "--binary", "HEAD"], cwd=repository)).hexdigest()


def stable_version(repository: Path, project: str) -> str:
    value = capture(repository, "dotnet", "msbuild", project, "-nologo", "-getProperty:Version",
                    "-p:UseHexalithProjectReferences=false")
    if not re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", value):
        raise ValueError("Candidate packages require an explicit stable assembly version.")
    return value


def package_identity(path: Path, expected_id: str, version: str, source: str,
                     candidate_versions: dict[str, str]) -> dict:
    with ZipFile(path) as archive:
        specifications = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(specifications) != 1:
            raise ValueError("A candidate package must contain exactly one nuspec.")
        specification = ET.fromstring(archive.read(specifications[0]))
    metadata = next(node for node in specification if node.tag.rsplit("}", 1)[-1] == "metadata")
    fields = {node.tag.rsplit("}", 1)[-1]: node for node in metadata}
    if fields["id"].text != expected_id or fields["version"].text != version:
        raise ValueError("Candidate package identity differs from the requested package.")
    if fields["repository"].attrib.get("commit") != source:
        raise ValueError("Candidate package repository commit differs from its source.")
    dependencies = []
    for node in metadata.iter():
        if node.tag.rsplit("}", 1)[-1] != "dependency":
            continue
        dependency = {"id": node.attrib["id"], "version": node.attrib["version"]}
        expected = candidate_versions.get(dependency["id"])
        if expected is not None and dependency["version"] != expected:
            raise ValueError("Candidate dependency escaped the exact candidate package chain.")
        dependencies.append(dependency)
    return {"id": expected_id, "version": version, "source": source,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "file": path.name,
            "dependencies": dependencies}


def environment_lines(feed: Path, version: str) -> str:
    values = {"CatalogCandidateTenantsVersion": version,
              "RestoreAdditionalProjectSources": str(feed),
              "NUGET_PACKAGES": str(feed.parent / "packages")}
    if any(any(ord(character) < 32 for character in value) for value in values.values()):
        raise ValueError("Candidate environment values must occupy one line.")
    return "".join(f"{key}={value}\n" for key, value in values.items())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "TestResults/catalog-package-bootstrap")
    arguments = parser.parse_args()
    tenants = ROOT / "references/Hexalith.Tenants"
    declared = capture(ROOT, "git", "config", "--file", ".gitmodules", "--get",
                       "submodule.Hexalith.Tenants.path")
    if declared != "references/Hexalith.Tenants":
        raise ValueError("Tenants must be an initialized root-declared dependency.")
    sources = {"EventStore": revision(ROOT), "Tenants": revision(tenants),
               "Builds": revision(ROOT / "references/Hexalith.Builds")}
    working_trees = {owner: tracked_diff_digest(repository) for owner, repository in
                     (("EventStore", ROOT), ("Tenants", tenants), ("Builds", ROOT / "references/Hexalith.Builds"))}
    versions = {
        "EventStore": stable_version(ROOT, "src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj"),
        "Tenants": stable_version(tenants, "src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj"),
    }
    candidates = {owner: f"{version}-story512.{sources[owner]}" for owner, version in versions.items()}
    candidate_versions = {
        **{f"Hexalith.EventStore.{suffix}": candidates["EventStore"] for suffix in EVENTSTORE_PACKAGES},
        **{f"Hexalith.Tenants.{suffix}": candidates["Tenants"] for suffix in TENANTS_PACKAGES},
    }
    output = arguments.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    feed = output / "feed"
    feed.mkdir()
    manifest = {"purpose": "temporary-unpublished-catalog-package-preparation", "sources": sources,
                "trackedWorkingTreeDiffSha256": working_trees,
                "preparationScriptSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                "assemblyVersions": versions, "consumers": CONSUMERS, "commands": [], "packages": []}
    for owner, repository, suffixes in (("EventStore", ROOT, EVENTSTORE_PACKAGES),
                                        ("Tenants", tenants, TENANTS_PACKAGES)):
        for suffix in suffixes:
            package_id = f"Hexalith.{owner}.{suffix}"
            command = ["dotnet", "pack", f"src/{package_id}/{package_id}.csproj",
                       "-c", "Release", "-p:UseHexalithProjectReferences=false",
                       "-p:GeneratePackageOnBuild=false", f"-p:Version={versions[owner]}",
                       f"-p:PackageVersion={candidates[owner]}", f"-p:RepositoryCommit={sources[owner]}",
                       "--artifacts-path", str(output / f"{owner.lower()}-artifacts"),
                       "-o", str(feed), "-m:1", "-v", "quiet"]
            if owner == "Tenants":
                command += [f"-p:HexalithEventStoreVersion={candidates['EventStore']}",
                            f"-p:RestoreAdditionalProjectSources={feed}",
                            f"-p:RestorePackagesPath={output / 'packages'}"]
            print(f"Preparing package {package_id} from {sources[owner]}", flush=True)
            with (output / f"{package_id}.log").open("w", encoding="utf-8") as log:
                result = subprocess.run(command, cwd=repository, stdout=log, stderr=subprocess.STDOUT,
                                        timeout=600, check=False)
            manifest["commands"].append({"cwd": str(repository), "arguments": command, "exit": result.returncode})
            if result.returncode:
                (output / "failed-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
                raise RuntimeError(f"Candidate packing failed: {package_id}; see {output / (package_id + '.log')}")
            package = feed / f"{package_id}.{candidates[owner]}.nupkg"
            manifest["packages"].append(package_identity(package, package_id, candidates[owner],
                                                          sources[owner], candidate_versions))
    if sources != {"EventStore": revision(ROOT), "Tenants": revision(tenants),
                   "Builds": revision(ROOT / "references/Hexalith.Builds")}:
        raise ValueError("A source revision changed while preparing candidate packages.")
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    lines = environment_lines(feed, candidates["Tenants"])
    (output / "environment.txt").write_text(lines, encoding="utf-8")
    if github_environment := os.environ.get("GITHUB_ENV"):
        with Path(github_environment).open("a", encoding="utf-8") as environment:
            environment.write(lines)
    print("Candidate package provenance: " + json.dumps(manifest, sort_keys=True), flush=True)
    print(f"Prepared seven unpublished Release packages; manifest: {output / 'manifest.json'}", flush=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        print(f"Catalog package preparation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
