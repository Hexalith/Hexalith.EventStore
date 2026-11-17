"""Closed publication lifecycle construction and fixed, fail-closed authority adapter.

No record issuer, executable plug-in, success override or publication operation exists.
Structural success only checks declarations; authenticated dedicated consumption is separate.
"""

import argparse
import copy
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess

import profile_bridge_build as B

SCHEMA = "hexalith.eventstore.publication-authority.v1"
RESULT_SCHEMA = "hexalith.eventstore.publication-authority-result.v1"
STATES = ("built", "evidence-candidate-published", "evidence-validated", "release-available", "production-promoted")
ROLES = ("eventstore-owner", "release-owner", "test-owner", "release-owner", "deployment-owner")
LEVELS = ("none", "single-maintainer-attested", "independent")
PROFILE = "deploy/dapr/production-profile.yaml"
INVENTORY = "deploy/dapr/production-composing-inventory.json"
RECORDS = "deploy/dapr/publication-authority.json"
REGISTRY = "_bmad-output/implementation-artifacts/owner-role-registry.json"
RELEASE_WORKFLOW = ".github/workflows/release.yml"
CONTROL_FILES = ("tools/publication_authority.py", "tools/validate-publication-authority.py",
                 "tools/publication-authority.schema.json", *B.CONTROLS,
                 "docs/architecture/publication-authority-v1.md", "docs/architecture/production-profile-v1.md",
                 "docs/architecture/production-profile-v2.md",
                 RELEASE_WORKFLOW, "global.json", "nuget.config", ".gitmodules",
                 "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
                 "src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj")
INVALIDATION = ["subject", "source", "workflow", "inputs", "profile", "deployment", "registry", "revocation"]
MAX_BYTES = 1024 * 1024
MAX_PROFILE = 4 * MAX_BYTES
MAX_DEPTH = 32
BUILD_INPUT_PROOF = "PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE"
FIXED_MISSING = (
    "G_RUNTIME_PARITY_VERIFIER_UNAVAILABLE", "STORY_3_21_QUALIFICATION_VERIFIER_UNAVAILABLE",
    "STORY_5_7_COMPONENTS_VERIFIER_UNAVAILABLE", "STORY_5_13_ACTIVATION_VERIFIER_UNAVAILABLE",
    "STORY_7_22_RESTORE_VERIFIER_UNAVAILABLE", "AD26_RATIFICATION_VERIFIER_UNAVAILABLE",
    "APPEND_PROTECTION_VERIFIER_UNAVAILABLE", "WORKLOAD_ISSUER_APPROVAL_VERIFIER_UNAVAILABLE",
    "CORRECTIVE_AUTHORIZATION_VERIFIER_UNAVAILABLE", "SEALED_PREDECESSOR_VERIFIER_UNAVAILABLE")
RECORD_KEYS = {"schema", "version", "id", "state", "subject", "candidate", "validator", "predecessor_sha256",
               "issuer", "approval", "registry", "outcome", "validity", "assurance", "promotion", "publisher"}


class Refusal(Exception):
    """Safe stable refusal; malformed inputs use exit 2, unproven authority exit 1."""
    def __init__(self, code, exit_code=1):
        super().__init__(code)
        self.code, self.exit_code = code, exit_code


def require(condition, code="SCHEMA_INVALID", exit_code=2):
    if not condition:
        raise Refusal(code, exit_code)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True, allow_nan=False).encode("ascii")


def digest(content):
    return hashlib.sha256(content).hexdigest()


def record_digest(record):
    return digest(canonical({"purpose": "publication-authority-record", "schemaVersion": 1, "record": record}))


def claim_digest(record):
    claim = copy.deepcopy(record)
    claim["approval"]["comment_id"] = None
    claim["approval"]["body_sha256"] = None
    claim["issuer"]["authentication_evidence_sha256"] = None
    return digest(canonical({"purpose": "publication-authority-approval", "schemaVersion": 1, "record": claim}))


def closed(value, keys):
    require(isinstance(value, dict) and set(value) == set(keys))


