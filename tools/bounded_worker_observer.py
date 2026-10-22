#!/usr/bin/env python3
"""Authenticate a waiting bounded worker's existing Linux module mappings.

The worker starts this child only after loading and hashing its four assemblies,
then waits under its unchanged outer phase watchdog. This observer never loads or
maps a target DLL. Its receipt binds real /proc mappings to open-file device/inode
identities and hashes, while retaining failures as well as successes.
"""
import argparse
import base64
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys

REQUIRED = frozenset({
    "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
    "Hexalith.EventStore.Server", "Hexalith.EventStore.Server.LiveSidecar.Tests",
})


def digest(stream):
    return hashlib.file_digest(stream, "sha256").hexdigest()


def start_ticks(pid):
    return (Path("/proc") / str(pid) / "stat").read_text().rsplit(")", 1)[1].split()[19]


def mapping_rows(raw):
    rows = []
    for line in raw.splitlines():
        fields = line.split(None, 5)
        if len(fields) != 6 or not fields[5].startswith("/"):
            continue
        major, minor = fields[3].split(":")
        rows.append({"raw": line, "address": fields[0], "permissions": fields[1],
                     "offset": fields[2], "device_major": int(major, 16),
                     "device_minor": int(minor, 16), "inode": int(fields[4]),
                     "path": re.sub(r"\\([0-7]{3})", lambda value: chr(int(value[1], 8)), fields[5])})
    return rows


def regular_file(path):
    """Open the claimed path without accepting a final-component symlink."""
    descriptor = os.open(path, os.O_RDONLY | os.O_CLOEXEC | os.O_NOFOLLOW)
    stream = os.fdopen(descriptor, "rb")
    if not stat.S_ISREG(os.fstat(stream.fileno()).st_mode):
        stream.close()
        raise ValueError("Not a regular file: " + str(path))
    return stream


def file_identity(info):
    return (info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns, info.st_ctime_ns)


def authenticate_image(pe, pdb, rows, expected=None):
    """Bind image bytes to mappings of that same open regular file, not its pathname alone."""
    if expected is not None:
        for key in ("peSha256", "pdbSha256"):
            value = expected[key]
            if not isinstance(value, str) or re.fullmatch(r"[0-9a-fA-F]{64}", value) is None:
                raise ValueError("Expected " + key + " must be a string of exactly 64 hexadecimal characters.")
    pe, pdb = Path(pe), Path(pdb)
    if not pe.is_absolute() or not pdb.is_absolute():
        raise ValueError("Module paths must be absolute.")
    with regular_file(pe) as stream:
        before = os.fstat(stream.fileno())
        matches = [row for row in rows if row["inode"] == before.st_ino
                   and row["device_major"] == os.major(before.st_dev)
                   and row["device_minor"] == os.minor(before.st_dev)
                   and not row["path"].endswith(" (deleted)")]
        if not matches:
            raise ValueError("Required image has no actual device/inode mapping: " + str(pe))
        pe_hash = digest(stream)
        if file_identity(before) != file_identity(os.fstat(stream.fileno())):
            raise ValueError("Mapped PE changed during observation: " + str(pe))
    with regular_file(pdb) as stream:
        pdb_before = os.fstat(stream.fileno())
        pdb_hash = digest(stream)
        if file_identity(pdb_before) != file_identity(os.fstat(stream.fileno())):
            raise ValueError("PDB changed during observation: " + str(pdb))
    if expected is not None:
        if pe_hash != expected["peSha256"].lower():
            raise ValueError("Mapped PE hash mismatch: " + str(pe))
        if pdb_hash != expected["pdbSha256"].lower():
            raise ValueError("Mapped PDB hash mismatch: " + str(pdb))
    return {"path": str(pe), "pdb": str(pdb), "dll_sha256": pe_hash,
            "pdb_sha256": pdb_hash, "device_major": os.major(before.st_dev),
            "device_minor": os.minor(before.st_dev), "inode": before.st_ino,
            "mapping_rows": matches, "loaded_mvid": expected["loadedMvid"] if expected else None}


