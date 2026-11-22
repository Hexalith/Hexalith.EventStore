"""Resolve Phase 4 gate evidence on a single approved tree and derive the sealed decision."""

import phase_4_transition as C

VERIFIER = "tools/phase_4_readiness.py"
CONTROL_FILES = (VERIFIER, "tools/phase_4_tenant_gate.py")
GATES = tuple(g for g in C.M.GATES if g not in ("G-HIGH-RISK", "G-READINESS"))
GATE_DECISIONS = ("G-OQ8", "G-TENANT")
IDENTITIES = ("command_sha256", "subject_sha256", "evidence_sha256", "result_sha256", "approvals_sha256")
DECLARATIONS = {"G-HIGH-RISK": "866882467b50a46efc18d0384a190f68325b060a5e1d1903e91366322ae95f61",
                "G-READINESS": "ecee0774b6ac67086fe3e348a170d77a981dff96c87c2aa4fdc4277330bd943d"}
OQ8_WORKFLOW = ".github/workflows/oq8-governing-design.yml"
OQ8_JOBS = {"OQ8 governing-design fixtures": ("Test exact identity and rejection paths",),
            "OQ8 governing-design live evaluation": ("Evaluate and report current authority availability",)}


def hashed(value):
    return C.digest(C.canonical(value))


def problem(gate, code, message):
    error = C.Refusal(code, message)
    error.gate = gate
    return error


def validate(request):
    claim = request.get("readiness")
    if claim is None:
        raise problem("G-BASELINE", "BUSINESS_PREREQUISITE_MISSING", "missing readiness baseline and gate inventory")
    C.closed(claim, ("baseline_tree", "workflow_sha256", "verifier_sha256", "gates"))
    C.require(C.sha(claim["baseline_tree"], 40) and C.sha(claim["workflow_sha256"]) and C.sha(claim["verifier_sha256"]),
              "INPUT_INVALID", "invalid readiness baseline or control identity", 2)
    C.require(request["gate"] is None and request["subject_mode"] == "files"
              and request["nfr_ids"] == [] and request["predecessors"] == [],
              "INPUT_INVALID", "readiness requires its own gate inventory and file subject", 2)
    gates = claim["gates"]
    C.require(isinstance(gates, dict), "INPUT_INVALID", "expected readiness gates keyed by gate ID", 2)
    missing = [g for g in GATES if g not in gates]
    extra = sorted(set(gates) - set(GATES))
    if missing or extra:
        failures = [{"gate": gate, "code": "BUSINESS_PREREQUISITE_MISSING", "message": "mandatory readiness input is absent"}
                    for gate in missing]
        if extra:
            failures.append({"gate": "G-READINESS", "code": "INPUT_INVALID", "message": "unexpected readiness inputs: " + ", ".join(extra)})
        error = C.Refusal(failures[0]["code"], "invalid readiness gate inventory", 1 if missing else 2)
        error.refusals = failures
        raise error
    for gate, entry in gates.items():
        try:
            fields = {*IDENTITIES, "run_id", "attempt"}
            if gate in GATE_DECISIONS:
                fields.add("owner_request")
            if gate == "G-OQ8":
                fields.add("availability_run")
            C.closed(entry, fields)
            has_attempt = C.positive(entry["run_id"]) and C.positive(entry["attempt"])
            artifact_only = gate not in C.M.HIGH_RISK_GATES and entry["run_id"] is None and entry["attempt"] is None
            C.require(all(C.sha(entry[key]) for key in IDENTITIES) and (has_attempt or artifact_only),
                      "INPUT_INVALID", "invalid gate evidence identity or attempt", 2)
            if gate in GATE_DECISIONS:
                owner = entry["owner_request"]
                C.require(isinstance(owner, dict) and owner.get("transition") == "high-risk-gate-pass"
                          and owner.get("gate") == gate, "PREDECESSOR_IDENTITY", "wrong gate owner request")
                C.validate_request(owner)
                if gate == "G-TENANT":
                    C.require("tenant_qualification" in owner, "BUSINESS_PREREQUISITE_MISSING", "missing tenant qualification")
            if gate == "G-OQ8":
                run = entry["availability_run"]
                C.closed(run, ("run_id", "attempt"))
                C.require(all(C.positive(n) for n in run.values()), "INPUT_INVALID", "invalid OQ8 availability run", 2)
        except C.Refusal as error:
            error.gate = gate
            raise