def sha(value, length=64):
    require(isinstance(value, str) and re.fullmatch("[0-9a-f]{" + str(length) + "}", value) is not None)


def text(value):
    require(isinstance(value, str) and 0 < len(value) <= 512 and all(ord(c) >= 32 and ord(c) != 127 for c in value))


def moment(value):
    require(isinstance(value, str) and re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", value) is not None)
    try:
        return dt.datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)
    except ValueError as error:
        raise Refusal("SCHEMA_INVALID", 2) from error


def safe_path(value):
    text(value)
    require(re.fullmatch(r"[A-Za-z0-9_.@/-]+", value) is not None and not value.startswith("/")
            and all(p not in ("", ".", "..") for p in value.split("/")))


def binding(value):
    closed(value, ("path", "sha256"))
    safe_path(value["path"])
    sha(value["sha256"])


def parse(content):
    require(0 < len(content) <= MAX_PROFILE, "INPUT_LIMIT", 2)
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result)
            result[key] = value
        return result
    def invalid(_):
        raise Refusal("SCHEMA_INVALID", 2)
    try:
        value = json.loads(content.decode("utf-8"), object_pairs_hook=unique, parse_constant=invalid)
    except (ValueError, UnicodeError, RecursionError) as error:
        raise Refusal("SCHEMA_INVALID", 2) from error
    def depth(node, level=0):
        require(level <= MAX_DEPTH, "INPUT_LIMIT", 2)
        if isinstance(node, dict):
            for item in node.values(): depth(item, level + 1)
        if isinstance(node, list):
            for item in node: depth(item, level + 1)
    depth(value)
    return value


def no_symbolic_ancestors(path):
    candidate = path.absolute()
    require(all(not part.is_symlink() for part in (candidate, *candidate.parents)), "INPUT_INVALID", 2)


def read(root, path, limit=MAX_BYTES):
    no_symbolic_ancestors(root)
    safe_path(path)
    candidate = root
    try:
        for part in path.split("/"):
            candidate = candidate / part
            require(not candidate.is_symlink(), "INPUT_INVALID", 2)
        require(stat.S_ISREG(candidate.stat().st_mode), "INPUT_INVALID", 2)
        with candidate.open("rb") as source:
            require(stat.S_ISREG(os.fstat(source.fileno()).st_mode), "INPUT_INVALID", 2)
            content = source.read(limit + 1)
    except OSError as error:
        raise Refusal("INPUT_MISSING") from error
    require(len(content) <= limit, "INPUT_LIMIT", 2)
    return content


