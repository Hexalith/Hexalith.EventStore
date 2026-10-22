#!/usr/bin/env python3
"""Authenticate final fixture-only source changes and the unchanged producer graph."""
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
S3 = Path("/tmp/eventstore-folders-12-2-guardtight-20261022T173000Z")
S4 = Path("/tmp/eventstore-folders-12-2-guardfixtures-20261022T173900Z")
GUARD = Path("tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs")
PARTIALS = [GUARD.with_name("SecretsProtectionTests.UnifiedDiffCursor.cs"), GUARD.with_name("SecretsProtectionTests.EmptyCalls.cs")]
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
              "source_selection": "S1 selected producer graph, inherited cursor/fallback test repairs, and only the two final fixture source constructions in S4.",
              "source_snapshot": str(S4), "errors": ERRORS}
    ordinary = read("snapshot-inputs.json")
    tracked = read("tracked-guard-inputs.json")
    original_binary = read("debug-binary-freeze.json")
    qualified = read("qualified-worker-runtime-inputs.json")
    previous_sources = read("guard-tightening/source-inputs.json")
    previous_producer = read("guard-tightening/producer-current-inputs.json")
    previous_binary = read("guard-tightening/binary-freeze.json")
    previous_v2 = read("guard-tightening/input-identity-audit.json")
    previous_v3 = read("guard-tightening/input-identity-audit-v3.json")
    overlay = read("guard-fixtures/overlay.json")
    preservation = read("final-input-integrity.json")
    strict_sha = sha(PACKET / "current-inputs.json")
    require(previous_v3["passed"] is True and previous_v3["producer_reuse_justified"] is True, "Prior S3 interpretation audit did not pass.")
    require(previous_v3["reused_exhaustive_audit"]["sha256"] == INPUTS["guard-tightening/input-identity-audit.json"], "Prior S3 exhaustive audit receipt changed.")
    result["earlier_audit_chain"] = []
    for name in ("guard-repair/input-identity-audit.json", "guard-tightening/input-identity-audit.py", "guard-tightening/input-identity-audit-initial-failure.json", "guard-tightening/input-identity-audit-v2.py", "guard-tightening/input-identity-audit.json", "guard-tightening/input-identity-audit-v3.py", "guard-tightening/input-identity-audit-v3.json"):
        result["earlier_audit_chain"].append({"path": name, "sha256": sha(PACKET / name)})
    # S1 and S4 are pinned in the final producer manifest. Earlier successors retain their own manifests.
    s1 = {}
    for values in ({name: row["sha256"] for name, row in ordinary.items()}, tracked, original_binary["files_sha256"], qualified["files"]):
        merge(s1, values)
    result["s1_fresh_changes"] = changes(s1)
    require(len(s1) == 72_224 and not result["s1_fresh_changes"], "Original S1 producer inputs changed.")
    producer = dict(s1)
    s3_sources_guard = dict(previous_sources)
    for name in tracked:
        mapped = str(S3 / Path(name).relative_to(S1))
        merge(s3_sources_guard, {mapped: previous_producer[mapped]})
    result["s3_fresh_source_guard_changes"] = changes(s3_sources_guard)
    require(not result["s3_fresh_source_guard_changes"], "S3 baseline source/guard inputs changed.")
    print(json.dumps({"s1_paths_checked": len(s1), "s1_changes": len(result["s1_fresh_changes"]), "s3_source_guard_paths_checked": len(s3_sources_guard), "s3_changes": len(result["s3_fresh_source_guard_changes"])}), flush=True)
    final_hash = next(row["after_sha256"] for row in overlay["changes"] if row["label"] == "snapshot")
    expected_final_change = {str(S4 / GUARD): final_hash}
    s4_sources_expected = map_paths(previous_sources, S3, S4)
    s4_guard_expected = map_paths({str(S3 / Path(name).relative_to(S1)): previous_producer[str(S3 / Path(name).relative_to(S1))] for name in tracked}, S3, S4)
    result["s4_ordinary_changes_from_s3"] = changes(s4_sources_expected)
    result["s4_tracked_guard_changes_from_s3"] = changes(s4_guard_expected)
    require({row["path"]: row["actual"] for row in result["s4_ordinary_changes_from_s3"]} == expected_final_change, "Unexpected S4 ordinary source/config delta.")
    require({row["path"]: row["actual"] for row in result["s4_tracked_guard_changes_from_s3"]} == expected_final_change, "Unexpected S4 tracked guard/evidence delta.")
    require(len(s4_guard_expected) == 59_487 and len(s4_sources_expected) == 16_764, "Final source/guard inventory count differs.")
    sources = {name: sha(name) for name in s4_sources_expected}
    merge(producer, sources)
    merge(producer, {name: sha(name) for name in s4_guard_expected})
    for path in PARTIALS:
        require(sha(S4 / path) == sha(S3 / path), "Inherited regression partial changed: " + str(path))
    require(sha(S3 / GUARD) == sha(OUT / "snapshot-before.cs.source") and sha(S4 / GUARD) == sha(OUT / "snapshot-after.cs.source"), "Final fixture source differs from its exact archived overlay.")
    result["fixture_source_diff"] = "".join(difflib.unified_diff((S3 / GUARD).read_text().splitlines(keepends=True), (S4 / GUARD).read_text().splitlines(keepends=True), fromfile=str(S3 / GUARD), tofile=str(S4 / GUARD)))
    print(json.dumps({"s4_ordinary_paths_checked": len(sources), "s4_guard_paths_checked": len(s4_guard_expected), "ordinary_changes": len(result["s4_ordinary_changes_from_s3"]), "guard_changes": len(result["s4_tracked_guard_changes_from_s3"])}), flush=True)
    before_unit, final_unit = inventory(S3 / UNIT), inventory(S4 / UNIT)
    unit_changes = [{"relative": name, "s3_sha256": before_unit.get(name), "s4_sha256": final_unit.get(name)} for name in sorted(set(before_unit) | set(final_unit)) if before_unit.get(name) != final_unit.get(name)]
    pair = {"Hexalith.EventStore.Server.Tests.dll", "Hexalith.EventStore.Server.Tests.pdb"}
    require(len(before_unit) == len(final_unit) == 682 and {row["relative"] for row in unit_changes} == CONTEXT | pair, "Final Unit output changes exceed its pair and exact path metadata.")
    first_party = lambda values: {name: digest for name, digest in values.items() if Path(name).name.startswith("Hexalith.") and Path(name).suffix in (".dll", ".pdb")}
    before_pe, final_pe = first_party(before_unit), first_party(final_unit)
    require({name for name in set(before_pe) | set(final_pe) if before_pe.get(name) != final_pe.get(name)} == pair, "Final Unit first-party dependencies changed.")
    context_rows = []
    for name in sorted(CONTEXT):
        before, after = (S3 / UNIT / name).read_bytes(), (S4 / UNIT / name).read_bytes()
        encoding = "utf-16-le" if before.startswith(b"\xff\xfe") else "utf-16-be" if before.startswith(b"\xfe\xff") else "utf-8"
        old_path, new_path = str(S3), str(S4)
        if name == "MvcTestingAppManifest.json":
            old_path, new_path = old_path.replace("/", "\\/"), new_path.replace("/", "\\/")
        path_only = before.replace(old_path.encode(encoding), new_path.encode(encoding)) == after
        require(path_only, "Final build metadata differs beyond exact snapshot path bytes: " + name)
        context_rows.append({"relative": name, "encoding": encoding, "path_only": path_only,
                             "before_sha256": sha(S3 / UNIT / name), "after_sha256": sha(S4 / UNIT / name),
                             "before_base64": base64.b64encode(before).decode(), "after_base64": base64.b64encode(after).decode()})
    result["unit_output_comparison"] = {"s3_paths": len(before_unit), "s4_paths": len(final_unit), "first_party_s3_paths": len(before_pe), "first_party_s4_paths": len(final_pe), "changes": unit_changes, "path_context_changes": context_rows}
    mapped_binary_expected = map_paths(original_binary["files_sha256"], S1, S4)
    result["s4_original_binary_changes"] = changes(mapped_binary_expected)
    require({row["path"] for row in result["s4_original_binary_changes"]} == {str(S4 / UNIT / name) for name in pair}, "Producer first-party images differ beyond the Unit pair.")
    final_binary_paths = {name: sha(name) for name in mapped_binary_expected}
    final_binary_paths.update({str(S4 / UNIT / name): digest for name, digest in final_unit.items()})
    merge(producer, final_binary_paths)
    final_qualified = map_paths(qualified["files"], S1, S4)
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
        passed = (len(phases) == len(rows) == 30 and len(mvids) == 1 and sha(S3 / pe) == sha(S4 / pe) == pe_hash and sha(S3 / pdb) == sha(S4 / pdb) == pdb_hash
                  and all(row["loadedMvidMatchesPe"] is True and row["peSha256"].lower() == pe_hash and row["pdbSha256"].lower() == pdb_hash for row in rows))
        require(passed, "Final worker graph differs from actual loaded phase proofs: " + name)
        result["worker_modules"].append({"module": name, "pe_sha256": pe_hash, "pdb_sha256": pdb_hash, "loaded_mvids": mvids, "phase_observations": len(rows), "passed": passed})
    binary = {"files_sha256": dict(original_binary["files_sha256"]), "assemblies_sha256": dict(previous_binary["assemblies_sha256"])}
    binary["files_sha256"].update(final_binary_paths)
    unit_pdb = str(S4 / UNIT / "Hexalith.EventStore.Server.Tests.pdb")
    binary["assemblies_sha256"][unit_pdb] = sha(unit_pdb)
    result["historical_pdb_pairs_fresh_hashes"] = [{"path": name, "expected": digest, "actual": sha(name)} for name, digest in previous_binary["assemblies_sha256"].items()]
    require(all(row["expected"] == row["actual"] for row in result["historical_pdb_pairs_fresh_hashes"]), "A prior separately retained Unit/producer PDB changed.")
    require(len(original_binary["assemblies_sha256"]) == 63 and len(previous_binary["assemblies_sha256"]) == 65 and len(binary["assemblies_sha256"]) == 66, "Expected original 63 pairs plus S2/S3/S4 Unit pairs.")
    runtime = OUT / "runtime"
    runtime.mkdir(exist_ok=False)
    archived = []
    archive_sources = [(S4 / UNIT / ("Hexalith.EventStore.Server.Tests" + suffix), "Hexalith.EventStore.Server.Tests" + suffix) for suffix in (".dll", ".pdb", ".deps.json", ".runtimeconfig.json")]
    archive_sources += [(S4 / path, path.name + ".source") for path in (GUARD, *PARTIALS)]
    for source, name in archive_sources:
        target = runtime / name
        shutil.copyfile(source, target)
        require(sha(source) == sha(target), "Final runtime/source archive differs: " + str(source))
        producer[str(target)] = sha(target)
        archived.append({"source": str(source), "archive": str(target.relative_to(PACKET)), "sha256": sha(target)})
    result["final_unit_runtime_and_source_archive"] = archived
    altered = [name for name, before in STATS.items() if (Path(name).is_relative_to(S1) or Path(name).is_relative_to(S3) or Path(name).is_relative_to(S4)) and fingerprint(Path(name).stat()) != before]
    result["inputs_changed_during_audit"] = altered
    require(not altered, "A producer/successor input changed during the audit.")
    with (PACKET / "current-inputs.json").open("rb") as stream:
        strict_after = hashlib.file_digest(stream, "sha256").hexdigest()
    require(strict_after == strict_sha, "Existing strict current-inputs was changed.")
    require(not any("/projects/hexalith/folders/" in name for name in producer), "Mutable Folders paths entered the producer-only inventory.")
    result["strict_current_inputs_retained"] = {"path": str(PACKET / "current-inputs.json"), "sha256": strict_sha, "unchanged": strict_after == strict_sha}
    result["separate_preservation_conflicts"] = {"receipt": "final-input-integrity.json", "sha256": INPUTS["final-input-integrity.json"], "folders_passed": preservation["folders_passed"], "folders_changes": preservation["folders_changes"], "all_initial_protected_changes": preservation["all_initial_protected_changes"], "latest_origin_changes": preservation["latest_origin_changes"]}
    result["input_manifest_sha256"] = INPUTS
    result["s1_pinned_paths"], result["s4_source_paths"], result["s4_guard_paths"], result["producer_pinned_paths"] = len(s1), len(sources), len(s4_guard_expected), len(producer)
    result["pdb_pairs"] = len(binary["assemblies_sha256"])
    result["passed"] = not ERRORS
    result["producer_reuse_justified"] = result["passed"]
    result["reuse_disposition"] = "S1 fresh solution/Core/actor/nine-case Dapr execution applies to unchanged producer bytes. S4 only changes Unit guard/test source and its Unit pair; four Unit path metadata files authenticate by exact byte substitution. S2/S3 failed executions and audits remain separately retained; their whole input inventories are not relabeled as fresh S4 execution."
    result["scope_limit"] = "Fresh S4 full tests, all-tracked-files scan and 66-pair PDB/source audit remain separate evidence. Mutable origin, existing Folders preservation failure, adoption and whole-story completion remain outside this producer identity pass."
    result["utc_end"] = utc()
    write("source-inputs.json", sources)
    write("binary-freeze.json", binary)
    write("producer-current-inputs.json", producer)
    write("input-identity-audit.json", result)
    print(json.dumps({"passed": result["passed"], "producer_paths": len(producer), "s4_sources": len(sources), "s4_guard": len(s4_guard_expected), "pdb_pairs": result["pdb_pairs"], "errors": ERRORS}), flush=True)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
