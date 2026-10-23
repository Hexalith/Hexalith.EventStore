"""Versioned, immutable P1R evidence storage and bounded Git summary export.

Local captures are diagnostic proof, never enduring acceptance. Readers verify
all bytes and the enclosing identity before returning text or parsing JSON.
"""
from __future__ import annotations

import codecs
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import uuid

from p1r_qualification import InvalidPacket, canonical, digest, require

SCHEMA = "hexalith.p1r.artifact.v1"
OUTPUT_SCHEMA = "hexalith.p1r.command-output.v1"
JSON_SCHEMA = "hexalith.p1r.json-reference.v1"
SUMMARY_SCHEMA = "hexalith.p1r.attempt-summary.v1"
PRODUCER = "hexalith.p1r.evidence-storage.v1"
HOLD_SCHEMA = "hexalith.p1r.artifact-hold.v1"
EXCERPT_BYTES = 2048
ATTEMPT_BYTES = 10 * 1024 * 1024
READ_BYTES = 256 * 1024 * 1024
MANIFEST_BYTES = 1024 * 1024
CHUNK = 64 * 1024
INDEX_ENTRIES = 65536
INDEX_DEPTH = 128
INDEX_BYTES = READ_BYTES
EXPORT_BYTES = READ_BYTES
OUTPUT_MARKERS = {"output_schema", "output_ref", "output_excerpt"}
CONTEXT_KEYS = {"attempt", "repository", "source", "candidate", "inputs"}
REF_KEYS = {"schema", "producer", "representation", "bytes", "sha256", "decoded_sha256", "context", "identity",
            "locator", "retention", "retained_until", "disposition", "hold"}
COMMAND_FIELDS = ("id", "argv", "cwd", "started_utc", "finished_utc", "exit_code")


def utc():
    return dt.datetime.now(dt.timezone.utc)


def context(attempt, repository, source=None, candidate=None, inputs=None):
    """Create the closed identity used by producers and enclosing readers."""
    value = {"attempt": attempt, "repository": str(Path(repository).absolute()), "source": source,
             "candidate": candidate, "inputs": inputs}
    validate_context(value)
    return value


def validate_context(value):
    require(isinstance(value, dict) and set(value) == CONTEXT_KEYS, "artifact context members differ")
    require(isinstance(value["attempt"], str) and bool(re.fullmatch(r"[0-9a-f]{32}", value["attempt"])),
            "artifact attempt missing")
    require(isinstance(value["repository"], str) and Path(value["repository"]).is_absolute(), "artifact repository missing")
    for name in ("source", "candidate", "inputs"):
        require(value[name] is None or isinstance(value[name], str) and re.fullmatch(r"[0-9a-f]{64}", value[name]),
                "artifact source or candidate identity invalid")


def excerpt(data):
    """Bound the replacement-decoded representation on a UTF-8 character boundary."""
    return data[:EXCERPT_BYTES + 3].decode("utf-8", "replace").encode()[:EXCERPT_BYTES].decode("utf-8", "ignore")


def _open_directory(path, create=False):
    """Walk every component through anchored descriptors, refusing parent symlinks."""
    path = Path(path).absolute()
    descriptor = os.open(path.anchor, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        for part in path.parts[1:]:
            if create:
                try:
                    os.mkdir(part, mode=0o700, dir_fd=descriptor)
                except FileExistsError:
                    pass
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=descriptor)
            os.close(descriptor)
            descriptor = child
        return descriptor
    except OSError as error:
        os.close(descriptor)
        raise InvalidPacket("artifact directory unavailable or substituted") from error


def external_path(path, checkout):
    """Admit an external destination without creating any of its components."""
    path, checkout = Path(path).absolute(), Path(checkout).resolve()
    require(path == path.resolve() and path != Path(path.anchor), "artifact destination substituted")
    require(not path.is_relative_to(checkout), "artifact destination is inside checkout")
    # An umbrella checkout is also inadmissible, even when this repository is a submodule.
    for parent in checkout.parents:
        if (parent / ".git").exists():
            require(not path.is_relative_to(parent), "artifact destination is inside enclosing checkout")
    return path


def safe_directory(path, checkout, create=False):
    """Reject checkout writes and symlink substitutions before creating anything."""
    path = external_path(path, checkout)
    descriptor = _open_directory(path, create)
    os.close(descriptor)
    require(path.is_dir() and path == path.resolve(), "artifact root unavailable or substituted")
    return path