def validate_record(record):
    closed(record, RECORD_KEYS)
    require(record["schema"] == SCHEMA and type(record["version"]) is int and record["version"] == 1)
    text(record["id"])
    require(record["state"] in STATES, "STATE_UNKNOWN", 1)
    subject = record["subject"]
    closed(subject, ("kind", "owner", "source_repository", "source_sha", "subject_sha256", "packages", "oci_index_sha256",
                     "server_package_sha256", "extensions_sha256", "catalog_sha256", "runtime_sha256"))
    require(subject["kind"] in ("eventstore-package", "platform-composed-host"))
    owner = "Hexalith/Hexalith.EventStore" if subject["kind"] == "eventstore-package" else "Hexalith/Hexalith.Platform"
    require(subject["owner"] == owner and subject["source_repository"] == owner, "SUBJECT_OWNER_MISMATCH", 1)
    sha(subject["source_sha"], 40)
    for key in ("subject_sha256", "oci_index_sha256", "server_package_sha256", "catalog_sha256", "runtime_sha256"):
        sha(subject[key])
    require(isinstance(subject["extensions_sha256"], list) and len(subject["extensions_sha256"]) <= 100)
    for value in subject["extensions_sha256"]: sha(value)
    require(len(set(subject["extensions_sha256"])) == len(subject["extensions_sha256"]))
    packages = subject["packages"]
    require(isinstance(packages, list) and 1 <= len(packages) <= 100)
    for package in packages:
        closed(package, ("id", "version", "sha256"))
        text(package["id"]); text(package["version"]); sha(package["sha256"])
    require(len({p["id"] for p in packages}) == len(packages))
    closed(record["candidate"], ("id", "sha256"))
    text(record["candidate"]["id"]); sha(record["candidate"]["sha256"])
    validator = record["validator"]
    closed(validator, ("repository", "commit", "path", "sha256", "result_sha256", "receipt_set_sha256"))
    text(validator["repository"]); sha(validator["commit"], 40); safe_path(validator["path"])
    for key in ("sha256", "result_sha256", "receipt_set_sha256"): sha(validator[key])
    if record["predecessor_sha256"] is not None: sha(record["predecessor_sha256"])
    issuer = record["issuer"]
    closed(issuer, ("id", "role", "github_id", "authentication_evidence_sha256"))
    text(issuer["id"]); text(issuer["role"])
    require(type(issuer["github_id"]) is int and issuer["github_id"] > 0)
    sha(issuer["authentication_evidence_sha256"])
    expected_role = ROLES[STATES.index(record["state"])]
    if subject["kind"] == "platform-composed-host" and record["state"] == "built": expected_role = "deployment-owner"
    require(issuer["role"] == expected_role, "ISSUER_ROLE_MISMATCH", 1)
    closed(record["approval"], ("comment_id", "body_sha256"))
    require(type(record["approval"]["comment_id"]) is int and record["approval"]["comment_id"] > 0)
    sha(record["approval"]["body_sha256"])
    require(issuer["authentication_evidence_sha256"] == record["approval"]["body_sha256"], "AUTHENTICATION_MISMATCH", 1)
    binding(record["registry"])
    require(record["registry"]["path"] == REGISTRY)
    require(record["outcome"] == record["state"], "OUTCOME_MISMATCH", 1)
    validity = record["validity"]
    closed(validity, ("issued_at", "expires_at", "revoked", "invalidated", "invalidation_rules"))
    require(moment(validity["issued_at"]) < moment(validity["expires_at"]))
    require(type(validity["revoked"]) is bool and type(validity["invalidated"]) is bool)
    require(validity["invalidation_rules"] == INVALIDATION)
    assurance = record["assurance"]
    closed(assurance, ("required", "achieved", "inputs"))
    require(assurance["required"] in LEVELS[1:] and assurance["achieved"] in LEVELS)
    require(isinstance(assurance["inputs"], list) and 1 <= len(assurance["inputs"]) <= 100
            and all(level in LEVELS for level in assurance["inputs"]))
    lowest = min([assurance["achieved"], *assurance["inputs"]], key=LEVELS.index)
    require(assurance["achieved"] == lowest and LEVELS.index(lowest) >= LEVELS.index(assurance["required"]), "ASSURANCE_MISMATCH", 1)
    publisher = record["publisher"]
    closed(publisher, ("repository", "commit", "gitlink_commit"))
    require(publisher["repository"] == "Hexalith/Hexalith.Builds")
    sha(publisher["commit"], 40); sha(publisher["gitlink_commit"], 40)
    promotion = record["promotion"]
    if record["state"] != "production-promoted":
        require(promotion is None)
    else:
        closed(promotion, ("environment", "profile", "inventory", "rendered_deployment", "deployment_sha256"))
        text(promotion["environment"])
        closed(promotion["profile"], ("path", "resolved_sha256", "infrastructure_sha256"))
        require(promotion["profile"]["path"] == PROFILE, "PROFILE_SLOT_MISMATCH", 1)
        sha(promotion["profile"]["resolved_sha256"]); sha(promotion["profile"]["infrastructure_sha256"])
        binding(promotion["inventory"]); binding(promotion["rendered_deployment"]); sha(promotion["deployment_sha256"])
        require(promotion["inventory"]["path"] == INVENTORY, "INVENTORY_SLOT_MISMATCH", 1)
        expected = digest(canonical({"purpose": "publication-rendered-deployment", "schemaVersion": 1,
                                    "oci_index_sha256": subject["oci_index_sha256"],
                                    "profile_sha256": promotion["profile"]["resolved_sha256"],
                                    "rendered_sha256": promotion["rendered_deployment"]["sha256"]}))
        require(promotion["deployment_sha256"] == expected, "DEPLOYMENT_MISMATCH", 1)


