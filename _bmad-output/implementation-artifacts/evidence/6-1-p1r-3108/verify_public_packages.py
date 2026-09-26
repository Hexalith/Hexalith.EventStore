#!/usr/bin/env python3
"""Replay the public EventStore 3.108.1 archive and source checks."""

from __future__ import annotations

import concurrent.futures
import hashlib
import io
import json
import pathlib
import subprocess
import tempfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile


EVIDENCE = pathlib.Path(__file__).resolve().parent
ROOT = EVIDENCE.parents[3]
RECORD = json.loads((EVIDENCE / "public-packages.json").read_text(encoding="utf-8"))
USER_AGENT = "Hexalith-EventStore-3.108.1-evidence-replay"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def download(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=90) as response:
        require(response.status == 200, f"HTTP {response.status}: {url}")
        return response.read()


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def repository_metadata(archive: zipfile.ZipFile, package_id: str) -> dict[str, str]:
    metadata = ET.fromstring(archive.read(f"{package_id}.nuspec")).find("{*}metadata")
    require(metadata is not None, f"Missing nuspec metadata: {package_id}")
    repository = metadata.find("{*}repository")
    require(repository is not None, f"Missing repository metadata: {package_id}")
    return {
        "id": metadata.findtext("{*}id") or "",
        "version": metadata.findtext("{*}version") or "",
        **repository.attrib,
    }


def verify_package(row: dict, asset: dict, directory: pathlib.Path) -> str:
    package_id = row["id"]
    filename = f"{package_id}.{RECORD['version']}.nupkg"
    expected_nuget_url = (
        f"https://api.nuget.org/v3-flatcontainer/{package_id.lower()}/"
        f"{RECORD['version']}/{filename.lower()}"
    )
    require(row["nuget_url"] == expected_nuget_url, f"NuGet source URL drift: {package_id}")
    require(asset["name"] == filename, f"Release asset name drift: {package_id}")
    require(asset["digest"] == row["github_asset_digest"], f"Release digest drift: {package_id}")
    require(asset["size"] == row["github_asset_size"], f"Release size drift: {package_id}")
    require(asset["browser_download_url"] == row["github_asset_url"], f"Release URL drift: {package_id}")

    nuget_bytes = download(row["nuget_url"])
    asset_bytes = download(asset["browser_download_url"])
    require(sha256(nuget_bytes) == row["nuget_sha256"], f"NuGet SHA-256 drift: {package_id}")
    require(sha256(asset_bytes) == row["github_asset_sha256"], f"Release asset SHA-256 drift: {package_id}")
    require(sha256(asset_bytes) == asset["digest"].removeprefix("sha256:"), f"Live release digest mismatch: {package_id}")
    require(len(nuget_bytes) == row["nuget_size"], f"NuGet size drift: {package_id}")
    require(len(asset_bytes) == asset["size"], f"Release size mismatch: {package_id}")

    with zipfile.ZipFile(io.BytesIO(nuget_bytes)) as signed, zipfile.ZipFile(io.BytesIO(asset_bytes)) as unsigned:
        signed_names = signed.namelist()
        unsigned_names = unsigned.namelist()
        require(len(signed_names) == len(set(signed_names)), f"Duplicate NuGet ZIP entry: {package_id}")
        require(len(unsigned_names) == len(set(unsigned_names)), f"Duplicate release ZIP entry: {package_id}")
        require(set(signed_names) - set(unsigned_names) == {".signature.p7s"}, f"Unexpected signed ZIP entries: {package_id}")
        require(set(unsigned_names) <= set(signed_names), f"Missing signed ZIP entries: {package_id}")
        require(all(signed.read(name) == unsigned.read(name) for name in unsigned_names), f"ZIP payload difference: {package_id}")
        expected = {"id": package_id, "version": RECORD["version"], **row["nuget_repository"]}
        require(row["github_repository"] == row["nuget_repository"], f"Recorded repository mismatch: {package_id}")
        require(row["nuget_version"] == RECORD["version"], f"Recorded version mismatch: {package_id}")
        require(row["same_unsigned_payload"] and not row["differing_shared_entries"], f"Recorded payload comparison mismatch: {package_id}")
        require(row["nuget_signed_archive_extra_entries"] == [".signature.p7s"], f"Recorded signature entry mismatch: {package_id}")
        require(repository_metadata(signed, package_id) == expected, f"NuGet nuspec metadata drift: {package_id}")
        require(repository_metadata(unsigned, package_id) == expected, f"Release nuspec metadata drift: {package_id}")
        require(expected["commit"] == RECORD["tag_commit"], f"Source commit drift: {package_id}")

    (directory / filename).write_bytes(nuget_bytes)
    return package_id


def run_command(command: list[str], cwd: pathlib.Path) -> str:
    completed = subprocess.run(command, cwd=cwd, text=True, capture_output=True, check=False)
    require(completed.returncode == 0, f"{' '.join(command)} exited {completed.returncode}:\n{completed.stdout}{completed.stderr}")
    return completed.stdout + completed.stderr


def main() -> None:
    source_manifest = run_command(["git", "show", f"{RECORD['tag']}:tools/release-packages.json"], ROOT).encode()
    require(sha256(source_manifest) == RECORD["release_manifest_sha256"], "Tagged manifest SHA-256 drift")
    require(sha256((ROOT / "tools/release-packages.json").read_bytes()) == RECORD["release_manifest_sha256"], "Current manifest SHA-256 drift")
    tag_commit = run_command(["git", "rev-parse", f"{RECORD['tag']}^{{commit}}"], ROOT).strip()
    require(tag_commit == RECORD["tag_commit"], "Release tag commit drift")
    manifest = json.loads(source_manifest)
    require(manifest["packages"] == [{"id": row["id"], "project": row["project"]} for row in RECORD["packages"]], "Manifest inventory drift")
    require(len(RECORD["packages"]) == 14, "Expected 14 packages")

    release = json.loads(download("https://api.github.com/repos/Hexalith/Hexalith.EventStore/releases/tags/v3.108.1"))
    require(release["tag_name"] == RECORD["tag"], "Release tag drift")
    require(release["published_at"] == RECORD["release_published_at"], "Release publication date drift")
    assets = {asset["name"]: asset for asset in release["assets"] if asset["name"].endswith(".nupkg")}
    expected_names = {f"{row['id']}.{RECORD['version']}.nupkg" for row in RECORD["packages"]}
    require(set(assets) == expected_names, "Release asset inventory drift")

    with tempfile.TemporaryDirectory(prefix="eventstore-31081-public-") as temp:
        directory = pathlib.Path(temp)
        with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
            package_ids = list(pool.map(lambda row: verify_package(row, assets[f"{row['id']}.{RECORD['version']}.nupkg"], directory), RECORD["packages"]))
        archives = [str(directory / f"{package_id}.{RECORD['version']}.nupkg") for package_id in package_ids]
        signatures = run_command(["dotnet", "nuget", "verify", "--all", *archives, "--verbosity", "minimal"], ROOT)
        require(signatures.count("Signature type: Repository") == 14, "Expected 14 NuGet repository signatures")
        require(signatures.count(RECORD["nuget_signature_verification"]["certificate_sha256"]) == 14, "NuGet certificate mismatch")
        contract = run_command(["python3", "tools/validate-release-packages.py", str(directory), RECORD["version"]], ROOT)
        require("Validated 14 release packages" in contract, "Release package contract mismatch")

    print(f"PASS: {len(package_ids)} public packages match recorded hashes, tagged source metadata, release assets, ZIP payloads, signatures, and manifest contract")


if __name__ == "__main__":
    main()
