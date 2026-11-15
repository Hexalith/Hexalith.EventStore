#!/usr/bin/env python3
"""Check the Phase 4 gate risk matrix and the G-HIGH-RISK Assurance Control, offline.

Exit codes: 0 = PASS, 1 = FAIL (one or more findings), 2 = bad input or usage.
An offline result is evidence only, never a seal: seal records are checked for
shape alone, and retrieving the sealed run from the CI platform is separate work.
"""

import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys


# Owner decision 2026-11-15, pinned independently of the matrix and the PRD table.
CLASSIFICATIONS = {
    "G-BASELINE": ("standard-control", "planning-consistency"),
    "G-OQ8": ("high-risk", None),
    "G-TENANT": ("high-risk", None),
    "G-STATUS-ID": ("high-risk", None),
    "G-APPEND": ("high-risk", None),
    "G-CLAUSE": ("standard-control", "traceability-accounting"),
    "G-NFR8": ("standard-control", "approved-spec-no-seal"),
    "G-COMPAT": ("high-risk", None),
    "G-AUTH-HOSTS": ("high-risk", None),
    "G-RUNTIME-PARITY": ("high-risk", None),
    "G-PUBLICATION-AUTH": ("high-risk", None),
    "G-CONSUMER": ("high-risk", None),
    "G-HIGH-RISK": ("high-risk", None),
    "G-NFR18": ("standard-control", "documentation-review"),
    "G-NFR-OWNERSHIP": ("standard-control", "ownership-accounting"),
    "G-MVP-COVERAGE": ("high-risk", None),
    "G-READINESS": ("high-risk", None),
}
GATES = tuple(CLASSIFICATIONS)
HIGH_RISK_GATES = tuple(gate for gate, (kind, _) in CLASSIFICATIONS.items() if kind == "high-risk")

PRD = "_bmad-output/planning-artifacts/prd.md"
REGISTRY = "_bmad-output/implementation-artifacts/owner-role-registry.json"
PRD_TABLE_HEADER = "| Gate | Governed requirements / blockers |"
WORKFLOW = ".github/workflows/phase-4-gate-risk-matrix.yml"
MATRIX_SCHEMA = "hexalith.eventstore.phase-4-gate-risk-matrix.v1"
REGISTRY_SCHEMA = "hexalith.eventstore.owner-role-registry.v1"
RESULT_SCHEMA = "hexalith.eventstore.phase-4-gate-risk-matrix-result.v1"

LEVELS = ("none", "single-maintainer-attested", "independent")
TRANSITIONS = (
    "high-risk-gate-pass",
    "high-risk-nfr-story-done",
    "readiness",
    "release-available",
    "production-promoted",
    "consumer-removal",
)
DOWNSTREAM_RECORDS = ("READY", "release-available", "production-promoted", "consumer-removal")
WINDOW_SECONDS = 24 * 60 * 60

MATRIX_KEYS = ("schema", "registry", "gates", "downstream")
REGISTRY_KEYS = ("schema", "repository", "identities", "roles")
TEXT_BINDINGS = ("command", "trigger", "evidence_identity", "pass_condition")
BINDINGS = ("command", "trigger", "subject_paths", "evidence_identity", "pass_condition")
STANDARD_KEYS = ("gate", "classification", "reason")
HIGH_RISK_KEYS = (
    "gate", "classification", *BINDINGS, "binding_owner", "author", "author_role",
    "evaluator_roles", "required_level", "achieved_level", "guarded_transitions",
    "attestations", "seal",
)
SEAL_KEYS = ("workflow", "run_id", "head_sha", "workflow_sha256", "identity")
ATTESTATION_KEYS = ("identity", "role", "required_level", "subject_sha256", "row_sha256", "created_at", "evidence")
ROW_DIGEST_EXCLUDED = ("attestations", "seal", "achieved_level")
DOWNSTREAM_KEYS = ("record", "inputs", "assurance_level")

HUMAN_ID = re.compile(r"github:[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})")
CI_ID = re.compile(r"github:[A-Za-z0-9][A-Za-z0-9-]{0,38}\[bot\]")
PERSONA_ID = re.compile(r"bmad:[a-z0-9][a-z0-9-]*")
ROLE_ID = re.compile(r"[a-z][a-z0-9-]*")
REASON_CODE = re.compile(r"[a-z][a-z0-9-]*")
BINDING_OWNER = re.compile(r"Story [0-9]+\.[0-9]+[a-z]?|OR[0-9]+")
REPOSITORY = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+")
SEAL_WORKFLOW = re.compile(r"\.github/workflows/[A-Za-z0-9._-]+\.ya?ml")
SHA1 = re.compile(r"[0-9a-f]{40}")
SHA256 = re.compile(r"[0-9a-f]{64}")
TIMESTAMP = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z")

