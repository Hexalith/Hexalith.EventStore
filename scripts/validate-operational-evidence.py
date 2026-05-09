#!/usr/bin/env python3
"""Validate curated operational evidence markdown files.

DW4 intentionally keeps this as a small static validator. It supports only
query-operational-evidence/v1 and signalr-operational-evidence/v1 and fails
closed for any other schema marker.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path


SCHEMA_QUERY_V1 = "query-operational-evidence/v1"
SCHEMA_SIGNALR_V1 = "signalr-operational-evidence/v1"
SUPPORTED_SCHEMAS = {SCHEMA_QUERY_V1, SCHEMA_SIGNALR_V1}

QUERY_CLASSIFICATIONS = {
    "pass",
    "path-viability",
    "sample-only",
    "diagnostic-only",
    "not-claimable",
    "product-failure",
    "environment-blocker",
    "instrumentation-gap",
    "inconclusive",
}

SIGNALR_CLASSIFICATIONS = {
    "pass",
    "product-failure",
    "environment-blocker",
    "instrumentation-gap",
    "sample-only",
    "inconclusive",
}

# Required metadata fields are checked by validate_required_metadata.
# Control fields are checked separately by validate_required_value so reviewers see
# a more specific control-required-missing diagnostic instead of a generic metadata
# missing rule.
QUERY_REQUIRED = {
    "schema_version",
    "evidence_run_id",
    "story_key",
    "run_profile",
    "final_classification",
    "reviewer_verdict",
    "redaction_statement",
}

SIGNALR_REQUIRED = {
    "schema_version",
    "evidence_run_id",
    "story_key",
    "run_profile",
    "classification",
    "reviewer_verdict",
    "redaction_statement",
}

PROFILE_ASPIRE_FIELDS = {
    "apphost_url",
    "dapr_placement",
    "dapr_scheduler",
    "resource_snapshot",
}

EXPECTED_FIXTURE_RULES: dict[str, set[str]] = {
    "query-valid-minimal.md": set(),
    "query-valid-linked-control-run.md": set(),
    "query-valid-not-applicable-aspire.md": set(),
    "query-invalid-missing-metadata.md": {"query-required-metadata-missing"},
    "query-invalid-placeholder-unreplaced.md": {"placeholder-unreplaced"},
    "query-invalid-empty-required-table-cell.md": {"required-table-cell-empty"},
    "query-invalid-classification-not-in-enum.md": {"classification-invalid"},
    "query-invalid-control-missing.md": {"control-required-missing"},
    "query-invalid-correlation-control-missing.md": {"correlation-control-required-missing"},
    "query-invalid-control-linkage-missing.md": {"control-linkage-missing"},
    "query-invalid-control-linkage-unrelated.md": {"control-linkage-unrelated"},
    "query-invalid-redaction-bearer-token.md": {"redaction-unsafe-bearer-token"},
    "query-invalid-redaction-connection-string.md": {"redaction-unsafe-connection-string"},
    "query-invalid-redaction-production-hostname.md": {"redaction-unsafe-production-hostname"},
    "query-invalid-redaction-section-missing.md": {"redaction-section-missing"},
    "query-invalid-raw-secret-marker.md": {"redaction-raw-secret-marker"},
    "query-invalid-not-applicable-empty-reason.md": {"not-applicable-reason-missing"},
    "query-invalid-not-applicable-on-required-field.md": {"not-applicable-not-allowed-here"},
    "query-invalid-aspire-claimed-but-fields-missing.md": {"profile-aspire-fields-missing"},
    "signalr-valid-minimal.md": set(),
    "signalr-valid-linked-control-run.md": set(),
    "signalr-invalid-missing-metadata.md": {"signalr-required-metadata-missing"},
    "signalr-invalid-placeholder-unreplaced.md": {"placeholder-unreplaced"},
    "signalr-invalid-classification-not-in-enum.md": {"classification-invalid"},
    "signalr-invalid-control-missing.md": {"control-required-missing"},
    "signalr-invalid-control-linkage-missing.md": {"control-linkage-missing"},
    "signalr-invalid-control-linkage-unrelated.md": {"control-linkage-unrelated"},
    "signalr-invalid-empty-required-table-cell.md": {"required-table-cell-empty"},
    "signalr-invalid-redaction-bearer-token.md": {"redaction-unsafe-bearer-token"},
    "signalr-invalid-redaction-connection-string.md": {"redaction-unsafe-connection-string"},
    "signalr-invalid-redaction-production-hostname.md": {"redaction-unsafe-production-hostname"},
    "signalr-invalid-redaction-section-missing.md": {"redaction-section-missing"},
    "signalr-invalid-raw-secret-marker.md": {"redaction-raw-secret-marker"},
    "signalr-invalid-not-applicable-empty-reason.md": {"not-applicable-reason-missing"},
    "signalr-invalid-not-applicable-on-required-field.md": {"not-applicable-not-allowed-here"},
    "signalr-invalid-aspire-claimed-but-fields-missing.md": {"profile-aspire-fields-missing"},
    "schema-missing.md": {"schema-version-missing"},
    "schema-duplicate-markers.md": {"schema-version-duplicate"},
    "schema-contradictory.md": {"schema-version-contradictory"},
    "schema-unsupported-future-version.md": {"schema-version-unsupported"},
    "parse-malformed-yaml.md": {"parse-yaml-malformed"},
    "parse-malformed-table.md": {"parse-table-malformed"},
    "parse-duplicate-required-heading.md": {"parse-heading-duplicate"},
}


@dataclass(frozen=True)
class Diagnostic:
    file: str
    schema: str | None
    rule: str
    section: str | None
    field: str | None
    line: int | None
    hint: str
    level: str = "error"

    def to_json(self) -> dict[str, object]:
        data: dict[str, object] = {
            "file": self.file,
            "schema": self.schema,
            "rule": self.rule,
            "section": self.section,
            "field": self.field,
            "hint": self.hint,
            "level": self.level,
        }
        if self.line is not None:
            data["line"] = self.line
        return data


@dataclass(frozen=True)
class SchemaMarker:
    value: str
    line: int
    source: str


DEFAULT_FIXTURES_ROOT = (
    Path(__file__).resolve().parent.parent
    / "_bmad-output"
    / "test-artifacts"
    / "operational-evidence-validator"
    / "fixtures"
)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true", help="Emit JSON diagnostics only.")
    parser.add_argument("--self-test", action="store_true", help="Validate curated fixtures against expected rule ids.")
    parser.add_argument(
        "--fixtures-root",
        default=str(DEFAULT_FIXTURES_ROOT),
        help="Fixture root for --self-test (defaults to repo-relative fixtures dir).",
    )
    parser.add_argument("paths", nargs="*", help="Evidence markdown files or directories to validate.")
    args = parser.parse_args(argv)

    if args.self_test:
        return run_self_test(Path(args.fixtures_root), args.json)

    missing_paths = [p for p in args.paths if not Path(p).exists()]
    if missing_paths:
        for missing in missing_paths:
            print(f"ERROR: path not found: {missing}", file=sys.stderr)
        return 2

    diagnostics = validate_paths(args.paths)
    emit(diagnostics, args.json)
    return 1 if has_errors(diagnostics) else 0


def run_self_test(fixtures_root: Path, emit_json: bool) -> int:
    diagnostics: list[Diagnostic] = []
    failures: list[str] = []

    for file_name, expected in sorted(EXPECTED_FIXTURE_RULES.items()):
        path = fixtures_root / file_name
        if not path.exists():
            failures.append(f"{file_name}: fixture missing")
            continue

        result = validate_file(path)
        rules = {d.rule for d in result if d.level == "error"}
        diagnostics.extend(result)
        if expected:
            missing = expected - rules
            extra = rules - expected
            if missing:
                failures.append(f"{file_name}: missing expected rule(s) {', '.join(sorted(missing))}")
            if extra:
                failures.append(f"{file_name}: emitted unexpected rule(s) {', '.join(sorted(extra))}")
        elif rules:
            failures.append(f"{file_name}: expected pass but emitted {', '.join(sorted(rules))}")

    if emit_json:
        print(json.dumps({"diagnostics": [d.to_json() for d in sort_diagnostics(diagnostics)], "selfTestFailures": failures}, indent=2))
    else:
        print(f"Operational evidence validator self-test: {len(EXPECTED_FIXTURE_RULES)} fixtures checked")
        for failure in failures:
            print(f"FAIL: {failure}")
        if not failures:
            print("PASS: fixture expectations matched")

    return 1 if failures else 0


def validate_paths(paths: list[str]) -> list[Diagnostic]:
    files: list[Path] = []
    for raw in paths:
        path = Path(raw)
        if path.is_dir():
            files.extend(sorted(p for p in path.rglob("*.md") if p.is_file()))
        elif path.is_file():
            files.append(path)
        else:
            files.append(path)

    diagnostics: list[Diagnostic] = []
    for path in sorted(files, key=lambda p: str(p).lower()):
        diagnostics.extend(validate_file(path))
    return sort_diagnostics(diagnostics)


def validate_file(path: Path) -> list[Diagnostic]:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as exc:
        return [diag(path, None, "parse-file-unreadable", None, None, None, str(exc))]

    skip_reason = skip_reason_for(path, text)
    if skip_reason is not None:
        return [info(path, None, "evidence-file-skipped", "Skip", None, None, f"Skipped by {skip_reason}.")]

    markers = find_schema_markers(text)
    schema_diagnostics, schema = identify_schema(path, markers)
    if schema_diagnostics:
        return schema_diagnostics

    diagnostics: list[Diagnostic] = []
    lines = text.splitlines()
    sections = find_headings(lines)
    metadata, metadata_line, yaml_error = parse_first_yaml_block(lines)
    if yaml_error is not None:
        diagnostics.append(diag(path, schema, "parse-yaml-malformed", "Metadata", None, yaml_error, "Fix the fenced YAML key/value block."))
        return sort_diagnostics(diagnostics)

    diagnostics.extend(validate_duplicate_headings(path, schema, sections))
    diagnostics.extend(validate_tables(path, schema, lines))
    if any(d.rule.startswith("parse-") for d in diagnostics):
        return sort_diagnostics(diagnostics)

    if schema == SCHEMA_QUERY_V1:
        diagnostics.extend(validate_required_metadata(path, schema, QUERY_REQUIRED, metadata, metadata_line, "query-required-metadata-missing"))
        diagnostics.extend(validate_classification(path, schema, metadata, metadata_line, "final_classification", QUERY_CLASSIFICATIONS))
        diagnostics.extend(validate_required_value(path, schema, metadata, metadata_line, "false_positive_control", "control-required-missing"))
        diagnostics.extend(validate_required_value(path, schema, metadata, metadata_line, "correlation_control", "correlation-control-required-missing"))
    else:
        diagnostics.extend(validate_required_metadata(path, schema, SIGNALR_REQUIRED, metadata, metadata_line, "signalr-required-metadata-missing"))
        diagnostics.extend(validate_classification(path, schema, metadata, metadata_line, "classification", SIGNALR_CLASSIFICATIONS))
        diagnostics.extend(validate_required_value(path, schema, metadata, metadata_line, "reliability_control", "control-required-missing"))

    diagnostics.extend(validate_not_applicable(path, schema, metadata, metadata_line))
    diagnostics.extend(validate_profile_scope(path, schema, metadata, metadata_line))
    diagnostics.extend(validate_placeholders(path, schema, lines, metadata))
    diagnostics.extend(validate_redaction(path, schema, text, lines, sections))
    if not has_errors(diagnostics):
        if schema == SCHEMA_QUERY_V1:
            diagnostics.extend(validate_control_linkage(path, schema, metadata, metadata_line, "false_positive_control"))
            diagnostics.extend(validate_control_linkage(path, schema, metadata, metadata_line, "correlation_control"))
        else:
            diagnostics.extend(validate_control_linkage(path, schema, metadata, metadata_line, "reliability_control"))
    return sort_diagnostics(diagnostics)


SKIP_MARKER = "<!-- evidence-validator: skip -->"
# Stand-alone marker: HTML comment occupying its own line in the head of the
# file. Whitespace-tolerant inside the comment so authors can format it like a
# normal HTML idiom. Anchored to start/end of line so prose mentions of the
# marker (e.g. validator README, deferred-work ledger entries quoting it
# inline in backticks) do NOT silently shadow-ban the surrounding doc.
SKIP_MARKER_LINE_RE = re.compile(r"^\s*<!--\s*evidence-validator:\s*skip\s*-->\s*$", re.IGNORECASE)
# Only the head of the file is considered; an opt-out marker belongs at the top
# (typically before the first metadata block), not buried in the body where a
# legitimate prose discussion of skip behavior could plausibly sit.
SKIP_MARKER_HEAD_LINES = 20


def skip_reason_for(path: Path, text: str) -> str | None:
    head = text.splitlines()[:SKIP_MARKER_HEAD_LINES]
    if any(SKIP_MARKER_LINE_RE.match(line) for line in head):
        return "marker"
    if path.name.lower().endswith("-template.md"):
        return "template-pattern"
    return None


def strip_fenced_blocks_for_scan(text: str) -> str:
    """Replace fenced code blocks with blank lines so prose-style scanners see only narrative.

    The first YAML metadata block is preserved (its `schema_version` line is a real declaration).
    Subsequent fenced blocks (illustrative examples, JSON snippets, etc.) are masked.
    """
    lines = text.splitlines()
    output: list[str] = []
    in_fence = False
    fence_lang: str | None = None
    yaml_block_seen = False
    for line in lines:
        stripped = line.strip()
        if not in_fence and stripped.startswith("```"):
            fence_lang = stripped[3:].strip().lower() or None
            keep = (fence_lang in {"yaml", "yml"}) and not yaml_block_seen
            if keep:
                yaml_block_seen = True
            in_fence = True
            output.append(line if keep else "")
            output[-1] = output[-1] if keep else ""
            output.append("__FENCE_KEEP__" if keep else "__FENCE_MASK__")
            continue
        if in_fence and stripped == "```":
            in_fence = False
            mode = output[-1]
            output[-1] = line if mode == "__FENCE_KEEP__" else ""
            fence_lang = None
            continue
        if in_fence:
            mode = output[-1] if output and output[-1] in {"__FENCE_KEEP__", "__FENCE_MASK__"} else "__FENCE_MASK__"
            output.append(line if mode == "__FENCE_KEEP__" else "")
        else:
            output.append(line)
    return "\n".join(output)


def find_schema_markers(text: str) -> list[SchemaMarker]:
    markers: list[SchemaMarker] = []
    pattern = re.compile(r"(schema_version\s*:\s*|Schema version:\s*`?)([A-Za-z0-9/_\-.]+)", re.IGNORECASE)
    scan_text = strip_fenced_blocks_for_scan(text)
    for index, line in enumerate(scan_text.splitlines(), start=1):
        match = pattern.search(line)
        if match:
            source = "yaml" if match.group(1).lower().startswith("schema_version") else "markdown"
            markers.append(SchemaMarker(match.group(2).strip("`'\""), index, source))
    return markers


def identify_schema(path: Path, markers: list[SchemaMarker]) -> tuple[list[Diagnostic], str | None]:
    if not markers:
        return [diag(path, None, "schema-version-missing", "Schema", "schema_version", None, "Declare one supported schema version.")], None

    values = {m.value for m in markers}
    supported_values = values & SUPPORTED_SCHEMAS
    if not supported_values:
        return [diag(path, None, "schema-version-unsupported", "Schema", "schema_version", markers[0].line, "Supported schemas: query-operational-evidence/v1, signalr-operational-evidence/v1.")], None

    if len(values) > 1:
        return [diag(path, None, "schema-version-contradictory", "Schema", "schema_version", markers[0].line, "Different schema values declared. Use exactly one supported schema.")], None

    source_counts: dict[str, int] = {}
    for marker in markers:
        source_counts[marker.source] = source_counts.get(marker.source, 0) + 1
    if any(count > 1 for count in source_counts.values()):
        return [diag(path, None, "schema-version-duplicate", "Schema", "schema_version", markers[0].line, "Same schema declared multiple times in one source. Keep one canonical declaration per source.")], None

    return [], next(iter(supported_values))


def parse_first_yaml_block(lines: list[str]) -> tuple[dict[str, str], int | None, int | None]:
    in_block = False
    start_line: int | None = None
    metadata: dict[str, str] = {}
    key_pattern = re.compile(r"^[A-Za-z_][\w.\-]*$")
    for index, line in enumerate(lines, start=1):
        if not in_block and line.strip().lower() in {"```yaml", "```yml"}:
            in_block = True
            start_line = index + 1
            continue
        if in_block and line.strip() == "```":
            return metadata, start_line, None
        if in_block:
            if not line.strip() or line.lstrip().startswith("#"):
                continue
            if ":" not in line:
                return metadata, start_line, index
            key, value = line.split(":", 1)
            key = key.strip()
            if not key_pattern.match(key):
                return metadata, start_line, index
            stripped_value = value.strip()
            if (stripped_value.startswith('"') and not stripped_value.endswith('"')) or (
                stripped_value.startswith("'") and not stripped_value.endswith("'")
            ):
                return metadata, start_line, index
            metadata[key] = stripped_value.strip('"').strip("'")
    return metadata, start_line, None


def find_headings(lines: list[str]) -> dict[str, list[int]]:
    headings: dict[str, list[int]] = {}
    for index, line in enumerate(lines, start=1):
        match = re.match(r"^(#{2,6})\s+(.+?)\s*$", line)
        if match:
            heading = match.group(2).strip()
            headings.setdefault(heading.lower(), []).append(index)
    return headings


def validate_duplicate_headings(path: Path, schema: str | None, headings: dict[str, list[int]]) -> list[Diagnostic]:
    result: list[Diagnostic] = []
    for heading in ("run identity", "metadata", "controls", "redaction"):
        lines = headings.get(heading, [])
        if len(lines) > 1:
            result.append(diag(path, schema, "parse-heading-duplicate", heading.title(), None, lines[1], "Keep required section headings unique."))
    return result


REQUIRED_TABLE_HEADINGS = {
    "controls",
    "cache state matrix",
    "cache-state matrix",
    "latency calculation",
    "correlation",
    "correlation matrix",
    "scenario matrix",
    "false-positive controls",
    "fail-closed reviewer checklist",
}


def validate_tables(path: Path, schema: str | None, lines: list[str]) -> list[Diagnostic]:
    result: list[Diagnostic] = []
    previous_pipe_count: int | None = None
    table_active = False
    current_heading: str = ""
    for index, line in enumerate(lines, start=1):
        stripped = line.strip()
        heading_match = re.match(r"^#{2,6}\s+(.+?)\s*$", stripped)
        if heading_match:
            current_heading = heading_match.group(1).strip().lower()
            previous_pipe_count = None
            table_active = False
            continue
        if not stripped.startswith("|"):
            previous_pipe_count = None
            table_active = False
            continue
        pipe_count = stripped.count("|")
        if previous_pipe_count is not None and pipe_count != previous_pipe_count:
            result.append(diag(path, schema, "parse-table-malformed", "Markdown table", None, index, "Keep each table row at the same column count."))
            previous_pipe_count = pipe_count
            table_active = False
            continue
        previous_pipe_count = pipe_count
        if re.match(r"^\|\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?$", stripped):
            table_active = True
            continue
        if table_active and current_heading in REQUIRED_TABLE_HEADINGS:
            cells = [cell.strip() for cell in stripped.strip("|").split("|")]
            if any(cell == "" for cell in cells):
                result.append(diag(path, schema, "required-table-cell-empty", current_heading.title(), None, index, "Fill required table cells or mark the row out of scope explicitly."))
    return result


def validate_required_metadata(
    path: Path,
    schema: str | None,
    required: set[str],
    metadata: dict[str, str],
    metadata_line: int | None,
    rule: str,
) -> list[Diagnostic]:
    result: list[Diagnostic] = []
    for field in sorted(required):
        if not clean_value(metadata.get(field)):
            result.append(diag(path, schema, rule, "Metadata", field, metadata_line, "Add the required metadata field with a concrete value."))
    return result


def validate_required_value(
    path: Path,
    schema: str | None,
    metadata: dict[str, str],
    metadata_line: int | None,
    field: str,
    rule: str,
) -> list[Diagnostic]:
    if clean_value(metadata.get(field)):
        return []
    return [diag(path, schema, rule, "Controls", field, metadata_line, "Record the required control result and same-run linkage.")]


def validate_control_linkage(
    path: Path,
    schema: str | None,
    metadata: dict[str, str],
    metadata_line: int | None,
    field: str,
) -> list[Diagnostic]:
    value = metadata.get(field)
    if not clean_value(value):
        return []

    evidence_run_id = normalize_reference(metadata.get("evidence_run_id", ""))
    linked_control_run_ids = parse_linked_control_run_ids(metadata.get("linked_control_run_ids", ""))
    evidence_refs = set(extract_references(value, "evidence_run_id"))
    control_refs = set(extract_references(value, "control_run_id"))

    if not evidence_refs and not control_refs:
        return [
            diag(
                path,
                schema,
                "control-linkage-missing",
                "Controls",
                field,
                metadata_line,
                "Add an explicit evidence_run_id:<same-run> reference or control_run_id:<linked-control-run> reference.",
            ),
        ]

    invalid_evidence_refs = sorted(ref for ref in evidence_refs if ref != evidence_run_id)
    invalid_control_refs = sorted(ref for ref in control_refs if ref not in linked_control_run_ids)
    if invalid_evidence_refs or invalid_control_refs:
        mismatches = []
        if invalid_evidence_refs:
            mismatches.append(f"evidence_run_id {', '.join(invalid_evidence_refs)} does not match {evidence_run_id}")
        if invalid_control_refs:
            allowed = ", ".join(sorted(linked_control_run_ids)) or "(none declared in linked_control_run_ids)"
            mismatches.append(f"control_run_id {', '.join(invalid_control_refs)} is not in linked control runs {allowed}")
        return [
            diag(
                path,
                schema,
                "control-linkage-unrelated",
                "Controls",
                field,
                metadata_line,
                "; ".join(mismatches),
            ),
        ]

    return []


def parse_linked_control_run_ids(value: str) -> set[str]:
    if not clean_value(value):
        return set()
    return {normalize_reference(part.strip()) for part in re.split(r"[,;\s]+", value) if part.strip()}


_REFERENCE_TRAILING_PUNCT = ".,;:)]'\"`"


def normalize_reference(value: str) -> str:
    """Strip trailing punctuation that authors commonly append (sentence
    enders, list separators, closing brackets/quotes). Applied symmetrically
    to inline references and to the metadata value they are compared against.
    """
    return value.rstrip(_REFERENCE_TRAILING_PUNCT)


def extract_references(value: str | None, key: str) -> list[str]:
    if value is None:
        return []
    # Surrounding quotes are tolerated because YAML strips them from metadata
    # values but authors may still paste quoted inline references inside the
    # control field's free-form text.
    pattern = re.compile(rf"\b{re.escape(key)}\s*[:=]\s*[\"']?([A-Za-z0-9_.:-]+)[\"']?")
    return [normalize_reference(match.group(1)) for match in pattern.finditer(value)]


def validate_classification(
    path: Path,
    schema: str | None,
    metadata: dict[str, str],
    metadata_line: int | None,
    field: str,
    allowed: set[str],
) -> list[Diagnostic]:
    value = metadata.get(field, "")
    if not value or value in allowed:
        return []
    return [diag(path, schema, "classification-invalid", "Metadata", field, metadata_line, f"Use one of: {', '.join(sorted(allowed))}.")]


NOT_APPLICABLE_STOPWORDS = {"", "-", "n/a", "na", "tbd", "todo", "?", "none", "unspecified"}


def validate_not_applicable(path: Path, schema: str | None, metadata: dict[str, str], metadata_line: int | None) -> list[Diagnostic]:
    result: list[Diagnostic] = []
    for field, value in metadata.items():
        if not value.lower().startswith("not-applicable"):
            continue
        reason = value.split(":", 1)[1].strip() if ":" in value else ""
        if reason.lower() in NOT_APPLICABLE_STOPWORDS:
            result.append(diag(path, schema, "not-applicable-reason-missing", "Metadata", field, metadata_line, "Use 'not-applicable: <reason>' with a specific, non-generic reason."))
        if field not in PROFILE_ASPIRE_FIELDS:
            result.append(diag(path, schema, "not-applicable-not-allowed-here", "Metadata", field, metadata_line, "Only profile-scoped Aspire/DAPR fields may use not-applicable."))
    return result


def _is_aspire_profile(profile: str) -> bool:
    """Recognise an Aspire/DAPR runtime-proof profile.

    Match by token rather than substring so values like `non-aspire-static-fixture`
    do not accidentally claim Aspire scope.
    """
    tokens = re.split(r"[^a-z0-9]+", profile.lower())
    return "aspire" in tokens or "dapr" in tokens


def validate_profile_scope(path: Path, schema: str | None, metadata: dict[str, str], metadata_line: int | None) -> list[Diagnostic]:
    profile = metadata.get("run_profile", "").lower()
    if not _is_aspire_profile(profile):
        return []
    missing = [field for field in sorted(PROFILE_ASPIRE_FIELDS) if not clean_value(metadata.get(field))]
    if not missing:
        return []
    return [diag(path, schema, "profile-aspire-fields-missing", "Metadata", ",".join(missing), metadata_line, "Aspire/DAPR proof profiles require AppHost, DAPR placement/scheduler, and resource snapshot fields.")]


def validate_placeholders(path: Path, schema: str | None, lines: list[str], metadata: dict[str, str]) -> list[Diagnostic]:
    """Flag unreplaced template tokens.

    `<scenario-id>` is intentionally a literal angle-bracket placeholder; bare `scenario-id` is a
    valid YAML key/value used by real evidence and must not trigger this rule.
    """
    placeholder = re.compile(r"<required>|<\.\.\.>|<scenario-id>|TODO\(dev\)", re.IGNORECASE)
    result: list[Diagnostic] = []
    current_heading = "Content"
    yaml_keys_by_value: dict[str, str] = {value: key for key, value in metadata.items()}
    for index, line in enumerate(lines, start=1):
        heading_match = re.match(r"^#{2,6}\s+(.+?)\s*$", line)
        if heading_match:
            current_heading = heading_match.group(1).strip()
            continue
        match = placeholder.search(line)
        if not match:
            continue
        field = None
        yaml_match = re.match(r"^\s*([A-Za-z_][\w.\-]*)\s*:", line)
        if yaml_match:
            field = yaml_match.group(1)
        elif match.group(0) in yaml_keys_by_value:
            field = yaml_keys_by_value[match.group(0)]
        result.append(diag(path, schema, "placeholder-unreplaced", current_heading, field, index, f"Replace template placeholder '{match.group(0)}' before evidence can close."))
    return result


def _has_redaction_section(headings: dict[str, list[int]]) -> bool:
    return any(name == "redaction" or name.startswith("redaction ") or name.startswith("redaction:") for name in headings)


def validate_redaction(path: Path, schema: str | None, text: str, lines: list[str], headings: dict[str, list[int]]) -> list[Diagnostic]:
    result: list[Diagnostic] = []
    if not _has_redaction_section(headings):
        result.append(diag(path, schema, "redaction-section-missing", "Redaction", None, None, "Add a Redaction section that states evidence was reviewed/redacted."))

    unsafe_patterns = [
        ("redaction-unsafe-bearer-token", re.compile(r"Bearer\s+eyJ[A-Za-z0-9_.-]+", re.IGNORECASE), "Redact bearer/JWT tokens."),
        (
            "redaction-unsafe-connection-string",
            re.compile(
                r"\b(Server|Password|Pwd|User\s*Id|Uid|Initial\s*Catalog|AccountKey|SharedAccessKey|AccessKey|Secret)\s*=\s*\S",
                re.IGNORECASE,
            ),
            "Redact connection strings and secret-bearing key/value pairs.",
        ),
        (
            "redaction-unsafe-production-hostname",
            re.compile(r"\b[a-z0-9-]+\.(prod|production|live)\.[a-z0-9.-]+\b", re.IGNORECASE),
            "Replace production hostnames with synthetic or redacted values.",
        ),
        (
            "redaction-raw-secret-marker",
            re.compile(r"\bAKIA[0-9A-Z]{16}\b|-----BEGIN [A-Z ]*PRIVATE KEY-----|\braw_secret\b|\bclient_secret\s*=", re.IGNORECASE),
            "Remove raw secret markers before committing evidence.",
        ),
    ]
    for rule, pattern, hint in unsafe_patterns:
        match = pattern.search(text)
        if match:
            result.append(diag(path, schema, rule, "Redaction", None, line_for_offset(lines, text, match.start()), hint))
    return result


def clean_value(value: str | None) -> bool:
    if value is None:
        return False
    stripped = value.strip().lower()
    return bool(stripped) and stripped not in {"-", "todo", "tbd", "n/a", "na"}


def line_for_offset(lines: list[str], text: str, offset: int) -> int:
    return text[:offset].count("\n") + 1


def diag(path: Path, schema: str | None, rule: str, section: str | None, field: str | None, line: int | None, hint: str) -> Diagnostic:
    return Diagnostic(str(path).replace("\\", "/"), schema, rule, section, field, line, hint)


def info(path: Path, schema: str | None, rule: str, section: str | None, field: str | None, line: int | None, hint: str) -> Diagnostic:
    return Diagnostic(str(path).replace("\\", "/"), schema, rule, section, field, line, hint, "info")


def has_errors(diagnostics: list[Diagnostic]) -> bool:
    return any(d.level == "error" for d in diagnostics)


def sort_diagnostics(diagnostics: list[Diagnostic]) -> list[Diagnostic]:
    return sorted(
        diagnostics,
        key=lambda d: (d.file.lower(), d.schema or "", d.rule, d.section or "", d.field or "", d.line or 0),
    )


def emit(diagnostics: list[Diagnostic], emit_json: bool) -> None:
    if emit_json:
        print(json.dumps({"diagnostics": [d.to_json() for d in diagnostics]}, indent=2))
        return
    if not diagnostics:
        print("PASS: operational evidence validation")
        return
    for d in diagnostics:
        location = f"{d.file}:{d.line}" if d.line is not None else d.file
        schema = d.schema or "(schema unknown)"
        section = d.section or "(section unknown)"
        field = d.field or "(field unknown)"
        level = d.level.upper()
        print(f"{level}: {location} | schema={schema} | rule={d.rule} | section={section} | field={field} | hint={d.hint}")


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