def tree_at(api, commit):
    try:
        doc = api.get("/git/commits/" + commit)
        tree = doc.get("tree")
        C.require(doc.get("sha") == commit and isinstance(tree, dict) and C.sha(tree.get("sha"), 40),
                  "SOURCE_MISMATCH", "missing authenticated source tree")
        return tree["sha"]
    except C.Refusal as error:
        error.gate = "G-BASELINE"
        raise


def check_checkout(control):
    tree = control.request["readiness"]["baseline_tree"]
    if (C.M.source_sha(control.root) != control.head
            or C.M.git(control.root, "rev-parse", "--verify", "HEAD^{tree}") != tree + "\n"
            or C.M.git(control.root, "status", "--porcelain=v1", "--untracked-files=all", "--ignore-submodules=none") != ""):
        raise problem("G-BASELINE", "SOURCE_MISMATCH", "readiness requires a clean checkout of the bound head and tree")


def bind(control, paths, matrix, validator):
    claim = control.request["readiness"]
    if tree_at(control.api, control.head) != claim["baseline_tree"]:
        raise problem("G-BASELINE", "SOURCE_MISMATCH", "baseline tree differs from authenticated head")
    C.require(claim["workflow_sha256"] == paths[C.workflow_path("readiness")] and
              claim["verifier_sha256"] == hashed({p: paths[p] for p in (*C.CONTROL_FILES, *CONTROL_FILES)}),
              "SOURCE_MISMATCH", "readiness workflow or verifier differs")
    for gate, pin in DECLARATIONS.items():
        row = control.rows[gate]
        declaration = {key: row[key] for key in (*C.M.BINDINGS, "author_role", "evaluator_roles", "guarded_transitions")}
        if hashed(declaration) != pin:
            raise problem(gate, "MATRIX_INVALID", "decision differs from the fixed matrix declaration")
    C.require(control.request["author"] == control.rows["G-READINESS"]["author"],
              "AUTHOR_INVALID", "readiness requires its matrix author")
    # Check historical declarations against historical achieved levels. Newly
    # authenticated inputs supply the final minimum after the seal is verified.
    validator.rows = list(control.rows.values())
    validator.transitions = {g: row.get("guarded_transitions", []) for g, row in control.rows.items()}
    records = [validator.check_downstream(i, row) for i, row in enumerate(matrix["downstream"])]
    C.require(not validator.findings and sum(row.get("record") == "READY" for row in records) == 1,
              "MATRIX_INVALID", "invalid offline downstream inventory or assurance declaration")


def current_attempt(control, run_id, attempt):
    run = control.api.get(f"/actions/runs/{run_id}")
    C.require(run.get("id") == run_id and run.get("run_attempt") == attempt and run.get("head_sha") == control.head and
              run.get("status") == "completed" and run.get("conclusion") == "success",
              "RUN_NOT_SUCCESSFUL", "bound evidence attempt is no longer the current successful run")