def observe(request, parent_pid, required=REQUIRED):
    """Read-only observation; a failed identity or missing image always rejects the receipt."""
    result = {"schema": "bounded-worker-os-maps-v1", "passed": False,
              "observed_at_utc": datetime.now(timezone.utc).isoformat(),
              "observer_pid": os.getpid(), "observer_start_ticks": start_ticks(os.getpid()),
              "observer_script": str(Path(__file__).resolve()),
              "observer_script_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              "pid": parent_pid, "start_ticks": None, "request": request,
              "images": [], "errors": []}
    try:
        before = start_ticks(parent_pid)
        result["start_ticks"] = before
        if os.getppid() != parent_pid or request["pid"] != parent_pid or request["start_ticks"] != before:
            raise ValueError("Worker PID/start identity does not match the observer's living parent.")
        proc = Path("/proc") / str(parent_pid)
        cmdline = (proc / "cmdline").read_bytes()
        result["argv"] = [part.decode("utf-8", errors="backslashreplace") for part in cmdline.rstrip(b"\0").split(b"\0")]
        result["cmdline_base64"] = base64.b64encode(cmdline).decode("ascii")
        result["exe"] = os.readlink(proc / "exe")
        with (proc / "exe").open("rb") as stream:
            result["exe_sha256"] = digest(stream)
        result["raw_maps"] = (proc / "maps").read_text()
        rows = mapping_rows(result["raw_maps"])
        modules = request["modules"]
        if len(modules) != len(required) or {item["name"] for item in modules} != required:
            raise ValueError("Expected the exact required module inventory.")
        for module in modules:
            try:
                if Path(module["pe"]).stem != module["name"]:
                    raise ValueError("Module name disagrees with its image path: " + module["name"])
                image = authenticate_image(module["pe"], module["pdb"], rows, module)
                result["images"].append({"pid": parent_pid, "start_ticks": before, **image})
            except (OSError, ValueError, KeyError, TypeError) as error:
                result["errors"].append(str(error))
        recorded = {(row["device_major"], row["device_minor"], row["inode"]) for row in result["images"]}
        for row in rows:
            identity = (row["device_major"], row["device_minor"], row["inode"])
            deleted = row["path"].endswith(" (deleted)")
            path = Path(row["path"].removesuffix(" (deleted)") if deleted else row["path"])
            if not path.name.startswith("Hexalith.") or path.suffix != ".dll":
                continue
            if deleted:
                result.setdefault("deleted_sdk_mapping_rows", []).append(row)
                result["errors"].append("Deleted first-party image mapping cannot be authenticated: " + row["path"])
                continue
            if identity in recorded:
                continue
            recorded.add(identity)
            try:
                image = authenticate_image(path, path.with_suffix(".pdb"), rows)
                result["images"].append({"pid": parent_pid, "start_ticks": before, **image})
            except (OSError, ValueError) as error:
                result["errors"].append(str(error))
        after = start_ticks(parent_pid)
        result["start_ticks_after"] = after
        if before != after or os.getppid() != parent_pid:
            result["errors"].append("Worker PID/start identity changed during observation.")
        result["passed"] = not result["errors"]
    except (OSError, ValueError, KeyError, IndexError, TypeError) as error:
        result["errors"].append(str(error))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if not args.output.is_absolute():
        parser.error("--output must be absolute")
    parent_pid = os.getppid()
    try:
        request = json.load(sys.stdin)
    except (ValueError, OSError) as error:
        request = {"invalid_request": str(error)}
    result = observe(request, parent_pid)
    args.output.mkdir(parents=True, exist_ok=True)
    receipt = args.output / (str(parent_pid) + "-" + str(result["start_ticks"]) + ".json")
    try:
        with receipt.open("x") as stream:
            json.dump(result, stream, indent=2)
            stream.write("\n")
    except FileExistsError:
        result["passed"] = False
        result["errors"].append("An earlier receipt already exists; it was preserved.")
        receipt = args.output / (str(parent_pid) + "-" + str(result["start_ticks"]) + "-rejected-" + str(os.getpid()) + ".json")
        with receipt.open("x") as stream:
            json.dump(result, stream, indent=2)
            stream.write("\n")
    print(json.dumps({"passed": result["passed"], "receipt": str(receipt), "errors": result["errors"]}))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
