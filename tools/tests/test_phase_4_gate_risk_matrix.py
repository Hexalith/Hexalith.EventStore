"""Gate classification and Assurance Control checks in disposable Git repositories."""

import copy
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = "tools/validate-phase-4-gate-risk-matrix.py"
SPEC = importlib.util.spec_from_file_location("phase_4_gate_risk_matrix", ROOT / SCRIPT)
CHECK = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK)

MATRIX = "_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json"
REGISTRY = "_bmad-output/implementation-artifacts/owner-role-registry.json"
PRD = "_bmad-output/planning-artifacts/prd.md"
WORKFLOW = ".github/workflows/phase-4-gate-risk-matrix.yml"
OQ8_WORKFLOW = ".github/workflows/oq8-governing-design.yml"

# Required coverage is pinned here independently of the implementation: the PRD G-HIGH-RISK
# gate list and the 2026-11-15 owner classification decision.
REQUIRED_GATES = (
    "G-BASELINE", "G-OQ8", "G-TENANT", "G-STATUS-ID", "G-APPEND", "G-CLAUSE", "G-NFR8", "G-COMPAT",
    "G-AUTH-HOSTS", "G-RUNTIME-PARITY", "G-PUBLICATION-AUTH", "G-CONSUMER", "G-HIGH-RISK", "G-NFR18",
    "G-NFR-OWNERSHIP", "G-MVP-COVERAGE", "G-READINESS",
)
STANDARD = {
    "G-BASELINE": "planning-consistency",
    "G-CLAUSE": "traceability-accounting",
    "G-NFR8": "approved-spec-no-seal",
    "G-NFR18": "documentation-review",
    "G-NFR-OWNERSHIP": "ownership-accounting",
}
HIGH_RISK = tuple(gate for gate in REQUIRED_GATES if gate not in STANDARD)
PRD_OWNER_ROLES = ("eventstore-owner", "release-owner", "deployment-owner", "consumer-owner",
                   "security-approver", "product-owner")
OWNER = {"id": "github:jpiquot", "kind": "human", "github_id": 6775094}
EXPECTED_CODES = (
    "MATRIX_MALFORMED", "PRD_TABLE_MISSING", "PRD_GATE_UNCLASSIFIED", "PRD_GATE_ABSENT", "GATE_MISSING",
    "GATE_DUPLICATE", "GATE_UNKNOWN", "ROW_MALFORMED", "CLASSIFICATION_INVALID", "CLASSIFICATION_MISMATCH",
    "REASON_MISSING", "REASON_MISMATCH", "REGISTRY_MALFORMED", "REGISTRY_IDENTITY_INVALID",
    "REGISTRY_ROLE_INVALID", "REGISTRY_NO_HUMAN", "BINDING_UNBOUND", "BINDING_OWNER_INVALID",
    "SUBJECT_INVALID", "ROLE_UNKNOWN", "AUTHOR_INVALID", "TRANSITION_INVALID", "SEAL_MISSING",
    "SEAL_INVALID", "ATTESTATION_MISSING", "ATTESTATION_MALFORMED", "ATTESTATION_IDENTITY_UNKNOWN",
    "ATTESTATION_NOT_HUMAN", "ATTESTATION_ROLE", "ATTESTATION_SUBJECT_MISMATCH", "ATTESTATION_LEVEL_MISMATCH",
    "ATTESTATION_ROW_MISMATCH", "SUBJECT_HISTORY_UNAVAILABLE", "ATTESTATION_WINDOW", "ATTESTATION_FUTURE", "REQUIRED_LEVEL_MISMATCH",
    "ACHIEVED_LEVEL_MISMATCH", "INDEPENDENT_SINGLE_HUMAN", "INDEPENDENT_SELF_APPROVAL", "INDEPENDENT_ALIAS",
    "ACHIEVED_BELOW_REQUIRED", "DOWNSTREAM_INVALID", "DOWNSTREAM_INPUTS_INCOMPLETE",
    "DOWNSTREAM_LEVEL_OVERSTATED", "DOWNSTREAM_LEVEL_UNDERSTATED",
)
# Real-repository rows may fail only on progress codes, so a legitimate attestation or seal never
# breaks the fixture job; structural, label-mismatch, INDEPENDENT_* and DOWNSTREAM_* codes may not.
PROGRESS_CODES = {"BINDING_UNBOUND", "SEAL_MISSING", "SEAL_INVALID", "SUBJECT_HISTORY_UNAVAILABLE",
                  "ACHIEVED_BELOW_REQUIRED", *(code for code in EXPECTED_CODES if code.startswith("ATTESTATION_"))}
# Fixture attestations bind the row and required level as they stand when the fixture is saved.
AUTO = "<bound at save>"
ROW_DIGEST_EXCLUDED = ("attestations", "seal", "achieved_level")
PINNED_ACTION = re.compile(r"(?m)^( +(?:- )?uses: actions/[A-Za-z0-9_.-]+)@[0-9a-f]{40} # v[0-9]+(?:\.[0-9]+)*$")

# Fixed fixture history: the committer date is later than the author date.
AUTHOR_DATE = "2026-01-01T00:00:00+00:00"
COMMITTER_DATE = "2026-01-01T06:00:00+00:00"
ATTESTED_AT = "2026-01-02T06:00:00Z"  # exactly 24 h after the later commit date
SUBJECTS = ("subject/alpha.txt", "subject/beta.txt")
SEAL = {
    "workflow": ".github/workflows/fixture-transition.yml",
    "run_id": 123456789,
    "head_sha": "0123456789abcdef0123456789abcdef01234567",
    "workflow_sha256": "89abcdef" * 8,
    "identity": "github:github-actions[bot]",
}
SINGLE = "single-maintainer-attested"
INDEPENDENT = "independent"

