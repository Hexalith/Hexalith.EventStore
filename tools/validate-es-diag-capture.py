#!/usr/bin/env python3
"""Verify a closed ES-DIAG capture archive; never grant technical or owner authority."""
from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import sys
import tarfile
import tempfile
import types
import unicodedata
import zlib

VERIFIER = "tools/dapr_broker_qualification.py"
RUNTIME_LOCK = "deploy/dapr/qualification/runtime-lock.json"
BASELINE_IDENTITIES = "_bmad-output/implementation-artifacts/evidence/story-3-21/verification-2026-10-20/packet/execution-identities.json"
VERIFIER_SHA256 = "7357c189c12ce284196b84ceba47a435f21003d898f65ef6a9b272030fd80afa"
MAX_VERIFIER_BYTES = 256 * 1024
MAX_MEMBERS = 4096
MAX_FILE_BYTES = 64 * 1024 * 1024
MAX_TOTAL_BYTES = 512 * 1024 * 1024
MAX_ARCHIVE_BYTES = 128 * 1024 * 1024
MAX_STREAM_BYTES = MAX_TOTAL_BYTES + MAX_MEMBERS * 2048 + 10240


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "JSON: duplicate object key")
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_bytes(), object_pairs_hook=unique)


def canonical_path(name):
    require(isinstance(name, str) and 0 < len(name) <= 512, "archive: invalid member name")
    require("\\" not in name and not any(unicodedata.category(c) == "Cc" for c in name),
            "archive: backslash or control character in member name")
    path = PurePosixPath(name)
    require(not path.is_absolute() and not re.match(r"^[A-Za-z]:", name) and name == path.as_posix()
            and all(part not in ("", ".", "..") for part in name.split("/")),
            "archive: member name is not canonical relative path")
    return name


def git_bytes(root, *arguments, limit):
    # File-backed subprocess output and a size-limited read avoid unbounded pipes.
    with tempfile.TemporaryFile() as output, tempfile.TemporaryFile() as errors:
        process = subprocess.run(["git", "--no-replace-objects", "-C", str(root), "cat-file", *arguments],
                                 stdout=output, stderr=errors, timeout=10, check=False)
        require(process.returncode == 0, "baseline: git cat-file failed")
        output.seek(0)
        value = output.read(limit + 1)
        require(len(value) <= limit, "baseline: git object exceeds byte limit")
        return value


def baseline_blob(root, baseline, name):
    subject = baseline + ":" + name
    require(git_bytes(root, "-t", subject, limit=32) == b"blob\n", "baseline: input is not a blob")
    size = int(git_bytes(root, "-s", subject, limit=32))
    require(0 < size <= MAX_VERIFIER_BYTES, "baseline: input exceeds byte limit")
    source = git_bytes(root, "blob", subject, limit=MAX_VERIFIER_BYTES)
    require(len(source) == size, "baseline: input size mismatch")
    return source


def baseline_verifier(root, baseline):
    require(re.fullmatch(r"[0-9a-f]{40}", baseline) is not None,
            "baseline: a full lowercase 40-character commit is required")
    require(git_bytes(root, "-t", baseline, limit=32) == b"commit\n", "baseline: object is not a commit")
    source = baseline_blob(root, baseline, VERIFIER)
    require(hashlib.sha256(source).hexdigest() == VERIFIER_SHA256, "baseline: verifier digest mismatch")
    module = types.ModuleType("es_diag_digest_bound_verifier")
    # Only the exact trusted baseline bytes execute. Neither archive code nor
    # mutable sibling modules participate, including under Python -I -S -B.
    exec(compile(source, VERIFIER, "exec"), module.__dict__)
    profile = json.loads(baseline_blob(root, baseline, RUNTIME_LOCK), object_pairs_hook=unique)
    identities = json.loads(baseline_blob(root, baseline, BASELINE_IDENTITIES), object_pairs_hook=unique)
    return module, profile, identities["controlPlane"]


