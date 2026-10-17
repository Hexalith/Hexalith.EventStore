#!/usr/bin/env python3
"""Check bounded corrective authority; a PASS never closes the underlying gate.

The versioned canonicalization excludes the two derived envelope fields from
all schema-recognized, correctly located authorizations. All other bytes remain
bound, including divergent HEAD/index shadows. Raw states enforce scope even
when Git's diff filters hide changes. Completion requires a read-only owner
commitment; no command posts receipts or grants external authority.
"""

import sys

if __name__ == "__main__" and (not sys.flags.isolated or not sys.flags.no_site):
    _platform = sys.modules["nt" if sys.platform == "win32" else "posix"]
    _platform.execv(sys.executable, [sys.executable, "-I", "-S", "-B", *(["-" + "O" * sys.flags.optimize] if sys.flags.optimize else []), __file__, *sys.argv[1:]])

import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import urllib.error
import urllib.request


REPOSITORY = "Hexalith/Hexalith.EventStore"
DIRECTORY = "_bmad-output/implementation-artifacts/evidence/corrective-work-authorizations"
REGISTRY = DIRECTORY + "/owner-role-registry.json"
SCRIPT = "tools/validate-corrective-work-authorization.py"
CONTRACT = "tools/corrective-work-authorization.schema.json"
PRD = "_bmad-output/planning-artifacts/prd.md"
RULE = "hexalith.corrective-subject.v1"
ACTIONS = ["readiness", "release", "deployment", "production", "migration", "removal"]
MAX_BYTES = 4 * 1024 * 1024


class Rejection(ValueError):
    """A machine-readable refusal, with a stable reason code."""

    def __init__(self, code, detail, **metadata):
        super().__init__(detail)
        self.code = code
        self.metadata = metadata


def require(condition, code, detail):
    if not condition:
        raise Rejection(code, detail)


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "json", "duplicate JSON member: " + key)
        result[key] = value
    return result


def no_number(value):
    raise Rejection("json", "only integer JSON numbers are permitted: " + value)


def load(data):
    require(len(data) <= MAX_BYTES, "json", "JSON exceeds the size limit")
    try:
        return json.loads(data.decode("utf-8"), object_pairs_hook=unique, parse_float=no_number, parse_constant=no_number)
    except (UnicodeError, json.JSONDecodeError, RecursionError) as error:
        raise Rejection("json", "invalid UTF-8 JSON") from error


def shape(value, schema, definitions, location="record"):
    """Enforce the small, closed JSON Schema vocabulary shipped with this tool."""
    if "$ref" in schema:
        return shape(value, definitions[schema["$ref"].split("/")[-1]], definitions, location)
    if "oneOf" in schema:
        matches = 0
        for choice in schema["oneOf"]:
            try:
                shape(value, choice, definitions, location)
                matches += 1
            except Rejection:
                pass
        require(matches == 1, "schema", location + " must match exactly one permitted type")
        return
    types = {"object": dict, "array": list, "string": str, "integer": int, "boolean": bool, "null": type(None)}
    require(type(value) is types[schema["type"]], "schema", location + " has the wrong type")
    if "const" in schema:
        require(type(value) is type(schema["const"]) and value == schema["const"], "schema", location + " has an invalid literal")
    if "enum" in schema:
        require(value in schema["enum"], "schema", location + " has an unknown value")
    if type(value) is dict:
        properties = schema["properties"]
        require(set(value) == set(schema["required"]) and not set(value) - set(properties), "schema", location + " has missing or unknown members")
        for key in value:
            shape(value[key], properties[key], definitions, location + "." + key)
    elif type(value) is list:
        require(schema.get("minItems", 0) <= len(value) <= schema.get("maxItems", 256), "schema", location + " has an invalid length")
        if schema.get("uniqueItems"):
            require(len({canonical(item) for item in value}) == len(value), "schema", location + " contains duplicates")
        for index, item in enumerate(value):
            shape(item, schema["items"], definitions, f"{location}[{index}]")
    elif type(value) is str:
        require(schema.get("minLength", 0) <= len(value) <= schema.get("maxLength", 4096), "schema", location + " has an invalid length")
        if "pattern" in schema:
            require(re.fullmatch(schema["pattern"], value) is not None, "schema", location + " has an invalid format")
    elif type(value) is int:
        require(schema.get("minimum", 0) <= value <= schema.get("maximum", 2**63 - 1), "schema", location + " is outside its range")