def owner_evidence(control, gate, claim):
    """Only enrolled owner consumers can supply facts. Every invocation authenticates anew."""
    if gate not in GATE_DECISIONS:
        raise C.Refusal("ADAPTER_UNAVAILABLE", f"{gate} has no fixed owner evidence binding")
    import phase_4_gate_decision as D
    owner = claim["owner_request"]
    C.require(all(owner[key] == control.request[key] for key in ("head_sha", "matrix", "registry", "required_level")),
              "PREDECESSOR_IDENTITY", "gate owner request is for another source or control set")
    current_attempt(control, claim["run_id"], claim["attempt"])
    verifier = C.Control(control.root, owner, control.api)
    decision = D.decide(verifier, claim["run_id"], claim["attempt"], allow_tenant=True)
    current_attempt(control, claim["run_id"], claim["attempt"])
    binding = decision["binding"]
    evidence = {"binding": binding, "predecessors": decision["predecessors"], "seal": decision["seal"]}
    if gate == "G-OQ8":
        import phase_4_tenant_gate as T
        pin = {**claim["availability_run"], "head_sha": control.head, "repository": C.REPOSITORY,
               "workflow_sha256": C.digest(control.api.file(control.head, OQ8_WORKFLOW))}
        jobs = T.authenticate_workflow_jobs(control.api, gate, pin, C.REPOSITORY, OQ8_WORKFLOW, OQ8_JOBS, {})
        T.latest_attempt(control.api, gate, pin)
        evidence["availability"] = {**pin, "jobs": {name: job["id"] for name, job in jobs.items()}}
    control.readiness_owners[gate] = verifier
    return {"gate": decision["gate"], "head_sha": binding["head_sha"], "result": decision["result"],
            "assurance_level": decision["downstream_minimum"], "run_id": decision["seal"]["run_id"],
            "attempt": decision["seal"]["attempt"], "subject_sha256": binding["subject_sha256"],
            "command_sha256": C.digest(control.rows[gate]["command"].encode()),
            "evidence_sha256": hashed(evidence),
            "approvers": [{"role": a["role"], "github_id": a["github_id"]} for a in decision["approvals"]],
            "approvals_sha256": hashed(decision["approvals"]), "result_sha256": hashed(decision)}


def verify_gates(control):
    records = {}
    failures = []
    for gate in GATES:
        claim = control.request["readiness"]["gates"][gate]
        try:
            record = owner_evidence(control, gate, claim)
            C.require(record["gate"] == gate and record["head_sha"] == control.head,
                      "PREDECESSOR_IDENTITY", "verified gate or source differs")
            C.require(record["result"] == "PASS", "PREDECESSOR_FAILED", "owner business validator failed")
            for key in (*IDENTITIES, "run_id", "attempt"):
                C.require(record[key] == claim[key], "PREDECESSOR_IDENTITY", "verified " + key + " differs")
            if gate in C.M.HIGH_RISK_GATES:
                row = control.rows[gate]
                C.require(all(row[key] for key in C.M.BINDINGS) and "readiness" in row["guarded_transitions"]
                          and C.digest(row["command"].encode()) == record["command_sha256"],
                          "GATE_UNBOUND", "matrix has no matching business declaration for readiness")
                C.require(record["assurance_level"] == control.required,
                          "LEVEL_MISMATCH", "gate lacks registry-required assurance")
                C.require({a["role"] for a in record["approvers"]} == set(row["evaluator_roles"]),
                          "APPROVAL_ROLE", "authenticated approvals differ from the gate's evaluator roles")
            records[gate] = record
        except C.Refusal as error:
            failures.append({"gate": gate, "code": error.code, "message": str(error)})
        except (AttributeError, KeyError, TypeError, ValueError, OSError) as error:
            failures.append({"gate": gate, "code": "API_INVALID", "message": "unreadable owner evidence: " + type(error).__name__})
    if failures:
        error = C.Refusal(failures[0]["code"], "mandatory readiness evidence failed")
        error.refusals = failures
        raise error
    control.binding["readiness"] = {"baseline_tree": control.request["readiness"]["baseline_tree"], "gates": records}


def diagnose_candidate(candidate, expected):
    binding = candidate.get("binding")
    previous = binding.get("readiness") if isinstance(binding, dict) else None
    current = expected["binding"]["readiness"]
    if not isinstance(previous, dict) or previous.get("baseline_tree") != current["baseline_tree"]:
        raise problem("G-BASELINE", "RESULT_MISMATCH", "sealed baseline tree differs from current evaluation")
    gates = previous.get("gates")
    for gate, record in current["gates"].items():
        if not isinstance(gates, dict) or hashed(gates.get(gate)) != hashed(record):
            raise problem(gate, "RESULT_MISMATCH", "sealed gate differs from current owner evidence")