def unpack(archive, directory):
    names = set()
    total = 0
    # A seekable, bounded decompression lets us inspect bytes tarfile may skip
    # after a malformed final header, including concatenated gzip members.
    with tempfile.TemporaryFile() as raw:
        decompressed = 0
        with gzip.GzipFile(fileobj=archive, mode="rb") as compressed:
            while chunk := compressed.read(1024 * 1024):
                decompressed += len(chunk)
                require(decompressed <= MAX_STREAM_BYTES, "archive: excessive decompressed bytes")
                raw.write(chunk)
        raw.seek(0)
        data_end = 0
        with tarfile.open(fileobj=raw, mode="r:") as packet:
            for member in packet:
                require(len(names) < MAX_MEMBERS, "archive: excessive member count")
                name = canonical_path(member.name)
                require(name not in names, "archive: duplicate member")
                require(member.type in (tarfile.REGTYPE, tarfile.AREGTYPE) and not member.sparse,
                        "archive: only regular files are permitted")
                require(0 <= member.size <= MAX_FILE_BYTES, "archive: excessive member size")
                total += member.size
                require(total <= MAX_TOTAL_BYTES, "archive: excessive total bytes")
                names.add(name)
                data_end = member.offset_data + member.size
                destination = directory / name
                destination.parent.mkdir(parents=True, exist_ok=True)
                with packet.extractfile(member) as source, destination.open("xb") as output:
                    remaining = member.size
                    while remaining:
                        chunk = source.read(min(remaining, 1024 * 1024))
                        require(chunk, "archive: truncated member")
                        output.write(chunk)
                        remaining -= len(chunk)
        aligned_end = ((data_end + 511) // 512) * 512
        padding = decompressed - aligned_end
        require(1024 <= padding <= 11264 and padding % 512 == 0,
                "archive: truncated tar termination or excessive trailing data")
        raw.seek(data_end)
        require(not any(raw.read(11776)), "archive: nonzero tar termination or trailing data")
    require("packet.json" in names, "archive: packet.json is missing")
    manifest = read_json(directory / "packet.json")
    require(isinstance(manifest, dict) and isinstance(manifest.get("artifacts"), dict),
            "archive: missing artifact inventory")
    inventory = {canonical_path(name) for name in manifest["artifacts"]}
    require("packet.json" not in inventory and names == {"packet.json", *inventory},
            "archive: inventory is not exactly packet.json plus artifacts")
    for name in sorted(inventory):
        if name.lower().endswith(".json"):
            read_json(directory / name)
    return manifest


def preservation_receipts(directory, commands):
    previous = -1
    enumerations = [(index, row) for index, row in enumerate(commands)
                    if row.get("argv") == ["docker", "ps", "-aq", "--no-trunc"]]
    for name in ("workspace-before.json", "workspace-after.json"):
        snapshot = read_json(directory / name)
        matched = None
        for position, (index, receipt) in enumerate(enumerations):
            ids = receipt.get("output", "").split()
            if index <= previous or receipt.get("exitCode") != 0 or len(ids) != len(set(ids)) or set(ids) != set(snapshot):
                continue
            end = enumerations[position + 1][0] if position + 1 < len(enumerations) else len(commands)
            if snapshot and not any(row.get("argv") == ["docker", "inspect", *ids]
                                    and row.get("exitCode") == 0
                                    and row.get("output") == json.dumps(snapshot, sort_keys=True)
                                    for row in commands[index + 1:end]):
                continue
            matched = index
            break
        require(matched is not None, "capture: " + name + " lacks complete successful Docker inventory receipts")
        previous = matched


def complete_capture(directory, packet, verifier, baseline_profile, baseline_control):
    profile = read_json(directory / "inputs" / RUNTIME_LOCK)
    for section in ("runtime", "images", "openbao"):
        require(profile[section] == baseline_profile[section],
                "capture: immutable candidate " + section + " differs from baseline")
    require(set(baseline_profile["inputs"]).issubset(profile["inputs"]),
            "capture: baseline source/component/subscription input coverage is required")
    identities = read_json(directory / "execution-identities.json")
    require(identities.get("preflightPassed") is True, "capture: successful preflight is required")
    require(set(identities.get("controlPlane", {})) == {"placement", "scheduler"},
            "capture: both control-plane identities are required")
    for name in ("placement", "scheduler"):
        require(all(identities["controlPlane"][name].get(key) == baseline_control[name][key]
                    for key in ("image", "binarySha256")),
                "capture: immutable control-plane identity differs from baseline: " + name)
    commands = read_json(directory / "commands.json")
    for receipt in commands:
        argv = receipt.get("argv", [])
        if argv and argv[0] == "HTTP" and "/publish/" in argv[-1]:
            status = receipt.get("httpStatus")
            require(argv[:2] == ["HTTP", "POST"] and type(status) is int and 100 <= status <= 599
                    and receipt.get("exitCode") == (0 if status < 400 else 1),
                    "capture: publication requires POST with consistent HTTP status/exit receipt")
    require(set(packet.get("brokers", {})) == set(verifier.BROKERS), "capture: exact broker matrix required")
    count = 0
    evidence_paths = set()
    message_ids = set()
    receipt_indices = set()
    for broker in verifier.BROKERS:
        metadata = [read_json(path) for path in (directory / broker / "metadata").glob("*.json")]
        roles = {row.get("id") for row in metadata}
        require({"qualification-publisher", "qualification-consumer"}.issubset(roles),
                broker + ": both loaded sidecar roles are required")
        for loaded in metadata:
            require(any(receipt.get("argv", [])[:2] == ["HTTP", "GET"]
                        and receipt["argv"][-1].endswith("/v1.0/metadata")
                        and receipt.get("httpStatus") == 200 and receipt.get("exitCode") == 0
                        and json.loads(receipt["output"], object_pairs_hook=unique) == loaded
                        for receipt in commands), "capture: loaded metadata requires successful HTTP GET receipt")
        outcomes = packet["brokers"][broker]
        require(set(outcomes) == set(verifier.SCENARIOS), broker + ": exactly 12 executed vectors are required")
        for scenario in verifier.SCENARIOS:
            outcome = outcomes[scenario]
            documents = []
            for field in ("observation", "applicationReadback", "brokerDiagnostics"):
                name = canonical_path(outcome[field])
                require(name in packet["artifacts"] and name not in evidence_paths,
                        "capture: vector evidence must be independent inventoried files")
                evidence_paths.add(name)
                documents.append(read_json(directory / name))
            observation, readback, diagnostic = documents
            require(not any(row.get("blockedBy") for row in (outcome, *documents)),
                    broker + "/" + scenario + ": blocked vector is not executed capture")
            require(isinstance(observation.get("messageId"), str) and observation["messageId"],
                    "capture: executed observation requires a message identity")
            require(observation["messageId"] not in message_ids, "capture: primary vector message identities must be globally distinct")
            message_ids.add(observation["messageId"])
            for document in (readback, diagnostic):
                index = document.get("commandReceipt")
                require(type(index) is int and 0 <= index < len(commands) and index not in receipt_indices,
                        "capture: vector readback/diagnostic receipts must be independent")
                receipt_indices.add(index)
            require(readback.get("kind") == "persisted-application-http-readback",
                    "capture: every vector requires persisted HTTP application readback")
            require(diagnostic.get("kind") == "broker-administration-diagnostic",
                    "capture: every vector requires separate broker diagnostics")
            count += 1
    require(count == 24, "capture: exactly 24 executed observations are required")
    result = verifier.validate_packet(directory, allow_failures=True)
    preservation_receipts(directory, commands)
    # The baseline binds response bodies and persisted contents. Also demand a
    # successful HTTP GET process for rejected vectors, which it does not require.
    for broker in verifier.BROKERS:
        for outcome in packet["brokers"][broker].values():
            readback = read_json(directory / outcome["applicationReadback"])
            receipt = commands[readback["commandReceipt"]]
            require(receipt["argv"][:2] == ["HTTP", "GET"] and receipt["exitCode"] == 0,
                    "capture: readback requires a successful HTTP GET receipt")
    return {**result, "failedScenarios": sorted(result["failedScenarios"]),
            "captureIntegrity": "PASS", "executedObservations": count,
            "acceptanceScope": "capture-evidence-only"}


def main():
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("packet", type=Path, help="regular packet.tar.gz evidence file")
    parser.add_argument("--baseline", required=True, help="full baseline commit containing the trusted verifier")
    arguments = parser.parse_args()
    try:
        verifier, profile, control = baseline_verifier(Path(__file__).resolve().parents[1], arguments.baseline)
        descriptor = os.open(arguments.packet, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
        with os.fdopen(descriptor, "rb") as archive:
            metadata = os.fstat(archive.fileno())
            require(stat.S_ISREG(metadata.st_mode), "archive: evidence must be a regular file")
            require(metadata.st_size <= MAX_ARCHIVE_BYTES, "archive: excessive compressed bytes")
            with tempfile.TemporaryDirectory(prefix="es-diag-capture-") as temporary:
                directory = Path(temporary)
                packet = unpack(archive, directory)
                result = complete_capture(directory, packet, verifier, profile, control)
        result.update(baselineCommit=arguments.baseline, verifierSha256=VERIFIER_SHA256)
        code = 0
    except (ValueError, KeyError, TypeError, IndexError, AttributeError, OSError,
            EOFError, RecursionError, zlib.error, tarfile.TarError, subprocess.SubprocessError) as error:
        reason = error.msg if isinstance(error, json.JSONDecodeError) else str(error)
        # Avoid ephemeral extraction paths in deterministic rejected output.
        if isinstance(error, (gzip.BadGzipFile, EOFError, zlib.error)):
            reason = "archive: malformed gzip stream"
        elif isinstance(error, OSError):
            reason = "archive: filesystem operation failed (errno " + str(error.errno) + ")"
        result = {"captureIntegrity": "FAIL", "error": reason,
                  "productionEligibility": "FAIL", "selectedBroker": None}
        code = 1
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return code


if __name__ == "__main__":
    sys.exit(main())
