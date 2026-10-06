"""Published-package qualification harness for 6.1-P1R; it never grants usability.

Owner-selected execution inputs bind an exact candidate (and an optional capable
rollback) tuple with its tag, Builds identity, feed, archive/content hashes and
repository commits, plus the operational profile and the accepted assertion
instrumentation. Without them every dependent lane is refused and stays
``unavailable/unverified``; nothing is selected by default.

Package lanes observe the actual archives, signatures, isolated Release restore
graphs and physically loaded assemblies; the validator recomputes every check
from the retained observations. Imported scenario, restore and cleanup receipts
are validated against their contracts and bound to the verified packages and
selected profile. Synthetic fixtures and local process controls keep their
tooling-only scope, so they can never satisfy a published or operational lane.
Owner decisions and same-baseline conformance are evaluated separately, and P1R
usability always remains false: it belongs to a separately validated coordinated
transition that this harness neither performs nor authorizes.
"""
from __future__ import annotations

import datetime as dt
import io
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import uuid
import xml.etree.ElementTree as ET
import zipfile
import zlib

import p1r_qualification as preparation
import p1r_qualification_runtime as runtime
import release_package_contract as release
from p1r_qualification import (InvalidPacket, checks_counter, digest, members, observe, ordered_rows, regular, relative,
                               require, stamp, validate_times, write_json)

ROOT = preparation.ROOT
SCHEMA = "hexalith.p1r.published-qualification.v1"
INPUTS_SCHEMA = "hexalith.p1r.owner-inputs.v1"
DECISIONS_SCHEMA = "hexalith.p1r.owner-decisions.v1"
EVIDENCE_SCHEMA = "hexalith.p1r.package-evidence.v1"
PACKAGE_RECEIPT_SCHEMA = "hexalith.p1r.package-receipt.v1"
LANE_RECEIPT_SCHEMA = "hexalith.p1r.lane-receipt.v1"
SYNTHETIC = "hexalith-p1r-synthetic-fixture"
MARKER_ENTRY = SYNTHETIC + ".txt"
VERIFIER = "dotnet-nuget-verify"
REPOSITORY_SIGNATURE = "Signature type: Repository"

SCENARIOS, FAMILIES, ADDITIONS = preparation.SCENARIOS, preparation.FAMILIES, preparation.ADDITIONS
ROLES = runtime.ROLES
PACKAGE_LANES = ("candidate-packages", "rollback-packages")
RECOVERY_LANES = ("candidate-restore", "rollback-restore", "container-cleanup")
INVENTORIES = {"scenarios": SCENARIOS, "families": FAMILIES, "additions": ADDITIONS, "packages": PACKAGE_LANES,
               "recovery": RECOVERY_LANES}
ACCEPTED_SCOPES = {"scenarios": ("published-package", "operational"), "families": ("published-package", "operational"),
                   "additions": ("published-package", "operational"), "packages": ("published-package",),
                   "recovery": ("operational",)}
LANE_SCOPES = ("tooling-synthetic", "published-package", "operational")
DECISION_ROLES = ("eventstore-owner", "builds-owner", "solution-owner", "test-owner")
FREEZE = "mutation-freeze-and-forward-recovery"
USABILITY_AUTHORITY = ("separately validated coordinated fixed-record-v1/guard/catalog/Stack/pin transition "
                       "after P0/P2/P3/P4 and independent readiness")
DOWNSTREAM_GATES = {
    "fixed-p1r-acceptance-v1": "retained", "p0-stage-1": "retained", "p2": "retained", "p3": "retained",
    "p4": "retained", "g-6": "retained", "dw-35": "retained", "dw-68": "retained",
    "independent-readiness": "NOT_READY", "story-6.1": "blocked", "story-8.11": "terminal-release-decision",
    "publication": "separately-authorized", "deployment": "separately-authorized", "pins": "unchanged",
}
R_INPUTS = "owner execution inputs missing"
R_NOT_EXECUTED = "execution evidence not supplied in this invocation"
R_NOT_SELECTED = "addition not selected into the supported envelope"
R_NO_ROLLBACK = "capable rollback not selected; mutation freeze and forward recovery retained"
# Corrupt, encrypted or unsupported archive entries raise these; they are retained as failed checks.
ARCHIVE_ERRORS = (zipfile.BadZipFile, ET.ParseError, KeyError, OSError, ValueError, zlib.error, RuntimeError, EOFError,
                  NotImplementedError)
PACKET_KEYS = ("schema", "invocation", "started_utc", "finished_utc", "source_binding", "source_binding_sha256", "inputs",
               "decisions", "refusals", "lanes", "process_controls", "evaluation", "downstream_gates", "errors")
ROW_KEYS = ("id", "execution", "compatibility", "scope", "assertions", "receipts", "reason")
INPUT_KEYS = ("schema", "fixture", "authority", "candidate", "rollback", "operational_profile",
              "assertion_instrumentation", "selected_additions")
SELECTION_KEYS = ("version", "tag", "tag_commit", "builds", "feed", "packages")
SELECTED_PACKAGE_KEYS = ("id", "archive_sha256", "content_hash", "repository_commit")
DECISION_KEYS = ("schema", "fixture", "inputs_sha256", "decisions", "conformance")
EVIDENCE_KEYS = ("schema", "role", "fixture", "packages_root", "configuration", "projects")
PROJECT_KEYS = ("name", "assets", "lock", "loaded", "output")
LANE_KEYS = ("schema", "id", "lane", "scope", "fixture", "inputs_sha256", "instrumentation", "profile", "argv", "cwd",
             "started_utc", "finished_utc", "exit_code", "output_sha256", "identities", "cases", "assertions",
             "execution", "compatibility")
CASE_KEYS = ("id", "operation", "expected", "outcome", "disposition", "inventory", "checks", "assertions")
HEX32, HEX64 = runtime.HEX32, runtime.HEX64
HEX40 = re.compile(r"[0-9a-f]{40}")
SEMVER = re.compile(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?")
CONTENT_HASH = re.compile(r"[A-Za-z0-9+/]{86}==")
DATE = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}")
NAME = re.compile(r"[A-Za-z0-9_.-]{1,128}")
text, pattern = runtime.text, runtime.pattern


def parse_json(data):
    """Parse exact retained bytes; duplicate members and nonfinite numbers are refused."""
    def pairs(items):
        value = {}
        for key, item in items:
            require(key not in value, "duplicate JSON member")
            value[key] = item
        return value
    try:
        value = json.loads(data, object_pairs_hook=pairs,
                           parse_constant=lambda _: (_ for _ in ()).throw(InvalidPacket("nonfinite JSON number")))
    except (UnicodeError, json.JSONDecodeError) as error:
        raise InvalidPacket("unreadable JSON input") from error
    require(isinstance(value, dict), "JSON root must be an object")
    return value


def date(value, reason):
    pattern(value, DATE, reason)
    try:
        dt.date.fromisoformat(value)
    except ValueError as error:
        raise InvalidPacket(reason) from error
    return value


def is_eventstore(name):
    return isinstance(name, str) and release.is_eventstore_package_id(name)