# Stable finding codes; each one has an observed failing fixture in the test suite.
CODES = (
    "MATRIX_MALFORMED",
    "PRD_TABLE_MISSING",
    "PRD_GATE_UNCLASSIFIED",
    "PRD_GATE_ABSENT",
    "GATE_MISSING",
    "GATE_DUPLICATE",
    "GATE_UNKNOWN",
    "ROW_MALFORMED",
    "CLASSIFICATION_INVALID",
    "CLASSIFICATION_MISMATCH",
    "REASON_MISSING",
    "REASON_MISMATCH",
    "REGISTRY_MALFORMED",
    "REGISTRY_IDENTITY_INVALID",
    "REGISTRY_ROLE_INVALID",
    "REGISTRY_NO_HUMAN",
    "BINDING_UNBOUND",
    "BINDING_OWNER_INVALID",
    "SUBJECT_INVALID",
    "ROLE_UNKNOWN",
    "AUTHOR_INVALID",
    "TRANSITION_INVALID",
    "SEAL_MISSING",
    "SEAL_INVALID",
    "ATTESTATION_MISSING",
    "ATTESTATION_MALFORMED",
    "ATTESTATION_IDENTITY_UNKNOWN",
    "ATTESTATION_NOT_HUMAN",
    "ATTESTATION_ROLE",
    "ATTESTATION_SUBJECT_MISMATCH",
    "ATTESTATION_LEVEL_MISMATCH",
    "ATTESTATION_ROW_MISMATCH",
    "SUBJECT_HISTORY_UNAVAILABLE",
    "ATTESTATION_WINDOW",
    "ATTESTATION_FUTURE",
    "REQUIRED_LEVEL_MISMATCH",
    "ACHIEVED_LEVEL_MISMATCH",
    "INDEPENDENT_SINGLE_HUMAN",
    "INDEPENDENT_SELF_APPROVAL",
    "INDEPENDENT_ALIAS",
    "ACHIEVED_BELOW_REQUIRED",
    "DOWNSTREAM_INVALID",
    "DOWNSTREAM_INPUTS_INCOMPLETE",
    "DOWNSTREAM_LEVEL_OVERSTATED",
    "DOWNSTREAM_LEVEL_UNDERSTATED",
)


class InputError(Exception):
    """An input cannot be read or parsed; the validator exits 2."""