def private_directory(path, repair=False, controller_uid=None):
    """Keep shared host/container objects behind directories owned by the controller."""
    descriptor = _open_directory(path)
    try:
        info = os.fstat(descriptor)
        owners = {os.geteuid()}
        if os.geteuid() == 0 and controller_uid is not None:
            # A private container inherits the admitted repository's host owner.
            owners.add(controller_uid)
        require(info.st_uid in owners, "artifact directory has an unsafe owner")
        if repair:
            os.fchmod(descriptor, 0o700)
        require(os.fstat(descriptor).st_mode & 0o077 == 0, "artifact directory is public")
    finally:
        os.close(descriptor)


def decoded_digest(data):
    decoder, result = codecs.getincrementaldecoder("utf-8")("replace"), hashlib.sha256()
    for start in range(0, len(data), CHUNK):
        result.update(decoder.decode(data[start:start + CHUNK]).encode())
    result.update(decoder.decode(b"", final=True).encode())
    return result.hexdigest()


def default_root():
    return Path(os.environ.get("HEXALITH_P1R_EVIDENCE_ROOT") or Path.home() / ".local/share/hexalith/p1r-evidence").absolute()


def bounded_file(path, size=None, limit=READ_BYTES):
    """Read a regular immutable object with bounded I/O and physical identity checks."""
    path = Path(path).absolute()
    require(path == path.resolve(), "artifact path substituted")
    try:
        descriptor = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
        with os.fdopen(descriptor, "rb") as handle:
            before = os.fstat(handle.fileno())
            require(stat.S_ISREG(before.st_mode) and before.st_size <= limit
                    and (size is None or before.st_size == size), "artifact size or type differs")
            chunks, total = [], 0
            while True:
                chunk = handle.read(min(CHUNK, limit - total + 1))
                if not chunk:
                    break
                total += len(chunk)
                require(total <= limit, "artifact exceeds reader budget")
                chunks.append(chunk)
            after = os.fstat(handle.fileno())
            current = path.stat(follow_symlinks=False)
            require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns, before.st_ctime_ns)
                    == (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns, after.st_ctime_ns)
                    and (current.st_dev, current.st_ino) == (before.st_dev, before.st_ino)
                    and path == path.resolve() and (total == before.st_size or str(path).startswith("/proc/") and before.st_size == 0), "artifact changed while reading")
            return b"".join(chunks)
    except OSError as error:
        raise InvalidPacket("artifact unavailable or substituted") from error