def validate_chain(document, now=None):
    closed(document, ("schema", "records"))
    require(document["schema"] == SCHEMA)
    records = document["records"]
    require(isinstance(records, list) and 1 <= len(records) <= len(STATES), "CHAIN_INVALID", 1)
    now = now or dt.datetime.now(dt.timezone.utc)
    for index, record in enumerate(records):
        validate_record(record)
        require(record["state"] == STATES[index], "CHAIN_ORDER", 1)
        require(record["predecessor_sha256"] == (record_digest(records[index - 1]) if index else None), "CHAIN_PREDECESSOR", 1)
        require(record["subject"] == records[0]["subject"] and record["candidate"] == records[0]["candidate"], "CHAIN_SUBJECT", 1)
        require(record["registry"] == records[0]["registry"] and record["publisher"] == records[0]["publisher"], "CHAIN_INPUTS", 1)
        require(record["id"] not in {r["id"] for r in records[:index]}, "CHAIN_DUPLICATE", 1)
        require(not index or moment(record["validity"]["issued_at"]) >= moment(records[index - 1]["validity"]["issued_at"]), "CHAIN_TIME", 1)
        validity = record["validity"]
        require(moment(validity["issued_at"]) <= now < moment(validity["expires_at"]), "RECORD_EXPIRED_OR_FUTURE", 1)
        require(not validity["revoked"], "RECORD_REVOKED", 1)
        require(not validity["invalidated"], "RECORD_INVALIDATED", 1)
        require(not index or LEVELS.index(record["assurance"]["achieved"]) <= LEVELS.index(records[index - 1]["assurance"]["achieved"]), "ASSURANCE_MISMATCH", 1)
    return {"structural": "PASS", "authority": "unevaluated", "state": records[-1]["state"],
            "record_sha256": record_digest(records[-1])}


def git(root, *args):
    try:
        output = subprocess.run(["git", "-C", str(root), *args], capture_output=True, timeout=10, check=True)
        require(len(output.stdout) <= MAX_BYTES, "INPUT_LIMIT", 2)
        return output.stdout.decode("utf-8").strip()
    except (OSError, subprocess.SubprocessError, UnicodeError) as error:
        raise Refusal("TRUSTED_IDENTITY_UNAVAILABLE") from error


def publisher_identity(root, head):
    sha(head, 40)
    require(git(root, "rev-parse", "HEAD") == head, "SOURCE_MISMATCH", 1)
    tree = git(root, "ls-tree", head, "--", "references/Hexalith.Builds")
    match = re.fullmatch(r"160000 commit ([0-9a-f]{40})\treferences/Hexalith.Builds", tree)
    require(match is not None, "BUILDS_GITLINK_MISSING", 1)
    workflow = git(root, "show", head + ":" + RELEASE_WORKFLOW)
    pins = re.findall(r"repository: Hexalith/Hexalith.Builds\s+ref: ([0-9a-f]{40})\s+path: \.hexalith/builds-execution", workflow)
    require(len(pins) == 1, "PUBLISHER_IDENTITY_UNAVAILABLE", 1)
    return {"repository": "Hexalith/Hexalith.Builds", "commit": pins[0], "gitlink_commit": match[1]}


def missing_prerequisites(root, head, records=(), build_proof=None):
    codes = []
    try:
        identity = publisher_identity(root, head)
        if records and any(r["publisher"] != identity for r in records): codes.append("BUILDS_RECORD_IDENTITY_MISMATCH")
    except Refusal as error:
        identity = None
        codes.append(error.code)
    # There is no approved owner codec API to invoke today. A file named like a
    # codec or ReleaseRecordCodec is never evidence of availability or verification.
    codes.append("RELEASE_EVIDENCE_CODEC_UNAVAILABLE")
    if not B.proved(build_proof):
        codes.append(BUILD_INPUT_PROOF)
    codes.extend(FIXED_MISSING)
    if any(r["subject"]["kind"] == "platform-composed-host" for r in records):
        codes += ["PLATFORM_ISSUER_APPROVAL_VERIFIER_UNAVAILABLE", "EXTENSION_API_APPROVAL_VERIFIER_UNAVAILABLE"]
    return identity, codes