# Owner inputs and decisions -------------------------------------------------------------------------------------

def validate_selection(value, role):
    reason = f"invalid {role} selection"
    members(value, SELECTION_KEYS, reason)
    pattern(value["version"], SEMVER, reason)
    require(value["tag"] == "v" + value["version"], f"{role} tag differs from its version")
    pattern(value["tag_commit"], HEX40, reason)
    members(value["builds"], ("version", "commit"), reason)
    pattern(value["builds"]["version"], SEMVER, reason)
    pattern(value["builds"]["commit"], HEX40, reason)
    require(text(value["feed"], reason).startswith("https://"), f"{role} feed must be an https package source")
    require(isinstance(value["packages"], list) and value["packages"], reason)
    seen = set()
    for package in value["packages"]:
        members(package, SELECTED_PACKAGE_KEYS, reason)
        require(is_eventstore(pattern(package["id"], NAME, reason)), f"{role} package outside EventStore scope")
        require(package["id"].casefold() not in seen, f"duplicate {role} package selection")
        seen.add(package["id"].casefold())
        pattern(package["archive_sha256"], HEX64, reason)
        pattern(package["content_hash"], CONTENT_HASH, reason)
        pattern(package["repository_commit"], HEX40, reason)


def validate_inputs(value):
    """Validate owner execution inputs; returns their scope. Synthetic fixtures select no published tuple."""
    members(value, INPUT_KEYS, "owner input members differ from contract")
    require(value["schema"] == INPUTS_SCHEMA, "unsupported owner input contract")
    require(value["fixture"] in (None, SYNTHETIC), "unknown fixture marker")
    members(value["authority"], ("owner", "reference", "date"), "invalid execution authority")
    text(value["authority"]["owner"], "invalid execution authority")
    text(value["authority"]["reference"], "invalid execution authority")
    date(value["authority"]["date"], "invalid execution authority")
    require(value["candidate"] is not None, "candidate selection missing")
    validate_selection(value["candidate"], "candidate")
    if value["rollback"] is not None:
        validate_selection(value["rollback"], "rollback")
        require(value["rollback"]["version"] != value["candidate"]["version"], "rollback must differ from the candidate")
    if value["operational_profile"] is not None:
        runtime.validate_profile(value["operational_profile"])
    if value["assertion_instrumentation"] is not None:
        instrumentation = value["assertion_instrumentation"]
        members(instrumentation, ("mechanism", "accepted_by", "reference"), "invalid assertion instrumentation")
        text(instrumentation["mechanism"], "invalid assertion instrumentation")
        text(instrumentation["reference"], "invalid assertion instrumentation")
        require(instrumentation["accepted_by"] == "test-owner", "assertion instrumentation lacks Test owner acceptance")
    additions = value["selected_additions"]
    require(isinstance(additions, list) and additions == [name for name in ADDITIONS if name in additions],
            "unknown, duplicate or reordered selected additions")
    return "tooling-synthetic" if value["fixture"] == SYNTHETIC else "owner-selected"


def validate_decisions(value):
    members(value, DECISION_KEYS, "owner decision members differ from contract")
    require(value["schema"] == DECISIONS_SCHEMA, "unsupported owner decision contract")
    require(value["fixture"] in (None, SYNTHETIC), "unknown fixture marker")
    pattern(value["inputs_sha256"], HEX64, "invalid decision input binding")
    rows = value["decisions"]
    require(isinstance(rows, list) and all(isinstance(row, dict) for row in rows)
            and [row.get("role") for row in rows] == list(DECISION_ROLES), "missing, duplicate or reordered owner decision")
    for row in rows:
        members(row, ("role", "decision", "owner", "date", "reference"), "invalid owner decision")
        require(row["decision"] in ("approved", "rejected", "pending"), "invalid owner decision")
        for key in ("owner", "reference"):
            require((row["decision"] == "pending" and row[key] is None) or text(row[key], "invalid owner decision"),
                    "invalid owner decision")
        require((row["decision"] == "pending" and row["date"] is None) or date(row["date"], "invalid owner decision"),
                "invalid owner decision")
    conformance = value["conformance"]
    members(conformance, ("status", "baseline", "reference"), "invalid same-baseline conformance")
    require(conformance["status"] in ("conformant", "nonconformant", "pending"), "invalid same-baseline conformance")
    if conformance["status"] == "pending":
        require(conformance["baseline"] is None and conformance["reference"] is None, "invalid same-baseline conformance")
    else:
        pattern(conformance["baseline"], HEX40, "invalid same-baseline conformance")
        text(conformance["reference"], "invalid same-baseline conformance")
    return "tooling-synthetic" if value["fixture"] == SYNTHETIC else "owner-selected"


def load_document(path, validator):
    data = regular(Path(path).absolute())
    value = parse_json(data)
    return value, data, validator(value)


def decisions_complete(inputs, decisions):
    if inputs is None or decisions is None:
        return False
    value = decisions["value"]
    return (inputs["scope"] == "owner-selected" and decisions["scope"] == "owner-selected"
            and value["inputs_sha256"] == inputs["sha256"]
            and all(row["decision"] == "approved" for row in value["decisions"])
            and value["conformance"]["status"] == "conformant")


def refusals(inputs, decisions):
    """Deterministic list of missing execution and acceptance inputs."""
    values = []
    if inputs is None:
        values.append(R_INPUTS)
    else:
        selection = inputs["value"]
        if inputs["scope"] != "owner-selected":
            values.append("synthetic tooling inputs select no published tuple")
        if selection["rollback"] is None:
            values.append(R_NO_ROLLBACK)
        if selection["operational_profile"] is None:
            values.append("operational profile not selected")
        if selection["assertion_instrumentation"] is None:
            values.append("assertion instrumentation not accepted")
    if decisions is None:
        values.append("owner decisions and same-baseline conformance missing")
    else:
        value = decisions["value"]
        if decisions["scope"] != "owner-selected":
            values.append("synthetic tooling decisions grant no acceptance")
        if inputs is None or value["inputs_sha256"] != inputs["sha256"]:
            values.append("owner decisions bind different execution inputs")
        values.extend(f"{row['role']} decision {row['decision']}" for row in value["decisions"] if row["decision"] != "approved")
        if value["conformance"]["status"] != "conformant":
            values.append("same-baseline conformance " + value["conformance"]["status"])
    return values


# Lane inventory and evaluation ----------------------------------------------------------------------------------

def unavailable_row(group, name, selection):
    if selection is None:
        reason = R_INPUTS
    elif group == "additions" and name not in selection["selected_additions"]:
        reason = R_NOT_SELECTED
    elif name in ("rollback-packages", "rollback-restore") and selection["rollback"] is None:
        reason = R_NO_ROLLBACK
    else:
        reason = R_NOT_EXECUTED
    return {"id": name, "execution": "unavailable", "compatibility": "unverified", "scope": None, "assertions": None,
            "receipts": [], "reason": reason}


def initial_lanes(selection):
    return {group: [unavailable_row(group, name, selection) for name in names] for group, names in INVENTORIES.items()}