EXPECTED_WORKFLOW = """name: Phase 4 gate risk matrix

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:

permissions:
  contents: read

jobs:
  fixtures:
    name: Phase 4 gate risk matrix fixtures
    runs-on: ubuntu-24.04
    timeout-minutes: 5
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false
          submodules: false
      - name: Test classification and Assurance Control rejection paths
        run: python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_risk_matrix.py'

  live-evaluation:
    name: Phase 4 gate risk matrix live evaluation
    if: github.event_name == 'push' || github.event_name == 'workflow_dispatch'
    runs-on: ubuntu-24.04
    timeout-minutes: 5
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false
          submodules: false
          fetch-depth: 0
      - name: Evaluate and report the current matrix
        shell: bash
        run: |
{script}      - name: Upload the content-bound result
        if: always()
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
        with:
          name: phase-4-gate-risk-matrix-result-${{{{ github.run_id }}}}-${{{{ github.run_attempt }}}}
          path: ${{{{ runner.temp }}}}/phase-4-gate-risk-matrix/
          if-no-files-found: error
          retention-days: 90
"""
LIVE_SCRIPT = """status=0
output="$RUNNER_TEMP/phase-4-gate-risk-matrix"
mkdir -p "$output"
python3 -B tools/validate-phase-4-gate-risk-matrix.py _bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json --result "$output/result.json" >"$output/report.txt" 2>&1 || status=$?
source_sha="$(git rev-parse HEAD)"
workflow_digest="$(sha256sum .github/workflows/phase-4-gate-risk-matrix.yml | cut -d ' ' -f 1)"
result=FAIL
if [ "$status" -eq 0 ]; then
  result=PASS
fi
{
  printf 'Phase 4 gate risk matrix: %s (evidence only, never a seal)\\n\\nSource SHA: %s\\n\\nWorkflow SHA-256: %s\\n\\n```text\\n' "$result" "$source_sha" "$workflow_digest"
  cat "$output/report.txt"
  printf '```\\n'
} | tee -a "$GITHUB_STEP_SUMMARY"
exit "$status"
"""


def git_environment(**extra):
    environment = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
    environment.update({
        "GIT_CONFIG_GLOBAL": os.devnull,
        "GIT_CONFIG_NOSYSTEM": "1",
        "GIT_AUTHOR_NAME": "Fixture Author",
        "GIT_AUTHOR_EMAIL": "author@example.invalid",
        "GIT_COMMITTER_NAME": "Fixture Committer",
        "GIT_COMMITTER_EMAIL": "committer@example.invalid",
    }, **extra)
    return environment


def run_git(root, *arguments, **extra):
    return subprocess.run(["git", "-C", str(root), *arguments], check=True, capture_output=True, text=True,
                          env=git_environment(**extra)).stdout


def commit(root, message, author_date=AUTHOR_DATE, committer_date=COMMITTER_DATE):
    run_git(root, "add", "-A")
    run_git(root, "commit", "-q", "--no-verify", "-m", message,
            GIT_AUTHOR_DATE=author_date, GIT_COMMITTER_DATE=committer_date)


def digest_of(root, paths):
    """Independent subject digest: SHA-256 of the sorted `<sha256>  <path>` lines."""
    lines = sorted(f"{hashlib.sha256((root / path).read_bytes()).hexdigest()}  {path}\n" for path in paths)
    return hashlib.sha256("".join(lines).encode("utf-8")).hexdigest()