def lifecycle_subject_at(control, commit, allow_absent=False, record_id=None):
    """Authored claims define the window; raw receipt attachments remain seal inputs."""
    import phase_4_transition as T
    files = []
    for item in control.request["subjects"]:
        content = control.api.file(commit, item["path"], allow_absent)
        if content is None:
            return None
        if item["path"] == RECORDS:
            document = parse(content)
            closed(document, ("schema", "records"))
            require(document["schema"] == SCHEMA and isinstance(document["records"], list)
                    and 1 <= len(document["records"]) <= 5)
            records = document["records"]
            if record_id is not None:
                records = [record for record in records if record.get("id") == record_id]
                if not records:
                    return None
                require(len(records) == 1)
            claims = [{"id": record["id"], "claim_sha256": claim_digest(record)} for record in records]
            require(len({claim["id"] for claim in claims}) == len(claims))
            content = canonical({"purpose": "publication-authority-window", "schemaVersion": 1, "claims": claims})
        files.append((item["path"], digest(content)))
    return T.M.subject_digest(files)


def record_changed_push(control, record):
    """Later lifecycle records never rewrite an earlier record's issuance window."""
    import phase_4_transition as T
    scoped = copy.copy(control)
    scoped.subject_at = lambda commit, allow_absent=False: lifecycle_subject_at(control, commit, allow_absent, record["id"])
    scoped.subject = scoped.subject_at(control.head)
    return T.Control.changed_push(scoped)


def authenticate_records(control, records):
    import phase_4_transition as T
    seen = set()
    for record in records:
        changed = record_changed_push(control, record)
        issuer, reference = record["issuer"], record["approval"]
        require(reference["comment_id"] not in seen, "AUTHENTICATION_REUSED", 1)
        seen.add(reference["comment_id"])
        require(record["registry"]["sha256"] == digest(control.local(REGISTRY)), "REGISTRY_MISMATCH", 1)
        holder = control.registry["identities"].get(issuer["id"], {})
        require(holder.get("kind") == "human" and holder.get("github_id") == issuer["github_id"]
                and issuer["id"] in control.registry["roles"].get(issuer["role"], []), "ISSUER_UNREGISTERED", 1)
        comment = control.api.get("/issues/comments/" + str(reference["comment_id"]))
        require(comment.get("id") == reference["comment_id"] and isinstance(comment.get("body"), str)
                and digest(comment["body"].encode()) == reference["body_sha256"], "AUTHENTICATION_CHANGED", 1)
        user = comment.get("user", {})
        require(user.get("type") == "User" and user.get("id") == issuer["github_id"]
                and comment.get("performed_via_github_app") is None, "ISSUER_UNREGISTERED", 1)
        require(re.fullmatch(re.escape(control.api.prefix) + r"/issues/[1-9][0-9]*", comment.get("issue_url", "")) is not None,
                "AUTHENTICATION_INVALID", 1)
        require(comment.get("created_at") == comment.get("updated_at"), "AUTHENTICATION_EDITED", 1)
        created = T.timestamp(comment.get("created_at"))
        require(changed + dt.timedelta(seconds=T.M.WINDOW_SECONDS) <= created <= dt.datetime.now(dt.timezone.utc), "AUTHENTICATION_WINDOW", 1)
        require(moment(record["validity"]["issued_at"]) <= created < moment(record["validity"]["expires_at"]), "AUTHENTICATION_WINDOW", 1)
        expected = {"schema": "hexalith.eventstore.publication-authority-approval.v1", "record_claim_sha256": claim_digest(record),
                    "role": issuer["role"], "subject_sha256": record["subject"]["subject_sha256"], "decision": "approved"}
        require(parse(comment["body"].encode()) == expected, "AUTHENTICATION_INVALID", 1)
        require(record["assurance"]["required"] == control.required and record["assurance"]["achieved"] == control.achieved
                and all(LEVELS.index(level) >= LEVELS.index(control.required) for level in record["assurance"]["inputs"]),
                "ASSURANCE_MISMATCH", 1)
        require(control.required != "independent" or issuer["github_id"] != control.author_id, "ASSURANCE_MISMATCH", 1)