def mirror_families(lanes):
    by_id = {row["id"]: row for row in lanes["scenarios"]}
    lanes["families"] = [dict(by_id[name]) for name in FAMILIES]


def required_lanes(selection):
    required = [("scenarios", name) for name in SCENARIOS] + [("families", name) for name in FAMILIES]
    required += [("additions", name) for name in ADDITIONS if selection is None or name in selection["selected_additions"]]
    required += [("packages", "candidate-packages"), ("recovery", "candidate-restore"), ("recovery", "container-cleanup")]
    if selection is None or selection["rollback"] is not None:
        required += [("packages", "rollback-packages"), ("recovery", "rollback-restore")]
    return required


def satisfied(group, row):
    counter = row["assertions"]
    return (row["execution"] == "passed" and row["compatibility"] == "compatible"
            and row["scope"] in ACCEPTED_SCOPES[group] and bool(row["receipts"])
            and isinstance(counter, dict) and counter["attempted"] > 0 and counter["failed"] == 0)


def evaluate(lanes, inputs, decisions):
    """Independent technical evaluation; usability is never granted here."""
    selection = inputs["value"] if inputs is not None else None
    rows = {group: {row["id"]: row for row in lanes[group]} for group in INVENTORIES}
    required = required_lanes(selection)
    unsatisfied = [f"{group}/{name}" for group, name in required if not satisfied(group, rows[group][name])]
    owner_selected = inputs is not None and inputs["scope"] == "owner-selected"
    technical = owner_selected and selection["assertion_instrumentation"] is not None and not unsatisfied
    complete = decisions_complete(inputs, decisions)
    rollback = (technical and complete and selection["rollback"] is not None
                and satisfied("packages", rows["packages"]["rollback-packages"])
                and satisfied("recovery", rows["recovery"]["rollback-restore"]))
    return {"technically_qualified": technical, "decisions_complete": complete, "qualified": technical and complete,
            "capable_rollback_qualified": rollback, "recovery": FREEZE, "p1r_usable": False,
            "usability_authority": USABILITY_AUTHORITY, "required_lanes": len(required), "unsatisfied": unsatisfied}


def executed_row(name, outcome, receipt_path):
    return {"id": name, "execution": outcome["execution"], "compatibility": outcome["compatibility"],
            "scope": outcome["scope"], "assertions": outcome["assertions"], "receipts": [receipt_path], "reason": None}


# Package archive, provenance, graph and loaded-binary checks ------------------------------------------------------

def default_verifier(archive, output, cwd):
    """Run the actual NuGet signature verifier and retain its literal output bytes."""
    argv = ["dotnet", "nuget", "verify", "--all", str(archive), "--verbosity", "minimal"]
    started = stamp()
    try:
        result = subprocess.run(argv, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                stdin=subprocess.DEVNULL, timeout=180, check=False)
        exit_code, data = result.returncode, result.stdout
    except FileNotFoundError:
        exit_code, data = None, b"signature verifier unavailable\n"
    except subprocess.TimeoutExpired as error:
        exit_code, data = None, (error.stdout or b"") + b"\nsignature verifier timed out\n"
    with output.open("xb") as handle:
        handle.write(data)
    return {"verifier": VERIFIER, "argv": argv, "cwd": str(cwd), "started_utc": started, "finished_utc": stamp(),
            "exit_code": exit_code}


def inspect_archive(path, data):
    """Return nuspec identity/provenance and archive-entry observations; unsafe archives are recorded, not trusted."""
    nuspec = {"valid": False, "error": None, "id": None, "version": None, "repository_commit": None,
              "repository_url": None, "synthetic_marker": False}
    entries = {"duplicates": None, "signature": False, "synthetic_marker": False}
    dlls = {}
    try:
        metadata = release.read_package_metadata(path)
        nuspec.update(valid=True, id=metadata.package_id, version=metadata.version)
    except ARCHIVE_ERRORS as error:
        nuspec["error"] = (type(error).__name__ + ": " + str(error))[:300]
    try:
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            names = archive.namelist()
            entries.update(duplicates=len(names) != len(set(names)), signature=".signature.p7s" in names,
                           synthetic_marker=MARKER_ENTRY in names)
            for name in sorted(set(names)):
                if name.casefold().endswith(".dll"):
                    dlls[name] = digest(archive.read(name))
            specs = [name for name in names if "/" not in name and name.casefold().endswith(".nuspec")]
            if len(specs) == 1:
                root = ET.fromstring(archive.read(specs[0]))
                repository = root.find("{*}metadata/{*}repository")
                if repository is None:
                    repository = root.find("metadata/repository")
                if repository is not None:
                    nuspec.update(repository_commit=repository.attrib.get("commit"),
                                  repository_url=repository.attrib.get("url"))
                tags = root.findtext("{*}metadata/{*}tags") or root.findtext("metadata/tags") or ""
                nuspec["synthetic_marker"] = SYNTHETIC in tags.split()
    except ARCHIVE_ERRORS as error:
        nuspec["error"] = nuspec["error"] or (type(error).__name__ + ": " + str(error))[:300]
        nuspec["valid"] = False
    return nuspec, entries, dlls


def observe_archive(packages_root, package, version, retained, index, receipt_name, directory, verifier):
    lower = package["id"].lower()
    archive_name = f"{lower}/{version.lower()}/{lower}.{version.lower()}.nupkg"
    row = {"id": package["id"], "archive": archive_name, "state": "missing", "sha256": None, "bytes": None,
           "nuspec": None, "entries": None, "dlls": {}, "metadata": None, "signature": None}
    path = packages_root / archive_name
    try:
        data = regular(path)
    except InvalidPacket:
        row["state"] = "substituted" if path.is_symlink() or path.exists() else "missing"
        return row
    row.update(state="regular", sha256=digest(data), bytes=len(data))
    row["nuspec"], row["entries"], row["dlls"] = inspect_archive(path, data)
    require(digest(regular(path)) == row["sha256"], "package archive changed during observation")
    try:
        metadata = parse_json(regular(path.parent / ".nupkg.metadata"))
        row["metadata"] = {"contentHash": metadata.get("contentHash"), "source": metadata.get("source")}
    except InvalidPacket:
        row["metadata"] = None
    output = retained / f"signature-{index}.txt"
    result = verifier(path, output, directory / "receipts")
    row["signature"] = {**result, "verifier": VERIFIER if verifier is default_verifier else "injected-verifier",
                        "output": {"path": f"{receipt_name}/signature-{index}.txt", "sha256": digest(regular(output))}}
    return row


def observe_loaded_file(root, value):
    row = {"path": None, "state": "invalid", "sha256": None}
    if not isinstance(value, str) or not Path(value).is_absolute():
        return row
    path = Path(value)
    try:
        row["path"] = path.relative_to(root).as_posix()
    except ValueError:
        row["state"] = "outside"
        return row
    try:
        row.update(state="regular", sha256=digest(regular(path)))
    except InvalidPacket:
        row["state"] = "substituted" if path.is_symlink() or path.exists() else "missing"
    return row


