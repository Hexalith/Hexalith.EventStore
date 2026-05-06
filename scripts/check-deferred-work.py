#!/usr/bin/env python3
"""Read-only deferred-work governance checker for DW6."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path


COUNT_BUCKETS = [
    "OPEN",
    "STORY",
    "ACCEPTED-DEBT",
    "RESOLVED",
    "DUPLICATE",
    "NO-ACTION",
    "unclassified",
]

BLOCKING_RULES = {
    "dw6-unclassified-live-bullet",
    "dw6-open-missing-owner",
    "dw6-open-missing-next-review-date",
    "dw6-story-missing-owner",
    "dw6-story-missing-next-review-date",
    "dw6-missing-grouping",
}

DISPOSITION_PRECEDENCE = [
    "RESOLVED",
    "DUPLICATE",
    "NO-ACTION",
    "STORY",
    "ACCEPTED-DEBT",
    "OPEN",
]

FIXTURES = {
    "missing-open-metadata": """# Fixture

## Current Review

- **[OPEN] Missing owner and review date** - rationale: sample finding.
- **[STORY:post-epic-deferred-dw6-deferred-work-governance] Missing story owner and date** - grouping: post-epic-deferred.
""",
    "unclassified-live-bullet": """# Fixture

## Current Review

- Unclassified live finding with a diagnostic URL https://example.test/path?token=super-secret&mode=debug and password=abc123.
""",
    "legacy-mixed-marker": """# Fixture

## Historical Review

- Mixed marker sample. **STORY:post-epic-deferred-dw4-operational-evidence-schema-validation / RESOLVED-IN-VALIDATOR:** original text remains.
- Legacy accepted-debt sample.
  - DW1 disposition 2026-05-05: `accepted-debt` for terminal behavior.