def transition_inputs(control):
    """Require every fixed lifecycle and owner-profile implementation byte bound at exact head."""
    required = set(CONTROL_FILES) | {RECORDS, PROFILE, INVENTORY}
    required |= set(B.SOURCES)
    actual = {item["path"] for item in control.binding["inputs"]}
    require(required <= actual, "PUBLICATION_INPUT_UNBOUND", 1)
    inventory = parse(control.local(INVENTORY))
    closed(inventory, ("schema", "source", "expectedApplicationIds", "expectedComponentIds"))
    require(inventory["schema"] in ("hexalith.eventstore.production-composing-inventory.v1",
                                    "hexalith.eventstore.production-composing-inventory.v2"))
    binding(inventory["source"])
    require(inventory["source"]["path"] not in (PROFILE, INVENTORY), "SELF_DERIVED_INVENTORY", 1)
    required.add(inventory["source"]["path"])
    require(required <= actual, "PUBLICATION_INPUT_UNBOUND", 1)
    require(control.request["subject_mode"] == "files" and required <= {item["path"] for item in control.request["subjects"]},
            "PUBLICATION_SUBJECT_UNBOUND", 1)
    try:
        B.source_paths(control.root)
    except B.Refusal as error:
        raise Refusal(error.code) from error
    records = parse(control.local(RECORDS))
    validate_chain(records)
    promotion = records["records"][-1]["promotion"]
    if promotion:
        require(promotion["rendered_deployment"]["path"] in actual
                and promotion["rendered_deployment"]["path"] in {item["path"] for item in control.request["subjects"]},
                "PUBLICATION_INPUT_UNBOUND", 1)
    return records


def evaluate_transition(control):
    import phase_4_transition as T
    try:
        document = transition_inputs(control)
        records = document["records"]
        require(records[-1]["state"] == control.request["transition"], "STATE_MISMATCH", 1)
        authenticate_records(control, records)
        proofs = []
        profile_check(control.root, records[-1], authenticated=control, proofs=proofs)
        require(len(proofs) == 1 and B.proved(proofs[0]), BUILD_INPUT_PROOF, 1)
        control.current_head()
        _, missing = missing_prerequisites(control.root, control.head, records, build_proof=proofs[0])
        # Every unrelated fixed authority prerequisite remains mandatory.
        require(not missing, missing[0] if missing else BUILD_INPUT_PROOF, 1)
    except Refusal as error:
        raise T.Refusal("BUSINESS_PREREQUISITE_MISSING", error.code, error.exit_code) from error