def observe_project(root, packages_root, project, retained, receipt_name):
    name = project["name"]
    target = retained / name
    target.mkdir()
    row = {"name": name, "output": project["output"], "packages_path_isolated": False, "loaded_files": []}
    copies = {}
    for key, filename in (("assets", "project.assets.json"), ("lock", "packages.lock.json"), ("loaded", "loaded.json")):
        data = regular(root / relative(project[key]))
        copies[key] = parse_json(data)
        (target / filename).write_bytes(data)
        row[key] = {"path": f"{receipt_name}/{name}/{filename}", "sha256": digest(data)}
    restore = copies["assets"].get("project", {}).get("restore", {}) if isinstance(copies["assets"].get("project"), dict) else {}
    packages_path = restore.get("packagesPath") if isinstance(restore, dict) else None
    row["packages_path_isolated"] = (isinstance(packages_path, str) and Path(packages_path).is_absolute()
                                     and Path(packages_path).resolve() == packages_root.resolve())
    assemblies = copies["loaded"].get("assemblies")
    for assembly in assemblies if isinstance(assemblies, list) else []:
        if isinstance(assembly, dict) and is_eventstore(assembly.get("name")):
            row["loaded_files"].append(observe_loaded_file(root, assembly.get("path")))
    return row


def observe_package_evidence(evidence, role, selection, directory, receipt_id, verifier):
    """Physically observe one package-evidence directory; retains graph/loaded copies for revalidation."""
    root = Path(evidence).absolute()
    require(root.is_dir() and root == root.resolve(), "package evidence path unavailable or substituted")
    manifest_data = regular(root / "evidence.json")
    manifest = parse_json(manifest_data)
    members(manifest, EVIDENCE_KEYS, "package evidence members differ from contract")
    require(manifest["schema"] == EVIDENCE_SCHEMA and manifest["role"] == role, "package evidence role or contract differs")
    require(manifest["fixture"] in (None, SYNTHETIC), "unknown fixture marker")
    text(manifest["configuration"], "invalid package evidence configuration")
    packages_root = root / relative(manifest["packages_root"])
    require(packages_root.is_dir() and packages_root == packages_root.resolve(), "isolated package folder unavailable or substituted")
    projects = manifest["projects"]
    require(isinstance(projects, list) and projects, "package evidence lists no consumer project")
    names = []
    for project in projects:
        members(project, PROJECT_KEYS, "consumer project members differ from contract")
        names.append(pattern(project["name"], NAME, "invalid consumer project name"))
        for key in ("assets", "lock", "loaded", "output"):
            relative(project[key])
    require(len(names) == len(set(names)), "duplicate consumer project")
    receipt_name = f"receipts/{receipt_id}"
    retained = directory / receipt_name
    retained.mkdir()
    (retained / "evidence.json").write_bytes(manifest_data)
    packages = [observe_archive(packages_root, package, selection["version"], retained, index, receipt_name, directory, verifier)
                for index, package in enumerate(selection["packages"])]
    return {"evidence_root": str(root), "evidence": {"path": f"{receipt_name}/evidence.json", "sha256": digest(manifest_data)},
            "fixture": manifest["fixture"], "configuration": manifest["configuration"],
            "packages_root": manifest["packages_root"], "packages": packages,
            "projects": [observe_project(root, packages_root, project, retained, receipt_name) for project in projects]}


def bind_evidence_copy(directory, observation, role):
    """The retained evidence manifest fixes which consumer projects and folders were observed."""
    data = regular(directory / relative(observation["evidence"]["path"]))
    require(digest(data) == observation["evidence"]["sha256"], "retained package evidence manifest hash mismatch")
    manifest = parse_json(data)
    members(manifest, EVIDENCE_KEYS, "package evidence members differ from contract")
    require(manifest["schema"] == EVIDENCE_SCHEMA and manifest["role"] == role
            and [manifest[key] for key in ("fixture", "configuration", "packages_root")]
            == [observation[key] for key in ("fixture", "configuration", "packages_root")]
            and isinstance(manifest["projects"], list)
            and [(p.get("name"), p.get("output")) for p in manifest["projects"] if isinstance(p, dict)]
            == [(p["name"], p["output"]) for p in observation["projects"]],
            "package observations differ from the retained evidence manifest")
    return observation["evidence"]["path"]


def library_rows(assets):
    libraries = assets["libraries"]
    require(isinstance(libraries, dict), "malformed restore graph")
    rows = []
    for key, library in libraries.items():
        name, version = key.split("/")
        rows.append((name, version, library))
    return rows


def graph_checks(prefix, assets, lock, selection):
    """Assets/lock agreement, package-only EventStore resolution and exact selected versions/content hashes."""
    def safely(function):
        try:
            return bool(function())
        except (TypeError, KeyError, AttributeError, ValueError):
            return False
    selected = {package["id"]: package for package in selection["packages"]}

    def lock_entries():
        require(lock["version"] in (1, 2) and isinstance(lock["dependencies"], dict) and lock["dependencies"], "")
        entries = {}
        for framework in lock["dependencies"].values():
            for name, entry in framework.items():
                entries.setdefault(name.casefold(), []).append(entry)
        return entries

    def coverage():
        names = {name.casefold() for name, _, _ in library_rows(assets)}
        return names and names == set(lock_entries())

    def agreement():
        entries = lock_entries()
        for name, version, library in library_rows(assets):
            for entry in entries[name.casefold()]:
                if library["type"] == "package":
                    require(entry["resolved"] == version and entry["contentHash"] == library["sha512"]
                            and entry["type"] in ("Direct", "Transitive", "CentralTransitive"), "")
                else:
                    require(library["type"] == "project" and entry["type"] == "Project", "")
        return True

    def eventstore():
        return [(name, version, library) for name, version, library in library_rows(assets) if is_eventstore(name)]

    checks = [
        (prefix + "lock-covers-restore-graph", safely(coverage)),
        (prefix + "lock-agrees-with-restore-graph", safely(agreement)),
        (prefix + "no-eventstore-source-dependency", safely(lambda: all(library["type"] == "package" for _, _, library in eventstore()))),
        (prefix + "eventstore-packages-consumed", safely(lambda: bool(eventstore()))),
        (prefix + "selected-versions-and-content", safely(lambda: all(
            name in selected and version == selection["version"] and library["sha512"] == selected[name]["content_hash"]
            for name, version, library in eventstore()))),
    ]
    try:
        identifiers = [name for name, _, _ in eventstore()]
    except (TypeError, KeyError, AttributeError, ValueError):
        identifiers = []
    return checks, identifiers