class Store:
    """Content-addressed bytes with immutable per-identity metadata manifests."""

    def __init__(self, root, enclosing, retention="diagnostic", hold=False):
        validate_context(enclosing)
        require(retention == "diagnostic", "local storage cannot confer durable acceptance")
        require(type(hold) is bool, "artifact hold must be boolean")
        self.context = dict(enclosing)
        self.controller_uid = Path(enclosing["repository"]).stat().st_uid
        self.root = safe_directory(root, enclosing["repository"], create=True)
        self.retention, self.hold = retention, hold
        self.retained_until = (utc() + dt.timedelta(days=30)).isoformat()
        private_directory(self.root, repair=True, controller_uid=self.controller_uid)
        for name in ("objects", "manifests"):
            path = self.root / name
            require(path == path.resolve(), "artifact destination substituted")
            descriptor = _open_directory(path, create=True)
            os.close(descriptor)
            private_directory(path, repair=True, controller_uid=self.controller_uid)

    def _put(self, directory, data):
        name = digest(data)
        root = safe_directory(self.root, self.context["repository"])
        target = root / directory / name
        private_directory(root, controller_uid=self.controller_uid)
        private_directory(target.parent, controller_uid=self.controller_uid)
        parent = _open_directory(target.parent)
        temporary = "." + name + "." + uuid.uuid4().hex + ".part"
        try:
            expected = os.fstat(parent)
            require(target.parent == target.parent.resolve()
                    and (expected.st_dev, expected.st_ino) == (target.parent.stat().st_dev, target.parent.stat().st_ino),
                    "artifact directory substituted")
            def read_published():
                # A peer dropping its temporary hard link changes ctime without changing
                # the published bytes. Retry that race and still reject a different payload.
                changed = None
                for _ in range(3):
                    try:
                        return bounded_file(target, len(data), max(READ_BYTES, MANIFEST_BYTES))
                    except InvalidPacket as error:
                        if str(error) != "artifact changed while reading":
                            raise
                        changed = error
                raise changed

            if target.exists() or target.is_symlink():
                require(read_published() == data, "immutable artifact collision")
                return str(target)
            fd = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=parent)
            with os.fdopen(fd, "wb") as handle:
                handle.write(data)
                handle.flush()
                os.fchmod(handle.fileno(), 0o444)
                os.fsync(handle.fileno())
            try:
                # link is exclusive and publishes the already-complete inode atomically.
                os.link(temporary, name, src_dir_fd=parent, dst_dir_fd=parent, follow_symlinks=False)
            except FileExistsError:
                require(read_published() == data, "immutable artifact collision")
            os.fsync(parent)
            require(target.parent == target.parent.resolve()
                    and (target.parent.stat().st_dev, target.parent.stat().st_ino) == (expected.st_dev, expected.st_ino),
                    "artifact directory substituted during write")
        finally:
            try:
                os.unlink(temporary, dir_fd=parent)
            except FileNotFoundError:
                pass
            os.close(parent)
        return str(target)

    def _reference(self, data, identity, representation):
        require(isinstance(data, bytes) and len(data) <= READ_BYTES, "artifact exceeds capture budget")
        require(representation in ("raw-bytes", "canonical-json"), "unsupported artifact representation")
        ref = {"schema": SCHEMA, "producer": PRODUCER, "representation": representation, "bytes": len(data),
               "sha256": digest(data), "decoded_sha256": decoded_digest(data),
               "context": dict(self.context), "identity": identity, "locator": str(self.root / "objects" / digest(data)),
               "retention": self.retention, "retained_until": self.retained_until, "disposition": "diagnostic-only",
               "hold": self.hold}
        encoded = canonical(ref)
        require(len(encoded) <= MANIFEST_BYTES, "artifact manifest exceeds capture budget")
        return {"artifact": ref, "manifest": str(self.root / "manifests" / digest(encoded)),
                "manifest_sha256": digest(encoded)}, encoded

    def retain(self, data, identity, representation="raw-bytes"):
        reference, encoded = self._reference(data, identity, representation)
        self._put("objects", data)
        self._put("manifests", encoded)
        return reference

    def command(self, row, data):
        identity = {name: row[name] for name in COMMAND_FIELDS}
        row.update(output_schema=OUTPUT_SCHEMA, output_ref=self.retain(data, identity), output_excerpt=excerpt(data),
                   output_sha256=decoded_digest(data))
        return row

    def inventory(self, value):
        return {"schema": JSON_SCHEMA, "reference": self.retain(canonical(value), {"kind": "inventory"}, "canonical-json")}

    def place_hold(self, reference):
        """Preserve already captured bytes without changing their sealed reference."""
        require(Path(reference.get("manifest", "")).parent.parent == self.root, "hold artifact belongs to another store")
        _resolve(reference, self.context, reference.get("artifact", {}).get("identity"), _allow_expired=True)
        path = self.root / "holds"
        descriptor = _open_directory(path, create=True)
        os.close(descriptor)
        private_directory(path, repair=True, controller_uid=self.controller_uid)
        return self._put("holds", canonical(_hold_record(reference)))


def _hold_record(reference):
    return {"schema": HOLD_SCHEMA, "producer": PRODUCER, "context": reference["artifact"]["context"],
        "manifest_sha256": reference["manifest_sha256"], "artifact_sha256": reference["artifact"]["sha256"],
        "hold": True, "disposition": "preserve-required-bytes"}


def _held(reference, root, controller_uid, budget):
    """Only an owner-written, immutable record for these exact bytes extends retention."""
    encoded = canonical(_hold_record(reference))
    directory = root / "holds"
    if not directory.exists() and not directory.is_symlink():
        return False
    private_directory(directory, controller_uid=controller_uid)
    path = directory / digest(encoded)
    if not path.exists() and not path.is_symlink():
        return False
    try:
        info = path.stat(follow_symlinks=False)
    except OSError as error:
        raise InvalidPacket("artifact hold unavailable or substituted") from error
    owners = {os.geteuid()} | ({controller_uid} if os.geteuid() == 0 else set())
    require(info.st_uid in owners and info.st_mode & 0o222 == 0, "artifact hold is not owner-written immutable proof")
    read = bounded_file if budget is None else budget.read
    require(read(path, len(encoded), MANIFEST_BYTES) == encoded, "artifact hold record differs")
    return True