def relative(name):
    require(type(name) is str and name and "\\" not in name and not any(ord(c) < 32 for c in name), "path", "unsafe repository path")
    path = PurePosixPath(name)
    require(not path.is_absolute() and str(path) == name and all(p not in (".", "..", ".git") for p in path.parts), "path", "noncanonical repository path: " + name)
    require(not any(c in name for c in "*?[]{}!"), "scope", "globs and broad scope expressions are forbidden")
    return path


def regular(root, name, missing=False):
    path = root
    parts = relative(name).parts
    for index, part in enumerate(parts):
        path = path / part
        try:
            info = path.lstat()
        except FileNotFoundError:
            require(missing, "missing-input", "missing repository file: " + name)
            return None
        require(not stat.S_ISLNK(info.st_mode), "filesystem", "symlink in repository input: " + name)
        require(stat.S_ISDIR(info.st_mode) if index < len(parts) - 1 else stat.S_ISREG(info.st_mode), "filesystem", "nonregular repository input: " + name)
    require(info.st_nlink == 1, "filesystem", "hard-linked repository input: " + name)
    with path.open("rb") as stream:
        data = stream.read()
    return data


def run_git(root, *arguments, check=True):
    result = subprocess.run(["git", "-c", "core.fsmonitor=false", "-c", "submodule.recurse=false", *arguments], cwd=root,
                            env={**os.environ, "GIT_NO_REPLACE_OBJECTS": "1", "GIT_OPTIONAL_LOCKS": "0"},
                            capture_output=True, timeout=60, check=False)
    require(not check or result.returncode == 0, "git", "git " + " ".join(arguments[:3]) + " failed: " + result.stderr.decode("utf-8", "replace")[:400])
    return result


def tree(root, revision):
    entries = {}
    for row in run_git(root, "ls-tree", "-rz", revision).stdout.split(b"\0"):
        if row:
            metadata, raw_name = row.split(b"\t", 1)
            mode, _kind, oid = metadata.decode().split()
            name = raw_name.decode("utf-8")
            relative(name)
            entries[name] = (mode, oid)
    return entries


def projected_record(document):
    return {key: value for key, value in document.items() if key not in ("authorization_sha256", "postflight")}


def blob_identity(data, name, definitions):
    # Normalize every recognized authorization, in every layer. Another record's
    # envelope must not recursively enter this record's derived output subject.
    if name.startswith(DIRECTORY + "/") and name.endswith(".json"):
        try:
            document = load(data)
            shape(document, definitions["authorization"], definitions)
            expected = f"{DIRECTORY}/{document['failed_gate']['id']}/{document['authorization_id']}.json"
            if name == expected:
                data = canonical(projected_record(document))
        except Rejection:
            pass  # Unrelated or malformed bytes are still bound without exclusions.
    return digest(data)


def check_gitlink_directory(root, name, expected_head):
    """Inspect immediate names and superproject Git metadata, never source files."""
    path = root
    for part in relative(name).parts:
        path = path / part
        try:
            info = path.lstat()
        except FileNotFoundError:
            return  # An uninitialized checkout may be absent or empty.
        require(stat.S_ISDIR(info.st_mode), "filesystem", "unsafe Gitlink entry: " + name)
    entries = list(path.iterdir())
    if not entries:
        return
    marker = path / ".git"
    require(marker in entries and stat.S_ISREG(marker.lstat().st_mode) and marker.lstat().st_nlink == 1,
            "gitlink", "nonempty uninitialized Gitlink: " + name)
    with marker.open("rb") as stream:
        raw = stream.read(4097)
    require(len(raw) <= 4096 and raw.startswith(b"gitdir: ") and b"\0" not in raw, "gitlink", "invalid Gitlink metadata: " + name)
    gitdir = (path / raw[8:].decode("utf-8").strip()).resolve()
    configured = run_git(root, "config", "--file", ".gitmodules", "--get-regexp", r"^submodule\..*\.path$", check=False)
    names = []
    for row in configured.stdout.decode("utf-8").splitlines():
        key, value = row.split(None, 1)
        if value == name:
            names.append(key[len("submodule."):-len(".path")])
    require(configured.returncode == 0 and len(names) == 1, "gitlink", "Gitlink lacks superproject registration: " + name)
    expected = run_git(root, "rev-parse", "--git-path", "modules/" + names[0]).stdout.decode().strip()
    require(gitdir == (root / expected).resolve() and gitdir.is_dir(), "gitlink", "Gitlink metadata is not owned by superproject: " + name)
    actual = run_git(root, "--git-dir", str(gitdir), "rev-parse", "--verify", "HEAD").stdout.decode().strip()
    require(actual == expected_head, "gitlink", "initialized Gitlink HEAD differs: " + name)
    return True


