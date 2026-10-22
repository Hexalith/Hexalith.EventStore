#!/usr/bin/env python3
"""Authenticate final own-source fixture enrollment and the unchanged producer graph."""
import base64
from datetime import datetime, timezone
import difflib
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import sys

OUT = Path(__file__).resolve().parent
PACKET = OUT.parent
S1 = Path("/tmp/eventstore-folders-12-2-latest-20261022T155454Z")
BASE = Path("/tmp/eventstore-folders-12-2-guardfixtures-20261022T173900Z")
FINAL = Path("/tmp/eventstore-folders-12-2-guardfinal-20261022T175200Z")
GUARD = Path("tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs")
PARTIALS = [GUARD.with_name("SecretsProtectionTests.UnifiedDiffCursor.cs"), GUARD.with_name("SecretsProtectionTests.EmptyCalls.cs")]
INGESTION = GUARD.with_name("SecretsProtectionTests.EvidenceSemanticIngestion.cs")
UNIT = Path("tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0")
WORKER = Path("tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/qualified-Debug/net10.0")
MODULES = ("Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.Server", "Hexalith.EventStore.Server.LiveSidecar.Tests")
CONTEXT = {".msCoverageExtensionSourceRootsMapping_Hexalith.EventStore.Server.Tests", ".msCoverageSourceRootsMapping_Hexalith.EventStore.Server.Tests", "CoverletSourceRootsMapping_Hexalith.EventStore.Server.Tests", "MvcTestingAppManifest.json"}
CACHE, STATS, INPUTS, ERRORS = {}, {}, {}, []


def utc():
    return datetime.now(timezone.utc).isoformat()


def fingerprint(value):
    return (value.st_dev, value.st_ino, value.st_size, value.st_mtime_ns, value.st_ctime_ns)


def sha(path):
    path = Path(path)
    name = str(path)
    if name not in CACHE:
        with path.open("rb") as stream:
            before = os.fstat(stream.fileno())
            if not stat.S_ISREG(before.st_mode):
                raise ValueError("Not a regular file: " + name)
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
            after = os.fstat(stream.fileno())
            if fingerprint(before) != fingerprint(after):
                raise ValueError("Changed while hashing: " + name)
        CACHE[name], STATS[name] = digest, fingerprint(after)
    return CACHE[name]


def read(name):
    path = PACKET / name
    INPUTS[name] = sha(path)
    return json.loads(path.read_text())


def require(condition, description):
    if not condition:
        ERRORS.append(description)


def map_paths(values, before, after):
    return {str(after / Path(name).relative_to(before)): digest for name, digest in values.items()}


def changes(expected):
    return [{"path": name, "expected": digest, "actual": actual} for name, digest in expected.items() if (actual := sha(name)) != digest]


def merge(target, values):
    for name, digest in values.items():
        require(name not in target or target[name] == digest, "Conflicting producer pin: " + name)
        target[name] = digest


def inventory(directory):
    return {str(path.relative_to(directory)): sha(path) for path in sorted(directory.rglob("*")) if path.is_file()}


def write(name, value):
    with (OUT / name).open("x") as stream:
        json.dump(value, stream, indent=2)
        stream.write("\n")