def _resolve(reference, enclosing, identity, *, now=None, limit=READ_BYTES, _budget=None, _allow_expired=False):
    """Verify the manifest, complete bytes, context and retention before returning bytes."""
    require(isinstance(reference, dict) and set(reference) == {"artifact", "manifest", "manifest_sha256"},
            "artifact reference members differ")
    ref = reference["artifact"]
    require(isinstance(ref, dict) and set(ref) == REF_KEYS and ref["schema"] == SCHEMA and ref["producer"] == PRODUCER,
            "unsupported artifact schema or producer")
    validate_context(ref["context"])
    require(isinstance(enclosing, dict) and set(enclosing) == CONTEXT_KEYS and ref["context"] == enclosing,
            "artifact enclosing identity differs")
    require(ref["identity"] == identity and ref["representation"] in ("raw-bytes", "canonical-json"), "artifact identity differs")
    require(type(ref["bytes"]) is int and 0 <= ref["bytes"] <= limit and type(ref["hold"]) is bool,
            "artifact size or hold invalid")
    require(ref["retention"] == "diagnostic" and ref["disposition"] == "diagnostic-only", "unsupported artifact retention")
    try:
        until = dt.datetime.fromisoformat(ref["retained_until"])
    except (ValueError, TypeError) as error:
        raise InvalidPacket("invalid artifact retention date") from error
    require(until.tzinfo is not None, "invalid artifact retention date")
    manifest, target = Path(reference["manifest"]), Path(ref["locator"])
    require(manifest.is_absolute() and target.is_absolute() and manifest.parent.name == "manifests"
            and target.parent.name == "objects" and manifest.parent.parent == target.parent.parent,
            "artifact locator substituted")
    safe_directory(target.parent.parent, ref["context"]["repository"])
    for directory in (target.parent.parent, target.parent, manifest.parent):
        private_directory(directory, controller_uid=Path(enclosing["repository"]).stat().st_uid)
    encoded = canonical(ref)
    require(len(encoded) <= MANIFEST_BYTES, "artifact manifest exceeds reader budget")
    read = bounded_file if _budget is None else _budget.read
    require(reference["manifest_sha256"] == digest(encoded) == manifest.name
            and read(manifest, len(encoded), MANIFEST_BYTES) == encoded, "artifact manifest differs")
    require(_allow_expired or ref["hold"] or (now or utc()) <= until
            or _held(reference, target.parent.parent, Path(enclosing["repository"]).stat().st_uid, _budget),
            "artifact expired without hold")
    data = read(target, ref["bytes"], limit)
    raw_hash, decoded_hash = (digest(data), decoded_digest(data)) if _budget is None else _budget.hashes(data)
    require(raw_hash == ref["sha256"] == target.name
            and decoded_hash == ref["decoded_sha256"], "artifact hash differs")
    return data


def resolve(reference, enclosing, identity, *, now=None, limit=READ_BYTES, _budget=None):
    """Return complete verified bytes only while retained or covered by an owner hold."""
    return _resolve(reference, enclosing, identity, now=now, limit=limit, _budget=_budget)


def command_text(row, enclosing=None, *, _budget=None):
    """Read external output or apply the original inline output/hash rule."""
    if OUTPUT_MARKERS.intersection(row):
        require(row.get("output_schema") == OUTPUT_SCHEMA and "output" not in row and "raw_output_base64" not in row,
                "unsupported or mixed command output schema")
        require(isinstance(row.get("output_excerpt"), str) and len(row["output_excerpt"].encode()) <= EXCERPT_BYTES,
                "command excerpt exceeds budget")
        require(enclosing is not None, "external command lacks enclosing identity")
        require(OUTPUT_MARKERS <= row.keys() and all(name in row for name in COMMAND_FIELDS), "incomplete command output reference")
        data = resolve(row["output_ref"], enclosing, {name: row[name] for name in COMMAND_FIELDS}, _budget=_budget)
        require(row.get("output_sha256") == (decoded_digest(data) if _budget is None else _budget.hashes(data)[1])
                and row["output_excerpt"] == excerpt(data), "command representation differs")
        return data.decode("utf-8", "replace") if _budget is None else _budget.text(data, "replace")
    require("output_schema" not in row and "output_ref" not in row, "unknown command output schema")
    value = row.get("output")
    require(value is None or isinstance(value, str) and digest(value.encode()) == row.get("output_sha256"),
            "legacy output hash differs")
    return value


