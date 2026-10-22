#!/usr/bin/env python3
"""Authenticate the test-only guard successor without relabeling mutable origin.

All snapshot source/guard inputs are byte-hashed. Reuse is allowed only when the
original graph is intact, the successor has exactly the authorized source delta,
and every producer/runtime dependency remains byte-identical. No build runs here.
"""
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
ORIGIN = Path("/home/administrator/projects/hexalith/eventstore")
GUARD = Path("tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs")
REGRESSION = GUARD.with_name("SecretsProtectionTests.UnifiedDiffCursor.cs")
UNIT = Path("tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0")
WORKER = Path("tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/qualified-Debug/net10.0")
WORKER_NAMES = ("Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
                "Hexalith.EventStore.Server", "Hexalith.EventStore.Server.LiveSidecar.Tests")
ERRORS = []
CACHE = {}
STATS = {}
INPUT_MANIFESTS = {}


def utc():
    return datetime.now(timezone.utc).isoformat()


def fingerprint(info):
    return (info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns, info.st_ctime_ns)


def sha(path):
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
                raise ValueError("Input changed while hashing")
        CACHE[name] = digest
        STATS[name] = fingerprint(after)
        return digest
    except (OSError, ValueError) as error:
        ERRORS.append("Cannot authenticate " + name + ": " + str(error))
        return None


def manifest(relative):
    path = PACKET / relative
    INPUT_MANIFESTS[relative] = sha(path)
    return json.loads(path.read_text())


def require(condition, message):
    if not condition:
        ERRORS.append(message)


def enroll(target, values, conflicts, label):
    for name, digest in values.items():
        if name in target and target[name] != digest:
            conflicts.append({"path": name, "earlier": target[name], "later": digest, "enrollment": label})
        else:
            target[name] = digest


def compare_expected(values):
    return [{"path": name, "expected": digest, "actual": actual}
            for name, digest in values.items() if (actual := sha(name)) != digest]


def mapped(path):
    return str(S2 / Path(path).relative_to(S1))


def head_from_files(root):
    directory = root / ".git"
    if directory.is_file():
        directory = (root / directory.read_text().strip().removeprefix("gitdir: ")).resolve()
    value = (directory / "HEAD").read_text().strip()
    if not value.startswith("ref: "):
        return {"head": value, "head_reference": None}
    reference = value.removeprefix("ref: ")
    loose = directory / reference
    if loose.exists():
        return {"head": loose.read_text().strip(), "head_reference": reference}
    packed = directory / "packed-refs"
    for line in packed.read_text().splitlines() if packed.exists() else []:
        if not line.startswith(("#", "^")) and line.endswith(" " + reference):
            return {"head": line.split()[0], "head_reference": reference}
    return {"head": None, "head_reference": reference}


def write_new(name, value):
    with (OUTPUT / name).open("x") as stream:
        json.dump(value, stream, indent=2)
        stream.write("\n")