def loaded_checks(prefix, project, loaded, graph_ids, archives, packages_root):
    """Physically loaded EventStore assemblies must be the verified archive bytes from the isolated Release lane."""
    try:
        assemblies = [a for a in loaded["assemblies"] if isinstance(a, dict) and is_eventstore(a.get("name"))]
        files = project["loaded_files"]
        require(isinstance(files, list) and len(files) == len(assemblies)
                and all(isinstance(a.get("sha256"), str) and isinstance(row, dict) for a, row in zip(assemblies, files)), "")
    except (TypeError, KeyError, AttributeError, ValueError):
        return [(prefix + name, False) for name in ("loaded-eventstore-assemblies", "loaded-verified-archive-bytes",
                                                     "loaded-from-selected-graph", "loaded-release-isolated-paths")]
    output = PurePosixPath(project["output"])
    providers = {}
    for package in archives:
        for entry, value in package["dlls"].items():
            providers.setdefault(PurePosixPath(entry).name.casefold(), []).append((package["id"], value))

    def provided(assembly):
        return providers.get((str(assembly["name"]) + ".dll").casefold(), [])

    def isolated(row):
        if row.get("state") != "regular" or not isinstance(row.get("path"), str):
            return False
        path = PurePosixPath(row["path"])
        within = path.is_relative_to(output) or path.is_relative_to(PurePosixPath(packages_root))
        return within and not {"Debug", "obj"} & set(path.parts)

    return [
        (prefix + "loaded-eventstore-assemblies", bool(assemblies)),
        (prefix + "loaded-verified-archive-bytes", all(
            row.get("state") == "regular" and row.get("sha256") == assembly["sha256"]
            and any(value == assembly["sha256"] for _, value in provided(assembly))
            for row, assembly in zip(files, assemblies))),
        (prefix + "loaded-from-selected-graph", all(
            any(package_id in graph_ids for package_id, _ in provided(assembly)) for assembly in assemblies)),
        (prefix + "loaded-release-isolated-paths", bool(assemblies) and all(isolated(row) for row in files)),
    ]


def signature_verified(directory, signature):
    if not isinstance(signature, dict) or signature.get("exit_code") != 0:
        return False
    output = signature["output"]
    data = regular(directory / relative(output["path"]))
    require(digest(data) == output["sha256"], "signature verifier output hash mismatch")
    return REPOSITORY_SIGNATURE in data.decode("utf-8", "replace")


def package_checks(directory, observation, selection):
    """Recompute every package check from bound observations and retained graph/loaded copies."""
    require([package["id"] for package in observation["packages"]] == [package["id"] for package in selection["packages"]],
            "package observations differ from the selection")
    checks = []
    for package, selected in zip(observation["packages"], selection["packages"]):
        prefix = selected["id"] + ":"
        nuspec = package["nuspec"] or {}
        entries = package["entries"] or {}
        metadata = package["metadata"] or {}
        for name, value in (
            ("archive-regular", package["state"] == "regular"),
            ("archive-sha256", package["sha256"] == selected["archive_sha256"]),
            ("nuspec-contract", nuspec.get("valid") is True),
            ("nuspec-identity", nuspec.get("id") == selected["id"] and nuspec.get("version") == selection["version"]),
            ("repository-commit", nuspec.get("repository_commit") == selected["repository_commit"]),
            ("unique-archive-entries", entries.get("duplicates") is False),
            ("signature-entry", entries.get("signature") is True),
            ("repository-signature-verified", signature_verified(directory, package["signature"])),
            ("content-hash", metadata.get("contentHash") == selected["content_hash"]),
            ("restore-source", metadata.get("source") == selection["feed"]),
            ("assemblies-present", bool(package["dlls"])),
        ):
            observe(checks, prefix + name, value)
    verified = [package for package, selected in zip(observation["packages"], selection["packages"])
                if package["state"] == "regular" and package["sha256"] == selected["archive_sha256"]]
    consumed = set()
    for project in observation["projects"]:
        prefix = project["name"] + ":"
        copies = {}
        for key in ("assets", "lock", "loaded"):
            data = regular(directory / relative(project[key]["path"]))
            require(digest(data) == project[key]["sha256"], "retained consumer evidence hash mismatch")
            copies[key] = parse_json(data)
        graph, graph_ids = graph_checks(prefix, copies["assets"], copies["lock"], selection)
        consumed.update(graph_ids)
        output = PurePosixPath(project["output"]).parts
        rows = graph + [
            (prefix + "isolated-package-folder", project["packages_path_isolated"] is True),
            (prefix + "release-configuration", observation["configuration"] == "Release" and "Release" in output
             and "Debug" not in output),
        ] + loaded_checks(prefix, project, copies["loaded"], set(graph_ids), verified, observation["packages_root"])
        for name, value in rows:
            observe(checks, name, value)
    observe(checks, "selected-packages-consumed", all(package["id"] in consumed for package in selection["packages"]))
    return checks


def package_scope(observation, inputs):
    # A missing archive is a failed published observation, not synthetic evidence; any marker or
    # non-actual signature verifier keeps the whole lane tooling-only.
    synthetic = (inputs["scope"] != "owner-selected" or observation["fixture"] is not None
                 or any((package["entries"] or {}).get("synthetic_marker") or (package["nuspec"] or {}).get("synthetic_marker")
                        or (package["signature"] is not None and package["signature"].get("verifier") != VERIFIER)
                        for package in observation["packages"]))
    return "tooling-synthetic" if synthetic else "published-package"


def package_outcome(directory, receipt, selection, inputs):
    checks = package_checks(directory, receipt["observation"], selection)
    counter = checks_counter(checks)
    scope = package_scope(receipt["observation"], inputs)
    passed = counter["attempted"] > 0 and counter["failed"] == 0
    execution = "passed" if passed else "failed"
    compatibility = "unverified" if scope != "published-package" else ("compatible" if passed else "incompatible")
    return checks, {"execution": execution, "compatibility": compatibility, "scope": scope, "assertions": counter}


def verified_identities(receipt, outcome):
    """Archive and DLL identities usable by dependent lanes only after a passing published package lane."""
    if outcome["scope"] != "published-package" or outcome["execution"] != "passed":
        return None
    packages = receipt["observation"]["packages"]
    return {"archives": {package["id"]: package["sha256"] for package in packages},
            "dlls": {value for package in packages for value in package["dlls"].values()}}


def execute_package_lane(directory, invocation, role, evidence, inputs, verifier):
    selection = inputs["value"][role]
    receipt_id = uuid.uuid4().hex
    receipt = {"schema": PACKAGE_RECEIPT_SCHEMA, "id": receipt_id, "invocation": invocation, "role": role,
               "started_utc": stamp(), "finished_utc": None, "observation": None, "checks": [], "assertions": None,
               "execution": "failed", "compatibility": "unverified", "scope": None}
    receipt["observation"] = observe_package_evidence(evidence, role, selection, directory, receipt_id, verifier)
    checks, outcome = package_outcome(directory, receipt, selection, inputs)
    receipt.update(finished_utc=stamp(), checks=checks, **outcome)
    write_json(directory / "receipts" / (receipt_id + ".json"), receipt)
    return executed_row(role + "-packages", outcome, f"receipts/{receipt_id}.json"), verified_identities(receipt, outcome)


