#!/usr/bin/env python3
"""Run isolated negative controls only after the final nine-case matrix completes.

The exact auditors and JSON inputs are copied as ordinary files into one owned
temporary directory. Each control changes one logical field, runs its auditor,
and restores the copied input. Original evidence and source remain read-only.
"""
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

PACKET = Path(__file__).resolve().parent
SOURCE_ROOT = Path("/tmp/eventstore-folders-12-2-latest-20261022T155454Z")
RUNTIME = "audit-runtime.py"
CAPTURE = "audit-worker-captures.py"


def sha_bytes(data):
    return hashlib.sha256(data).hexdigest()


def read_json(path):
    return json.loads(Path(path).read_text())


def run_auditor(workspace, name, label):
    argv = [sys.executable, str(workspace / name)]
    output = "bounded-runtime-audit.json" if name == RUNTIME else "worker-capture-audit.json"
    (workspace / output).unlink(missing_ok=True)
    if name == RUNTIME:
        (workspace / 'bounded-summary.json').unlink(missing_ok=True)
    started = datetime.now(timezone.utc).isoformat()
    result = subprocess.run(argv, cwd=workspace,
                            env=dict(os.environ, EVENTSTORE_QUALIFICATION_SOURCE_ROOT=str(SOURCE_ROOT)),
                            capture_output=True, text=True, check=False)
    audit = read_json(workspace / output) if (workspace / output).exists() else None
    return {"label": label, "argv": argv, "cwd": str(workspace),
            "environment_overrides": {"EVENTSTORE_QUALIFICATION_SOURCE_ROOT": str(SOURCE_ROOT)},
            "auditor_sha256": sha_bytes((workspace / name).read_bytes()),
            "utc_start": started, "utc_end": datetime.now(timezone.utc).isoformat(),
            "exit_code": result.returncode, "stdout": result.stdout, "stderr": result.stderr,
            "audit_passed": audit.get("passed") if isinstance(audit, dict) else None,
            "audit_errors": audit.get("errors") if isinstance(audit, dict) else None}


