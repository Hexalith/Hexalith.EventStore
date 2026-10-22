#!/usr/bin/env python3
"""Join retained actor OS-map hashes to frozen PE/PDB/source identities and cleanup.

MVIDs originate in the exact-PE metadata binding, not in the OS-map receipt or a
CLR self-report. This audit consumes the fresh actor execution already retained
by this packet; it does not rerun actor behavior or claim Folders acceptance.
"""
from collections import defaultdict
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

PACKET = Path(__file__).resolve().parent
INPUTS = {}
ERRORS = []


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read(relative):
    path = PACKET / relative
    INPUTS[relative] = sha(path)
    return json.loads(path.read_text())


def require(predicate, description):
    if not predicate:
        ERRORS.append(description)


def command(argv):
    started = datetime.now(timezone.utc).isoformat()
    result = subprocess.run(argv, capture_output=True, text=True, check=False, timeout=30)
    return {"argv": argv, "utc_start": started, "exit_code": result.returncode,
            "stdout": result.stdout, "stderr": result.stderr}


def main():
    bindings = read("source-binding-final/all-pdb-documents.json")
    freeze = read("debug-binary-freeze.json")["files_sha256"]
    by_pe = defaultdict(list)
    for binding in bindings:
        by_pe[binding["peSha256"].lower()].append(binding)
    processes = read("actor-live/processes.json")
    by_pid = {row["pid"]: row for row in processes}
    runtime_inputs = read("actor-live/runtime-inputs.json")
    infrastructure = read("actor-live/infrastructure.json")
    cleanup = read("actor-live/cleanup.json")
    execution = read("actor-live-command.json")
    checks = read("actor-live/checks.json")
    commands = read("actor-live/infrastructure-commands.json")
    observations, map_summaries = [], []
    framework_versions = set()
    expected_counts = {"eventstore-1-mapped-images.json": 10,
                       "eventstore-2-mapped-images.json": 10,
                       "sample-1-mapped-images.json": 6}
    map_files = sorted((PACKET / "actor-live").glob("*-mapped-images.json"))
    require({path.name for path in map_files} == set(expected_counts), "Actor map receipt inventory differs.")
    for path in map_files:
        relative = str(path.relative_to(PACKET))
        mapped = read(relative)
        pid = mapped["pid"]
        require(pid in by_pid, "Mapped actor PID absent from process receipt: " + str(pid))
        runtime = "sample" if path.name.startswith("sample") else "eventstore"
        rows = [(name, row) for name, row in mapped["images"].items()
                if Path(name).name.startswith("Hexalith.")]
        require(len(rows) == expected_counts[path.name], "Wrong first-party actor module count: " + path.name)
        for image_path in mapped["images"]:
            match = re.search(r"/shared/(Microsoft\.(?:NETCore|AspNetCore)\.App)/([^/]+)/", image_path)
            if match:
                framework_versions.add((match[1], match[2]))
        map_summaries.append({"receipt": relative, "pid": pid, "first_party_modules": len(rows),
                              "total_mapped_modules": len(mapped["images"]),
                              "argv": by_pid.get(pid, {}).get("argv")})
        for image_path, image in sorted(rows):
            errors_before = len(ERRORS)
            pe_hash = image["dll_sha256"].lower()
            pdb_hash = (image["pdb_sha256"] or "").lower()
            candidates = [row for row in by_pe.get(pe_hash, []) if row["pdbSha256"].lower() == pdb_hash]
            require(bool(candidates), "Actor mapped PE/PDB pair has no source binding: " + image_path)
            mvids = {str(uuid.UUID(row["mvid"])) for row in candidates}
            require(len(mvids) == 1, "Missing/ambiguous bound PE MVID: " + image_path)
            bound_rows = []
            for binding in candidates:
                require(binding["identityMatches"] is True, "Bound PE/PDB identity mismatch: " + binding["pe"])
                require(bool(binding["documents"]) and all(doc["matches"] is True for doc in binding["documents"]),
                        "Bound source checksum mismatch: " + binding["pe"])
                require(freeze.get(binding["pe"]) == pe_hash and freeze.get(binding["pdb"]) == pdb_hash,
                        "Binding differs from frozen Debug inputs: " + binding["pe"])
                require(sha(binding["pe"]) == pe_hash and sha(binding["pdb"]) == pdb_hash,
                        "Current frozen snapshot PE/PDB differs: " + binding["pe"])
                bound_rows.append({"pe": binding["pe"], "pdb": binding["pdb"], "mvid": binding["mvid"],
                                   "documents": len(binding["documents"]), "identity_matches": binding["identityMatches"]})
            filename = Path(image_path).name
            pdb_name = Path(filename).with_suffix(".pdb").name
            selection = runtime_inputs[runtime]["files"]
            require(selection.get(filename) == pe_hash and selection.get(pdb_name) == pdb_hash,
                    "Runtime selection differs from mapped PE/PDB: " + image_path)
            retained = PACKET / "actor-live/qualified-runtime" / runtime / filename
            require(sha(retained) == pe_hash and sha(retained.with_suffix(".pdb")) == pdb_hash,
                    "Retained actor PE/PDB differs from mapped pair: " + image_path)
            observations.append({"pid": pid, "maps_receipt": relative, "mapped_path": image_path,
                                 "pe_sha256": pe_hash, "pdb_sha256": pdb_hash,
                                 "mvid_from_exact_pe_binding": next(iter(mvids)) if len(mvids) == 1 else None,
                                 "source_bindings": bound_rows, "retained_pe": str(retained.relative_to(PACKET)),
                                 "passed": len(ERRORS) == errors_before})
    require(len(observations) == 26 and all(row["passed"] for row in observations),
            "Expected all 26 first-party actor observations to authenticate.")
    require(len({row["pid"] for row in map_summaries}) == 3, "Expected three distinct actor application PIDs.")
    require(execution["exit_code"] == 0, "Fresh actor execution failed.")
    require(len(checks) == 47 and all(row["passed"] is True for row in checks), "Expected 47 successful actor checks.")
    xml_relative = "actor-live.xml"
    INPUTS[xml_relative] = sha(PACKET / xml_relative)
    xml = ET.parse(PACKET / xml_relative).getroot()
    assemblies = list(xml.findall("assembly"))
    require(len(assemblies) == 1 and assemblies[0].get("total") == "1"
            and assemblies[0].get("passed") == "1" and assemblies[0].get("failed") == "0"
            and assemblies[0].get("skipped") == "0" and assemblies[0].get("errors") == "0",
            "Actor test XML does not report exactly one passing, unskipped test.")
    native_daprd = {"path": infrastructure["daprd"], "recorded_sha256": infrastructure["daprd_sha256"],
                    "current_file_sha256": sha(infrastructure["daprd"])}
    require(native_daprd["recorded_sha256"] == native_daprd["current_file_sha256"], "Native daprd file changed.")
    sidecars = [row for row in processes if Path(row["argv"][0]).name == "daprd"]
    require(len(sidecars) == 3, "Expected three actor-sidecar process records.")
    sidecar_logs = []
    for path in sorted((PACKET / "actor-live").glob("*-sidecar.stdout.log")):
        relative = str(path.relative_to(PACKET))
        INPUTS[relative] = sha(path)
        versions = sorted(set(re.findall(r"\bver=([^\s]+)", path.read_text())))
        require(versions == ["1.18.7"], "Unexpected/missing native daprd log version: " + relative)
        sidecar_logs.append({"path": relative, "recorded_versions": versions})
    container_rows = []
    for role in ("redis", "placement", "scheduler"):
        role_image = read("actor-live/" + role + "-image.json")
        require(len(role_image) == 1, "Ambiguous container image: " + role)
        image = role_image[0]
        name = infrastructure["namespace"] + "-" + role
        clean = next((row for row in cleanup["containers"] if row["name"] == name), None)
        require(clean is not None, "Container has no cleanup identity: " + name)
        if clean is None:
            continue
        container_id = clean["id"]
        created = [(index, row) for index, row in enumerate(commands)
                   if row["argv"][:3] == ["docker", "run", "--rm"] and name in row["argv"]
                   and row["stdout"].strip() == container_id and row["exit_code"] == 0]
        require(len(created) == 1, "Container creation receipt mismatch: " + name)
        inspections = []
        for index, row in enumerate(commands):
            if row["argv"] == ["docker", "container", "inspect", name] and row["exit_code"] == 0:
                for value in json.loads(row["stdout"]):
                    if value["Id"] == container_id:
                        inspections.append((index, value))
        require(bool(inspections), "Container identity has no retained inspect: " + name)
        for _, inspected in inspections:
            require(inspected["Image"] == image["Id"], "Container image identity mismatch: " + name)
            require(inspected["Config"]["Labels"].get("hexalith.qualification.run-id") == infrastructure["namespace"],
                    "Container ownership label mismatch: " + name)
        removes = [index for index, row in enumerate(commands)
                   if row["argv"] == ["docker", "rm", "-f", container_id] and row["exit_code"] == 0]
        require(bool(removes) and clean["remove_exit_code"] == 0 and clean["absent"] is True
                and not clean["remaining_ids"], "Container cleanup receipt failed: " + name)
        absence = command(["docker", "container", "inspect", container_id])
        require(absence["exit_code"] != 0 and "no such" in absence["stderr"].lower(),
                "Exact actor container ID is not independently absent: " + container_id)
        container_rows.append({"role": role, "name": name, "container_id": container_id,
                               "image_id": image["Id"], "repo_digests": image["RepoDigests"],
                               "created_argv": created[0][1]["argv"] if created else None,
                               "creation_command_indices": [index for index, _ in created],
                               "inspect_command_indices": [index for index, _ in inspections],
                               "remove_command_indices": removes, "cleanup": clean,
                               "fresh_read_only_absence_probe": absence})
    require(len(cleanup["processes"]) == 6 and {row["pid"] for row in cleanup["processes"]} == set(by_pid)
            and all(row["absent"] is True for row in cleanup["processes"]), "Actor process cleanup coverage failed.")
    require(cleanup["passed"] is True and cleanup["runtime_absent"] is True
            and not Path(cleanup["runtime"]).exists() and all(row["closed"] is True for row in cleanup["logs"]),
            "Actor fixture directory/log cleanup failed.")
    require({"pubsub.yaml", "statestore.yaml"}.issubset(infrastructure["components"]),
            "Missing exact materialized actor Dapr components.")
    result = {"passed": not ERRORS, "audit_utc": datetime.now(timezone.utc).isoformat(),
              "audit_command": [sys.executable, str(Path(__file__).resolve())],
              "execution_kind": "Fresh artifact audit of fresh actor execution; actor behavior was not rerun by this audit.",
              "mvid_attribution": "MVIDs below come from source-binding-final PE metadata authenticated by exact mapped PE/PDB hashes. OS maps do not self-report MVIDs; no CLR loaded-MVID claim is made.",
              "process_identity_limit": "Actor process receipts retain PID/argv and mapped hashes, without PID start ticks. Their original cleanup checks report all six owned processes absent.",
              "first_party_observation_count": len(observations), "authenticated_observation_count": sum(row["passed"] for row in observations),
              "map_receipts": map_summaries, "first_party_observations": observations,
              "fresh_actor_checks": {"count": len(checks), "passed": sum(row["passed"] is True for row in checks)},
              "actor_execution": execution, "test_xml_environment": [row.get("environment") for row in assemblies],
              "mapped_framework_versions": sorted([{"framework": name, "version": version} for name, version in framework_versions], key=lambda row: row["framework"]),
              "native_daprd": native_daprd, "sidecars": sidecars, "sidecar_log_versions": sidecar_logs,
              "containers": container_rows, "materialized_components": infrastructure["components"],
              "discovery_configuration": infrastructure["discovery"], "cleanup": cleanup,
              "current_runtime_directory_absent": not Path(cleanup["runtime"]).exists(),
              "input_sha256": dict(sorted(INPUTS.items())), "errors": ERRORS}
    if "--stdout" in sys.argv:
        print(json.dumps(result, indent=2))
    else:
        with (PACKET / "actor-runtime-audit.json").open("x") as stream:
            json.dump(result, stream, indent=2)
            stream.write("\n")
        print(json.dumps({key: result[key] for key in ("passed", "first_party_observation_count", "authenticated_observation_count", "errors")}))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
