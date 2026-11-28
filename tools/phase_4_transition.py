"""Authenticated Phase 4 transitions and the ordered Phase 4 readiness decision.

Only fixed adapters execute, with pinned code/inputs in a token-free scratch process.
Evaluation is candidate evidence. Consumption independently fetches a completed run
and recomputes every input, approval and predecessor before accepting its artifact.
"""

import argparse
import base64
import binascii
import ctypes
import datetime as dt
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import signal
import stat
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile
import zlib

_SPEC = importlib.util.spec_from_file_location(
    "phase_4_matrix", Path(__file__).with_name("validate-phase-4-gate-risk-matrix.py"))
M = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(M)
REPOSITORY = "Hexalith/Hexalith.EventStore"
TENANTS = "Hexalith/Hexalith.Tenants"
MATRIX = "_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json"
REGISTRY = M.REGISTRY
ARCHITECTURE = "_bmad-output/planning-artifacts/architecture.md"
REQUEST_SCHEMA = "hexalith.eventstore.phase-4-transition-request.v1"
RESULT_SCHEMA = "hexalith.eventstore.phase-4-transition-result.v1"
APPROVAL_SCHEMA = "hexalith.eventstore.phase-4-transition-approval.v1"
REQUEST_KEYS = {"schema", "repository", "transition", "head_sha", "subject_mode", "subjects", "evidence",
                "matrix", "registry", "required_level", "author", "gate", "nfr_ids", "approvals", "predecessors"}
RESULT_KEYS = {"schema", "phase", "result", "codes", "binding", "required_level", "achieved_level",
               "downstream_minimum", "approvals", "approval_payloads", "predecessors", "seal"}
HIGH_RISK_NFRS = {f"NFR{n}" for n in (1, 2, 3, 4, 7, 10, 11, 12, 14, 15, 16, 17)}
CONTROL_FILES = ("tools/phase_4_transition.py", "tools/validate-phase-4-transition.py",
                 "tools/phase-4-transition.schema.json", "tools/validate-phase-4-gate-risk-matrix.py",
                 "tools/phase_4_gate_decision.py", "tools/validate-phase-4-gate-decision.py")
STORY_COMPLETION_FILES = ("tools/phase_4_story_completion.py", "tools/validate-phase-4-story-completion.py",
                          "_bmad/custom/bmad-build.toml")
STORY_CONTROL_FILES = (*STORY_COMPLETION_FILES, "tools/phase_4_story_5_3.py", "tools/validate-story-5-3-nfr.py",
                       "tools/tests/test_phase_4_story_5_3.py")
OQ8_VALIDATOR = "tools/validate-oq8-governing-design.py"
OQ8_FIXTURES = "tools/tests/test_oq8_governing_design.py"
OQ8_INPUTS = (
    OQ8_FIXTURES, ".editorconfig", ".gitattributes",
    "docs/architecture/oq8/oq8-idempotency-design.md", "docs/architecture/oq8/LICENSE",
    "docs/architecture/oq8/README.md", "_bmad-output/planning-artifacts/prd.md", ARCHITECTURE,
    "_bmad-output/planning-artifacts/epics.md",
    "_bmad-output/implementation-artifacts/spec-4-17-oq8-governing-design-authority-import.md",
    ".github/workflows/oq8-governing-design.yml")
OQ8_BINDINGS = {'command': 'python3 -B tools/validate-oq8-governing-design.py', 'trigger': 'push and pull_request to main and release/parties-sdk-3.130.2, and workflow_dispatch, in .github/workflows/oq8-governing-design.yml', 'subject_paths': ['.github/workflows/oq8-governing-design.yml', 'docs/architecture/oq8/LICENSE', 'docs/architecture/oq8/README.md', 'docs/architecture/oq8/oq8-idempotency-design.md', 'tools/tests/test_oq8_governing_design.py', 'tools/validate-oq8-governing-design.py'], 'evidence_identity': 'Job live-evaluation of .github/workflows/oq8-governing-design.yml, reporting the result with the source SHA and workflow SHA-256', 'pass_condition': 'The validator exits 0 and reports design SHA-256 1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8, and the fixture job running test_oq8_governing_design.py passes'}
MAX_BYTES = 1024 * 1024
MAX_ARCHIVE = 4 * MAX_BYTES
MAX_PAGES = 50
MAX_RESULT_INPUTS = 200 + 2 + len(CONTROL_FILES) + 1
ACTIONS_APP_ID = 15368
CODES = (
    "INPUT_INVALID", "API_UNAVAILABLE", "API_INVALID", "PAGINATION_INCOMPLETE", "SOURCE_MISMATCH",
    "REGISTRY_INVALID", "LEVEL_MISMATCH", "AUTHOR_INVALID", "SUBJECT_MISMATCH", "MATRIX_INVALID",
    "GATE_UNBOUND", "BUSINESS_PREREQUISITE_MISSING", "ADAPTER_UNAVAILABLE", "HISTORY_UNAVAILABLE",
    "APPROVAL_MISSING", "APPROVAL_INVALID", "APPROVAL_IDENTITY", "APPROVAL_ROLE", "APPROVAL_EDITED",
    "APPROVAL_WINDOW", "APPROVAL_FUTURE", "INDEPENDENT_REQUIRED", "PREDECESSOR_IDENTITY",
    "PREDECESSOR_FAILED", "PREDECESSOR_LIMIT", "RUN_INVALID", "RUN_NOT_SUCCESSFUL", "CI_UNAUTHENTICATED",
    "WORKFLOW_MISMATCH", "ARTIFACT_INVALID", "RESULT_MISMATCH", "SELF_SEAL_FORBIDDEN")
CODES += ("STORY_IDENTITY_MISSING", "COVERAGE_AMBIGUOUS", "COVERAGE_MISMATCH", "SEAL_MISSING")
CODES += ("STORY_EVIDENCE_FAILED", "STORY_EVIDENCE_LIMIT")


class Refusal(Exception):
    """Stable, controlled failure without a traceback."""

    def __init__(self, code, message, exit_code=1):
        super().__init__(message)
        self.code, self.exit_code = code, exit_code


def require(condition, code, message, exit_code=1):
    if not condition:
        raise Refusal(code, message, exit_code)


def digest(value):
    return hashlib.sha256(value).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()


def closed(value, keys):
    require(isinstance(value, dict) and set(value) == set(keys), "INPUT_INVALID",
            "object must have exactly: " + ", ".join(sorted(keys)), 2)


def sha(value, size=64):
    return isinstance(value, str) and re.fullmatch(r"[0-9a-f]{" + str(size) + "}", value) is not None


def positive(value):
    return type(value) is int and value > 0


def timestamp(value):
    try:
        require(isinstance(value, str) and M.TIMESTAMP.fullmatch(value), "API_INVALID", "invalid UTC timestamp")
        return dt.datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)
    except ValueError as error:
        raise Refusal("API_INVALID", "invalid UTC timestamp") from error


def parse(content):
    try:
        return M.parse_json(content, "transition JSON")
    except M.InputError as error:
        raise Refusal("INPUT_INVALID", str(error), 2) from error