def row_digest(entry):
    """Independent row digest: canonical JSON without attestations, seal and achieved level."""
    bound = {key: value for key, value in entry.items() if key not in ROW_DIGEST_EXCLUDED}
    return hashlib.sha256(json.dumps(bound, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def fixture_prd(gates=REQUIRED_GATES):
    rows = "".join(f"| {gate} | fixture | fixture | fixture | fixture | fixture |\n" for gate in gates)
    return ("# Fixture PRD\n\n"
            "| Gate | Governed requirements / blockers | Required evidence and validation | Evaluator and approval "
            "| Current result and blocking effect | Waiver / invalidation |\n"
            "| --- | --- | --- | --- | --- | --- |\n" + rows +
            "\n**Post-MVP gate, excluded from the Phase 4 MVP aggregate:**\n\n"
            "| Gate | Governed scope | Current result | Rule |\n| --- | --- | --- | --- |\n"
            "| G5 | fixture | fixture | fixture |\n")


def fixture_registry(humans=1, alias=False, withhold=()):
    identities = [{"id": "github:alice", "kind": "human", "github_id": 101}]
    if humans >= 2:
        identities.append({"id": "github:bob", "kind": "human", "github_id": 202})
    if alias:
        identities.append({"id": "github:alice-admin", "kind": "human", "github_id": 101})
    holders = [identity["id"] for identity in identities]
    identities += [{"id": "bmad:murat", "kind": "tool-persona"},
                   {"id": "github:github-actions[bot]", "kind": "ci"}]
    return {
        "schema": "hexalith.eventstore.owner-role-registry.v1",
        "repository": "Example/Fixture",
        "identities": identities,
        "roles": {role: [holder for holder in holders if (holder, role) not in withhold]
                  for role in ("eventstore-owner", "test-owner")},
    }


def attestation(digest, identity="github:alice", created_at=ATTESTED_AT, role="test-owner"):
    return {
        "identity": identity,
        "role": role,
        "required_level": AUTO,
        "subject_sha256": digest,
        "row_sha256": AUTO,
        "created_at": created_at,
        "evidence": "https://github.com/Example/Fixture/pull/1#pullrequestreview-1",
    }


def fixture_matrix(digest, level=SINGLE):
    gates = []
    for gate in REQUIRED_GATES:
        if gate in STANDARD:
            gates.append({"gate": gate, "classification": "standard-control", "reason": STANDARD[gate]})
            continue
        gates.append({
            "gate": gate,
            "classification": "high-risk",
            "command": f"python3 -B tools/check-{gate.lower()}.py",
            "trigger": "push to main",
            "subject_paths": list(SUBJECTS),
            "evidence_identity": f"fixture result for {gate}",
            "pass_condition": "the command exits 0",
            "binding_owner": None,
            "author": "github:alice",
            "author_role": "eventstore-owner",
            "evaluator_roles": ["test-owner"],
            "required_level": level,
            "achieved_level": level,
            "guarded_transitions": ["high-risk-gate-pass", "readiness"],
            "attestations": [attestation(digest, "github:bob" if level == INDEPENDENT else "github:alice")],
            "seal": dict(SEAL),
        })
    return {
        "schema": "hexalith.eventstore.phase-4-gate-risk-matrix.v1",
        "registry": REGISTRY,
        "gates": gates,
        "downstream": [{"record": "READY", "inputs": list(HIGH_RISK), "assurance_level": level}],
    }


def row(test, gate="G-OQ8"):
    return next(entry for entry in test.matrix["gates"] if entry["gate"] == gate)


def all_high_risk(test):
    return [entry for entry in test.matrix["gates"] if entry["classification"] == "high-risk"]


def two_humans(test, level):
    test.registry = fixture_registry(humans=2)
    for entry in all_high_risk(test):
        entry["required_level"] = level
    test.matrix["downstream"][0]["assurance_level"] = SINGLE


def declare_independent(test, approver, alias=False, withhold=()):
    test.registry = fixture_registry(humans=2, alias=alias, withhold=withhold)
    for entry in all_high_risk(test):
        entry["required_level"] = INDEPENDENT
        entry["achieved_level"] = INDEPENDENT
        entry["attestations"] = [attestation(test.digest, "github:bob")]
    row(test)["attestations"] = [attestation(test.digest, approver)]
    test.matrix["downstream"][0]["assurance_level"] = SINGLE


def untracked_subject(test):
    (test.root / "subject/untracked.txt").write_text("never committed\n")
    paths = [*SUBJECTS, "subject/untracked.txt"]
    row(test)["subject_paths"] = paths
    row(test)["attestations"] = [attestation(digest_of(test.root, paths))]


def ignored_subject(test):
    test.write(".gitignore", "subject/*.ignored\n")
    commit(test.root, "ignore local subject scratch")
    (test.root / "subject/local.ignored").write_text("ignored, never committed\n")
    paths = [*SUBJECTS, "subject/local.ignored"]
    row(test)["subject_paths"] = paths
    row(test)["attestations"] = [attestation(digest_of(test.root, paths))]


def uncommitted_subject(test):
    (test.root / SUBJECTS[0]).write_text("alpha, not yet committed\n")
    row(test)["attestations"] = [attestation(digest_of(test.root, SUBJECTS))]


def later_subject_commit(test):
    (test.root / SUBJECTS[0]).write_text("alpha, revised\n")
    commit(test.root, "revise subject", "2026-01-02T00:00:00+00:00", "2026-01-02T00:00:00+00:00")
    digest = digest_of(test.root, SUBJECTS)
    for entry in all_high_risk(test):
        entry["attestations"] = [attestation(digest, created_at="2026-01-03T00:00:00Z")]
    row(test)["attestations"] = [attestation(digest, created_at=ATTESTED_AT)]


def author_later_commit(test):
    # The author date is later than the committer date; the window starts at the later of the two.
    (test.root / SUBJECTS[0]).write_text("alpha, authored late\n")
    commit(test.root, "author-dated revision", "2026-01-02T12:00:00+00:00", "2026-01-01T18:00:00+00:00")
    digest = digest_of(test.root, SUBJECTS)
    for entry in all_high_risk(test):
        entry["attestations"] = [attestation(digest, created_at="2026-01-03T12:00:00Z")]
    # 24 h after the committer date, but only 6 h after the author date.
    row(test)["attestations"] = [attestation(digest, created_at="2026-01-02T18:00:00Z")]


def pin_then_change(test, key, value):
    # The attestation binds the row as approved; the binding then changes underneath it.
    row(test)["attestations"][0]["row_sha256"] = row_digest(row(test))
    row(test)[key] = value


def foreign_registry(test):
    test.write("other/registry.json", json.dumps(fixture_registry(humans=2), indent=2) + "\n")
    test.matrix["registry"] = "other/registry.json"


def partial_consumer_removal(test):
    for gate in ("G-PUBLICATION-AUTH", "G-CONSUMER"):
        row(test, gate)["guarded_transitions"].append("consumer-removal")
    test.matrix["downstream"].append({"record": "consumer-removal", "inputs": ["G-OQ8"], "assurance_level": SINGLE})


def mutate_prd(test, old, new):
    path = test.root / PRD
    content = path.read_text()
    test.assertIn(old, content)
    path.write_text(content.replace(old, new, 1))


def remove_gate(test, gate):
    test.matrix["gates"] = [entry for entry in test.matrix["gates"] if entry["gate"] != gate]


def downgrade(test):
    entry = row(test, "G-TENANT")
    entry.clear()
    entry.update({"gate": "G-TENANT", "classification": "standard-control", "reason": "downgraded"})


def replace_seal(test, **fields):
    row(test)["seal"] = {**SEAL, **fields}


# One or more observed failing fixtures per stable code, each a mutation of the positive control.
NEGATIVE_FIXTURES = {
    "MATRIX_MALFORMED": [
        lambda t: t.matrix.update(schema="hexalith.eventstore.phase-4-gate-risk-matrix.v0"),
        lambda t: t.matrix.update(registry="../owner-role-registry.json"),
        lambda t: t.matrix.update(unexpected=True),
        foreign_registry,
        lambda t: t.matrix.update(registry=REGISTRY + "\x00"),
    ],
    "PRD_TABLE_MISSING": [lambda t: mutate_prd(t, "| Gate | Governed requirements / blockers |", "| Gate | Requirements |")],
    "PRD_GATE_UNCLASSIFIED": [lambda t: mutate_prd(t, "| G-READINESS |", "| G-NEW-GATE | x | x | x | x | x |\n| G-READINESS |")],
    "PRD_GATE_ABSENT": [lambda t: mutate_prd(t, "| G-NFR8 | fixture | fixture | fixture | fixture | fixture |\n", "")],
    "GATE_MISSING": [lambda t: remove_gate(t, "G-NFR18"), lambda t: remove_gate(t, "G-OQ8")],
    "GATE_DUPLICATE": [lambda t: t.matrix["gates"].append(copy.deepcopy(row(t, "G-CLAUSE")))],
    "GATE_UNKNOWN": [lambda t: t.matrix["gates"].append(
        {"gate": "G-UNPINNED", "classification": "standard-control", "reason": "unpinned"})],
    "ROW_MALFORMED": [lambda t: row(t).update(unexpected=True), lambda t: row(t).pop("trigger"),
                      lambda t: row(t).update(command=""), lambda t: row(t).update(evaluator_roles=[])],
    "CLASSIFICATION_INVALID": [lambda t: row(t, "G-CLAUSE").update(classification="medium-risk"),
                               lambda t: row(t, "G-CLAUSE").pop("classification")],
    "CLASSIFICATION_MISMATCH": [downgrade, lambda t: row(t, "G-BASELINE").update(classification="high-risk")],
    "REASON_MISSING": [lambda t: row(t, "G-NFR8").pop("reason"), lambda t: row(t, "G-NFR8").update(reason=""),
                       lambda t: row(t, "G-NFR8").update(reason=None)],
    "REASON_MISMATCH": [lambda t: row(t, "G-NFR8").update(reason="another-reason")],
    "REGISTRY_MALFORMED": [lambda t: t.registry.update(schema="other"), lambda t: t.registry.pop("repository"),
                           lambda t: t.registry.update(roles={})],
    "REGISTRY_IDENTITY_INVALID": [
        lambda t: t.registry["identities"].append({"id": "bmad:winston", "kind": "human", "github_id": 9}),
        lambda t: t.registry["identities"].append({"id": "github:ci[bot]", "kind": "human", "github_id": 9}),
        lambda t: t.registry["identities"].append({"id": "github:carol", "kind": "human"}),
        lambda t: t.registry["identities"].append({"id": "github:alice", "kind": "human", "github_id": 101}),
        lambda t: t.registry["identities"].append({"id": "github:dave", "kind": "reviewer"}),
    ],
    "REGISTRY_ROLE_INVALID": [lambda t: t.registry["roles"]["test-owner"].append("bmad:murat"),
                              lambda t: t.registry["roles"]["test-owner"].append("github:nobody")],
    "REGISTRY_NO_HUMAN": [lambda t: t.registry.update(identities=t.registry["identities"][1:])],
    "BINDING_UNBOUND": [lambda t: row(t).update(command=None, binding_owner="Story 4.17"),
                        lambda t: row(t).update(subject_paths=None, binding_owner="OR10")],
    "BINDING_OWNER_INVALID": [lambda t: row(t).update(command=None),
                              lambda t: row(t).update(command=None, binding_owner="someone"),
                              lambda t: row(t).update(binding_owner="Story 4.17")],
    "SUBJECT_INVALID": [lambda t: row(t).update(subject_paths=["../outside.txt"]),
                        lambda t: row(t).update(subject_paths=["/etc/hostname"]),
                        lambda t: row(t).update(subject_paths=[SUBJECTS[0], SUBJECTS[0]]),
                        lambda t: row(t).update(subject_paths=["subject/absent.txt"]),
                        lambda t: row(t).update(subject_paths=[]),
                        lambda t: row(t).update(subject_paths=["subject/alpha.txt\x00"]),
                        lambda t: row(t).update(subject_paths=["subject/al\npha.txt"])],
    "ROLE_UNKNOWN": [lambda t: row(t).update(evaluator_roles=["test-owner", "unknown-owner"]),
                     lambda t: row(t).update(author_role="unknown-owner")],
    "AUTHOR_INVALID": [lambda t: row(t).update(author="bmad:murat"), lambda t: row(t).update(author="github:mallory"),
                       lambda t: row(t).update(author_role="test-owner", author="github:github-actions[bot]"),
                       lambda t: setattr(t, "registry", fixture_registry(
                           alias=True, withhold={("github:alice", "eventstore-owner")}))],
    "TRANSITION_INVALID": [lambda t: row(t).update(guarded_transitions=["readiness"]),
                           lambda t: row(t).update(guarded_transitions=["high-risk-gate-pass", "merge-to-main"]),
                           lambda t: row(t).update(guarded_transitions=["high-risk-gate-pass", "high-risk-gate-pass"])],
    "SEAL_MISSING": [lambda t: row(t).update(seal=None)],
    "SEAL_INVALID": [lambda t: replace_seal(t, head_sha="abc"), lambda t: replace_seal(t, run_id=0),
                     lambda t: replace_seal(t, run_id=True), lambda t: replace_seal(t, workflow="ci.yml"),
                     lambda t: replace_seal(t, workflow_sha256="89abcdef"),
                     lambda t: replace_seal(t, identity="github:alice"),
                     lambda t: replace_seal(t, identity="bmad:murat"), lambda t: replace_seal(t, result="PASS"),
                     lambda t: row(t).update(seal="sealed")],
    "ATTESTATION_MISSING": [lambda t: row(t).update(attestations=[])],
    "ATTESTATION_MALFORMED": [lambda t: row(t)["attestations"][0].update(created_at="2026-01-02 06:00:00"),
                              lambda t: row(t)["attestations"][0].update(created_at="2026-02-30T00:00:00Z"),
                              lambda t: row(t)["attestations"][0].update(evidence=""),
                              lambda t: row(t)["attestations"][0].pop("evidence")],
    "ATTESTATION_IDENTITY_UNKNOWN": [lambda t: row(t)["attestations"][0].update(identity="github:mallory")],
    "ATTESTATION_NOT_HUMAN": [lambda t: row(t)["attestations"][0].update(identity="bmad:murat"),
                              lambda t: row(t)["attestations"][0].update(identity="github:github-actions[bot]")],
    "ATTESTATION_ROLE": [lambda t: row(t)["attestations"][0].update(role="eventstore-owner"),
                         lambda t: row(t)["attestations"][0].update(role="release-owner"),
                         lambda t: declare_independent(t, "github:bob", withhold={("github:bob", "test-owner")})],
    "ATTESTATION_SUBJECT_MISMATCH": [lambda t: row(t)["attestations"][0].update(subject_sha256="0" * 64),
                                     lambda t: (t.root / SUBJECTS[1]).write_text("beta, edited after approval\n")],
    "ATTESTATION_LEVEL_MISMATCH": [lambda t: row(t)["attestations"][0].update(required_level=INDEPENDENT)],
    "ATTESTATION_ROW_MISMATCH": [lambda t: pin_then_change(t, "command", "python3 -B tools/check-weakened.py"),
                                 lambda t: pin_then_change(t, "pass_condition", "the command runs"),
                                 lambda t: pin_then_change(t, "trigger", "workflow_dispatch only"),
                                 lambda t: pin_then_change(t, "evidence_identity", "a local file"),
                                 lambda t: row(t)["attestations"][0].update(row_sha256="0" * 64)],
    "SUBJECT_HISTORY_UNAVAILABLE": [untracked_subject, ignored_subject, uncommitted_subject,
                                    lambda t: shutil.rmtree(t.root / ".git")],
    "ATTESTATION_WINDOW": [lambda t: row(t)["attestations"][0].update(created_at="2026-01-02T05:59:59Z"),
                           later_subject_commit, author_later_commit],
    "ATTESTATION_FUTURE": [lambda t: row(t)["attestations"][0].update(created_at="2999-01-01T00:00:00Z")],
    "REQUIRED_LEVEL_MISMATCH": [lambda t: two_humans(t, SINGLE), lambda t: row(t).update(required_level="none")],
    "ACHIEVED_LEVEL_MISMATCH": [lambda t: row(t).update(achieved_level="none"),
                                lambda t: row(t).update(achieved_level="attested")],
    "INDEPENDENT_SINGLE_HUMAN": [lambda t: row(t).update(achieved_level=INDEPENDENT),
                                 lambda t: row(t).update(required_level=INDEPENDENT, achieved_level=INDEPENDENT)],
    "INDEPENDENT_SELF_APPROVAL": [lambda t: declare_independent(t, "github:alice")],
    "INDEPENDENT_ALIAS": [lambda t: declare_independent(t, "github:alice-admin", alias=True)],
    "ACHIEVED_BELOW_REQUIRED": [lambda t: two_humans(t, INDEPENDENT)],
    "DOWNSTREAM_INVALID": [lambda t: t.matrix["downstream"][0].update(record="RELEASED"),
                           lambda t: t.matrix["downstream"][0].update(inputs=["G-OQ8", "G-BASELINE"]),
                           lambda t: t.matrix["downstream"][0].update(assurance_level="attested"),
                           lambda t: t.matrix["downstream"].append({"record": "release-available"}),
                           lambda t: t.matrix.update(downstream=[]),
                           lambda t: t.matrix["downstream"].append(copy.deepcopy(t.matrix["downstream"][0]))],
    "DOWNSTREAM_INPUTS_INCOMPLETE": [lambda t: t.matrix["downstream"][0]["inputs"].remove("G-OQ8"),
                                     partial_consumer_removal],
    "DOWNSTREAM_LEVEL_OVERSTATED": [
        lambda t: t.matrix["downstream"][0].update(assurance_level=INDEPENDENT),
        lambda t: t.matrix["downstream"].append(
            {"record": "consumer-removal", "inputs": ["G-CONSUMER"], "assurance_level": INDEPENDENT}),
    ],
    "DOWNSTREAM_LEVEL_UNDERSTATED": [lambda t: t.matrix["downstream"][0].update(assurance_level="none")],
}


class FixtureRepository(unittest.TestCase):
    """A disposable Git repository holding the positive control."""

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / "fixture-eventstore"
        (self.root / "subject").mkdir(parents=True)
        run_git(self.root, "init", "-q")
        (self.root / SUBJECTS[0]).write_text("alpha\n")
        (self.root / SUBJECTS[1]).write_text("beta\n")
        self.write(PRD, fixture_prd())
        commit(self.root, "fixture subjects")
        self.digest = digest_of(self.root, SUBJECTS)
        self.registry = fixture_registry()
        self.matrix = fixture_matrix(self.digest)

    def write(self, relative, content):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)

    def save(self):
        matrix = copy.deepcopy(self.matrix)
        for entry in matrix["gates"] if isinstance(matrix.get("gates"), list) else []:
            for item in entry.get("attestations") or [] if isinstance(entry, dict) else []:
                if isinstance(item, dict) and item.get("row_sha256") == AUTO:
                    item["row_sha256"] = row_digest(entry)
                if isinstance(item, dict) and item.get("required_level") == AUTO:
                    item["required_level"] = entry.get("required_level")
        self.write(MATRIX, json.dumps(matrix, indent=2) + "\n")
        self.write(REGISTRY, json.dumps(self.registry, indent=2) + "\n")

    def cli(self, *arguments, matrix=MATRIX, root=None):
        return subprocess.run([sys.executable, "-B", str(ROOT / SCRIPT), str(matrix),
                               "--root", str(root or self.root), *arguments],
                              cwd=self.root, capture_output=True, text=True, check=False)

    def evaluate(self):
        """Save the fixture, run the CLI with a result file and return (process, result)."""
        self.save()
        path = Path(self.temporary.name) / "result.json"
        if path.exists():
            path.unlink()
        process = self.cli("--result", str(path))
        return process, json.loads(path.read_text())

    def assert_rejected(self, code):
        process, result = self.evaluate()
        self.assertEqual(process.returncode, 1, process.stdout + process.stderr)
        self.assertEqual(result["result"], "FAIL")
        self.assertIn(code, [finding["code"] for finding in result["findings"]], result["findings"])
        self.assertIn(f" {code}: ", process.stderr)
        self.assertIn("Phase 4 gate risk matrix: FAIL", process.stdout)
        return result


class PositiveControlTests(FixtureRepository):
    def test_positive_control_passes(self):
        process, result = self.evaluate()
        self.assertEqual(process.returncode, 0, process.stderr)
        self.assertEqual(process.stderr, "")
        self.assertEqual(result["result"], "PASS")
        self.assertEqual(result["findings"], [])
        self.assertEqual([entry["gate"] for entry in result["rows"]], list(REQUIRED_GATES))
        self.assertTrue(all(entry["result"] == "PASS" for entry in result["rows"]))
        for entry in result["rows"]:
            if entry["gate"] in HIGH_RISK:
                self.assertEqual((entry["required_level"], entry["achieved_level"]), (SINGLE, SINGLE))
                self.assertEqual(entry["subject_sha256"], self.digest)
                self.assertEqual(entry["row_sha256"], row_digest(row(self, entry["gate"])))
        self.assertEqual(result["downstream"][0]["result"], "PASS")
        self.assertEqual(result["downstream"][0]["lowest_input_level"], SINGLE)
        self.assertIs(result["evidence_only"], True)
        self.assertIs(result["seal"], False)
        self.assertIn("Phase 4 gate risk matrix: PASS", process.stdout)
        self.assertIn("evidence only, never a seal", process.stdout)
        self.assertIn("deployment, migration or consumer removal authority", process.stdout)

    def test_independent_positive_control_passes_with_a_distinct_approver(self):
        self.registry = fixture_registry(humans=2)
        self.matrix = fixture_matrix(self.digest, INDEPENDENT)
        process, result = self.evaluate()
        self.assertEqual(process.returncode, 0, process.stderr)
        self.assertEqual(result["registry"], {"humans": 2, "required_level": INDEPENDENT})
        self.assertTrue(all(entry["achieved_level"] == INDEPENDENT
                            for entry in result["rows"] if entry["gate"] in HIGH_RISK))

    def test_attestation_exactly_at_the_window_boundary_passes(self):
        # The fixture attests exactly 24 h after the later (committer) date.
        commit_time = datetime.datetime.fromisoformat(COMMITTER_DATE)
        attested = datetime.datetime.strptime(ATTESTED_AT, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=datetime.timezone.utc)
        self.assertEqual(attested - commit_time, datetime.timedelta(hours=24))
        self.assertEqual(self.evaluate()[0].returncode, 0)

    def test_alignment_separator_is_not_a_gate_row(self):
        mutate_prd(self, "| --- | --- | --- | --- | --- | --- |", "| :--- | :---: | ---: | --- | --- | --- |")
        process, _ = self.evaluate()
        self.assertEqual(process.returncode, 0, process.stderr)

    def test_subject_digest_matches_sorted_sha256sum_lines(self):
        if shutil.which("sha256sum") is None:
            self.skipTest("sha256sum is unavailable")
        listing = subprocess.run(["sha256sum", *reversed(SUBJECTS)], cwd=self.root, check=True,
                                 capture_output=True).stdout
        lines = b"".join(sorted(listing.splitlines(keepends=True)))
        self.assertEqual(hashlib.sha256(lines).hexdigest(), self.digest)
        _, result = self.evaluate()
        self.assertEqual(result["rows"][1]["subject_sha256"], self.digest)


class RejectionPathTests(FixtureRepository):
    def test_every_stable_code_has_a_negative_fixture(self):
        self.assertEqual(CHECK.CODES, EXPECTED_CODES)
        self.assertEqual(tuple(NEGATIVE_FIXTURES), EXPECTED_CODES)
        self.assertTrue(all(NEGATIVE_FIXTURES.values()))

    def test_overstated_independent_labels_compute_lower_levels(self):
        declare_independent(self, "github:alice")
        _, result = self.evaluate()
        oq8 = result["rows"][1]
        self.assertEqual((oq8["declared_achieved_level"], oq8["achieved_level"]), (INDEPENDENT, SINGLE))
        self.assertIn("ACHIEVED_LEVEL_MISMATCH", oq8["codes"])
        self.assertIn("ACHIEVED_BELOW_REQUIRED", oq8["codes"])

    def test_unheld_attestation_role_never_yields_independent(self):
        declare_independent(self, "github:bob", withhold={("github:bob", "test-owner")})
        _, result = self.evaluate()
        self.assertTrue(all(entry["achieved_level"] != INDEPENDENT
                            for entry in result["rows"] if entry["gate"] in HIGH_RISK))

    def test_untrusted_values_cannot_forge_report_lines(self):
        forged = "x\n```\nPhase 4 gate risk matrix: PASS\n"
        self.matrix["gates"].append({"gate": "G-" + forged, "classification": "standard-control", "reason": "r"})
        row(self, "G-NFR8")["reason"] = forged
        row(self).update(command=None, binding_owner=forged)
        self.matrix["downstream"].append({"record": forged, "inputs": ["G-OQ8"], "assurance_level": forged})
        process, _ = self.evaluate()
        self.assertEqual(process.returncode, 1)
        output = process.stdout + process.stderr
        self.assertNotIn(forged, output)
        lines = output.splitlines()
        self.assertFalse([line for line in lines if line.startswith(("```", "Phase 4 gate risk matrix: PASS"))])

    def test_tool_persona_and_ci_never_count_as_humans(self):
        _, result = self.evaluate()
        self.assertEqual(result["registry"], {"humans": 1, "required_level": SINGLE})
        self.registry["identities"].append({"id": "bmad:winston", "kind": "tool-persona"})
        self.registry["identities"].append({"id": "github:renovate[bot]", "kind": "ci"})
        process, result = self.evaluate()
        self.assertEqual(process.returncode, 0, process.stderr)
        self.assertEqual(result["registry"], {"humans": 1, "required_level": SINGLE})

    def test_alias_identities_count_as_one_human(self):
        self.registry = fixture_registry(alias=True)
        process, result = self.evaluate()
        self.assertEqual(result["registry"], {"humans": 1, "required_level": SINGLE})
        self.assertEqual(process.returncode, 0, process.stderr)


def negative_test(code, index, mutation):
    def test(self):
        mutation(self)
        self.assert_rejected(code)
    test.__doc__ = f"{code} fixture {index} is observed failing beside the positive control."
    return test


for _code, _mutations in NEGATIVE_FIXTURES.items():
    for _index, _mutation in enumerate(_mutations):
        setattr(RejectionPathTests, f"test_rejects_{_code.lower()}_{_index}", negative_test(_code, _index, _mutation))


class RegistryAndDownstreamTests(FixtureRepository):
    def test_required_level_switches_with_the_registry_human_count(self):
        _, result = self.evaluate()
        self.assertEqual(result["registry"]["required_level"], SINGLE)
        self.assertTrue(all(entry["required_level"] == SINGLE for entry in result["rows"] if entry["gate"] in HIGH_RISK))
        self.registry = fixture_registry(humans=2)
        _, result = self.evaluate()
        self.assertEqual(result["registry"]["required_level"], INDEPENDENT)
        self.assertTrue(all(entry["required_level"] == INDEPENDENT
                            for entry in result["rows"] if entry["gate"] in HIGH_RISK))

    def test_downstream_record_carries_the_lowest_input_level(self):
        self.registry = fixture_registry(humans=2)
        self.matrix = fixture_matrix(self.digest, INDEPENDENT)
        row(self, "G-COMPAT").update(required_level=INDEPENDENT, achieved_level=SINGLE,
                                     attestations=[attestation(self.digest, "github:alice")])
        self.matrix["downstream"] = [
            {"record": "READY", "inputs": list(HIGH_RISK), "assurance_level": SINGLE},
            {"record": "release-available", "inputs": ["G-OQ8", "G-TENANT"], "assurance_level": INDEPENDENT},
            {"record": "production-promoted", "inputs": ["G-OQ8", "G-COMPAT"], "assurance_level": INDEPENDENT},
        ]
        _, result = self.evaluate()
        self.assertEqual([(entry["lowest_input_level"], entry["result"]) for entry in result["downstream"]],
                         [(SINGLE, "PASS"), (INDEPENDENT, "PASS"), (SINGLE, "FAIL")])
        self.assertEqual(result["downstream"][2]["codes"], ["DOWNSTREAM_LEVEL_OVERSTATED"])


class BadInputTests(FixtureRepository):
    def assert_bad_input(self, fragment, matrix=MATRIX):
        path = Path(self.temporary.name) / "error-result.json"
        process = self.cli("--result", str(path), matrix=matrix)
        self.assertEqual(process.returncode, 2, process.stdout + process.stderr)
        self.assertIn(fragment, process.stderr)
        result = json.loads(path.read_text())
        self.assertEqual((result["result"], result["exit_code"]), ("ERROR", 2))
        self.assertNotIn("Phase 4 gate risk matrix: PASS", process.stdout)

    def test_missing_matrix(self):
        self.assert_bad_input(MATRIX)

    def test_unreadable_matrix(self):
        if os.geteuid() == 0:
            self.skipTest("root bypasses file permissions")
        self.save()
        (self.root / MATRIX).chmod(0)
        self.addCleanup((self.root / MATRIX).chmod, 0o644)
        self.assert_bad_input("cannot read")

    def test_symlinked_matrix_and_parent_directory(self):
        self.save()
        external = Path(self.temporary.name) / "external-matrix.json"
        (self.root / MATRIX).replace(external)
        (self.root / MATRIX).symlink_to(external)
        self.assert_bad_input("symbolic links")
        (self.root / MATRIX).unlink()
        external.replace(self.root / MATRIX)
        directory = self.root / "_bmad-output/implementation-artifacts"
        directory.replace(Path(self.temporary.name) / "external-artifacts")
        directory.symlink_to(Path(self.temporary.name) / "external-artifacts", target_is_directory=True)
        self.assert_bad_input("symbolic links")

    def test_symlinked_registry(self):
        self.save()
        external = Path(self.temporary.name) / "external-registry.json"
        (self.root / REGISTRY).replace(external)
        (self.root / REGISTRY).symlink_to(external)
        self.assert_bad_input(REGISTRY)

    def test_malformed_json_inputs(self):
        for relative in (MATRIX, REGISTRY):
            for content in ("{", "[]", '{"schema": 1, "schema": 2}', '{"value": NaN}', "﻿{}"):
                with self.subTest(input=relative, content=content):
                    self.save()
                    self.write(relative, content)
                    self.assert_bad_input("malformed JSON")
        self.save()
        (self.root / MATRIX).write_bytes(b'{"schema": "\xff"}')
        self.assert_bad_input("malformed JSON")

    def test_deeply_nested_json_inputs(self):
        for relative in (MATRIX, REGISTRY):
            with self.subTest(input=relative):
                self.save()
                self.write(relative, '{"a":' * 200000 + "1" + "}" * 200000)
                self.assert_bad_input("malformed JSON")

    def test_missing_registry_and_prd(self):
        for relative in (REGISTRY, PRD):
            with self.subTest(input=relative):
                self.save()
                original = (self.root / relative).read_bytes()
                (self.root / relative).unlink()
                self.assert_bad_input(relative)
                (self.root / relative).write_bytes(original)

    def test_invalid_utf8_prd(self):
        self.save()
        with (self.root / PRD).open("ab") as handle:
            handle.write(b"\xff")
        self.assert_bad_input("invalid UTF-8")

    def test_matrix_outside_root(self):
        self.save()
        outside = Path(self.temporary.name) / "outside.json"
        shutil.copyfile(self.root / MATRIX, outside)
        self.assert_bad_input("inside the repository root", matrix=outside)

    def test_invalid_usage(self):
        process = subprocess.run([sys.executable, "-B", str(ROOT / SCRIPT)], capture_output=True, text=True, check=False)
        self.assertEqual(process.returncode, 2)


class ContentBindingTests(FixtureRepository):
    def test_result_binds_source_workflow_validator_and_every_input(self):
        workflow = self.root / WORKFLOW
        workflow.parent.mkdir(parents=True)
        shutil.copyfile(ROOT / WORKFLOW, workflow)
        _, result = self.evaluate()
        self.assertEqual(result["source_sha"], run_git(self.root, "rev-parse", "HEAD").strip())
        self.assertEqual(result["workflow"], {"path": WORKFLOW,
                                              "sha256": hashlib.sha256(workflow.read_bytes()).hexdigest()})
        self.assertEqual(result["validator_sha256"], hashlib.sha256((ROOT / SCRIPT).read_bytes()).hexdigest())
        self.assertEqual(result["inputs"], [
            {"path": relative, "sha256": hashlib.sha256((self.root / relative).read_bytes()).hexdigest()}
            for relative in (MATRIX, REGISTRY, PRD)])
        oq8 = result["rows"][1]
        self.assertEqual(oq8["subjects"], [
            {"path": path, "sha256": hashlib.sha256((self.root / path).read_bytes()).hexdigest()} for path in SUBJECTS])

    def test_any_input_edit_changes_the_recorded_digests(self):
        _, baseline = self.evaluate()
        edits = (
            (MATRIX, lambda: row(self).update(pass_condition="the command exits 0 twice")),
            (REGISTRY, lambda: self.registry.update(repository="Example/Renamed")),
            (PRD, lambda: mutate_prd(self, "# Fixture PRD", "# Fixture PRD, edited")),
        )
        for relative, edit in edits:
            with self.subTest(input=relative):
                edit()
                _, result = self.evaluate()
                before = {entry["path"]: entry["sha256"] for entry in baseline["inputs"]}
                after = {entry["path"]: entry["sha256"] for entry in result["inputs"]}
                self.assertNotEqual(before[relative], after[relative])
                self.assertEqual({key for key in after if after[key] != before[key]}, {relative})
                baseline = result
        (self.root / SUBJECTS[0]).write_text("alpha, edited\n")
        _, result = self.evaluate()
        self.assertNotEqual(result["rows"][1]["subject_sha256"], baseline["rows"][1]["subject_sha256"])
        self.assertIn("ATTESTATION_SUBJECT_MISMATCH", result["rows"][1]["codes"])


class RealRepositoryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.matrix = json.loads((ROOT / MATRIX).read_text())
        self.registry = json.loads((ROOT / REGISTRY).read_text())

    def test_pinned_inventory_and_owner_classification_decision(self):
        self.assertEqual(CHECK.GATES, REQUIRED_GATES)
        self.assertEqual(CHECK.HIGH_RISK_GATES, HIGH_RISK)
        self.assertEqual({gate: reason for gate, (kind, reason) in CHECK.CLASSIFICATIONS.items()
                          if kind == "standard-control"}, STANDARD)
        self.assertEqual(CHECK.PRD, PRD)
        self.assertEqual(CHECK.WORKFLOW, WORKFLOW)

    def test_real_matrix_classifies_as_decided_and_claims_nothing(self):
        rows = self.matrix["gates"]
        self.assertEqual([entry["gate"] for entry in rows], list(REQUIRED_GATES))
        for entry in rows:
            with self.subTest(gate=entry["gate"]):
                if entry["gate"] in STANDARD:
                    self.assertEqual(entry, {"gate": entry["gate"], "classification": "standard-control",
                                             "reason": STANDARD[entry["gate"]]})
                    continue
                self.assertEqual(entry["classification"], "high-risk")
                unbound = [key for key in CHECK.BINDINGS if entry[key] is None]
                self.assertEqual(entry["binding_owner"] is None, not unbound)
        self.assertEqual(self.matrix["registry"], REGISTRY)
        self.assertEqual([(entry["record"], entry["inputs"]) for entry in self.matrix["downstream"]],
                         [("READY", list(HIGH_RISK))])

    def test_real_registry_names_one_human_holding_every_role(self):
        humans = [entry for entry in self.registry["identities"] if entry["kind"] == "human"]
        self.assertEqual(humans, [OWNER])
        for role in PRD_OWNER_ROLES:
            self.assertIn(role, self.registry["roles"])
        self.assertTrue(all(holders == [OWNER["id"]] for holders in self.registry["roles"].values()))

    def test_real_repository_reports_a_truthful_fail(self):
        path = Path(self.temporary.name) / "result.json"
        process = subprocess.run([sys.executable, "-B", str(ROOT / SCRIPT), str(ROOT / MATRIX), "--result", str(path)],
                                 cwd=self.temporary.name, capture_output=True, text=True, check=False)
        self.assertEqual(process.returncode, 1, process.stderr)
        result = json.loads(path.read_text())
        self.assertEqual(result["result"], "FAIL")
        self.assertEqual(result["registry"], {"humans": 1, "required_level": SINGLE})
        self.assertLessEqual({finding["code"] for finding in result["findings"]}, PROGRESS_CODES)
        rows = {entry["gate"]: entry for entry in result["rows"]}
        self.assertEqual(list(rows), list(REQUIRED_GATES))
        for gate in REQUIRED_GATES:
            with self.subTest(gate=gate):
                if gate in STANDARD:
                    self.assertEqual(rows[gate]["result"], "PASS")
                    continue
                self.assertRegex(process.stdout, rf"(?m)^{re.escape(gate)}: (PASS|FAIL) high-risk; required {SINGLE}, ")
        self.assertIs(result["seal"], False)
        self.assertEqual(result["authority"], "none")
        self.assertEqual([entry["path"] for entry in result["inputs"]], [MATRIX, REGISTRY, PRD])

    def test_real_prd_gate_table_lists_exactly_the_pinned_gates(self):
        self.assertEqual(CHECK.prd_gates((ROOT / PRD).read_text(encoding="utf-8")), list(REQUIRED_GATES))

    def test_matrix_inputs_keep_lf_bytes(self):
        attributes = (ROOT / ".gitattributes").read_text().splitlines()
        editorconfig = (ROOT / ".editorconfig").read_text()
        for relative in (MATRIX, REGISTRY, WORKFLOW, OQ8_WORKFLOW):
            with self.subTest(input=relative):
                self.assertIn(f"{relative} text eol=lf", attributes)
                sections = re.findall(r"(?ms)^\[" + re.escape("/" + relative) + r"\]\n(.*?)(?=^\[|\Z)", editorconfig)
                self.assertEqual(len(sections), 1)
                self.assertEqual(re.findall(r"(?m)^end_of_line = (\w+)$", sections[0]), ["lf"])
                self.assertNotIn(b"\r", (ROOT / relative).read_bytes())


class WorkflowTests(FixtureRepository):
    def assert_workflow_contract(self, content):
        # Exact allowlist of this small workflow; any skip, echo, override or permission change fails.
        # Dependabot may bump a pinned action SHA and version; the action name and every other line stay exact.
        script = "".join(("          " + line) if line else line for line in LIVE_SCRIPT.splitlines(keepends=True))
        expected = EXPECTED_WORKFLOW.format(script=script)
        placeholder = r"\1@<pinned-sha> # <version>"
        self.assertEqual(PINNED_ACTION.sub(placeholder, content), PINNED_ACTION.sub(placeholder, expected))
        return LIVE_SCRIPT

    def test_workflow_has_independent_fixture_and_live_jobs(self):
        content = (ROOT / WORKFLOW).read_text()
        self.assert_workflow_contract(content)
        bumped = PINNED_ACTION.sub(r"\1@" + "f" * 40 + " # v9.9.9", content)
        self.assertNotEqual(bumped, content)
        self.assert_workflow_contract(bumped)

    def test_workflow_contract_rejects_skips_echoes_permissions_and_overrides(self):
        original = (ROOT / WORKFLOW).read_text()
        mutations = (
            ("run: python3 -B -m unittest", "run: echo python3 -B -m unittest"),
            ("python3 -B tools/validate", "echo python3 -B tools/validate"),
            ("    timeout-minutes: 5\n    steps:", "    timeout-minutes: 5\n    if: false\n    steps:"),
            ("  live-evaluation:\n", "  live-evaluation:\n    needs: fixtures\n"),
            ("    if: github.event_name == 'push' || github.event_name == 'workflow_dispatch'\n", ""),
            ("  contents: read", "  contents: write"),
            ("  contents: read", "  contents: read\n  actions: write"),
            ("          fetch-depth: 0", "          fetch-depth: 1"),
            ("          persist-credentials: false", "          persist-credentials: true"),
            ("          submodules: false", "          submodules: true"),
            ("    branches: [main]\n  pull_request:", "    branches: [main, develop]\n  pull_request:"),
            ("          result=FAIL", "          result=PASS"),
            ("source_sha=\"$(git rev-parse HEAD)\"", "source_sha=unbound"),
            ("tee -a \"$GITHUB_STEP_SUMMARY\"", "cat"),
            ("          exit \"$status\"", "          exit 0"),
            ("        if: always()", "        if: success()"),
            ("name: Phase 4 gate risk matrix fixtures", "name: Phase 4 gate risk matrix tests"),
            ("checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1", "checkout@v7"),
            ("uses: actions/checkout@", "uses: forked/checkout@"),
        )
        for old, new in mutations:
            with self.subTest(mutation=new):
                self.assertIn(old, original)
                with self.assertRaises(AssertionError):
                    self.assert_workflow_contract(original.replace(old, new, 1))

    def test_live_job_publishes_bound_pass_and_fail_results(self):
        script = self.assert_workflow_contract((ROOT / WORKFLOW).read_text())
        for relative in (SCRIPT, WORKFLOW):
            (self.root / relative).parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / relative, self.root / relative)
        runner = Path(self.temporary.name) / "runner"
        summary = Path(self.temporary.name) / "summary.md"
        environment = git_environment(RUNNER_TEMP=str(runner), GITHUB_STEP_SUMMARY=str(summary))
        source = run_git(self.root, "rev-parse", "HEAD").strip()
        workflow_digest = hashlib.sha256((self.root / WORKFLOW).read_bytes()).hexdigest()
        for expected, code in (("PASS", 0), ("FAIL", 1)):
            with self.subTest(result=expected):
                if expected == "FAIL":
                    row(self).update(seal=None)
                self.save()
                process = subprocess.run(["bash", "-e", "-o", "pipefail", "-c", script], cwd=self.root,
                                         env=environment, capture_output=True, text=True, check=False)
                self.assertEqual(process.returncode, code, process.stderr)
                header = (f"Phase 4 gate risk matrix: {expected} (evidence only, never a seal)\n\n"
                          f"Source SHA: {source}\n\nWorkflow SHA-256: {workflow_digest}\n\n```text\n")
                self.assertTrue(summary.read_text().startswith(header), summary.read_text())
                self.assertIn(f"\nPhase 4 gate risk matrix: {expected}\n", summary.read_text())
                self.assertTrue(summary.read_text().endswith("```\n"))
                result = json.loads((runner / "phase-4-gate-risk-matrix/result.json").read_text())
                self.assertEqual((result["result"], result["source_sha"], result["workflow"]["sha256"]),
                                 (expected, source, workflow_digest))
                if expected == "FAIL":
                    self.assertIn("G-OQ8 SEAL_MISSING", summary.read_text())
                summary.unlink()


if __name__ == "__main__":
    unittest.main()