def validate_package_receipt(directory, invocation, role, path, inputs, start, end):
    receipt = parse_json(regular(directory / path))
    members(receipt, ("schema", "id", "invocation", "role", "started_utc", "finished_utc", "observation", "checks",
                      "assertions", "execution", "compatibility", "scope"), "package receipt members differ")
    require(receipt["schema"] == PACKAGE_RECEIPT_SCHEMA and receipt["invocation"] == invocation and receipt["role"] == role
            and path == f"receipts/{receipt['id']}.json" and HEX32.fullmatch(receipt["id"]), "package receipt binding differs")
    receipt_start, receipt_end = validate_times(receipt)
    require(start <= receipt_start <= receipt_end <= end, "package receipt interval outside invocation")
    try:
        observation = receipt["observation"]
        retained = {bind_evidence_copy(directory, observation, role)}
        checks, outcome = package_outcome(directory, receipt, inputs["value"][role], inputs)
        retained |= {project[key]["path"] for project in observation["projects"] for key in ("assets", "lock", "loaded")}
        retained |= {package["signature"]["output"]["path"] for package in observation["packages"] if package["signature"]}
        prefix = f"receipts/{receipt['id']}/"
        require(all(name.startswith(prefix) for name in retained), "retained package evidence escapes its receipt")
    except (TypeError, KeyError, AttributeError, IndexError) as error:
        raise InvalidPacket("malformed package observation") from error
    require(receipt["checks"] == checks, "claimed package observations differ from bound evidence")
    require({key: receipt[key] for key in outcome} == outcome, "claimed package outcome differs from bound evidence")
    return receipt, outcome, retained


# Imported lane receipts -----------------------------------------------------------------------------------------

def lane_outcome(receipt):
    """Validate one scenario/addition lane receipt contract and derive its outcome from its cases."""
    members(receipt, LANE_KEYS, "lane receipt members differ from contract")
    require(receipt["schema"] == LANE_RECEIPT_SCHEMA, "unsupported receipt contract")
    pattern(receipt["id"], HEX32, "invalid receipt identity")
    require(receipt["lane"] in SCENARIOS + ADDITIONS, "unknown qualification lane")
    scope = receipt["scope"]
    require(scope in LANE_SCOPES, "unsupported evidence scope")
    runtime.validate_fixture(receipt["fixture"], scope)
    require(receipt["inputs_sha256"] is None or (isinstance(receipt["inputs_sha256"], str) and HEX64.fullmatch(receipt["inputs_sha256"])),
            "invalid input binding")
    require(receipt["instrumentation"] is None or text(receipt["instrumentation"], "invalid instrumentation"), "invalid instrumentation")
    if receipt["profile"] is not None:
        runtime.validate_profile(receipt["profile"])
    require(isinstance(receipt["argv"], list) and receipt["argv"] and all(isinstance(a, str) and a for a in receipt["argv"]),
            "missing literal command")
    text(receipt["cwd"], "missing working directory")
    validate_times(receipt)
    require(type(receipt["exit_code"]) is int, "missing exit status")
    pattern(receipt["output_sha256"], HEX64, "missing output hash")
    identities = receipt["identities"]
    members(identities, ("configuration", "packages", "loaded_assemblies"), "invalid lane identities")
    require(identities["configuration"] in ("Release", "Debug"), "invalid lane identities")
    # A list of {id, sha256}: cross-version lanes bind the candidate and rollback archives of the same package id.
    require(isinstance(identities["packages"], list), "invalid lane identities")
    for package in identities["packages"]:
        members(package, ("id", "sha256"), "invalid lane identities")
        require(is_eventstore(package["id"]), "invalid lane identities")
        pattern(package["sha256"], HEX64, "invalid lane identities")
    require(len({(p["id"], p["sha256"]) for p in identities["packages"]}) == len(identities["packages"]),
            "duplicate lane package identity")
    require(isinstance(identities["loaded_assemblies"], list), "invalid lane identities")
    for assembly in identities["loaded_assemblies"]:
        members(assembly, ("name", "version", "path", "sha256"), "invalid loaded assembly identity")
        text(assembly["name"], "invalid loaded assembly identity")
        text(assembly["version"], "invalid loaded assembly identity")
        text(assembly["path"], "invalid loaded assembly identity")
        pattern(assembly["sha256"], HEX64, "invalid loaded assembly identity")
    cases = receipt["cases"]
    require(isinstance(cases, list) and cases, "lane receipt has no executed case")
    identifiers = set()
    passed, unsupported, incompatible, totals = True, False, False, []
    for case in cases:
        members(case, CASE_KEYS, "lane case members differ from contract")
        require(text(case["id"], "invalid case identity") not in identifiers, "duplicate lane case")
        identifiers.add(case["id"])
        require(case["operation"] in ("supported", "unsupported") and case["expected"] in ("effect", "refusal")
                and case["outcome"] in ("effect", "refusal", "error") and case["disposition"] in ("compatible", "incompatible"),
                "invalid lane case")
        if case["operation"] == "unsupported":
            # An unsupported operation can only be honestly refused; it is never a compatible execution.
            require(case["expected"] == "refusal", "unsupported operation cannot expect an effect")
            require(case["disposition"] == "incompatible", "unsupported operation claimed compatibility")
        members(case["inventory"], ("before_sha256", "after_sha256"), "missing persisted inventory binding")
        pattern(case["inventory"]["before_sha256"], HEX64, "missing persisted inventory binding")
        pattern(case["inventory"]["after_sha256"], HEX64, "missing persisted inventory binding")
        if case["assertions"] is None:
            require(case["checks"] == [], "unmeasured case cannot retain observations")
        else:
            preparation.validate_counter(case["assertions"], case["checks"])
        unchanged = case["inventory"]["before_sha256"] == case["inventory"]["after_sha256"]
        passed &= (case["assertions"] is not None and case["assertions"]["failed"] == 0 and case["outcome"] == case["expected"]
                   and (case["outcome"] != "refusal" or unchanged))
        unsupported |= case["operation"] == "unsupported"
        incompatible |= case["disposition"] == "incompatible"
        totals.append(case["assertions"])
    total = None if None in totals else {key: sum(counter[key] for counter in totals) for key in ("attempted", "passed", "failed")}
    require(receipt["assertions"] == total, "lane assertion counter differs from case observations")
    execution = "passed" if passed and receipt["exit_code"] == 0 and total is not None and total["attempted"] > 0 else "failed"
    require(receipt["execution"] == execution, "claimed lane outcome differs from bound evidence")
    if scope == "tooling-synthetic":
        compatibility = "unverified"
    elif execution == "passed":
        compatibility = "incompatible" if unsupported or incompatible else "compatible"
    else:
        compatibility = "incompatible" if unsupported or incompatible else "unverified"
    require(receipt["compatibility"] == compatibility, "claimed lane compatibility differs from bound evidence")
    return {"execution": execution, "compatibility": compatibility, "scope": scope, "assertions": total}


def require_owner_binding(receipt, context, reason):
    inputs = context["inputs"]
    require(inputs is not None and inputs["scope"] == "owner-selected" and receipt["inputs_sha256"] == inputs["sha256"], reason)