def profile_check(root, record, *, authenticated=None, proofs=None):
    require((root / PROFILE).exists(), "PROFILE_MISSING", 1)
    require((root / INVENTORY).exists(), "COMPOSING_INVENTORY_MISSING", 1)
    inventory_bytes = read(root, INVENTORY)
    inventory = parse(inventory_bytes)
    closed(inventory, ("schema", "source", "expectedApplicationIds", "expectedComponentIds"))
    require(inventory["schema"] in ("hexalith.eventstore.production-composing-inventory.v1",
                                    "hexalith.eventstore.production-composing-inventory.v2"))
    binding(inventory["source"])
    require(inventory["source"]["path"] not in (PROFILE, INVENTORY), "SELF_DERIVED_INVENTORY", 1)
    composition = read(root, inventory["source"]["path"])
    require(digest(composition) == inventory["source"]["sha256"], "COMPOSING_SOURCE_MISMATCH", 1)
    for key in ("expectedApplicationIds", "expectedComponentIds"):
        require(isinstance(inventory[key], list) and 1 <= len(inventory[key]) <= 1000)
        for value in inventory[key]: text(value)
        require(len(set(inventory[key])) == len(inventory[key]))
    content = read(root, PROFILE, MAX_PROFILE)
    promotion = record.get("promotion") if record else None
    resolved = promotion["profile"]["resolved_sha256"] if promotion else digest(content)
    require(digest(content) == resolved, "PROFILE_RESOLVED_DIGEST_MISMATCH", 1)
    data = {PROFILE: content, INVENTORY: inventory_bytes, inventory["source"]["path"]: composition}
    if promotion:
        require(digest(inventory_bytes) == promotion["inventory"]["sha256"], "COMPOSING_INVENTORY_MISMATCH", 1)
        rendered = read(root, promotion["rendered_deployment"]["path"])
        require(digest(rendered) == promotion["rendered_deployment"]["sha256"], "DEPLOYMENT_MISMATCH", 1)
        data[promotion["rendered_deployment"]["path"]] = rendered
    try:
        captured = B.capture(root, data)
        if authenticated is not None:
            # Recheck bytes captured after authentication, before even SDK
            # preflight. Neither caller hashes nor a previous success suffice.
            authenticated.current_head()
            bindings = {item["path"]: item.get("sha256") for item in authenticated.binding["inputs"]}
            subjects = {item["path"]: item.get("sha256") for item in authenticated.request["subjects"]}
            captured_files = dict(captured.files)
            for path in set(CONTROL_FILES) | set(captured_files) | {RECORDS}:
                bound_content = captured_files.get(path)
                if bound_content is None:
                    bound_content = B.read(root, path)
                require(digest(bound_content) == bindings.get(path) == subjects.get(path)
                        == digest(authenticated.local(path))
                        == digest(authenticated.api.file(authenticated.head, path)), BUILD_INPUT_PROOF, 1)
        output = B.execute(captured, digest(inventory_bytes), resolved,
                           promotion["profile"]["infrastructure_sha256"] if promotion else "-")
    except B.Refusal as error:
        raise Refusal(BUILD_INPUT_PROOF if authenticated is not None else error.code) from error
    profile = parse(content)
    validate_owner_result(output, resolved, profile.get("infrastructureDigest"),
                          promotion["profile"]["infrastructure_sha256"] if promotion else None)
    if proofs is not None:
        require(B.proved(output, captured=captured), BUILD_INPUT_PROOF, 1)
        proofs.append(output)
    if promotion:
        # These associations are interpreted only after the genuine owner result
        # attests canonical decoding and both digest domains.
        release, subject = profile["release"], record["subject"]
        packages = {(artifact["id"], artifact["version"], artifact["digest"]) for artifact in release["artifacts"]
                    if artifact["kind"] == "package" or artifact["kind"].endswith("-package")}
        declared = {(package["id"], package["version"], package["sha256"]) for package in subject["packages"]}
        require(packages == declared and subject["server_package_sha256"] in {item[2] for item in packages}
                and set(subject["extensions_sha256"]) == set(release["extensionDigests"])
                and subject["catalog_sha256"] == release["catalogContentDigest"]
                and subject["runtime_sha256"] == release["runtimeImageDigest"], "PROFILE_SUBJECT_MISMATCH", 1)
        require(profile["environment"]["id"] == promotion["environment"]
                and profile["environment"]["renderedDeploymentDigest"] == promotion["rendered_deployment"]["sha256"]
                and profile["release"]["composedHostDigest"] == record["subject"]["oci_index_sha256"], "PROFILE_SUBJECT_MISMATCH", 1)
    return inventory["source"]["path"]



