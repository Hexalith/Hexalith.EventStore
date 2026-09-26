#!/usr/bin/env python3
"""Prepare unapproved OQ8 v5 review inputs while verifying sealed v4 history.

The active OQ8 selector and lifecycle record remain on v4. This tool never
creates reviews, writes inside the repository, or grants current-source authority.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
V4_SOURCE_COMMIT = "30b279bd841c671932b51fad6bcf78b147079d21"
V5_SELECTION_REASON = "Retain immutable v1-v4 history and activate the reviewed v5 source-only successor for EventStore-owned trusted publishing."
V4_DIRECTORY = Path(
    "_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v4"
)
V5_DIRECTORY = Path(
    "_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v5"
)
SELECTOR = Path(
    "_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json"
)
POST_REVIEW_PATHS = (
    "_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json",
    "_bmad-output/implementation-artifacts/4-15-oq8-platform-lifecycle-state.json",
    "_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v5/closure-sha256.txt",
    "_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v5/packet.json",
)
RELEASE_SOURCE_PATHS = (
    ".github/workflows/release.yml",
    ".releaserc.json",
    "scripts/validate-release-secrets.sh",
    "scripts/validate-publication-preflight.sh",
    "scripts/verify-oq8-v5-candidate.sh",
    ".github/workflows/commitlint.yml",
    "docs/ci-secrets-checklist.md",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/TrustedPublishingReleaseTests.cs",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8V5CandidateTests.cs",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/oq8-v5-synthetic-fixture.py",
    "tools/release-packages.json",
    "tools/validate-oq8-platform-evidence.py",
    "tools/prepare-oq8-v5-candidate.py",
    "tools/oq8-v5-packet.py",
    "tools/oq8-v5-packet.schema.json",
    "docs/oq8-v5-activation.md",
)
EXPECTED_CHANGED_V4_GATE_INPUTS = {
    "docs/ci.md",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs",
    "tools/validate-oq8-platform-evidence.py",
}
HISTORICAL_MODES = (
    ("v1", "--historical-v1-only"),
    ("v2", "--historical-v2-only"),
    ("v3", "--historical-v3-only"),
    ("v4", None),
)


def sha256(path: Path) -> str:
    if path.is_symlink() or not path.is_file():
        raise ValueError(f"Required regular file is unavailable: {path}")
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(*arguments: str) -> str:
    result = subprocess.run(
        ["git", *arguments],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
        timeout=30,
    )
    return result.stdout.strip()


def reviewed_source_tree_sha256(commit: str) -> str:
    """Hash every committed Git tree entry except the four reviewed handoff paths."""
    result = subprocess.run(
        ["git", "--no-replace-objects", "ls-tree", "--full-tree", "--full-name", "-r", "-z", commit],
        cwd=ROOT,
        capture_output=True,
        check=True,
        timeout=30,
    )
    excluded = {path.encode("utf-8") for path in POST_REVIEW_PATHS}
    content = hashlib.sha256()
    for entry in result.stdout.split(b"\0"):
        if not entry:
            continue
        metadata, path = entry.split(b"\t", 1)
        if path not in excluded:
            content.update(metadata + b"\t" + path + b"\0")
    return content.hexdigest()


def archive_source(commit: str, destination: Path) -> None:
    producer = subprocess.Popen(
        ["git", "archive", "--format=tar", commit],
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    assert producer.stdout is not None
    assert producer.stderr is not None
    try:
        with tarfile.open(fileobj=producer.stdout, mode="r|") as archive:
            archive.extractall(destination, filter="data")
    finally:
        producer.stdout.close()
    error = producer.stderr.read().decode("utf-8", errors="replace")
    if producer.wait(timeout=30) != 0:
        raise RuntimeError(f"Unable to read approved source commit: {error.strip()}")


def archive_v4_source(destination: Path) -> None:
    archive_source(V4_SOURCE_COMMIT, destination)


def verify_historical_evidence(archive_root: Path) -> dict[str, str]:
    validator = archive_root / "tools/validate-oq8-platform-evidence.py"
    results: dict[str, str] = {}
    for version, mode in HISTORICAL_MODES:
        command = [
            sys.executable,
            str(validator),
            "--root",
            str(archive_root),
            "--git-root",
            str(ROOT),
        ]
        if mode is not None:
            command.append(mode)
        result = subprocess.run(
            command,
            cwd=archive_root,
            capture_output=True,
            text=True,
            timeout=90,
        )
        if result.returncode != 0 or "validation passed" not in result.stdout:
            raise RuntimeError(
                f"Historical OQ8 {version} validation failed: "
                f"{result.stdout[-500:]} {result.stderr[-500:]}"
            )
        results[version] = "passed"
    return results


def verify_sealed_v4(root: Path, archive_root: Path, *, allow_v5: bool) -> tuple[dict, dict]:
    selector = json.loads((root / SELECTOR).read_text(encoding="utf-8"))
    archived_selector = json.loads(
        (archive_root / SELECTOR).read_text(encoding="utf-8")
    )
    if sha256(archive_root / SELECTOR) != "b61c8112a281b6dc74511b8392684c8e62c67b285580f82ce331b6bcb189fd8c":
        raise ValueError("The archived OQ8 v4 selector has changed.")
    if allow_v5:
        if selector.get("schema") != "hexalith.eventstore.story-4-15-successor-selection/v4":
            raise ValueError("A v5 activation must use the v4 selector schema.")
        if selector.get("reason") != V5_SELECTION_REASON:
            raise ValueError("A v5 activation must use the reviewed selection reason.")
        if selector.get("historical", {}).get("v4Successor") != archived_selector["successor"]:
            raise ValueError("A v5 activation must retain the exact v4 predecessor selection.")
        if selector.get("successor", {}).get("directory") != "_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v5":
            raise ValueError("A v5 activation must select the v5 successor directory.")
    elif sha256(root / SELECTOR) != sha256(archive_root / SELECTOR) or selector != archived_selector:
        raise ValueError("The active OQ8 selector differs from sealed v4 history.")
    selected = archived_selector["successor"]
    directory = root / V4_DIRECTORY
    if directory.is_symlink() or not directory.is_dir():
        raise ValueError("The sealed v4 evidence directory is unavailable.")
    expected_paths = set(selected["files"]) | {"closure-sha256.txt", "reviews"}
    actual_paths = {
        path.relative_to(root / V4_DIRECTORY).as_posix()
        for path in directory.rglob("*")
    }
    if actual_paths != expected_paths:
        raise ValueError("The sealed v4 file set has changed.")
    identity = json.loads(
        (root / V4_DIRECTORY / "source-artifact-identity.json").read_text(
            encoding="utf-8"
        )
    )
    for relative, expected in selected["files"].items():
        path = V4_DIRECTORY / relative
        if sha256(root / path) != expected or sha256(archive_root / path) != expected:
            raise ValueError(f"Sealed v4 artifact drift: {path}")
    for record in ("closure-sha256.txt",):
        path = V4_DIRECTORY / record
        expected = selected["manifestSha256"]
        if sha256(root / path) != expected or sha256(archive_root / path) != expected:
            raise ValueError(f"Sealed v4 manifest drift: {path}")
    for relative in ("docs/ci.md",):
        expected = identity["gateInputs"][relative]
        if sha256(archive_root / relative) != expected:
            raise ValueError(f"Archived v4 documentation drift: {relative}")
    return archived_selector, identity


def prepare(*, allow_v5: bool = False) -> dict:
    if git("rev-parse", "--show-toplevel") != str(ROOT):
        raise ValueError("Candidate preparation must run from the EventStore repository.")
    git("cat-file", "-e", f"{V4_SOURCE_COMMIT}^{{commit}}")
    subprocess.run(
        ["git", "merge-base", "--is-ancestor", V4_SOURCE_COMMIT, "HEAD"],
        cwd=ROOT,
        check=True,
        timeout=30,
    )
    with tempfile.TemporaryDirectory(prefix="oq8-v4-history-") as temporary:
        archive_root = Path(temporary)
        archive_v4_source(archive_root)
        historical = verify_historical_evidence(archive_root)
        selector, identity = verify_sealed_v4(ROOT, archive_root, allow_v5=allow_v5)

    v5_packet_path = ROOT / V5_DIRECTORY / "packet.json"
    reviewed_commit = None
    if allow_v5 and v5_packet_path.is_file():
        packet_data = json.loads(v5_packet_path.read_text(encoding="utf-8"))
        reviewed_commit = (
            packet_data.get("subjectInputs", {})
            .get("sourceIdentity", {})
            .get("reviewedCommit")
        )

    if reviewed_commit is not None:
        git("cat-file", "-e", f"{reviewed_commit}^{{commit}}")
        subprocess.run(
            ["git", "merge-base", "--is-ancestor", reviewed_commit, "HEAD"],
            cwd=ROOT,
            check=True,
            timeout=30,
        )

    with tempfile.TemporaryDirectory(prefix="oq8-v5-source-") as v5_temp:
        if reviewed_commit is not None:
            source_root = Path(v5_temp)
            archive_source(reviewed_commit, source_root)
            head = reviewed_commit
            working_tree_dirty = False
        else:
            source_root = ROOT
            head = git("rev-parse", "HEAD")
            working_tree_dirty = bool(git("status", "--porcelain"))

        current_gate_inputs = {
            relative: sha256(source_root / relative)
            for relative in identity["gateInputs"]
        }
        changed_gate_inputs = {
            relative: {"v4Sha256": expected, "candidateSha256": current_gate_inputs[relative]}
            for relative, expected in identity["gateInputs"].items()
            if current_gate_inputs[relative] != expected
        }
        if set(changed_gate_inputs) != EXPECTED_CHANGED_V4_GATE_INPUTS:
            raise ValueError("The v5 candidate changes an unexpected v4 gate input or omits an approved transition.")
        release_source = {
            relative: sha256(source_root / relative) for relative in RELEASE_SOURCE_PATHS
        }
        manifest = json.loads(
            (source_root / "tools/release-packages.json").read_text(encoding="utf-8")
        )
        package_ids = [item["id"] for item in manifest["packages"]]
        if len(package_ids) != 14 or len(set(package_ids)) != 14:
            raise ValueError("The 14-package release inventory has changed.")

        return {
            "schema": "hexalith.eventstore.oq8-v5-review-candidate/v1",
            "status": "draft-unapproved",
            "repository": "Hexalith/Hexalith.EventStore",
            "head": head,
            "postReviewPaths": list(POST_REVIEW_PATHS),
            "reviewedSourceTreeSha256": reviewed_source_tree_sha256(head),
            "workingTreeDirty": working_tree_dirty,
        "historical": {
            "v4SourceCommit": V4_SOURCE_COMMIT,
            "v4Directory": V4_DIRECTORY.as_posix(),
            "v4ManifestSha256": selector["successor"]["manifestSha256"],
            "v4ReviewSubjectSha256": selector["successor"]["reviewSubjectSha256"],
            "validation": historical,
        },
        "v4GateInputs": identity["gateInputs"],
        "currentGateInputs": current_gate_inputs,
        "changedGateInputs": changed_gate_inputs,
        "releaseSourceSha256": release_source,
        "packageIds": package_ids,
        "review": {
            "subjectFrozen": False,
            "subjectSha256": None,
            "architecture": "pending",
            "security": "pending",
            "test": "pending",
        },
        "requiredBeforeActivation": [
            "Commit the final implementation source on a review branch and run focused pre-review checks.",
            "Regenerate the receipt-independent v5 subject draft from that clean source.",
            "Freeze one v5 subject and obtain fresh architecture, security, and test approvals.",
            "Assemble the reviewed packet and v5 manifest, then update selector and lifecycle bindings.",
            "Run full Contracts and reviewed PR CI, merge, and require exact-source main push CI before any release.",
        ],
        "authority": {
            "currentSourceApproved": False,
            "releaseApproved": False,
            "packageAuthority": False,
            "registryAuthority": False,
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        help="New candidate JSON path outside the repository; defaults to stdout.",
    )
    parser.add_argument(
        "--for-v5-activation",
        action="store_true",
        help="Check a selected v5 packet while retaining archived v4 identity; grants no authority.",
    )
    args = parser.parse_args()
    try:
        payload = prepare(allow_v5=args.for_v5_activation)
        rendered = json.dumps(payload, indent=2, sort_keys=True) + "\n"
        if args.output is None:
            sys.stdout.write(rendered)
        else:
            output = args.output.resolve()
            if output == ROOT or ROOT in output.parents:
                raise ValueError("Candidate output must be outside the repository.")
            with output.open("x", encoding="utf-8") as stream:
                stream.write(rendered)
            print(output)
        return 0
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        print(f"oq8-v5-candidate: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