def bind_lane(receipt, outcome, context):
    selection = context["inputs"]["value"] if context["inputs"] else None
    if receipt["lane"] in ADDITIONS:
        require(selection is not None and receipt["lane"] in selection["selected_additions"], R_NOT_SELECTED)
    if outcome["scope"] == "tooling-synthetic":
        return
    require_owner_binding(receipt, context, "published or operational evidence lacks owner-selected inputs")
    instrumentation = selection["assertion_instrumentation"]
    require(instrumentation is not None and receipt["instrumentation"] == instrumentation["mechanism"],
            "lane assertions lack the accepted instrumentation")
    identities = receipt["identities"]
    candidate = context["verified"].get("candidate")
    archives = {(k, v) for verified in context["verified"].values() for k, v in verified["archives"].items()}
    dlls = {value for verified in context["verified"].values() for value in verified["dlls"]}
    packages = {(package["id"], package["sha256"]) for package in identities["packages"]}
    loaded = {assembly["sha256"] for assembly in identities["loaded_assemblies"]}
    # The candidate must be verified and fully bound; verified rollback archives may appear alongside it.
    require(candidate is not None and identities["configuration"] == "Release" and packages <= archives
            and set(candidate["archives"].items()) <= packages, "lane lacks verified published package binding")
    require(loaded and loaded <= dlls and loaded & candidate["dlls"], "lane loaded assemblies differ from verified archives")
    if outcome["scope"] == "operational":
        require(selection["operational_profile"] is not None and receipt["profile"] == selection["operational_profile"],
                "operational evidence lacks the selected profile")


def bind_recovery(receipt, outcome, lane, context):
    selection = context["inputs"]["value"] if context["inputs"] else None
    if lane == "rollback-restore":
        require(selection is not None and selection["rollback"] is not None, R_NO_ROLLBACK)
    if outcome["scope"] == "tooling-synthetic":
        return
    require_owner_binding(receipt, context, "operational evidence lacks owner-selected inputs")
    runtime.require_operational_profile(selection)
    require(receipt["profile"] == selection["operational_profile"], "operational evidence lacks the selected profile")
    if lane != "container-cleanup":
        verified = context["verified"].get(receipt["role"])
        require(verified is not None and receipt["packages"]
                and set(receipt["packages"].items()) <= set(verified["archives"].items()),
                "restore writer lacks verified published package binding")


def receipt_outcome(receipt, context):
    """Return (group, lane, outcome) for any importable receipt after contract and binding validation."""
    schema = receipt.get("schema")
    if schema == LANE_RECEIPT_SCHEMA:
        outcome = lane_outcome(receipt)
        bind_lane(receipt, outcome, context)
        return ("scenarios" if receipt["lane"] in SCENARIOS else "additions"), receipt["lane"], outcome
    if schema == runtime.RESTORE_SCHEMA:
        outcome = runtime.restore_outcome(receipt)
        lane = receipt["role"] + "-restore"
    elif schema == runtime.CLEANUP_SCHEMA:
        outcome = runtime.cleanup_outcome(receipt)
        lane = "container-cleanup"
    else:
        raise InvalidPacket("unsupported receipt contract")
    bind_recovery(receipt, outcome, lane, context)
    return "recovery", lane, {key: outcome[key] for key in ("execution", "compatibility", "scope", "assertions")}


def import_receipt(path, directory, lanes, context):
    data = regular(Path(path).absolute())
    receipt = parse_json(data)
    group, lane, outcome = receipt_outcome(receipt, context)
    name = f"receipts/{receipt['id']}.json"
    target = directory / name
    require(not target.exists(), "duplicate receipt identity")
    index = INVENTORIES[group].index(lane)
    require(not lanes[group][index]["receipts"], "duplicate lane evidence")
    with target.open("xb") as handle:
        handle.write(data)
    lanes[group][index] = executed_row(lane, outcome, name)


# Packet creation and independent validation ---------------------------------------------------------------------

def bound_document(directory, value, data, scope, name):
    (directory / name).write_bytes(data)
    return {"path": name, "sha256": digest(data), "scope": scope, "value": value}


def public(record):
    return None if record is None else {key: record[key] for key in ("path", "sha256", "scope")}


def create_packet(directory, inputs=None, decisions=None, candidate_evidence=None, rollback_evidence=None, receipts=(),
                  process_controls=False, root=ROOT, verifier=None):
    """Create a new sealed packet; refusals and interruptions are retained rather than defaulted."""
    directory = Path(directory).absolute()
    require(not directory.exists() and not directory.is_symlink(), "output path already exists")
    require(directory == directory.resolve(), "output parent path is substituted")
    directory.mkdir(parents=True, exist_ok=False)
    (directory / "receipts").mkdir()
    lanes = initial_lanes(None)
    packet = {"schema": SCHEMA, "invocation": uuid.uuid4().hex, "started_utc": stamp(), "finished_utc": None,
              "source_binding": "source-binding.json", "source_binding_sha256": None, "inputs": None, "decisions": None,
              "refusals": refusals(None, None), "lanes": lanes, "process_controls": None,
              "evaluation": evaluate(lanes, None, None), "downstream_gates": dict(DOWNSTREAM_GATES), "errors": []}
    write_json(directory / "packet.json", packet)  # Survives interruption before any observation.
    inputs_record = decisions_record = None
    try:
        dependent = (candidate_evidence, rollback_evidence, decisions, *receipts)
        require(inputs is not None or all(value is None for value in dependent),
                "dependent execution refused: " + R_INPUTS)
        binding = preparation.source_binding(root)
        write_json(directory / "source-binding.json", binding)
        packet["source_binding_sha256"] = digest(regular(directory / "source-binding.json"))
        if inputs is not None:
            value, data, scope = load_document(inputs, validate_inputs)
            inputs_record = bound_document(directory, value, data, scope, "inputs.json")
            packet["inputs"] = public(inputs_record)
            packet["lanes"] = lanes = initial_lanes(value)
        if decisions is not None:
            value, data, scope = load_document(decisions, validate_decisions)
            decisions_record = bound_document(directory, value, data, scope, "decisions.json")
            packet["decisions"] = public(decisions_record)
        require(rollback_evidence is None or inputs_record["value"]["rollback"] is not None,
                "rollback execution refused: " + R_NO_ROLLBACK)
        context = {"inputs": inputs_record, "verified": {}}
        for role, evidence in zip(ROLES, (candidate_evidence, rollback_evidence)):
            if evidence is None:
                continue
            preparation.check_current_source(binding)
            # Unreadable evidence manifests/graphs refuse the invocation; archive-level substitutions,
            # mismatches and graph defects are retained below as failed checks.
            row, verified = execute_package_lane(directory, packet["invocation"], role, evidence, inputs_record,
                                                 verifier or default_verifier)
            lanes["packages"][ROLES.index(role)] = row
            if verified is not None:
                context["verified"][role] = verified
            write_json(directory / "packet.json", packet)
        for path in receipts:
            import_receipt(path, directory, lanes, context)
            write_json(directory / "packet.json", packet)
        if process_controls:
            packet["process_controls"] = runtime.new_process_section()
            runtime.run_process_controls(packet["process_controls"], directory, packet["invocation"], binding["python"]["path"],
                                         lambda: preparation.check_current_source(binding),
                                         lambda: write_json(directory / "packet.json", packet))
        preparation.check_current_source(binding)
    except (Exception, KeyboardInterrupt) as error:  # Every failure is retained; nothing seals as complete.
        packet["errors"].append(str(error) if isinstance(error, InvalidPacket) else type(error).__name__)
    finally:
        mirror_families(lanes)
        packet["refusals"] = refusals(inputs_record, decisions_record)
        packet["evaluation"] = evaluate(lanes, inputs_record, decisions_record)
        packet["finished_utc"] = stamp()
        write_json(directory / "packet.json", packet)
        preparation.seal(directory)
    return packet