def preserve_file(store, path):
    path = Path(path).absolute()
    data = bounded_file(path)
    return {"path": str(path), "sha256": digest(data),
            "content_ref": store.retain(data, {"kind": "preserved-file", "path": str(path)})}


def preserved_bytes(record, enclosing, *, _budget=None):
    """Resolve a retained file without permitting mixed legacy/external forms."""
    require(isinstance(record, dict), "invalid preservation reference")
    if "content_ref" in record:
        require(set(record) == {"path", "sha256", "content_ref"}, "mixed preservation reference")
        data = resolve(record["content_ref"], enclosing, {"kind": "preserved-file", "path": record["path"]}, _budget=_budget)
    else:
        require(set(record) == {"path", "sha256", "content"} and isinstance(record["content"], str),
                "invalid legacy preservation reference")
        data = record["content"].encode()
    require(digest(data) == record["sha256"], "preservation content hash differs")
    return data


class _Materialization:
    """One verified-byte/text cache with aggregate limits for one reader operation."""

    def __init__(self, io_limit, decoded_limit):
        self.remaining_io, self.remaining_decoded = io_limit, decoded_limit
        self.files, self.texts, self.parsed, self.digests = {}, {}, {}, {}

    def read(self, path, size, limit):
        path = Path(path)
        try:
            info = path.stat(follow_symlinks=False)
        except OSError as error:
            raise InvalidPacket("artifact unavailable or substituted") from error
        physical = (info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns, info.st_ctime_ns)
        require(path == path.resolve() and stat.S_ISREG(info.st_mode), "artifact path substituted")
        key = str(path)
        if key in self.files:
            data, prior = self.files[key]
            require(prior == physical and len(data) == size, "cached artifact changed")
            return data
        require(size <= self.remaining_io, "aggregate materialization I/O budget exceeded")
        data = bounded_file(path, size, limit)
        self.remaining_io -= len(data)
        self.files[key] = data, physical
        return data

    def hashes(self, data):
        if id(data) not in self.digests:
            self.digests[id(data)] = digest(data), decoded_digest(data)
        return self.digests[id(data)]

    def text(self, data, errors):
        key = self.hashes(data)[0], errors
        if key not in self.texts:
            decoder, chunks, used = codecs.getincrementaldecoder("utf-8")(errors), [], 0
            for start in range(0, len(data), CHUNK):
                part = decoder.decode(data[start:start + CHUNK])
                used += len(part.encode())
                require(used <= self.remaining_decoded, "aggregate materialization decoded budget exceeded")
                chunks.append(part)
            part = decoder.decode(b"", final=True)
            used += len(part.encode())
            require(used <= self.remaining_decoded, "aggregate materialization decoded budget exceeded")
            chunks.append(part)
            self.remaining_decoded -= used
            self.texts[key] = "".join(chunks)
        return self.texts[key]

    def parse(self, data):
        key = self.hashes(data)[0]
        if key not in self.parsed:
            def pairs(items):
                value = {}
                for name, item in items:
                    require(name not in value, "duplicate JSON member")
                    value[name] = item
                return value
            def constant(_):
                raise InvalidPacket("nonfinite JSON number")
            try:
                self.parsed[key] = json.loads(self.text(data, "strict"), object_pairs_hook=pairs,
                    parse_constant=constant)
            except (UnicodeError, json.JSONDecodeError) as error:
                raise InvalidPacket("invalid inventory JSON") from error
        return self.parsed[key]