def sha256(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def read_local(root: Path, relative: str) -> bytes:
    """Read a repository file without following an external or symbolic input."""
    path = root
    for part in PurePosixPath(relative).parts:
        path /= part
        try:
            symbolic = path.is_symlink()
        except (OSError, ValueError) as error:
            raise InputError(f"{relative!r}: cannot read file ({error})") from error
        if symbolic:
            raise InputError(f"{relative!r}: symbolic links cannot supply an input")
    try:
        return path.read_bytes()
    except OSError as error:
        raise InputError(f"{relative!r}: cannot read file ({error.strerror})") from error


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate key {key!r}")
        result[key] = value
    return result


def _reject_constant(name):
    raise ValueError(f"non-standard JSON constant {name}")


def parse_json(content: bytes, relative: str) -> dict:
    try:
        value = json.loads(content.decode("utf-8"), object_pairs_hook=_unique_object,
                           parse_constant=_reject_constant)
    except (ValueError, RecursionError) as error:
        raise InputError(f"{relative}: malformed JSON ({error})") from error
    if not isinstance(value, dict):
        raise InputError(f"{relative}: malformed JSON (the top level must be an object)")
    return value


def repository_path(value) -> str | None:
    """Return a normalized repository-relative POSIX path, or None when unsafe."""
    if (not isinstance(value, str) or not value or "\\" in value or value != value.strip()
            or any(ord(character) < 0x20 for character in value)):
        return None
    if value.startswith("/") or any(part in ("", ".", "..") for part in value.split("/")):
        return None
    return value


def row_sha256(row: dict) -> str:
    """SHA-256 of the row's canonical JSON without its attestations, seal and achieved level."""
    bound = {key: value for key, value in row.items() if key not in ROW_DIGEST_EXCLUDED}
    return sha256(json.dumps(bound, sort_keys=True, separators=(",", ":")).encode("utf-8"))


def is_text(value) -> bool:
    return isinstance(value, str) and bool(value.strip())


def distinct_strings(value) -> bool:
    return (isinstance(value, list) and bool(value) and all(isinstance(item, str) for item in value)
            and len(set(value)) == len(value))


def git(root: Path, *arguments: str) -> str | None:
    """Run one read-only Git query against the root, isolated from caller GIT_* state."""
    environment = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
    try:
        completed = subprocess.run(
            ["git", "--literal-pathspecs", "-C", str(root), "-c", "log.showSignature=false", *arguments],
            capture_output=True, text=True, check=False, timeout=60, env=environment)
    except (OSError, subprocess.SubprocessError):
        return None
    return completed.stdout if completed.returncode == 0 else None


def source_sha(root: Path) -> str | None:
    output = git(root, "rev-parse", "--verify", "HEAD^{commit}")
    value = output.strip() if output else ""
    return value if SHA1.fullmatch(value) else None


def last_subject_change(root: Path, paths: list[str]) -> int | None:
    """Later of the author and committer times of the latest commit touching the paths.

    None when the history is unreadable or any subject path has an uncommitted, untracked
    or ignored change, because the last authored change is then not a commit at all.
    """
    pending = git(root, "status", "--porcelain=v1", "--untracked-files=all", "--ignored", "--", *paths)
    if pending is None or pending.strip():
        return None
    output = git(root, "log", "-1", "--format=%at %ct", "--", *paths)
    fields = output.split() if output else []
    if len(fields) != 2 or not all(field.isdigit() for field in fields):
        return None
    return max(int(field) for field in fields)


def subject_digest(files: list[tuple[str, str]]) -> str:
    """SHA-256 of the sorted `<sha256>  <path>` lines, as `sha256sum | LC_ALL=C sort` prints them."""
    lines = sorted(f"{digest}  {path}\n" for path, digest in files)
    return sha256("".join(lines).encode("utf-8"))


def prd_gates(content: str) -> list[str] | None:
    """Gate IDs from the PRD mandatory gate table, or None when the table is not found once."""
    lines = content.replace("\r\n", "\n").split("\n")
    headers = [index for index, line in enumerate(lines) if line.startswith(PRD_TABLE_HEADER)]
    if len(headers) != 1:
        return None
    gates = []
    for line in lines[headers[0] + 1:]:
        if not line.startswith("|"):
            break
        cell = line.split("|")[1].strip()
        if not re.fullmatch(r":?-+:?", cell.replace(" ", "")):
            gates.append(cell)
    return gates


def level_rank(level) -> int:
    return LEVELS.index(level) if level in LEVELS else 0


def utc(timestamp: int) -> str:
    return datetime.datetime.fromtimestamp(timestamp, datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


class Evaluation:
    """One offline evaluation of a matrix against its registry, the PRD table and Git history."""

    def __init__(self, root: Path, matrix: str, now: datetime.datetime):
        self.root = root
        self.matrix_path = matrix
        self.now = now
        self.inputs = []
        self.findings = []
        self.registry = None
        self.transitions = {}
        self.rows = []
        self.downstream = []

    def add(self, scope: str, code: str, message: str) -> None:
        assert code in CODES, code
        self.findings.append({"scope": scope, "code": code, "message": message})

    def read_input(self, relative: str) -> bytes:
        content = read_local(self.root, relative)
        self.inputs.append({"path": relative, "sha256": sha256(content)})
        return content

    def run(self) -> None:
        matrix = parse_json(self.read_input(self.matrix_path), self.matrix_path)
        registry_path = self.check_matrix(matrix)
        if registry_path is not None:
            self.check_registry(parse_json(self.read_input(registry_path), registry_path))
        try:
            table = self.read_input(PRD).decode("utf-8")
        except UnicodeError as error:
            raise InputError(f"{PRD}: invalid UTF-8") from error
        gates = matrix.get("gates") if isinstance(matrix.get("gates"), list) else []
        rows = self.check_inventory(gates, prd_gates(table))
        for gate in GATES:
            if gate in rows:
                self.rows.append(self.check_row(rows[gate]))
            else:
                self.rows.append({"gate": gate, "classification": None, "result": "FAIL",
                                  "codes": ["GATE_MISSING"]})
        records = matrix.get("downstream") if isinstance(matrix.get("downstream"), list) else []
        for index, record in enumerate(records):
            self.downstream.append(self.check_downstream(index, record))
        ready = sum(1 for record in self.downstream if record.get("record") == "READY")
        if ready != 1:
            self.add("downstream", "DOWNSTREAM_INVALID", f"exactly one READY record is required; found {ready}")
        for row in self.rows:
            codes = sorted({finding["code"] for finding in self.findings if finding["scope"] == row["gate"]},
                           key=CODES.index)
            row["codes"] = codes
            row["result"] = "FAIL" if codes else "PASS"

    def check_matrix(self, matrix: dict) -> str | None:
        keys = set(matrix)
        if keys != set(MATRIX_KEYS):
            self.add("matrix", "MATRIX_MALFORMED",
                     f"keys must be exactly {', '.join(MATRIX_KEYS)}; missing {sorted(set(MATRIX_KEYS) - keys)}, "
                     f"unexpected {sorted(keys - set(MATRIX_KEYS))}")
        if matrix.get("schema") != MATRIX_SCHEMA:
            self.add("matrix", "MATRIX_MALFORMED", f"schema must be {MATRIX_SCHEMA!r}")
        for key in ("gates", "downstream"):
            if not isinstance(matrix.get(key), list):
                self.add("matrix", "MATRIX_MALFORMED", f"{key} must be a list")
        if matrix.get("registry") != REGISTRY:
            self.add("matrix", "MATRIX_MALFORMED", f"registry must be {REGISTRY!r}")
            return None
        return REGISTRY

    def check_registry(self, document: dict) -> None:
        keys = set(document)
        if keys != set(REGISTRY_KEYS):
            self.add("registry", "REGISTRY_MALFORMED",
                     f"keys must be exactly {', '.join(REGISTRY_KEYS)}; missing {sorted(set(REGISTRY_KEYS) - keys)}, "
                     f"unexpected {sorted(keys - set(REGISTRY_KEYS))}")
        if document.get("schema") != REGISTRY_SCHEMA:
            self.add("registry", "REGISTRY_MALFORMED", f"schema must be {REGISTRY_SCHEMA!r}")
        if not isinstance(document.get("repository"), str) or not REPOSITORY.fullmatch(document["repository"]):
            self.add("registry", "REGISTRY_MALFORMED", "repository must be <owner>/<name>")
        identities = {}
        entries = document.get("identities")
        if not isinstance(entries, list):
            self.add("registry", "REGISTRY_MALFORMED", "identities must be a list")
            entries = []
        for index, entry in enumerate(entries):
            problem = self.identity_problem(entry)
            identity = entry.get("id") if isinstance(entry, dict) else None
            if problem is None and identity in identities:
                problem = "duplicate identity id"
            if problem is not None:
                self.add("registry", "REGISTRY_IDENTITY_INVALID", f"identities[{index}] {identity!r}: {problem}")
                continue
            identities[identity] = {"kind": entry["kind"], "github_id": entry.get("github_id")}
        roles = {}
        mapping = document.get("roles")
        if not isinstance(mapping, dict) or not mapping:
            self.add("registry", "REGISTRY_MALFORMED", "roles must be a non-empty object")
            mapping = {}
        for role, holders in mapping.items():
            problem = None
            if not ROLE_ID.fullmatch(role):
                problem = "role names are kebab-case"
            elif (not isinstance(holders, list) or not holders
                  or not all(isinstance(holder, str) for holder in holders)
                  or len(set(holders)) != len(holders)):
                problem = "holders must be a non-empty list of distinct identity ids"
            else:
                strangers = [holder for holder in holders
                             if identities.get(holder, {}).get("kind") != "human"]
                if strangers:
                    problem = f"holders {strangers} are not registry humans; tool personas and CI never hold roles"
            if problem is not None:
                self.add("registry", "REGISTRY_ROLE_INVALID", f"{role!r}: {problem}")
                continue
            roles[role] = list(holders)
        humans = len({value["github_id"] for value in identities.values() if value["kind"] == "human"})
        if humans == 0:
            self.add("registry", "REGISTRY_NO_HUMAN",
                     "no human identity: the required Assurance Control level cannot be computed")
        required = None if humans == 0 else ("single-maintainer-attested" if humans == 1 else "independent")
        self.registry = {"identities": identities, "roles": roles, "humans": humans, "required_level": required}

    @staticmethod
    def identity_problem(entry) -> str | None:
        if not isinstance(entry, dict) or not isinstance(entry.get("id"), str):
            return "an identity is an object with a string id"
        kind = entry.get("kind")
        expected = {"id", "kind", "github_id"} if kind == "human" else {"id", "kind"}
        if set(entry) != expected:
            return f"keys must be exactly {sorted(expected)}"
        identity = entry["id"]
        if kind == "human":
            if not HUMAN_ID.fullmatch(identity):
                return "a human is github:<login>; bmad:* personas and [bot] accounts are never human"
            if type(entry["github_id"]) is not int or entry["github_id"] <= 0:
                return "a human needs a positive GitHub numeric id"
        elif kind == "tool-persona":
            if not PERSONA_ID.fullmatch(identity):
                return "a tool persona is bmad:<persona>"
        elif kind == "ci":
            if not CI_ID.fullmatch(identity):
                return "a CI identity is github:<app>[bot]"
        else:
            return "kind must be human, tool-persona or ci"
        return None

    def check_inventory(self, gates: list, table: list[str] | None) -> dict:
        rows = {}
        for index, row in enumerate(gates):
            gate = row.get("gate") if isinstance(row, dict) else None
            if not isinstance(gate, str):
                self.add("matrix", "ROW_MALFORMED", f"gates[{index}] must be an object with a string gate id")
            elif gate in rows:
                self.add(gate if gate in CLASSIFICATIONS else "matrix", "GATE_DUPLICATE",
                         f"gates[{index}] repeats {gate!r}")
            elif gate not in CLASSIFICATIONS:
                self.add("matrix", "GATE_UNKNOWN", f"gates[{index}] {gate!r} is not one of the 17 pinned Phase 4 gates")
                rows[gate] = row
            else:
                rows[gate] = row
        for gate in GATES:
            if gate not in rows:
                self.add(gate, "GATE_MISSING", f"{gate} is pinned but absent from the matrix")
        if table is None:
            self.add("prd", "PRD_TABLE_MISSING", f"{PRD}: the mandatory gate table header must appear exactly once")
            return {gate: row for gate, row in rows.items() if gate in CLASSIFICATIONS}
        for gate in table:
            if gate not in rows:
                self.add("prd", "PRD_GATE_UNCLASSIFIED", f"{PRD} gate-table row {gate!r} is absent from the matrix")
        for gate in GATES:
            if gate not in table:
                self.add("prd", "PRD_GATE_ABSENT", f"pinned gate {gate} is absent from the {PRD} gate table")
        return {gate: row for gate, row in rows.items() if gate in CLASSIFICATIONS}

    def check_row(self, row: dict) -> dict:
        gate = row["gate"]
        pinned, pinned_reason = CLASSIFICATIONS[gate]
        classification = row.get("classification")
        result = {"gate": gate, "classification": classification}
        if classification not in ("high-risk", "standard-control"):
            self.add(gate, "CLASSIFICATION_INVALID",
                     f"classification {classification!r} must be high-risk or standard-control")
            return result
        if classification != pinned:
            self.add(gate, "CLASSIFICATION_MISMATCH",
                     f"classified {classification}, but the owner decision pins {pinned}")
        if classification == "standard-control":
            self.check_keys(gate, row, STANDARD_KEYS, optional=("reason",))
            reason = row.get("reason")
            result["reason"] = reason
            if not isinstance(reason, str) or not REASON_CODE.fullmatch(reason):
                self.add(gate, "REASON_MISSING", "a standard-control classification needs a kebab-case reason code")
            elif pinned_reason is not None and reason != pinned_reason:
                self.add(gate, "REASON_MISMATCH", f"reason {reason!r}, but the owner decision pins {pinned_reason!r}")
            return result
        self.check_keys(gate, row, HIGH_RISK_KEYS)
        result.update(self.check_high_risk(gate, row))
        return result

    def check_keys(self, gate: str, row: dict, keys: tuple, optional: tuple = ()) -> None:
        missing = sorted(set(keys) - set(row) - set(optional))
        unexpected = sorted(set(row) - set(keys))
        if missing or unexpected:
            self.add(gate, "ROW_MALFORMED", f"missing keys {missing}, unexpected keys {unexpected}")

    def holds(self, identity, role) -> bool:
        return (self.registry is not None and isinstance(identity, str) and isinstance(role, str)
                and identity in self.registry["roles"].get(role, []))

    def check_high_risk(self, gate: str, row: dict) -> dict:
        registry = self.registry
        unbound = []
        for key in TEXT_BINDINGS:
            value = row.get(key)
            if value is None:
                unbound.append(key)
            elif not is_text(value):
                self.add(gate, "ROW_MALFORMED", f"{key} must be null or a non-empty string")
        paths = row.get("subject_paths")
        files, digest = [], None
        if paths is None:
            unbound.append("subject_paths")
        elif (not isinstance(paths, list) or not paths
              or any(repository_path(path) is None for path in paths) or len(set(paths)) != len(paths)):
            self.add(gate, "SUBJECT_INVALID", "subject_paths must be a non-empty list of distinct repository-relative paths")
        else:
            for path in paths:
                try:
                    files.append((path, sha256(read_local(self.root, path))))
                except InputError as error:
                    self.add(gate, "SUBJECT_INVALID", str(error))
            if len(files) == len(paths):
                digest = subject_digest(files)
        unbound.sort(key=BINDINGS.index)
        owner = row.get("binding_owner")
        if unbound:
            if not isinstance(owner, str) or not BINDING_OWNER.fullmatch(owner):
                self.add(gate, "BINDING_OWNER_INVALID",
                         f"unbound {', '.join(unbound)} must name its owning story (Story <n.m> or OR<n>)")
            self.add(gate, "BINDING_UNBOUND", f"{', '.join(unbound)} not yet defined; owner {owner!r}")
        elif owner is not None:
            self.add(gate, "BINDING_OWNER_INVALID", "binding_owner must be null once every binding is defined")

        author, author_role = row.get("author"), row.get("author_role")
        evaluators = row.get("evaluator_roles")
        if not distinct_strings(evaluators):
            self.add(gate, "ROW_MALFORMED", "evaluator_roles must be a non-empty list of distinct roles")
            evaluators = []
        if registry is not None:
            for role in [author_role, *evaluators]:
                if not isinstance(role, str) or role not in registry["roles"]:
                    self.add(gate, "ROLE_UNKNOWN", f"role {role!r} is not a valid owner-role registry role")
        identity = registry["identities"].get(author) if registry is not None and isinstance(author, str) else None
        if identity is None or identity["kind"] != "human" or not self.holds(author, author_role):
            self.add(gate, "AUTHOR_INVALID", f"author {author!r} must be a registry human holding {author_role!r}")
            author_id = None
        else:
            author_id = identity["github_id"]

        transitions = row.get("guarded_transitions")
        if distinct_strings(transitions):
            self.transitions[gate] = list(transitions)
        if (not distinct_strings(transitions) or "high-risk-gate-pass" not in transitions
                or any(transition not in TRANSITIONS for transition in transitions)):
            self.add(gate, "TRANSITION_INVALID",
                     f"guarded_transitions must be distinct values of {', '.join(TRANSITIONS)} "
                     "including high-risk-gate-pass")

        seal = row.get("seal")
        sealed = False
        if seal is None:
            self.add(gate, "SEAL_MISSING", "no seal record: the achieved level stays none")
        else:
            problem = self.seal_problem(seal)
            if problem:
                self.add(gate, "SEAL_INVALID", problem)
            else:
                sealed = True

        required = registry["required_level"] if registry is not None else None
        bound_row = row_sha256(row)
        approvers = self.check_attestations(gate, row, evaluators, paths if digest else None, digest,
                                            required, bound_row)
        if not approvers:
            self.add(gate, "ATTESTATION_MISSING", "no valid attestation by a registry human holding an evaluator role")
        humans = registry["humans"] if registry is not None else 0
        computed = "none"
        if sealed and approvers:
            computed = "single-maintainer-attested"
            if humans >= 2 and author_id is not None and any(
                    registry["identities"][approver]["github_id"] != author_id for approver in approvers):
                computed = "independent"

        declared_required, declared_achieved = row.get("required_level"), row.get("achieved_level")
        if required is not None and declared_required != required:
            self.add(gate, "REQUIRED_LEVEL_MISMATCH",
                     f"declares required {declared_required!r}; the registry names {humans} distinct human(s), "
                     f"so the required level is {required}")
        if declared_achieved != computed:
            self.add(gate, "ACHIEVED_LEVEL_MISMATCH",
                     f"declares achieved {declared_achieved!r}; the evidence achieves {computed}")
        if "independent" in (declared_required, declared_achieved) and humans < 2:
            self.add(gate, "INDEPENDENT_SINGLE_HUMAN",
                     f"independent is labelled while the registry names {humans} distinct human(s)")
        if declared_achieved == "independent" and humans >= 2 and computed != "independent":
            for approver in approvers:
                if approver == author:
                    self.add(gate, "INDEPENDENT_SELF_APPROVAL", f"approver {approver} is the author")
                elif author_id is not None and registry["identities"][approver]["github_id"] == author_id:
                    self.add(gate, "INDEPENDENT_ALIAS",
                             f"approver {approver} aliases author {author} (GitHub id {author_id})")
        if required is not None and level_rank(computed) < level_rank(required):
            self.add(gate, "ACHIEVED_BELOW_REQUIRED", f"achieved {computed} is below required {required}")
        return {
            "required_level": required,
            "declared_required_level": declared_required,
            "achieved_level": computed,
            "declared_achieved_level": declared_achieved,
            "unbound": unbound,
            "binding_owner": owner,
            "subjects": [{"path": path, "sha256": value} for path, value in files],
            "subject_sha256": digest,
            "row_sha256": bound_row,
        }

    def seal_problem(self, seal) -> str | None:
        if not isinstance(seal, dict) or set(seal) != set(SEAL_KEYS):
            return f"a seal record has exactly {', '.join(SEAL_KEYS)}"
        if not isinstance(seal["workflow"], str) or not SEAL_WORKFLOW.fullmatch(seal["workflow"]):
            return "seal workflow must be a .github/workflows/*.yml path"
        if type(seal["run_id"]) is not int or seal["run_id"] <= 0:
            return "seal run_id must be a positive integer"
        if not isinstance(seal["head_sha"], str) or not SHA1.fullmatch(seal["head_sha"]):
            return "seal head_sha must be 40 lowercase hex characters"
        if not isinstance(seal["workflow_sha256"], str) or not SHA256.fullmatch(seal["workflow_sha256"]):
            return "seal workflow_sha256 must be 64 lowercase hex characters"
        identities = self.registry["identities"] if self.registry is not None else {}
        if not isinstance(seal["identity"], str) or identities.get(seal["identity"], {}).get("kind") != "ci":
            return f"seal identity {seal['identity']!r} must be a registry ci identity"
        return None

    def check_attestations(self, gate, row, evaluators, paths, digest, required, bound_row) -> list[str]:
        attestations = row.get("attestations")
        if not isinstance(attestations, list):
            self.add(gate, "ROW_MALFORMED", "attestations must be a list")
            return []
        identities = self.registry["identities"] if self.registry is not None else {}
        approvers, history = [], {}
        for index, attestation in enumerate(attestations):
            label = f"attestations[{index}]"
            if not isinstance(attestation, dict) or set(attestation) != set(ATTESTATION_KEYS):
                self.add(gate, "ATTESTATION_MALFORMED", f"{label} has exactly {', '.join(ATTESTATION_KEYS)}")
                continue
            valid = True
            identity, role = attestation["identity"], attestation["role"]
            created = attestation["created_at"]
            if not is_text(attestation["evidence"]):
                self.add(gate, "ATTESTATION_MALFORMED", f"{label} evidence must name the authenticated record")
                valid = False
            if not isinstance(created, str) or not TIMESTAMP.fullmatch(created):
                self.add(gate, "ATTESTATION_MALFORMED", f"{label} created_at must be YYYY-MM-DDTHH:MM:SSZ")
                valid, created = False, None
            else:
                try:
                    created = datetime.datetime.strptime(created, "%Y-%m-%dT%H:%M:%SZ").replace(
                        tzinfo=datetime.timezone.utc)
                except ValueError:
                    self.add(gate, "ATTESTATION_MALFORMED", f"{label} created_at is not a real UTC time")
                    valid, created = False, None
            if not isinstance(identity, str) or identity not in identities:
                self.add(gate, "ATTESTATION_IDENTITY_UNKNOWN", f"{label} identity {identity!r} is not in the registry")
                continue
            if identities[identity]["kind"] != "human":
                self.add(gate, "ATTESTATION_NOT_HUMAN",
                         f"{label} identity {identity} is a {identities[identity]['kind']}, never an approver")
                continue
            if role not in evaluators or not self.holds(identity, role):
                self.add(gate, "ATTESTATION_ROLE", f"{label} {identity} does not hold evaluator role {role!r}")
                valid = False
            if digest is None or attestation["subject_sha256"] != digest:
                self.add(gate, "ATTESTATION_SUBJECT_MISMATCH",
                         f"{label} binds {attestation['subject_sha256']!r}; the computed subject digest is {digest}")
                valid = False
            if required is None or attestation["required_level"] != required:
                self.add(gate, "ATTESTATION_LEVEL_MISMATCH",
                         f"{label} binds required level {attestation['required_level']!r}; the registry requires "
                         f"{required}")
                valid = False
            if attestation["row_sha256"] != bound_row:
                self.add(gate, "ATTESTATION_ROW_MISMATCH",
                         f"{label} binds row {attestation['row_sha256']!r}; the row now digests to {bound_row}")
                valid = False
            if created is not None:
                if created > self.now:
                    self.add(gate, "ATTESTATION_FUTURE", f"{label} created_at is later than the evaluation time")
                    valid = False
                elif paths is not None:
                    if "time" not in history:
                        history["time"] = last_subject_change(self.root, paths)
                        if history["time"] is None:
                            self.add(gate, "SUBJECT_HISTORY_UNAVAILABLE",
                                     "the subject has an uncommitted change or no readable commit, so the 24 h "
                                     "window is unproven")
                    if history["time"] is None:
                        valid = False
                    elif created.timestamp() - history["time"] < WINDOW_SECONDS:
                        self.add(gate, "ATTESTATION_WINDOW",
                                 f"{label} created {created:%Y-%m-%dT%H:%M:%SZ}, less than 24 h after the last "
                                 f"subject change at {utc(history['time'])}")
                        valid = False
            if valid:
                approvers.append(identity)
        return approvers

    def check_downstream(self, index: int, record) -> dict:
        scope = f"downstream[{index}]"
        if not isinstance(record, dict) or set(record) != set(DOWNSTREAM_KEYS):
            self.add(scope, "DOWNSTREAM_INVALID", f"a downstream record has exactly {', '.join(DOWNSTREAM_KEYS)}")
            return {"scope": scope, "result": "FAIL"}
        kind, inputs, declared = record["record"], record["inputs"], record["assurance_level"]
        if kind not in DOWNSTREAM_RECORDS:
            self.add(scope, "DOWNSTREAM_INVALID", f"record must be one of {', '.join(DOWNSTREAM_RECORDS)}")
        if declared not in LEVELS:
            self.add(scope, "DOWNSTREAM_INVALID", f"assurance_level must be one of {', '.join(LEVELS)}")
        achieved = {row["gate"]: row.get("achieved_level") for row in self.rows
                    if row.get("classification") == "high-risk" and CLASSIFICATIONS[row["gate"]][0] == "high-risk"}
        if not distinct_strings(inputs) or any(gate not in achieved for gate in inputs):
            self.add(scope, "DOWNSTREAM_INVALID", "inputs must be distinct high-risk gates of this matrix")
            inputs = ([gate for gate in inputs if isinstance(gate, str) and gate in achieved]
                      if isinstance(inputs, list) else [])
        if kind == "READY":
            omitted = [gate for gate in HIGH_RISK_GATES if gate not in inputs]
            if omitted:
                self.add(scope, "DOWNSTREAM_INPUTS_INCOMPLETE", f"READY consumes every high-risk gate; omits {omitted}")
        elif kind in DOWNSTREAM_RECORDS:
            omitted = [gate for gate in HIGH_RISK_GATES if kind in self.transitions.get(gate, ()) and gate not in inputs]
            if omitted:
                self.add(scope, "DOWNSTREAM_INPUTS_INCOMPLETE",
                         f"{kind} consumes every high-risk gate guarding it; omits {omitted}")
        lowest = min((achieved[gate] for gate in inputs), key=level_rank, default="none")
        if declared in LEVELS and level_rank(declared) > level_rank(lowest):
            self.add(scope, "DOWNSTREAM_LEVEL_OVERSTATED", f"declares {declared} above the lowest input level {lowest}")
        elif declared in LEVELS and level_rank(declared) < level_rank(lowest):
            self.add(scope, "DOWNSTREAM_LEVEL_UNDERSTATED", f"declares {declared} below the lowest input level {lowest}")
        codes = sorted({finding["code"] for finding in self.findings if finding["scope"] == scope}, key=CODES.index)
        return {"scope": scope, "record": kind, "inputs": inputs, "declared_level": declared,
                "lowest_input_level": lowest, "result": "FAIL" if codes else "PASS", "codes": codes}


def workflow_sha256(root: Path) -> str | None:
    try:
        return sha256(read_local(root, WORKFLOW))
    except InputError:
        return None


def matrix_relative(roots: tuple[Path, ...], argument: str) -> tuple[Path, str]:
    candidate = Path(os.path.abspath(argument))
    for root in roots:
        try:
            relative = candidate.relative_to(root).as_posix()
        except ValueError:
            continue
        if repository_path(relative) is not None:
            return root, relative
    raise InputError(f"{argument}: the matrix must be a file inside the repository root {roots[0]}")


def build_result(evaluation, root, status, error=None) -> dict:
    result = {
        "schema": RESULT_SCHEMA,
        "result": {0: "PASS", 1: "FAIL"}.get(status, "ERROR"),
        "exit_code": status,
        "evidence_only": True,
        "seal": False,
        "authority": "none",
        "evaluated_at": evaluation.now.strftime("%Y-%m-%dT%H:%M:%SZ") if evaluation else None,
        "source_sha": source_sha(root),
        "workflow": {"path": WORKFLOW, "sha256": workflow_sha256(root)},
        "validator_sha256": sha256(Path(__file__).read_bytes()),
        "inputs": evaluation.inputs if evaluation else [],
    }
    if error is not None:
        result["error"] = error
        return result
    registry = evaluation.registry
    result.update({
        "registry": None if registry is None else {
            "humans": registry["humans"], "required_level": registry["required_level"]},
        "rows": evaluation.rows,
        "downstream": evaluation.downstream,
        "findings": evaluation.findings,
    })
    return result


def report(result: dict) -> str:
    lines = [
        f"Phase 4 gate risk matrix: {result['result']}",
        f"Source SHA: {result['source_sha'] or 'unavailable'}",
        f"Workflow SHA-256: {result['workflow']['sha256'] or 'absent'}  {result['workflow']['path']}",
        f"Validator SHA-256: {result['validator_sha256']}",
    ]
    lines += [f"Input SHA-256: {entry['sha256']}  {entry['path']}" for entry in result["inputs"]]
    if "error" in result:
        lines.append(f"Error: {result['error']}")
    else:
        registry = result["registry"]
        if registry is not None:
            lines.append(f"Registry: {registry['humans']} distinct human(s); required level "
                         f"{registry['required_level'] or 'not computable'}")
        for row in result["rows"]:
            codes = f" [{', '.join(row['codes'])}]" if row["codes"] else ""
            if row.get("classification") == "high-risk":
                detail = f"high-risk; required {row.get('required_level')}, achieved {row.get('achieved_level')}"
            elif row.get("classification") == "standard-control":
                detail = f"standard-control ({row.get('reason')!r})"
            else:
                detail = "unclassified"
            lines.append(f"{row['gate']}: {row['result']} {detail}{codes}")
        for record in result["downstream"]:
            codes = f" [{', '.join(record['codes'])}]" if record.get("codes") else ""
            lines.append(f"{record.get('record', 'record')!r} ({record['scope']}): {record['result']} declared "
                         f"{record.get('declared_level')!r}, lowest input {record.get('lowest_input_level')}{codes}")
    lines.append("Offline result: evidence only, never a seal; it grants no readiness, release, deployment, "
                 "migration or consumer removal authority.")
    return "\n".join(lines) + "\n"


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("matrix", help="gate risk matrix JSON; its registry path is read from the matrix")
    parser.add_argument("--root", default=str(Path(__file__).resolve().parents[1]),
                        help="EventStore checkout root (defaults to this script's repository)")
    parser.add_argument("--result", type=Path, help="write the JSON result with its input digests to this path")
    args = parser.parse_args(argv)
    roots = (Path(os.path.abspath(args.root)), Path(args.root).resolve())
    evaluation, error, root = None, None, roots[0]
    try:
        root, relative = matrix_relative(roots, args.matrix)
        evaluation = Evaluation(root, relative, datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0))
        evaluation.run()
        status = 1 if evaluation.findings else 0
    except InputError as failure:
        error, status = str(failure), 2
    result = build_result(evaluation, root, status, error)
    if args.result is not None:
        try:
            args.result.parent.mkdir(parents=True, exist_ok=True)
            args.result.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        except OSError as failure:
            print(f"phase-4-gate-risk-matrix: cannot write result {args.result} ({failure.strerror})", file=sys.stderr)
            return 2
    if error is not None:
        print(f"phase-4-gate-risk-matrix: {error}", file=sys.stderr)
    for finding in result.get("findings", []):
        print(f"phase-4-gate-risk-matrix: {finding['scope']} {finding['code']}: {finding['message']}", file=sys.stderr)
    sys.stderr.flush()
    sys.stdout.write(report(result))
    return status


if __name__ == "__main__":
    sys.exit(main())