def verify_index(directory):
    expected = {}
    for line in regular(directory / "SHA256SUMS").decode().splitlines():
        require(re.fullmatch(r"[0-9a-f]{64}  .+", line) is not None, "invalid packet checksum index")
        value, name = line.split("  ", 1)
        relative(name)
        require(name not in expected and name != "SHA256SUMS", "duplicate packet input")
        expected[name] = value
    actual = {p.relative_to(directory).as_posix() for p in directory.rglob("*") if p.is_file() and p != directory / "SHA256SUMS"}
    require(actual == set(expected), "omitted or unexpected packet input")
    for name, value in expected.items():
        require(digest(regular(directory / name)) == value, "packet input hash mismatch")
    return actual


def validate_document(directory, record, name, validator):
    if record is None:
        require(not (directory / name).exists(), "unbound owner document")
        return None
    members(record, ("path", "sha256", "scope"), "owner document binding differs")
    require(record["path"] == name, "owner document binding differs")
    data = regular(directory / name)
    require(digest(data) == record["sha256"], "owner document hash mismatch")
    value = parse_json(data)
    require(validator(value) == record["scope"], "owner document scope differs")
    return {**record, "value": value}


def validate_unexecuted(group, row, selection):
    require(row == unavailable_row(group, row["id"], selection), "required qualification lane claims unbound evidence")


def validate_packet(directory):
    """Read-only independent validation; returns the recomputed evaluation, never usability."""
    directory = Path(directory).absolute()
    require(directory.is_dir() and directory == directory.resolve(), "packet directory unavailable or substituted")
    actual = verify_index(directory)
    packet = parse_json(regular(directory / "packet.json"))
    members(packet, PACKET_KEYS, "packet members differ from contract")
    require(packet["schema"] == SCHEMA, "unsupported packet contract")
    pattern(packet["invocation"], HEX32, "invalid invocation identity")
    start, end = validate_times(packet)
    require(isinstance(packet["evaluation"], dict) and packet["evaluation"].get("p1r_usable") is False, "invented qualification")
    require(packet["downstream_gates"] == DOWNSTREAM_GATES, "downstream gate decisions changed")
    require(isinstance(packet["errors"], list) and not packet["errors"], "refused or incomplete invocation")
    require(packet["source_binding"] == "source-binding.json", "unexpected source binding")
    require(digest(regular(directory / "source-binding.json")) == packet["source_binding_sha256"], "source binding hash mismatch")
    binding = parse_json(regular(directory / "source-binding.json"))
    require(binding.get("repository") == str(ROOT), "unauthorized source repository")
    preparation.check_current_source(binding)  # Independently reconstructs the bound source closure.
    allowed = {"packet.json", "source-binding.json"}
    inputs = validate_document(directory, packet["inputs"], "inputs.json", validate_inputs)
    decisions = validate_document(directory, packet["decisions"], "decisions.json", validate_decisions)
    allowed |= {record["path"] for record in (inputs, decisions) if record is not None}
    require(decisions is None or inputs is not None, "owner decisions lack execution inputs")
    require(packet["refusals"] == refusals(inputs, decisions), "refusals differ from bound inputs")
    selection = inputs["value"] if inputs is not None else None
    lanes = packet["lanes"]
    require(isinstance(lanes, dict) and set(lanes) == set(INVENTORIES), "lane groups differ from contract")
    for group, names in INVENTORIES.items():
        ordered_rows(lanes[group], names)
        for row in lanes[group]:
            members(row, ROW_KEYS, "lane row members differ from contract")
            require(isinstance(row["receipts"], list) and len(row["receipts"]) <= 1, "unbound lane evidence")
    by_id = {row["id"]: row for row in lanes["scenarios"]}
    require(lanes["families"] == [by_id[name] for name in FAMILIES], "family dispositions differ from their scenarios")
    require(inputs is not None or not any(row["receipts"] for group in INVENTORIES for row in lanes[group]),
            "dependent evidence lacks owner execution inputs")
    context = {"inputs": inputs, "verified": {}}
    for role, row in zip(ROLES, lanes["packages"]):
        if not row["receipts"]:
            validate_unexecuted("packages", row, selection)
            continue
        require(selection is not None and (role == "candidate" or selection["rollback"] is not None),
                "package evidence lacks its selection")
        receipt, outcome, retained = validate_package_receipt(directory, packet["invocation"], role, row["receipts"][0],
                                                              inputs, start, end)
        require(row == executed_row(role + "-packages", outcome, row["receipts"][0]), "package lane differs from its receipt")
        allowed |= retained | set(row["receipts"])
        verified = verified_identities(receipt, outcome)
        if verified is not None:
            context["verified"][role] = verified
    for group in ("scenarios", "additions", "recovery"):
        for row in lanes[group]:
            if not row["receipts"]:
                validate_unexecuted(group, row, selection)
                continue
            name = row["receipts"][0]
            receipt = parse_json(regular(directory / relative(name)))
            receipt_group, lane, outcome = receipt_outcome(receipt, context)
            require(receipt_group == group and lane == row["id"] and name == f"receipts/{receipt['id']}.json",
                    "lane receipt binding differs")
            require(row == executed_row(lane, outcome, name), "lane differs from its receipt")
            allowed.add(name)
    process_assertions = 0
    if packet["process_controls"] is not None:
        files, process_assertions = runtime.validate_process_controls(directory, packet["process_controls"], packet["invocation"],
                                                                      binding["python"]["path"], start, end)
        allowed |= files
    evaluation = evaluate(lanes, inputs, decisions)
    require(packet["evaluation"] == evaluation, "invented or stale qualification evaluation")
    require(actual == allowed, "unbound or extra packet input")
    package_assertions = sum(row["assertions"]["attempted"] for row in lanes["packages"] if row["assertions"])
    scopes = sorted({row["scope"] for group in INVENTORIES for row in lanes[group] if row["scope"]})
    return {"valid": True, **{key: evaluation[key] for key in ("technically_qualified", "decisions_complete", "qualified",
                                                               "capable_rollback_qualified", "recovery", "p1r_usable")},
            "unsatisfied": evaluation["unsatisfied"], "refusals": packet["refusals"], "evidence_scopes": scopes,
            "process_scope": "local-process-control" if packet["process_controls"] is not None else None,
            "process_assertions": process_assertions, "package_assertions": package_assertions}
