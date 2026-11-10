#!/usr/bin/env python3
"""Verify only the availability of the pinned OQ8 governing design, offline."""

import argparse
import hashlib
from pathlib import Path
import re
import sys


# These approved pins are independent of all editable provenance and bindings.
IDENTITY = {
    "source-repository": "github.com/Hexalith/Hexalith.Folders",
    "source-path": "docs/exit-criteria/oq8-idempotency-design.md",
    "source-commit": "a9cfea91c8a987ef7a836c216e633a92321fc3c2",
    "design-version": "1.0.0",
    "design-sha256": "1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8",
    "local-design": "docs/architecture/oq8/oq8-idempotency-design.md",
}
PERMISSION = {
    "source-license-path": "LICENSE",
    "source-license-commit": "a9cfea91c8a987ef7a836c216e633a92321fc3c2",
    "source-license-sha256": "6af66baf053023d456706de321e3cea6a6edbb288ffec10f673186b1befe7ac2",
    "local-license": "docs/architecture/oq8/LICENSE",
    "permission-basis": "MIT License; Copyright (c) 2026 Hexalith; copying associated documentation with the copyright and permission notice retained.",
}
PROVENANCE = "docs/architecture/oq8/README.md"
STORY = "_bmad-output/implementation-artifacts/spec-4-17-oq8-governing-design-authority-import.md"
DOCUMENTS = (
    PROVENANCE,
    "_bmad-output/planning-artifacts/prd.md",
    "_bmad-output/planning-artifacts/architecture.md",
    "_bmad-output/planning-artifacts/epics.md",
    STORY,
)
WORKFLOW = ".github/workflows/oq8-governing-design.yml"
CI_FIELDS = {
    "OQ8_SOURCE_REPOSITORY": "source-repository",
    "OQ8_SOURCE_PATH": "source-path",
    "OQ8_SOURCE_COMMIT": "source-commit",
    "OQ8_DESIGN_VERSION": "design-version",
    "OQ8_DESIGN_SHA256": "design-sha256",
    "OQ8_LOCAL_DESIGN": "local-design",
}


def read_local(root: Path, relative: str) -> bytes:
    """Read a repository file without following an external or symbolic input."""
    path = root
    for part in Path(relative).parts:
        path /= part
        if path.is_symlink():
            raise ValueError(f"{relative}: symbolic links cannot supply local authority")
    try:
        return path.read_bytes()
    except OSError as error:
        raise ValueError(f"{relative}: cannot read file ({error.strerror})") from error


def binding(content: str, relative: str, workflow: bool = False) -> dict[str, str]:
    """Read one explicit current binding, rejecting ambiguous or malformed fields."""
    if workflow:
        start, end = "# oq8-governing-design:begin", "# oq8-governing-design:end"
    else:
        start, end = "<!-- oq8-governing-design:begin -->", "<!-- oq8-governing-design:end -->"
    if content.count(start) != 1 or content.count(end) != 1:
        raise ValueError(f"{relative}: expected exactly one current governing-design binding")
    start_index, end_index = content.index(start), content.index(end)
    if end_index <= start_index:
        raise ValueError(f"{relative}: governing-design end marker must follow begin marker")
    block = content[start_index + len(start):end_index].strip()
    if workflow:
        lines = block.splitlines()
        if not lines or lines.pop(0).strip() != "env:":
            raise ValueError(f"{relative}: governing-design binding must be workflow env fields")
    else:
        match = re.fullmatch(r"```text\n(.*?)\n```", block, re.DOTALL)
        if not match:
            raise ValueError(f"{relative}: governing-design binding must be a text fence")
        lines = match[1].splitlines()
    fields = {}
    for line in lines:
        match = re.fullmatch(r"\s*([A-Za-z0-9_-]+): (\S[^\r\n]*)", line)
        if not match:
            raise ValueError(f"{relative}: malformed governing-design field: {line.strip()!r}")
        key, value = match.groups()
        if key in fields:
            raise ValueError(f"{relative}: duplicate governing-design field {key}")
        fields[key] = value
    return fields


def validate(root: Path) -> list[str]:
    """Return actionable failures; success establishes availability only."""
    errors = []
    for relative, expected in (
        (IDENTITY["local-design"], IDENTITY["design-sha256"]),
        (PERMISSION["local-license"], PERMISSION["source-license-sha256"]),
    ):
        try:
            actual = hashlib.sha256(read_local(root, relative)).hexdigest()
            if actual != expected:
                errors.append(f"{relative}: SHA-256 mismatch; expected {expected}, found {actual}")
        except ValueError as error:
            errors.append(str(error))
    for relative in (*DOCUMENTS, WORKFLOW):
        expected = dict(IDENTITY)
        if relative == PROVENANCE:
            expected.update(PERMISSION)
        if relative == WORKFLOW:
            expected = {key: IDENTITY[field] for key, field in CI_FIELDS.items()}
        try:
            # Metadata follows ordinary checkout/editor line endings; import hashes above
            # deliberately consume raw bytes and never receive this normalization.
            content = read_local(root, relative).decode("utf-8").replace("\r\n", "\n")
            if relative == STORY:
                frozen_identity = (
                    f"**Always:** Bind repository `{IDENTITY['source-repository']}`, "
                    f"path `{IDENTITY['source-path']}`, commit `{IDENTITY['source-commit']}`, "
                    f"version `{IDENTITY['design-version']}`, and SHA-256 `{IDENTITY['design-sha256']}`."
                )
                frozen = re.search(r"<frozen-after-approval\b[^>]*>(.*?)</frozen-after-approval>",
                                   content, re.DOTALL)
                if frozen is None or frozen[1].count(frozen_identity) != 1:
                    errors.append(f"{relative}: frozen source identity binding mismatch (repository/path/commit/version/digest)")
            actual = binding(content, relative, workflow=relative == WORKFLOW)
            for key, value in expected.items():
                if key not in actual:
                    errors.append(f"{relative}: missing governing-design field {key}")
                elif actual[key] != value:
                    errors.append(f"{relative}: {key} mismatch; expected {value!r}, found {actual[key]!r}")
            for key in actual.keys() - expected.keys():
                errors.append(f"{relative}: unexpected governing-design field {key}")
        except (ValueError, UnicodeError) as error:
            errors.append(f"{relative}: invalid UTF-8" if isinstance(error, UnicodeError) else str(error))
    return errors


def main() -> int:
    """Run the standalone offline check against this checkout or a test fixture."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1],
                        help="EventStore checkout root (defaults to this script's repository)")
    args = parser.parse_args()
    errors = validate(args.root.resolve())
    if errors:
        for error in errors:
            print(f"OQ8 governing-design check: {error}", file=sys.stderr)
        return 1
    print(f"OQ8 governing-design availability verified: {IDENTITY['design-sha256']}")
    print("Authority availability only; runtime conformance, assurance, readiness, release, deployment, consumer migration and Folders closure remain separate gates.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
