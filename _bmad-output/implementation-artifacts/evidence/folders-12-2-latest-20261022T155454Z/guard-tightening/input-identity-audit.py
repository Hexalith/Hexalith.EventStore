#!/usr/bin/env python3
"""Byte-authenticate the final test-only guard tightening and conditional producer reuse."""
from datetime import datetime, timezone
import difflib
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import sys

OUTPUT = Path(__file__).resolve().parent
PACKET = OUTPUT.parent
S1 = Path("/tmp/eventstore-folders-12-2-latest-20261022T155454Z")
S2 = Path("/tmp/eventstore-folders-12-2-guardfix-20261022T171346Z")
S3 = Path("/tmp/eventstore-folders-12-2-guardtight-20261022T173000Z")
ORIGIN = Path("/home/administrator/projects/hexalith/eventstore")
GUARD = Path("tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs")
CURSOR = GUARD.with_name("SecretsProtectionTests.UnifiedDiffCursor.cs")
EMPTY = GUARD.with_name("SecretsProtectionTests.EmptyCalls.cs")
UNIT = Path("tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0")
WORKER = Path("tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/qualified-Debug/net10.0")
WORKER_NAMES = ("Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
                "Hexalith.EventStore.Server", "Hexalith.EventStore.Server.LiveSidecar.Tests")
CONTEXT_FILES = {".msCoverageExtensionSourceRootsMapping_Hexalith.EventStore.Server.Tests",
                 ".msCoverageSourceRootsMapping_Hexalith.EventStore.Server.Tests",
                 "CoverletSourceRootsMapping_Hexalith.EventStore.Server.Tests", "MvcTestingAppManifest.json"}
ERRORS = []
CACHE = {}
STATS = {}
MANIFESTS = {}


def utc():
    return datetime.now(timezone.utc).isoformat()


def fingerprint(info):
    return (info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns, info.st_ctime_ns)


def sha(path, strict=True):
    path = Path(path)
    name = str(path)
    if name in CACHE:
        return CACHE[name]
    try:
        with path.open("rb") as stream:
            before = os.fstat(stream.fileno())
            if not stat.S_ISREG(before.st_mode):
                raise ValueError("Not a regular file")
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
            after = os.fstat(stream.fileno())
            if fingerprint(before) != fingerprint(after):
                raise ValueError("Input changed during hashing")
        CACHE[name], STATS[name] = digest, fingerprint(after)
        return digest
    except (OSError, ValueError) as error:
        if strict:
            ERRORS.append("Cannot authenticate " + name + ": " + str(error))
        return None


def manifest(relative):
    path = PACKET / relative
    MANIFESTS[relative] = sha(path)
    return json.loads(path.read_text())


def require(condition, message):
    if not condition:
        ERRORS.append(message)


def enroll(target, values, conflicts, label):
    for name, value in values.items():
        if name in target and target[name] != value:
            conflicts.append({"path": name, "earlier": target[name], "later": value, "enrollment": label})
        else:
            target[name] = value


def changes(expected):
    return [{"path": name, "expected": digest, "actual": actual}
            for name, digest in expected.items() if (actual := sha(name)) != digest]


def map_path(path, before, after):
    return str(after / Path(path).relative_to(before))


def inventory(directory):
    return {str(path.relative_to(directory)): sha(path) for path in sorted(directory.rglob("*")) if path.is_file()}


def fresh_sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_new(name, value):
    with (OUTPUT / name).open("x") as stream:
        json.dump(value, stream, indent=2)
        stream.write("\n")


