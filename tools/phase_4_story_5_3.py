"""Fixed Story 5.3 evidence adapter; diagnostic execution never grants assurance.

Membership comes from the authenticated main tree. Git blob identities bind local
bytes without thousands of mutable Contents API calls. The complete inventory is
hashed in the result; only its private scratch copy contains the expanded list.
"""

import configparser
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile
import zlib

import phase_4_transition as C

ADAPTER = "story-5-3-auth-secrets-v1"
VALIDATOR = "tools/validate-story-5-3-nfr.py"
MODULE = "tools/phase_4_story_5_3.py"
ORIGINAL_SPEC = "_bmad-output/implementation-artifacts/spec-5-3-production-authentication-guards-and-secret-stripping.md"
STORY_IDENTITY = "5-3-production-authentication-guards-and-secret-stripping"
SCHEMA = "hexalith.eventstore.story-5-3-evidence.v1"
INVENTORY = ".story-5-3-inputs.json"
AUTH_OWNER_ENV = "HEXALITH_PRIVATE_AUTH_OWNER"
AUTH_TEMP_ENV = "HEXALITH_PRIVATE_AUTH_TEMP"
AUTH_MARKER_PREFIX = ".story-5-3-authentication-started-"
SUBMODULES = ("references/Hexalith.Builds", "references/Hexalith.Tenants", "references/Hexalith.FrontComposer")
PACT = "tests/Hexalith.FrontComposer.Shell.Tests/Pact/frontcomposer-eventstore-command-dispatch.json"
BUILDS_HISTORY = ("30ea43a56330a9828ed0bb2e837289231d8630d1", "22a578b576a515d2af214fe81859447fffc97981")
MAX_FILES = 25000
MAX_SOURCE_BYTES = 512 * C.MAX_BYTES
MAX_FILE_BYTES = 32 * C.MAX_BYTES
MAX_INVENTORY_BYTES = 32 * C.MAX_BYTES
CONTROL_FILES = (MODULE, VALIDATOR, "tools/tests/test_phase_4_story_5_3.py", "tools/tests/check_phase_4_story_5_3_schema.py")

# Each selector must discover and execute positive evidence. None is inferred
# from whichever tests happen to be green. The complete Admin host lane is run.
LANES = (
    ("gateway-auth-probes-secrets", "Hexalith.EventStore.Server.Tests", (
        "Authentication.EventStoreAuthenticationOptionsTests",
        "Authentication.ConfigureJwtBearerOptionsTests",
        "Authentication.ProductionAuthorityAuthenticationTests",
        "HealthChecks.DefaultHealthEndpointResponseWriterTests",
        "Security.SecretsProtectionTests")),
    ("admin-host-regression", "Hexalith.EventStore.Admin.Server.Host.Tests", ()),
    ("sample-api-ui", "Hexalith.EventStore.Sample.Tests", (
        "HttpHostSecurityTests", "SampleApi.SampleApiHostMiddlewareTests", "BlazorUI.EventStoreApiAccessTokenProviderTests")),
    ("apphost-credentials-model", "Hexalith.EventStore.AppHost.Tests", (
        "Configuration.AppHostAuthenticationModelTests", "Configuration.HexalithEventStoreJwtAuthenticationTests",
        "Configuration.LocalAuthenticationCredentialsTests", "Configuration.KeycloakRealmTemplateTests")),
    ("admin-ui-token-channels", "Hexalith.EventStore.Admin.UI.Tests", (
        "Services.AdminApiAccessTokenProviderRoleTests", "Services.AdminApiAuthorizationHandlerTests")),
    ("integration-auth", "Hexalith.EventStore.IntegrationTests", (
        "EventStore.JwtAuthenticationIntegrationTests", "ContractTests.KeycloakAuthenticationTests",
        "Security.KeycloakE2ESecurityTests", "Security.KeycloakE2ESmokeTests")),
    ("load-token", "Hexalith.EventStore.LoadTests.Tests", ("LoadTestJwtTokenGeneratorTests",)),
    ("packaging-auth-contract", "Hexalith.EventStore.Contracts.Tests", ("Packaging.PublishedProductionAuthenticationTests",)),
    ("provider-ephemeral-token", "Hexalith.EventStore.ProviderVerification.Tests", ("RealKestrelPactTests",)),
)
CAPABILITIES = ("positive-assignment", "yaml-credential-sequence", "qualified-network-credential",
                "qualified-client-secret-credential", "xml-cdata", "space-separated-cli-secret", "private-octet-jwk")
PLAN = {"lanes": LANES, "capabilities": CAPABILITIES, "configuration": "Release", "build": "solution-and-projects",
        "script": "scripts/tests/generated-api-smoke-preflight.test.sh", "consumer_sources": SUBMODULES}


def supported(claim):
    """Only a canonical, numerically identified new Story 5.3 slice is supported."""
    return (claim["story_key"] == STORY_IDENTITY and claim["story_id"] == "5.3"
            and re.fullmatch(r"_bmad-output/implementation-artifacts/spec-5-3-[a-z0-9-]+\.md", claim["spec_path"]) is not None
            and claim["nfr_ids"] == ["NFR3", "NFR4"] and claim["high_risk_nfr_ids"] == ["NFR3", "NFR4"])


def subject_at(api, commit, allow_absent=False):
    try:
        document = api.get("/git/commits/" + commit)
    except C.Refusal as error:
        if allow_absent:
            raise C.Refusal("HISTORY_UNAVAILABLE", "previous root tree cannot be authenticated") from error
        raise
    tree = document.get("tree", {}).get("sha")
    C.require(document.get("sha") == commit and C.sha(tree, 40), "API_INVALID", "subject lacks authenticated root tree")
    return C.digest(C.canonical({"adapter": ADAPTER, "root_tree": tree}))


def git(root, *arguments, content=None, limit=MAX_INVENTORY_BYTES):
    """Read repository metadata only; no checkout, index or dependency mutation."""
    process = subprocess.run(["git", "--no-replace-objects", "-c", "core.attributesFile=/dev/null",
                             "-c", "core.fsmonitor=false", "-C", str(root), *arguments],
                             input=content, capture_output=True, timeout=60,
                             env={"PATH": os.defpath, "LANG": "C.UTF-8", "GIT_CONFIG_NOSYSTEM": "1",
                                  "GIT_CONFIG_GLOBAL": "/dev/null", "GIT_NO_REPLACE_OBJECTS": "1"})
    C.require(process.returncode == 0, "SOURCE_MISMATCH", "cannot read adapter Git authority")
    C.require(len(process.stdout) <= limit, "INPUT_INVALID", "oversized Git inventory", 2)
    return process.stdout


def object_sha(kind, value):
    return hashlib.sha1(kind.encode() + b" " + str(len(value)).encode() + b"\0" + value).hexdigest()


def verified_object(root, kind, oid):
    C.require(C.sha(oid, 40), "SOURCE_MISMATCH", "invalid Git object identity")
    try:
        size = int(git(root, "cat-file", "-s", oid).decode().strip())
        C.require(0 <= size <= MAX_FILE_BYTES, "SOURCE_MISMATCH", "required Git object size bound")
        value = git(root, "cat-file", kind, oid, limit=MAX_FILE_BYTES)
    except C.Refusal as error:
        raise C.Refusal("SOURCE_MISMATCH", "required Git " + kind + " unavailable: " + oid) from error
    C.require(object_sha(kind, value) == oid, "SOURCE_MISMATCH", "Git object content differs from required identity")
    return value


def store_object(root, kind, value):
    oid = object_sha(kind, value)
    path = root / ".git/objects" / oid[:2] / oid[2:]
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(zlib.compress(kind.encode() + b" " + str(len(value)).encode() + b"\0" + value))
    return oid