def parse_transition_document(content):
    try:
        return parse(content)
    except Refusal as error:
        if "duplicate key" in str(error):
            # Only locate the gate after strict parsing has already refused the
            # document. Preserve object members so nested duplicates survive.
            class Members(list):
                pass

            def repeated(node):
                if isinstance(node, Members):
                    return len(dict(node)) != len(node) or any(repeated(v) for _, v in node)
                return isinstance(node, list) and any(repeated(v) for v in node)

            def fields(node):
                return dict(node) if isinstance(node, Members) else {}

            try:
                doc = fields(json.loads(content, object_pairs_hook=Members, parse_constant=M._reject_constant))
                source = doc if "transition" in doc else fields(doc.get("binding"))
                gates = fields(source.get("readiness")).get("gates")
                if source.get("transition") == "readiness" and isinstance(gates, Members):
                    seen = set()
                    for gate, item in gates:
                        if gate in seen or repeated(item):
                            error.gate = gate
                            break
                        seen.add(gate)
            except (ValueError, RecursionError):
                pass
        raise


def bindings(items, empty=False):
    require(isinstance(items, list) and len(items) <= 100 and (empty or items), "INPUT_INVALID", "invalid file inventory", 2)
    paths = []
    for item in items:
        closed(item, ("path", "sha256"))
        require(M.repository_path(item["path"]) is not None and sha(item["sha256"]),
                "INPUT_INVALID", "invalid file path or digest", 2)
        paths.append(item["path"])
    require(len(set(paths)) == len(paths), "INPUT_INVALID", "duplicate file binding", 2)


def validate_request(request):
    require(isinstance(request, dict), "INPUT_INVALID", "request must be an object", 2)
    story_completion = request.get("transition") == "high-risk-nfr-story-done"
    if story_completion:
        require("story" in request, "STORY_IDENTITY_MISSING", "story completion needs a canonical story claim")
        closed(request, (REQUEST_KEYS - {"nfr_ids"}) | {"story"} | ({"nfr_ids", "tenant_gate"} & set(request)))
        import phase_4_story_completion as S
        S.validate_claim(request["story"])
        if "tenant_gate" in request:
            import phase_4_story_2_14 as A
            A.validate_request(request)
    elif request.get("gate") == "G-TENANT" and request.get("transition") == "high-risk-gate-pass":
        closed(request, REQUEST_KEYS | ({"tenant_qualification"} & request.keys()))
        if "tenant_qualification" in request:
            import phase_4_tenant_gate as T
            T.validate_claim(request["tenant_qualification"], request["head_sha"])
    elif request.get("gate") == "G-STATUS-ID" and request.get("transition") == "high-risk-gate-pass":
        import phase_4_status_id_gate as S
        require("status_qualification" in request, "BUSINESS_PREREQUISITE_MISSING", "G-STATUS-ID needs its EventStore CI qualification")
        closed(request, REQUEST_KEYS | {"status_qualification"})
        S.validate_claim(request["status_qualification"], request["head_sha"])
    elif request.get("transition") == "readiness":
        import phase_4_readiness as R
        closed(request, REQUEST_KEYS | ({"readiness"} & request.keys()))
        R.validate(request)
    else:
        closed(request, REQUEST_KEYS)
    require(request["schema"] == REQUEST_SCHEMA and request["repository"] == REPOSITORY,
            "INPUT_INVALID", "unsupported schema or repository", 2)
    require(request["transition"] in M.TRANSITIONS and sha(request["head_sha"], 40),
            "INPUT_INVALID", "unknown transition or invalid head SHA", 2)
    require(request["subject_mode"] in ("files", "ad26") and request["required_level"] in M.LEVELS[1:]
            and isinstance(request["author"], str), "INPUT_INVALID", "invalid subject mode, level or author", 2)
    require(request["gate"] is None or isinstance(request["gate"], str) and request["gate"] in M.HIGH_RISK_GATES,
            "INPUT_INVALID", "unknown high-risk gate", 2)
    for key in ("subjects", "evidence"):
        bindings(request[key], empty=key == "evidence")
    for key, path in (("matrix", MATRIX), ("registry", REGISTRY)):
        bindings([request[key]])
        require(request[key]["path"] == path, "INPUT_INVALID", "fixed matrix/registry slot required", 2)
    nfr_ids = request.get("nfr_ids", [])
    maximum = 19 if story_completion else 18
    require(isinstance(nfr_ids, list) and len(nfr_ids) <= maximum
            and all(isinstance(n, str) and re.fullmatch(r"NFR(?:[1-9]|1[0-" + str(maximum - 10) + r"])", n) for n in nfr_ids)
            and len(set(nfr_ids)) == len(nfr_ids), "INPUT_INVALID", "invalid NFR inventory", 2)
    require(isinstance(request["approvals"], list) and len(request["approvals"]) <= 30,
            "INPUT_INVALID", "invalid approval inventory", 2)
    for approval in request["approvals"]:
        closed(approval, ("comment_id", "body_sha256", "role"))
        require(positive(approval["comment_id"]) and sha(approval["body_sha256"])
                and isinstance(approval["role"], str) and M.ROLE_ID.fullmatch(approval["role"]),
                "INPUT_INVALID", "invalid approval reference", 2)
    require(len({a["comment_id"] for a in request["approvals"]}) == len(request["approvals"]),
            "INPUT_INVALID", "duplicate approval comment", 2)
    require(isinstance(request["predecessors"], list) and len(request["predecessors"]) <= 20,
            "INPUT_INVALID", "invalid predecessor inventory", 2)
    for predecessor in request["predecessors"]:
        closed(predecessor, ("adapter", "commit", "validator", "inputs", "output_sha256", "assurance_level"))
        require(isinstance(predecessor["adapter"], str) and sha(predecessor["commit"], 40)
                and sha(predecessor["output_sha256"]) and predecessor["assurance_level"] in M.LEVELS,
                "INPUT_INVALID", "invalid predecessor identity", 2)
        bindings([predecessor["validator"]])
        bindings(predecessor["inputs"])
    return request


class NoRedirect(urllib.request.HTTPRedirectHandler):
    """Prevent credentials leaving the fixed API origin, including download redirects."""

    def redirect_request(self, request, response, code, message, headers, newurl):
        return None