def main():
    for name in ("input-identity-audit.json", "producer-current-inputs.json", "binary-freeze.json", "source-inputs.json"):
        if (OUTPUT / name).exists():
            raise SystemExit("Refusing to overwrite evidence: " + name)
    result = {"passed": False, "utc_start": utc(), "command": [sys.executable, str(Path(__file__).resolve())],
              "auditor_sha256": sha(Path(__file__).resolve()), "cwd": os.getcwd(),
              "source_selection": "S3 copies the selected S2 graph and adds only the exact guard fallback/fixture correction and ten new regression rows.",
              "original_producer": str(S1), "cursor_successor": str(S2), "final_successor": str(S3), "errors": ERRORS}
    ordinary = manifest("snapshot-inputs.json")
    tracked = manifest("tracked-guard-inputs.json")
    original_binary = manifest("debug-binary-freeze.json")
    qualified = manifest("qualified-worker-runtime-inputs.json")
    strict_original = manifest("current-inputs.json")
    earlier_integrity = manifest("final-input-integrity.json")
    previous_inputs = manifest("guard-repair/current-inputs.json")
    previous_sources = manifest("guard-repair/source-inputs.json")
    previous_binary = manifest("guard-repair/binary-freeze.json")
    previous_audit = manifest("guard-repair/input-identity-audit.json")
    overlay = manifest("guard-tightening/overlay.json")
    correction = manifest("guard-tightening/fixture-correction.json")
    current, conflicts, s1_expected = {}, [], {}
    enroll(s1_expected, {name: row["sha256"] for name, row in ordinary.items()}, conflicts, "S1 ordinary")
    enroll(s1_expected, tracked, conflicts, "S1 tracked guard")
    enroll(s1_expected, original_binary["files_sha256"], conflicts, "S1 Debug output")
    enroll(s1_expected, qualified["files"], conflicts, "S1 isolated runtime")
    result["s1_expected_paths"] = len(s1_expected)
    result["s1_fresh_changes"] = changes(s1_expected)
    require(len(s1_expected) == 72_224 and not result["s1_fresh_changes"], "S1 original producer inputs changed.")
    require(previous_audit["passed"] is True, "The retained S2 identity audit did not pass.")
    result["s2_previous_pinned_paths"] = len(previous_inputs)
    result["s2_fresh_changes"] = changes(previous_inputs)
    require(not result["s2_fresh_changes"], "S2 or its pinned original inputs changed.")
    enroll(current, s1_expected, conflicts, "S1 producer")
    enroll(current, previous_inputs, conflicts, "all preserved S2 inputs")
    print(json.dumps({"s1_inputs_checked": len(s1_expected), "s1_changes": len(result["s1_fresh_changes"]),
                      "s2_inputs_checked": len(previous_inputs), "s2_changes": len(result["s2_fresh_changes"])}), flush=True)

    expected_guard_hash = next(row["after_sha256"] for row in correction["changes"] if row["label"] == "snapshot")
    expected_new_hash = overlay["new_regression"]["sha256"]
    mapped_sources = {map_path(name, S2, S3): digest for name, digest in previous_sources.items()}
    source_changes = changes(mapped_sources)
    result["s3_ordinary_changes_from_s2"] = source_changes
    require({row["path"]: row["actual"] for row in source_changes} == {str(S3 / GUARD): expected_guard_hash},
            "S3 ordinary source/config delta exceeds the exact final guard change.")
    s3_sources = {name: sha(name) for name in mapped_sources}
    s3_sources[str(S3 / EMPTY)] = sha(S3 / EMPTY)
    require(not (S1 / EMPTY).exists() and not (S2 / EMPTY).exists()
            and s3_sources[str(S3 / EMPTY)] == expected_new_hash, "New empty-call regression source identity differs.")
    mapped_guard = {map_path(name, S1, S3): digest for name, digest in tracked.items()}
    guard_changes = changes(mapped_guard)
    result["s3_tracked_guard_changes_from_s1"] = guard_changes
    require(len(mapped_guard) == 59_487
            and {row["path"]: row["actual"] for row in guard_changes} == {str(S3 / GUARD): expected_guard_hash},
            "S3 tracked guard/evidence delta exceeds the authorized guard source.")
    require(sha(S3 / CURSOR) == sha(S2 / CURSOR), "Inherited cursor regression source changed.")
    for source, archive in ((S3 / GUARD, OUTPUT / "snapshot-final.cs.source"), (S3 / EMPTY, OUTPUT / "regressions.cs.source")):
        require(sha(source) == sha(archive), "Final authorized source archive differs: " + str(source))
    result["exact_guard_source_diff_from_s2"] = "".join(difflib.unified_diff((S2 / GUARD).read_text().splitlines(keepends=True),
                                                                           (S3 / GUARD).read_text().splitlines(keepends=True),
                                                                           fromfile=str(S2 / GUARD), tofile=str(S3 / GUARD)))
    result["s3_ordinary_source_paths"] = len(s3_sources)
    result["s3_tracked_guard_paths"] = len(mapped_guard)
    result["new_regression_source"] = {"path": str(S3 / EMPTY), "sha256": sha(S3 / EMPTY), "rows": overlay["new_regression"]["rows"]}
    enroll(current, {name: sha(name) for name in mapped_guard}, conflicts, "S3 tracked guard current bytes")
    enroll(current, s3_sources, conflicts, "S3 ordinary source/config plus both regression partials")
    print(json.dumps({"s3_ordinary_paths": len(s3_sources), "s3_guard_paths": len(mapped_guard),
                      "ordinary_changes_from_s2": len(source_changes), "guard_changes_from_s1": len(guard_changes)}), flush=True)

    binary_freeze = {"files_sha256": dict(previous_binary["files_sha256"]),
                     "assemblies_sha256": dict(previous_binary["assemblies_sha256"])}
    s2_unit, s3_unit = inventory(S2 / UNIT), inventory(S3 / UNIT)
    unit_changes = [{"relative": name, "s2_sha256": s2_unit.get(name), "s3_sha256": s3_unit.get(name)}
                    for name in sorted(set(s2_unit) | set(s3_unit)) if s2_unit.get(name) != s3_unit.get(name)]
    unit_pe_pdb = {"Hexalith.EventStore.Server.Tests.dll", "Hexalith.EventStore.Server.Tests.pdb"}
    require(len(s2_unit) == 682 and len(s3_unit) == 682, "Unit output file count changed.")
    require({row["relative"] for row in unit_changes} == CONTEXT_FILES | unit_pe_pdb,
            "Unit output differences exceed its PE/PDB and four exact path-context files.")
    context_changes = []
    for name in sorted(CONTEXT_FILES):
        before, after = (S2 / UNIT / name).read_bytes(), (S3 / UNIT / name).read_bytes()
        path_only = before.replace(str(S2).encode(), str(S3).encode()) == after
        require(path_only, "Build-context metadata differs beyond literal S2-to-S3 paths: " + name)
        context_changes.append({"relative": name, "before_sha256": sha(S2 / UNIT / name), "after_sha256": sha(S3 / UNIT / name),
                                "only_literal_snapshot_path_substitution": path_only,
                                "before_text": before.decode(), "after_text": after.decode()})
    first_party_s2 = {name: digest for name, digest in s2_unit.items()
                      if Path(name).name.startswith("Hexalith.") and Path(name).suffix in (".dll", ".pdb")}
    first_party_s3 = {name: digest for name, digest in s3_unit.items()
                      if Path(name).name.startswith("Hexalith.") and Path(name).suffix in (".dll", ".pdb")}
    first_party_changes = [{"relative": name, "s2_sha256": first_party_s2.get(name), "s3_sha256": first_party_s3.get(name)}
                           for name in sorted(set(first_party_s2) | set(first_party_s3)) if first_party_s2.get(name) != first_party_s3.get(name)]
    require({row["relative"] for row in first_party_changes} == unit_pe_pdb,
            "S3 Unit first-party PE/PDB dependencies changed.")
    result["unit_output_comparison"] = {"s2_count": len(s2_unit), "s3_count": len(s3_unit), "changes": unit_changes,
                                         "path_context_changes": context_changes, "first_party_s2_count": len(first_party_s2),
                                         "first_party_s3_count": len(first_party_s3), "first_party_changes": first_party_changes}
    mapped_binary = {map_path(name, S2, S3): digest for name, digest in previous_binary["files_sha256"].items()
                     if Path(name).is_relative_to(S2)}
    binary_changes = changes(mapped_binary)
    result["s3_mapped_binary_changes"] = binary_changes
    require({row["path"] for row in binary_changes} == {str(S3 / UNIT / name) for name in CONTEXT_FILES | unit_pe_pdb},
            "S3 producer/dependency output changed beyond the Unit-specific files.")
    current_binary = {name: sha(name) for name in mapped_binary}
    unit_output_paths = {str(S3 / UNIT / name): digest for name, digest in s3_unit.items()}
    enroll(current, current_binary, conflicts, "S3 actual first-party/dependency outputs")
    enroll(current, unit_output_paths, conflicts, "all S3 Unit output files")
    binary_freeze["files_sha256"].update(current_binary)
    binary_freeze["files_sha256"].update(unit_output_paths)
    unit_pdb = str(S3 / UNIT / "Hexalith.EventStore.Server.Tests.pdb")
    binary_freeze["assemblies_sha256"][unit_pdb] = sha(unit_pdb)
    require(len(original_binary["assemblies_sha256"]) == 63 and len(previous_binary["assemblies_sha256"]) == 64
            and len(binary_freeze["assemblies_sha256"]) == 65, "Expected 63 original pairs plus separate S2 and S3 Unit pairs.")
    result["pdb_manifest"] = {"original_pairs": 63, "s2_pairs": 64, "s3_pairs": len(binary_freeze["assemblies_sha256"]), "added_s3_pdb": unit_pdb}
    mapped_qualified = {map_path(name, S1, S3): digest for name, digest in qualified["files"].items()}
    result["s3_qualified_worker_output_changes"] = changes(mapped_qualified)
    require(len(mapped_qualified) == 788 and not result["s3_qualified_worker_output_changes"], "S3 isolated worker output changed.")
    enroll(current, mapped_qualified, conflicts, "S3 isolated worker output")
    phases = [manifest(str(path.relative_to(PACKET))) for path in sorted((PACKET / "bounded-scenarios").glob("*/*.json"))]
    result["worker_module_identity"] = []
    for name in WORKER_NAMES:
        pe, pdb = WORKER / (name + ".dll"), WORKER / (name + ".pdb")
        pe_hash, pdb_hash = sha(S1 / pe), sha(S1 / pdb)
        loads = [row for proof in phases for row in proof["runtime"] if row["name"] == name]
        mvids = sorted({row["loadedMvid"] for row in loads})
        passed = (len(phases) == 30 and len(loads) == 30 and len(mvids) == 1
                  and all(row["loadedMvidMatchesPe"] is True and row["peSha256"].lower() == pe_hash
                          and row["pdbSha256"].lower() == pdb_hash for row in loads)
                  and sha(S2 / pe) == sha(S3 / pe) == pe_hash and sha(S2 / pdb) == sha(S3 / pdb) == pdb_hash)
        require(passed, "S3/S2 producer byte identity differs from actual worker loads: " + name)
        result["worker_module_identity"].append({"module": name, "pe_sha256": pe_hash, "pdb_sha256": pdb_hash,
                                                 "phase_observations": len(loads), "loaded_mvids": mvids, "passed": passed})
    runtime = OUTPUT / "runtime"
    runtime.mkdir(exist_ok=False)
    archived = []
    sources = [(S3 / UNIT / ("Hexalith.EventStore.Server.Tests" + suffix), "Hexalith.EventStore.Server.Tests" + suffix)
               for suffix in (".dll", ".pdb", ".deps.json", ".runtimeconfig.json")]
    sources += [(S3 / relative, relative.name + ".source") for relative in (GUARD, CURSOR, EMPTY)]
    for source, filename in sources:
        target = runtime / filename
        shutil.copyfile(source, target)
        require(sha(source) == sha(target), "Archived final Unit runtime/source differs: " + str(source))
        archived.append({"source": str(source), "archive": str(target.relative_to(PACKET)), "sha256": sha(target)})
        current[str(target)] = sha(target)
    result["archived_final_unit_runtime_and_source"] = archived
    result["mutable_origin_source_differences_from_s3"] = []
    for name, row in ordinary.items():
        successor = str(S3 / Path(name).relative_to(S1))
        observed = sha(row["origin"], strict=False)
        if observed != s3_sources[successor]:
            result["mutable_origin_source_differences_from_s3"].append({"origin": row["origin"], "selected_s1_sha256": row["sha256"],
                                                                        "s3_sha256": s3_sources[successor], "mutable_origin_sha256": observed})
    result["strict_current_inputs_retained"] = {"path": str(PACKET / "current-inputs.json"), "paths": len(strict_original),
                                                "sha256": MANIFESTS["current-inputs.json"], "unchanged": fresh_sha(PACKET / "current-inputs.json") == MANIFESTS["current-inputs.json"]}
    require(result["strict_current_inputs_retained"]["unchanged"], "Existing strict current-inputs changed during audit.")
    result["separate_preservation_conflicts"] = {"receipt": "final-input-integrity.json", "sha256": MANIFESTS["final-input-integrity.json"],
                                                 "folders_passed": earlier_integrity["folders_passed"], "folders_changes": earlier_integrity["folders_changes"],
                                                 "all_initial_protected_changes": earlier_integrity["all_initial_protected_changes"],
                                                 "conflicting_input_hashes": earlier_integrity["conflicting_input_hashes"],
                                                 "latest_origin_changes": earlier_integrity["latest_origin_changes"]}
    require(not any(Path(name).is_relative_to(ORIGIN.parent / "folders") for name in current),
            "Producer current-input inventory unexpectedly includes mutable Folders paths.")
    altered = []
    for name, before in STATS.items():
        path = Path(name)
        if path.is_relative_to(S1) or path.is_relative_to(S2) or path.is_relative_to(S3):
            try:
                if fingerprint(path.stat()) != before:
                    altered.append(name)
            except OSError:
                altered.append(name)
    result["snapshot_inputs_changed_during_audit"] = altered
    require(not altered and not conflicts and all(digest is not None for digest in current.values()),
            "Producer snapshots changed, have conflicting pins or contain missing inputs.")
    result["pin_conflicts"] = conflicts
    result["source_manifest_sha256"] = MANIFESTS
    result["producer_current_paths"] = len(current)
    result["passed"] = not ERRORS
    result["producer_reuse_justified"] = result["passed"]
    result["reuse_disposition"] = ("S1 fresh solution/Core/actor/nine-case Dapr execution remains applicable to byte-identical producer inputs in S2/S3. Only Unit guard/test source and Unit PE/PDB changed; four Unit metadata files differ only by literal snapshot paths. Fresh S3 Unit results and its PDB/source audit are separate required evidence."
                                   if result["passed"] else "Producer reuse is not justified; inspect every mismatch.")
    result["scope_limit"] = "No current mutable-origin, Folders adoption or whole-story qualification is established. The existing strict current-inputs and its explicit Folders failure are retained unchanged."
    result["utc_end"] = utc()
    write_new("source-inputs.json", s3_sources)
    write_new("binary-freeze.json", binary_freeze)
    write_new("producer-current-inputs.json", current)
    write_new("input-identity-audit.json", result)
    print(json.dumps({"passed": result["passed"], "s1_inputs": len(s1_expected), "s2_inputs": len(previous_inputs),
                      "s3_guard_inputs": len(mapped_guard), "source_paths": len(s3_sources), "producer_paths": len(current),
                      "unit_first_party_changes": len(first_party_changes), "errors": ERRORS}), flush=True)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