def text_attributes(root, entries, exact):
    """Ask Git only about bound attributes in a private database; never clean.

    No local info/attributes, global attributes, filters or external commands can
    influence canonicalization. Execution bytes retain a separate SHA-256.
    """
    attributes = [row for row in entries if Path(row["path"]).name == ".gitattributes"]
    if not attributes:
        return {}
    with tempfile.TemporaryDirectory(prefix="story-5-3-attributes-") as directory:
        private = Path(directory)
        index = [dict(row) for row in entries]
        for row in attributes:
            value = verified_object(root, "blob", row["git_sha"]) if exact else read_source(root, row)
            matching = next(item for item in index if item["path"] == row["path"])
            matching["git_sha"] = store_object(private, "blob", value)
        write_index(private, index)
        raw = git(private, "check-attr", "--cached", "-z", "--stdin", "text", "eol",
                  content=b"".join(row["path"].encode() + b"\0" for row in entries))
    fields = raw.decode("utf-8").split("\0")
    result = {}
    for offset in range(0, len(fields) - 1, 3):
        path, name, value = fields[offset:offset + 3]
        result.setdefault(path, {})[name] = value
    return result


def index_entries(root):
    entries = []
    for row in git(root, "ls-files", "--stage", "-z").split(b"\0"):
        if not row:
            continue
        metadata, name = row.split(b"\t", 1)
        mode, oid, stage = metadata.decode("ascii").split()
        C.require(stage == "0", "SOURCE_MISMATCH", "unmerged adapter inventory")
        entries.append({"path": name.decode("utf-8"), "mode": mode, "git_sha": oid})
    return validate_entries(entries)


def validate_entries(entries):
    C.require(isinstance(entries, list) and 0 < len(entries) <= MAX_FILES, "INPUT_INVALID", "invalid source inventory", 2)
    names = set()
    for entry in entries:
        C.closed(entry, ("path", "mode", "git_sha"))
        C.require(C.M.repository_path(entry["path"]) and entry["path"] not in names
                  and entry["mode"] in ("100644", "100755", "120000", "160000") and C.sha(entry["git_sha"], 40),
                  "INPUT_INVALID", "invalid or duplicate source member", 2)
        names.add(entry["path"])
    return sorted(entries, key=lambda item: item["path"])


def read_source(root, entry):
    path = root
    parts = entry["path"].split("/")
    for component in parts[:-1]:
        path /= component
        C.require(not path.is_symlink(), "SOURCE_MISMATCH", "symbolic source parent")
    path /= parts[-1]
    if entry["mode"] == "120000":
        C.require(path.is_symlink() and not Path(os.readlink(path)).is_absolute()
                  and path.resolve().is_relative_to(root.resolve()),
                  "SOURCE_MISMATCH", "unsafe tracked symbolic link")
        value = os.readlink(path).encode("utf-8")
    else:
        C.require(not path.is_symlink() and stat.S_ISREG(path.stat().st_mode), "SOURCE_MISMATCH", "source must be a regular file")
        with path.open("rb") as stream:
            value = stream.read(MAX_FILE_BYTES + 1)
    C.require(len(value) <= MAX_FILE_BYTES, "INPUT_INVALID", "source exceeds size bound", 2)
    return value


def blob_sha(value):
    return hashlib.sha1(b"blob " + str(len(value)).encode() + b"\0" + value).hexdigest()


def bind_entries(root, entries, exact=True):
    result, total = [], 0
    names = {entry["path"] for entry in entries if entry["mode"] != "160000"}
    attributes = text_attributes(root, entries, exact)
    for entry in entries:
        item = dict(entry)
        if item["mode"] != "160000":
            value = read_source(root, item)
            if item["mode"] == "120000":
                C.require((root / item["path"]).resolve().relative_to(root.resolve()).as_posix() in names,
                          "SOURCE_MISMATCH", "symbolic target omitted from source inventory")
            total += len(value)
            C.require(total <= MAX_SOURCE_BYTES, "INPUT_INVALID", "source inventory exceeds size bound", 2)
            rules = attributes.get(item["path"], {})
            normalize = item["mode"] != "120000" and (rules.get("text") == "set"
                or rules.get("text") == "auto" and b"\0" not in value[:8000]
                or rules.get("eol") in ("lf", "crlf") and rules.get("text") != "unset")
            canonical = value if blob_sha(value) == item["git_sha"] else value.replace(b"\r\n", b"\n") if normalize else value
            C.require(not exact or blob_sha(canonical) == item["git_sha"], "SOURCE_MISMATCH", "source differs from bound Git blob: " + item["path"])
            item.update(git_sha=blob_sha(canonical), sha256=C.digest(value))
        else:
            item["sha256"] = None
        result.append(item)
    return result


def submodule_entries(root, entries, exact):
    config = configparser.ConfigParser(interpolation=None)
    config.read_string((root / ".gitmodules").read_text(encoding="utf-8"))
    declarations = {config.get(section, "path"): config.get(section, "url") for section in config.sections()}
    result = []
    for path in SUBMODULES:
        repo = path.split("/")[-1]
        C.require(declarations.get(path) == "https://github.com/Hexalith/" + repo + ".git",
                  "SOURCE_MISMATCH", "required input is not a root-declared submodule: " + path)
        links = [item for item in entries if item["path"] == path and item["mode"] == "160000"]
        C.require(len(links) == 1, "SOURCE_MISMATCH", "missing required root gitlink: " + path)
        commit = links[0]["git_sha"]
        commit_value = verified_object(root / path, "commit", commit)
        tree = re.match(rb"tree ([0-9a-f]{40})\n", commit_value)
        C.require(tree is not None, "SOURCE_MISMATCH", "required commit lacks tree")
        verified_object(root / path, "tree", tree[1].decode())
        members = []
        for row in git(root / path, "ls-tree", "-r", "-z", commit).split(b"\0"):
            if row:
                metadata, name = row.split(b"\t", 1)
                mode, kind, oid = metadata.decode("ascii").split()
                C.require(kind in ("blob", "commit"), "SOURCE_MISMATCH", "unsupported submodule input")
                members.append({"path": name.decode("utf-8"), "mode": mode, "git_sha": oid})
        members = validate_entries(members)
        C.require(index_entries(root / path) == members, "SOURCE_MISMATCH", "submodule membership differs from pinned gitlink: " + path)
        if path.endswith("Hexalith.FrontComposer"):
            members = [row for row in members if row["path"] == PACT or Path(row["path"]).name == ".gitattributes"]
            C.require(any(row["path"] == PACT for row in members), "SOURCE_MISMATCH", "required pinned FrontComposer Pact input missing")
        result.append({"path": path, "commit": commit, "entries": bind_entries(root / path, members, exact)})
    return result


def read_objects(root, oids):
    """Verify the content address of every selected object, without replacements."""
    oids = sorted(set(oids))
    C.require(len(oids) <= 40000 and all(C.sha(oid, 40) for oid in oids), "SOURCE_MISMATCH", "Git metadata inventory limit")
    request = "".join(oid + "\n" for oid in oids).encode()
    headers = git(root, "cat-file", "--batch-check", content=request).decode().splitlines()
    C.require(len(headers) == len(oids), "SOURCE_MISMATCH", "incomplete Git object inventory")
    total = 0
    for oid, line in zip(oids, headers):
        fields = line.split()
        C.require(len(fields) == 3 and fields[0] == oid and fields[1] in ("commit", "tree", "blob"),
                  "SOURCE_MISMATCH", "required Git object unavailable")
        size = int(fields[2])
        total += size
        C.require(0 <= size <= MAX_FILE_BYTES and total <= MAX_SOURCE_BYTES,
                  "SOURCE_MISMATCH", "Git object byte bound")
    raw = git(root, "cat-file", "--batch", content=request,
              limit=MAX_SOURCE_BYTES + MAX_INVENTORY_BYTES)
    offset, result = 0, []
    for oid in oids:
        end = raw.find(b"\n", offset)
        header = raw[offset:end].decode("ascii").split()
        C.require(len(header) == 3 and header[0] == oid and header[1] in ("commit", "tree", "blob"),
                  "SOURCE_MISMATCH", "required Git object unavailable")
        size = int(header[2])
        C.require(0 <= size <= MAX_FILE_BYTES, "SOURCE_MISMATCH", "Git object size bound")
        value = raw[end + 1:end + 1 + size]
        C.require(len(value) == size and object_sha(header[1], value) == oid, "SOURCE_MISMATCH", "required Git object hash mismatch")
        result.append({"oid": oid, "kind": header[1], "size": size, "sha256": C.digest(value)})
        offset = end + size + 2
    C.require(offset == len(raw), "SOURCE_MISMATCH", "ambiguous Git object data")
    return result


