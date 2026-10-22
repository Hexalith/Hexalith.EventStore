#!/usr/bin/env python3
"""Independently inspect exact resources owned by the two continuation packets.

This performs read-only Docker and /proc checks. It neither signals processes
nor removes files or containers. Earlier cancelled/coverage failures stay in the
retained inputs; current resource absence does not relabel their acceptance.
"""
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

PACKET = Path(__file__).resolve().parent
INITIAL = PACKET.parent / "folders-12-2-gates-20261022T151209Z"
SOURCE_SNAPSHOT = Path("/tmp/eventstore-folders-12-2-latest-20261022T155454Z")
INPUTS = {}
ERRORS = []


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def load(path):
    INPUTS[str(path)] = sha(path)
    return json.loads(path.read_text())


def require(condition, message):
    if not condition:
        ERRORS.append(message)


def run(argv):
    started = datetime.now(timezone.utc).isoformat()
    process = subprocess.run(argv, capture_output=True, text=True, check=False, timeout=30)
    return {"argv": argv, "utc_start": started, "utc_end": datetime.now(timezone.utc).isoformat(),
            "exit_code": process.returncode, "stdout": process.stdout, "stderr": process.stderr}


def process_identity_absent(pid, expected_start):
    row = {"pid": pid, "recorded_start_ticks": str(expected_start),
           "observed_at_utc": datetime.now(timezone.utc).isoformat()}
    try:
        actual = (Path("/proc") / str(pid) / "stat").read_text().rsplit(")", 1)[1].split()[19]
        row.update({"current_start_ticks": actual, "pid_currently_present": True,
                    "recorded_identity_absent": actual != str(expected_start)})
    except FileNotFoundError:
        row.update({"pid_currently_present": False, "recorded_identity_absent": True})
    except (OSError, ValueError, IndexError) as error:
        row.update({"recorded_identity_absent": False, "error": repr(error)})
    return row