def materialize(value, enclosing, *, io_limit=READ_BYTES, decoded_limit=READ_BYTES, _budget=None):
    """Verify once per physical payload and bound aggregate I/O and decoded bytes."""
    budget = _budget if _budget is not None else _Materialization(io_limit, decoded_limit)
    completed, active = {}, set()
    remaining_nodes = 1_000_000
    def visit(item, depth=0):
        nonlocal remaining_nodes
        remaining_nodes -= 1
        require(depth <= 128 and remaining_nodes >= 0, "materialization traversal budget exceeded")
        if not isinstance(item, (list, dict)):
            return item
        identity = id(item)
        require(identity not in active, "cyclic materialization reference")
        if identity in completed:
            return completed[identity]
        active.add(identity)
        try:
            result = expand(item, depth)
            completed[identity] = result
            return result
        finally:
            active.remove(identity)
    def expand(item, depth):
        def child(value):
            return visit(value, depth + 1)
        if isinstance(item, list):
            return [child(value) for value in item]
        if isinstance(item.get("schema"), str) and item["schema"].startswith("hexalith.p1r.json-reference"):
            require(set(item) == {"schema", "reference"} and item["schema"] == JSON_SCHEMA, "unsupported inventory schema")
            data = resolve(item["reference"], enclosing, {"kind": "inventory"}, _budget=budget)
            require(item["reference"]["artifact"]["representation"] == "canonical-json", "inventory representation differs")
            return child(budget.parse(data))
        if "content_ref" in item:
            return {"path": item["path"], "sha256": item["sha256"],
                    "content": budget.text(preserved_bytes(item, enclosing, _budget=budget), "strict")}
        if "output_command" in item:
            require(set(item) == {"command", "output_command", "observation"}, "mixed recovery output form")
            return {"command": item["command"], "output": command_text(item["output_command"], enclosing, _budget=budget),
                    "observation": child(item["observation"])}
        if OUTPUT_MARKERS.intersection(item):
            output = command_text(item, enclosing, _budget=budget)
            return {**{key: child(value) for key, value in item.items() if key not in OUTPUT_MARKERS}, "output": output}
        result = {key: child(value) for key, value in item.items() if key != "command_stdout_command"}
        if "command_stdout_command" in item:
            require("command_stdout" not in item, "mixed isolation output form")
            result["command_stdout"] = command_text(item["command_stdout_command"], enclosing, _budget=budget)
        return result
    return visit(value)


def retain_inventories(value, store):
    """Deduplicate full ordered state inventories, including repeated recovery observations."""
    if isinstance(value, list):
        if value and all(isinstance(row, dict) and set(row) == {"key", "tenant", "kind", "sha256", "sequence", "floor"} for row in value):
            return store.inventory(value)
        return [retain_inventories(item, store) for item in value]
    if isinstance(value, dict):
        return {key: retain_inventories(item, store) for key, item in value.items()}
    return value


def _write_summary(destination, encoded):
    """Create a fresh summary using exclusive operations anchored to directory FDs."""
    destination = Path(destination).absolute()
    parent = _open_directory(destination.parent, create=True)
    directory = None
    try:
        require(destination == destination.resolve() and not destination.exists(), "summary destination substituted")
        os.mkdir(destination.name, mode=0o700, dir_fd=parent)
        directory = os.open(destination.name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent)
        info, selected = os.fstat(directory), destination.stat(follow_symlinks=False)
        require(destination == destination.resolve() and (info.st_dev, info.st_ino) == (selected.st_dev, selected.st_ino),
                "summary destination substituted")
        fd = os.open("summary.json", os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=directory)
        with os.fdopen(fd, "wb") as handle:
            handle.write(encoded)
            handle.flush()
            os.fsync(handle.fileno())
        os.fsync(directory)
        require(destination == destination.resolve()
                and (destination.stat().st_dev, destination.stat().st_ino) == (info.st_dev, info.st_ino),
                "summary destination changed during write")
    except OSError as error:
        raise InvalidPacket("summary destination unavailable or substituted") from error
    finally:
        if directory is not None:
            os.close(directory)
        os.close(parent)


def validate_summary_destination(packet, destination, *, isolation=None):
    """Preflight a fresh summary destination without creating or reserving it."""
    destination = Path(destination).absolute()
    require(destination == destination.resolve() and not destination.exists() and not destination.is_symlink(),
            "summary destination exists or is substituted")
    for root in (packet, isolation):
        if root is not None:
            root = Path(root).absolute()
            require(not destination.is_relative_to(root) and not root.is_relative_to(destination),
                    "summary destination overlaps attempt packet")
    existing = destination.parent
    while not existing.exists() and not existing.is_symlink():
        existing = existing.parent
    descriptor = _open_directory(existing)
    os.close(descriptor)
    return destination