def metadata_inventory(root, head, submodules, tree=None):
    """Only exact current trees and the publisher's existing pinned ancestry."""
    result = []
    repositories = [("", head), *((row["path"], row["commit"]) for row in submodules)]
    for path, revision in repositories:
        repository = root / path
        value = verified_object(repository, "commit", revision)
        found = re.match(rb"tree ([0-9a-f]{40})\n", value)
        C.require(found is not None and (path or tree is None or found[1].decode() == tree),
                  "SOURCE_MISMATCH", "commit tree differs from authenticated identity")
        revisions = [revision, *(BUILDS_HISTORY if path.endswith("Hexalith.Builds") else ())]
        oids = set(revisions)
        if path.endswith("Hexalith.Builds"):
            ancestors = git(repository, "rev-list", "--max-count=5001", *revisions).decode().splitlines()
            C.require(len(ancestors) <= 5000, "SOURCE_MISMATCH", "publisher history exceeds bound")
            oids.update(ancestors)
        for selected in revisions:
            commit = verified_object(repository, "commit", selected)
            selected_tree = re.match(rb"tree ([0-9a-f]{40})\n", commit)
            C.require(selected_tree is not None, "SOURCE_MISMATCH", "required publisher commit tree unavailable")
            oids.add(selected_tree[1].decode())
            for row in git(repository, "ls-tree", "-r", "-t", "-z", selected).split(b"\0"):
                if row:
                    metadata, name = row.split(b"\t", 1)
                    _, kind, oid = metadata.decode().split()
                    if kind != "commit" and (not path.endswith("Hexalith.FrontComposer")
                        or kind == "tree" or name.decode() == PACT or Path(name.decode()).name == ".gitattributes"):
                        oids.add(oid)
        origin = "https://github.com/Hexalith/" + (path.split("/")[-1] if path else "Hexalith.EventStore") + ".git"
        result.append({"path": path, "head": revision, "origin": origin, "objects": read_objects(repository, oids)})
    return result


def file_digest(path):
    C.require(path.is_file() and not path.is_symlink(), "SOURCE_MISMATCH", "required runtime/dependency file is not regular")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(C.MAX_BYTES), b""):
            digest.update(block)
    return digest.hexdigest()


def runtime_digest(path):
    """Bind the bytes behind installed SDK aliases and recheck their target."""
    return file_digest(path.resolve(strict=True))


def runtime_inventory(root):
    """Bind existing tools; copy only token-free Dapr inputs into private HOME."""
    allowed = [Path.home() / ".dotnet", Path("/usr/local/bin"), Path("/usr/bin"), Path("/bin"), Path.home() / ".dapr/bin"]
    selected = shutil.which("dotnet", path=os.pathsep.join(map(str, allowed)))
    C.require(selected is not None, "STORY_EVIDENCE_FAILED", "prerequisite:dotnet-sdk-missing")
    dotnet = Path(selected).resolve()
    entries = []
    def bind(path, name, destination=None):
        C.require(path.stat().st_size <= 512 * C.MAX_BYTES, "STORY_EVIDENCE_LIMIT", "runtime input too large")
        entries.append({"name": name, "source": str(path), "destination": destination,
                        "sha256": runtime_digest(path), "size": path.stat().st_size})
    bind(dotnet, "dotnet")
    version = json.loads((root / "global.json").read_text())["sdk"]["version"]
    sdk = dotnet.parent / "sdk" / version
    C.require(sdk.is_dir(), "STORY_EVIDENCE_FAILED", "prerequisite:pinned-dotnet-sdk-missing")
    for directory in (sdk, dotnet.parent / "packs", dotnet.parent / "shared"):
        for path in sorted(directory.rglob("*")):
            if path.is_file():
                bind(path, "dotnet/" + path.relative_to(dotnet.parent).as_posix())
    dapr = shutil.which("dapr", path=os.pathsep.join(map(str, allowed)))
    if dapr is not None:
        bind(Path(dapr).resolve(), "dapr-cli", ".story-5-3-tools/dapr")
    home = Path.home() / ".dapr"
    runtime = home / "bin/daprd"
    if runtime.is_file():
        bind(runtime, "dapr-runtime", ".dapr/bin/daprd")
    # Ambient Dapr configuration, components and certificates may contain
    # operational credentials. Only source-owned component configuration is used.
    C.require(len(entries) <= 30000, "STORY_EVIDENCE_LIMIT", "runtime inventory exceeds bound")
    return entries