class GitHub:
    """Read-only bounded GitHub transport. Tests replace the opener, never identity logic."""

    def __init__(self, repository=REPOSITORY):
        require(repository in (REPOSITORY, TENANTS), "API_INVALID", "unsupported evidence repository")
        self.repository = repository
        self.prefix = f"https://api.github.com/repos/{repository}"
        self.opener = urllib.request.build_opener(NoRedirect())
        self.cache = {}
        self.calls = 0

    def fetch(self, url, binary=False, binary_limit=MAX_ARCHIVE):
        require(url.startswith(self.prefix + "/"), "API_INVALID", "API URL outside repository")
        headers = {"Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28"}
        token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
        if self.repository == TENANTS:
            token = os.environ.get("PHASE4_TENANTS_READ_TOKEN") or token
        if token:
            headers["Authorization"] = "Bearer " + token
        self.calls += 1
        require(self.calls <= 1000, "API_UNAVAILABLE", "API request budget exhausted")
        try:
            request = urllib.request.Request(url, headers=headers)
            try:
                response = self.opener.open(request, timeout=20)
            except urllib.error.HTTPError as error:
                if binary and error.code == 302:
                    location = error.headers.get("Location", "")
                    host = urllib.parse.urlsplit(location)
                    require(host.scheme == "https" and host.hostname is not None
                            and (host.hostname.endswith(".blob.core.windows.net")
                                 or host.hostname.endswith(".actions.githubusercontent.com"))
                            and host.username is None and host.password is None,
                            "ARTIFACT_INVALID", "unsafe artifact redirect")
                    # Signed storage URL receives no API token, and no subsequent redirect.
                    response = self.opener.open(urllib.request.Request(location), timeout=20)
                else:
                    raise
            with response:
                require(response.status == 200, "API_UNAVAILABLE", "unexpected API response")
                # Complete story-source membership is larger than ordinary API
                # documents; only the fixed Git tree endpoint gets this bound.
                tree_inventory = url.startswith(self.prefix + "/git/trees/")
                limit = binary_limit if binary else 16 * MAX_BYTES if tree_inventory else 2 * MAX_BYTES
                content = response.read(limit + 1)
                require(len(content) <= limit, "ARTIFACT_INVALID" if binary else "API_INVALID", "response too large", 2 if binary else 1)
                links = response.headers.get("Link", "")
            if binary:
                return content, links
            try:
                return json.loads(content.decode(), object_pairs_hook=M._unique_object,
                                  parse_constant=M._reject_constant), links
            except (ValueError, UnicodeError, RecursionError) as error:
                raise Refusal("API_INVALID", "malformed API JSON") from error
        except (urllib.error.URLError, OSError, TimeoutError) as error:
            refusal = Refusal("API_UNAVAILABLE", "GitHub read failed: " + type(error).__name__)
            refusal.http_status = error.code if isinstance(error, urllib.error.HTTPError) else None
            raise refusal from error

    def get(self, endpoint):
        value, _ = self.fetch(self.prefix + endpoint)
        require(isinstance(value, dict), "API_INVALID", "expected an API object")
        return value

    def iter_pages(self, endpoint, key=None):
        """Yield authenticated pages as needed; consumers may stop once proof is complete."""
        url, seen, count = self.prefix + endpoint, set(), 0
        for _ in range(MAX_PAGES):
            require(url not in seen, "PAGINATION_INCOMPLETE", "pagination cycle")
            seen.add(url)
            value, links = self.fetch(url)
            page = value.get(key) if key and isinstance(value, dict) else value if key is None else None
            require(isinstance(page, list) and all(isinstance(item, dict) for item in page),
                    "API_INVALID", "invalid API inventory")
            count += len(page)
            matches = re.findall(r'<([^>]+)>;\s*rel="next"', links)
            require(len(matches) <= 1, "PAGINATION_INCOMPLETE", "ambiguous next page")
            if not matches and key and "total_count" in value:
                require(value["total_count"] == count, "PAGINATION_INCOMPLETE", "incomplete API inventory")
            yield page
            if not matches:
                return
            url = matches[0]
        raise Refusal("PAGINATION_INCOMPLETE", "pagination limit reached")

    def pages(self, endpoint, key=None):
        return [item for page in self.iter_pages(endpoint, key) for item in page]

    def path_absent(self, commit, path):
        """Confirm absence through the authenticated commit/tree, never from 404 alone."""
        record = self.get("/git/commits/" + commit)
        require(record.get("sha") == commit and sha(record.get("tree", {}).get("sha"), 40),
                "HISTORY_UNAVAILABLE", "before commit is not authenticated")
        tree = record["tree"]["sha"]
        parts = path.split("/")
        for index, part in enumerate(parts):
            record = self.get("/git/trees/" + tree)
            require(record.get("sha") == tree and record.get("truncated") is False
                    and isinstance(record.get("tree"), list), "HISTORY_UNAVAILABLE", "before tree is incomplete")
            entries = record["tree"]
            require(all(isinstance(item, dict) and isinstance(item.get("path"), str) for item in entries)
                    and len({item["path"] for item in entries}) == len(entries),
                    "HISTORY_UNAVAILABLE", "before tree is malformed")
            matches = [item for item in entries if item["path"] == part]
            if not matches:
                return True
            entry = matches[0]
            if index == len(parts) - 1:
                return False
            if entry.get("type") != "tree":
                require(entry.get("type") in ("blob", "commit"), "HISTORY_UNAVAILABLE", "unknown before tree entry")
                return True
            require(sha(entry.get("sha"), 40), "HISTORY_UNAVAILABLE", "invalid before subtree")
            tree = entry["sha"]
        return False

    def file(self, commit, path, allow_absent=False):
        key = (commit, path)
        if key not in self.cache:
            try:
                data = self.get("/contents/" + urllib.parse.quote(path, safe="/") + "?ref=" + commit)
            except Refusal as error:
                if allow_absent and getattr(error, "http_status", None) == 404 and self.path_absent(commit, path):
                    return None
                raise
            require(data.get("type") == "file" and data.get("path") == path
                    and data.get("encoding") == "base64" and type(data.get("size")) is int
                    and 0 <= data["size"] <= MAX_BYTES, "SOURCE_MISMATCH", "missing or oversized Git blob: " + path)
            try:
                content = base64.b64decode(data["content"].replace("\n", ""), validate=True)
            except (ValueError, KeyError, AttributeError, binascii.Error) as error:
                raise Refusal("API_INVALID", "invalid Git blob encoding") from error
            require(len(content) == data["size"] and data.get("sha") == hashlib.sha1(
                b"blob " + str(len(content)).encode() + b"\0" + content).hexdigest(),
                "SOURCE_MISMATCH", "Git blob digest mismatch: " + path)
            self.cache[key] = content
        return self.cache[key]


def ad26_subject(content):
    """AD-26's exact ratification subject, independent of whole-file traceability."""
    try:
        lines = content.decode("utf-8").splitlines(keepends=True)
    except UnicodeError as error:
        raise Refusal("SUBJECT_MISMATCH", "invalid architecture UTF-8") from error
    append = [n for n, line in enumerate(lines) if line.startswith("**Append race (NFR7 class (c)).**")]
    section = [n for n, line in enumerate(lines) if line.startswith("### AD-26 ")]
    require(len(append) == len(section) == 1, "SUBJECT_MISMATCH", "ambiguous AD-26 subject")
    first, start = append[0], section[0]
    last = first
    while last < len(lines) and lines[last].strip():
        last += 1
    end = start + 1
    while end < len(lines) and not lines[end].startswith(("### ", "## ")):
        end += 1
    while end > start and not lines[end - 1].strip():
        end -= 1
    selected = [*lines[first:last], *lines[start:end]]
    require(all(line.endswith("\n") and not line.endswith("\r\n") for line in selected),
            "SUBJECT_MISMATCH", "AD-26 selected subject requires exact LF lines")
    header = re.sub(r" \[(?:ASSUMPTION|ADOPTED)\](?=\n$)", "", lines[start])
    return digest(("".join(lines[first:last]) + header + "".join(lines[start + 1:end])).encode())


def workflow_path(transition):
    return ".github/workflows/phase-4-" + transition + ".yml"


def approval_request_digest(request):
    # Approval references are added after the human posts; all substantive inputs remain bound.
    return digest(canonical({k: v for k, v in request.items() if k != "approvals"}))