def _attempt_files(root, budget, depth=0):
    """Enumerate incrementally: directory width, depth and the whole walk are bounded."""
    require(depth <= INDEX_DEPTH, "attempt file enumeration exceeds depth budget")
    try:
        with os.scandir(root) as entries:
            for entry in entries:
                budget[0] += 1
                require(budget[0] <= INDEX_ENTRIES, "attempt file enumeration exceeds entry budget")
                if entry.is_dir(follow_symlinks=False):
                    yield from _attempt_files(Path(entry.path), budget, depth + 1)
                else:
                    yield Path(entry.path)
    except OSError as failure:
        raise InvalidPacket("attempt file enumeration unavailable") from failure


def _regenerable_cache(name):
    """Consumer NuGet caches and build outputs are restore/build products of the packet."""
    parts = name.split("/")
    return parts[0] == "consumers" and (len(parts) >= 4 and parts[2] == "packages"
                                        or len(parts) >= 5 and parts[3] in ("bin", "obj"))


def export_attempt(packet, destination, store, *, isolation=None, exit_code=None, error=None):
    """Export one bounded outcome, including failed initialization and outer cleanup."""
    packet, destination = Path(packet).absolute(), Path(destination).absolute()
    require(packet == packet.resolve(), "attempt packet substituted")
    validate_summary_destination(packet, destination, isolation=isolation)
    files, total, export_bytes, command_totals, documents, proofs = [], 0, 0, None, {}, {}
    regenerable = {"files": 0, "bytes": 0}
    receipts = {"total": 0, "passed": 0, "failed": 0, "assertions": {"attempted": 0, "passed": 0, "failed": 0}}
    cleanup = {"attempts": 0, "errors": 0, "remaining": 0}
    roots = [("", packet)] + ([("isolation/", Path(isolation).absolute())] if isolation is not None else [])
    enumeration = [0]
    index_bytes = len(canonical({"schema": "hexalith.p1r.complete-index.v1", "context": store.context, "files": []}))
    for prefix, root in roots:
        if not root.exists():
            continue
        require(root == root.resolve() and root.is_dir(), "attempt packet substituted")
        for path in _attempt_files(root, enumeration):
            name = prefix + path.relative_to(root).as_posix()
            if not prefix and _regenerable_cache(name):
                # Owner decision 2026-10-22: regenerable consumer caches are indexed by
                # path, size and SHA-256 only. No copy is stored, so they consume no
                # export byte budget; the cap remains a safety net for everything else.
                data = bounded_file(path)
                total += len(data)
                entry = {"path": name, "bytes": len(data), "sha256": digest(data), "disposition": "regenerable-hash-only"}
                index_bytes += len(canonical(entry)) + bool(files)
                require(index_bytes <= INDEX_BYTES, "attempt complete index exceeds capture budget")
                files.append(entry)
                regenerable["files"] += 1
                regenerable["bytes"] += len(data)
                continue
            require(path.stat(follow_symlinks=False).st_size <= EXPORT_BYTES - export_bytes, "attempt export exceeds byte budget")
            data = bounded_file(path, limit=min(READ_BYTES, EXPORT_BYTES - export_bytes))
            total += len(data)
            identity = {"kind": "packet-file", "path": name}
            reference, manifest = store._reference(data, identity, "raw-bytes")
            export_bytes += len(data) + len(manifest)
            require(export_bytes <= EXPORT_BYTES, "attempt export exceeds byte budget")
            entry = {"path": name, "bytes": len(data), "sha256": digest(data), "reference": reference}
            index_bytes += len(canonical(entry)) + bool(files)
            require(index_bytes <= INDEX_BYTES, "attempt complete index exceeds capture budget")
            store.retain(data, identity)
            files.append(entry)
            if not prefix and path.parent == root and path.name == "commands.json":
                rows = json.loads(data)
                require(isinstance(rows, list), "invalid command journal export")
                command_totals = {"total": len(rows), "succeeded": sum(row.get("exit_code") == 0 for row in rows),
                    "nonzero": sum(row.get("exit_code") != 0 for row in rows),
                    "raw_output_bytes": sum(row.get("output_ref", {}).get("artifact", {}).get("bytes", 0) for row in rows)}
            if path.parent == root and path.name in ("execution.json", "isolation.json", "private-execution.json"):
                value = json.loads(data)
                documents[path.name] = value
                if path.name in ("isolation.json", "private-execution.json"):
                    proofs[path.name] = reference
                for attempt in value.get("cleanup", []):
                    cleanup["attempts"] += 1
                    cleanup["errors"] += len(attempt.get("errors", []))
                    cleanup["remaining"] += len(attempt.get("remaining", []))
            if not prefix and path.parent.name == "receipts" and path.suffix == ".json":
                value = json.loads(data)
                if isinstance(value, dict) and "execution" in value:
                    receipts["total"] += 1
                    receipts["passed"] += value["execution"] == "passed"
                    receipts["failed"] += value["execution"] != "passed"
                    for key in receipts["assertions"]:
                        receipts["assertions"][key] += (value.get("assertions") or {}).get(key, 0)
                    for attempt in value.get("attempts", []):
                        cleanup["attempts"] += 1
                        cleanup["errors"] += len(attempt.get("errors", []))
                        cleanup["remaining"] += len(attempt.get("remaining", []))
    files.sort(key=lambda row: row["path"])
    index = canonical({"schema": "hexalith.p1r.complete-index.v1", "context": store.context, "files": files})
    require(len(index) <= INDEX_BYTES, "attempt complete index exceeds capture budget")
    _, manifest = store._reference(index, {"kind": "complete-index"}, "canonical-json")
    require(export_bytes + len(index) + len(manifest) <= EXPORT_BYTES, "attempt export exceeds byte budget")
    execution, outer = documents.get("execution.json", {}), documents.get("isolation.json", {})
    errors = int(error is not None) + sum(len(value.get("errors", [])) for value in documents.values())
    observed_exit = exit_code if exit_code is not None else outer.get("command_exit_code")
    failed = (observed_exit not in (None, 0) or errors > 0 or cleanup["errors"] > 0 or cleanup["remaining"] > 0
              or receipts["failed"] > 0 or receipts["assertions"]["failed"] > 0 or outer.get("passed") is False)
    finished = bool(outer.get("finished_utc") or execution.get("finished_utc"))
    summary = {"schema": SUMMARY_SCHEMA, "context": store.context, "files": len(files), "bytes": total,
               "regenerable_hash_only": regenerable,
               "complete_index": store.retain(index, {"kind": "complete-index"}, "canonical-json"),
               "disposition": "diagnostic-only", "p1r_usable": False, "errors_count": errors,
               "outcome": {"status": "failed" if failed else "completed" if finished else "incomplete",
                   "exit_code": observed_exit, "execution": {key: execution.get(key) for key in
                       ("invocation", "started_utc", "finished_utc", "p1r_usable")},
                   "isolation_passed": outer.get("passed"), "cleanup": cleanup, "receipts": receipts},
               "isolation_proof": proofs}
    if error is not None:
        summary["error_excerpt"] = excerpt(str(error).encode())
    if command_totals is not None:
        summary["commands"] = command_totals
    encoded = canonical(summary) + b"\n"
    require(len(encoded) <= ATTEMPT_BYTES, "aggregate attempt summary exceeds budget")
    _write_summary(destination, encoded)
    return summary


def source_identity(binding):
    """Bind source inventories while the separately sealed interpreter remains authoritative."""
    def source(value):
        if isinstance(value, dict):
            return {key: source(item) for key, item in value.items() if key != "python"}
        if isinstance(value, list):
            return [source(item) for item in value]
        return value
    return digest(canonical(source(binding)))


def receipt_context(receipt, binding=None, selection=None):
    """Derive witness identity from its enclosing receipt and admitted source selection."""
    fixture = receipt.get("fixture") or {}
    identifier = fixture.get("id", "")
    result = {"attempt": identifier.removeprefix("p1r-"), "inputs": receipt.get("inputs_sha256")}
    if binding is not None and "repository" in binding:
        result.update(repository=binding["repository"], source=digest(
            json.dumps(binding, indent=2, sort_keys=True, allow_nan=False).encode() + b"\n"))
    if selection is not None:
        result["candidate"] = digest(canonical(selection["candidate"]))
    return result