def main():
    for name in ("input-identity-audit.json", "current-inputs.json", "binary-freeze.json", "source-inputs.json"):
        if (OUTPUT / name).exists():
            raise SystemExit("Refusing to overwrite evidence: " + name)
    result = {"passed": False, "utc_start": utc(), "command": [sys.executable, str(Path(__file__).resolve())],
              "cwd": os.getcwd(), "source_selection": "User-selected frozen latest S1 plus only the cursor repair and final seven-row regression source in S2.",
              "base": str(S1), "successor": str(S2), "mutable_origin": str(ORIGIN), "errors": ERRORS}
    current, conflicts = {}, []
    ordinary = manifest("snapshot-inputs.json")
    tracked = manifest("tracked-guard-inputs.json")
    original_binary = manifest("debug-binary-freeze.json")
    qualified = manifest("qualified-worker-runtime-inputs.json")
    earlier_integrity = manifest("final-input-integrity.json")
    overlay = manifest("guard-repair/overlay.json")
    correction = manifest("guard-repair/regression-build-correction.json")
    source_selection = manifest("snapshot-selection.json")
    original_expected = {}
    enroll(original_expected, {name: row["sha256"] for name, row in ordinary.items()}, conflicts, "S1 ordinary")
    enroll(original_expected, tracked, conflicts, "S1 tracked guard")
    enroll(original_expected, original_binary["files_sha256"], conflicts, "S1 Debug binaries")
    enroll(original_expected, qualified["files"], conflicts, "S1 isolated worker output")
    result["s1_expected_input_count"] = len(original_expected)
    result["s1_input_conflicts"] = conflicts
    require(len(original_expected) == 72_224 and not conflicts, "Original frozen input inventory count/conflicts differ.")
    result["s1_fresh_input_changes"] = compare_expected(original_expected)
    require(not result["s1_fresh_input_changes"], "S1 source/binary inputs changed; reuse is not justified.")
    enroll(current, original_expected, conflicts, "preserved S1 graph")
    print(json.dumps({"s1_frozen_inputs_checked": len(original_expected), "changes": len(result["s1_fresh_input_changes"])}), flush=True)

    expected_guard_hash = next(row["after_sha256"] for row in overlay["changes"] if row["path"] == str(GUARD))
    expected_regression_hash = correction["after_sha256"]
    expected_changed = {str(S2 / GUARD): expected_guard_hash}
    mapped_ordinary = {mapped(name): row["sha256"] for name, row in ordinary.items()}
    mapped_guard = {mapped(name): digest for name, digest in tracked.items()}
    s2_sources = {name: sha(name) for name in mapped_ordinary}
    s2_sources[str(S2 / REGRESSION)] = sha(S2 / REGRESSION)
    ordinary_delta = [{"path": name, "before_sha256": expected, "after_sha256": s2_sources[name]}
                      for name, expected in mapped_ordinary.items() if s2_sources[name] != expected]
    guard_delta = compare_expected(mapped_guard)
    result["s2_ordinary_input_count"] = len(mapped_ordinary)
    result["s2_tracked_guard_input_count"] = len(mapped_guard)
    result["s2_ordinary_changes_from_s1"] = ordinary_delta
    result["s2_tracked_guard_changes_from_s1"] = guard_delta
    require({row["path"]: row["after_sha256"] for row in ordinary_delta} == expected_changed,
            "Unexpected S2 ordinary source/config change.")
    require({row["path"]: row["actual"] for row in guard_delta} == expected_changed,
            "Unexpected S2 tracked guard/evidence change.")
    require(not (S1 / REGRESSION).exists() and s2_sources[str(S2 / REGRESSION)] == expected_regression_hash,
            "Final new regression source differs from its CA1062 correction receipt.")
    before_guard, after_guard = (S1 / GUARD).read_text(), (S2 / GUARD).read_text()
    require(before_guard.count("search = lineStart;") == 2
            and after_guard == before_guard.replace("search = lineStart;", "search = Math.Max(0, lineStart - 1);"),
            "Guard delta is not exactly the two cursor-advance assignments.")
    result["exact_guard_source_diff"] = "".join(difflib.unified_diff(before_guard.splitlines(keepends=True), after_guard.splitlines(keepends=True),
                                                                   fromfile=str(S1 / GUARD), tofile=str(S2 / GUARD)))
    result["new_regression_source"] = {"path": str(S2 / REGRESSION), "sha256": s2_sources[str(S2 / REGRESSION)]}
    enroll(current, {name: sha(name) for name in mapped_guard}, conflicts, "S2 tracked guard current bytes")
    enroll(current, s2_sources, conflicts, "S2 ordinary current bytes plus new regression")
    print(json.dumps({"s2_ordinary_inputs_checked": len(mapped_ordinary), "s2_tracked_guard_inputs_checked": len(mapped_guard),
                      "ordinary_delta_count": len(ordinary_delta), "tracked_guard_delta_count": len(guard_delta)}), flush=True)

    binary_freeze = {"files_sha256": dict(original_binary["files_sha256"]),
                     "assemblies_sha256": dict(original_binary["assemblies_sha256"])}
    expected_unit_changes = {str(S2 / UNIT / ("Hexalith.EventStore.Server.Tests" + extension)) for extension in (".dll", ".pdb")}
    mapped_binary = {mapped(name): digest for name, digest in original_binary["files_sha256"].items()}
    mapped_binary_changes = compare_expected(mapped_binary)
    result["s2_original_binary_freeze_changes"] = mapped_binary_changes
    require({row["path"] for row in mapped_binary_changes} == expected_unit_changes, "S2 producer/dependency images changed beyond the Unit test PE/PDB.")
    enroll(current, {name: sha(name) for name in mapped_binary}, conflicts, "S2 frozen first-party outputs")
    binary_freeze["files_sha256"].update({name: sha(name) for name in mapped_binary})
    s1_unit = {str(path.relative_to(S1 / UNIT)): sha(path) for path in (S1 / UNIT).rglob("Hexalith.*")
               if path.is_file() and path.suffix in (".dll", ".pdb")}
    s2_unit = {str(path.relative_to(S2 / UNIT)): sha(path) for path in (S2 / UNIT).rglob("Hexalith.*")
               if path.is_file() and path.suffix in (".dll", ".pdb")}
    unit_deltas = [{"relative": name, "s1_sha256": s1_unit.get(name), "s2_sha256": s2_unit.get(name)}
                   for name in sorted(set(s1_unit) | set(s2_unit)) if s1_unit.get(name) != s2_unit.get(name)]
    result["unit_first_party_comparison"] = {"s1_paths": len(s1_unit), "s2_paths": len(s2_unit), "changes": unit_deltas}
    require({row["relative"] for row in unit_deltas} == {"Hexalith.EventStore.Server.Tests.dll", "Hexalith.EventStore.Server.Tests.pdb"},
            "Unit output first-party dependencies are not byte-identical.")
    unit_outputs = {str(path): sha(path) for path in sorted((S2 / UNIT).rglob("*")) if path.is_file()}
    enroll(current, unit_outputs, conflicts, "all actual Unit runtime dependency output files")
    binary_freeze["files_sha256"].update(unit_outputs)
    unit_pdb = str(S2 / UNIT / "Hexalith.EventStore.Server.Tests.pdb")
    binary_freeze["assemblies_sha256"][unit_pdb] = sha(unit_pdb)
    result["actual_unit_output_file_count"] = len(unit_outputs)
    result["pdb_pair_manifest"] = {"original_pairs": len(original_binary["assemblies_sha256"]),
                                    "successor_pairs": len(binary_freeze["assemblies_sha256"]), "added_pdb": unit_pdb}
    require(len(original_binary["assemblies_sha256"]) == 63 and len(binary_freeze["assemblies_sha256"]) == 64,
            "Expected original 63 PE/PDB pairs plus the repaired Unit pair.")
    mapped_qualified = {mapped(name): digest for name, digest in qualified["files"].items()}
    result["s2_qualified_worker_output_changes"] = compare_expected(mapped_qualified)
    require(len(mapped_qualified) == 788 and not result["s2_qualified_worker_output_changes"], "Isolated worker runtime changed.")
    enroll(current, mapped_qualified, conflicts, "S2 isolated worker runtime")
    phase_paths = sorted((PACKET / "bounded-scenarios").glob("*/*.json"))
    phase_modules = []
    for path in phase_paths:
        proof = manifest(str(path.relative_to(PACKET)))
        phase_modules += proof["runtime"]
    result["worker_module_identity"] = []
    for name in WORKER_NAMES:
        pe, pdb = WORKER / (name + ".dll"), WORKER / (name + ".pdb")
        pe_hash, pdb_hash = sha(S1 / pe), sha(S1 / pdb)
        observations = [row for row in phase_modules if row["name"] == name]
        authenticated = (len(observations) == 30 and all(row["peSha256"].lower() == pe_hash and row["pdbSha256"].lower() == pdb_hash
                         and row["loadedMvidMatchesPe"] is True for row in observations)
                         and sha(S2 / pe) == pe_hash and sha(S2 / pdb) == pdb_hash)
        mvids = sorted({row["loadedMvid"] for row in observations})
        require(authenticated and len(mvids) == 1, "Worker phase PE/PDB/MVID identity is not reusable: " + name)
        result["worker_module_identity"].append({"module": name, "s1_pe": str(S1 / pe), "s2_pe": str(S2 / pe),
                                                 "pe_sha256": pe_hash, "pdb_sha256": pdb_hash, "phase_observations": len(observations),
                                                 "phase_loaded_mvids": mvids, "passed": authenticated and len(mvids) == 1})

    runtime = OUTPUT / "runtime"
    runtime.mkdir(exist_ok=False)
    archived = []
    for suffix in (".dll", ".pdb", ".deps.json", ".runtimeconfig.json"):
        source = S2 / UNIT / ("Hexalith.EventStore.Server.Tests" + suffix)
        target = runtime / source.name
        shutil.copyfile(source, target)
        require(sha(source) == sha(target), "Archived Unit runtime differs: " + str(source))
        archived.append({"source": str(source), "archive": str(target.relative_to(PACKET)), "sha256": sha(target)})
        current[str(target)] = sha(target)
    for relative in (GUARD, REGRESSION):
        source, target = S2 / relative, runtime / (relative.name + ".source")
        shutil.copyfile(source, target)
        require(sha(source) == sha(target), "Archived Unit source differs: " + str(source))
        archived.append({"source": str(source), "archive": str(target.relative_to(PACKET)), "sha256": sha(target)})
        current[str(target)] = sha(target)
    result["archived_new_unit_runtime_and_source"] = archived
    root_delta = []
    for snapshot_name, row in ordinary.items():
        successor_name, origin_name = mapped(snapshot_name), row["origin"]
        actual = sha(origin_name)
        if actual != s2_sources[successor_name]:
            root_delta.append({"origin": origin_name, "selected_s1_sha256": row["sha256"],
                               "s2_sha256": s2_sources[successor_name], "mutable_origin_sha256": actual})
    result["mutable_origin_differences_from_s2"] = root_delta
    result["mutable_origin_head"] = head_from_files(ORIGIN)
    result["selected_s1_head"] = head_from_files(S1)
    result["s2_head"] = head_from_files(S2)
    result["source_selection_receipt"] = source_selection
    result["earlier_preservation_conflicts_retained"] = {"receipt": "final-input-integrity.json",
                                                         "sha256": INPUT_MANIFESTS["final-input-integrity.json"],
                                                         "folders_passed": earlier_integrity["folders_passed"],
                                                         "folders_changes": earlier_integrity["folders_changes"],
                                                         "all_initial_protected_changes": earlier_integrity["all_initial_protected_changes"],
                                                         "conflicting_input_hashes": earlier_integrity["conflicting_input_hashes"],
                                                         "latest_origin_changes": earlier_integrity["latest_origin_changes"]}
    changed_during_audit = []
    for name, before in STATS.items():
        path = Path(name)
        if not (path.is_relative_to(S1) or path.is_relative_to(S2)):
            continue
        try:
            if fingerprint(path.stat()) != before:
                changed_during_audit.append(name)
        except OSError:
            changed_during_audit.append(name)
    result["snapshot_inputs_changed_during_audit"] = changed_during_audit
    require(not changed_during_audit, "S1/S2 inputs changed during audit.")
    result["current_input_conflicts"] = conflicts
    require(not conflicts and all(value is not None for value in current.values()), "Pinned current inputs have conflicts/missing values.")
    result["pinned_current_paths"] = len(current)
    result["pinned_s2_source_paths"] = len(s2_sources)
    result["source_manifest_sha256"] = INPUT_MANIFESTS
    result["passed"] = not ERRORS
    result["reuse_disposition"] = ("Phase-one fresh solution/Core/actor/nine-case Dapr evidence is reusable for unchanged producer bytes. Only the Unit guard PE/PDB and its two source files changed; repaired Unit execution and its new PDB source audit remain separate required evidence."
                                   if result["passed"] else "Reuse is not justified: inspect every recorded mismatch.")
    result["mutable_origin_disposition"] = "The mutable origin is outside this source selection and is not qualified by S1/S2 execution. Existing Folders/source preservation conflicts remain open and unchanged."
    result["utc_end"] = utc()
    write_new("source-inputs.json", s2_sources)
    write_new("binary-freeze.json", binary_freeze)
    write_new("current-inputs.json", current)
    write_new("input-identity-audit.json", result)
    print(json.dumps({"passed": result["passed"], "s1_frozen_inputs": len(original_expected), "s2_guard_inputs": len(mapped_guard),
                      "source_paths": len(s2_sources), "pinned_paths": len(current), "unit_first_party_changes": len(unit_deltas), "errors": ERRORS}), flush=True)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
