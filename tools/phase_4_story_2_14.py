"""Consume the authenticated G-TENANT decision for Story 2.14's NFR2 slice."""

import phase_4_gate_decision as D
import phase_4_tenant_gate as T
import phase_4_transition as C

PATH = "tools/phase_4_story_2_14.py"
ADAPTER = "story-2-14-canonical-tenant-boundary-v1"
SCHEMA = "hexalith.eventstore.story-2-14-evidence.v1"
SPEC = "_bmad-output/implementation-artifacts/spec-2-14-canonical-tenant-boundary.md"
STORY_KEY = "2-14-canonical-tenant-boundary-in-generated-and-tenants-hosts"
GATE = "G-TENANT"
CONTROL_FILES = (PATH, T.PATH, C.workflow_path("high-risk-gate-pass"))


def supported(claim):
    return (claim["story_key"] == STORY_KEY and claim["story_id"] == "2.14"
            and claim["spec_path"] == SPEC and claim["target"] in ("spec", "tracker")
            and claim["nfr_ids"] == claim["high_risk_nfr_ids"] == ["NFR2"])


def validate_request(request):
    claim = request["tenant_gate"]
    C.require(supported(request["story"]), "ADAPTER_UNAVAILABLE",
              "G-TENANT completion evidence is specific to the canonical Story 2.14 NFR2 slice")
    C.require(not request["predecessors"], "INPUT_INVALID",
              "Story 2.14 uses the G-TENANT decision as its fixed prerequisite", 2)
    C.closed(claim, ("owner_request", "run_id", "attempt", "tenants_head"))
    C.require(C.positive(claim["run_id"]) and C.positive(claim["attempt"]), "SEAL_MISSING",
              "Story 2.14 requires a completed G-TENANT run and attempt")
    owner = claim["owner_request"]
    C.require(isinstance(owner, dict) and owner.get("transition") == "high-risk-gate-pass"
              and owner.get("gate") == GATE and owner.get("subject_mode") == "files",
              "SUBJECT_MISMATCH", "Story 2.14 requires a G-TENANT file subject")
    C.require(all(key in owner and key in request and owner[key] == request[key]
                  for key in ("head_sha", "matrix", "registry", "required_level")),
              "SUBJECT_MISMATCH", "G-TENANT decision must bind the same EventStore subject and controls")
    C.validate_request(owner)
    C.require("tenant_qualification" in owner, "BUSINESS_PREREQUISITE_MISSING",
              "G-TENANT qualification must bind both repositories")
    qualification = owner["tenant_qualification"]
    C.require(C.sha(claim["tenants_head"], 40) and claim["tenants_head"] == qualification["tenants_head"],
              "SUBJECT_MISMATCH", "G-TENANT decision must bind the same Tenants subject")


def evaluate(control):
    request = control.request
    C.require("tenant_gate" in request, "SEAL_MISSING",
              "Story 2.14 completion requires fresh authenticated G-TENANT decision consumption")
    claim = request["tenant_gate"]
    owner = claim["owner_request"]
    verifier = C.Control(control.root, owner, control.api)
    verifier.current_attempt(claim["run_id"], claim["attempt"])
    decision = D.decide(verifier, claim["run_id"], claim["attempt"], allow_tenant=True)
    binding = decision["binding"]
    C.require(binding["head_sha"] == control.head
              and binding["tenant_qualification"] == owner["tenant_qualification"],
              "SUBJECT_MISMATCH", "authenticated G-TENANT decision differs from the approved subjects")
    seal = decision["seal"]
    C.require(seal and seal["run_id"] == claim["run_id"] and seal["attempt"] == claim["attempt"],
              "SEAL_MISSING", "G-TENANT decision lacks its authenticated seal")
    C.require({approval["role"] for approval in decision["approvals"]} == set(control.rows[GATE]["evaluator_roles"]),
              "APPROVAL_MISSING", "G-TENANT decision lacks the required human attestations")
    C.require(decision["result"] == "PASS", "PREDECESSOR_FAILED", "G-TENANT decision did not pass")
    inventory = {item["path"]: item["sha256"] for item in (*binding["inputs"], *control.binding["inputs"])}
    assert_stable(control.root, inventory)
    reconfirm(verifier, seal)
    summary = {"adapter": ADAPTER, "eventstore_head": control.head, "tenants_head": claim["tenants_head"],
               "gate_subject_sha256": binding["subject_sha256"], "decision_sha256": C.digest(C.canonical(decision))}
    level = decision["downstream_minimum"]
    control.binding["story_adapter"] = summary
    control.story_evidence = {"schema": SCHEMA, "adapter": ADAPTER, "result": "PASS", "binding": summary,
                              "gate": GATE, "seal": seal, "assurance_level": level}
    control.predecessors.append({"identity_sha256": C.digest(C.canonical(claim)),
                                 "output_sha256": summary["decision_sha256"], "assurance_level": level})
    control.story_inventory = inventory
    control.tenant_story_verifier = verifier


def reconfirm(verifier, seal):
    """Refresh mutable gate and qualification facts after reproducing the decision."""
    verifier.reconfirm_candidate(seal["run_id"], seal["attempt"], verifier.result("evaluate", []), seal["artifact_id"])
    verifier.reconfirm_approvals()
    tenants = C.GitHub(C.TENANTS)
    for lane, pin in verifier.request["tenant_qualification"]["runs"].items():
        T.latest_attempt(verifier.api if pin["repository"] == C.REPOSITORY else tenants, lane, pin)
    verifier.current_attempt(seal["run_id"], seal["attempt"])


def assert_stable(root, inventory):
    reader = C.Control.__new__(C.Control)
    reader.root = root
    for path, digest in inventory.items():
        C.require(C.digest(reader.local(path)) == digest, "SOURCE_MISMATCH",
                  "Story 2.14 input changed during verification: " + path)