def validate_owner_result(output, resolved, infrastructure, expected_infrastructure=None):
    """The final owner JSON may follow ordinary SDK build diagnostics."""
    try:
        lines = output.splitlines()
        require(bool(lines), "PROFILE_OWNER_RESULT_INVALID", 1)
        result = parse(lines[-1])
        closed(result, ("isStructurallyValid", "infrastructureDigest", "resolvedDigest", "issues", "authorization", "qualification"))
        sha(result["infrastructureDigest"])
        sha(result["resolvedDigest"])
        require(result["isStructurallyValid"] is True and result["issues"] == []
                and result["authorization"] == result["qualification"] == "unevaluated"
                and result["resolvedDigest"] == resolved and result["infrastructureDigest"] == infrastructure
                and (expected_infrastructure is None or result["infrastructureDigest"] == expected_infrastructure),
                "PROFILE_OWNER_RESULT_INVALID", 1)
    except (Refusal, ValueError, TypeError, KeyError, AttributeError) as error:
        raise Refusal("PROFILE_OWNER_RESULT_INVALID") from error


def live(root):
    codes = []
    status = 1
    records = []
    proofs = []
    for path, code in ((RECORDS, "AUTHORITY_RECORDS_MISSING"), (PROFILE, "PROFILE_MISSING"), (INVENTORY, "COMPOSING_INVENTORY_MISSING")):
        try:
            content = read(root, path, MAX_PROFILE if path == PROFILE else MAX_BYTES)
            if path == RECORDS:
                document = parse(content)
                validate_chain(document)
                records = document["records"]
        except Refusal as error:
            codes.append(code if error.code == "INPUT_MISSING" else error.code)
            status = max(status, error.exit_code)
    if (root / PROFILE).exists() and (root / INVENTORY).exists():
        try: profile_check(root, records[-1] if records else None, proofs=proofs)
        except Refusal as error:
            codes.append(error.code)
            status = max(status, error.exit_code)
    try: head = git(root, "rev-parse", "HEAD")
    except Refusal: head = "0" * 40
    identity, missing = missing_prerequisites(root, head, records, build_proof=proofs[0] if len(proofs) == 1 else None)
    codes.extend(missing)
    codes.append("AUTHENTICATED_DEDICATED_CONSUMPTION_REQUIRED")
    return {"schema": RESULT_SCHEMA, "result": "FAIL", "authority": "unproven", "head_sha": head,
            "publisher": identity, "codes": list(dict.fromkeys(codes)), "seal": None}, status


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("structural", "evaluate"))
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--records", type=Path)
    parser.add_argument("--result", type=Path)
    args = parser.parse_args(argv)
    try:
        if args.mode == "structural":
            require(args.records is not None, "INPUT_INVALID", 2)
            document = parse(read(args.records.parent, args.records.name))
            result = {"schema": RESULT_SCHEMA, **validate_chain(document)}
            code = 0
        else:
            require(args.records is None, "INPUT_INVALID", 2)
            no_symbolic_ancestors(args.root)
            result, code = live(args.root.resolve())
    except (Refusal, OSError, ValueError, TypeError, KeyError, AttributeError, RecursionError) as error:
        refusal = error if isinstance(error, Refusal) else Refusal("INPUT_INVALID", 2)
        result, code = {"schema": RESULT_SCHEMA, "result": "FAIL", "authority": "unproven", "codes": [refusal.code], "seal": None}, refusal.exit_code
    content = json.dumps(result, sort_keys=True, indent=2) + "\n"
    if args.result:
        try:
            raw_destination = args.result.absolute()
            no_symbolic_ancestors(raw_destination)
            destination = raw_destination.resolve()
            root = args.root.resolve()
            require(not destination.is_relative_to(root) and not destination.is_relative_to(Path(__file__).resolve().parents[1]),
                    "OUTPUT_OVERLAP", 2)
            require(args.records is None or destination != args.records.resolve(), "OUTPUT_OVERLAP", 2)
            require(all(not parent.is_symlink() for parent in [destination, *destination.parents]), "OUTPUT_INVALID", 2)
            with destination.open("x", encoding="utf-8") as output:
                output.write(content)
        except (OSError, Refusal):
            print('{"result":"FAIL","codes":["OUTPUT_INVALID"],"seal":null}')
            return 2
    print(content, end="")
    return code