def snapshot(root, baseline, definitions):
    """Derive scope from raw states; Git's change filters are not authority."""
    head = run_git(root, "rev-parse", "HEAD").stdout.decode().strip()
    before, committed = tree(root, baseline), tree(root, "HEAD")
    indexed = {}
    for row in run_git(root, "ls-files", "--stage", "-z").stdout.split(b"\0"):
        if row:
            metadata, raw_name = row.split(b"\t", 1)
            mode, oid, stage = metadata.decode().split()
            name = raw_name.decode("utf-8")
            relative(name)
            require(stage == "0" and name not in indexed, "index", "unmerged repository index")
            indexed[name] = (mode, oid)
    index_flags = {row[2:].decode("utf-8"): chr(row[0]) for row in run_git(root, "ls-files", "-v", "-z").stdout.split(b"\0") if row}
    untracked = {name.decode("utf-8") for name in run_git(root, "ls-files", "--others", "--exclude-standard", "-z").stdout.split(b"\0") if name}
    inventory, index_shadows, committed_shadows, mutations = [], [], [], {}
    blob_cache = {}

    def identity(name, entry):
        if entry is None:
            return None
        if name.startswith(DIRECTORY + "/") and name.endswith(".json"):
            if entry[1] not in blob_cache:
                blob_cache[entry[1]] = run_git(root, "cat-file", "blob", entry[1]).stdout
            return (entry[0], blob_identity(blob_cache[entry[1]], name, definitions))
        return entry

    def shadow(name, entry):
        if entry is None:
            return {"path": name, "mode": None, "sha256": None}
        data = run_git(root, "cat-file", "blob", entry[1]).stdout
        return {"path": name, "mode": entry[0], "sha256": blob_identity(data, name, definitions)}

    for name in sorted(set(before) | set(committed) | set(indexed) | untracked):
        relative(name)
        entries = [mapping.get(name) for mapping in (before, committed, indexed)]
        if any(entry and entry[0] == "160000" for entry in entries):
            require(entries[0] is not None and entries.count(entries[0]) == 3, "gitlink", "changed Gitlink: " + name)
            require(index_flags.get(name, "H") not in ("S", "s") and not index_flags.get(name, "H").islower(), "gitlink", "hidden Gitlink index flags: " + name)
            initialized = check_gitlink_directory(root, name, entries[0][1])
            # Absent/empty checkouts contain no source to inspect.
            if initialized:
                dirty = run_git(root, "diff", "--no-ext-diff", "--ignore-submodules=none", "--raw", "HEAD", "--", name).stdout
                require(not dirty, "gitlink", "dirty Gitlink: " + name)
            inventory.append({"path": name, "mode": "160000", "gitlink": entries[0][1]})
            continue
        require(all(not entry or entry[0] in ("100644", "100755", "120000") for entry in entries), "filesystem", "unsafe Git mode: " + name)
        # Check every parent using lstat, and never dereference a tracked symlink.
        path = root
        for part in relative(name).parts[:-1]:
            path = path / part
            if not path.exists() and not path.is_symlink():
                break
            require(stat.S_ISDIR(path.lstat().st_mode), "filesystem", "unsafe repository parent: " + name)
        path = root / name
        try:
            info = path.lstat()
        except FileNotFoundError:
            info = None
        if info is not None and stat.S_ISLNK(info.st_mode):
            require(path.resolve().is_relative_to(root), "filesystem", "repository symlink escapes root: " + name)
            data, mode = os.fsencode(os.readlink(path)), "120000"
        else:
            data = regular(root, name, missing=True)
            mode = "100755" if info is not None and info.st_mode & 0o111 else "100644"
        current = raw_current = None
        if data is not None:
            oid = hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()
            current = raw_current = (mode, oid)
            if name.startswith(DIRECTORY + "/") and name.endswith(".json"):
                current = (mode, blob_identity(data, name, definitions))
            inventory.append({"path": name, "mode": mode, "sha256": blob_identity(data, name, definitions)})
        states = [identity(name, entry) for entry in entries]
        if any(entry and entry[0] == "120000" for entry in [*entries, (mode, "") if data is not None else None]):
            require(states[0] is not None and states.count(states[0]) == 3 and current == states[0], "filesystem", "new or changed repository symlink: " + name)
        if states[1] not in (states[0], current):
            committed_shadows.append(shadow(name, entries[1]))
        if states[2] not in (states[0], current):
            index_shadows.append(shadow(name, entries[2]))
        kinds = set()
        for left, right in zip([*entries, raw_current], [*entries[1:], raw_current]):
            if left == right:
                continue
            if left is None:
                kinds.add("add")
            elif right is None:
                kinds.add("delete")
            else:
                if left[0] != right[0]:
                    kinds.add("mode")
                if left[1] != right[1]:
                    kinds.add("modify")
        if kinds:
            mutations[name] = sorted(kinds)
    require(run_git(root, "rev-parse", "HEAD").stdout.decode().strip() == head, "concurrent-drift", "HEAD changed during inventory")
    return {"files": inventory, "committed_shadows": committed_shadows, "index_shadows": index_shadows}, mutations, head