def diagnostic_entries(root, entries=None):
    """Use the same tracked and untracked path/mode denominator at capture and recheck."""
    entries = [dict(entry) for entry in (index_entries(root) if entries is None else entries)]
    names = {entry["path"] for entry in entries}
    for raw in git(root, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0"):
        if raw:
            path = raw.decode("utf-8")
            source = root / path
            mode = "120000" if source.is_symlink() else ("100755" if source.stat().st_mode & 0o111 else "100644")
            entries.append({"path": path, "mode": mode, "git_sha": "0" * 40})
            names.add(path)
    for path in (*CONTROL_FILES, *C.STORY_CONTROL_FILES):
        if path not in names:
            entries.append({"path": path, "mode": "100644", "git_sha": "0" * 40})
            names.add(path)
    return validate_entries(entries)


def inventory(root, api=None, head=None):
    """Derive membership independently; never accept an inventory from a caller."""
    entries = index_entries(root)
    tree = None
    if api is not None:
        commit = api.get("/git/commits/" + head)
        tree = commit.get("tree", {}).get("sha")
        C.require(commit.get("sha") == head and C.sha(tree, 40), "API_INVALID", "commit lacks authenticated tree identity")
        document = api.get("/git/trees/" + tree + "?recursive=1")
        C.require(document.get("sha") == tree and document.get("truncated") is False
                  and isinstance(document.get("tree"), list), "PAGINATION_INCOMPLETE", "complete source tree required")
        remote = []
        for item in document["tree"]:
            C.require(isinstance(item, dict) and item.get("type") in ("tree", "blob", "commit"), "API_INVALID", "invalid tree member")
            if item["type"] != "tree":
                remote.append({"path": item.get("path"), "mode": item.get("mode"), "git_sha": item.get("sha")})
        C.require(entries == validate_entries(remote), "SOURCE_MISMATCH", "tracked membership differs from authenticated head")
    else:
        # Local diagnostics bind new adapter code too. This path has no approval
        # or seal authority, and is never used by shared evaluation/consumption.
        entries = diagnostic_entries(root, entries)
    names = {entry["path"] for entry in entries}
    C.require({*CONTROL_FILES, ORIGINAL_SPEC, "Hexalith.EventStore.slnx", "global.json", "Directory.Build.props",
               "Directory.Build.targets", "Directory.Packages.props", *C.CONTROL_FILES, *C.STORY_CONTROL_FILES} <= names,
              "SOURCE_MISMATCH", "adapter/build/coverage code omitted from source inventory")
    submodules = submodule_entries(root, entries, api is not None)
    head = head or git(root, "rev-parse", "HEAD").decode().strip()
    return {"adapter": ADAPTER, "root_tree": tree, "entries": bind_entries(root, entries, api is not None),
            "submodules": submodules, "metadata": metadata_inventory(root, head, submodules, tree)}


def summary(manifest):
    members = lambda entries: [{key: row[key] for key in ("path", "mode", "git_sha")} for row in entries]
    validator = next(row for row in manifest["entries"] if row["path"] == VALIDATOR)
    normalized = {**manifest, "runtime": [{key: value for key, value in row.items() if key != "source"}
                                          for row in manifest.get("runtime", [])]}
    return {"adapter": ADAPTER, "inventory_sha256": C.digest(C.canonical(normalized)), "root_tree": manifest["root_tree"],
            "root_membership_sha256": C.digest(C.canonical(members(manifest["entries"]))),
            "submodule_membership_sha256": C.digest(C.canonical([
                {"path": item["path"], "commit": item["commit"], "entries": members(item["entries"])}
                for item in manifest["submodules"]])),
            "source_count": sum(row["mode"] != "160000" for row in manifest["entries"])
                            + sum(sum(row["mode"] != "160000" for row in item["entries"]) for item in manifest["submodules"]),
            "validator_sha256": validator["sha256"], "plan_sha256": C.digest(C.canonical(PLAN))}


def validate_report(report, binding):
    """The fixed validator's PASS is a closed, complete evidence contract."""
    C.closed(report, ("schema", "adapter", "result", "binding", "lanes", "capabilities", "gaps", "notes", "diagnostics", "execution_inputs"))
    C.require(report["schema"] == SCHEMA and report["adapter"] == ADAPTER and report["binding"] == binding
              and report["result"] in ("PASS", "FAIL"), "STORY_EVIDENCE_FAILED", "validator returned unbound evidence")
    C.require(isinstance(report["gaps"], list) and len(report["gaps"]) <= 100
              and all(isinstance(item, str) for item in report["gaps"])
              and len(set(report["gaps"])) == len(report["gaps"])
              and isinstance(report["notes"], list) and len(report["notes"]) <= 10
              and all(isinstance(item, str) for item in report["notes"]), "STORY_EVIDENCE_FAILED", "invalid evidence diagnostics")
    expected = {key for label, _, _ in LANES for key in (label + "-build", label)} | {
        "scanner-capabilities", "script-smoke-preflight", "solution-release-build"}
    C.closed(report["execution_inputs"], ("packages_sha256", "toolchain_sha256"))
    C.require(all(value is None or C.sha(value) for value in report["execution_inputs"].values()),
              "STORY_EVIDENCE_FAILED", "invalid execution input binding")
    C.require(isinstance(report["diagnostics"], list) and len(report["diagnostics"]) <= 30,
              "STORY_EVIDENCE_FAILED", "invalid support-safe diagnostics")
    for diagnostic in report["diagnostics"]:
        C.closed(diagnostic, ("lane", "code", "summary", "compiler_codes", "failed_tests", "source_findings", "log_sha256"))
        C.require(diagnostic["lane"] in expected | {"dependencies"} and diagnostic["code"] in C.CODES
                  and isinstance(diagnostic["summary"], str) and len(diagnostic["summary"]) <= 2048
                  and isinstance(diagnostic["compiler_codes"], list) and len(diagnostic["compiler_codes"]) <= 20
                  and all(isinstance(value, str) and re.fullmatch(r"[A-Z][A-Z0-9]{1,10}", value) for value in diagnostic["compiler_codes"])
                  and isinstance(diagnostic["failed_tests"], list) and len(diagnostic["failed_tests"]) <= 12
                  and all(isinstance(value, str) and re.fullmatch(r"[A-Za-z_][A-Za-z0-9_.+`]{0,500}", value) for value in diagnostic["failed_tests"])
                  and isinstance(diagnostic["source_findings"], list) and len(diagnostic["source_findings"]) <= 20
                  and all(isinstance(value, str) and re.fullmatch(r"[A-Za-z0-9_./@-]+:[1-9][0-9]{0,7}", value) for value in diagnostic["source_findings"])
                  and (diagnostic["log_sha256"] is None or C.sha(diagnostic["log_sha256"])),
                  "STORY_EVIDENCE_FAILED", "invalid support-safe diagnostic")
    C.require(isinstance(report["lanes"], list) and len(report["lanes"]) <= len(expected),
              "STORY_EVIDENCE_FAILED", "invalid evidence lane inventory")
    found = set()
    for lane in report["lanes"]:
        C.closed(lane, ("id", "result", "counts"))
        C.require(lane["id"] in expected and lane["id"] not in found and lane["result"] in ("PASS", "FAIL"),
                  "STORY_EVIDENCE_FAILED", "duplicate or unknown evidence lane")
        found.add(lane["id"])
        matching = [row for row in LANES if row[0] == lane["id"]]
        if lane["counts"] is not None:
            value = lane["counts"]
            C.closed(value, ("total", "passed", "failed", "skipped", "errors", "not_run", "selectors"))
            C.require(all(type(value[key]) is int and value[key] >= 0 for key in value if key != "selectors")
                      and isinstance(value["selectors"], dict)
                      and all(isinstance(name, str) and type(count) is int and count >= 0 for name, count in value["selectors"].items()),
                      "STORY_EVIDENCE_FAILED", "invalid test execution counts")
        if matching and lane["result"] == "PASS":
            value = lane["counts"]
            selectors = {matching[0][1] + "." + suffix for suffix in matching[0][2]}
            C.require(value is not None and value["total"] == value["passed"] > 0
                      and value["failed"] == value["skipped"] == value["errors"] == value["not_run"] == 0
                      and set(value["selectors"]) == selectors and all(value["selectors"].values()),
                      "STORY_EVIDENCE_FAILED", "passing lane lacks complete discovery/execution")
        if lane["id"] == "script-smoke-preflight" and lane["result"] == "PASS":
            value = lane["counts"]
            C.require(value is not None and value["total"] == value["passed"] > 0
                      and value["failed"] == value["skipped"] == value["errors"] == value["not_run"] == 0
                      and value["selectors"] == {}, "STORY_EVIDENCE_FAILED", "passing script lacks positive execution")
        if not matching and lane["id"] != "script-smoke-preflight":
            C.require(lane["counts"] is None, "STORY_EVIDENCE_FAILED", "unexpected execution counts")
    C.require(isinstance(report["capabilities"], list) and len(report["capabilities"]) <= len(CAPABILITIES),
              "STORY_EVIDENCE_FAILED", "invalid capability inventory")
    capabilities = []
    for item in report["capabilities"]:
        C.closed(item, ("id", "detected"))
        C.require(item["id"] in CAPABILITIES and item["id"] not in capabilities and type(item["detected"]) is bool,
                  "STORY_EVIDENCE_FAILED", "invalid capability evidence")
        capabilities.append(item["id"])
    if report["result"] == "PASS":
        C.require(not report["gaps"] and found == expected and all(lane["result"] == "PASS" for lane in report["lanes"])
                  and capabilities == list(CAPABILITIES) and all(item["detected"] for item in report["capabilities"])
                  and not report["diagnostics"] and all(C.sha(value) for value in report["execution_inputs"].values()),
                  "STORY_EVIDENCE_FAILED", "complete declared evidence required")
    else:
        C.require(report["gaps"], "STORY_EVIDENCE_FAILED", "failed evidence needs precise gaps")
    return report


def assert_stable(root, manifest, diagnostic=False):
    expected = [{key: entry[key] for key in ("path", "mode", "git_sha")} for entry in manifest["entries"]]
    current = index_entries(root)
    if diagnostic:
        # Uncommitted bytes are allowed only for a standalone diagnostic run.
        actual = diagnostic_entries(root, current)
        C.require([(row["path"], row["mode"]) for row in actual] == [(row["path"], row["mode"]) for row in expected],
                  "SOURCE_MISMATCH", "diagnostic membership changed")
    else:
        C.require(current == expected, "SOURCE_MISMATCH", "source membership changed during evidence")
    for prefix, entries in [("", manifest["entries"]), *((item["path"], item["entries"]) for item in manifest["submodules"])]:
        for entry in entries:
            if entry["mode"] != "160000":
                C.require(C.digest(read_source(root / prefix, entry)) == entry["sha256"], "SOURCE_MISMATCH", "source content changed: " + prefix + "/" + entry["path"])
        if prefix and not diagnostic:
            actual = index_entries(root / prefix)
            if prefix.endswith("Hexalith.FrontComposer"):
                selected = {row["path"] for row in entries}
                actual = [row for row in actual if row["path"] in selected]
            C.require(actual == [{key: row[key] for key in ("path", "mode", "git_sha")} for row in entries],
                      "SOURCE_MISMATCH", "submodule membership changed during evidence")
    for repository in manifest.get("metadata", []):
        C.require(read_objects(root / repository["path"], [row["oid"] for row in repository["objects"]]) == repository["objects"],
                  "SOURCE_MISMATCH", "required Git metadata changed during evidence")
    for item in manifest.get("runtime", []):
        C.require(runtime_digest(Path(item["source"])) == item["sha256"], "SOURCE_MISMATCH", "runtime input changed: " + item["name"])


def write_index(root, entries):
    """Write a private scratch index so the genuine tracked scan sees all members.

    No shared repository index, object, branch, or lifecycle label is written.
    Nested gitlinks remain gitlinks; their repositories are never initialized.
    """
    directory = root / ".git"
    directory.mkdir(exist_ok=True)
    body = b"DIRC" + struct.pack("!II", 2, len(entries))
    for entry in sorted(entries, key=lambda item: item["path"].encode()):
        name = entry["path"].encode()
        record = struct.pack("!10I", 0, 0, 0, 0, 0, 0, int(entry["mode"], 8), 0, 0, 0)
        record += bytes.fromhex(entry["git_sha"]) + struct.pack("!H", min(len(name), 4095)) + name + b"\0"
        body += record + b"\0" * ((-len(record)) % 8)
    (directory / "index").write_bytes(body + hashlib.sha1(body).digest())
    (directory / "HEAD").write_text("ref: refs/heads/diagnostic\n")
    (directory / "config").write_text("[core]\nrepositoryformatversion = 0\nbare = false\n")
    (directory / "objects").mkdir(exist_ok=True)
    (directory / "refs").mkdir(exist_ok=True)


def materialize(root, scratch, manifest):
    for prefix, entries in [("", manifest["entries"]), *((item["path"], item["entries"]) for item in manifest["submodules"])]:
        for entry in entries:
            if entry["mode"] == "160000":
                continue
            value = read_source(root / prefix, entry)
            C.require(C.digest(value) == entry["sha256"], "SOURCE_MISMATCH", "input changed during scratch copy")
            path = scratch / prefix / entry["path"]
            path.parent.mkdir(parents=True, exist_ok=True)
            if entry["mode"] == "120000":
                path.symlink_to(value.decode("utf-8"))
            else:
                path.write_bytes(value)
                path.chmod(0o700 if entry["mode"] == "100755" else 0o600)
    write_index(scratch, manifest["entries"])
    for item in manifest["submodules"]:
        write_index(scratch / item["path"], item["entries"])
    for repository in manifest.get("metadata", []):
        destination = scratch / repository["path"]
        pack = git(root / repository["path"], "pack-objects", "--stdout", "--threads=1", "--compression=1",
                   content="".join(row["oid"] + "\n" for row in repository["objects"]).encode(), limit=MAX_SOURCE_BYTES)
        # This is a bounded object database, not a full history clone. Strict
        # connectivity would require unrelated ancestor trees and commits.
        # Verify every selected object's content address below instead.
        git(destination, "index-pack", "--stdin", content=pack)
        C.require(read_objects(destination, [row["oid"] for row in repository["objects"]]) == repository["objects"],
                  "SOURCE_MISMATCH", "private Git object materialization mismatch")
        (destination / ".git/HEAD").write_text(repository["head"] + "\n")
        with (destination / ".git/config").open("a") as stream:
            stream.write('[remote "origin"]\nurl = ' + repository["origin"] + "\n")
    runtime = []
    for item in manifest.get("runtime", []):
        source = Path(item["source"])
        C.require(runtime_digest(source) == item["sha256"], "SOURCE_MISMATCH", "runtime input changed during copy")
        copied = dict(item)
        if item["destination"] is not None:
            destination = scratch / item["destination"]
            C.require(destination.resolve().is_relative_to(scratch.resolve()), "SOURCE_MISMATCH", "unsafe runtime destination")
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, destination)
            destination.chmod(0o700 if item["name"] in ("dapr-cli", "dapr-runtime") else 0o600)
            copied["source"] = str(destination)
        runtime.append(copied)
    manifest = {**manifest, "runtime": runtime}
    if any(item["name"] == "dapr-runtime" for item in runtime):
        (scratch / ".dapr/components").mkdir(parents=True, exist_ok=True)
        (scratch / ".dapr/config.yaml").write_text('apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: story-5-3\nspec:\n  tracing:\n    samplingRate: "0"\n')
    (scratch / INVENTORY).write_bytes(C.canonical(manifest))


