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

QUERY_REQUIRED = {
    "schema_version",
    "evidence_run_id",
    "story_key",
    "run_profile",
    "final_classification",
    "reviewer_verdict",
    "redaction_statement",
    "false_positive_control",
    "correlation_control",
}

SIGNALR_REQUIRED = {
    "schema_version",
    "evidence_run_id",
    "story_key",
    "run_profile",
    "classification",
    "reviewer_verdict",
    "redaction_statement",
    "reliability_control",
}

PROFILE_ASPIRE_FIELDS = {
    "apphost_url",
    "dapr_placement",
    "dapr_scheduler",
    "resource_snapshot",
}

EXPECTED_FIXTURE_RULES: dict[str, set[str]] = {
    "query-valid-minimal.md": set(),
    "query-valid-not-applicable-aspire.md": set(),
    "query-invalid-missing-metadata.md": {"query-required-metadata-missing"},
    "query-invalid-placeholder-unreplaced.md": {"placeholder-unreplaced"},
    "query-invalid-empty-required-table-cell.md": {"required-table-cell-empty"},
    "query-invalid-classification-not-in-enum.md": {"classification-invalid"},
    "query-invalid-control-missing.md": {"control-required-missing"},
    "query-invalid-correlation-control-missing.md": {"correlation-control-required-missing"},
    "query-invalid-redaction-bearer-token.md": {"redaction-unsafe-bearer-token"},
    "query-invalid-redaction-connection-string.md": {"redaction-unsafe-connection-string"},
    "query-invalid-redaction-production-hostname.md": {"redaction-unsafe-production-hostname"},
    "query-invalid-redaction-section-missing.md": {"redaction-section-missing"},
    "query-invalid-raw-secret-marker.md": {"redaction-raw-secret-marker"},
    "query-invalid-not-applicable-empty-reason.md": {"not-applicable-reason-missing"},
    "query-invalid-not-applicable-on-required-field.md": {"not-applicable-not-allowed-here"},
    "query-invalid-aspire-claimed-but-fields-missing.md": {"profile-aspire-fields-missing"},
    "signalr-valid-minimal.md": set(),
    "signalr-invalid-missing-metadata.md": {"signalr-required-metadata-missing"},
    "signalr-invalid-placeholder-unreplaced.md": {"placeholder-unreplaced"},
    "signalr-invalid-classification-not-in-enum.md": {"classification-invalid"},
    "signalr-invalid-control-missing.md": {"control-required-missing"},
    "signalr-invalid-redaction-bearer-token.md": {"redaction-unsafe-bearer-token"},
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

    def to_json(self) -> dict[str, object]:
        data: dict[str, object] = {
            "file": self.file,
            "schema": self.schema,
            "rule": self.rule,
            "section": self.section,
            "field": self.field,
            "hint": self.hint,
        }
        if self.line is not None:
            data["line"] = self.line
        return data


@dataclass(frozen=True)
class SchemaMarker:
    value: str
    line: int
    source: str


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true", help="Emit JSON diagnostics only.")
    parser.add_argument("--self-test", action="store_true", help="Validate curated fixtures against expected rule ids.")
    parser.add_argument(
        "--fixtures-root",
        default="_bmad-output/test-artifacts/operational-evidence-validator/fixtures",
        help="Fixture root for --self-test.",
    )
    parser.add_argument("paths", nargs="*", help="Evidence markdown files or directories to validate.")
    args = parser.parse_args(argv)

    if args.self_test:
        return run_self_test(Path(args.fixtures_root), args.json)

    diagnostics = validate_paths(args.paths)
    emit(diagnostics, args.json)
    return 1 if diagnostics else 0


def run_self_test(fixtures_root: Path, emit_json: bool) -> int:
    diagnostics: list[Diagnostic] = []
    failures: list[str] = []

    for file_name, expected in sorted(EXPECTED_FIXTURE_RULES.items()):
        path = fixtures_root / file_name
        if not path.exists():
            failures.append(f"{file_name}: fixture missing")
            continue

        result = validate_file(path)
        rules = {d.rule for d in result}
        diagnostics.extend(result)
        if expected:
            missing = expected - rules
            if missing:
                failures.append(f"{file_name}: missing expected rule(s) {', '.join(sorted(missing))}")
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
    diagnostics.extend(validate_placeholders(path, schema, lines))
    diagnostics.extend(validate_redaction(path, schema, text, lines, sections))
    return sort_diagnostics(diagnostics)


def find_schema_markers(text: str) -> list[SchemaMarker]:
    markers: list[SchemaMarker] = []
    pattern = re.compile(r"(schema_version\s*:\s*|Schema version:\s*`?)([A-Za-z0-9/_\-.]+)", re.IGNORECASE)
    for index, line in enumerate(text.splitlines(), start=1):
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
        sources = {m.source for m in markers}
        rule = "schema-version-contradictory" if len(sources) > 1 else "schema-version-duplicate"
        return [diag(path, None, rule, "Schema", "schema_version", markers[0].line, "Use exactly one supported schema marker.")], None

    return [], next(iter(supported_values))


def parse_first_yaml_block(lines: list[str]) -> tuple[dict[str, str], int | None, int | None]:
    in_block = False
    start_line: int | None = None
    metadata: dict[str, str] = {}
    for index, line in enumerate(lines, start=1):
        if not in_block and line.strip().lower() in {"```yaml", "```yml"}:
            in_block = True
            start_line = index + 1
            continue
        if in_block and line.strip() == "```":
            return metadata, start_line, None
        if in_block:
            if line.count('"') % 2 == 1 or line.count("'") % 2 == 1:
                return metadata, start_line, index
            if not line.strip() or line.lstrip().startswith("#"):
                continue
            if ":" not in line:
                return metadata, start_line, index
            key, value = line.split(":", 1)
            key = key.strip()
            if not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", key):
                return metadata, start_line, index
            metadata[key] = value.strip().strip('"').strip("'")
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


def validate_tables(path: Path, schema: str | None, lines: list[str]) -> list[Diagnostic]:
    result: list[Diagnostic] = []
    previous_pipe_count: int | None = None
    table_active = False
    for index, line in enumerate(lines, start=1):
        stripped = line.strip()
        if not stripped.startswith("|"):
            previous_pipe_count = None
            table_active = False
            continue
        pipe_count = stripped.count("|")
        if previous_pipe_count is not None and pipe_count != previous_pipe_count:
            result.append(diag(path, schema, "parse-table-malformed", "Markdown table", None, index, "Keep each table row at the same column count."))
            continue
        previous_pipe_count = pipe_count
        if re.match(r"^\|\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?$", stripped):
            table_active = True
            continue
        if table_active:
            cells = [cell.strip() for cell in stripped.strip("|").split("|")]
            if any(cell == "" for cell in cells):
                result.append(diag(path, schema, "required-table-cell-empty", "Markdown table", None, index, "Fill required table cells or mark the row out of scope explicitly."))
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


def validate_not_applicable(path: Path, schema: str | None, metadata: dict[str, str], metadata_line: int | None) -> list[Diagnostic]:
    result: list[Diagnostic] = []
    for field, value in metadata.items():
        if not value.lower().startswith("not-applicable"):
            continue
        reason = value.split(":", 1)[1].strip() if ":" in value else ""
        if not reason:
            result.append(diag(path, schema, "not-applicable-reason-missing", "Metadata", field, metadata_line, "Use 'not-applicable: <reason>' with a specific reason."))
        if field not in PROFILE_ASPIRE_FIELDS:
            result.append(diag(path, schema, "not-applicable-not-allowed-here", "Metadata", field, metadata_line, "Only profile-scoped Aspire/DAPR fields may use not-applicable."))
    return result


def validate_profile_scope(path: Path, schema: str | None, metadata: dict[str, str], metadata_line: int | None) -> list[Diagnostic]:
    profile = metadata.get("run_profile", "").lower()
    if "aspire" not in profile and "dapr" not in profile:
        return []
    missing = [field for field in sorted(PROFILE_ASPIRE_FIELDS) if not clean_value(metadata.get(field))]
    if not missing:
        return []
    return [diag(path, schema, "profile-aspire-fields-missing", "Metadata", ",".join(missing), metadata_line, "Aspire/DAPR proof profiles require AppHost, DAPR placement/scheduler, and resource snapshot fields.")]


def validate_placeholders(path: Path, schema: str | None, lines: list[str]) -> list[Diagnostic]:
    placeholder = re.compile(r"<required>|<\.\.\.>|TODO\(dev\)|\bscenario-id\b", re.IGNORECASE)
    for index, line in enumerate(lines, start=1):
        if placeholder.search(line):
            return [diag(path, schema, "placeholder-unreplaced", "Content", None, index, "Replace template placeholders before evidence can close.")]
    return []


def validate_redaction(path: Path, schema: str | None, text: str, lines: list[str], headings: dict[str, list[int]]) -> list[Diagnostic]:
    result: list[Diagnostic] = []
    if "redaction" not in headings:
        result.append(diag(path, schema, "redaction-section-missing", "Redaction", None, None, "Add a Redaction section that states evidence was reviewed/redacted."))

    unsafe_patterns = [
        ("redaction-unsafe-bearer-token", re.compile(r"Bearer\s+eyJ[A-Za-z0-9_.-]+", re.IGNORECASE), "Redact bearer/JWT tokens."),
        ("redaction-unsafe-connection-string", re.compile(r"\b(Server|Password|AccountKey|SharedAccessKey)\s*=", re.IGNORECASE), "Redact connection strings and secret-bearing key/value pairs."),
        ("redaction-unsafe-production-hostname", re.compile(r"\b[a-z0-9-]+\.prod\.[a-z0-9.-]+\b", re.IGNORECASE), "Replace production hostnames with synthetic or redacted values."),
        ("redaction-raw-secret-marker", re.compile(r"\bAKIA[0-9A-Z]{16}\b|-----BEGIN [A-Z ]*PRIVATE KEY-----|\braw_secret\b|\bclient_secret\s*=", re.IGNORECASE), "Remove raw secret markers before committing evidence."),
    ]
    for rule, pattern, hint in unsafe_patterns:
        match = pattern.search(text)
        if match:
            result.append(diag(path, schema, rule, "Redaction", None, line_for_offset(lines, text, match.start()), hint))
    return result


def clean_value(value: str | None) -> bool:
    if value is None:
        return False
    stripped = value.strip()
    return bool(stripped) and stripped not in {"-", "TODO", "TBD"}


def line_for_offset(lines: list[str], text: str, offset: int) -> int:
    return text[:offset].count("\n") + 1


def diag(path: Path, schema: str | None, rule: str, section: str | None, field: str | None, line: int | None, hint: str) -> Diagnostic:
    return Diagnostic(str(path).replace("\\", "/"), schema, rule, section, field, line, hint)


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
        print(f"{location} | schema={schema} | rule={d.rule} | section={section} | field={field} | hint={d.hint}")


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