def bound(root, binding, baseline=None):
    data = regular(root, binding["path"])
    require(digest(data) == binding["sha256"], "binding", "digest mismatch: " + binding["path"])
    if baseline is not None:
        original = run_git(root, "show", baseline + ":" + binding["path"]).stdout
        require(data == original, "baseline-input", "immutable input differs from baseline: " + binding["path"])
    return data


def timestamp(value):
    try:
        require(bool(re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z", value)), "time", "timestamp must be UTC seconds")
        return datetime.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise Rejection("time", "invalid timestamp") from error


class FixedGitHubRedirect(urllib.request.HTTPRedirectHandler):
    """Authority reads never follow redirects to another resource or host."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise Rejection("receipt", "GitHub authority endpoint redirected")


def comments(issue):
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), FixedGitHubRedirect())
    result = []
    for page in range(1, 11):
        url = f"https://api.github.com/repos/{REPOSITORY}/issues/{issue}/comments?per_page=100&page={page}"
        headers = {"Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "Hexalith-corrective-work-validator-v1"}
        token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
        if token:
            headers["Authorization"] = "Bearer " + token
        try:
            with opener.open(urllib.request.Request(url, headers=headers, method="GET"), timeout=20) as response:
                data = response.read(MAX_BYTES + 1)
            rows = load(data)
        except (OSError, urllib.error.URLError) as error:
            raise Rejection("receipt", "GitHub authority read failed") from error
        require(type(rows) is list and len(rows) <= 100, "receipt", "invalid GitHub comment listing")
        result.extend(rows)
        if len(rows) < 100:
            return result
    raise Rejection("receipt", "authority comment inventory exceeds bounded pagination")


def authenticated(comment, issue, payload, owner=None, owner_only=False):
    require(type(comment) is dict, "receipt", "invalid GitHub comment")
    user = comment.get("user")
    comment_id = comment.get("id")
    require(type(comment_id) is int and comment_id > 0 and type(user) is dict, "receipt", "missing numeric human identity")
    require(type(user.get("id")) is int and user["id"] > 0 and type(user.get("login")) is str and user.get("type") == "User", "receipt", "receipt author is not a human")
    require(comment.get("performed_via_github_app") is None and comment.get("author_association") in (("OWNER",) if owner_only else ("OWNER", "MEMBER", "COLLABORATOR")), "receipt", "receipt is not from an authorized human")
    require(comment.get("url") == f"https://api.github.com/repos/{REPOSITORY}/issues/comments/{comment_id}"
            and comment.get("issue_url") == f"https://api.github.com/repos/{REPOSITORY}/issues/{issue}"
            and comment.get("html_url") == f"https://github.com/{REPOSITORY}/issues/{issue}#issuecomment-{comment_id}", "receipt", "receipt endpoint identity mismatch")
    created = timestamp(comment.get("created_at", ""))
    require(comment.get("updated_at") == comment.get("created_at"), "receipt", "edited authority receipt")
    require(comment.get("body") == canonical(payload).decode(), "receipt", "receipt payload mismatch")
    if owner is not None:
        require((user["id"], user["login"]) == (owner["github_id"], owner["github_login"]), "owner", "receipt owner identity mismatch")
    return created, comment_id


def select_receipts(rows, schema, identity, owner, identity_field="subject_sha256"):
    matches = []
    for row in rows:
        # Unrelated accounts cannot deny authority by copying a valid payload.
        if type(row) is not dict or type(row.get("user")) is not dict:
            continue
        user = row["user"]
        if type(user.get("id")) is not int or (user["id"], user.get("login")) != (owner["github_id"], owner["github_login"]):
            continue
        require(type(row.get("body")) is str, "receipt", "malformed eligible GitHub comment")
        try:
            body = load(row["body"].encode())
        except Rejection:
            continue  # Ordinary issue discussion is not authority.
        if type(body) is dict and body.get("schema") == schema and body.get(identity_field) == identity:
            matches.append(row)
    return matches


def authority(root, document, definitions, baseline, now):
    registry_bytes = bound(root, document["owner_role_registry"], baseline)
    registry = load(registry_bytes)
    shape(registry, definitions["registry"], definitions, "registry")
    require(registry["repository"] == REPOSITORY, "registry", "registry repository mismatch")
    owners = registry["owners"]
    require(len({o["github_id"] for o in owners}) == len(owners) and len({o["github_login"].casefold() for o in owners}) == len(owners), "registry", "duplicate human registry identity")
    owner = document["owner"]
    require(any(owner["github_id"] == o["github_id"] and owner["github_login"] == o["github_login"] and owner["role"] in o["roles"] for o in owners), "owner", "owner role is absent from unchanged registry")
    registry_schema = "hexalith.corrective-owner-registry-approval.v1"
    registry_payload = {"schema": registry_schema, "subject_sha256": digest(registry_bytes)}
    roster_receipts = select_receipts(comments(registry["authority_issue"]), registry_schema, digest(registry_bytes), registry["authority_owner"])
    require(len(roster_receipts) == 1, "registry", "exactly one authenticated registry approval is required")
    roster_time, roster_id = authenticated(roster_receipts[0], registry["authority_issue"], registry_payload, registry["authority_owner"])
    require(timestamp(registry["created_at"]) <= roster_time <= timestamp(document["issued_at"]) <= now, "registry", "registry authority does not predate issuance")
    approval_schema = "hexalith.corrective-work-approval.v1"
    approval_payload = {"schema": approval_schema, "subject_sha256": document["authorization_sha256"]}
    rows = comments(registry["approvals_issue"])
    approvals = select_receipts(rows, approval_schema, document["authorization_sha256"], owner)
    require(len(approvals) == 1, "receipt", "exactly one unchanged authorization approval is required")
    approved_at, approval_id = authenticated(approvals[0], registry["approvals_issue"], approval_payload, owner)
    now = datetime.datetime.now(datetime.timezone.utc)
    require(roster_id != approval_id and timestamp(document["issued_at"]) <= approved_at <= now < timestamp(document["expires_at"]), "receipt", "approval is outside the authority interval")
    for row in select_receipts(rows, "hexalith.corrective-work-revocation.v1", document["authorization_sha256"], owner):
        payload = {"schema": "hexalith.corrective-work-revocation.v1", "subject_sha256": document["authorization_sha256"]}
        revoked_at, _identifier = authenticated(row, registry["approvals_issue"], payload, owner)
        require(approved_at <= revoked_at <= datetime.datetime.now(datetime.timezone.utc), "receipt", "revocation is outside the authority interval")
        raise Rejection("revoked", "authorization has an authenticated revocation")
    completion_schema = "hexalith.corrective-work-completion.v1"
    completions = select_receipts(rows, completion_schema, document["authorization_sha256"], owner, "authorization_sha256")
    require(len(completions) <= 1, "completion", "at most one eligible completion commitment may exist; obtain new authorization")
    receipt = {"registry_sha256": digest(registry_bytes), "registry_receipt": roster_id, "approval_receipt": approval_id}
    completion = None
    if completions:
        completion = load(completions[0]["body"].encode())
        shape(completion, definitions["completion"], definitions, "completion")
        completed_at, completion_id = authenticated(completions[0], registry["approvals_issue"], completion, owner)
        require(approved_at <= completed_at <= datetime.datetime.now(datetime.timezone.utc) < timestamp(document["expires_at"]), "completion", "completion is outside the authority interval")
        require(completion_id not in (roster_id, approval_id), "completion", "completion receipt identity is reused")
        receipt["completion_receipt"] = completion_id
    return receipt, completion


def validate_scope(document, mutations):
    rules = document["scope"]
    allowed = {item["path"]: set(item["mutations"]) for item in rules["allowed"]}
    require(len(allowed) == len(rules["allowed"]), "scope", "duplicate allowed path")
    for name in allowed:
        relative(name)
    for name in rules["prohibited_paths"]:
        relative(name[:-1] if name.endswith("/") else name)
    require(rules["prohibited_actions"] == ACTIONS, "scope", "all external authority actions must be prohibited")
    for name, kinds in mutations.items():
        require(not any(name == blocked.rstrip("/") or name.startswith(blocked.rstrip("/") + "/") for blocked in rules["prohibited_paths"]), "prohibited-path", "prohibited changed path: " + name)
        require(name in allowed, "out-of-scope", "changed path has no exact authorization: " + name)
        require(set(kinds) <= allowed[name], "mutation", "unauthorized mutation for " + name + ": " + ",".join(kinds))


def input_subject(root, document):
    baseline_tree = run_git(root, "rev-parse", document["baseline_commit"] + "^{tree}").stdout.decode().strip()
    inputs = {key: document[key] for key in ("baseline_commit", "prd", "failed_gate", "owner_role_registry", "validator", "contract", "expected_evidence", "derivation")}
    return digest(canonical({"rule": RULE, "baseline_tree": baseline_tree, "immutable_inputs": inputs}))


def output_subject(document, repository, evidence_results):
    return digest(canonical({"rule": RULE, "authorization_sha256": document["authorization_sha256"],
                             "input_subject_sha256": document["input_subject_sha256"],
                             "repository": repository, "evidence_results": evidence_results}))


def execute_evidence(root, document):
    results = []
    for evidence in document["expected_evidence"]:
        path = evidence["file"]["path"]
        require(run_git(root, "check-ignore", "--no-index", "--quiet", "--", path, check=False).returncode == 1, "ignored-evidence", "evidence is ignored: " + path)
        evidence_bytes = regular(root, path) if evidence["file"]["sha256"] is None else bound(root, evidence["file"])
        validator = evidence["validator"]
        relative(validator["path"])
        require(validator["path"].startswith("tools/") and validator["path"].endswith(".py"), "validator", "evidence validator must be an approved repository Python script")
        bound(root, validator)
        for argument in evidence["arguments"]:
            require(argument and not any(ord(c) < 32 for c in argument), "validator", "unsafe evidence argument")
        # The evidence path is mandatory and first; no interpreter flags are supplied by the record.
        # Normalize only the interpreter token for portable developer/CI subjects;
        # the remaining recorded argv is exactly the executed argv.
        command = ["python3", "-I", "-S", "-B", validator["path"], "./" + path, *evidence["arguments"]]
        try:
            process = subprocess.run([sys.executable, *command[1:]], cwd=root, capture_output=True, timeout=60, check=False,
                                     env={key: value for key, value in os.environ.items() if key in ("PATH", "SYSTEMROOT", "WINDIR", "LANG", "LC_ALL")})
        except (OSError, subprocess.TimeoutExpired) as error:
            raise Rejection("evidence", "evidence validator could not complete: " + path) from error
        require(process.returncode == 0, "evidence", "evidence validator failed: " + path)
        results.append({"path": path, "file_sha256": digest(evidence_bytes), "validator_sha256": validator["sha256"], "command": command,
                        "exit_code": process.returncode, "stdout_sha256": digest(process.stdout), "stderr_sha256": digest(process.stderr)})
    return results


def evaluate(root, record_name, baseline, postflight=False, write_postflight=False):
    record_bytes = regular(root, record_name)
    document = load(record_bytes)
    contract_bytes = regular(root, CONTRACT)
    definitions = load(contract_bytes)["$defs"]
    shape(document, definitions["authorization"], definitions)
    require(document["baseline_commit"] == baseline and bool(re.fullmatch(r"[0-9a-f]{40}", baseline)), "baseline", "CLI baseline must equal the full authorized commit")
    require(run_git(root, "rev-parse", "--verify", baseline + "^{commit}").stdout.decode().strip() == baseline, "baseline", "baseline is not a canonical commit")
    require(run_git(root, "merge-base", "--is-ancestor", baseline, "HEAD", check=False).returncode == 0, "baseline", "baseline is not an ancestor of HEAD")
    require(record_name == f"{DIRECTORY}/{document['failed_gate']['id']}/{document['authorization_id']}.json", "record-location", "record is outside its gate/authorization directory")
    require(document["derivation"] == RULE and document["prd"]["path"] == PRD and document["owner_role_registry"]["path"] == REGISTRY
            and document["validator"]["path"] == SCRIPT and document["contract"]["path"] == CONTRACT, "contract", "immutable input path or derivation mismatch")
    for key in ("prd", "owner_role_registry", "validator", "contract"):
        bound(root, document[key], baseline)
    require(Path(__file__).resolve() == (root / SCRIPT).resolve(), "validator", "the authorized validator is not the running script")
    require(document["authorization_sha256"] == digest(canonical(projected_record(document))), "authorization-digest", "authorization bytes changed; obtain new approval")
    require(document["input_subject_sha256"] == input_subject(root, document), "input-subject", "input subject changed")
    now = datetime.datetime.now(datetime.timezone.utc)
    require(timestamp(document["issued_at"]) <= now < timestamp(document["expires_at"]), "expired", "authorization is unissued or expired")
    require(timestamp(document["issued_at"]) < timestamp(document["expires_at"]), "time", "invalid authority interval")
    require(not document["revoked"], "revoked", "authorization is revoked")
    gate = load(bound(root, document["failed_gate"]["evidence"], baseline))
    shape(gate, definitions["failed_gate_result"], definitions, "failed_gate_result")
    require(gate["gate"] == document["failed_gate"]["id"] and gate["result"] == "FAIL", "failed-gate", "bound gate result is not a failure at this baseline")
    evidence_paths = [item["file"]["path"] for item in document["expected_evidence"]]
    require(len(set(evidence_paths)) == len(evidence_paths), "evidence", "duplicate expected evidence")
    for item in document["expected_evidence"]:
        for binding in (item["file"], item["validator"]):
            relative(binding["path"])
        require(item["file"]["path"] != record_name, "evidence", "record cannot be its own evidence")
        require(item["validator"]["path"].startswith("tools/") and item["validator"]["path"].endswith(".py"), "validator", "evidence validator must be an approved repository Python script")
        allowed = {entry["path"]: entry["mutations"] for entry in document["scope"]["allowed"]}
        if item["file"]["sha256"] is None:
            require(item["file"]["path"] in allowed and set(allowed[item["file"]["path"]]) & {"add", "modify"}, "evidence", "deferred evidence requires an explicitly scoped output path")
        else:
            bound(root, item["file"])
        for argument in item["arguments"]:
            require(argument and not any(ord(c) < 32 for c in argument), "validator", "unsafe evidence argument")
        require(run_git(root, "check-ignore", "--no-index", "--quiet", "--", item["validator"]["path"], check=False).returncode == 1,
                "ignored-validator", "evidence validator is ignored: " + item["validator"]["path"])
        if regular(root, item["validator"]["path"], missing=True) is None:
            require(item["validator"]["path"] in allowed and "add" in allowed[item["validator"]["path"]], "validator", "an absent validator requires an explicitly scoped addition and preapproved digest")
        else:
            bound(root, item["validator"])
        require(run_git(root, "check-ignore", "--no-index", "--quiet", "--", item["file"]["path"], check=False).returncode == 1, "ignored-evidence", "evidence is ignored: " + item["file"]["path"])
    initial, mutations, head = snapshot(root, baseline, definitions)
    immutable_paths = {document[key]["path"] for key in ("prd", "owner_role_registry", "validator", "contract")} | {document["failed_gate"]["evidence"]["path"]}
    require(not immutable_paths.intersection(mutations), "baseline-input", "an immutable authority input appears in a committed, staged or working diff")
    validate_scope(document, mutations)
    receipt, completion = authority(root, document, definitions, baseline, now)
    # A current authenticated completion still applies when the local envelope is reset.
    postflight = postflight or completion is not None or document["postflight"] is not None
    results = None
    if postflight:
        results = execute_evidence(root, document)
        refreshed_receipt, refreshed_completion = authority(root, document, definitions, baseline, datetime.datetime.now(datetime.timezone.utc))
        require((receipt, completion) == (refreshed_receipt, refreshed_completion), "authority-drift", "authenticated authority changed during evidence execution")
    final, final_mutations, final_head = snapshot(root, baseline, definitions)
    require((initial, mutations, head) == (final, final_mutations, final_head) and regular(root, record_name) == record_bytes
            and regular(root, CONTRACT) == contract_bytes, "concurrent-drift", "repository inputs changed during evaluation")
    require(datetime.datetime.now(datetime.timezone.utc) < timestamp(document["expires_at"]), "expired", "authorization expired during evaluation")
    verdict = {"result": "PASS", "phase": "postflight" if postflight else "preflight", "authorization_id": document["authorization_id"],
               "authorization_sha256": document["authorization_sha256"], "baseline_commit": baseline,
               "input_subject_sha256": document["input_subject_sha256"], "changed_paths": sorted(mutations), "authority": receipt,
               "corrective-work-only": True, **{action: False for action in ACTIONS}}
    if postflight:
        output = output_subject(document, final, results)
        derived = {"output_subject_sha256": output, "evidence_results": results}
        if document["postflight"] is not None:
            require(document["postflight"] == derived, "postflight", "recorded output/evidence changed; obtain new authorization")
        payload = {"schema": "hexalith.corrective-work-completion.v1", "authorization_sha256": document["authorization_sha256"], "output_subject_sha256": output}
        if completion is None:
            raise Rejection("missing-completion", "owner completion commitment required; proposed payload grants no authority", proposed_completion=payload,
                            proposed_completion_body=canonical(payload).decode(), proposed_completion_sha256=digest(canonical(payload)))
        require(completion == payload, "completion", "output differs from the authenticated first-output commitment; obtain new authorization")
        if write_postflight:
            require(document["postflight"] is None, "postflight", "a recorded postflight cannot be overwritten; obtain new authorization")
            document["postflight"] = derived
            (root / record_name).write_bytes(canonical(document) + b"\n")
        else:
            require(document["postflight"] == derived, "postflight", "postflight output/evidence changed or has not been recorded")
        verdict["output_subject_sha256"] = output
        verdict["evidence_results"] = results
    return verdict


def registry_evaluation(root):
    """Report all live authorizations without treating an absent registry as PASS."""
    head = run_git(root, "rev-parse", "HEAD").stdout.decode().strip()
    result = {"result": "FAIL", "phase": "registry", "source_commit": head, "validator_sha256": digest(regular(root, SCRIPT)),
              "contract_sha256": digest(regular(root, CONTRACT)), "evaluations": [], "corrective-work-only": True,
              **{action: False for action in ACTIONS}}
    directory = root / DIRECTORY
    require(directory.exists(), "missing-authority", "live corrective-work authorization registry is absent")
    regular(root, REGISTRY)
    records = []
    for gate_dir in sorted(directory.iterdir()):
        require(not gate_dir.is_symlink(), "filesystem", "symlink in authorization registry")
        if gate_dir.name == "owner-role-registry.json":
            continue
        require(gate_dir.is_dir(), "registry", "unexpected live registry entry")
        for path in sorted(gate_dir.iterdir()):
            require(path.is_file() and not path.is_symlink() and path.suffix == ".json", "registry", "unexpected authorization entry")
            records.append(path.relative_to(root).as_posix())
    require(records, "missing-authority", "live registry has no authorizations")
    for name in records:
        try:
            document = load(regular(root, name))
            definitions = load(regular(root, CONTRACT))["$defs"]
            shape(document, definitions["authorization"], definitions)
            baseline = document["baseline_commit"]
            result["evaluations"].append(evaluate(root, name, baseline, postflight=True))
        except (Rejection, KeyError, TypeError, ValueError, OSError) as error:
            result["evaluations"].append({"record": name, "result": "FAIL", "reason": getattr(error, "code", "schema"), "detail": str(error)})
    result["result"] = "PASS" if all(item["result"] == "PASS" for item in result["evaluations"]) else "FAIL"
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("record", nargs="?")
    parser.add_argument("--changed-paths-from")
    parser.add_argument("--postflight", action="store_true")
    parser.add_argument("--write-postflight", action="store_true", help="record the first derived postflight after successful evidence validation")
    parser.add_argument("--evaluate-registry", action="store_true", help="evaluate every live record; absent authority is FAIL")
    arguments = parser.parse_args()
    try:
        root = Path(run_git(Path.cwd(), "rev-parse", "--show-toplevel").stdout.decode().strip()).resolve()
        if arguments.evaluate_registry:
            require(not arguments.record and not arguments.changed_paths_from and not arguments.postflight and not arguments.write_postflight, "usage", "registry evaluation does not accept record options")
            verdict = registry_evaluation(root)
        else:
            require(arguments.record is not None and arguments.changed_paths_from is not None, "usage", "record and --changed-paths-from are required")
            require(not arguments.write_postflight or arguments.postflight, "usage", "--write-postflight requires --postflight")
            path = Path(arguments.record)
            record_name = path.relative_to(root).as_posix() if path.is_absolute() else path.as_posix()
            relative(record_name)
            verdict = evaluate(root, record_name, arguments.changed_paths_from, arguments.postflight, arguments.write_postflight)
    except (Rejection, OSError, ValueError, KeyError, TypeError, UnicodeError, subprocess.SubprocessError) as error:
        verdict = {"result": "FAIL", "phase": "registry" if arguments.evaluate_registry else "postflight" if arguments.postflight else "preflight",
                   "reason": getattr(error, "code", "invalid-input"), "detail": str(error), "corrective-work-only": True,
                   **{action: False for action in ACTIONS}}
        verdict.update(getattr(error, "metadata", {}))
        if arguments.evaluate_registry:
            # These pins make even missing-authority failures retrievable against exact code/HEAD.
            try:
                verdict.update({"source_commit": run_git(root, "rev-parse", "HEAD").stdout.decode().strip(),
                                "validator_sha256": digest(regular(root, SCRIPT)), "contract_sha256": digest(regular(root, CONTRACT))})
            except (Rejection, OSError, UnboundLocalError):
                pass
    print(canonical(verdict).decode())
    return 0 if verdict["result"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