def reconfirm_approvals(verifier):
    previous = verifier.approvals
    verifier.approvals = []
    verifier.check_approvals()
    C.require(verifier.approvals == previous, "APPROVAL_INVALID", "authenticated approvals changed during consumption")


def reconfirm(control):
    """Re-read mutable platform facts after all potentially long business validators."""
    import phase_4_tenant_gate as T
    for gate, verifier in control.readiness_owners.items():
        claim = control.request["readiness"]["gates"][gate]
        try:
            current_attempt(control, claim["run_id"], claim["attempt"])
            if gate == "G-OQ8":
                run = claim["availability_run"]
                current_attempt(control, run["run_id"], run["attempt"])
            elif gate == "G-TENANT":
                tenants = C.GitHub(C.TENANTS)
                for lane, pin in verifier.request["tenant_qualification"]["runs"].items():
                    api = control.api if pin["repository"] == C.REPOSITORY else tenants
                    T.latest_attempt(api, lane, pin)
            reconfirm_approvals(verifier)
        except C.Refusal as error:
            error.gate = gate
            raise
    reconfirm_approvals(control)


def decide(control, candidate, seal):
    """Called only after authenticating the completed attempt and reproducing its evaluation."""
    gates = control.binding["readiness"]["gates"]
    source = hashed(control.binding)
    inputs = {gate: hashed(record) for gate, record in gates.items()}
    attempt = hashed({"candidate_sha256": hashed(candidate), "seal": seal})
    high_risk_levels = [gates[gate]["assurance_level"] for gate in GATES if gate in C.M.HIGH_RISK_GATES]
    minimum = min([control.achieved, *high_risk_levels], key=C.M.level_rank)
    high_risk_row = control.rows["G-HIGH-RISK"]
    author_id = control.registry["identities"][high_risk_row["author"]]["github_id"]
    approvers = [*control.approvals, *(a for record in gates.values() for a in record["approvers"])]
    eligible_roles = set()
    for a in approvers:
        role, github_id = a["role"], a["github_id"]
        holders = control.registry["roles"].get(role, [])
        if not C.positive(github_id) or not any(control.registry["identities"][h]["github_id"] == github_id for h in holders):
            raise problem("G-HIGH-RISK", "APPROVAL_IDENTITY", "approver is not a registered numeric role holder")
        if control.required != "independent" or github_id != author_id:
            eligible_roles.add(role)
    if control.required == "independent" and not eligible_roles.issuperset(high_risk_row["evaluator_roles"]):
        raise problem("G-HIGH-RISK", "INDEPENDENT_REQUIRED", "each aggregate evaluator role needs an approver distinct from the aggregate author")
    if not eligible_roles.issuperset(high_risk_row["evaluator_roles"]):
        raise problem("G-HIGH-RISK", "APPROVAL_ROLE", "assurance records omit a required evaluator role")

    def passing(name, predecessors):
        return {"record": name, "result": "PASS", "source_sha256": source, "assurance_level": minimum,
                "inputs": dict(predecessors)}

    high_risk = passing("G-HIGH-RISK", {**inputs, "readiness-attempt": attempt})
    inputs["G-HIGH-RISK"] = hashed(high_risk)
    readiness = passing("G-READINESS", inputs)
    inputs["G-READINESS"] = hashed(readiness)
    ready = passing("READY", inputs)
    return {"disposition": "READY", "records": [high_risk, readiness, ready], "refusals": []}


def reject(error):
    refusals = getattr(error, "refusals", None) or [
        {"gate": getattr(error, "gate", "G-READINESS"), "code": error.code, "message": str(error)}]
    return {"disposition": "Reject", "records": [], "refusals": refusals}