""",
}


@dataclass(frozen=True)
class Diagnostic:
    file: str
    rule: str
    disposition: str | None
    heading: str
    excerpt: str
    line: int | None
    hint: str

    def as_dict(self) -> dict[str, object | None]:
        return {
            "file": self.file,
            "rule": self.rule,
            "disposition": self.disposition,
            "heading": self.heading,
            "excerpt": self.excerpt,
            "line": self.line,
            "hint": self.hint,
        }


@dataclass(frozen=True)
class Bullet:
    line: int
    heading: str
    text: str


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(
        description="Reports deferred-work disposition counts and governance diagnostics.",
    )
    parser.add_argument("paths", nargs="*", help="Markdown ledger paths to inspect.")
    parser.add_argument("--json", action="store_true", help="Emit machine-readable JSON.")
    parser.add_argument("--help-json", action="store_true", help="Emit JSON help and exit.")
    parser.add_argument("--legacy-advisory", action="store_true", help="Treat historical unclassified bullets as advisory.")
    parser.add_argument("--fixture", choices=sorted(FIXTURES), help="Run against a built-in deterministic fixture.")
    parser.add_argument("--max-excerpt", type=int, default=160, help="Maximum diagnostic excerpt length.")
    args = parser.parse_args(argv)

    if args.help_json:
        report = help_report()
        emit(report, json_output=True)
        return 0

    report = check(args)
    emit(report, json_output=args.json)
    return int(report["exitCode"])


def help_report() -> dict[str, object]:
    return {
        "exitCode": 0,
        "counts": {bucket: 0 for bucket in COUNT_BUCKETS},
        "diagnostics": [
            Diagnostic(
                "_bmad-output/implementation-artifacts/deferred-work.md",
                "dw6-help",
                None,
                "Deferred-Work Governance",
                "",
                None,
                "Exit code 1 is blocking for malformed canonical OPEN/STORY entries, missing owner or next-review-date, missing grouping, and unclassified live bullets. Legacy historical unclassified entries, unknown STORY:<id> existence checks, and secondary mixed markers are advisory. Exit code 0 can still include advisory or informational diagnostics.",
            ).as_dict()
        ],
    }


def check(args: argparse.Namespace) -> dict[str, object]:
    counts = {bucket: 0 for bucket in COUNT_BUCKETS}
    diagnostics: list[Diagnostic] = []
    known_story_keys = load_known_story_keys(Path.cwd())

    sources = load_sources(args)
    for display_path, content, fixture_mode in sources:
        for bullet in parse_bullets(content):
            if bullet.heading == "Deferred-Work Governance":
                continue

            classification, secondary = classify(bullet.text)
            counts[classification or "unclassified"] += 1

            if secondary:
                diagnostics.append(
                    Diagnostic(
                        display_path,
                        "dw6-secondary-disposition-advisory",
                        classification,
                        bullet.heading,
                        sanitize_excerpt(bullet.text, args.max_excerpt),
                        bullet.line,
                        f"Secondary disposition markers observed: {', '.join(secondary)}. Primary disposition follows documented precedence.",
                    )
                )

            if classification is None:
                rule = "dw6-unclassified-live-bullet" if fixture_mode else "dw6-unclassified-legacy-advisory"
                diagnostics.append(
                    Diagnostic(
                        display_path,
                        rule,
                        None,
                        bullet.heading,
                        sanitize_excerpt(bullet.text, args.max_excerpt),
                        bullet.line,
                        "Add a canonical disposition marker or leave as legacy-advisory until a curated sweep owns this heading.",
                    )
                )
                continue

            validate_metadata(display_path, bullet, classification, diagnostics, args.max_excerpt)
            validate_story_key(display_path, bullet, classification, diagnostics, known_story_keys, args.max_excerpt)

    diagnostics = sorted(
        diagnostics,
        key=lambda d: (d.file, d.heading, d.rule, d.disposition or "", d.line or 0, d.excerpt),
    )
    exit_code = 1 if any(d.rule in BLOCKING_RULES for d in diagnostics) else 0

    return {
        "exitCode": exit_code,
        "counts": counts,
        "diagnostics": [d.as_dict() for d in diagnostics],
    }


def load_sources(args: argparse.Namespace) -> list[tuple[str, str, bool]]:
    if args.fixture:
        return [(f"fixture:{args.fixture}", FIXTURES[args.fixture], True)]

    paths = args.paths or ["_bmad-output/implementation-artifacts/deferred-work.md"]
    sources: list[tuple[str, str, bool]] = []
    for raw in paths:
        path = Path(raw)
        display = display_path(path)
        sources.append((display, path.read_text(encoding="utf-8"), False))
    return sources


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(Path.cwd().resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def parse_bullets(content: str) -> list[Bullet]:
    bullets: list[Bullet] = []
    heading = ""
    for index, raw_line in enumerate(content.splitlines(), start=1):
        stripped = raw_line.strip()
        if stripped.startswith("#"):
            heading = stripped.lstrip("#").strip()
            continue

        match = re.match(r"^\s*[-*]\s+(.*)$", raw_line)
        if match:
            bullets.append(Bullet(index, heading, match.group(1).strip()))
    return bullets


def classify(text: str) -> tuple[str | None, list[str]]:
    markers: set[str] = set()

    if re.search(r"\[(OPEN)\]", text):
        markers.add("OPEN")
    if re.search(r"\[(STORY:[A-Za-z0-9._-]+)\]", text):
        markers.add("STORY")
    if re.search(r"\[(ACCEPTED-DEBT)\]", text):
        markers.add("ACCEPTED-DEBT")
    if re.search(r"\[(RESOLVED)\]", text):
        markers.add("RESOLVED")
    if re.search(r"\[(DUPLICATE)\]", text):
        markers.add("DUPLICATE")
    if re.search(r"\[(NO-ACTION)\]", text):
        markers.add("NO-ACTION")

    if re.search(r"\bSTORY:[A-Za-z0-9][A-Za-z0-9._-]*\b", text):
        markers.add("STORY")
    if "ACCEPTED-DEBT" in text:
        markers.add("ACCEPTED-DEBT")
    if "RESOLVED-IN-" in text or re.search(r"\bRESOLVED\b", text):
        markers.add("RESOLVED")
    if re.search(r"\bDUPLICATE\b", text):
        markers.add("DUPLICATE")
    if re.search(r"\bNO-ACTION\b", text):
        markers.add("NO-ACTION")

    lower = text.lower()
    if re.match(r"^\[[xX]\]\s+", text):
        markers.add("RESOLVED")
    if "dw1 disposition" in lower or "dw2 disposition" in lower or "dw3 disposition" in lower or "dw4 disposition" in lower or "dw5 disposition" in lower:
        if "accepted-debt" in lower:
            markers.add("ACCEPTED-DEBT")
        if "patch-now" in lower or "closed by" in lower:
            markers.add("RESOLVED")
        if "decision-now" in lower or "not-dw" in lower:
            markers.add("NO-ACTION")

    for disposition in DISPOSITION_PRECEDENCE:
        if disposition in markers:
            secondary = sorted(markers - {disposition})
            bucket = "STORY" if disposition == "STORY" else disposition
            return bucket, secondary

    return None, []


def validate_metadata(
    display_path: str,
    bullet: Bullet,
    classification: str,
    diagnostics: list[Diagnostic],
    max_excerpt: int,
) -> None:
    canonical_open_or_story = re.search(r"\[(OPEN|STORY:[A-Za-z0-9._-]+)\]", bullet.text) is not None
    if not canonical_open_or_story:
        return

    checks = [
        ("owner", f"dw6-{classification.lower()}-missing-owner"),
        ("next-review-date", f"dw6-{classification.lower()}-missing-next-review-date"),
        ("grouping", "dw6-missing-grouping"),
    ]

    for metadata_name, rule in checks:
        if not re.search(rf"\b{re.escape(metadata_name)}\s*:", bullet.text):
            diagnostics.append(
                Diagnostic(
                    display_path,
                    rule,
                    classification,
                    bullet.heading,
                    sanitize_excerpt(bullet.text, max_excerpt),
                    bullet.line,
                    f"Canonical {classification} entries require {metadata_name}.",
                )
            )

    if "next-review-date" in bullet.text and not re.search(r"next-review-date:\s*\d{4}-\d{2}-\d{2}\b", bullet.text):
        diagnostics.append(
            Diagnostic(
                display_path,
                f"dw6-{classification.lower()}-invalid-next-review-date",
                classification,
                bullet.heading,
                sanitize_excerpt(bullet.text, max_excerpt),
                bullet.line,
                "next-review-date must use YYYY-MM-DD format.",
            )
        )


def validate_story_key(
    display_path: str,
    bullet: Bullet,
    classification: str,
    diagnostics: list[Diagnostic],
    known_story_keys: set[str],
    max_excerpt: int,
) -> None:
    if classification != "STORY":
        return

    for story_id in re.findall(r"STORY:([A-Za-z0-9._-]+)", bullet.text):
        if story_id not in known_story_keys:
            diagnostics.append(
                Diagnostic(
                    display_path,
                    "dw6-story-id-advisory",
                    "STORY",
                    bullet.heading,
                    sanitize_excerpt(bullet.text, max_excerpt),
                    bullet.line,
                    f"STORY:{story_id} was not found as a repository story key. DW6 reports this as advisory.",
                )
            )


def load_known_story_keys(repo_root: Path) -> set[str]:
    keys: set[str] = set()
    artifact_root = repo_root / "_bmad-output" / "implementation-artifacts"
    if artifact_root.exists():
        keys.update(path.stem for path in artifact_root.glob("*.md"))

    sprint_status = artifact_root / "sprint-status.yaml"
    if sprint_status.exists():
        for line in sprint_status.read_text(encoding="utf-8").splitlines():
            match = re.match(r"^\s{2}([A-Za-z0-9._-]+):\s+\S+", line)
            if match:
                keys.add(match.group(1))

    return keys


def sanitize_excerpt(text: str, max_len: int) -> str:
    redacted = re.sub(r"https?://[^\s)]+?\?[^\s)]+", lambda m: m.group(0).split("?", 1)[0] + "?[redacted-query]", text)
    redacted = re.sub(r"(?i)\b(token|secret|password|pwd|apikey|api-key|access[_-]?key)=([^\s,;]+)", r"\1=[redacted]", redacted)
    redacted = " ".join(redacted.split())
    if len(redacted) <= max_len:
        return redacted
    return redacted[: max_len - 3].rstrip() + "..."


def emit(report: dict[str, object], json_output: bool) -> None:
    if json_output:
        print(json.dumps(report, indent=2, sort_keys=True))
        return

    counts = report["counts"]
    print("Deferred-work governance report")
    for bucket in COUNT_BUCKETS:
        print(f"- {bucket}: {counts[bucket]}")

    diagnostics = report["diagnostics"]
    if diagnostics:
        print("\nDiagnostics:")
        for item in diagnostics:
            locator = f"{item['file']}:{item['line']}" if item.get("line") else item["file"]
            print(f"- {item['rule']} [{item.get('disposition') or 'n/a'}] {locator} {item['heading']}: {item['excerpt']}")
    else:
        print("\nDiagnostics: none")

    print(f"\nExit code: {report['exitCode']}")


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