def dependency_inventory(cache):
    """Verify freshly restored archives and every extracted execution byte."""
    entries = []
    if not cache.exists():
        return entries
    total = 0
    for package in sorted(cache.glob("*/*")):
        if not package.is_dir():
            continue
        package_id = package.parent.name
        archive = package / f"{package_id}.{package.name}.nupkg"
        C.require(archive.is_file() and not archive.is_symlink(), "STORY_EVIDENCE_FAILED", "canonical dependency archive missing")
        checksum = archive.with_suffix(archive.suffix + ".sha512")
        expected = base64.b64decode(checksum.read_text().strip(), validate=True)
        digest = hashlib.sha512()
        archive_digest = hashlib.sha256()
        with archive.open("rb") as stream:
            for block in iter(lambda: stream.read(C.MAX_BYTES), b""):
                digest.update(block)
                archive_digest.update(block)
        C.require(digest.digest() == expected, "SOURCE_MISMATCH", "dependency archive checksum mismatch")
        archive_sha256 = archive_digest.hexdigest()
        with zipfile.ZipFile(archive) as document:
            members = {}
            for row in document.infolist():
                if row.is_dir():
                    continue
                # NuGet normalizes redundant separators in published analyzer
                # packages. Preserve traversal/absolute-path rejection and
                # refuse collisions after that deterministic normalization.
                name = "/".join(part for part in row.filename.split("/") if part)
                C.require(not row.filename.startswith("/") and C.M.repository_path(name)
                          and name not in members, "SOURCE_MISMATCH", "unsafe or ambiguous dependency archive")
                members[name] = row
            C.require(len(members) <= 30000, "SOURCE_MISMATCH", "dependency archive entry limit")
            for path in sorted(package.rglob("*")):
                if path.is_dir():
                    continue
                name = path.relative_to(package).as_posix()
                if name == ".nupkg.metadata":
                    continue  # NuGet transport metadata contains timestamps, not executable inputs.
                value = file_digest(path)
                total += path.stat().st_size
                C.require(total <= 8 * 1024 * C.MAX_BYTES, "STORY_EVIDENCE_LIMIT", "dependency byte limit")
                if path not in (archive, checksum):
                    member = members.get(name)
                    if member is None and name.endswith(".nuspec"):
                        member = next((row for key, row in members.items() if key.lower() == name), None)
                    if member is None and name.lower() == f"{package_id}.nupkg":
                        # Aspire's CLI bundle keeps an unversioned copy beside the restored package.
                        C.require(value == archive_sha256, "SOURCE_MISMATCH", "dependency archive copy differs from verified archive")
                    else:
                        C.require(member is not None, "SOURCE_MISMATCH", "unverified extracted dependency input")
                        original = hashlib.sha256()
                        with document.open(member) as stream:
                            for block in iter(lambda: stream.read(C.MAX_BYTES), b""):
                                original.update(block)
                        C.require(original.hexdigest() == value, "SOURCE_MISMATCH", "extracted dependency differs from verified archive")
                elif path == archive:
                    C.require(value == archive_sha256, "SOURCE_MISMATCH", "dependency archive changed during verification")
                entries.append({"path": path.relative_to(cache).as_posix(), "sha256": value})
    C.require(len(entries) <= 150000, "STORY_EVIDENCE_LIMIT", "dependency inventory limit")
    return entries