def main():
    receipt = {"passed": False, "utc_start": datetime.now(timezone.utc).isoformat(),
               "command": [sys.executable, str(Path(__file__).resolve())],
               "runner_sha256": sha_bytes(Path(__file__).read_bytes()),
               "source_root": str(SOURCE_ROOT), "baselines": [], "controls": [], "errors": []}
    originals = {}
    workspace = None
    temporary = None
    try:
        # This precondition prevents incomplete live evidence from becoming a baseline.
        proofs = sorted((PACKET / "bounded-scenarios").glob("*/*.json"))
        captures = sorted((PACKET / "worker-maps").glob("*.json"))
        terminal = read_json(PACKET / "bounded-infrastructure/result.json")
        if len(proofs) != 30 or len(captures) != 30 or terminal.get("test_exit_code") != 0:
            raise RuntimeError("Require the completed successful matrix, 30 proofs and 30 captures before controls.")
        relative_inputs = [RUNTIME, CAPTURE,
                           "bounded-infrastructure/observed-processes.json",
                           "bounded-infrastructure/mapped-images.json",
                           "bounded-infrastructure/components.json",
                           "bounded-infrastructure/observed-cleanup.json",
                           "bounded-infrastructure/result.json",
                           "source-binding-final/all-pdb-documents.json"]
        relative_inputs += [str(path.relative_to(PACKET)) for path in proofs + captures]
        temporary = tempfile.TemporaryDirectory(prefix="folders-12-2-audit-controls-")
        workspace = Path(temporary.name)
        receipt["owned_temporary_directory"] = str(workspace)
        for relative in relative_inputs:
            source, target = PACKET / relative, workspace / relative
            if source.is_symlink() or not source.is_file():
                raise RuntimeError("Control input must be an ordinary file: " + relative)
            originals[relative] = source.read_bytes()
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            if source.samefile(target) or target.read_bytes() != originals[relative]:
                raise RuntimeError("Control input is not a separate exact byte copy: " + relative)
        observer = SOURCE_ROOT / "tools/bounded_worker_observer.py"
        receipt["observer_input"] = {"path": str(observer), "sha256": sha_bytes(observer.read_bytes())}
        receipt["input_sha256"] = {name: sha_bytes(data) for name, data in sorted(originals.items())}
        for name in (RUNTIME, CAPTURE):
            baseline = run_auditor(workspace, name, "pristine baseline")
            receipt["baselines"].append(baseline)
            if baseline["exit_code"] != 0 or baseline["audit_passed"] is not True or baseline["audit_errors"]:
                raise RuntimeError("Pristine control baseline failed: " + name)

        def control(label, auditor, edits, relevant_error):
            mutations = []
            try:
                for relative, mutate in edits:
                    value = json.loads(originals[relative])
                    detail = mutate(value)
                    encoded = (json.dumps(value, indent=2) + "\n").encode()
                    (workspace / relative).write_bytes(encoded)
                    mutations.append({"path": relative, "original_file_sha256": sha_bytes(originals[relative]),
                                      "mutated_file_sha256": sha_bytes(encoded), **detail})
                result = run_auditor(workspace, auditor, label)
                rejected = (result["exit_code"] != 0 and result["audit_passed"] is False
                            and any(relevant_error in error for error in (result["audit_errors"] or [])))
                receipt["controls"].append({"label": label, "expected_error_contains": relevant_error,
                                            "mutations": mutations, "execution": result, "passed": rejected})
                if not rejected:
                    receipt["errors"].append("Control did not reject with its relevant error: " + label)
            finally:
                for relative, _ in edits:
                    (workspace / relative).write_bytes(originals[relative])
                    if (workspace / relative).read_bytes() != originals[relative]:
                        raise RuntimeError("Control restoration failed: " + relative)

        def replace(pointer, key, replacement):
            def mutate(value):
                target = value
                for segment in pointer:
                    target = target[segment]
                before = target[key]
                target[key] = replacement
                return {"field": "/" + "/".join(str(item) for item in (*pointer, key)),
                        "before": before, "after": replacement}
            return mutate

        scenario_paths = sorted((workspace / "bounded-scenarios").iterdir())
        scenario_rows = [(directory, read_json(sorted(directory.glob("*.json"))[0])) for directory in scenario_paths]
        source_scenario, duplicate_scenario = next(
            ((left, first), (right, second))
            for left, first in scenario_rows for right, second in scenario_rows
            if left != right and first["bufferedOwner"] == second["bufferedOwner"]
            and first["inheritOwner"] == second["inheritOwner"] and first["count"] != second["count"])
        source_directory, source_identity = source_scenario
        _, duplicate_identity = duplicate_scenario
        duplicate_edits = [(str(path.relative_to(workspace)), replace((), "count", duplicate_identity["count"]))
                           for path in sorted(source_directory.glob("*.json"))]
        control("duplicate scenario tuple with consistent phases", RUNTIME, duplicate_edits,
                "Expected the exact unchanged nine-case matrix.")
        stage_path = next(path for path in proofs if read_json(path)["phase"] == "stage")
        stage_relative = str(stage_path.relative_to(PACKET))
        stage = json.loads(originals[stage_relative])
        control("stage prepared marker missing", RUNTIME,
                [(stage_relative, replace(("observations",), "preparedMarkerObserved", False))],
                "Incomplete stage interruption proof:")
        control("physical value exceeds byte limit", RUNTIME,
                [(stage_relative, replace(("measurements",), "MaximumPhysicalValueBytes", 1_048_577))],
                "Storage bound exceeded:")
        control("identity value reaches forbidden byte limit", RUNTIME,
                [(stage_relative, replace(("measurements",), "MaximumIdentityValueBytes", 65_536))],
                "Storage bound exceeded:")

        def remove_worker_server_images(value):
            removed = [{"index": index, "value": row} for index, row in enumerate(value)
                       if row["pid"] == stage["pid"] and Path(row["path"]).stem == "Hexalith.EventStore.Server"]
            if not removed:
                raise RuntimeError("Selected worker has no Server maps to remove.")
            for row in reversed(removed):
                del value[row["index"]]
            return {"field": "worker Server entries", "selector": {"pid": stage["pid"], "module": "Hexalith.EventStore.Server"},
                    "operation": "remove all matching array entries", "removed": removed}

        control("worker Server external mapping absent", RUNTIME,
                [("bounded-infrastructure/mapped-images.json", remove_worker_server_images)],
                "Worker module observation gap:")
        capture_path = next(path for path in captures if read_json(path)["pid"] == stage["pid"])
        capture_relative = str(capture_path.relative_to(PACKET))
        capture = json.loads(originals[capture_relative])
        control("capture process start identity changes", CAPTURE,
                [(capture_relative, replace((), "start_ticks_after", str(int(capture["start_ticks"]) + 1)))],
                "Rejected or changed process identity:")
        server = next(module for module in capture["request"]["modules"] if module["name"] == "Hexalith.EventStore.Server")
        image_index = next(index for index, image in enumerate(capture["images"]) if image["path"] == server["pe"])
        control("capture Server mapping inode differs", CAPTURE,
                [(capture_relative, replace(("images", image_index), "inode", capture["images"][image_index]["inode"] + 1))],
                "Module is not authenticated by retained raw mappings:")

        def remove_raw_server_rows(value):
            before = value["raw_maps"]
            removed = [row["raw"] for row in value["images"][image_index]["mapping_rows"]]
            retained = [line for line in before.splitlines() if line not in set(removed)]
            value["raw_maps"] = "\n".join(retained) + "\n"
            if value["raw_maps"] == before:
                raise RuntimeError("Selected Server raw mapping rows were not present.")
            return {"field": "/raw_maps", "operation": "remove exactly the listed complete raw mapping lines",
                    "removed_lines": removed, "before_sha256": sha_bytes(before.encode()),
                    "after_sha256": sha_bytes(value["raw_maps"].encode())}

        control("capture Server raw map rows absent", CAPTURE, [(capture_relative, remove_raw_server_rows)],
                "Module is not authenticated by retained raw mappings:")
        # A crashing auditor must never borrow a prior relevant rejection.
        stale_path = workspace / 'bounded-runtime-audit.json'
        stale_path.write_text(json.dumps({'passed': False, 'errors': ['Storage bound exceeded: stale']}) + '\n')
        crashed_source = b"raise RuntimeError('Intentional stale-result rejection control')\n"
        try:
            (workspace / RUNTIME).write_bytes(crashed_source)
            crash = run_auditor(workspace, RUNTIME, 'crashed auditor cannot inherit stale rejection')
            crash['passed'] = crash['exit_code'] != 0 and crash['audit_passed'] is None and crash['audit_errors'] is None and not stale_path.exists()
            receipt['stale_result_control'] = crash
            if not crash['passed']:
                receipt['errors'].append('Crashed auditor inherited a stale rejection.')
        finally:
            (workspace / RUNTIME).write_bytes(originals[RUNTIME])
        receipt["copied_inputs_restored"] = all((workspace / name).read_bytes() == data for name, data in originals.items())
        if not receipt["copied_inputs_restored"]:
            receipt["errors"].append("Copied auditor inputs were not restored.")
        receipt["passed"] = len(receipt["controls"]) == 8 and all(row["passed"] for row in receipt["controls"]) and not receipt["errors"]
    except Exception as error:
        receipt["errors"].append(type(error).__name__ + ": " + str(error))
    finally:
        if temporary is not None:
            temporary.cleanup()
        receipt["owned_temporary_directory_absent"] = workspace is None or not workspace.exists()
        receipt["original_inputs_unchanged"] = all((PACKET / name).read_bytes() == data for name, data in originals.items())
        if not receipt["owned_temporary_directory_absent"] or not receipt["original_inputs_unchanged"]:
            receipt["passed"] = False
            receipt["errors"].append("Temporary cleanup or original input preservation failed.")
        receipt["utc_end"] = datetime.now(timezone.utc).isoformat()
        with (PACKET / "audit-controls-result.json").open("x") as stream:
            json.dump(receipt, stream, indent=2)
            stream.write("\n")
    print(json.dumps({key: receipt[key] for key in ("passed", "errors", "owned_temporary_directory_absent", "original_inputs_unchanged")}))
    return 0 if receipt["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