class Control:
    """One evaluation or consumption, using current platform facts on every call."""

    def __init__(self, root, request, api):
        self.root, self.request, self.api = root, validate_request(request), api
        self.head = request["head_sha"]
        self.required = "none"
        self.subject = None
        self.roles = []
        self.binding = {}
        self.payloads = []
        self.approvals = []
        self.predecessors = []
        self.achieved = "none"
        self.minimum = "none"
        self.story_evidence = None
        self.story_inventory = None
        self.tenant_gate = request["gate"] == "G-TENANT" and request["transition"] == "high-risk-gate-pass"
        self.status_gate = request["gate"] == "G-STATUS-ID" and request["transition"] == "high-risk-gate-pass"
        self.tenant_story = "tenant_gate" in request
        self.readiness_owners = {}

    def local(self, path):
        require(M.repository_path(path) is not None, "INPUT_INVALID", "unsafe path", 2)
        candidate = self.root
        try:
            for part in path.split("/"):
                candidate /= part
                require(not candidate.is_symlink(), "INPUT_INVALID", "symbolic input path", 2)
            require(stat.S_ISREG(candidate.stat().st_mode), "INPUT_INVALID", "input must be a regular file", 2)
            with candidate.open("rb") as stream:
                require(stat.S_ISREG(os.fstat(stream.fileno()).st_mode), "INPUT_INVALID", "input must be a regular file", 2)
                content = stream.read(MAX_BYTES + 1)
        except OSError as error:
            raise Refusal("INPUT_INVALID", "cannot read local input: " + path, 2) from error
        require(len(content) <= MAX_BYTES, "INPUT_INVALID", "oversized local input", 2)
        return content

    def subject_at(self, commit, allow_absent=False):
        if self.request["transition"] == "readiness":
            import phase_4_readiness as R
            return digest(canonical({"baseline_tree": R.tree_at(self.api, commit)}))
        if self.request["transition"] in ("release-available", "production-promoted") and self.request["subject_mode"] == "files":
            import publication_authority as P
            try:
                return P.lifecycle_subject_at(self, commit, allow_absent)
            except P.Refusal as error:
                raise Refusal("SUBJECT_MISMATCH", error.code, error.exit_code) from error
        if (self.request["transition"] == "high-risk-nfr-story-done"
                and self.request["story"]["story_id"] == "5.3"):
            import phase_4_story_5_3 as A
            if A.supported(self.request["story"]):
                # The complete authenticated root tree, including gitlinks, is
                # the subject for current and historical pushes. Caller-selected
                # coverage files cannot hide a recent auth/scanner/build edit.
                return A.subject_at(self.api, commit, allow_absent)
        if self.request["subject_mode"] == "ad26":
            content = self.api.file(commit, ARCHITECTURE, allow_absent)
            return None if content is None else ad26_subject(content)
        files = []
        for binding in self.request["subjects"]:
            content = self.api.file(commit, binding["path"], allow_absent)
            if content is None:
                return None
            files.append((binding["path"], digest(content)))
        return M.subject_digest(files)

    def current_attempt(self, run_id, attempt):
        run = self.api.get(f"/actions/runs/{run_id}")
        require(run.get("id") == run_id and run.get("run_attempt") == attempt and run.get("head_sha") == self.head and
                run.get("status") == "completed" and run.get("conclusion") == "success",
                "RUN_NOT_SUCCESSFUL", "bound evidence attempt is no longer the current successful run")

    def reconfirm_approvals(self):
        previous = self.approvals
        self.approvals = []
        self.check_approvals()
        require(self.approvals == previous, "APPROVAL_INVALID", "authenticated approvals changed during consumption")

    def current_head(self):
        require(self.api.get("/git/ref/heads/main").get("object", {}).get("sha") == self.head,
                "SOURCE_MISMATCH", "attempt must bind the current main head")
        if self.request["transition"] == "readiness":
            import phase_4_readiness as R
            R.check_checkout(self)

    def matrix_invariants(self, validator, rows):
        for gate, row in rows.items():
            kind, reason = M.CLASSIFICATIONS[gate]
            expected = M.STANDARD_KEYS if kind == "standard-control" else M.HIGH_RISK_KEYS
            require(set(row) == set(expected) and row.get("classification") == kind,
                    "MATRIX_INVALID", "matrix row keys/classification differ from pinned invariants")
            if kind == "standard-control":
                require(row.get("reason") == reason, "MATRIX_INVALID", "standard-control reason differs from pin")
                continue
            require(row.get("required_level") == self.required and row.get("achieved_level") in M.LEVELS
                    and M.level_rank(row["achieved_level"]) <= M.level_rank(self.required),
                    "MATRIX_INVALID", "row assurance labels differ from registry")
            require(all(row[key] is None or M.is_text(row[key]) for key in M.TEXT_BINDINGS),
                    "MATRIX_INVALID", "invalid business binding")
            paths = row["subject_paths"]
            require(paths is None or M.distinct_strings(paths) and all(M.repository_path(p) for p in paths),
                    "MATRIX_INVALID", "invalid row subject paths")
            unbound = any(row[key] is None for key in M.BINDINGS)
            require((isinstance(row["binding_owner"], str) and M.BINDING_OWNER.fullmatch(row["binding_owner"]))
                    if unbound else row["binding_owner"] is None,
                    "MATRIX_INVALID", "invalid binding owner")
            author = self.registry["identities"].get(row["author"], {})
            require(author.get("kind") == "human" and row["author"] in self.registry["roles"].get(row["author_role"], [])
                    and M.distinct_strings(row["evaluator_roles"])
                    and all(role in self.registry["roles"] for role in row["evaluator_roles"]),
                    "MATRIX_INVALID", "invalid author or evaluator role")
            require(M.distinct_strings(row["guarded_transitions"])
                    and "high-risk-gate-pass" in row["guarded_transitions"]
                    and set(row["guarded_transitions"]) <= set(M.TRANSITIONS),
                    "MATRIX_INVALID", "invalid guarded transitions")
            require(isinstance(row["attestations"], list), "MATRIX_INVALID", "invalid row attestations")
            if row["seal"] is None or not row["attestations"]:
                require(row["achieved_level"] == "none", "MATRIX_INVALID", "unachieved row overstates assurance")
            if row["seal"] is not None:
                require(validator.seal_problem(row["seal"]) is None, "MATRIX_INVALID", "invalid offline seal shape")
            if row["achieved_level"] != "none":
                # Existing claims must satisfy the completed offline control too;
                # absent seals on truthful pending rows never become prerequisites.
                validator.check_high_risk(gate, row)
                require(not validator.findings, "MATRIX_INVALID", "unsupported existing row assurance")

    def prepare(self):
        request = self.request
        self.current_head()
        bound = [*request["subjects"], *request["evidence"], request["matrix"], request["registry"]]
        paths = {}
        for entry in bound:
            path = entry["path"]
            require(path not in paths or paths[path] == entry["sha256"], "INPUT_INVALID", "conflicting file bindings", 2)
            paths[path] = entry["sha256"]
        extra_controls = STORY_CONTROL_FILES if request["transition"] == "high-risk-nfr-story-done" else ()
        if self.tenant_story:
            import phase_4_story_2_14 as A
            extra_controls = STORY_COMPLETION_FILES + A.CONTROL_FILES
        if request["transition"] == "readiness":
            import phase_4_readiness as R
            extra_controls = R.CONTROL_FILES
        if request["transition"] in ("release-available", "production-promoted"):
            import publication_authority as P
            extra_controls = P.CONTROL_FILES + P.B.SOURCES
        if self.tenant_gate and "tenant_qualification" in request:
            extra_controls = ("tools/phase_4_tenant_gate.py",)
        if self.status_gate:
            import phase_4_status_id_gate as S
            extra_controls = (S.PATH, "tools/phase_4_tenant_gate.py")
        for path in (*CONTROL_FILES, *extra_controls, workflow_path(request["transition"])):
            value = digest(self.local(path))
            require(path not in paths or paths[path] == value, "SOURCE_MISMATCH", "control file mismatch")
            paths[path] = value
        require(len(paths) <= MAX_RESULT_INPUTS + len(extra_controls), "INPUT_INVALID", "binding inventory too large", 2)
        for path, expected in paths.items():
            require(digest(self.local(path)) == expected == digest(self.api.file(self.head, path)),
                    "SOURCE_MISMATCH", "input differs from exact head: " + path)
        if request["transition"] == "high-risk-nfr-story-done":
            import phase_4_story_completion as S
            self.story = S.bind(self)
        registry = parse(self.local(REGISTRY))
        validator = M.Evaluation(self.root, MATRIX, dt.datetime.now(dt.timezone.utc))
        validator.check_registry(registry)
        require(not validator.findings and registry["repository"] == REPOSITORY, "REGISTRY_INVALID", "invalid owner-role registry")
        self.registry = validator.registry
        self.required = self.registry["required_level"]
        require(request["required_level"] == self.required, "LEVEL_MISMATCH", "required level differs from distinct-human roster")
        author = self.registry["identities"].get(request["author"], {})
        require(author.get("kind") == "human", "AUTHOR_INVALID", "author must be a numeric registry human")
        self.author_id = author["github_id"]
        matrix = parse(self.local(MATRIX))
        validator.check_matrix(matrix)
        rows = validator.check_inventory(matrix.get("gates", []), M.prd_gates(self.api.file(self.head, M.PRD).decode()))
        require(not validator.findings, "MATRIX_INVALID", "invalid pinned gate inventory")
        self.matrix_invariants(validator, rows)
        self.rows = rows
        if request["transition"] == "readiness":
            R.bind(self, paths, matrix, validator)
        if request["gate"] is not None:
            row = rows[request["gate"]]
            if request["gate"] == "G-OQ8":
                require(all(row.get(key) == value for key, value in OQ8_BINDINGS.items()),
                        "MATRIX_INVALID", "OQ8 adapter business bindings differ from approved declaration")
            require(row.get("classification") == "high-risk" and request["transition"] in row.get("guarded_transitions", [])
                    and row.get("author") == request["author"], "MATRIX_INVALID", "gate/transition/author differs from matrix")
            require(all(row.get(key) for key in M.BINDINGS), "GATE_UNBOUND", "gate lacks business bindings")
            require(set(row["subject_paths"]) <= {entry["path"] for entry in request["subjects"]},
                    "SUBJECT_MISMATCH", "subjects omit matrix paths")
            if self.tenant_gate:
                require(request["subject_mode"] == "files" and not request["nfr_ids"]
                        and set(row["subject_paths"]) == {s["path"] for s in request["subjects"]},
                        "SUBJECT_MISMATCH", "G-TENANT requires exactly its declared file subjects")
                require("tenant_qualification" in request and not request["predecessors"],
                        "BUSINESS_PREREQUISITE_MISSING", "G-TENANT requires its four qualification lanes")
                import phase_4_tenant_gate as T
                T.check_declaration(row, request["tenant_qualification"], paths)
            if self.status_gate:
                require(request["subject_mode"] == "files" and not request["nfr_ids"]
                        and set(row["subject_paths"]) == {s["path"] for s in request["subjects"]},
                        "SUBJECT_MISMATCH", "G-STATUS-ID requires exactly its declared file subjects")
                require(not request["predecessors"] and not request["evidence"], "INPUT_INVALID",
                        "G-STATUS-ID uses only its fixed CI qualification", 2)
                S.check_declaration(row, request["status_qualification"], paths)
            require(request["author"] in self.registry["roles"].get(row.get("author_role"), []),
                    "AUTHOR_INVALID", "author does not hold the matrix author role")
            self.roles = row["evaluator_roles"]
            require(isinstance(self.roles, list) and self.roles and len(set(self.roles)) == len(self.roles)
                    and all(role in self.registry["roles"] for role in self.roles), "MATRIX_INVALID", "invalid evaluator roles")
        else:
            self.roles = {
                "high-risk-nfr-story-done": ["product-owner", "test-owner"],
                "readiness": ["product-owner"], "release-available": ["release-owner"],
                "production-promoted": ["deployment-owner"], "consumer-removal": ["consumer-owner"],
            }.get(request["transition"], [])
        require(self.roles, "MATRIX_INVALID", "gate-pass requires a gate")
        if request["subject_mode"] == "ad26":
            require({b["path"] for b in request["subjects"]} == {ARCHITECTURE},
                    "SUBJECT_MISMATCH", "AD-26 subject must use the canonical architecture slot")
            self.roles = list(dict.fromkeys([*self.roles, "architecture-owner", "deployment-owner"]))
        self.subject = self.subject_at(self.head)
        self.binding = {"repository": REPOSITORY, "transition": request["transition"], "head_sha": self.head,
                        "request_sha256": digest(canonical(request)), "subject_sha256": self.subject,
                        "workflow_sha256": paths[workflow_path(request["transition"])],
                        "inputs": [{"path": p, "sha256": value} for p, value in sorted(paths.items())]}
        if request["transition"] == "high-risk-nfr-story-done":
            self.binding["story"] = self.story
        self.payloads = [self.approval_payload(role) for role in self.roles]

    def approval_payload(self, role):
        approval_digest = approval_request_digest(self.request)
        if self.request["subject_mode"] == "ad26":
            # Ratification survives architecture edits outside AD-26's exact subject.
            # Its whole-file digest and head describe issuance, not current authority.
            approval_digest = digest(canonical({"repository": REPOSITORY, "transition": self.request["transition"],
                                                "subject_sha256": self.subject, "required_level": self.required}))
        return {"schema": APPROVAL_SCHEMA, "repository": REPOSITORY, "transition": self.request["transition"],
                "head_sha": self.head, "role": role, "subject_sha256": self.subject,
                "request_sha256": approval_digest, "required_level": self.required,
                "traceability_sha256": digest(self.local(ARCHITECTURE)) if self.request["subject_mode"] == "ad26" else None,
                "decision": "approved"}

    def changed_push(self):
        current = self.head
        records = (record for page in self.api.iter_pages("/activity?ref=refs%2Fheads%2Fmain&per_page=100") for record in page)
        previous_time = dt.datetime.max.replace(tzinfo=dt.timezone.utc)
        seen = set()
        for record in records:
            require(record.get("ref") == "refs/heads/main" and record.get("activity_type") in
                    ("push", "force_push", "pr_merge", "merge_queue_merge", "branch_creation", "branch_deletion"),
                    "HISTORY_UNAVAILABLE", "unexpected main activity")
            moment = timestamp(record.get("timestamp"))
            require(moment <= previous_time and moment <= dt.datetime.now(dt.timezone.utc),
                    "HISTORY_UNAVAILABLE", "unordered or future push history")
            previous_time = moment
            after, before = record.get("after"), record.get("before")
            require(sha(after, 40) and sha(before, 40) and after == current and after not in seen,
                    "HISTORY_UNAVAILABLE", "main push history is incomplete")
            seen.add(after)
            require(self.subject_at(after) == self.subject, "HISTORY_UNAVAILABLE", "subject history discontinuity")
            if before == "0" * 40 or self.subject_at(before, allow_absent=True) != self.subject:
                return moment
            current = before
        raise Refusal("HISTORY_UNAVAILABLE", "latest subject-changing push cannot be established")

    def check_approvals(self):
        changed = self.changed_push()
        approvals = self.request["approvals"]
        require(approvals, "APPROVAL_MISSING", "owner must post approval JSON; copy approval_payloads from diagnostics")
        approved_roles = set()
        for reference in approvals:
            role = reference["role"]
            require(role in self.roles, "APPROVAL_ROLE", "approval role not required by transition")
            record = self.api.get("/issues/comments/" + str(reference["comment_id"]))
            require(record.get("id") == reference["comment_id"] and isinstance(record.get("body"), str)
                    and digest(record["body"].encode()) == reference["body_sha256"], "APPROVAL_INVALID", "comment identity/body changed")
            require(record.get("issue_url", "").startswith(self.api.prefix + "/issues/")
                    and re.fullmatch(re.escape(self.api.prefix) + r"/issues/[1-9][0-9]*", record["issue_url"]),
                    "APPROVAL_INVALID", "approval must be an issue/PR conversation comment")
            user = record.get("user", {})
            holders = self.registry["roles"].get(role, [])
            identities = self.registry["identities"]
            matching = [identity for identity in holders if identities[identity]["github_id"] == user.get("id")]
            require(user.get("type") == "User" and positive(user.get("id")) and matching,
                    "APPROVAL_IDENTITY", "comment author is not a numeric human role holder")
            require(record.get("performed_via_github_app") is None, "APPROVAL_IDENTITY", "GitHub App cannot approve")
            created = timestamp(record.get("created_at"))
            require(record.get("updated_at") == record.get("created_at"), "APPROVAL_EDITED", "edited approval is invalid")
            require(created >= changed + dt.timedelta(seconds=M.WINDOW_SECONDS), "APPROVAL_WINDOW", "approval predates 24-hour main-push window")
            require(created <= dt.datetime.now(dt.timezone.utc), "APPROVAL_FUTURE", "approval is in the future")
            try:
                body = parse(record["body"].encode())
            except Refusal as error:
                raise Refusal("APPROVAL_INVALID", "comment body must be exact approval JSON") from error
            expected = self.approval_payload(role)
            if self.request["subject_mode"] == "ad26":
                require(sha(body.get("head_sha"), 40) and sha(body.get("traceability_sha256")),
                        "APPROVAL_INVALID", "AD-26 requires issuance head and traceability digest")
                issued = self.api.file(body["head_sha"], ARCHITECTURE)
                require(digest(issued) == body["traceability_sha256"] and ad26_subject(issued) == self.subject,
                        "APPROVAL_INVALID", "AD-26 issuance traceability or ratification subject differs")
                expected["head_sha"] = body["head_sha"]
                expected["traceability_sha256"] = body["traceability_sha256"]
            require(body == expected, "APPROVAL_INVALID", "approval payload differs from exact subject/request/role/level")
            if self.required == "independent":
                require(user["id"] != self.author_id, "INDEPENDENT_REQUIRED", "author and aliases cannot supply independent approval")
            approved_roles.add(role)
            self.approvals.append({"comment_id": record["id"], "body_sha256": reference["body_sha256"],
                                   "role": role, "github_id": user["id"], "created_at": record["created_at"],
                                   "subject_changed_at": changed.strftime("%Y-%m-%dT%H:%M:%SZ")})
        require(approved_roles == set(self.roles), "APPROVAL_MISSING", "every evaluator role needs a separate comment")
        self.achieved = self.required

    def rerun(self, predecessor):
        require(predecessor["adapter"] == "oq8-governing-design-v1", "ADAPTER_UNAVAILABLE", "no approved fixed predecessor adapter")
        require(all(self.rows["G-OQ8"].get(key) == value for key, value in OQ8_BINDINGS.items()),
                "MATRIX_INVALID", "OQ8 adapter business bindings differ from approved declaration")
        require(predecessor["validator"]["path"] == OQ8_VALIDATOR
                and {item["path"] for item in predecessor["inputs"]} == set(OQ8_INPUTS),
                "PREDECESSOR_IDENTITY", "OQ8 validator/input inventory differs from approved fixed adapter")
        require(M.level_rank(predecessor["assurance_level"]) <= M.level_rank(self.achieved),
                "LEVEL_MISMATCH", "predecessor label exceeds authenticated current approval")
        # The code identity is locally approved, not merely self-declared by its record.
        require(predecessor["validator"]["sha256"] == digest(self.local(OQ8_VALIDATOR)),
                "PREDECESSOR_IDENTITY", "validator differs from approved adapter code")
        with tempfile.TemporaryDirectory(prefix="phase-4-predecessor-") as directory:
            scratch = Path(directory)
            for item in (predecessor["validator"], *predecessor["inputs"]):
                content = self.api.file(predecessor["commit"], item["path"])
                require(digest(content) == item["sha256"], "PREDECESSOR_IDENTITY", "pinned predecessor input/code changed")
                # Bind predecessor content to the current request too; a predecessor may not
                # silently validate different evidence than the guarded attempt.
                current = {b["path"]: b["sha256"] for b in (*self.request["subjects"], *self.request["evidence"])}
                require(current.get(item["path"]) == item["sha256"], "PREDECESSOR_IDENTITY", "predecessor input is not bound by request")
                destination = scratch / item["path"]
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(content)
            content = self.bounded_process([sys.executable, "-I", "-B", str(scratch / OQ8_VALIDATOR),
                                            "--root", str(scratch)], scratch, "validator-output.txt")
            require(digest(content) == predecessor["output_sha256"],
                    "PREDECESSOR_FAILED", "pinned predecessor changed result")
            # The existing matrix pass condition also requires its genuine rejection fixtures.
            self.bounded_process([sys.executable, "-I", "-B", str(scratch / OQ8_FIXTURES)],
                                 scratch, "fixture-output.txt")
        self.predecessors.append({"identity_sha256": digest(canonical(predecessor)), "output_sha256": digest(content),
                                  "assurance_level": predecessor["assurance_level"]})

    @staticmethod
    def bounded_process(argv, scratch, filename, *, timeout_seconds=30, environment_extra=None,
                        failure_code="PREDECESSOR_FAILED", limit_code="PREDECESSOR_LIMIT", accept_failure=False):
        environment = {"PATH": os.defpath, "HOME": str(scratch), "TMPDIR": str(scratch), "TMP": str(scratch),
                       "TEMP": str(scratch), "LANG": "C.UTF-8"}
        environment.update(environment_extra or {})
        require(sys.platform.startswith("linux"), "ADAPTER_UNAVAILABLE", "bounded predecessor requires Linux child reaping")
        libc = ctypes.CDLL(None, use_errno=True)
        previous = ctypes.c_int()
        require(libc.prctl(37, ctypes.byref(previous), 0, 0, 0) == 0
                and libc.prctl(36, 1, 0, 0, 0) == 0,
                "ADAPTER_UNAVAILABLE", "cannot enable descendant reaping")

        def processes():
            result = {}
            for path in Path("/proc").iterdir():
                if path.name.isdigit():
                    try:
                        fields = (path / "stat").read_text().rpartition(")")[2].split()
                        result[int(path.name)] = (int(fields[1]), fields[19])
                    except (OSError, ValueError, IndexError):
                        continue
            return result

        baseline = {pid for pid, (parent, _) in processes().items() if parent == os.getpid()}
        tracked = {}

        def discover(process):
            current = processes()
            parents = {process.pid, *tracked}
            for pid, (parent, started) in current.items():
                if parent == os.getpid() and pid not in baseline:
                    tracked[pid] = started
                    parents.add(pid)
            while True:
                found = {pid for pid, (parent, _) in current.items() if parent in parents} - parents
                if not found:
                    break
                parents.update(found)
            for pid in parents:
                if pid in current:
                    tracked[pid] = current[pid][1]
            return current

        output = scratch / filename
        try:
            with output.open("wb") as stream:
                process = subprocess.Popen(argv, cwd=scratch, env=environment, stdin=subprocess.DEVNULL,
                                           stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
                deadline, next_discovery = time.monotonic() + timeout_seconds, 0
                try:
                    while process.poll() is None:
                        if time.monotonic() > deadline or output.stat().st_size > MAX_BYTES:
                            raise Refusal(limit_code, "evidence exceeded time/output bound")
                        if time.monotonic() >= next_discovery:
                            discover(process)
                            next_discovery = time.monotonic() + 0.2
                        time.sleep(0.02)
                finally:
                    # Subreaping and /proc parentage also catch a descendant that
                    # calls setsid or double-forks away from the original group.
                    for _ in range(20):
                        current = discover(process)
                        try:
                            os.killpg(process.pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                        for pid, started in tracked.items():
                            if pid in current and current[pid][1] == started:
                                try:
                                    os.kill(pid, signal.SIGKILL)
                                except ProcessLookupError:
                                    pass
                        process.wait()
                        for pid in tracked:
                            if pid != process.pid:
                                try:
                                    os.waitpid(pid, os.WNOHANG)
                                except ChildProcessError:
                                    pass
                        remaining = discover(process)
                        if not any(pid in remaining and remaining[pid][1] == started for pid, started in tracked.items()):
                            break
                        time.sleep(0.02)
                    require(not any(pid in remaining and remaining[pid][1] == started for pid, started in tracked.items()),
                            limit_code, "evidence descendants did not terminate")
        finally:
            libc.prctl(36, previous.value, 0, 0, 0)
        require(output.stat().st_size <= MAX_BYTES, limit_code, "evidence exceeded output bound")
        require(process.returncode == 0 or accept_failure and process.returncode == 1, failure_code, "fixed evidence command failed")
        return output.read_bytes()

    def prerequisites(self):
        request = self.request
        for predecessor in request["predecessors"]:
            self.rerun(predecessor)
        transition = request["transition"]
        if self.tenant_gate:
            import phase_4_tenant_gate as T
            T.verify(self)
            self.binding["tenant_qualification"] = request["tenant_qualification"]
        elif self.status_gate:
            import phase_4_status_id_gate as S
            self.binding["status_qualification"] = S.verify(self)
        elif transition == "high-risk-gate-pass":
            require(request["gate"] == "G-OQ8", "ADAPTER_UNAVAILABLE", "gate has no approved business adapter (runtime parity is follow-on)")
            require(len(request["predecessors"]) == 1 and request["predecessors"][0]["adapter"] == "oq8-governing-design-v1",
                    "BUSINESS_PREREQUISITE_MISSING", "G-OQ8 requires genuine governing-design validator success")
        elif transition == "high-risk-nfr-story-done":
            import phase_4_story_completion as S
            adapter = S.adapter_for(self.story)
            require(adapter is not None, "ADAPTER_UNAVAILABLE",
                    "story-owned NFR evidence adapter unavailable: " + ", ".join(self.story["high_risk_nfr_ids"]))
            adapter.evaluate(self)
        elif transition == "readiness":
            import phase_4_readiness as R
            R.verify_gates(self)
        elif transition in ("release-available", "production-promoted"):
            import publication_authority as P
            P.evaluate_transition(self)
        else:
            raise Refusal("BUSINESS_PREREQUISITE_MISSING", "Story 3.20 consumer-removal adapter remains absent")
        levels = [self.achieved, *(entry["assurance_level"] for entry in self.predecessors)]
        self.minimum = min(levels, key=M.LEVELS.index)
        require(M.LEVELS.index(self.minimum) >= M.LEVELS.index(self.required), "LEVEL_MISMATCH", "downstream minimum below required assurance")

    def evaluate(self):
        self.prepare()
        self.check_approvals()
        self.prerequisites()
        self.current_head()
        return self.result("evaluate", [])

    def result(self, phase, codes, seal=None):
        result = {"schema": RESULT_SCHEMA, "phase": phase, "result": "FAIL" if codes else "PASS", "codes": codes,
                "binding": self.binding, "required_level": self.required, "achieved_level": self.achieved,
                "downstream_minimum": self.minimum, "approvals": self.approvals,
                "approval_payloads": self.payloads, "predecessors": self.predecessors, "seal": seal}
        if self.request["transition"] == "high-risk-nfr-story-done":
            result["story_evidence"] = self.story_evidence
        return result

    def authenticate_run(self, run_id, attempt):
        require(str(run_id) != os.environ.get("GITHUB_RUN_ID"), "SELF_SEAL_FORBIDDEN", "a running job cannot seal itself")
        run = self.api.get(f"/actions/runs/{run_id}/attempts/{attempt}")
        require(run.get("id") == run_id and run.get("run_attempt") == attempt and run.get("event") == "workflow_dispatch"
                and run.get("head_sha") == self.head and run.get("head_branch") == "main"
                and run.get("repository", {}).get("full_name") == REPOSITORY
                and run.get("head_repository", {}).get("full_name") == REPOSITORY,
                "RUN_INVALID", "run is not this repository/transition/head/attempt")
        require(run.get("status") == "completed" and run.get("conclusion") == "success",
                "RUN_NOT_SUCCESSFUL", "only completed successful attempts can seal")
        workflow = workflow_path(self.request["transition"])
        require(run.get("path") == workflow and positive(run.get("workflow_id")), "WORKFLOW_MISMATCH", "wrong dedicated workflow")
        metadata = self.api.get("/actions/workflows/" + str(run["workflow_id"]))
        require(metadata.get("id") == run["workflow_id"] and metadata.get("path") == workflow and metadata.get("state") == "active",
                "WORKFLOW_MISMATCH", "workflow identity is not the dedicated transition workflow")
        require(positive(run.get("check_suite_id")), "CI_UNAUTHENTICATED", "missing Actions check suite")
        suite = self.api.get("/check-suites/" + str(run["check_suite_id"]))
        app = suite.get("app", {})
        require(suite.get("id") == run["check_suite_id"] and suite.get("head_sha") == self.head
                and suite.get("status") == "completed" and suite.get("conclusion") == "success"
                and app.get("id") == ACTIONS_APP_ID and app.get("slug") == "github-actions",
                "CI_UNAUTHENTICATED", "check suite lacks authenticated successful GitHub Actions identity")
        jobs = self.api.pages(f"/actions/runs/{run_id}/attempts/{attempt}/jobs?per_page=100", "jobs")
        require(len(jobs) == 1 and positive(jobs[0].get("id")) and jobs[0].get("name") == "Phase 4 transition attempt"
                and jobs[0].get("run_id") == run_id and jobs[0].get("head_sha") == self.head
                and jobs[0].get("run_attempt", attempt) == attempt
                and jobs[0].get("status") == "completed" and jobs[0].get("conclusion") == "success",
                "CI_UNAUTHENTICATED", "missing successful attempt job")
        checks = self.api.pages(f"/check-suites/{suite['id']}/check-runs?per_page=100", "check_runs")
        require(any(positive(check.get("id")) and check.get("app", {}).get("id") == ACTIONS_APP_ID
                    and check.get("check_suite", {}).get("id") == suite["id"]
                    and check.get("head_sha") == self.head
                    and check.get("status") == "completed" and check.get("conclusion") == "success"
                    and check.get("url") == self.api.prefix + f"/check-runs/{check['id']}" == jobs[0].get("check_run_url")
                    for check in checks),
                "CI_UNAUTHENTICATED", "attempt job is not linked to the Actions check suite")
        return run

    def artifact(self, run_id, attempt):
        name = f"phase-4-transition-result-{run_id}-{attempt}"
        artifacts = self.api.pages(f"/actions/runs/{run_id}/artifacts?per_page=100", "artifacts")
        matches = [item for item in artifacts if item.get("name") == name]
        require(len(matches) == 1, "ARTIFACT_INVALID", "missing/ambiguous attempt artifact")
        artifact = matches[0]
        require(positive(artifact.get("id")) and artifact.get("expired") is False
                and type(artifact.get("size_in_bytes")) is int and 0 < artifact["size_in_bytes"] <= MAX_ARCHIVE
                and artifact.get("workflow_run", {}).get("id") == run_id
                and artifact.get("workflow_run", {}).get("head_sha") == self.head,
                "ARTIFACT_INVALID", "artifact identity/expiry/size mismatch")
        content, _ = self.api.fetch(self.api.prefix + f"/actions/artifacts/{artifact['id']}/zip", binary=True)
        require(artifact.get("digest") == "sha256:" + digest(content), "ARTIFACT_INVALID", "artifact archive digest mismatch")
        try:
            with zipfile.ZipFile(io.BytesIO(content)) as archive:
                entries = archive.infolist()
                require(1 <= len(entries) <= 10 and len({e.filename for e in entries}) == len(entries), "INPUT_INVALID", "ambiguous artifact archive", 2)
                total = 0
                for entry in entries:
                    total += entry.file_size
                    require(M.repository_path(entry.filename) is not None and not entry.is_dir()
                            and (entry.external_attr >> 16) & 0o170000 not in (0o120000, 0o040000)
                            and not entry.flag_bits & 1 and total <= MAX_ARCHIVE,
                            "INPUT_INVALID", "unsafe artifact archive", 2)
                require(sum(e.filename == "result.json" for e in entries) == 1,
                        "ARTIFACT_INVALID", "artifact has no unique result.json")
                require(archive.getinfo("result.json").file_size <= MAX_BYTES,
                        "INPUT_INVALID", "oversized result", 2)
                return parse_transition_document(archive.read("result.json")), artifact["id"]
        except (zipfile.BadZipFile, RuntimeError, NotImplementedError, OSError, zlib.error) as error:
            raise Refusal("INPUT_INVALID", "malformed artifact archive", 2) from error

    def reconfirm_candidate(self, run_id, attempt, candidate, artifact_id):
        current, current_id = self.artifact(run_id, attempt)
        require(current_id == artifact_id and canonical(current) == canonical(candidate),
                "RESULT_MISMATCH", "sealed gate result changed during consumption")

    def consume(self, run_id, attempt):
        readiness = self.request["transition"] == "readiness"
        if readiness:
            import phase_4_readiness as R
        if readiness or self.status_gate or self.tenant_story:
            self.current_attempt(run_id, attempt)
        self.authenticate_run(run_id, attempt)
        candidate, artifact_id = self.artifact(run_id, attempt)
        keys = RESULT_KEYS | ({"story_evidence"} if self.request["transition"] == "high-risk-nfr-story-done" else set())
        require(set(candidate) == keys and candidate.get("phase") == "evaluate"
                and candidate.get("result") == "PASS" and candidate.get("seal") is None,
                "RESULT_MISMATCH", "artifact is not a passing candidate evaluation")
        expected = self.evaluate()
        if canonical(candidate) != canonical(expected):
            if readiness:
                R.diagnose_candidate(candidate, expected)
            raise Refusal("RESULT_MISMATCH", "completed result differs from current bound inputs/approvals/predecessors")
        if readiness:
            R.reconfirm(self)
        if self.status_gate:
            import phase_4_status_id_gate as S
            self.reconfirm_candidate(run_id, attempt, expected, artifact_id)
            self.reconfirm_approvals()
            S.reconfirm(self)
        if self.tenant_story:
            import phase_4_story_2_14 as A
            try:
                self.reconfirm_candidate(run_id, attempt, expected, artifact_id)
                self.reconfirm_approvals()
                A.reconfirm(self.tenant_story_verifier, self.story_evidence["seal"])
            except Refusal:
                self.story_evidence = None
                raise
        if readiness or self.status_gate or self.tenant_story:
            self.current_attempt(run_id, attempt)
        self.current_head()
        seal = {"run_id": run_id, "attempt": attempt, "artifact_id": artifact_id, "ci_app_id": ACTIONS_APP_ID}
        result = self.result("consume", [], seal)
        if readiness:
            result["readiness_decision"] = R.decide(self, expected, seal)
            result["downstream_minimum"] = result["readiness_decision"]["records"][-1]["assurance_level"]
        return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=("evaluate", "consume"))
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--request", type=Path)
    source.add_argument("--request-env", action="store_true", help="read PHASE4_REQUEST_JSON without shell interpolation")
    parser.add_argument("--expected-transition", choices=M.TRANSITIONS)
    parser.add_argument("--run-id")
    parser.add_argument("--attempt")
    parser.add_argument("--result", type=Path, required=True)
    args = parser.parse_args(argv)
    control = None
    readiness = args.expected_transition == "readiness"
    try:
        if args.request_env:
            content = os.environ.get("PHASE4_REQUEST_JSON", "").encode()
        else:
            require(not args.request.is_symlink(), "INPUT_INVALID", "request cannot be a symbolic link", 2)
            with args.request.open("rb") as stream:
                content = stream.read(MAX_BYTES + 1)
        require(len(content) <= MAX_BYTES, "INPUT_INVALID", "request too large", 2)
        request = parse_transition_document(content)
        readiness = readiness or request.get("transition") == "readiness"
        request = validate_request(request)
        require(args.expected_transition is None or args.expected_transition == request["transition"],
                "INPUT_INVALID", "request differs from dedicated transition", 2)
        control = Control(args.root.resolve(), request, GitHub())
        if args.phase == "consume":
            require(all(isinstance(value, str) and re.fullmatch(r"[1-9][0-9]*", value)
                        for value in (args.run_id, args.attempt)), "INPUT_INVALID", "consume requires positive run-id and attempt", 2)
            result = control.consume(int(args.run_id), int(args.attempt))
        else:
            require(args.run_id is None and args.attempt is None, "INPUT_INVALID", "evaluation cannot supply seal coordinates", 2)
            result = control.evaluate()
        code = 0
    except (Refusal, OSError, UnicodeError, ValueError, KeyError, TypeError, AttributeError, RecursionError) as error:
        if not isinstance(error, Refusal):
            error = Refusal("INPUT_INVALID", "malformed or unreadable input: " + type(error).__name__, 2)
        print(error.code + ": " + str(error), file=sys.stderr)
        result = control.result(args.phase, [error.code]) if control else {
            "schema": RESULT_SCHEMA, "phase": args.phase, "result": "FAIL", "codes": [error.code], "binding": {},
            "required_level": "none", "achieved_level": "none", "downstream_minimum": "none", "approvals": [],
            "approval_payloads": [], "predecessors": [], "seal": None}
        if readiness or getattr(error, "gate", None) is not None:
            import phase_4_readiness as R
            result.update(achieved_level="none", downstream_minimum="none", readiness_decision=R.reject(error))
        code = error.exit_code
    try:
        args.result.parent.mkdir(parents=True, exist_ok=True)
        args.result.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
    except OSError:
        print("INPUT_INVALID: cannot write diagnostics", file=sys.stderr)
        return 2
    print(json.dumps(result, sort_keys=True))
    return code