def main():
    result = {"passed": False, "utc_start": datetime.now(timezone.utc).isoformat(),
              "command": [sys.executable, str(Path(__file__).resolve())],
              "auditor_sha256": sha(Path(__file__).resolve()),
              "scope": [str(INITIAL), str(PACKET)],
              "earlier_failures_preserved": "This current resource-absence audit does not replace cancelled acceptance or missing observer coverage in the sealed continuation.",
              "retained_source_snapshot": {"path": str(SOURCE_SNAPSHOT), "exists": SOURCE_SNAPSHOT.exists(),
                                            "disposition": "Deliberately retained build and review input."},
              "cleanup_receipts": [], "containers": [], "directories": [], "process_identities": [], "errors": ERRORS}
    try:
        # A successful terminal result and all worker records must already exist.
        terminal = load(PACKET / "bounded-infrastructure/result.json")
        require(terminal.get("test_exit_code") == 0, "Latest bounded matrix is not successfully complete.")
        require(len(list((PACKET / "bounded-scenarios").glob("*/*.json"))) == 30
                and len(list((PACKET / "worker-maps").glob("*.json"))) == 30,
                "Latest matrix does not have 30 phase proofs and 30 captures.")
        if ERRORS:
            raise RuntimeError("Final cleanup audit requires the completed matrix.")
        initial_seal_path = INITIAL / "SHA256SUMS"
        INPUTS[str(initial_seal_path)] = sha(initial_seal_path)
        initial_seal = {}
        for line in initial_seal_path.read_text().splitlines():
            digest, name = line.split(maxsplit=1)
            initial_seal[name.removeprefix("*")] = digest.lower()
        result["initial_continuation_seal_sha256"] = INPUTS[str(initial_seal_path)]
        directories = {}
        identities = {}
        seen_containers = set()
        for packet in (INITIAL, PACKET):
            cleanups = sorted(packet.rglob("cleanup.json"))
            require(bool(cleanups), "No cleanup receipts in scoped packet: " + str(packet))
            for path in cleanups:
                cleanup = load(path)
                result["cleanup_receipts"].append({"path": str(path), "recorded_passed": cleanup.get("passed"),
                                                    "recorded_runtime_absent": cleanup.get("runtime_absent"),
                                                    "container_count": len(cleanup.get("containers", [])),
                                                    "recorded_processes": cleanup.get("processes", [])})
                if cleanup.get("runtime"):
                    directories.setdefault(cleanup["runtime"], []).append({"receipt": str(path), "field": "runtime"})
                commands = load(path.parent / "infrastructure-commands.json")
                for container in cleanup.get("containers", []):
                    name, container_id = container["name"], container["id"]
                    require(bool(re.fullmatch(r"[0-9a-f]{64}", container_id)), "Container identity is not a full ID: " + name)
                    require(container_id not in seen_containers, "Duplicate owned container identity: " + container_id)
                    seen_containers.add(container_id)
                    role = next((role for role in ("redis", "placement", "scheduler") if name.endswith("-" + role)), None)
                    require(role is not None, "Unexpected scoped container role: " + name)
                    if role is None:
                        continue
                    image_rows = load(path.parent / (role + "-image.json"))
                    require(len(image_rows) == 1, "Saved image identity is ambiguous: " + name)
                    image = image_rows[0]
                    require(bool(image.get("RepoDigests")), "Saved image digest is absent: " + name)
                    creates = [(index, row) for index, row in enumerate(commands)
                               if row["argv"][:3] == ["docker", "run", "--rm"]
                               and "--name" in row["argv"] and row["argv"][row["argv"].index("--name") + 1] == name
                               and row["exit_code"] == 0 and row["stdout"].strip() == container_id]
                    require(len(creates) == 1, "Container creation ID/config receipt missing: " + name)
                    label = "hexalith.qualification.run-id=" + name.removesuffix("-" + role)
                    if creates:
                        argv = creates[0][1]["argv"]
                        require(any(argv[index:index + 2] == ["--label", label] for index in range(len(argv))),
                                "Container creation ownership label differs: " + name)
                        require(any(tag in argv for tag in image.get("RepoTags", [])),
                                "Container creation image differs from saved image tag: " + name)
                    removes = [index for index, row in enumerate(commands)
                               if row["argv"] == ["docker", "rm", "-f", container_id] and row["exit_code"] == 0]
                    require(bool(removes), "Exact owned container removal receipt missing: " + name)
                    inspections = []
                    for index, row in enumerate(commands):
                        if row["argv"] == ["docker", "container", "inspect", name] and row["exit_code"] == 0:
                            for inspected in json.loads(row["stdout"]):
                                if inspected["Id"] == container_id:
                                    inspections.append((index, inspected))
                    require(bool(inspections), "Cleanup-time actual container image identity missing: " + name)
                    for index, inspected in inspections:
                        require(inspected["Image"] == image["Id"], "Actual container image ID differs from saved image: " + name)
                        require(inspected["Name"] == "/" + name, "Actual container name differs: " + name)
                        require(inspected["Config"].get("Labels", {}).get("hexalith.qualification.run-id") == label.split("=", 1)[1],
                                "Actual container ownership label differs: " + name)
                        require(any(index < remove for remove in removes), "Container inspect does not precede cleanup removal: " + name)
                    inspect = run(["docker", "container", "inspect", container_id])
                    listing = run(["docker", "container", "ls", "--all", "--no-trunc", "--filter", "id=" + container_id,
                                   "--format", "{{.ID}}"])
                    absent = (inspect["exit_code"] != 0 and "no such" in inspect["stderr"].lower()
                              and listing["exit_code"] == 0 and not listing["stdout"].strip())
                    require(absent, "Owned container is not independently absent: " + container_id)
                    result["containers"].append({"cleanup_receipt": str(path), "name": name, "id": container_id,
                                                  "role": role, "saved_image_id": image["Id"], "saved_repo_digests": image["RepoDigests"],
                                                  "creation_argv": creates[0][1]["argv"] if creates else None,
                                                  "creation_command_indices": [index for index, _ in creates],
                                                  "cleanup_inspections": [{"command_index": index, "actual_image_id": value["Image"],
                                                                            "actual_label": value["Config"].get("Labels", {}).get("hexalith.qualification.run-id")}
                                                                           for index, value in inspections],
                                                  "remove_command_indices": removes, "recorded_cleanup": container,
                                                  "fresh_inspect": inspect, "fresh_id_listing": listing, "absent": absent})
            for path in sorted(packet.rglob("observed-cleanup.json")):
                cleanup = load(path)
                result["cleanup_receipts"].append({"path": str(path), "recorded_passed": cleanup.get("passed"),
                                                    "recorded_coverage_present": cleanup.get("coverage_present"),
                                                    "recorded_remaining_processes": cleanup.get("remaining_processes")})
                for directory, recorded_absent in cleanup.get("component_directories_absent", {}).items():
                    directories.setdefault(directory, []).append({"receipt": str(path), "field": "component_directories_absent",
                                                                  "recorded_absent": recorded_absent})
            for path in sorted(packet.rglob("components.json")):
                components = load(path)
                for directory in components:
                    directories.setdefault(directory, []).append({"receipt": str(path), "field": "materialized component directory"})
            for path in sorted(packet.rglob("observed-processes.json")):
                observed = load(path)
                for row in observed.values():
                    identities.setdefault((row["pid"], str(row["start_ticks"])), []).append(str(path))
            for path in sorted((packet / "worker-maps").glob("*.json")):
                capture = load(path)
                identities.setdefault((capture["pid"], str(capture["start_ticks"])), []).append(str(path))
                if "observer_pid" in capture and "observer_start_ticks" in capture:
                    identities.setdefault((capture["observer_pid"], str(capture["observer_start_ticks"])), []).append(str(path))
        require(str(SOURCE_SNAPSHOT) not in directories, "A runtime cleanup receipt unexpectedly names the retained source snapshot.")
        for directory, provenance in sorted(directories.items()):
            absent = Path(directory).is_absolute() and not os.path.lexists(directory)
            require(absent, "Recorded owned runtime/component directory remains: " + directory)
            result["directories"].append({"path": directory, "absent": absent, "receipts": provenance,
                                           "observed_at_utc": datetime.now(timezone.utc).isoformat()})
        for (pid, ticks), provenance in sorted(identities.items()):
            identity = process_identity_absent(pid, ticks)
            identity["receipts"] = provenance
            require(identity["recorded_identity_absent"], "Exact bounded process identity remains: " + str(pid) + ":" + ticks)
            result["process_identities"].append(identity)
        require(bool(result["process_identities"]), "No exact bounded PID/start identities were audited.")
        sealed_inputs = 0
        for filename, digest in INPUTS.items():
            path = Path(filename)
            if path.is_relative_to(INITIAL) and path != initial_seal_path:
                relative = str(path.relative_to(INITIAL))
                require(initial_seal.get(relative) == digest, "Initial continuation input differs from its seal: " + relative)
                sealed_inputs += 1
        result["sealed_initial_inputs_verified"] = sealed_inputs
        result["input_sha256"] = dict(sorted(INPUTS.items()))
        result["inputs_still_unchanged"] = all(sha(path) == digest for path, digest in INPUTS.items())
        require(result["inputs_still_unchanged"], "A scoped cleanup audit input changed during inspection.")
        result["container_count"] = len(result["containers"])
        result["exact_process_identity_count"] = len(result["process_identities"])
        result["directory_count"] = len(result["directories"])
        result["passed"] = not ERRORS
    except Exception as error:
        ERRORS.append(type(error).__name__ + ": " + str(error))
    result["utc_end"] = datetime.now(timezone.utc).isoformat()
    with (PACKET / "closing-runtime-cleanup.json").open("x") as stream:
        json.dump(result, stream, indent=2)
        stream.write("\n")
    print(json.dumps({"passed": result["passed"], "containers": len(result["containers"]),
                      "directories": len(result["directories"]), "exact_process_identities": len(result["process_identities"]), "errors": ERRORS}))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