def main():
    for name in ("input-identity-audit.json", "producer-current-inputs.json", "source-inputs.json", "binary-freeze.json"):
        if (OUT / name).exists():
            raise SystemExit("Refusing to overwrite evidence: " + name)
    result = {"passed": False, "utc_start": utc(), "argv": [sys.executable, str(Path(__file__).resolve())],
              "auditor_sha256": sha(Path(__file__).resolve()), "cwd": os.getcwd(),
              "source_selection": "S1 selected producer graph plus the inherited test repairs, final cursor fixture fragments and explicit enrollment of both task-owned partials in FINAL.",
              "source_snapshot": str(FINAL), "errors": ERRORS}
    ordinary = read("snapshot-inputs.json")
    tracked = read("tracked-guard-inputs.json")
    original_binary = read("debug-binary-freeze.json")
    qualified = read("qualified-worker-runtime-inputs.json")
    previous_sources = read("guard-fixtures/source-inputs.json")
    previous_producer = read("guard-fixtures/producer-current-inputs.json")
    previous_binary = read("guard-fixtures/binary-freeze-complete.json")
    previous_audit = read("guard-fixtures/input-identity-audit.json")
    failed_descriptor_audit = read("source-binding-guardfixtures/summary.json")
    corrected_descriptor_audit = read("source-binding-guardfixtures-final/summary.json")
    original_descriptor = read("guard-fixtures/binary-freeze.json")
    overlay = read("guard-final/overlay.json")
    preservation = read("final-input-integrity.json")
    strict_sha = sha(PACKET / "current-inputs.json")
    require(previous_audit["passed"] is True and previous_audit["producer_reuse_justified"] is True, "Prior baseline identity audit did not pass.")
    require(failed_descriptor_audit["passed"] is False and corrected_descriptor_audit["passed"] is True
            and corrected_descriptor_audit["errors"] == [], "Retained failed/corrected descriptor audit states differ.")
    result["prior_descriptor_closure"] = {"original_manifest": "guard-fixtures/binary-freeze.json",
                                           "original_sha256": INPUTS["guard-fixtures/binary-freeze.json"],
                                           "complete_manifest": "guard-fixtures/binary-freeze-complete.json",
                                           "complete_sha256": INPUTS["guard-fixtures/binary-freeze-complete.json"],
                                           "added_file_descriptors": {name: value for name, value in previous_binary["files_sha256"].items() if name not in original_descriptor["files_sha256"]},
                                           "retained_failed_summary": failed_descriptor_audit,
                                           "retained_corrected_summary": corrected_descriptor_audit}
    result["earlier_audit_chain"] = []
    for name in ("guard-repair/input-identity-audit.json", "guard-tightening/input-identity-audit.py", "guard-tightening/input-identity-audit-initial-failure.json", "guard-tightening/input-identity-audit-v2.py", "guard-tightening/input-identity-audit.json", "guard-tightening/input-identity-audit-v3.py", "guard-tightening/input-identity-audit-v3.json"):
        result["earlier_audit_chain"].append({"path": name, "sha256": sha(PACKET / name)})
    # S1 and FINAL are pinned in the final producer manifest. Earlier successors retain their own manifests.
    s1 = {}
    for values in ({name: row["sha256"] for name, row in ordinary.items()}, tracked, original_binary["files_sha256"], qualified["files"]):
        merge(s1, values)
    result["s1_fresh_changes"] = changes(s1)
    require(len(s1) == 72_224 and not result["s1_fresh_changes"], "Original S1 producer inputs changed.")
    producer = dict(s1)
    s3_sources_guard = dict(previous_sources)
    for name in tracked:
        mapped = str(BASE / Path(name).relative_to(S1))
        merge(s3_sources_guard, {mapped: previous_producer[mapped]})
    result["base_fresh_source_guard_changes"] = changes(s3_sources_guard)
    require(not result["base_fresh_source_guard_changes"], "BASE baseline source/guard inputs changed.")
    print(json.dumps({"s1_paths_checked": len(s1), "s1_changes": len(result["s1_fresh_changes"]), "base_source_guard_paths_checked": len(s3_sources_guard), "base_changes": len(result["base_fresh_source_guard_changes"])}), flush=True)
    expected_final_change = {row["path"]: row["after_sha256"] for row in overlay["changes"] if row["label"] == "snapshot"}
    require(set(expected_final_change) == {str(FINAL / PARTIALS[0]), str(FINAL / INGESTION)}, "Final overlay must change only cursor fixtures and owning-reader enrollment.")
    s4_sources_expected = map_paths(previous_sources, BASE, FINAL)
    s4_guard_expected = map_paths({str(BASE / Path(name).relative_to(S1)): previous_producer[str(BASE / Path(name).relative_to(S1))] for name in tracked}, BASE, FINAL)
    result["final_ordinary_changes_from_baseline"] = changes(s4_sources_expected)
    result["final_tracked_guard_changes_from_baseline"] = changes(s4_guard_expected)
    require({row["path"]: row["actual"] for row in result["final_ordinary_changes_from_baseline"]} == expected_final_change, "Unexpected FINAL ordinary source/config delta.")
    require({row["path"]: row["actual"] for row in result["final_tracked_guard_changes_from_baseline"]} == {str(FINAL / INGESTION): expected_final_change[str(FINAL / INGESTION)]}, "Unexpected FINAL tracked guard/evidence delta.")
    require(len(s4_guard_expected) == 59_487 and len(s4_sources_expected) == 16_764, "Final source/guard inventory count differs.")
    sources = {name: sha(name) for name in s4_sources_expected}
    merge(producer, sources)
    merge(producer, {name: sha(name) for name in s4_guard_expected})
    for path in (GUARD, PARTIALS[1]):
        require(sha(FINAL / path) == sha(BASE / path), "Inherited guard/empty-call source changed: " + str(path))
    result["final_source_diffs"] = []
    for path in (PARTIALS[0], INGESTION):
        require(sha(BASE / path) == sha(OUT / ("snapshot-before-" + path.name + ".source"))
                and sha(FINAL / path) == sha(OUT / ("snapshot-after-" + path.name + ".source")), "Final source differs from its exact archived overlay: " + str(path))
        result["final_source_diffs"].append({"path": str(path), "diff": "".join(difflib.unified_diff((BASE / path).read_text().splitlines(keepends=True), (FINAL / path).read_text().splitlines(keepends=True), fromfile=str(BASE / path), tofile=str(FINAL / path)))})
    print(json.dumps({"final_ordinary_paths_checked": len(sources), "final_guard_paths_checked": len(s4_guard_expected), "ordinary_changes": len(result["final_ordinary_changes_from_baseline"]), "guard_changes": len(result["final_tracked_guard_changes_from_baseline"])}), flush=True)
    before_unit, final_unit = inventory(BASE / UNIT), inventory(FINAL / UNIT)
    unit_changes = [{"relative": name, "base_sha256": before_unit.get(name), "final_sha256": final_unit.get(name)} for name in sorted(set(before_unit) | set(final_unit)) if before_unit.get(name) != final_unit.get(name)]
    pair = {"Hexalith.EventStore.Server.Tests.dll", "Hexalith.EventStore.Server.Tests.pdb"}
    require(len(before_unit) == len(final_unit) == 682 and {row["relative"] for row in unit_changes} == CONTEXT | pair, "Final Unit output changes exceed its pair and exact path metadata.")
    first_party = lambda values: {name: digest for name, digest in values.items() if Path(name).name.startswith("Hexalith.") and Path(name).suffix in (".dll", ".pdb")}
    before_pe, final_pe = first_party(before_unit), first_party(final_unit)
    require({name for name in set(before_pe) | set(final_pe) if before_pe.get(name) != final_pe.get(name)} == pair, "Final Unit first-party dependencies changed.")
    context_rows = []
    for name in sorted(CONTEXT):
        before, after = (BASE / UNIT / name).read_bytes(), (FINAL / UNIT / name).read_bytes()
        encoding = "utf-16-le" if before.startswith(b"\xff\xfe") else "utf-16-be" if before.startswith(b"\xfe\xff") else "utf-8"
        old_path, new_path = str(BASE), str(FINAL)
        if name == "MvcTestingAppManifest.json":
            old_path, new_path = old_path.replace("/", "\\/"), new_path.replace("/", "\\/")
        path_only = before.replace(old_path.encode(encoding), new_path.encode(encoding)) == after
        require(path_only, "Final build metadata differs beyond exact snapshot path bytes: " + name)
        context_rows.append({"relative": name, "encoding": encoding, "path_only": path_only,
                             "before_sha256": sha(BASE / UNIT / name), "after_sha256": sha(FINAL / UNIT / name),
                             "before_base64": base64.b64encode(before).decode(), "after_base64": base64.b64encode(after).decode()})
    result["unit_output_comparison"] = {"base_paths": len(before_unit), "final_paths": len(final_unit), "first_party_s3_paths": len(before_pe), "first_party_s4_paths": len(final_pe), "changes": unit_changes, "path_context_changes": context_rows}
    mapped_binary_expected = map_paths(original_binary["files_sha256"], S1, FINAL)
    result["final_original_binary_changes"] = changes(mapped_binary_expected)
    require({row["path"] for row in result["final_original_binary_changes"]} == {str(FINAL / UNIT / name) for name in pair}, "Producer first-party images differ beyond the Unit pair.")
    final_binary_paths = {name: sha(name) for name in mapped_binary_expected}
    final_binary_paths.update({str(FINAL / UNIT / name): digest for name, digest in final_unit.items()})
    merge(producer, final_binary_paths)
    final_qualified = map_paths(qualified["files"], S1, FINAL)
    result["qualified_worker_changes"] = changes(final_qualified)
    require(len(final_qualified) == 788 and not result["qualified_worker_changes"], "Final isolated worker output differs.")
    merge(producer, final_qualified)
    phases = [read(str(path.relative_to(PACKET))) for path in sorted((PACKET / "bounded-scenarios").glob("*/*.json"))]
    result["worker_modules"] = []
    for name in MODULES:
        pe, pdb = WORKER / (name + ".dll"), WORKER / (name + ".pdb")
        pe_hash, pdb_hash = sha(S1 / pe), sha(S1 / pdb)
        rows = [module for proof in phases for module in proof["runtime"] if module["name"] == name]
        mvids = sorted({row["loadedMvid"] for row in rows})
        passed = (len(phases) == len(rows) == 30 and len(mvids) == 1 and sha(BASE / pe) == sha(FINAL / pe) == pe_hash and sha(BASE / pdb) == sha(FINAL / pdb) == pdb_hash
                  and all(row["loadedMvidMatchesPe"] is True and row["peSha256"].lower() == pe_hash and row["pdbSha256"].lower() == pdb_hash for row in rows))
        require(passed, "Final worker graph differs from actual loaded phase proofs: " + name)
        result["worker_modules"].append({"module": name, "pe_sha256": pe_hash, "pdb_sha256": pdb_hash, "loaded_mvids": mvids, "phase_observations": len(rows), "passed": passed})
    binary = {"files_sha256": dict(original_binary["files_sha256"]), "assemblies_sha256": dict(previous_binary["assemblies_sha256"])}
    binary["files_sha256"].update(final_binary_paths)
    unit_pdb = str(FINAL / UNIT / "Hexalith.EventStore.Server.Tests.pdb")
    binary["assemblies_sha256"][unit_pdb] = sha(unit_pdb)
    result["complete_pair_descriptors"] = []
    for pdb, pdb_hash in binary["assemblies_sha256"].items():
        pe = str(Path(pdb).with_suffix(".dll"))
        pe_hash = binary["files_sha256"].get(pe, previous_binary["files_sha256"].get(pe))
        require(pe_hash is not None, "Expected PE descriptor missing: " + pe)
        if pe_hash is None:
            continue
        require(sha(pe) == pe_hash and sha(pdb) == pdb_hash, "A final or retained PE/PDB pair changed: " + pe)
        binary["files_sha256"][pe], binary["files_sha256"][pdb] = pe_hash, pdb_hash
        result["complete_pair_descriptors"].append({"pe": pe, "pe_sha256": pe_hash, "pdb": pdb, "pdb_sha256": pdb_hash, "fresh_hashes_match": sha(pe) == pe_hash and sha(pdb) == pdb_hash})
    require(len(original_binary["assemblies_sha256"]) == 63 and len(previous_binary["assemblies_sha256"]) == 66 and len(binary["assemblies_sha256"]) == 67
            and len(result["complete_pair_descriptors"]) == 67, "Expected all 67 pairs with corresponding PE/PDB expected descriptors.")
    runtime = OUT / "runtime"
    runtime.mkdir(exist_ok=False)
    archived = []
    archive_sources = [(FINAL / UNIT / ("Hexalith.EventStore.Server.Tests" + suffix), "Hexalith.EventStore.Server.Tests" + suffix) for suffix in (".dll", ".pdb", ".deps.json", ".runtimeconfig.json")]
    archive_sources += [(FINAL / path, path.name + ".source") for path in (GUARD, *PARTIALS, INGESTION)]
    for source, name in archive_sources:
        target = runtime / name
        shutil.copyfile(source, target)
        require(sha(source) == sha(target), "Final runtime/source archive differs: " + str(source))
        producer[str(target)] = sha(target)
        archived.append({"source": str(source), "archive": str(target.relative_to(PACKET)), "sha256": sha(target)})
    result["final_unit_runtime_and_source_archive"] = archived
    altered = [name for name, before in STATS.items() if (Path(name).is_relative_to(S1) or Path(name).is_relative_to(BASE) or Path(name).is_relative_to(FINAL)) and fingerprint(Path(name).stat()) != before]
    result["inputs_changed_during_audit"] = altered
    require(not altered, "A producer/successor input changed during the audit.")
    with (PACKET / "current-inputs.json").open("rb") as stream:
        strict_after = hashlib.file_digest(stream, "sha256").hexdigest()
    require(strict_after == strict_sha, "Existing strict current-inputs was changed.")
    require(not any("/projects/hexalith/folders/" in name for name in producer), "Mutable Folders paths entered the producer-only inventory.")
    result["strict_current_inputs_retained"] = {"path": str(PACKET / "current-inputs.json"), "sha256": strict_sha, "unchanged": strict_after == strict_sha}
    result["separate_preservation_conflicts"] = {"receipt": "final-input-integrity.json", "sha256": INPUTS["final-input-integrity.json"], "folders_passed": preservation["folders_passed"], "folders_changes": preservation["folders_changes"], "all_initial_protected_changes": preservation["all_initial_protected_changes"], "latest_origin_changes": preservation["latest_origin_changes"]}
    result["input_manifest_sha256"] = INPUTS
    result["s1_pinned_paths"], result["final_source_paths"], result["final_guard_paths"], result["producer_pinned_paths"] = len(s1), len(sources), len(s4_guard_expected), len(producer)
    result["pdb_pairs"] = len(binary["assemblies_sha256"])
    result["passed"] = not ERRORS
    result["producer_reuse_justified"] = result["passed"]
    result["reuse_disposition"] = "S1 fresh solution/Core/actor/nine-case Dapr execution applies to unchanged producer bytes. FINAL only changes Unit fixture/enrollment source and its Unit pair; four Unit path metadata files authenticate by exact byte substitution. Earlier failed executions and audits remain separately retained; their whole input inventories are not relabeled as fresh FINAL execution."
    result["scope_limit"] = "Fresh FINAL full tests, all-tracked-files scan and 67-pair PDB/source audit remain separate evidence. Mutable origin, existing Folders preservation failure, adoption and whole-story completion remain outside this producer identity pass."
    result["utc_end"] = utc()
    write("source-inputs.json", sources)
    write("binary-freeze.json", binary)
    write("producer-current-inputs.json", producer)
    write("input-identity-audit.json", result)
    print(json.dumps({"passed": result["passed"], "producer_paths": len(producer), "final_sources": len(sources), "final_guard": len(s4_guard_expected), "pdb_pairs": result["pdb_pairs"], "errors": ERRORS}), flush=True)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