def failure_diagnostic(label, error, work, manifest):
    """Keep locations and parameter-free test names; never retain log excerpts."""
    log = work / (label + ".log")
    compiler, failed, findings = [], set(), set()
    if log.exists():
        compiler = sorted(set(re.findall(r"\berror ([A-Z][A-Z0-9]{1,10})\b", log.read_text(errors="replace"))))[:20]
    paths = {row["path"] for row in manifest["entries"]}
    paths.update(item["path"] + "/" + row["path"] for item in manifest.get("submodules", []) for row in item["entries"])
    xml = work / (label + ".xml")
    if xml.exists() and xml.stat().st_size <= 16 * C.MAX_BYTES:
        try:
            for test in ET.parse(xml).getroot().iter("test"):
                if test.get("result") != "Fail":
                    continue
                owner, method = test.get("type", ""), test.get("method", "")
                if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_.+`]{0,400}", owner) and re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]{0,100}", method):
                    failed.add(owner + "." + method)
                for part in test.iter("message"):
                    for path, line in re.findall(r"([A-Za-z0-9_./@-]+):([1-9][0-9]{0,7})(?![0-9])", part.text or ""):
                        if path.startswith(str(work.parent) + "/"):
                            path = path[len(str(work.parent)) + 1:]
                        if path in paths:
                            findings.add(path + ":" + line)
        except ET.ParseError:
            pass
    return {"lane": label, "code": error.code if isinstance(error, C.Refusal) else "STORY_EVIDENCE_FAILED",
            "summary": str(error)[:2048] if isinstance(error, C.Refusal) else "unreadable evidence: " + type(error).__name__,
            "compiler_codes": compiler, "failed_tests": sorted(failed)[:12], "source_findings": sorted(findings)[:20],
            "log_sha256": file_digest(log) if log.exists() else None}


def counts(xml, selectors, passing=True):
    """Require complete, nonempty xUnit v3 execution, including every selector."""
    C.require(xml.exists() and xml.stat().st_size <= 16 * C.MAX_BYTES, "STORY_EVIDENCE_FAILED", "missing or oversized test evidence")
    root = ET.parse(xml).getroot()
    assemblies = list(root.iter("assembly"))
    C.require(len(assemblies) == 1, "STORY_EVIDENCE_FAILED", "ambiguous test assembly report")
    assembly = assemblies[0]
    values = {key: int(assembly.get(key, "-1")) for key in ("total", "passed", "failed", "skipped", "errors")}
    values["not_run"] = sum(int(assembly.get(key, "0")) for key in ("not-run", "notRun", "notrun", "not_run"))
    tests = list(root.iter("test"))
    discovered = {name: sum(test.get("type") == name for test in tests) for name in selectors}
    C.require(all(value >= 0 for value in values.values()) and values["total"] == len(tests),
              "STORY_EVIDENCE_FAILED", "malformed or incomplete test counts")
    if passing:
        C.require(values["total"] > 0 and values["passed"] == values["total"]
                  and values["failed"] == values["skipped"] == values["errors"] == values["not_run"] == 0
                  and all(test.get("result") == "Pass" for test in tests), "STORY_EVIDENCE_FAILED", "empty, failed, skipped or incomplete test evidence")
        C.require(all(value > 0 for value in discovered.values()), "STORY_EVIDENCE_FAILED", "required test selector discovered no tests")
    return {**values, "selectors": discovered}


# This program calls the current, built business recognizer. Probe values exist
# only at runtime; findings expose capability names and booleans, never values.
PROBE_SOURCE = r'''
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
public static class CompletionCapabilityProbe
{
    public static int Main(string[] args)
    {
        var assembly = Assembly.LoadFrom(args[0]);
        var type = assembly.GetType("Hexalith.EventStore.Server.Tests.Security.SecretsProtectionTests", true)!;
        var method = type.GetMethod("FindViolations", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null)!;
        string value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string quote = "\"";
        var probes = new (string Id, string Path, string Content)[]
        {
            ("positive-assignment", "config/probe.json", "{" + quote + "pass" + "word" + quote + ":" + quote + value + quote + "}"),
            ("yaml-credential-sequence", "config/probe.yaml", "pass" + "words:\n  - " + value + "\n"),
            ("qualified-network-credential", "src/probe.cs", "var x = new System.Net.NetworkCredential(" + quote + value + quote + ", " + quote + value + quote + ");"),
            ("qualified-client-secret-credential", "src/probe.cs", "var x = new Azure.Identity.ClientSecretCredential(" + quote + value + quote + ", " + quote + value + quote + ", " + quote + value + quote + ");"),
            ("xml-cdata", "config/probe.xml", "<pass" + "word><![CDATA[" + value + "]]></pass" + "word>"),
            ("space-separated-cli-secret", "scripts/probe.sh", "tool --client-" + "secret " + value),
            ("private-octet-jwk", "config/probe.json", JsonSerializer.Serialize(new Dictionary<string, string> { ["kty"] = "oct", ["k"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }))
        };
        foreach (var probe in probes)
        {
            var findings = (IEnumerable<string>)method.Invoke(null, new object[] { probe.Path, probe.Content })!;
            Console.WriteLine(JsonSerializer.Serialize(new { id = probe.Id, detected = findings.Any() }));
        }
        return 0;
    }
}
'''


def capability_probe(scratch, source_root, dotnet, fallback):
    """Compile every current scanner partial and execute its actual recognizer.

    This small business-code lane is independent of the broad server project's
    unrelated build prerequisites. It never substitutes for that required lane.
    Package versions come from the repository's existing central configuration.
    """
    probe = scratch / ".scanner-capability"
    probe.mkdir(mode=0o700)
    for filename in ("Directory.Build.props", "Directory.Build.targets"):
        (probe / filename).write_text("<Project />\n")
    (probe / "Directory.Packages.props").write_text("<Project />\n")
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    ET.SubElement(project, "Import", Project=str(source_root / "Directory.Packages.props"))
    properties = ET.SubElement(project, "PropertyGroup")
    for name, value in {"OutputType": "Exe", "TargetFramework": "net11.0", "ImplicitUsings": "enable", "Nullable": "enable",
                        "IsTestProject": "false", "StartupObject": "CompletionCapabilityProbe", "GenerateProgramFile": "false",
                        "NuGetAudit": "true", "NuGetAuditMode": "all", "NuGetAuditLevel": "low",
                        "WarningsAsErrors": "NU1900;NU1901;NU1902;NU1903;NU1904;NU1905"}.items():
        ET.SubElement(properties, name).text = value
    items = ET.SubElement(project, "ItemGroup")
    ET.SubElement(items, "Using", Include="Xunit")
    for package in ("Shouldly", "xunit.v3"):
        ET.SubElement(items, "PackageReference", Include=package)
    sources = sorted((source_root / "tests/Hexalith.EventStore.Server.Tests/Security").glob("SecretsProtectionTests*.cs"))
    C.require(sources, "STORY_EVIDENCE_FAILED", "scanner source unavailable")
    for source in sources:
        ET.SubElement(items, "Compile", Include=str(source), Link=source.name)
    ET.ElementTree(project).write(probe / "probe.csproj", encoding="unicode")
    (probe / "CompletionCapabilityProbe.cs").write_text(PROBE_SOURCE)
    environment = {"PATH": str(dotnet.parent) + os.pathsep + os.defpath, "DOTNET_ROOT": str(dotnet.parent),
                   "DOTNET_CLI_HOME": str(scratch), "DOTNET_NOLOGO": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
                   "NUGET_PACKAGES": str(probe / "packages"), "MSBUILDDISABLENODEREUSE": "1"}
    def command(argv, label):
        return C.Control.bounded_process(argv, scratch, label + ".log", timeout_seconds=180,
                                         environment_extra=environment, failure_code="STORY_EVIDENCE_FAILED", limit_code="STORY_EVIDENCE_LIMIT")
    command([str(dotnet), "build", str(probe / "probe.csproj"), "--configuration", "Release", "--disable-build-servers", "-m:1",
             "-p:UseSharedCompilation=false", "-p:RestoreFallbackFolders="], "capabilities-build")
    executable = probe / "bin/Release/net11.0/probe.dll"
    raw = command([str(dotnet), str(executable), str(executable)], "capabilities")
    found = [json.loads(line) for line in raw.decode().splitlines() if line.startswith('{')]
    C.require([item.get("id") for item in found] == list(CAPABILITIES), "STORY_EVIDENCE_FAILED", "missing capability evidence")
    for item in found:
        C.closed(item, ("id", "detected"))
        C.require(type(item["detected"]) is bool, "STORY_EVIDENCE_FAILED", "invalid capability result")
    return found


def cleanup_authentication_containers(owner, run):
    """Reap only this invocation's labelled containers, including on lane timeout."""
    C.require(re.fullmatch(r"[0-9a-f]{32}", owner) is not None, "STORY_EVIDENCE_FAILED", "invalid private topology owner")
    label = "hexalith.eventstore.authentication-test-owner"
    raw = run(["docker", "ps", "--all", "--filter", "label=" + label + "=" + owner,
               "--format", "{{.ID}}", "--no-trunc"], "integration-auth-cleanup-list", timeout=30)
    identifiers = raw.decode().splitlines()
    C.require(len(identifiers) <= 32 and all(C.sha(item, 64) for item in identifiers),
              "STORY_EVIDENCE_FAILED", "invalid private topology cleanup inventory")
    failures = []
    for index, identifier in enumerate(identifiers):
        def disappeared():
            current = run(["docker", "ps", "--all", "--filter", "id=" + identifier, "--format", "{{.ID}}", "--no-trunc"],
                          "integration-auth-cleanup-absent-" + str(index), timeout=30).decode().splitlines()
            C.require(current in ([], [identifier]), "STORY_EVIDENCE_FAILED", "invalid exact-container cleanup inventory")
            return not current
        try:
            actual = json.loads(run(["docker", "inspect", "--format", '{{json (index .Config.Labels "' + label + '")}}', identifier],
                                    "integration-auth-cleanup-owner-" + str(index), timeout=30).decode())
            if actual != owner:
                failures.append(identifier + ":ownership changed; refusing cleanup")
                continue
            run(["docker", "rm", "--force", identifier], "integration-auth-cleanup-remove-" + str(index), timeout=30)
        except (C.Refusal, OSError, ValueError, subprocess.SubprocessError) as error:
            try:
                if disappeared():
                    continue
            except (C.Refusal, OSError, ValueError, subprocess.SubprocessError) as absence_error:
                failures.append(identifier + ":absence check failed:" + type(absence_error).__name__)
            failures.append(identifier + ":cleanup failed:" + type(error).__name__)
    C.require(not failures, "STORY_EVIDENCE_FAILED", "private topology cleanup failures: " + "; ".join(failures))


def execute(scratch, manifest, dotnet, fallback):
    """Execute only the fixed evidence plan. Continue collecting truthful gaps."""
    binding = summary(manifest)
    report = {"schema": SCHEMA, "adapter": ADAPTER, "result": "FAIL", "binding": binding,
              "lanes": [], "capabilities": [], "gaps": [], "diagnostics": [],
              "execution_inputs": {"packages_sha256": None, "toolchain_sha256": C.digest(C.canonical([
                  {key: value for key, value in item.items() if key != "source"} for item in manifest.get("runtime", [])]))},
              "notes": ["Slice covers Story 5.3 hosts only; no Tenants/generated/future-host or whole-NFR closure.",
                        "Epics permits a symmetric override outside Development; current PRD and implementation reject it in Production."]}
    work = scratch / ".story-5-3-work"
    work.mkdir(mode=0o700)
    environment = {"PATH": os.pathsep.join((str(scratch / ".story-5-3-tools"), str(dotnet.parent), "/usr/local/bin", os.defpath)),
                   "GIT_NO_REPLACE_OBJECTS": "1", "GIT_CONFIG_NOSYSTEM": "1", "GIT_CONFIG_GLOBAL": "/dev/null",
                   "DOTNET_ROOT": str(dotnet.parent), "DOTNET_CLI_HOME": str(work), "DOTNET_NOLOGO": "1",
                   "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "NUGET_PACKAGES": str(work / "packages"),
                   "MSBUILDDISABLENODEREUSE": "1"}
    build_options = ["--configuration", "Release", "--disable-build-servers", "-m:1", "-p:UseSharedCompilation=false",
                     "-p:MinVerVersionOverride=1.0.0", "-p:UseNuGetDeps=true",
                     "-p:RestoreFallbackFolders="]
    package_inputs = {}
    authentication_owner = os.environ.get(AUTH_OWNER_ENV) or os.urandom(16).hex()
    C.require(re.fullmatch(r"[0-9a-f]{32}", authentication_owner) is not None,
              "STORY_EVIDENCE_FAILED", "invalid private topology owner")
    environment[AUTH_OWNER_ENV] = authentication_owner
    authentication_temp = os.environ.get(AUTH_TEMP_ENV)
    C.require(authentication_temp is not None and Path(authentication_temp).is_dir(),
              "STORY_EVIDENCE_FAILED", "prerequisite:private-authentication-temporary-space-missing")
    environment[AUTH_TEMP_ENV] = authentication_temp

    def bind_packages(cache):
        for item in dependency_inventory(cache):
            key = cache.name + "/" + item["path"]
            C.require(key not in package_inputs or package_inputs[key] == item["sha256"],
                      "SOURCE_MISMATCH", "consumed dependency bytes changed")
            package_inputs[key] = item["sha256"]

    def run(argv, label, timeout=180):
        selected_environment = dict(environment)
        if label == "integration-auth":
            selected_environment.update({name: authentication_temp for name in ("TMPDIR", "TMP", "TEMP")})
        return C.Control.bounded_process(argv, scratch, str(work.relative_to(scratch) / (label + ".log")),
                                         timeout_seconds=timeout, environment_extra=selected_environment,
                                         failure_code="STORY_EVIDENCE_FAILED", limit_code="STORY_EVIDENCE_LIMIT")

    def attempt(label, action):
        try:
            evidence = action()
            report["lanes"].append({"id": label, "result": "PASS", "counts": evidence})
            return True
        except (C.Refusal, OSError, ValueError, ET.ParseError, subprocess.SubprocessError, zipfile.BadZipFile) as error:
            code = error.code if isinstance(error, C.Refusal) else "STORY_EVIDENCE_FAILED"
            report["lanes"].append({"id": label, "result": "FAIL", "counts": getattr(error, "counts", None)})
            report["gaps"].append(label + ":" + code)
            report["diagnostics"].append(failure_diagnostic(label, error, work, manifest))
            return False

    for label, project, suffixes in LANES:
        path = "tests/" + project + "/" + project + ".csproj"
        built = attempt(label + "-build", lambda path=path, label=label: (run([str(dotnet), "build", path, *build_options], label + "-build"), None)[1])
        if not built:
            report["gaps"].append(label + ":missing-built-lane")
            continue
        selectors = [project + "." + suffix for suffix in suffixes]
        xml = work / (label + ".xml")
        assembly = scratch / "tests" / project / "bin/Release/net11.0" / (project + ".dll")
        def test(assembly=assembly, selectors=selectors, xml=xml, label=label):
            try:
                bind_packages(work / "packages")
                if label == "integration-auth":
                    names = {item["name"] for item in manifest.get("runtime", [])}
                    C.require("dapr-cli" in names, "STORY_EVIDENCE_FAILED", "prerequisite:dapr-cli-missing")
                    C.require("dapr-runtime" in names, "STORY_EVIDENCE_FAILED", "prerequisite:dapr-runtime-missing")
                    (scratch / (AUTH_MARKER_PREFIX + authentication_owner)).touch(mode=0o600, exist_ok=False)
                run([str(dotnet), str(assembly), "-xml", str(xml), *[argument for name in selectors for argument in ("-class", name)]], label)
                bind_packages(work / "packages")
                return counts(xml, selectors)
            except C.Refusal as error:
                if xml.exists():
                    error.counts = counts(xml, selectors, passing=False)
                raise
            finally:
                if label == "integration-auth":
                    cleanup_authentication_containers(authentication_owner, run)
        attempt(label, test)

    def probes():
        report["capabilities"] = capability_probe(work, scratch, dotnet, fallback)
        bind_packages(work / ".scanner-capability/packages")
        missing = []
        for item in report["capabilities"]:
            if not item["detected"]:
                report["gaps"].append("scanner-capability:" + item["id"])
                missing.append(item["id"])
        C.require(not missing, "STORY_EVIDENCE_FAILED", "missing scanner capabilities: " + ",".join(missing))
        return None
    attempt("scanner-capabilities", probes)

    def script():
        output = run(["/bin/bash", "scripts/tests/generated-api-smoke-preflight.test.sh"], "script-smoke-preflight")
        found = re.findall(rb"# summary: ([0-9]+) passed, ([0-9]+) failed", output)
        C.require(len(found) == 1 and int(found[0][0]) > 0 and int(found[0][1]) == 0,
                  "STORY_EVIDENCE_FAILED", "empty or failed script evidence")
        return {"total": int(found[0][0]), "passed": int(found[0][0]), "failed": 0, "skipped": 0,
                "errors": 0, "not_run": 0, "selectors": {}}
    attempt("script-smoke-preflight", script)
    attempt("solution-release-build", lambda: (run([str(dotnet), "build", "Hexalith.EventStore.slnx", *build_options], "solution-release-build", 600), None)[1])
    try:
        bind_packages(work / "packages")
        bind_packages(work / ".scanner-capability/packages")
        report["execution_inputs"]["packages_sha256"] = C.digest(C.canonical(package_inputs))
    except (C.Refusal, OSError, ValueError, zipfile.BadZipFile) as error:
        report["gaps"].append("dependencies:SOURCE_MISMATCH")
        report["diagnostics"].append({"lane": "dependencies", "code": "SOURCE_MISMATCH", "summary": "dependency content validation failed",
                                      "compiler_codes": [], "failed_tests": [], "source_findings": [], "log_sha256": None})
    report["result"] = "PASS" if not report["gaps"] else "FAIL"
    return report


def evaluate(control):
    C.require(supported(control.story), "ADAPTER_UNAVAILABLE", "story-owned NFR evidence adapter unavailable: " + ", ".join(control.story["high_risk_nfr_ids"]))
    manifest = inventory(control.root, control.api, control.head)
    manifest["runtime"] = runtime_inventory(control.root)
    control.story_inventory = manifest
    control.binding["story_adapter"] = summary(manifest)
    try:
        report = run(control.root, manifest)
    except C.Refusal as error:
        control.story_evidence = getattr(error, "story_evidence", None)
        raise
    control.story_evidence = report
    assert_stable(control.root, manifest)
    C.require(report["result"] == "PASS", "STORY_EVIDENCE_FAILED", "Story 5.3 evidence gaps: " + ", ".join(report["gaps"]))


def run(root, manifest, diagnostic=False, logs=None):
    if "runtime" not in manifest:
        manifest["runtime"] = runtime_inventory(root)
    dotnet = Path(next(item["source"] for item in manifest["runtime"] if item["name"] == "dotnet"))
    with tempfile.TemporaryDirectory(prefix="story-5-3-") as directory, \
         tempfile.TemporaryDirectory(prefix="story-5-3-authentication-") as authentication_directory:
        scratch = Path(directory)
        materialize(root, scratch, manifest)
        # The explicit entry point is rerun in isolation, never a saved report.
        owner = os.urandom(16).hex()
        environment = {"PATH": str(dotnet.parent) + os.pathsep + "/usr/local/bin" + os.pathsep + os.defpath,
                       AUTH_OWNER_ENV: owner, AUTH_TEMP_ENV: authentication_directory}
        try:
            output = C.Control.bounded_process([sys.executable, "-I", "-B", str(scratch / VALIDATOR), "--root", str(scratch), "--bound"],
                                               scratch, "evidence.json", timeout_seconds=2400,
                                               failure_code="STORY_EVIDENCE_FAILED", limit_code="STORY_EVIDENCE_LIMIT",
                                               accept_failure=True, environment_extra=environment)
        finally:
            # The bounded validator can itself be killed before its inner
            # finally runs. Its still-live parent retains the same exact owner.
            try:
                if (scratch / (AUTH_MARKER_PREFIX + owner)).exists():
                    cleanup_authentication_containers(owner, lambda argv, label, timeout:
                        C.Control.bounded_process(argv, scratch, "outer-" + label + ".log", timeout_seconds=timeout,
                            environment_extra=environment, failure_code="STORY_EVIDENCE_FAILED", limit_code="STORY_EVIDENCE_LIMIT"))
            finally:
                if logs is not None:
                    for path in [*(scratch / ".story-5-3-work").glob("*"), *scratch.glob("outer-integration-auth-cleanup-*.log")]:
                        if path.is_file() and path.suffix in (".log", ".xml"):
                            shutil.copyfile(path, logs / path.name)
        report = C.parse(output)
        validate_report(report, summary(manifest))
        if logs is not None:
            for path in (scratch / ".story-5-3-work").glob("*"):
                if path.is_file() and path.suffix in (".log", ".xml"):
                    shutil.copyfile(path, logs / path.name)
            shutil.copyfile(scratch / INVENTORY, logs / "source-inventory.json")
            (logs / "result.json").write_bytes(C.canonical(report) + b"\n")
        try:
            assert_stable(root, manifest, diagnostic)
        except C.Refusal as error:
            error.story_evidence = report
            raise
        return report


def main(argv=None):
    from phase_4_gate_decision import Parser
    parser = Parser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--bound", action="store_true", help="private adapter invocation; diagnostics confer no authority")
    parser.add_argument("--logs", type=Path, help="save standalone diagnostic logs in a new empty directory")
    try:
        args = parser.parse_args(argv)
        root = args.root.resolve()
        if args.bound:
            C.require(args.logs is None, "INPUT_INVALID", "private evidence cannot select persistent output", 2)
            dotnet = shutil.which("dotnet")
            C.require(dotnet is not None, "STORY_EVIDENCE_FAILED", "dotnet SDK unavailable")
            with (root / INVENTORY).open("rb") as stream:
                content = stream.read(MAX_INVENTORY_BYTES + 1)
            C.require(len(content) <= MAX_INVENTORY_BYTES, "INPUT_INVALID", "oversized private inventory", 2)
            manifest = C.parse(content)
            assert_stable(root, manifest)
            report = execute(root, manifest, Path(dotnet).resolve(), None)
            assert_stable(root, manifest)
        else:
            logs = args.logs.absolute() if args.logs is not None else None
            if logs is not None:
                C.require(not any(part.is_symlink() for part in (logs, *logs.parents)) and not logs.exists(),
                          "INPUT_INVALID", "logs require a new directory without symbolic parents", 2)
                C.require(not logs.is_relative_to(root / ".git"), "INPUT_INVALID", "logs cannot write Git metadata", 2)
                logs.mkdir(parents=True, mode=0o700)
            manifest = inventory(root)
            report = run(root, manifest, diagnostic=True, logs=logs)
        print(json.dumps(report, sort_keys=True))
        # Bounded parent reads the closed FAIL evidence as well as PASS evidence.
        return 0 if report["result"] == "PASS" else 1
    except (C.Refusal, OSError, ValueError, KeyError, TypeError, AttributeError, ET.ParseError, subprocess.SubprocessError, zipfile.BadZipFile) as error:
        code = error.code if isinstance(error, C.Refusal) else "STORY_EVIDENCE_FAILED"
        print(json.dumps({"schema": SCHEMA, "adapter": ADAPTER, "result": "FAIL", "binding": summary(manifest) if "manifest" in locals() else None,
                          "lanes": [], "capabilities": [], "gaps": [code], "diagnostics": [],
                          "execution_inputs": {"packages_sha256": None, "toolchain_sha256": None},
                          "notes": [str(error) if isinstance(error, C.Refusal) else "unreadable evidence: " + type(error).__name__]}, sort_keys=True))
        return 1
