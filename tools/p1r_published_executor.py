"""Invocation-owned published P1R consumers, measurements, backups and cleanup.

Every command and Boolean observation is retained, including failures. Package
downloads, restore graphs and loaded files are separate from the executor source
checkout and the selected package build provenance. No owner acceptance is made.
"""
from __future__ import annotations

import itertools
import base64
import datetime
import json
import os
import re
from pathlib import Path
import shutil
import signal
import socket
import struct
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

import p1r_check_witnesses as witnesses
import p1r_published_qualification as qualification
import p1r_evidence_storage as storage
import p1r_qualification as preparation
import p1r_qualification_runtime as runtime
from p1r_qualification import InvalidPacket, canonical, checks_counter, digest, regular, require, stamp, write_json as _write_json

ROOT = preparation.ROOT
CONSUMERS = ROOT / "tools/p1r-published-consumers"
MECHANISM = "p1r-executed-checks-v1"
CANDIDATE = "3.119.0"
VERSIONS = ("3.110.0", "3.70.1", CANDIDATE)
REDIS = "redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf"
POSTGRES = "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
DAPR_IMAGE = "daprio/dapr@sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59"
TAG_COMMIT = "f463442cca19e4199982a23a08bae4a490767d4a"
BUILDS_COMMIT = "2cf00028bbe563d80d4d12b5fb2054914f14fcb6"
TAG_BUILDS_COMMIT = "468fdbba04e2d9a27d251298875125b57fa6d836"
PROJECTS = ("Host", "Domain", "Probe")
STATUS_RETRYABILITY = (None, False, True)
REMINDER_STARTUP_LEAD_SECONDS = 120
REMINDER_MINIMUM_WINDOW_SECONDS = 5
DOCKER_FORMAT = ('{"Id":{{json .Id}},"Image":{{json .Image}},"Name":{{json .Name}},'
                 '"State":{"Running":{{json .State.Running}},"StartedAt":{{json .State.StartedAt}}},'
                 '"Config":{"Labels":{"hexalith.p1r.invocation":{{json (index .Config.Labels "hexalith.p1r.invocation")}}}}}')
RESERVED_PORTS = set()
PACKAGE_IDS = tuple("Hexalith.EventStore." + name for name in ("Client", "Contracts", "DomainService", "Server", "ServiceDefaults"))
REDIS_DIAGNOSTIC = ("local rows={} for _,key in ipairs(redis.call('KEYS','*')) do "
                    "if redis.call('TYPE',key).ok=='hash' then local data=redis.call('HGET',key,'data') "
                    "if data then table.insert(rows,{key,data}) end end end return cjson.encode(rows)")
PRECONDITION_QUERY = "SELECT coalesce(json_agg(json_build_array(key,value) ORDER BY key)::text,'[]') FROM state"


def write_json(path, value):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    _write_json(Path(path), value)


def safe_inspect_argv(*identities):
    """Ask Docker for only public preservation identities and this invocation's ownership label."""
    return ["docker", "inspect", "--format", DOCKER_FORMAT, *identities]


def docker_observations(data):
    """Decode the bounded safe-format observation returned once per container."""
    return [json.loads(line) for line in data.decode().splitlines() if line.strip()]


def postgres_value(value):
    """Decode PostgreSQL v1's base64 JSONB string to the original Dapr state bytes."""
    if isinstance(value, str):
        try:
            return base64.b64decode(value, validate=True)
        except (ValueError, base64.binascii.Error):
            pass
    return canonical(value)


def redis_inventory(container, invocation):
    """Read bounded fixture diagnostics only after independently verifying container ownership."""
    def command(*args):
        return subprocess.check_output(["docker", "exec", container, "redis-cli", "--raw", *args])
    observed = docker_observations(subprocess.check_output(safe_inspect_argv(container)))[0]
    require(observed["Id"] == container and observed["Config"]["Labels"].get("hexalith.p1r.invocation") == invocation,
            "unowned provider inventory")
    rows = []
    values = json.loads(command("EVAL", REDIS_DIAGNOSTIC, "0"))
    for key, raw_data in values:
        data = raw_data.encode()
        value = json.loads(data)
        tenant = "tenant-a" if "tenant-a" in key else "tenant-b" if "tenant-b" in key else "infrastructure"
        kind, sequence, floor = "bookkeeping", None, None
        if key.endswith(":metadata"):
            kind, sequence, floor = "metadata", value.get("currentSequence"), value.get("retainedFloor", 1)
        elif ":events:" in key:
            kind, sequence = "event", int(key.rsplit(":", 1)[1])
        elif key.endswith(":snapshot"):
            kind, sequence = "snapshot", value.get("sequenceNumber")
        rows.append({"key": key, "tenant": tenant, "kind": kind, "sha256": digest(data), "sequence": sequence, "floor": floor})
    rows.sort(key=lambda row: row["key"])
    runtime.validate_rows(rows)
    return rows


def postgres_inventory(container, invocation):
    """Read structural fixture state from an exactly owned PostgreSQL v1 container."""
    observed = docker_observations(subprocess.check_output(safe_inspect_argv(container)))[0]
    require(observed["Id"] == container and observed["Config"]["Labels"].get("hexalith.p1r.invocation") == invocation,
            "unowned provider inventory")
    query = "SELECT coalesce(json_agg(json_build_array(key,value) ORDER BY key)::text,'[]') FROM state"
    data = subprocess.check_output(["docker", "exec", container, "psql", "-U", "postgres", "-d", "eventstore",
                                    "-At", "-v", "ON_ERROR_STOP=1", "-c", query])
    rows = []
    for key, value in json.loads(data):
        raw_data = postgres_value(value)
        try:
            value = json.loads(raw_data)
        except (ValueError, UnicodeError):
            value = None
        tenant = "tenant-a" if "tenant-a" in key else "tenant-b" if "tenant-b" in key else "infrastructure"
        kind, sequence, floor = "bookkeeping", None, None
        if key.endswith(":metadata"):
            require(isinstance(value, dict), "invalid persisted metadata content")
            kind, sequence, floor = "metadata", value.get("currentSequence"), value.get("retainedFloor", 1)
        elif ":events:" in key:
            kind, sequence = "event", int(key.rsplit(":", 1)[1])
        elif key.endswith(":snapshot"):
            require(isinstance(value, dict), "invalid persisted snapshot content")
            kind, sequence = "snapshot", value.get("sequenceNumber")
        rows.append({"key": key, "tenant": tenant, "kind": kind, "sha256": digest(raw_data), "sequence": sequence, "floor": floor})
    rows.sort(key=lambda row: row["key"])
    runtime.validate_rows(rows)
    return rows


class Measurements:
    """Record executed checks without failing early or inventing totals."""

    def __init__(self):
        self.checks = []
        self.witnesses = []

    def check(self, identity, passed, witness=None):
        require(type(passed) is bool and not any(row["id"] == identity for row in self.checks),
                "invalid or duplicate executed check")
        witness = witness or witnesses.capture(identity)
        require(witness["kind"] == "probe" or witnesses.evaluate(witness["predicate"]) is passed, "check predicate contradicts observation")
        self.witnesses.append(witness)
        self.checks.append({"id": identity, "passed": passed})

    def import_probe(self, value, prefix, command=None):
        require(value.get("instrumentation") == MECHANISM, "probe instrumentation substituted")
        measured = value["measurement"]
        preparation.validate_counter(measured["assertions"], measured["checks"])
        for check in measured["checks"]:
            self.check(prefix + check["id"], check["passed"],
                       {"id": prefix + check["id"], "kind": "probe", "command": command, "probe_id": check["id"]})

    @property
    def counter(self):
        return checks_counter(self.checks)


class SeedFailure(InvalidPacket):
    """Carry actual seed observations through case and paired-branch failures."""

    def __init__(self, observations):
        super().__init__("seed preconditions invalid; dependent operations unattempted")
        self.observations = {"seed_preconditions": observations, "dependent_operations": "unattempted"}


def reserve_port():
    # Stay below Linux's ephemeral client range: releasing an ephemeral listener
    # before Docker publishes it otherwise races ordinary outbound connections.
    for _ in range(100):
        port = 10000 + uuid.uuid4().int % 20000
        if port in RESERVED_PORTS:
            continue
        try:
            with socket.socket() as connection:
                connection.bind(("127.0.0.1", port))
            RESERVED_PORTS.add(port)
            return port
        except OSError:
            continue
    raise InvalidPacket("invocation loopback port unavailable")


def validate_execution_inputs(inputs, source_root=None, source_builds_root=None):
    """Refuse a substituted approved tuple before any consumer or operational resource starts."""
    require(qualification.validate_inputs(inputs) == "owner-selected", "synthetic inputs cannot execute published operations")
    candidate, profile = inputs["candidate"], inputs["operational_profile"]
    if "tooling" in inputs:
        binding = preparation.source_binding(ROOT, inputs["tooling"], source_root, source_builds_root)
        preparation.validate_selected_source(inputs, binding)
    elif source_root is not None:
        require(source_builds_root is None, "source builds path requires selected tooling pins")
        binding = preparation.source_binding(ROOT, source_root)
        qualification.validate_candidate_source(binding, inputs)
    else:
        require(source_root is None and source_builds_root is None, "source paths require selected tooling pins")
    require(inputs["rollback"] is None and inputs["assertion_instrumentation"]["mechanism"] == MECHANISM
            and [row["version"] for row in inputs.get("comparisons", [])] == list(qualification.COMPARISON_VERSIONS)
            and profile["runtime"] == "dapr" and profile["runtime_version"] == "1.18.2"
            and profile["backend"] == "state.postgresql" and profile["backend_image"] == POSTGRES
            and inputs["selected_additions"] == list(qualification.ADDITIONS), "execution selections substituted")
    if candidate["version"] == CANDIDATE:
        require(candidate["tag"] == "v3.119.0" and candidate["tag_commit"] == TAG_COMMIT
                and candidate["builds"] == {"version": "4.30.1-20-g2cf0002", "commit": BUILDS_COMMIT},
                "execution selections substituted")
        return
    require(qualification.is_successor_selection(inputs) and candidate["tag_builds_commit"],
            "unrepaired or unbound successor selection")
    if "tooling" in inputs:
        return
    selected_root = ROOT if source_root is None else Path(source_root)
    if source_root is None and candidate["version"] in ("3.125.1", "3.126.0"):
        binding = preparation.source_binding(ROOT, selected_root)
        qualification.validate_candidate_source(binding, inputs)
    tag_commit = preparation.git(selected_root, "rev-parse", candidate["tag"] + "^{commit}").decode().strip()
    source_commit = preparation.git(selected_root, "rev-parse", "HEAD").decode().strip()
    tag_builds = preparation.git(selected_root, "ls-tree", candidate["tag"], "references/Hexalith.Builds").decode().split()
    require(tag_commit == source_commit == candidate["tag_commit"]
            and len(tag_builds) == 4 and tag_builds[:2] == ["160000", "commit"]
            and tag_builds[2] == candidate["tag_builds_commit"],
            "successor tag, exact source, or tag Builds identity substituted")


def validate_observation_inputs(planning, source_root=None, source_builds_root=None):
    """Apply observation admission before restore, daemon creation or executor initialization."""
    candidate = planning["candidate_proposed"]
    if "tooling" in planning:
        qualification.validate_selection(candidate, "candidate")
        binding = preparation.source_binding(ROOT, planning["tooling"], source_root, source_builds_root)
        preparation.validate_selected_source({"candidate": candidate, "tooling": planning["tooling"]}, binding)
        require([row["id"] for row in candidate["packages"]] == list(PACKAGE_IDS)
                and all(row["repository_commit"] == preparation.RUNTIME_COMMIT for row in candidate["packages"]),
                "successor observation lacks exact source-bound packages")
        return binding
    require(source_builds_root is None, "source builds path requires selected tooling pins")
    selected_root = ROOT if source_root is None else Path(source_root)
    if source_root is not None:
        binding = preparation.source_binding(ROOT, source_root)
        qualification.validate_candidate_source(binding, {"candidate": candidate})
    if candidate["version"] == CANDIDATE:
        require(candidate["tag_commit"] == TAG_COMMIT and candidate["builds"]["commit"] == BUILDS_COMMIT,
                "candidate selection substituted")
        expected_builds = TAG_BUILDS_COMMIT
    else:
        require(qualification.is_successor_selection({"candidate": candidate}), "unrepaired or unbound successor selection")
        qualification.validate_selection(candidate, "candidate")
        tag = preparation.git(selected_root, "rev-parse", candidate["tag"] + "^{commit}").decode().strip()
        head = preparation.git(selected_root, "rev-parse", "HEAD").decode().strip()
        require(tag == head == candidate["tag_commit"], "successor observation source or tag substituted")
        expected_builds = candidate.get("tag_builds_commit")
    tree = preparation.git(selected_root, "ls-tree", candidate["tag"], "references/Hexalith.Builds").decode().split()
    require(len(tree) == 4 and tree[:2] == ["160000", "commit"] and tree[2] == expected_builds,
            "candidate tag Builds provenance substituted")


def provisional_successor_execution_policy(selection):
    """Route historically incapable old endpoints safely; this is not the successor case table."""
    original = qualification.load_support_table({"candidate": {"version": CANDIDATE},
        "comparisons": [{"version": version} for version in qualification.COMPARISON_VERSIONS]})
    version = selection["candidate"]["version"]
    inventory = qualification.executed_case_inventory(selection)
    rules = {}
    for lane, identifiers in inventory.items():
        old = original["lanes"][lane]["cases"]
        require([rule["id"].replace(CANDIDATE, version) for rule in old] == list(identifiers),
                "successor execution inventory differs from the historically fenced route plan")
        for rule in old:
            rules[(lane, rule["id"].replace(CANDIDATE, version))] = rule["treatment"]
    return rules


def semantic_observation(observation, outcome):
    """Normalize only declared semantic outputs and inventory coordinates."""
    result = {"outcome": outcome}
    for name in ("metadata", "primary", "secondary", "actor", "append", "replay"):
        value = observation.get(name)
        if isinstance(value, dict):
            result[name] = {key: value[key] for key in ("sequence", "floor", "accepted", "event_count", "etag", "last_modified") if key in value}
    for name in ("writer", "reader"):
        if name in observation:
            result[name] = {key:value for key,value in observation[name].items() if key not in ("output_sha256",)}
    for name in ("before", "after", "appended_inventory"):
        if observation.get(name) is not None:
            result[name] = [{key:item[key] for key in ("tenant", "kind", "sequence", "floor")}
                            for item in observation[name] if item["kind"] != "bookkeeping"]
    return result


def semantic_delta(package, source):
    return [{"field": key, "package": package.get(key), "source": source.get(key)}
            for key in sorted(set(package) | set(source)) if package.get(key) != source.get(key)]


def persisted_preconditions(rows, raw):
    """Keep exact domain bytes and declare each excluded dynamic field for comparison."""
    derived = fixture_domain_rows(raw)
    require([row for row in rows if row["kind"] != "bookkeeping"] == derived,
            "persisted precondition coordinates differ from raw domain bytes")
    state, semantics, excluded = {}, [], []
    dynamic = {"event": {"messageId", "correlationId", "causationId", "timestamp"},
               "metadata": {"lastModified", "eTag", "etag"}, "snapshot": {"createdAt"}}
    for row in rows:
        if row["kind"] == "bookkeeping":
            continue
        data = raw[row["key"]]
        require(digest(data) == row["sha256"], "persisted precondition bytes differ from inventory")
        value = json.loads(data)
        state[row["key"]] = {"base64": base64.b64encode(data).decode(), "sha256": digest(data)}
        normalized = dict(value) if isinstance(value, dict) else value
        if isinstance(normalized, dict):
            for field in sorted(dynamic.get(row["kind"], set())):
                if field in normalized:
                    excluded.append({"key": row["key"], "field": field, "value": normalized.pop(field)})
            if isinstance(normalized.get("sourceProof"), dict):
                normalized["sourceProof"] = dict(normalized["sourceProof"])
                for field in ("sourceIncarnation", "headMessageId", "cumulativeDigest"):
                    if field in normalized["sourceProof"]:
                        excluded.append({"key": row["key"], "field": "sourceProof." + field,
                                         "value": normalized["sourceProof"].pop(field)})
        semantics.append({"key": row["key"], "tenant": row["tenant"], "kind": row["kind"],
                          "sequence": row["sequence"], "floor": row["floor"], "value": normalized})
    return {"inventory": rows, "state": state, "semantic": semantics, "dynamic_fields": excluded}


def fixture_domain_rows(raw):
    """Derive fixture inventory coordinates from literal keys and persisted contents."""
    rows = []
    for key, data in sorted(raw.items()):
        kind = "metadata" if key.endswith(":metadata") else "snapshot" if key.endswith(":snapshot") else "event" if ":events:" in key else None
        if kind is None:
            continue
        match = re.fullmatch(r"(?:eventstore\|\|(?:AggregateActor\|\|(?P<actor_tenant>tenant-[ab]):counter:fixture\|\|)?)?"
                             r"(?P<tenant>tenant-[ab]):counter:fixture:(?:metadata|snapshot|events:(?P<sequence>[1-9][0-9]*))", key)
        require(match is not None, "unexpected persisted fixture domain key")
        tenant = match['tenant']
        require(match['actor_tenant'] is None or match['actor_tenant'] == tenant,
                "persisted actor and state-key tenant differ")
        value = json.loads(data)
        if isinstance(value, dict):
            require(all(value.get(field, expected) == expected for field, expected in
                        (("tenantId", tenant), ("domain", "counter"), ("aggregateId", "fixture"))),
                    "persisted payload and state-key identity differ")
            if 'identity' in value:
                identity = value['identity']
                require(isinstance(identity, dict) and all(identity.get(field, expected) == expected for field, expected in
                            (("tenantId", tenant), ("domain", "counter"), ("aggregateId", "fixture"),
                             ("actorId", tenant + ":counter:fixture"))), "persisted nested identity and state key differ")
        sequence = int(match['sequence']) if kind == "event" else value.get("currentSequence" if kind == "metadata" else "sequenceNumber")
        floor = value.get("retainedFloor", 1) if kind == "metadata" else None
        require(type(sequence) is int and sequence >= 0 and (kind != "metadata" or type(floor) is int),
                "persisted fixture coordinates require exact integer types")
        if kind == "event" and isinstance(value, dict):
            require(type(value.get("sequenceNumber")) is int and value["sequenceNumber"] == sequence,
                    "persisted event key and sequence differ")
        rows.append({"key": key, "tenant": tenant, "kind": kind, "sequence": sequence,
                     "floor": floor, "sha256": digest(data)})
    return rows


def validate_seed_proofs(rows, raw):
    """Validate each complete seed proof before discarding any dynamic comparison fields."""
    require([row for row in rows if row["kind"] != "bookkeeping"] == fixture_domain_rows(raw),
            "seed coordinates contradict persisted bytes")
    for tenant, head in (("tenant-a", 12), ("tenant-b", 3)):
        metadata = [json.loads(raw[row["key"]]) for row in rows if row["tenant"] == tenant and row["kind"] == "metadata"]
        require(len(metadata) == 1 and type(metadata[0].get("currentSequence")) is int
                and metadata[0]["currentSequence"] == head and type(metadata[0].get("retainedFloor", 1)) is int
                and metadata[0].get("retainedFloor", 1) == 1 and metadata[0].get("sourceErasurePending", False) is False,
                "invalid complete seed metadata")
        proof = metadata[0].get("sourceProof")
        if proof is None:
            continue
        events = {row["sequence"]: raw[row["key"]] for row in rows if row["tenant"] == tenant and row["kind"] == "event"}
        require(isinstance(proof, dict) and type(proof.get("codecVersion")) is int and proof["codecVersion"] == 1
                and proof.get("tenantId") == tenant and proof.get("domain") == "counter" and proof.get("aggregateId") == "fixture"
                and type(proof.get("headSequence")) is int and proof["headSequence"] == head
                and type(proof.get("retainedFloor")) is int and proof["retainedFloor"] == 1 and proof.get("erased") is False
                and isinstance(proof.get("sourceIncarnation"), str)
                and re.fullmatch(r"[0-7][0-9A-HJKMNP-TV-Z]{25}", proof["sourceIncarnation"]) is not None
                and isinstance(proof.get("cumulativeDigest"), str)
                and re.fullmatch(r"[0-9a-f]{64}", proof["cumulativeDigest"]) is not None
                and sorted(events) == list(range(1, head + 1)) and isinstance(proof.get("headMessageId"), str)
                and bool(proof["headMessageId"].strip()) and proof["headMessageId"] == json.loads(events[head]).get("messageId"),
                "malformed complete seed source proof")
        require(all(json.loads(data).get("tenantId") == tenant and json.loads(data).get("domain") == "counter"
                    and json.loads(data).get("aggregateId") == "fixture" for data in events.values())
                and source_proof_digest(events, proof) == proof["cumulativeDigest"], "complete seed source proof digest or identity differs")


def retained_metadata(values, floor):
    """Refuse malformed positive inputs before changing any retention coordinate."""
    rows = fixture_domain_rows(values)
    if floor == 1:
        validate_seed_proofs(rows, values)
    metadata = [json.loads(data) for key, data in values.items() if "tenant-a" in key and key.endswith(":metadata")]
    snapshots = [json.loads(data) for key, data in values.items() if "tenant-a" in key and key.endswith(":snapshot")]
    events = {int(key.rsplit(":", 1)[1]): data for key, data in values.items() if "tenant-a" in key and ":events:" in key}
    secondary = [json.loads(data) for key, data in values.items() if "tenant-b" in key and key.endswith(":metadata")]
    require(len(metadata) == len(snapshots) == len(secondary) == 1 and metadata[0].get("currentSequence") == 12
            and metadata[0].get("retainedFloor", 1) == floor and snapshots[0].get("sequenceNumber") == 9
            and sorted(events) == list(range(floor, 13)) and secondary[0].get("currentSequence") == 3,
            "malformed retained fixture coordinates")
    snapshot = snapshots[0]
    require(type(snapshot.get("sequenceNumber")) is int and type(snapshot.get("snapshotEnvelopeVersion", 0)) is int
            and snapshot.get("snapshotEnvelopeVersion", 0) in (0, 1) and snapshot.get("state") == {"count": 9}
            and type(snapshot["state"].get("count")) is int
            and all(snapshot.get(key, expected) == expected for key, expected in
                    (("tenantId", "tenant-a"), ("domain", "counter"), ("aggregateId", "fixture")))
            and ("protectionMetadata" not in snapshot or snapshot["protectionMetadata"].get("state") in (0, "Unprotected")),
            "malformed retained Counter snapshot envelope or folded state")
    value = metadata[0]
    proof = value.get("sourceProof")
    if proof is not None:
        head = json.loads(events[12])
        require(isinstance(proof, dict) and proof.get("codecVersion") == 1
                and proof.get("tenantId") == "tenant-a" and proof.get("domain") == "counter"
                and proof.get("aggregateId") == "fixture" and proof.get("headSequence") == 12
                and proof.get("retainedFloor") == floor and proof.get("erased") is False
                and re.fullmatch(r"[0-7][0-9A-HJKMNP-TV-Z]{25}", proof.get("sourceIncarnation", "")) is not None
                and re.fullmatch(r"[0-9a-f]{64}", proof.get("cumulativeDigest", "")) is not None
                and isinstance(proof.get("headMessageId"), str) and bool(proof["headMessageId"].strip())
                and proof["headMessageId"] == head.get("messageId") and not value.get("sourceErasurePending", False),
                "malformed retained fixture source proof")
        if floor == 1:
            require(source_proof_digest(events, proof) == proof["cumulativeDigest"],
                    "retained fixture source proof digest differs from original event chain")
    return value


def source_proof_digest(events, proof):
    """Recompute the source-v1 chain over the original complete stopped-writer fixture."""
    def string(value):
        data = None if value is None else value.encode()
        return struct.pack("<i", -1 if data is None else len(data)) + (data or b"")
    seed = b"".join(string(value) for value in ("hexalith.projection.source.seed.v1", proof["tenantId"],
        proof["domain"], proof["aggregateId"], proof["sourceIncarnation"]))
    chain = bytes.fromhex(digest(seed))
    for sequence in sorted(events):
        event = json.loads(events[sequence])
        require(all(type(event.get(field)) is int for field in ("sequenceNumber", "globalPosition", "metadataVersion"))
                and (event.get("payloadVersion") is None or type(event["payloadVersion"]) is int),
                "source proof event coordinates require exact integer types")
        timestamp = event["timestamp"]
        date = datetime.datetime.fromisoformat(timestamp.replace("Z", "+00:00"))
        utc = date.astimezone(datetime.timezone.utc)
        elapsed = utc - datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
        fraction = re.search(r"\.(\d+)", timestamp)
        ticks = ((elapsed.days * 86400 + elapsed.seconds) * 10_000_000
                 + int(((fraction.group(1) if fraction else "") + "0000000")[:7]))
        data = b"".join(string(value) for value in ("hexalith.projection.source.event.v1", event.get("messageId"),
            event.get("aggregateId"), event.get("aggregateType"), event.get("tenantId"), event.get("domain")))
        data += struct.pack("<qqqi", event["sequenceNumber"], event["globalPosition"], ticks,
                            int(date.utcoffset().total_seconds() // 60))
        data += b"".join(string(event.get(field)) for field in ("correlationId", "causationId", "userId",
            "domainServiceVersion", "eventTypeName"))
        data += struct.pack("<i", event["metadataVersion"]) + string(event.get("serializationFormat"))
        payload = base64.b64decode(event["payload"], validate=True)
        data += struct.pack("<i", len(payload)) + payload
        extensions = event.get("extensions")
        data += struct.pack("<i", -1 if extensions is None else len(extensions))
        if extensions is not None:
            data += b"".join(string(key) + string(extensions[key]) for key in sorted(extensions))
        data += string(event.get("eventContractType"))
        version = event.get("payloadVersion")
        data += struct.pack("<i", int(version is not None)) + (b"" if version is None else struct.pack("<i", version))
        data += string(event.get("applicationPayloadDigest"))
        chain = bytes.fromhex(digest(string("hexalith.projection.source.step.v1") + chain + bytes.fromhex(digest(data))))
    return chain.hex()


class Executor:
    """Own all resources by exact identity and retain partial progress on interruption."""

    @property
    def candidate(self):
        return getattr(self, "_candidate", CANDIDATE)

    @candidate.setter
    def candidate(self, value):
        self._candidate = value

    @property
    def versions(self):
        return getattr(self, "_versions", VERSIONS)

    @versions.setter
    def versions(self, value):
        self._versions = value

    def __init__(self, output, inputs=None, source_root=None, source_builds_root=None, tooling=None, inputs_sha256=None, observation=None, observation_sha256=None):
        selected_tooling = inputs.get("tooling") if inputs is not None else tooling
        if selected_tooling is not None:
            initial_binding = preparation.source_binding(ROOT, selected_tooling, source_root, source_builds_root)
            if inputs is not None:
                preparation.validate_selected_source(inputs, initial_binding)
        else:
            require(source_builds_root is None, "source builds path requires selected tooling pins")
        self.output = Path(output).absolute()
        require(not self.output.exists() and self.output == self.output.resolve(), "output exists or is substituted")
        storage.safe_directory(self.output.parent, ROOT, create=True)
        self.output.mkdir(parents=True)
        self.invocation = uuid.uuid4().hex
        self.storage = storage.Store(self.output.parent / ".p1r-artifacts", storage.context(self.invocation, ROOT))
        self.scratch = Path(tempfile.mkdtemp(prefix="p1r-published-" + self.invocation + "-"))
        self.inputs = inputs
        self.source_root = Path(source_root).absolute() if source_root is not None else ROOT
        self.explicit_source_root = source_root is not None and selected_tooling is None
        self.source_builds_root = Path(source_builds_root).absolute() if source_builds_root is not None else None
        self.candidate = inputs["candidate"]["version"] if inputs else CANDIDATE
        self.versions = ("3.110.0", "3.70.1", self.candidate)
        self.inputs_sha256 = inputs_sha256 or (digest(canonical(inputs)) if inputs else None)
        self.observation_capture = observation is not None
        self.storage.context.update(inputs=self.inputs_sha256 if inputs is not None else
            observation_sha256 or (digest(canonical(observation)) if observation is not None else None),
            candidate=digest(canonical(inputs["candidate"])) if inputs is not None else
                digest(canonical(observation["candidate_proposed"])) if observation is not None else None)
        self.commands, self.containers, self.processes, self.active = [], {}, [], []
        self.pending_containers = {}
        preparation.subreaper()
        self.receipts, self.identities, self.evidence = [], {}, {}
        self.configurations = []
        self.probe_binaries = {}
        self.witness_sources = {}
        self.shared_before = None
        self.fixture = {"id": "p1r-" + self.invocation, "synthetic": False}
        self.started = stamp()
        self.errors = []
        self.seeds = {}
        self.digest_key = base64.b64encode(os.urandom(32)).decode()
        self.delegation = uuid.uuid4().hex
        self.app_token = uuid.uuid4().hex
        self.workload_key = base64.b64encode(os.urandom(48)).decode()
        self.invocation_argv = [part.decode() for part in regular(Path(f"/proc/{os.getpid()}/cmdline")).split(b"\0") if part]
        self.pending_restore = None
        self.operational_started = False
        self.current_checks = None
        self.result = {"schema": "hexalith.p1r.executor.v1", "invocation": self.invocation, "started_utc": self.started,
                       "finished_utc": None, "executor_source_sha256": None,
                       "inputs_sha256": self.inputs_sha256, "receipts": [], "errors": [], "p1r_usable": False}
        self.save()
        try:
            self.source_binding = (initial_binding if selected_tooling is not None else
                                   preparation.source_binding(ROOT, self.source_root) if self.explicit_source_root else
                                   preparation.source_binding())
            write_json(self.output / "executor-source.json", self.source_binding)
            self.result["executor_source_sha256"] = digest(regular(self.output / "executor-source.json"))
            self.storage.context["source"] = self.result["executor_source_sha256"]
            self.shared_before = self.shared()
            self.save()
        except BaseException as error:
            self.errors.append("initialization: " + (str(error) if isinstance(error, InvalidPacket) else type(error).__name__))
            self.cleanup()
            raise

    def save(self):
        if hasattr(self, "storage"):
            self.result["capture_context"] = dict(self.storage.context)
        write_json(self.output / "commands.json", self.commands)
        self.result.update(receipts=self.receipts, errors=self.errors)
        write_json(self.output / "execution.json", self.result)

    def record(self, argv, cwd, started, code, data, retain=True, ownership=None, cleanup=None, timeout=None, input_bytes=None):
        row = {"id": len(self.commands) + 1, "argv": [str(arg) for arg in argv], "cwd": str(cwd),
               "started_utc": started, "finished_utc": max(started, stamp()), "exit_code": code,
               "output_sha256": digest(data)}
        if hasattr(self, "storage") and retain:
            self.storage.command(row, data)
        else:
            output = data.decode("utf-8", "replace") if retain else None
            row.update(output=output, output_sha256=digest(output.encode() if retain else data))
        if timeout is not None:
            row["timeout_seconds"] = timeout
        if input_bytes is not None:
            row.update(input_sha256=digest(input_bytes), input_bytes=len(input_bytes))
            if "psql" in row["argv"]:
                row["provider_sql"] = input_bytes.decode()
        binary = getattr(self, "probe_binaries", {}).get(row["argv"][1] if len(row["argv"]) > 1 else None)
        if row["argv"][:1] == ["dotnet"] and binary is not None:
            row["probe_binary"] = dict(binary)
        serving = getattr(self, "consumer_binaries", {}).get(row["argv"][1] if len(row["argv"]) > 1 else None)
        if row["argv"][:1] == ["dotnet"] and serving is not None:
            row["consumer_binary"] = dict(serving)
        if ownership is not None:
            row.update(process=ownership.root, owned_processes=list(ownership.owned.values()), cleanup=cleanup or [])
            if ownership.cleanup_diagnostics:
                row["cleanup_error_diagnostics"] = list(ownership.cleanup_diagnostics)
        if "--resources-path" in row["argv"]:
            row["runtime_configuration"] = self.configurations[-1]["id"]
            row["runtime_configuration_sha256"] = digest(canonical(self.configurations[-1]))
        self.commands.append(row)
        self.save()
        return row

    def command_output(self, row):
        if "output_ref" not in row:
            return row.get("output")
        return storage.command_text(row, self.storage.context)

    def retain_packet(self, value):
        return storage.retain_inventories(value, self.storage) if hasattr(self, "storage") else value

    def run(self, argv, cwd=None, env=None, timeout=180, input_bytes=None, retain=True, check=True):
        """Own synchronous trees, including nonce-registered detached descendants."""
        if os.environ.get("HEXALITH_P1R_ISOLATION"):
            from p1r_isolation import private_environment
            env = private_environment(os.environ if env is None else env)
        cwd = Path(cwd or self.scratch)
        started, interrupted, code = stamp(), False, 127
        ownership = preparation.OwnedProcesses()
        log = tempfile.TemporaryFile()
        input_file = tempfile.TemporaryFile() if input_bytes is not None else None
        item = None
        if input_file is not None:
            input_file.write(input_bytes)
            input_file.seek(0)
        try:
            process = subprocess.Popen([str(arg) for arg in argv], cwd=cwd,
                env=dict(os.environ if env is None else env, HEXALITH_P1R_CONTROL_NONCE=ownership.nonce),
                stdin=input_file if input_file is not None else subprocess.DEVNULL,
                stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            ownership.attach(process)
            item = {"process": process, "ownership": ownership, "argv": argv, "started": started, "synchronous": True}
            self.processes.append(item)
            deadline = time.monotonic() + timeout
            while process.poll() is None:
                ownership.discover()
                if time.monotonic() >= deadline:
                    code = 124
                    break
                time.sleep(min(.05, max(0, deadline - time.monotonic())))
            else:
                code = process.returncode
        except OSError as error:
            log.write((type(error).__name__ + ": " + str(error)).encode())
        except KeyboardInterrupt:
            code, interrupted = 130, True
        finally:
            handler = signal.signal(signal.SIGINT, signal.SIG_IGN)
            try:
                cleanup = [ownership.cleanup(), ownership.cleanup()] if item else []
                if code == 124:
                    log.write(b"\nexecutor timeout\n")
                if interrupted:
                    log.write(b"\nexecutor interrupted\n")
                log.seek(0)
                data = log.read()
                self.record(argv, cwd, started, code, data, retain, ownership, cleanup, timeout, input_bytes)
            finally:
                log.close()
                if input_file is not None:
                    input_file.close()
                signal.signal(signal.SIGINT, handler)
        if interrupted:
            raise KeyboardInterrupt("owned command interrupted; refusal and cleanup retained")
        require(all(not attempt["remaining"] and not attempt["errors"] for attempt in cleanup), "owned command cleanup failed")
        require(not check or code == 0, "command failed: " + " ".join(str(arg) for arg in argv) + "; exit=" + str(code))
        return data

    def http(self, method, url, value=None, timeout=45):
        body = None if value is None else canonical(value)
        argv = ["HTTP", method, url, "request-sha256=" + digest(body or b"")]
        started = stamp()
        try:
            request = urllib.request.Request(url, data=body, method=method, headers={"Content-Type": "application/json"})
            try:
                with urllib.request.urlopen(request, timeout=timeout) as response:
                    data, status = response.read(), response.status
            except urllib.error.HTTPError as error:
                data, status = error.read(), error.code
            self.record(argv, self.scratch, started, 0 if status < 400 else status, data)
            require(status < 400, "HTTP request refused: " + str(status))
            return json.loads(data) if data else None
        except (OSError, urllib.error.URLError) as error:
            self.record(argv, self.scratch, started, 124, type(error).__name__.encode())
            raise

    def shared(self):
        names = self.run(["docker", "ps", "-aq"]).decode().split()
        if not names:
            return {}
        rows = docker_observations(self.run(safe_inspect_argv(*names)))
        return {row["Id"]: {"image": row["Image"], "running": row["State"]["Running"], "started": row["State"]["StartedAt"]}
                for row in rows if row["Id"] not in self.containers}

    def container(self, role, image, arguments=(), port=6379, start=True, host_port=None, environment=None):
        name = "p1r-" + self.invocation + "-" + role + "-" + uuid.uuid4().hex[:8]
        host_port = host_port or reserve_port()
        cidfile = self.scratch / (name + ".cid")
        argv = ["docker", "create", "--cidfile", str(cidfile), "--name", name, "--label",
                "hexalith.p1r.invocation=" + self.invocation, "-p", f"127.0.0.1:{host_port}:{port}"]
        if image == POSTGRES:
            argv += ["-e", "POSTGRES_PASSWORD", "-e", "POSTGRES_DB=eventstore"]
        argv += [image, *arguments]
        self.pending_containers[name] = {"name": name, "image": image, "role": role, "port": host_port}
        try:
            self.run(argv, env=environment)
            command_id = self.commands[-1]["id"]
        finally:
            identity = self.recover_container(name, cidfile)
        require(identity is not None, "created owned container identity unavailable")
        if start:
            self.run(["docker", "start", identity])
            command_id = self.commands[-1]["id"]
        return identity, host_port, command_id

    def recover_container(self, name, cidfile=None):
        """Recover interrupted creation only through exact name AND invocation label."""
        requested = cidfile.read_text().strip() if cidfile is not None and cidfile.is_file() else name
        data = self.run(safe_inspect_argv(requested), cwd=ROOT, check=False)
        if self.commands[-1]["exit_code"] != 0:
            return None
        row = docker_observations(data)[0]
        require(row["Name"] == "/" + name
                and row["Config"]["Labels"].get("hexalith.p1r.invocation") == self.invocation
                and (requested == name or requested == row["Id"]), "container creation ownership substituted")
        identity = row["Id"]
        self.containers[identity] = self.pending_containers.pop(name)
        self.result["owned_containers"] = dict(self.containers)
        self.save()
        return identity

    def launch(self, argv, env):
        if os.environ.get("HEXALITH_P1R_ISOLATION"):
            from p1r_isolation import private_environment
            env = private_environment(env)
        log = self.scratch / ("process-" + uuid.uuid4().hex + ".log")
        ownership = preparation.OwnedProcesses()
        environment = dict(env, HEXALITH_P1R_CONTROL_NONCE=ownership.nonce)
        handle = log.open("wb")
        started = stamp()
        process = subprocess.Popen([str(arg) for arg in argv], cwd=self.scratch, env=environment,
                                   stdout=handle, stderr=subprocess.STDOUT, start_new_session=True)
        handle.close()
        ownership.attach(process)
        item = {"process": process, "ownership": ownership, "log": log, "argv": argv, "started": started}
        self.processes.append(item)
        self.active.append(item)
        return item

    def stop(self, item):
        cleanup = item["ownership"].cleanup()
        code = item["process"].poll()
        require(code is not None, "owned process exit status unavailable")
        self.record(item["argv"], self.scratch, item["started"], code, item["log"].read_bytes(),
                    ownership=item["ownership"], cleanup=[cleanup])
        require(not cleanup["remaining"] and not cleanup["errors"], "owned process cleanup failed")

    def stop_nodes(self):
        errors = []
        for item in reversed(self.active):
            try:
                self.stop(item)
            except (Exception, KeyboardInterrupt) as error:
                errors.append(type(error).__name__)
        self.active = [item for item in self.active if item["process"].poll() is None]
        require(not errors, "owned node cleanup failed: " + ",".join(errors))

    def check_sources(self):
        """Recheck both roles after waits and before retaining dependent measurements."""
        binding = getattr(self, "source_binding", None)
        if binding is None:
            return
        preparation.check_current_source(binding)
        selected = getattr(self, "inputs", None) or ({"candidate": self.selected_candidate} if hasattr(self, "selected_candidate") else None)
        if selected is not None and hasattr(qualification, "validate_candidate_source"):
            qualification.validate_candidate_source(binding, selected)

    def wait(self, url, item):
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            require(item["process"].poll() is None, "owned application exited during startup")
            try:
                observed = self.http("GET", url, timeout=1)
            except (OSError, urllib.error.URLError, InvalidPacket):
                time.sleep(.15)
                continue
            self.check_sources()
            return observed
        raise TimeoutError("owned endpoint startup timeout")

    def evaluate_source_consumers(self, root, environment):
        """Measure the connected source graph and the imports effective at this build boundary."""
        measured = []
        pending = [root / project.lower() / (project + ".csproj") for project in PROJECTS]
        visited = set()
        while pending:
            project = pending.pop(0)
            if project in visited:
                continue
            visited.add(project)
            self.run(preparation.source_evaluation_arguments(project, self.source_binding, "imports"),
                     cwd=root, env=environment)
            imports = self.commands[-1]
            data = self.run(preparation.source_evaluation_arguments(project, self.source_binding, "evaluation"),
                            cwd=root, env=environment)
            value = {"project": str(project), "imports": imports, "evaluation": self.commands[-1]}
            qualification.validate_source_evaluation(storage.materialize(value, self.storage.context) if hasattr(self, "storage") else value, self.source_binding, root)
            measured.append(value)
            pending.extend(Path(row["FullPath"]) for row in json.loads(data)["Items"]["ProjectReference"])
        qualification.validate_source_evaluations({"source_evaluations": storage.materialize(measured, self.storage.context) if hasattr(self, "storage") else measured}, self.source_binding, root)
        return measured

    def build_consumers(self, version, source=False):
        self.check_sources()
        role = "source" if source else "candidate" if version == self.candidate else "comparison-" + version
        root = self.output / "consumers" / role
        shutil.copytree(CONSUMERS, root)
        if getattr(self, "explicit_source_root", False) or "runtime_source" in getattr(self, "source_binding", {}):
            shutil.copyfile(self.source_root / "global.json", root / "global.json")
        packages = root / "packages"
        environment = dict(os.environ, CI="true", NUGET_PACKAGES=str(packages), DOTNET_NOLOGO="true",
                           DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE="false", DOTNET_USE_POLLING_FILE_WATCHER="1")
        configuration = "Debug" if source else "Release"
        props = ["-p:EventStoreVersion=" + version, "-p:NuGetAudit=false"]
        if source:
            environment.pop("CI", None)
            props += ["-p:UseCurrentSource=true", "-p:UseHexalithProjectReferences=true",
                      "-p:EventStoreSourceRoot=" + str(getattr(self, "source_root", ROOT))]
            if "runtime_source" in getattr(self, "source_binding", {}):
                require(version == preparation.RUNTIME_VERSION, "runtime source version substituted")
                props = ["-p:NuGetAudit=false", *("-p:" + name + "=" + value for name, value in
                                                preparation.runtime_source_properties(self.source_binding).items())]
                self.source_evaluations = self.evaluate_source_consumers(root, environment)
                for project in PROJECTS:
                    self.run(["dotnet", "restore", root / project.lower() / (project + ".csproj"), *props],
                             cwd=root, env=environment, timeout=300)
                # Restore writes NuGet imports. Retain and validate the graph that
                # the following --no-restore Debug builds actually consume.
                self.source_evaluations = self.evaluate_source_consumers(root, environment)
        manifest = {"schema": qualification.EVIDENCE_SCHEMA, "role": role, "fixture": None,
                    "packages_root": "packages", "configuration": configuration, "projects": []}
        serving_binaries = {}
        for project in PROJECTS:
            csproj = root / project.lower() / (project + ".csproj")
            if not (source and "runtime_source" in getattr(self, "source_binding", {})):
                self.run(["dotnet", "restore", csproj, *props], cwd=root, env=environment, timeout=300)
            self.run(["dotnet", "build", csproj, "--no-restore", "-c", configuration, "-m:1", *props],
                     cwd=root, env=environment, timeout=300)
            output = project.lower() + "/bin/" + configuration + "/net10.0"
            # Physically load all five package assemblies in each actual consumer.
            if project == "Probe":
                binary = {"role": role, "configuration": configuration,
                          "path": str(root / output / "Probe.dll"),
                          "sha256": digest(regular(root / output / "Probe.dll"))}
                self.probe_binaries[binary["path"]] = binary
                # Private executor witness; the published package manifest stays unchanged.
                write_json(root / "probe-binary.json", binary)
                observed = json.loads(self.run(["dotnet", root / output / "Probe.dll", "identity"], cwd=root, env=environment))
                identity = observed["observation"]
            else:
                binary = {"role":role,"configuration":configuration,"project":project,
                          "path":str(root / output / (project + ".dll")),
                          "sha256":digest(regular(root / output / (project + ".dll")))}
                if not hasattr(self, "consumer_binaries"):
                    self.consumer_binaries = {}
                self.consumer_binaries[binary["path"]] = binary
                serving_binaries[project] = binary
                write_json(root / "serving-binaries.json", serving_binaries)
                app_port = reserve_port()
                item = self.launch(["dotnet", root / output / (project + ".dll")],
                                   dict(environment, ASPNETCORE_URLS=f"http://127.0.0.1:{app_port}", ASPNETCORE_ENVIRONMENT="Development"))
                try:
                    ready = self.wait(f"http://127.0.0.1:{app_port}/ready", item)
                    identity = ready["identity"] if (source or version == self.candidate) and project == "Domain" else self.http("GET", f"http://127.0.0.1:{app_port}/identity")
                finally:
                    self.stop_nodes()
            loaded = "loaded/" + project + ".json"
            write_json(root / loaded, identity)
            manifest["projects"].append({"name": project, "assets": project.lower() + "/obj/project.assets.json",
                                         "lock": project.lower() + "/packages.lock.json", "loaded": loaded, "output": output})
            self.identities.setdefault(version if not source else "source", []).extend(identity["assemblies"])
            if "runtime_source" in getattr(self, "source_binding", {}):
                preparation.check_current_source(self.source_binding)
        write_json(root / "evidence.json", manifest)
        if source and "candidate_source" in getattr(self, "source_binding", {}):
            write_json(root / "source-evidence.json", qualification.source_consumer_binding(root, self.source_binding))
        self.check_sources()
        self.evidence[role] = root
        return root

    def verify_selected_candidate_archives(self):
        """Bind downloaded candidate bytes before any protected fixture route is started."""
        candidate = self.inputs["candidate"]
        root = self.evidence["candidate"]
        require([package["id"] for package in candidate["packages"]] == list(PACKAGE_IDS),
                "candidate package inventory substituted")
        for package in candidate["packages"]:
            identifier, version = package["id"], candidate["version"]
            lower = identifier.lower()
            base = root / "packages" / lower / version
            archive = base / f"{lower}.{version}.nupkg"
            data = regular(archive)
            nuspec, entries, _ = qualification.inspect_archive(archive, data)
            metadata = qualification.parse_json(regular(base / ".nupkg.metadata"))
            require(nuspec["valid"] and nuspec["id"] == identifier and nuspec["version"] == version
                    and entries["signature"] and not entries["synthetic_marker"]
                    and digest(data) == package["archive_sha256"]
                    and metadata["contentHash"] == package["content_hash"]
                    and nuspec["repository_commit"] == package["repository_commit"] == candidate["tag_commit"],
                    "candidate archive differs from selected published identity")
            self.run(["dotnet", "nuget", "verify", "--all", archive, "--verbosity", "minimal"], cwd=root)
            require(qualification.REPOSITORY_SIGNATURE in self.command_output(self.commands[-1]),
                    "candidate repository signature not verified")

    def observe_inputs(self, planning, reference):
        """Observe the already approved six selections; verify downloads rather than reuse preflight passes."""
        candidate = planning["candidate_proposed"]
        self.selected_candidate = candidate
        self.check_sources()
        tooling = planning.get("tooling")
        if tooling is not None:
            binding = validate_observation_inputs(planning, getattr(self, "source_root", None),
                                                  getattr(self, "source_builds_root", None))
            require(binding == self.source_binding, "observation tooling or runtime source binding differs")
        self.candidate = candidate["version"]
        self.versions = ("3.110.0", "3.70.1", self.candidate)
        if self.candidate == CANDIDATE:
            require(candidate["tag_commit"] == TAG_COMMIT and candidate["builds"]["commit"] == BUILDS_COMMIT,
                    "candidate selection substituted")
            expected_tag_builds = TAG_BUILDS_COMMIT
        else:
            require(qualification.is_successor_selection({"candidate": candidate}),
                    "unrepaired or unbound successor selection")
            qualification.validate_selection(candidate, "candidate")
            require([package["id"] for package in candidate["packages"]] == list(PACKAGE_IDS)
                    and all(package["repository_commit"] == candidate["tag_commit"]
                            for package in candidate["packages"])
                    and "tag_builds_commit" in candidate,
                    "successor observation lacks exact source-bound packages or tag Builds identity")
            if tooling is None:
                tag_commit = preparation.git(getattr(self, "source_root", ROOT), "rev-parse", candidate["tag"] + "^{commit}").decode().strip()
                source_commit = preparation.git(getattr(self, "source_root", ROOT), "rev-parse", "HEAD").decode().strip()
                require(tag_commit == source_commit == candidate["tag_commit"],
                        "successor observation source or tag substituted")
            expected_tag_builds = candidate["tag_builds_commit"]
        runtime_root = getattr(self, "source_root", ROOT)
        tag_builds = preparation.git(runtime_root, "ls-tree", "v" + self.candidate, "references/Hexalith.Builds").decode().split()
        require(len(tag_builds) == 4 and tag_builds[:2] == ["160000", "commit"]
                and tag_builds[2] == expected_tag_builds, "candidate tag Builds provenance substituted")
        selections = {}
        for version in self.versions:
            root = self.build_consumers(version)
            tag_commit = preparation.git(runtime_root if version == self.candidate else ROOT,
                                         "rev-parse", "v" + version + "^{commit}").decode().strip()
            if version == self.candidate:
                builds = {key: candidate["builds"][key] for key in ("version", "commit")}
                require(tag_commit == candidate["tag_commit"], "candidate tag substituted")
            else:
                tree = preparation.git(ROOT, "ls-tree", "v" + version, "references/Hexalith.Builds").decode().split()
                require(len(tree) == 4 and tree[:2] == ["160000", "commit"], "comparison Builds provenance missing")
                commit = tree[2]
                builds_root = (Path(self.source_binding["workspace_builds"]["path"]) if "workspace_builds" in self.source_binding
                               else Path(self.source_binding["repository"]) / "references/Hexalith.Builds")
                described = preparation.git(builds_root, "describe", "--tags", "--always", commit).decode().strip()
                builds = {"version": described.removeprefix("v"), "commit": commit}
            selected = []
            for identifier in PACKAGE_IDS:
                lower = identifier.lower()
                base = root / "packages" / lower / version
                archive = base / f"{lower}.{version}.nupkg"
                data = regular(archive)
                nuspec, entries, dlls = qualification.inspect_archive(archive, data)
                require(nuspec["valid"] and nuspec["id"] == identifier and nuspec["version"] == version
                        and entries["signature"] and not entries["synthetic_marker"], "published archive substituted")
                metadata = qualification.parse_json(regular(base / ".nupkg.metadata"))
                row = {"id": identifier, "archive_sha256": digest(data), "content_hash": metadata["contentHash"],
                       "repository_commit": nuspec["repository_commit"]}
                if version == self.candidate:
                    expected = next(package for package in candidate["packages"] if package["id"] == identifier)
                    require(all(row[key] == expected[key] for key in row), "candidate archive differs from approved observation")
                self.run(["dotnet", "nuget", "verify", "--all", archive, "--verbosity", "minimal"], cwd=root)
                require(qualification.REPOSITORY_SIGNATURE in self.command_output(self.commands[-1]), "repository signature not verified")
                target = self.output / "archives" / (identifier + "." + version + ".nupkg")
                target.parent.mkdir(exist_ok=True)
                shutil.copyfile(archive, target)
                selected.append(row)
            selections[version] = {"version": version, "tag": "v" + version, "tag_commit": tag_commit,
                                   "builds": builds, "feed": candidate["feed"], "packages": selected}
            if version == self.candidate and self.candidate != CANDIDATE:
                selections[version]["tag_builds_commit"] = expected_tag_builds
        inputs = {"schema": qualification.INPUTS_SCHEMA, "fixture": None,
                  "authority": {"owner": "user", "reference": reference,
                                "date": datetime.datetime.now(datetime.timezone.utc).date().isoformat()},
                  "candidate": selections[self.candidate], "rollback": None,
                  "comparisons": [selections[version] for version in qualification.COMPARISON_VERSIONS],
                  "operational_profile": {"runtime": "dapr", "runtime_version": "1.18.2", "backend": "state.postgresql",
                                          "backend_image": POSTGRES, "selected_by": "user", "reference": reference},
                  "assertion_instrumentation": {"mechanism": MECHANISM, "accepted_by": "test-owner", "reference": reference},
                  "selected_additions": list(qualification.ADDITIONS)}
        if tooling is not None:
            inputs["tooling"] = dict(tooling)
            preparation.check_current_source(self.source_binding)
        qualification.validate_inputs(inputs)
        self.check_sources()
        self.inputs = inputs
        self.candidate = inputs["candidate"]["version"] if inputs else CANDIDATE
        self.versions = ("3.110.0", "3.70.1", self.candidate)
        self.inputs_sha256 = digest(canonical(inputs))
        write_json(self.output / "owner-inputs.json", inputs)
        self.inputs_sha256 = digest(regular(self.output / "owner-inputs.json"))
        self.result["inputs_sha256"] = self.inputs_sha256
        self.result["generated_owner_inputs_sha256"] = self.inputs_sha256
        decisions = {"schema": qualification.DECISIONS_SCHEMA, "fixture": None, "inputs_sha256": self.inputs_sha256,
                     "decisions": [{"role": role, "decision": "pending", "owner": None, "date": None, "reference": None}
                                   for role in qualification.DECISION_ROLES],
                     "conformance": {"status": "pending", "baseline": None, "reference": None}}
        write_json(self.output / "owner-decisions.json", decisions)
        return inputs

    def topology(self):
        require(self.inputs["operational_profile"]["backend_image"] == POSTGRES
                and self.inputs["operational_profile"]["runtime_version"] == "1.18.2", "selected operational profile substituted")
        self.shared_before = self.shared()
        self.placement, self.placement_port, _ = self.container("placement", DAPR_IMAGE, ["./placement", "--port", "50005"], 50005)
        self.scheduler_port = reserve_port()
        self.scheduler, self.scheduler_port, _ = self.container("scheduler", DAPR_IMAGE,
            ["./scheduler", "--port", "50006", "--override-broadcast-host-port", f"127.0.0.1:{self.scheduler_port}",
             "--etcd-data-dir", "/tmp/p1r-etcd", "--etcd-client-listen-address", "0.0.0.0"], 50006, host_port=self.scheduler_port)
        self.pubsub, self.pubsub_port, _ = self.container("pubsub", REDIS)
        self.postgres_password = uuid.uuid4().hex
        self.redis, self.redis_port, _ = self.postgres_container("source")
        self.daprd = self.scratch / "daprd"
        self.run(["docker", "cp", self.placement + ":/daprd", self.daprd])
        self.daprd.chmod(0o700)
        version = self.run([self.daprd, "--version"]).decode()
        require(version.strip() == "1.18.2", "Dapr runtime substituted")
        self.operational_started = True
        write_json(self.output / "runtime-identity.json", {"profile": self.inputs["operational_profile"],
                   "daprd_sha256": digest(regular(self.daprd)), "dapr_version": version.strip(),
                   "images": docker_observations(self.run(safe_inspect_argv(self.placement, self.scheduler, self.pubsub, self.redis)))})

    def postgres_container(self, role, start=True):
        environment = dict(os.environ, POSTGRES_PASSWORD=self.postgres_password)
        identity, port, command_id = self.container(role, POSTGRES,
                                        port=5432, start=start, environment=environment)
        if start:
            self.postgres_wait(identity)
        return identity, port, command_id

    def postgres_wait(self, identity):
        for _ in range(60):
            self.run(["docker", "exec", identity, "pg_isready", "-h", "127.0.0.1", "-U", "postgres", "-d", "eventstore"], check=False)
            if self.commands[-1]["exit_code"] == 0:
                return
            time.sleep(.5)
        raise TimeoutError("owned PostgreSQL fixture startup timeout")

    def start_nodes(self, version, redis=None, port=None, interval=1000, write_alias=None):
        self.stop_nodes()
        resources = self.scratch / "resources"
        resources.mkdir(exist_ok=True)
        redis, port = redis or self.redis, port or self.redis_port
        tracked_state = regular(getattr(self, "source_root", ROOT) / "deploy/dapr/statestore-postgresql.yaml")
        require(b'type: state.postgresql' in tracked_state and b'version: v1' in tracked_state
                and b'{env:POSTGRES_CONNECTION_STRING}' in tracked_state, "tracked PostgreSQL v1 component substituted")
        connection = (f"host=127.0.0.1 port={port} user=postgres password={self.postgres_password} "
                      "dbname=eventstore sslmode=disable connect_timeout=10")
        (resources / "state.yaml").write_bytes(tracked_state.replace(b'{env:POSTGRES_CONNECTION_STRING}', connection.encode()))
        (resources / "pubsub.yaml").write_text(f'apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: pubsub\nspec:\n  type: pubsub.redis\n  version: v1\n  metadata:\n    - name: redisHost\n      value: "127.0.0.1:{self.pubsub_port}"\nscopes:\n  - eventstore\n')
        config = self.scratch / "discovery.yaml"
        config.write_text(f'apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: p1r-private\nspec:\n  features:\n    - name: HotReload\n      enabled: false\n  nameResolution:\n    component: sqlite\n    version: v1\n    configuration:\n      connectionString: "{self.scratch / "discovery.sqlite"}"\n')
        configuration = {
            "id": uuid.uuid4().hex, "version": version, "state_container": redis, "snapshot_interval": interval,
            "state_component_sha256": digest(tracked_state), "backend_image": POSTGRES,
            "runtime_version": self.inputs["operational_profile"]["runtime_version"],
            "observed_utc": stamp(),
            "source_workload_authority": "private Development symmetric JWT; production identity and P2 acceptance pending" if version in ("source", self.candidate) else None,
            "dotnet_reload_config_on_change": False, "dotnet_polling_file_watcher": True,
            "reminder_due": getattr(self, "reminder_due", None),
            "files": [{"name": path.name, "path": str(path),
                       "sha256": digest(tracked_state) if path.name == "state.yaml" else digest(regular(path)),
                       "content": tracked_state.decode() if path.name == "state.yaml" else path.read_text(),
                       **({"rendered_sha256": digest(regular(path)), "credential_redacted": True}
                          if path.name == "state.yaml" else {})}
                      for path in (config, resources / "state.yaml", resources / "pubsub.yaml")]}
        write_json(self.output / "configurations" / (configuration["id"] + ".json"), configuration)
        self.configurations.append(configuration)
        role = "source" if version == "source" else "candidate" if version == self.candidate else "comparison-" + version
        root = self.evidence[role]
        configuration = "Debug" if version == "source" else "Release"
        for project, appid in (("Domain", "counter"), ("Host", "eventstore")):
            app, http, grpc, internal, metrics, profile = [reserve_port() for _ in range(6)]
            environment = dict(os.environ, CI="true", NUGET_PACKAGES=str(root / "packages"),
                DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE="false", DOTNET_USE_POLLING_FILE_WATCHER="1",
                ASPNETCORE_ENVIRONMENT="Development", ASPNETCORE_URLS=f"http://127.0.0.1:{app}", DAPR_HTTP_PORT=str(http),
                DAPR_GRPC_PORT=str(grpc), NAMESPACE="p1r-" + self.invocation,
                P1R_DIGEST_KEY=self.digest_key, P1R_DELEGATION=self.delegation, APP_API_TOKEN=self.app_token,
                P1R_REMINDER_DUE=getattr(self, "reminder_due", "2030-01-01T00:00:00+00:00"),
                EventStore__Actors__AggregateActorTypeName="AggregateActor", EventStore__Snapshots__DefaultInterval=str(interval),
                EventStore__DomainServices__Registrations__counter__AppId="counter")
            environment.pop("POSTGRES_CONNECTION_STRING", None)
            registration = "EventStore__DomainServices__Registrations__*|counter|v1__"
            environment.update({registration + key: value for key, value in
                                {"AppId": "counter", "MethodName": "process", "TenantId": "*", "Domain": "counter", "Version": "v1"}.items()})
            if version in ("source", self.candidate):
                environment.update(Authentication__JwtBearer__Issuer="p1r-" + self.invocation,
                    Authentication__JwtBearer__Audience="counter", Authentication__JwtBearer__SigningKey=self.workload_key,
                    Authentication__Workload__Audience="counter", Authentication__Workload__AllowedCallers__0="eventstore",
                    Authentication__WorkloadIssuer__Workload=appid, EventStore__DomainService__AppId=appid)
            if project == "Domain" and write_alias is not None:
                environment["P1R_EVOLUTION_WRITE_ALIAS"] = write_alias
            else:
                environment.pop("P1R_EVOLUTION_WRITE_ALIAS", None)
            item = self.launch(["dotnet", root / project.lower() / "bin" / configuration / "net10.0" / (project + ".dll")], environment)
            self.wait(f"http://127.0.0.1:{app}/ready", item)
            sidecar = self.launch([self.daprd, "--app-id", appid, "--app-port", str(app), "--app-channel-address", "127.0.0.1",
                "--dapr-http-port", str(http), "--dapr-grpc-port", str(grpc), "--dapr-internal-grpc-port", str(internal),
                "--metrics-port", str(metrics), "--profile-port", str(profile), "--resources-path", resources, "--config", config,
                "--placement-host-address", f"127.0.0.1:{self.placement_port}", "--scheduler-host-address", f"127.0.0.1:{self.scheduler_port}",
                "--log-level", "warn"], environment)
            self.wait(f"http://127.0.0.1:{http}/v1.0/healthz/outbound", sidecar)
            if project == "Host":
                self.host_port, self.sidecar_port = app, http
            else:
                self.domain_port = app
        time.sleep(1)
        return [item["ownership"].root for item in self.active]

    def probe(self, version, arguments, checks, prefix="probe:", timeout=180):
        role = "source" if version == "source" else "candidate" if version == self.candidate else "comparison-" + version
        root = self.evidence[role]
        configuration = "Debug" if version == "source" else "Release"
        data = self.run(["dotnet", root / "probe" / "bin" / configuration / "net10.0/Probe.dll", *arguments], check=False, timeout=timeout)
        value = json.loads(data)
        checks.import_probe(value, prefix, self.commands[-1]["id"])
        return value["observation"]

    def redis_command(self, container, *arguments, input_bytes=None):
        require(container in self.containers and self.containers[container]["image"] == REDIS, "unowned Redis diagnostic")
        return self.run(["docker", "exec", *( ["-i"] if input_bytes is not None else [] ), container,
                         "redis-cli", "--raw", *arguments], input_bytes=input_bytes)

    def postgres_query(self, container, query, retain=True):
        require(container in self.containers and self.containers[container]["image"] == POSTGRES,
                "unowned PostgreSQL diagnostic")
        return self.run(["docker", "exec", "-i", container, "psql", "-U", "postgres", "-d", "eventstore",
                         "-At", "-v", "ON_ERROR_STOP=1", "-f", "-"], input_bytes=query.encode(), retain=retain)

    def postgres_state(self, container):
        data = self.postgres_query(container, PRECONDITION_QUERY)
        return {key: postgres_value(value) for key, value in json.loads(data)}

    def postgres_audits(self, container):
        data = self.postgres_query(container, "SELECT coalesce(json_agg(json_build_array(key,value) ORDER BY key)::text,'[]') "
                                   "FROM state WHERE key LIKE 'eventstore||p1r-qualification-audit-%'")
        records = []
        for key, value in json.loads(data):
            try:
                record = json.loads(postgres_value(value))
            except (ValueError, UnicodeError):
                record = None
            records.append({"key": key, "record": {name.lower(): item for name, item in record.items()}
                            if isinstance(record, dict) else {}})
        return records

    def postgres_delete(self, container, key):
        encoded = key.encode().hex()
        self.postgres_query(container, f"DELETE FROM state WHERE key=convert_from(decode('{encoded}','hex'),'UTF8');")

    def postgres_write(self, container, key, value):
        encoded_key = key.encode().hex()
        encoded_value = canonical(value).hex()
        self.postgres_query(container, "UPDATE state SET value=convert_from(decode('" + encoded_value
                            + "','hex'),'UTF8')::jsonb WHERE key=convert_from(decode('" + encoded_key + "','hex'),'UTF8');")

    def raw_state(self, container):
        if self.inputs["operational_profile"]["backend"] == "state.postgresql":
            return self.postgres_state(container)
        values = json.loads(self.redis_command(container, "EVAL", REDIS_DIAGNOSTIC, "0"))
        return {key: data.encode() for key, data in values}

    def inventory(self, container=None):
        container = container or self.redis
        return json.loads(self.run([str(Path(sys.executable).resolve()), "-I", ROOT / "tools/p1r-published-executor.py", "inventory",
                                   "--container", container, "--invocation", self.invocation,
                                   "--backend", self.inputs["operational_profile"]["backend"]]))

    def capture_preconditions(self, rows=None, complete_seed=False):
        rows = self.inventory() if rows is None else rows
        inventory_command = self.commands[-1]["id"] if getattr(self, "commands", []) else None
        raw = self.raw_state(self.redis)
        provider_command = self.commands[-1]["id"] if getattr(self, "commands", []) else None
        if complete_seed:
            domain_keys = {row["key"] for row in rows if row["kind"] != "bookkeeping"}
            self.last_seed["captured_input"] = {"inventory": rows, "database": self.redis, "invocation": getattr(self, "invocation", None),
                "commands": {"inventory": inventory_command, "provider": provider_command},
                "state": {key: {"base64": base64.b64encode(data).decode(), "sha256": digest(data)}
                          for key, data in raw.items() if key in domain_keys}}
            validate_seed_proofs(rows, raw)
        record = persisted_preconditions(rows, raw)
        record.update(database=self.redis, invocation=getattr(self, "invocation", None),
                      commands={"inventory": inventory_command, "provider": provider_command})
        return record, raw

    def mutate(self, kind, variant=None):
        """Bounded diagnostics modify only the stopped writer's invocation-owned test fixture."""
        require(not self.active, "fixture diagnostic refused while a writer is active")
        values = self.raw_state(self.redis)
        if kind == "retained":
            require(variant in (None, "mismatched-proof"), "unknown retained fixture control")
            original = retained_metadata(values, 1)
            require(variant != "mismatched-proof" or original.get("sourceProof") is not None,
                    "mismatched-proof control requires an existing proof")
        for key, data in values.items():
            if "tenant-a" not in key:
                continue
            value = json.loads(data)
            changed = False
            if key.endswith(":metadata") and kind in ("retained", "invalid-floor"):
                value["retainedFloor"] = 5 if kind == "retained" else 0
                if kind == "retained" and variant != "mismatched-proof" and value.get("sourceProof") is not None:
                    value["sourceProof"]["retainedFloor"] = 5
                changed = True
            if ":events:" in key:
                sequence = int(key.rsplit(":", 1)[1])
                remove = (kind == "retained" and sequence < 5) or (kind == "missing" and sequence == (7 if variant == "interior" else 12))
                if remove:
                    self.postgres_delete(self.redis, key)
                    continue
                if sequence == 7 and kind in ("unreadable", "protected", "unknown-type", "unknown-version"):
                    if kind == "unreadable":
                        value = "unreadable-fixture"
                    else:
                        field, replacement = {"protected": ("serializationFormat", "json+pdenc-v1"),
                                              "unknown-type": ("eventTypeName", "P1R.UnknownEvent"),
                                              "unknown-version": ("metadataVersion", 987)}[kind]
                        value[field] = replacement
                    changed = True
            if key.endswith(":snapshot") and kind == "uncovered":
                if variant == "absent":
                    self.postgres_delete(self.redis, key)
                    continue
                value["sequenceNumber"], value["state"] = 2, {"count": 2}
                changed = True
            if key.endswith(":snapshot") and kind == "snapshot-nine":
                value["sequenceNumber"] = 9
                # Historical automatic capture serialized DomainServiceCurrentState.
                # Adding a count leaves that ambiguous replay DTO intact. This
                # stopped-writer Counter fixture needs the exact folded state;
                # envelope, identity and protection fields retain their values.
                value["state"] = {"count": 9}
                changed = True
            if changed:
                self.postgres_write(self.redis, key, value)
        if kind == "retained":
            after = self.raw_state(self.redis)
            expected = dict(values)
            for key in list(expected):
                if "tenant-a" in key and ":events:" in key and int(key.rsplit(":", 1)[1]) < 5:
                    del expected[key]
                elif "tenant-a" in key and key.endswith(":metadata"):
                    metadata = json.loads(expected[key])
                    metadata["retainedFloor"] = 5
                    if variant != "mismatched-proof" and metadata.get("sourceProof") is not None:
                        metadata["sourceProof"]["retainedFloor"] = 5
                    expected[key] = canonical(metadata)
            require(after == expected, "retained fixture changed bytes outside retention coordinates")
            if variant != "mismatched-proof":
                retained_metadata(after, 5)
            self.last_retained_fixture = {"variant": variant or "coherent", "before_metadata": original,
                "after_metadata": next(json.loads(data) for key, data in after.items()
                                       if "tenant-a" in key and key.endswith(":metadata")),
                "before_hashes": {key: digest(data) for key, data in values.items()},
                "after_hashes": {key: digest(data) for key, data in after.items()}}

    def actor(self, version, tenant, expected, kind, checks, prefix):
        return self.probe(version, ["actor", f"http://127.0.0.1:{self.sidecar_port}", tenant, "fixture", str(expected), kind], checks, prefix)

    def seed(self, version, interval, checks, write_alias=None):
        require(self.candidate in (CANDIDATE, *qualification.FOLDED_SNAPSHOT_VERSIONS)
                and version in (self.candidate, "source", *qualification.COMPARISON_VERSIONS),
                "snapshot fixture candidate or writer is not explicitly enabled")
        start = len(checks.checks)
        self.last_snapshot_creation, self.last_retained_fixture = None, None
        self.last_seed = {"writer": version, "interval": interval, "tenants": {}, "valid": False}
        try:
            rows = self._seed(version, interval, checks, write_alias)
            record, raw = self.capture_preconditions(rows, complete_seed=True)
            self.last_seed["persisted"] = record
            primary, snapshot, events = runtime.stream(rows, "tenant-a")
            secondary, _, second_events = runtime.stream(rows, "tenant-b")
            checks.check("seed-primary-coordinates-exact", primary is not None and primary["sequence"] == 12
                         and primary["floor"] == 1 and sorted(events) == list(range(1, 13)))
            checks.check("seed-secondary-coordinates-exact", secondary is not None and secondary["sequence"] == 3
                         and secondary["floor"] == 1 and sorted(second_events) == [1, 2, 3])
            checks.check("seed-snapshot-coordinate-exact", (None if snapshot is None else snapshot["sequence"]) == (9 if interval == 10 else None))
            primary_snapshots = [row for row in rows if row["tenant"] == "tenant-a" and row["kind"] == "snapshot"]
            secondary_snapshots = [row for row in rows if row["tenant"] == "tenant-b" and row["kind"] == "snapshot"]
            checks.check("seed-snapshot-cardinality-exact", len(primary_snapshots) == (1 if interval == 10 else 0)
                         and len(secondary_snapshots) == 0)
        except Exception as error:
            # Preserve partial inventories even when startup/probe/byte binding fails.
            self.last_seed["error"] = {"type": type(error).__name__, "reason": str(error)}
            checks.check("seed-execution-completed", False)
            self.stop_nodes()
            try:
                self.last_seed["inventory"] = self.inventory()
            except Exception as inventory_error:
                self.last_seed["inventory_error"] = str(inventory_error)
        self.last_seed["checks"] = checks.checks[start:]
        self.last_seed["unattempted_tenants"] = [tenant for tenant in ("tenant-a", "tenant-b") if tenant not in self.last_seed["tenants"]]
        self.last_seed["valid"] = bool(self.last_seed["checks"]) and all(row["passed"] for row in self.last_seed["checks"])
        if not self.last_seed["valid"]:
            raise SeedFailure(self.last_seed)
        return rows

    def seed_probe(self, version, arguments, checks, prefix, tenant, stage):
        try:
            result = self.probe(version, arguments, checks, prefix)
        except Exception as error:
            checks.check(prefix + "execution-completed", False)
            result = {"error": type(error).__name__, "reason": str(error)}
        self.last_seed["tenants"].setdefault(tenant, {})[stage] = result
        return result

    def _seed(self, version, interval, checks, write_alias=None):
        require(self.candidate in (CANDIDATE, *qualification.FOLDED_SNAPSHOT_VERSIONS)
                and version in (self.candidate, "source", *qualification.COMPARISON_VERSIONS),
                "snapshot fixture candidate or writer is not explicitly enabled")
        genuine = interval == 10 and qualification.is_successor_selection(
            {"candidate": {"version": self.candidate if version == "source" else version}})
        if genuine:
            require(self.candidate in ("3.121.0", "3.122.1", "3.125.0", "3.125.1", "3.126.0")
                    and version in (self.candidate, "source"), "successor snapshot fixture is not explicitly enabled")
        self.stop_nodes()
        self.postgres_query(self.redis, "DO $$ BEGIN IF to_regclass('public.state') IS NOT NULL THEN TRUNCATE TABLE state; END IF; END $$;")
        self.start_nodes(version, interval=interval, write_alias=write_alias)
        self.last_snapshot_creation = None
        if genuine:
            registration = self.http("GET", f"http://127.0.0.1:{self.domain_port}/ready")["snapshot_qualification"]
            checks.check("snapshot-fixture-enrolled", registration.get("registered") is True and registration.get("test_only") is True)
            checks.check("snapshot-state-roundtrip-parity", registration.get("state_roundtrip") is True
                         and registration.get("original_count") == registration.get("roundtrip_count") == 9)
            checks.check("snapshot-canonical-event-parity", registration.get("canonical_event_parity") is True
                         and registration.get("canonical_event_sha256") == digest(b"{}"))
            checks.check("snapshot-detached-apply-parity", registration.get("detached_apply") is True and registration.get("applied_count") == 10)
            if not all(row["passed"] for row in checks.checks):
                return self.finish_seed(version, interval, checks, genuine)
            nine = self.seed_probe(version, ["seed", f"http://127.0.0.1:{self.sidecar_port}", "tenant-a", "fixture", "9"], checks, "tenant-a:prefix:", "tenant-a", "prefix")
            checks.check("snapshot-nine-events-committed", nine.get("committed") == 9 and nine.get("sequence") == 9)
            if nine.get("committed") != 9 or nine.get("sequence") != 9:
                return self.finish_seed(version, interval, checks, genuine)
            created = self.probe(version, ["manual-snapshot", f"http://127.0.0.1:{self.sidecar_port}", "tenant-a", "fixture"], checks, "snapshot:")
            checks.check("qualified-snapshot-created-by-published-actor", created.get("outcome") == "Created" and created.get("sequence") == 9)
            self.stop_nodes()
            raw = self.raw_state(self.redis)
            snapshots = [json.loads(data) for key, data in raw.items() if "tenant-a" in key and key.endswith(":snapshot")]
            snapshot = snapshots[0] if len(snapshots) == 1 else {}
            checks.check("qualified-snapshot-envelope-version-one", len(snapshots) == 1 and snapshot.get("snapshotEnvelopeVersion") == 1)
            checks.check("qualified-snapshot-persisted-nine", snapshot.get("sequenceNumber") == 9 and snapshot.get("state") == {"count": 9})
            before_tail = self.inventory()
            metadata, _, events = runtime.stream(before_tail, "tenant-a")
            checks.check("qualified-snapshot-prefix-coordinate-exact", metadata is not None
                         and metadata["sequence"] == 9 and sorted(events) == list(range(1, 10)))
            if not all(row["passed"] for row in checks.checks):
                self.start_nodes(version, interval=interval, write_alias=write_alias)
                return self.finish_seed(version, interval, checks, genuine)
            self.start_nodes(version, interval=interval, write_alias=write_alias)
            tail = self.seed_probe(version, ["seed", f"http://127.0.0.1:{self.sidecar_port}", "tenant-a", "fixture", "3"], checks, "tenant-a:tail:", "tenant-a", "tail")
            checks.check("tenant-a:committed-count", nine.get("committed") == 9 and tail.get("committed") == 3 and tail.get("sequence") == 12)
            self.last_snapshot_creation = {"registration": registration, "manual": created,
                "persisted_snapshot": snapshot, "before_tail": before_tail, "tail": tail}
        else:
            result = self.seed_probe(version, ["seed", f"http://127.0.0.1:{self.sidecar_port}", "tenant-a", "fixture", "12"], checks, "tenant-a:", "tenant-a", "seed")
            checks.check("tenant-a:committed-count", result.get("committed") == 12)
        rows = self.finish_seed(version, interval, checks, genuine)
        if genuine:
            metadata, _, events = runtime.stream(rows, "tenant-a")
            checks.check("qualified-snapshot-tail-coordinate-exact", metadata is not None
                         and metadata["sequence"] == 12 and sorted(events) == list(range(1, 13)))
            originals = {row["key"]: row["sha256"] for row in before_tail if row["kind"] in ("event", "snapshot")}
            after_tail = {row["key"]: row["sha256"] for row in rows}
            checks.check("qualified-snapshot-and-prefix-bytes-preserved", all(after_tail.get(key) == value for key, value in originals.items()))
            self.last_snapshot_creation["after_tail"] = rows
        return rows

    def finish_seed(self, version, interval, checks, genuine):
        second = self.seed_probe(version, ["seed", f"http://127.0.0.1:{self.sidecar_port}", "tenant-b", "fixture", "3"], checks, "tenant-b:", "tenant-b", "seed")
        checks.check("tenant-b:committed-count", second.get("committed") == 3)
        self.stop_nodes()
        if interval == 10 and not genuine and all(row["passed"] for row in checks.checks):
            # Preserve the historical diagnostic only after successful seeds.
            self.mutate("snapshot-nine")
        rows = self.inventory()
        checks.check("seed-persisted-fifteen-events", len([row for row in rows if row["kind"] == "event"]) == 15)
        return rows

    def prepare_live_case(self, lane, identifier, checks):
        parts = identifier.split("-to-")
        writer, tail = parts
        reader, _, variant = tail.partition("-")
        interval = 10 if lane in ("snapshot-tail", "retained-covered", "metadata-write", "retained-uncovered") else 1000
        self.seed(writer, interval, checks)
        seed_snapshot = getattr(self, "last_snapshot_creation", None)
        if lane in ("retained-covered", "metadata-write", "retained-uncovered"):
            self.mutate("retained")
        if lane == "retained-uncovered":
            self.mutate("uncovered", variant)
        if lane == "missing-event":
            self.mutate("missing", variant)
        before = self.inventory()
        preconditions, _ = self.capture_preconditions(before)
        return {"before": before, "seed_snapshot": seed_snapshot,
                "seed_preconditions": self.last_seed,
                "persisted_preconditions": preconditions,
                "retained_fixture": getattr(self, "last_retained_fixture", None)}

    def live_case(self, lane, identifier, checks, prepared=None):
        self.live_progress = dict(prepared or {})
        try:
            return self._live_case(lane, identifier, checks, prepared)
        except Exception as error:
            error.observations = dict(self.live_progress, **getattr(error, "observations", {}))
            raise

    def _live_case(self, lane, identifier, checks, prepared=None):
        writer, tail = identifier.split("-to-")
        reader, _, variant = tail.partition("-")
        interval = 10 if lane in ("snapshot-tail", "retained-covered", "metadata-write", "retained-uncovered") else 1000
        prepared = self.prepare_live_case(lane, identifier, checks) if prepared is None else prepared
        self.live_progress.update(prepared)
        before, seed_snapshot = prepared["before"], prepared["seed_snapshot"]
        self.start_nodes(reader, interval=interval)
        negative = lane in ("retained-uncovered", "missing-event")
        result = self.actor(reader, "tenant-a", 12, "AssertCounter", checks, "primary:")
        self.live_progress["primary"] = result
        checks.check("primary-expected-hydration", result.get("accepted") is (not negative))
        checks.check("hydration-no-events", result.get("event_count") == 0)
        checks.check("primary-sequence-twelve", result.get("sequence") == 12)
        second = self.actor(reader, "tenant-b", 3, "AssertCounter", checks, "secondary:")
        self.live_progress["secondary"] = second
        checks.check("second-tenant-hydrates", second.get("accepted") is True)
        append, replay, appended = None, None, None
        if lane == "metadata-write":
            append = self.actor(reader, "tenant-a", 12, "IncrementCounter", checks, "append:")
            self.live_progress["append"] = append
            checks.check("append-effect-accepted", append.get("accepted") is True and append.get("event_count") == 1)
        self.stop_nodes()
        if lane == "metadata-write" and append.get("accepted") is True and append.get("event_count") == 1:
            appended = self.inventory()
            self.live_progress["appended_inventory"] = appended
            self.start_nodes(reader, interval=interval)
            replay = self.actor(reader, "tenant-a", 13, "AssertCounter", checks, "restart:")
            self.live_progress["replay"] = replay
            checks.check("supported-append-restart-rehydrates-thirteen", replay.get("accepted") is True and replay.get("sequence") == 13)
            restarted_second = self.actor(reader, "tenant-b", 3, "AssertCounter", checks, "restart-secondary:")
            checks.check("supported-append-restart-second-tenant-hydrates", restarted_second.get("accepted") is True and restarted_second.get("sequence") == 3)
            self.stop_nodes()
        after = self.inventory()
        self.live_progress["after"] = after
        before_domain = [row for row in before if row["kind"] != "bookkeeping"]
        after_domain = [row for row in after if row["kind"] != "bookkeeping"]
        prior = {row["key"]: row["sha256"] for row in before if row["kind"] in ("event", "snapshot")}
        observed = {row["key"]: row["sha256"] for row in after}
        checks.check("prior-event-and-snapshot-bytes-preserved", all(observed.get(key) == value for key, value in prior.items()))
        checks.check("second-tenant-state-preserved", runtime.domain(before, "tenant-b") == runtime.domain(after, "tenant-b"))
        if lane == "metadata-write":
            metadata, snapshot, events = runtime.stream(after, "tenant-a")
            checks.check("append-thirteen", metadata is not None and metadata["sequence"] == 13)
            checks.check("retained-floor-five-preserved", metadata is not None and metadata["floor"] == 5)
        else:
            checks.check("hydration-domain-inventory-unchanged", before_domain == after_domain)
        metadata, snapshot, events = runtime.stream(after, "tenant-a")
        expected_head = 13 if append is not None and append.get("accepted") is True and append.get("event_count") == 1 else 12
        expected_sequences = list(range(5 if lane in ("retained-covered", "metadata-write", "retained-uncovered") else 1, expected_head + 1))
        if lane == "missing-event":
            expected_sequences.remove(7 if variant == "interior" else 12)
        expected_snapshot = (None if variant == "absent" else 2) if lane == "retained-uncovered" else 9 if interval == 10 else None
        checks.check("persisted-current-sequence-exact", metadata is not None and metadata["sequence"] == expected_head)
        checks.check("persisted-event-sequence-set-exact", sorted(events) == expected_sequences)
        checks.check("persisted-snapshot-coordinate-exact", (None if snapshot is None else snapshot["sequence"]) == expected_snapshot)
        disposition = "compatible" if all(check["passed"] for check in checks.checks) else "incompatible"
        return dict(prepared, after=after, primary=result, secondary=second,
                append=append, replay=replay, appended_inventory=appended,
                expected_coordinates={"head": expected_head, "events": expected_sequences, "snapshot": expected_snapshot}), \
            "refusal" if result.get("accepted") is False else "effect", disposition

    def metadata_case(self, lane, identifier, checks):
        version, style, _, floor = identifier.split("-")
        value = {"CurrentSequence": 12, "LastModified": "2026-01-01T00:00:00Z", "ETag": "fixture-etag"}
        if floor != "None":
            value["RetainedFloor"] = int(floor)
        if style == "web":
            value = {name[0].lower() + name[1:]: item for name, item in value.items()}
        path = self.scratch / (uuid.uuid4().hex + ".json")
        path.write_bytes(canonical(value))
        target = self.scratch / (uuid.uuid4().hex + ".json")
        observed = self.probe(version, ["metadata", str(path), str(target), style], checks)
        expected = 1 if floor == "None" else int(floor)
        checks.check("retained-floor-read", observed.get("floor") == expected)
        return {"input_sha256": digest(regular(path)), "output_sha256": digest(regular(target)), "metadata": observed}, "effect", "compatible" if observed.get("floor") == expected else "incompatible"

    def wire_case(self, lane, identifier, checks):
        fields = identifier.split("-")
        writer, reader = fields[:2]
        format_name = "json" if lane == "projection-wire" else fields[2]
        shape = "dual" if lane == "projection-wire" else fields[3]
        if lane == "query-wire":
            value = {"tenantId": "tenant-a", "domain": "counter", "aggregateId": "fixture", "queryType": "FixtureQuery",
                     "payload": "e30=", "correlationId": "fixture-correlation", "userId": "fixture-user", "entityId": "fixture",
                     "isGlobalAdmin": False, "paging": None}
            if shape == "dual":
                value.update(originalActorId="original-fixture", authenticatedWorkloadId="workload-fixture", isDelegated=True,
                             delegationId="delegation-fixture", scopes=["counter.read"], audience=["audience-fixture"],
                             identityAdmissionProof="bounded-wire-fixture-proof")
        else:
            value = {"sequenceNumber": 12,
                     "globalPosition": 987, "eventTypeName": "P1R.Counter.CounterIncremented", "payload": "e30=",
                     "serializationFormat": "json", "timestamp": "2026-01-01T00:00:00Z", "messageId": "fixture-message",
                     "correlationId": "fixture-correlation", "userId": "fixture-user"}
        original, intermediate, target = [self.scratch / (uuid.uuid4().hex + ".wire") for _ in range(3)]
        original.write_bytes(canonical(value))
        kind = "query" if lane == "query-wire" else "projection"
        first = self.probe(writer, ["wire", kind, "json" if format_name == "json" else "to-xml", str(original), str(intermediate)], checks, "writer:")
        if first.get("handling") != "executed":
            return {"writer": first}, "refusal", "incompatible"
        second = self.probe(reader, ["wire", kind, "json" if format_name == "json" else "xml", str(intermediate), str(target)], checks, "reader:")
        if second.get("handling") != "executed":
            return {"writer": first, "reader": second}, "refusal", "incompatible"
        observed = {name[0].lower() + name[1:]: item for name, item in second["fields"].items()}
        for name, expected in value.items():
            actual = observed.get(name)
            if name == "timestamp" and isinstance(actual, str):
                actual = datetime.datetime.fromisoformat(actual.replace("Z", "+00:00")).isoformat()
                expected = datetime.datetime.fromisoformat(expected.replace("Z", "+00:00")).isoformat()
            checks.check("preserved:" + name, actual == expected)
        return {"writer": first, "reader": second}, "effect", "compatible" if all(row["passed"] for row in checks.checks) else "incompatible"

    def boundary_case(self, lane, identifier, checks):
        """Run only the incapable endpoint's admission check before any protected route."""
        version = qualification.refusal_endpoint(lane, identifier)
        admission = self.probe(version, ["admit", lane, identifier], checks, "admission:")
        checks.check("bounded-route-refused-before-access", admission.get("handling") == "refused"
                     and admission.get("boundary") == "before-protected-route"
                     and admission.get("protected_access") is False
                     and admission.get("effect") is False and admission.get("write") is False)
        return {"unsupported": True, "admission": admission}, "refusal" if admission.get("handling") == "refused" else "error", "incompatible"

    def case(self, lane, identifier, action, operational):
        checks, start_index, started = Measurements(), len(self.commands), stamp()
        start_configuration = len(getattr(self, "configurations", []))
        interrupted = False
        expected = "refusal" if lane in ("retained-uncovered", "missing-event", "invalid-evidence") else "effect"
        if lane == "mixed-api" and identifier.endswith(("stale-fence", "unauthorized-effect")):
            expected = "refusal"
        if lane == "reminder-recovery" and identifier == "stale-generation-refusal":
            expected = "refusal"
        if lane == "logical-event-evolution" and identifier == "unknown-version-refusal":
            expected = "refusal"
        if lane == "checkout" and identifier in ("retained-uncovered", "missing-event", "invalid-evidence"):
            expected = "refusal"
        if getattr(self, "support_rules", {}).get((lane, identifier)) == "refused":
            expected = "refusal"
        observed, outcome, disposition = {}, "error", "unverified"
        source_guard_phase = "before-action"
        try:
            self.check_sources()
            source_guard_phase = "action"
            observed, outcome, disposition = action(checks)
            source_guard_phase = "after-action"
            self.check_sources()
        except (Exception, KeyboardInterrupt) as error:
            checks.check("execution-completed", False)
            observed = dict(observed, **getattr(error, "observations", {}), error=type(error).__name__, reason=str(error))
            if source_guard_phase != "action":
                observed["source_guard_failure"] = source_guard_phase
            outcome = "error"
            disposition = "unverified"
            self.errors.append(lane + "/" + identifier + ": " + str(error))
            if isinstance(error, KeyboardInterrupt):
                interrupted = True
                self.errors.append("interrupted")
        finally:
            if operational:
                try:
                    self.stop_nodes()
                except (Exception, KeyboardInterrupt) as error:
                    checks.check("case-cleanup-completed", False)
                    self.errors.append("case cleanup: " + str(error))
        if len(self.commands) == start_index:
            self.run([sys.executable, "-I", "-c", "import sys; print(sys.argv[1]); sys.exit(2)",
                      canonical({"lane": lane, "case": identifier, "observation": observed}).decode()], check=False)
        inventories = observed if isinstance(observed, dict) and "before" in observed and "after" in observed else None
        before = digest(canonical([row for row in inventories["before"] if row["kind"] != "bookkeeping"])) if inventories else digest(canonical(observed))
        after = digest(canonical([row for row in inventories["after"] if row["kind"] != "bookkeeping"])) if inventories else before
        row = {"id": identifier, "operation": "supported", "expected": expected, "outcome": outcome,
               "disposition": disposition, "inventory": {"before_sha256": before, "after_sha256": after},
               "checks": checks.checks, "assertions": checks.counter}
        if observed.get("unsupported") is True:
            row.update(operation="unsupported", expected="refusal", disposition="incompatible")
        if not hasattr(self, "witness_sources"):
            self.witness_sources = {}
        for witness in checks.witnesses:
            if witness["kind"] == "predicate" and witness["source"] not in self.witness_sources:
                data = regular(Path(witness["source"]))
                require(digest(data) == witness["source_sha256"], "predicate source changed during execution")
                self.witness_sources[witness["source"]] = {"path": witness["source"], "sha256": digest(data), "content": data.decode()}
        evidence = {"id": identifier, "commands": self.commands[start_index:], "observations": observed,
                    "configurations": getattr(self, "configurations", [])[start_configuration:],
                    "check_witnesses": checks.witnesses}
        if self.candidate in ("3.121.0", "3.122.1", "3.125.0", "3.125.1", "3.126.0") and getattr(self, "last_snapshot_creation", None) is not None:
            path = ROOT / "tools/p1r-published-consumers/FixtureSnapshotQualification.cs"
            data = regular(path)
            evidence["snapshot_fixture_source"] = {"path": str(path), "sha256": digest(data), "content": data.decode()}
        if interrupted:
            write_json(self.output / "interrupted" / (uuid.uuid4().hex + ".json"),
                       {"lane": lane, "case": row, "execution_evidence": evidence,
                        "predicate_sources": list(self.witness_sources.values())})
            raise KeyboardInterrupt("published invocation interrupted; partial case retained")
        return row, evidence

    def lane(self, name, action, operational=False):
        started = stamp()
        cases, evidence = [], []
        for identifier in qualification.executed_case_inventory(self.inputs)[name]:
            if getattr(self, "support_rules", {}).get((name, identifier)) == "refused":
                selected = lambda checks, identifier=identifier: self.boundary_case(name, identifier, checks)
            else:
                selected = lambda checks, identifier=identifier: action(identifier, checks)
            row, observed = self.case(name, identifier, selected, operational)
            cases.append(row)
            evidence.append(observed)
        identities = {"configuration": "Release", "packages": [{"id": package["id"], "sha256": package["archive_sha256"]}
                       for selection in [self.inputs["candidate"], *self.inputs["comparisons"]] for package in selection["packages"]],
                       "loaded_assemblies": list({(row["name"], row["sha256"], row["path"]): row
                                                  for version in self.versions for row in self.identities[version]}.values())}
        execution_evidence = {"mechanism": MECHANISM, "executor_source_sha256": self.result["executor_source_sha256"],
                              "executor_source": self.source_binding, "cases": evidence,
                              "predicate_sources": [self.witness_sources[path] for path in sorted({
                                  witness["source"] for case in evidence for witness in case["check_witnesses"] if witness["kind"] == "predicate"})]}
        if name == "checkout" and "runtime_source" in self.source_binding:
            execution_evidence["source_evaluations"] = getattr(self, "source_evaluations", [])
        if name == "checkout" and "candidate_source" in self.source_binding and "source" in self.evidence:
            source_manifest = self.evidence["source"] / "source-evidence.json"
            execution_evidence["source_consumer"] = {"path": str(source_manifest), "sha256": digest(regular(source_manifest)),
                                                      "content": regular(source_manifest).decode()}
        counter = {key: sum(case["assertions"][key] for case in cases) for key in ("attempted", "passed", "failed")}
        passed = (counter["attempted"] > 0 and counter["failed"] == 0
                  and all(case["expected"] == case["outcome"] and (case["outcome"] != "refusal" or
                          case["inventory"]["before_sha256"] == case["inventory"]["after_sha256"]) for case in cases))
        incompatible = any(case["disposition"] == "incompatible" for case in cases)
        receipt = {"schema": qualification.LANE_RECEIPT_SCHEMA, "id": uuid.uuid4().hex, "lane": name,
                   "scope": "operational" if operational else "published-package", "fixture": self.fixture,
                   "inputs_sha256": self.inputs_sha256, "instrumentation": MECHANISM,
                   "profile": self.inputs["operational_profile"] if operational else None,
                   "argv": self.invocation_argv,
                   "cwd": str(ROOT), "started_utc": started, "finished_utc": stamp(), "exit_code": 0,
                   "output_sha256": digest(canonical(execution_evidence)), "execution_evidence": execution_evidence,
                   "identities": identities, "cases": cases, "assertions": counter,
                   "execution": "passed" if passed else "failed",
                   "compatibility": "incompatible" if incompatible else "compatible" if passed else "unverified"}
        if hasattr(self, "storage"):
            receipt["execution_evidence"] = self.retain_packet(receipt["execution_evidence"])
            receipt["output_sha256"] = digest(canonical(receipt["execution_evidence"]))
        qualification.lane_outcome(receipt)
        path = self.output / "receipts" / (receipt["id"] + ".json")
        write_json(path, self.retain_packet(receipt))
        self.receipts.append(str(path))
        self.save()
        print(json.dumps({"lane": name, "execution": receipt["execution"], "compatibility": receipt["compatibility"],
                          "assertions": receipt["assertions"]}), flush=True)
        return receipt

    def prepare_invalid_case(self, identifier, checks):
        version, _, mutation = identifier.partition("-")
        self.seed(version, 1000, checks)
        self.mutate(mutation)
        before = self.inventory()
        preconditions, _ = self.capture_preconditions(before)
        return {"before": before, "seed_preconditions": self.last_seed,
                "persisted_preconditions": preconditions}

    def invalid_case(self, identifier, checks, prepared=None):
        version, _, mutation = identifier.partition("-")
        prepared = self.prepare_invalid_case(identifier, checks) if prepared is None else prepared
        before = prepared["before"]
        self.start_nodes(version)
        observed = self.actor(version, "tenant-a", 12, "AssertCounter", checks, "primary:")
        checks.check("invalid-evidence-refused", observed.get("accepted") is False)
        checks.check("no-domain-events", observed.get("event_count") == 0)
        self.stop_nodes()
        after = self.inventory()
        checks.check("invalid-evidence-domain-unchanged", [row for row in before if row["kind"] != "bookkeeping"]
                     == [row for row in after if row["kind"] != "bookkeeping"])
        checks.check("second-tenant-preserved", runtime.domain(before, "tenant-b") == runtime.domain(after, "tenant-b"))
        return dict(prepared, after=after, actor=observed), "refusal" if observed.get("accepted") is False else "effect", \
            "compatible" if all(row["passed"] for row in checks.checks) else "incompatible"

    def provenance_case(self, identifier, checks):
        role = "candidate" if identifier == self.candidate else "comparison-" + identifier
        selection = qualification.role_selection(self.inputs, role)
        root = self.evidence[role]
        observed = self.probe(identifier, ["identity"], checks)
        for package in selection["packages"]:
            lower = package["id"].lower()
            archive = root / "packages" / lower / identifier / (lower + "." + identifier + ".nupkg")
            checks.check(package["id"] + ":archive-sha256", digest(regular(archive)) == package["archive_sha256"])
            self.run(["dotnet", "nuget", "verify", "--all", archive, "--verbosity", "minimal"])
            checks.check(package["id"] + ":signature", qualification.REPOSITORY_SIGNATURE in self.command_output(self.commands[-1]))
        return observed, "effect", "compatible"

    def mixed_case(self, identifier, checks):
        if "-client-" in identifier:
            client, host = identifier.removesuffix("-host").split("-client-")
            self.seed(host, 1000, checks)
            before = self.inventory()
            self.start_nodes(host)
            observed = self.actor(client, "tenant-a", 12, "AssertCounter", checks, "mixed:")
            checks.check("mixed-client-hydrates", observed.get("accepted") is True)
            self.stop_nodes()
            after = self.inventory()
            checks.check("mixed-domain-preserved", [row for row in before if row["kind"] != "bookkeeping"]
                         == [row for row in after if row["kind"] != "bookkeeping"])
            return {"before": before, "after": after, "actor": observed}, "effect", "compatible" if observed.get("accepted") else "incompatible"
        version, _, operation = identifier.partition("-")
        inventory = self.probe(version, ["capabilities"], checks, "capability:")
        method = {"fenced-effect": "ProcessFencedCommandAsync", "stale-fence": "ProcessFencedCommandAsync",
                  "trusted-effect": "ProcessTrustedEffectAsync", "unauthorized-effect": "ProcessTrustedEffectAsync",
                  "retained-floor": "GetRetainedFloorAsync"}.get(operation)
        if method and method not in inventory["actor_methods"]:
            self.seed(version, 1000, checks)
            before = self.inventory()
            checks.check("historical-operation-refused-before-dispatch", True)
            after = self.inventory()
            checks.check("unsupported-operation-domain-preserved", before == after)
            return {"unsupported": True, "method": method, "inventory": inventory,
                    "before": before, "after": after}, "refusal", "incompatible"
        if operation == "cursor-scope":
            observed = self.probe(version, ["cursor-scope"], checks, "cursor:")
            unsupported = observed.get("handling") == "unsupported"
            return {"unsupported": unsupported, "cursor": observed}, "refusal" if unsupported else "effect", "incompatible" if unsupported else "compatible"
        self.seed(version, 1000, checks)
        before = self.inventory()
        self.start_nodes(version)
        if operation == "status":
            observed = []
            for retryable in STATUS_RETRYABILITY:
                label = "null" if retryable is None else str(retryable).lower()
                value = {"status": 2, "timestamp": "2026-01-01T00:00:00Z", "aggregateId": "fixture", "eventCount": 1,
                         "rejectionEventType": None, "failureReason": None, "timeoutDuration": None,
                         "messageId": "fixture-status-" + label, "correlationId": "fixture-correlation", "retryable": retryable,
                         "recoveryReasonCode": "fixture-code", "drainAttemptCount": 3, "domain": "counter", "committedEventSequence": 12}
                url = f"http://127.0.0.1:{self.host_port}/status/tenant-a/" + value["messageId"]
                self.http("POST", url, value)
                actual = self.http("GET", url)
                for name in ("messageId", "correlationId", "retryable", "recoveryReasonCode", "drainAttemptCount", "domain", "committedEventSequence"):
                    checks.check("status:" + label + ":" + name, name in actual and actual[name] == value[name])
                observed.append({"retryability_case": label, "input": value, "readback": actual})
            outcome = "effect"
        elif operation == "retained-floor":
            observed = self.probe(version, ["retained-floor", f"http://127.0.0.1:{self.sidecar_port}"], checks, "floor:")
            checks.check("authoritative-floor-one", observed.get("floor") == 1)
            outcome = "effect"
        else:
            observed = self.http("POST", f"http://127.0.0.1:{self.host_port}/qualification/{operation}", {})
            if operation in ("stale-fence", "unauthorized-effect"):
                checks.check("protected-operation-refused", observed.get("accepted") is False)
                checks.check("protected-refusal-preserves-sequence-twelve", observed.get("sequence") == 12)
                if operation == "stale-fence":
                    checks.check("stale-context-refused", observed.get("stale_refused") is True and observed.get("stale_denial") == "stale-fencing-token"
                                 and observed.get("stale_actor_method") == "ProcessFencedCommandAsync"
                                 and any(item.get("actual_exception_type") == "System.InvalidOperationException"
                                         and item.get("denial_message") == "The idempotency execution authority is no longer current."
                                         for item in observed.get("stale_diagnostic", [])))
                    checks.check("forged-proof-refused", observed.get("forged_refused") is True and observed.get("forged_denial") == "stale-or-invalid-fence"
                                 and observed.get("forged_actor_method") == "ProcessFencedCommandAsync"
                                 and any(item.get("actual_exception_type") == "System.InvalidOperationException"
                                         and item.get("denial_message") == "The idempotency execution fence is missing, stale, or invalid."
                                         for item in observed.get("forged_diagnostic", [])))
                else:
                    checks.check("unauthorized-proof-refused", observed.get("unauthorized_refused") is True and observed.get("denial") == "invalid-gateway-proof")
                checks.check("execution-completed", observed.get("unexpected") is not True)
                outcome = "error" if observed.get("unexpected") is True else "refusal" if observed.get("accepted") is False else "effect"
            else:
                checks.check("non-null-protected-effect-accepted", observed.get("accepted") is True)
                checks.check("protected-effect-persisted-sequence-thirteen", observed.get("sequence") == 13)
                if operation == "trusted-effect":
                    checks.check("trusted-inbox-replays", observed.get("replayed") is True)
                outcome = "effect"
        self.stop_nodes()
        after = self.inventory()
        checks.check("second-tenant-preserved", runtime.domain(before, "tenant-b") == runtime.domain(after, "tenant-b"))
        audits = None
        if version == self.candidate and operation in ("trusted-effect", "unauthorized-effect"):
            audits = self.postgres_audits(self.redis)
            expected = {"action": "submission" if operation == "trusted-effect" else "gateway-proof",
                        "tenant": "tenant-a", "workload": "p1r-fixture", "purpose": "published-qualification",
                        "disposition": "authorized" if operation == "trusted-effect" else "denied"}
            matches = [row for row in audits if all(row["record"].get(name) == value for name, value in expected.items())
                       and (isinstance(row["record"].get("effectid"), str) and bool(row["record"]["effectid"])
                            if operation == "trusted-effect" else row["record"].get("effectid") is None)]
            checks.check("candidate-trusted-effect-audit-persisted" if operation == "trusted-effect"
                         else "candidate-unauthorized-effect-audit-persisted", len(matches) == 1)
        if operation in ("stale-fence", "unauthorized-effect"):
            checks.check("refusal-domain-preserved", [row for row in before if row["kind"] != "bookkeeping"]
                         == [row for row in after if row["kind"] != "bookkeeping"])
        return {"before": before, "after": after, "operation": observed,
                **({"audit_records": audits} if audits is not None else {}),
                "authority_scope": "bounded Test fixture; no production identity or P2 acceptance"}, outcome, "compatible" if all(row["passed"] for row in checks.checks) else "incompatible"

    def recovery_command(self, commands, step, command, database=None, processes=None, input_data=None, output_data=None):
        if isinstance(command, int):
            command = self.commands[command - 1]
        row = {"id": len(commands) + 1, "step": step, **{key: command[key] for key in
               ("argv", "cwd", "started_utc", "finished_utc", "exit_code", "output_sha256")},
               "database": database, "processes": processes, "input_sha256": digest(input_data) if input_data else None,
               "input_bytes": len(input_data) if input_data else None}
        if output_data is not None:
            row["output_sha256"] = digest(output_data)
        commands.append(row)
        return row["id"]

    def postgres_backup(self, container):
        name = "p1r-" + uuid.uuid4().hex + ".dump"
        self.run(["docker", "exec", container, "pg_dump", "-U", "postgres", "-d", "eventstore", "-Fc", "-f", "/tmp/" + name])
        command_id = self.commands[-1]["id"]
        target = self.scratch / name
        self.run(["docker", "cp", container + ":/tmp/" + name, target])
        return target, command_id

    def postgres_restore(self, container, backup):
        self.run(["docker", "cp", backup, container + ":/tmp/p1r-restore.dump"])
        self.run(["docker", "exec", container, "pg_restore", "-U", "postgres", "-d", "eventstore",
                  "--clean", "--if-exists", "--no-owner", "/tmp/p1r-restore.dump"])

    def restore_case(self, identifier, checks):
        self.restore_progress = {}
        try:
            return self._restore_case(identifier, checks)
        except Exception as error:
            error.observations = dict(self.restore_progress, **getattr(error, "observations", {}))
            raise

    def _restore_case(self, identifier, checks):
        self.seed(self.candidate, 10, checks)
        seed_snapshot = getattr(self, "last_snapshot_creation", None)
        self.mutate("retained")
        commands, inventories = [], {}
        source_db = digest(self.redis.encode())
        source = self.inventory()
        index = self.recovery_command(commands, "inventory", self.commands[-1], source_db)
        inventories["source"] = {"command": index, "rows": source, "sha256": digest(canonical(source))}
        self.restore_progress.update(before=source, seed_snapshot=seed_snapshot)
        source_raw = self.raw_state(self.redis)
        source_metadata = retained_metadata(source_raw, 5)
        backup_path, backup_command_id = self.postgres_backup(self.redis)
        backup = regular(backup_path)
        backup_id = self.recovery_command(commands, "backup", backup_command_id, source_db, output_data=backup)
        restored, port, create_command_id = self.postgres_container("restored")
        restored_db = digest(restored.encode())
        self.recovery_command(commands, "create-database", create_command_id, restored_db)
        self.postgres_restore(restored, backup_path)
        self.recovery_command(commands, "restore", self.commands[-1], restored_db, input_data=backup)
        self.postgres_wait(restored)
        copied = self.inventory(restored)
        index = self.recovery_command(commands, "inventory", self.commands[-1], restored_db)
        inventories["restored"] = {"command": index, "rows": copied, "sha256": digest(canonical(copied))}
        self.restore_progress["restored_inventory"] = copied
        copied_raw = self.raw_state(restored)
        checks.check("backup-nonempty", bool(backup))
        checks.check("fresh-restore-full-inventory-identical", source == copied)
        source_hashes = {key: digest(data) for key, data in source_raw.items()}
        copied_hashes = {key: digest(data) for key, data in copied_raw.items()}
        checks.check("fresh-restore-original-bytes-and-proof-identical", source_hashes == copied_hashes)
        processes = self.start_nodes(self.candidate, restored, port, interval=1000)
        self.recovery_command(commands, "start-writer", self.commands[-1], restored_db, processes)
        before = self.actor(self.candidate, "tenant-a", 12, "AssertCounter", checks, "restored:")
        primary_command = self.commands[-1]
        primary_id = self.recovery_command(commands, "replay", primary_command, restored_db)
        self.restore_progress["primary"] = before
        checks.check("fresh-restore-hydrates-twelve", before.get("accepted") is True
                     and before.get("sequence") == 12 and before.get("event_count") == 0)
        append = self.actor(self.candidate, "tenant-a", 12, "IncrementCounter", checks, "append:")
        self.restore_progress["append"] = append
        append_command = self.commands[-1]
        append_id = self.recovery_command(commands, "append", append_command, restored_db)
        checks.check("fresh-restore-appends-thirteen", append.get("accepted") is True and append.get("event_count") == 1
                     and append.get("sequence") == 13)
        appended = self.inventory(restored)
        index = self.recovery_command(commands, "inventory", self.commands[-1], restored_db)
        inventories["appended"] = {"command": index, "rows": appended, "sha256": digest(canonical(appended))}
        self.restore_progress["appended_inventory"] = appended
        appended_metadata, _, appended_events = runtime.stream(appended, "tenant-a")
        checks.check("fresh-append-persisted-head-and-event-thirteen", appended_metadata is not None
                     and appended_metadata["sequence"] == 13 and appended_metadata["floor"] == 5
                     and sorted(appended_events) == list(range(5, 14)))
        self.stop_nodes()
        self.run([sys.executable, "-I", "-c",
                  "import sys,json;sys.path.insert(0,sys.argv[1]);import p1r_qualification as p;"
                  "values=json.loads(sys.argv[2]);sys.exit(1 if any(p.same_process(v,p.process_state(v['pid'])) for v in values) else 0)",
                  str(ROOT / "tools"), canonical(processes).decode()])
        self.recovery_command(commands, "stop-writer", self.commands[-1], restored_db)
        restarted_processes = self.start_nodes(self.candidate, restored, port, interval=1000)
        self.recovery_command(commands, "restart", self.commands[-1], restored_db, restarted_processes)
        checks.check("fresh-restart-processes-distinct", bool(processes) and bool(restarted_processes)
                     and not {(item["pid"], item["start_ticks"]) for item in processes}
                     & {(item["pid"], item["start_ticks"]) for item in restarted_processes})
        replay = self.actor(self.candidate, "tenant-a", 13, "AssertCounter", checks, "restart:")
        self.restore_progress["replay"] = replay
        replay_command = self.commands[-1]
        replay_id = self.recovery_command(commands, "replay", replay_command, restored_db)
        checks.check("fresh-restart-reconstructs-thirteen", replay.get("accepted") is True
                     and replay.get("sequence") == 13 and replay.get("event_count") == 0)
        second = self.actor(self.candidate, "tenant-b", 3, "AssertCounter", checks, "secondary:")
        secondary_command = self.commands[-1]
        secondary_id = self.recovery_command(commands, "replay", secondary_command, restored_db)
        self.restore_progress["secondary"] = second
        checks.check("restored-second-tenant-hydrates-three", second.get("accepted") is True
                     and second.get("sequence") == 3 and second.get("event_count") == 0)
        restarted = self.inventory(restored)
        index = self.recovery_command(commands, "inventory", self.commands[-1], restored_db)
        inventories["restarted"] = {"command": index, "rows": restarted, "sha256": digest(canonical(restarted))}
        self.restore_progress["after"] = restarted
        self.stop_nodes()
        metadata, snapshot, events = runtime.stream(restarted, "tenant-a")
        checks.check("restored-floor-five", metadata is not None and metadata["floor"] == 5)
        checks.check("restored-head-thirteen", metadata is not None and metadata["sequence"] == 13)
        checks.check("restored-snapshot-nine", snapshot is not None and snapshot["sequence"] == 9)
        checks.check("restored-event-sequence-set-exact", sorted(events) == list(range(5, 14)))
        prior = {row["key"]: row["sha256"] for row in source if row["kind"] in ("event", "snapshot")}
        checks.check("restore-prior-event-and-snapshot-bytes-preserved", all(
            {row["key"]: row["sha256"] for row in rows}.get(key) == sha256
            for rows in (copied, appended, restarted) for key, sha256 in prior.items()))
        checks.check("restore-second-tenant-unchanged", runtime.domain(source, "tenant-b") == runtime.domain(restarted, "tenant-b"))
        self.pending_restore = {"schema": runtime.RESTORE_SCHEMA_V2, "id": uuid.uuid4().hex, "role": "candidate",
            "scope": "operational", "fixture": self.fixture, "inputs_sha256": self.inputs_sha256,
            "profile": self.inputs["operational_profile"], "packages": {package["id"]: package["archive_sha256"]
                for package in self.inputs["candidate"]["packages"]}, "tenants": {"primary": "tenant-a", "secondary": "tenant-b"},
            "commands": commands, "databases": {"source": source_db, "restored": restored_db},
            "backup": {"command": backup_id, "sha256": digest(backup), "bytes": len(backup)}, "inventories": inventories,
            "observations": {"state_before_append": before.get("sequence") if before.get("accepted") is True else None,
                "appended_sequence": append.get("sequence") if append.get("accepted") is True and append.get("event_count") == 1
                    and appended_metadata is not None and appended_metadata["sequence"] == append.get("sequence")
                    and append.get("sequence") in appended_events else None,
                "state_after_restart": replay.get("sequence") if replay.get("accepted") is True else None,
                "restore_checks": checks.checks,
                "responses": {name: {"command": identity, **({"output_command": command} if "output_ref" in command else {"output": command.get("output", "")}), "observation": observation}
                    for name, identity, command, observation in (("primary", primary_id, primary_command, before),
                        ("append", append_id, append_command, append), ("replay", replay_id, replay_command, replay),
                        ("secondary", secondary_id, secondary_command, second))}}}
        outcome = ("refusal" if before.get("accepted") is False or append.get("accepted") is False or replay.get("accepted") is False
                   else "effect" if before.get("accepted") is True and append.get("accepted") is True
                   and append.get("event_count") == 1 and replay.get("accepted") is True else "error")
        if outcome == "error":
            checks.check("execution-completed", False)
        return {"before": source, "after": restarted, "seed_snapshot": seed_snapshot, "backup_sha256": digest(backup), "backup_bytes": len(backup),
                "retained_fixture": getattr(self, "last_retained_fixture", None), "source_metadata": source_metadata,
                "primary": before, "append": append, "replay": replay, "secondary": second,
                "restored_inventory": copied, "appended_inventory": appended}, outcome, \
            "compatible" if all(row["passed"] for row in checks.checks) else "incompatible"

    def containment_case(self, identifier, checks):
        self.seed("3.70.1", 1000, checks)
        before = self.inventory()
        backup, _ = self.postgres_backup(self.redis)
        self.start_nodes(self.candidate)
        appended = self.actor(self.candidate, "tenant-a", 12, "IncrementCounter", checks, "new-write:")
        checks.check("post-backup-write-committed", appended.get("accepted") is True)
        self.stop_nodes()
        advanced = self.inventory()
        restored, port, _ = self.postgres_container("containment")
        self.postgres_restore(restored, backup)
        self.postgres_wait(restored)
        copied = self.inventory(restored)
        checks.check("containment-backup-restored", before == copied)
        checks.check("later-write-absent-from-pre-upgrade-backup", runtime.domain(advanced, "tenant-a") != runtime.domain(copied, "tenant-a"))
        self.start_nodes("3.70.1", restored, port, interval=1000)
        primary = self.actor("3.70.1", "tenant-a", 12, "AssertCounter", checks, "old-primary:")
        secondary = self.actor("3.70.1", "tenant-b", 3, "AssertCounter", checks, "old-secondary:")
        checks.check("old-host-primary-replays-twelve", primary.get("accepted") is True)
        checks.check("old-host-secondary-replays-three", secondary.get("accepted") is True)
        self.stop_nodes()
        replayed = self.inventory(restored)
        metadata, snapshot, events = runtime.stream(replayed, "tenant-a")
        checks.check("old-host-restored-head-twelve", metadata is not None and metadata["sequence"] == 12)
        checks.check("old-host-original-event-hashes-preserved", runtime.domain(before, "tenant-a") == runtime.domain(replayed, "tenant-a"))
        checks.check("old-host-second-tenant-preserved", runtime.domain(before, "tenant-b") == runtime.domain(replayed, "tenant-b"))
        return {"before": before, "after": replayed, "restored_inventory": copied,
                "old_host_hydration": {"primary": primary, "secondary": secondary}, "advanced": advanced, "containment_only": True,
                "rpo_zero": False, "backup_sha256": digest(regular(backup))}, "effect", "incompatible"

    def natural_reminder(self, checks):
        """Observe a completed scheduler effect through read-only actor sequence calls before injection."""
        deadline, observations = time.monotonic() + 30, []
        started_utc = stamp()
        deadline_utc = (datetime.datetime.fromisoformat(started_utc) + datetime.timedelta(seconds=30)).isoformat()
        completed = False
        while time.monotonic() < deadline:
            observation = self.probe(self.candidate, ["sequence", f"http://127.0.0.1:{self.sidecar_port}", "tenant-a", "fixture"],
                                     checks, "natural-scheduler:" + str(len(observations)) + ":",
                                     timeout=max(.001, deadline - time.monotonic()))
            observations.append({"observed_utc": stamp(), "sequence": observation.get("sequence"), "command": self.commands[-1]["id"],
                                 "completed_before_deadline": time.monotonic() <= deadline})
            if observation.get("sequence") == 13 and observations[-1]["completed_before_deadline"]:
                completed = True
                break
            time.sleep(.2)
        checks.check("natural-scheduler-effect-completed-before-manual-callback", completed)
        return {"completed": completed, "observations": observations, "manual_callback_sent_before_observation": False,
                "started_utc": started_utc, "deadline_utc": deadline_utc, "budget_seconds": 30}

    def reminder_window(self, due, selected_utc, phase, checks):
        observed_utc = stamp()
        remaining = (due - datetime.datetime.fromisoformat(observed_utc)).total_seconds()
        checks.check("reminder-" + phase + "-deadline-future", remaining >= REMINDER_MINIMUM_WINDOW_SECONDS)
        require(remaining >= REMINDER_MINIMUM_WINDOW_SECONDS,
                "reminder fixture future deadline missed before dependent operation")
        return {"phase": phase, "selected_utc": selected_utc, "due_utc": due.isoformat(),
                "observed_utc": observed_utc, "remaining_seconds": remaining,
                "startup_lead_seconds": REMINDER_STARTUP_LEAD_SECONDS,
                "minimum_remaining_seconds": REMINDER_MINIMUM_WINDOW_SECONDS}

    def reminder_case(self, identifier, checks):
        self.seed(self.candidate, 1000, checks)
        before = self.inventory()
        selected_utc = stamp()
        due = datetime.datetime.fromisoformat(selected_utc) + datetime.timedelta(seconds=REMINDER_STARTUP_LEAD_SECONDS)
        self.reminder_due = due.isoformat()
        self.start_nodes(self.candidate)
        windows = [self.reminder_window(due, selected_utc, "initial-before-register", checks)]
        registration = self.http("POST", f"http://127.0.0.1:{self.host_port}/reminder/register/1", {})
        windows.append(self.reminder_window(due, selected_utc, "initial-after-register", checks))
        original_due = self.reminder_due
        checks.check("reminder-armed", registration.get("armed") == 1 and registration.get("unresolved") == 0)
        checks.check("initial-convergence-no-direct-submission", registration.get("submitted") == 0)
        natural = None
        if identifier == "stale-generation-refusal":
            time.sleep(max(0, (due - datetime.datetime.now(datetime.timezone.utc)).total_seconds()) + .2)
            natural = self.natural_reminder(checks)
        else:
            before_restart = self.probe(self.candidate, ["sequence", f"http://127.0.0.1:{self.sidecar_port}", "tenant-a", "fixture"],
                                        checks, "before-restart:")
            checks.check("scheduled-effect-not-committed-before-restart", before_restart.get("sequence") == 12)
        self.stop_nodes()
        restart_before = self.inventory()
        revision = 2 if identifier == "stale-generation-refusal" else 1
        if revision == 2:
            selected_utc = stamp()
            due = datetime.datetime.fromisoformat(selected_utc) + datetime.timedelta(seconds=REMINDER_STARTUP_LEAD_SECONDS)
            self.reminder_due = due.isoformat()
        self.start_nodes(self.candidate)
        windows.append(self.reminder_window(due, selected_utc, "restart-before-register", checks))
        rearmed = self.http("POST", f"http://127.0.0.1:{self.host_port}/reminder/register/{revision}", {})
        windows.append(self.reminder_window(due, selected_utc, "restart-after-register", checks))
        checks.check("reminder-converges-after-restart", rearmed.get("unresolved") == 0 and rearmed.get("quarantined") == 0)
        checks.check("restart-convergence-no-direct-submission", rearmed.get("submitted") == 0)
        if revision == 1:
            time.sleep(max(0, (due - datetime.datetime.now(datetime.timezone.utc)).total_seconds()) + .2)
            natural = self.natural_reminder(checks)
        callback_url = f"http://127.0.0.1:{self.host_port}/reminder/callback/1"
        if self.candidate in ("3.125.1", "3.126.0"):
            require(registration.get("intent_due") == original_due and registration.get("intent_revision") == 1
                    and isinstance(registration.get("reminder_name"), str), "original armed reminder identity missing")
            callback_url += "?" + urllib.parse.urlencode({"due": original_due, "reminderName": registration["reminder_name"]})
        repeated = self.http("POST", callback_url, {})
        if self.candidate in ("3.125.1", "3.126.0"):
            checks.check("callback-actor-request-accepted", isinstance(repeated.get("callback_status"), int)
                         and not isinstance(repeated.get("callback_status"), bool)
                         and 200 <= repeated["callback_status"] < 300)
            checks.check("callback-replays-original-armed-identity", repeated.get("intent_due") == original_due
                         and repeated.get("intent_revision") == 1 and repeated.get("reminder_name") == registration["reminder_name"])
        checks.check("restart-or-stale-callback-no-second-effect", repeated.get("sequence") == 13)
        self.stop_nodes()
        after = self.inventory()
        checks.check("original-domain-event-envelope-preserved", all(row in after for row in before if row["kind"] == "event"))
        checks.check("reminder-second-tenant-preserved", runtime.domain(before, "tenant-b") == runtime.domain(after, "tenant-b"))
        if revision == 2:
            checks.check("stale-generation-domain-preserved", runtime.domain(restart_before, "tenant-a") == runtime.domain(after, "tenant-a"))
        return {"before": restart_before if revision == 2 else before, "after": after,
                "due": self.reminder_due, "deadline_windows": windows, "registration": registration, "rearmed": rearmed,
                "natural_scheduler_effect": natural, "repeated_or_stale": repeated,
                "fixture_authority": "bounded Test authority; P2 acceptance pending"}, "refusal" if revision == 2 else "effect", "compatible"

    def evolution_case(self, identifier, checks, version=None):
        version = self.candidate if version is None else version
        legacy_alias = "P1R.Legacy.CounterIncremented"
        self.seed(version, 1000, checks, write_alias="old")
        unknown_input = None
        if identifier == "unknown-version-refusal":
            self.mutate("unknown-version")
            event_key = "tenant-a:counter:fixture:events:7"
            raw_event = next((value for key, value in self.raw_state(self.redis).items() if key.endswith(event_key)), None)
            unknown_input = {"key_suffix": event_key, "sha256": digest(raw_event),
                             "metadata_version": json.loads(raw_event)["metadataVersion"]} if raw_event else None
            checks.check("unknown-version-fixture-persisted", unknown_input is not None
                         and unknown_input["metadata_version"] == 987)
        before = self.inventory()
        self.start_nodes(version)
        registration = self.http("GET", f"http://127.0.0.1:{self.domain_port}/ready")["evolution_registration"]
        actor = self.actor(version, "tenant-a", 12, "AssertCounter", checks, "logical:")
        expected = identifier != "unknown-version-refusal"
        checks.check("logical-hydration-disposition", actor.get("accepted") is expected)
        checks.check("unknown-version-refused-before-application", expected or (
            actor.get("accepted") is False and actor.get("event_count") == 0 and actor.get("sequence") == 12
            and actor.get("error") == "Protected data diagnostic details were redacted. ReasonCode=logical-event-read-rejected; Stage=Processing."))
        checks.check("bounded-v1-clr-serializer-registration-observed", registration.get("bounded_v1_serializer_registered") is True
                     and registration.get("write_alias") == "P1R.Counter.CounterIncremented")
        # Independently read actual application envelopes through the Dapr actor-state API.
        readback = []
        for sequence in range(1, 13):
            actor_id = urllib.parse.quote("tenant-a:counter:fixture", safe="")
            key = urllib.parse.quote("tenant-a:counter:fixture:events:" + str(sequence), safe="")
            url = f"http://127.0.0.1:{self.sidecar_port}/v1.0/actors/AggregateActor/{actor_id}/state/{key}"
            value = self.http("GET", url)
            checks.check("logical-envelope-sequence-" + str(sequence), value.get("sequenceNumber") == sequence)
            checks.check("logical-envelope-alias-" + str(sequence), value.get("eventTypeName") == legacy_alias)
            readback.append({"sequence": sequence, "url": url, "sha256": digest(canonical(value)),
                             "event_type_name": value.get("eventTypeName"), "payload_sha256": digest(base64.b64decode(value["payload"]))})
        self.stop_nodes()
        after = self.inventory()
        checks.check("logical-readback-original-domain-inventory-preserved", [row for row in before if row["kind"] != "bookkeeping"]
                     == [row for row in after if row["kind"] != "bookkeeping"])
        original_events = {row["key"]: row["sha256"] for row in before if row["kind"] == "event"}
        retained_events = {row["key"]: row["sha256"] for row in after if row["kind"] == "event"}
        checks.check("logical-original-event-bytes-preserved", original_events == retained_events and len(original_events) == 15)
        executed = actor.get("accepted") is True and actor.get("sequence") == 12 and expected and all(
            item["event_type_name"] == legacy_alias for item in readback)
        checks.check("registered-logical-alias-evolution-executed", registration.get("manifest_registered") is True
                     and registration.get("fixture_manifest_fingerprint") == "5a5748913258b5832b333fe507bc689f1ac9e53a9201b4bb5cb983b535da933b"
                     and registration.get("fixture_manifest_test_only") is True and registration.get("gateway_authority") is False
                     and (executed or not expected))
        return {"before": before, "after": after, "dapr_application_readback": readback,
                "coverage": "bounded V1 old-alias serialization, registered logical hydration and application-envelope readback",
                "actor": actor, "legacy_alias": legacy_alias,
                "unknown_version_input": unknown_input,
                "domain_registration_observation": registration,
                "registered_logical_alias_evolution": {"executed": executed,
                    "gateway_authority": False}}, \
            "effect" if actor.get("accepted") else "refusal", "compatible" if all(row["passed"] for row in checks.checks) else "incompatible"

    def checkout_case(self, identifier, checks):
        if identifier == "current-build":
            observation = self.probe("source", ["identity"], checks, "source:")
            checks.check("separate-debug-source-paths", all("/Debug/" in row["path"] for row in observation["assemblies"]))
            return {"scope": "source-comparison-only", "identity": observation}, "effect", "compatible"
        lane = identifier
        cases = qualification.executed_case_inventory(self.inputs)[lane]
        if lane == "legacy-metadata":
            selected_case = self.candidate + "-pascal-floor-None"
            action = lambda version_case, measurements: self.metadata_case(lane, version_case, measurements)
        elif lane in ("query-wire", "projection-wire"):
            selected_case = next(case for case in cases if self.candidate in case)
            action = lambda version_case, measurements: self.wire_case(lane, version_case, measurements)
        elif lane == "invalid-evidence":
            selected_case = self.candidate + "-unknown-version"
            return self.checkout_live_case(lane, selected_case, checks)
        else:
            selected_case = next(case for case in cases if "-to-" + self.candidate in case)
            return self.checkout_live_case(lane, selected_case, checks)
        paired = {}
        outcomes = {}
        dispositions = {}
        for version, case in (("package", selected_case), ("source", selected_case.replace(self.candidate, "source"))):
            measured = Measurements()
            try:
                observation, outcome, disposition = action(case, measured)
                paired[version], outcomes[version], dispositions[version] = observation, outcome, disposition
            except Exception as error:
                measured.check("execution-completed", False)
                paired[version] = dict(getattr(error, "observations", {}), error=type(error).__name__, reason=str(error))
                outcomes[version], dispositions[version] = "error", "unverified"
            finally:
                for check, witness in zip(measured.checks, measured.witnesses):
                    identity = version + ":" + check["id"]
                    checks.check(identity, check["passed"], dict(witness, id=identity))
        package_semantics = semantic_observation(paired["package"], outcomes["package"])
        source_semantics = semantic_observation(paired["source"], outcomes["source"])
        checks.check("source-package-semantics-equivalent", package_semantics == source_semantics
                     and all(value != "error" for value in outcomes.values()))
        comparison = {"selected_version": self.candidate, "selected_case": selected_case,
                      "source_case": selected_case.replace(self.candidate, "source"),
                      "scope": "same fixture operations, domain outcomes and persisted coordinates; dynamic identities excluded",
                      "package": package_semantics, "source": source_semantics,
                      "delta": semantic_delta(package_semantics, source_semantics)}
        disposition = ("compatible" if all(check["passed"] for check in checks.checks)
                       and all(value == "compatible" for value in dispositions.values()) else "incompatible")
        if "error" in outcomes.values():
            checks.check("execution-completed", False)
            disposition = "unverified"
        return {"scope": "source-comparison-only", "comparison": comparison, "paired_observations": paired}, \
            "error" if "error" in outcomes.values() else outcomes["source"], disposition

    def checkout_live_case(self, lane, selected_case, checks):
        """Seed both isolated branches before allowing either reader to run."""
        original_database = self.redis, self.redis_port
        paired, measured, databases, prepared, outcomes = {}, {}, {}, {}, {}
        cases = {"package": selected_case, "source": selected_case.replace(self.candidate, "source")}
        try:
            for branch in ("package", "source"):
                measured[branch] = Measurements()
                try:
                    self.stop_nodes()
                    database, port, _ = self.postgres_container("checkout-" + branch + "-" + uuid.uuid4().hex)
                    databases[branch] = database, port
                    self.redis, self.redis_port = database, port
                    prepared[branch] = (self.prepare_invalid_case(cases[branch], measured[branch]) if lane == "invalid-evidence"
                                        else self.prepare_live_case(lane, cases[branch], measured[branch]))
                    paired[branch] = prepared[branch]
                except Exception as error:
                    measured[branch].check("execution-completed", False)
                    paired[branch] = dict(getattr(error, "observations", {}), error=type(error).__name__, reason=str(error))
                    outcomes[branch] = "error"
                finally:
                    self.stop_nodes()
            valid = set(prepared) == {"package", "source"} and all(
                row["passed"] for branch in measured.values() for row in branch.checks)
            equal = valid and prepared["package"]["persisted_preconditions"]["semantic"] == prepared["source"]["persisted_preconditions"]["semantic"]
            checks.check("source-package-seed-preconditions-successful", valid is True)
            checks.check("source-package-seed-preconditions-equal", equal is True)
            if equal:
                for branch in ("package", "source"):
                    self.redis, self.redis_port = databases[branch]
                    reader_start = len(getattr(self, "commands", []))
                    try:
                        paired[branch], outcomes[branch], _ = (self.invalid_case(cases[branch], measured[branch], prepared[branch])
                            if lane == "invalid-evidence" else self.live_case(lane, cases[branch], measured[branch], prepared[branch]))
                    except Exception as error:
                        measured[branch].check("execution-completed", False)
                        paired[branch] = dict(prepared[branch], **getattr(error, "observations", {}), error=type(error).__name__, reason=str(error))
                        outcomes[branch] = "error"
                    finally:
                        self.stop_nodes()
                        paired[branch]["reader_commands"] = [command["id"] for command in getattr(self, "commands", [])[reader_start:]]
            else:
                for branch in paired.values():
                    branch["dependent_operations"] = "unattempted"
                outcomes.update(package="error", source="error")
        finally:
            self.redis, self.redis_port = original_database
            for branch, measurements in measured.items():
                for check, witness in zip(measurements.checks, measurements.witnesses):
                    identity = branch + ":" + check["id"]
                    checks.check(identity, check["passed"], dict(witness, id=identity))
        package_semantics = semantic_observation(paired["package"], outcomes["package"])
        source_semantics = semantic_observation(paired["source"], outcomes["source"])
        checks.check("source-package-semantics-equivalent", equal is True and package_semantics == source_semantics
                     and all(value != "error" for value in outcomes.values()) and all(row["passed"] for row in checks.checks))
        if "error" in outcomes.values():
            checks.check("execution-completed", False)
        comparison = {"selected_version": self.candidate, "selected_case": selected_case, "source_case": cases["source"],
            "preconditions": {"successful": valid, "equal": equal},
            "scope": "equal successful persisted fixtures; exact bytes retained and dynamic fields explicitly recorded",
            "package": package_semantics, "source": source_semantics, "delta": semantic_delta(package_semantics, source_semantics)}
        return {"scope": "source-comparison-only", "comparison": comparison, "paired_observations": paired}, \
            "error" if "error" in outcomes.values() else outcomes["source"], \
            "unverified" if "error" in outcomes.values() or not equal else "compatible" if all(row["passed"] for row in checks.checks) else "incompatible"

    def failure_case(self, identifier, checks):
        before_shared = self.shared()
        self.seed(self.candidate, 1000, checks)
        before = self.inventory()
        if identifier == "startup-failure":
            environment = dict(os.environ, ASPNETCORE_URLS="invalid-owned-url", ASPNETCORE_ENVIRONMENT="Development")
            root = self.evidence["candidate"]
            item = self.launch(["dotnet", root / "host/bin/Release/net10.0/Host.dll"], environment)
            item["process"].wait(timeout=15)
            checks.check("real-consumer-startup-failed", item["process"].returncode != 0)
        else:
            self.start_nodes(self.candidate)
            command = [sys.executable, "-I", "-c", "import sys,urllib.request; urllib.request.urlopen(sys.argv[1],timeout=30).read()",
                       f"http://127.0.0.1:{self.host_port}/qualification-hold"]
            if identifier == "cancellation":
                request = self.launch(command, dict(os.environ))
                time.sleep(.3)
                preparation.signal_owned(request["ownership"].root, signal.SIGINT)
                request["process"].wait(timeout=5)
                checks.check("real-operational-request-cancelled", request["process"].returncode != 0)
            else:
                self.run(command, timeout=.3, check=False)
                checks.check("real-operational-request-timed-out", self.commands[-1]["exit_code"] == 124)
            checks.check("operational-request-reached-published-host", any(b"/qualification-hold" in item["log"].read_bytes()
                         for item in self.active if str(item["argv"][-1]).endswith("Host.dll")))
            checks.check("owned-operational-drill-triggered", bool(self.active))
        identities = [item["ownership"].root for item in self.active]
        self.stop_nodes()
        self.stop_nodes()
        checks.check("drill-owned-processes-absent", all(not preparation.same_process(identity, preparation.process_state(identity["pid"])) for identity in identities))
        checks.check("drill-shared-containers-preserved", before_shared == self.shared())
        after = self.inventory()
        checks.check("drill-domain-inventory-preserved", [row for row in before if row["kind"] != "bookkeeping"]
                     == [row for row in after if row["kind"] != "bookkeeping"])
        return {"before": before, "after": after, "owned_processes": identities}, "effect", "compatible"

    def execute(self):
        selected_root = getattr(self, "source_root", None) if (
            "tooling" in self.inputs or getattr(self, "explicit_source_root", False)) else None
        validate_execution_inputs(self.inputs, selected_root,
                                  getattr(self, "source_builds_root", None))
        if self.candidate == CANDIDATE:
            policy = qualification.load_support_table(self.inputs)
            self.support_rules = {(lane, rule["id"]): rule["treatment"] for lane, entry in policy["lanes"].items()
                                  for rule in entry["cases"]}
        else:
            self.support_rules = provisional_successor_execution_policy(self.inputs)
        try:
            for version in self.versions:
                self.build_consumers(version)
            self.verify_selected_candidate_archives()
            self.topology()
            for lane in qualification.SCENARIOS:
                self.check_sources()
                if lane == "provenance":
                    action, operational = self.provenance_case, False
                elif lane in ("legacy-metadata", "metadata-read"):
                    action, operational = lambda identifier, checks, lane=lane: self.metadata_case(lane, identifier, checks), False
                elif lane in ("query-wire", "projection-wire"):
                    action, operational = lambda identifier, checks, lane=lane: self.wire_case(lane, identifier, checks), False
                elif lane == "invalid-evidence":
                    action, operational = self.invalid_case, True
                elif lane == "mixed-api":
                    action, operational = self.mixed_case, True
                elif lane == "post-upgrade-restore":
                    action, operational = self.restore_case, True
                elif lane == "pre-upgrade-restore":
                    action, operational = self.containment_case, True
                elif lane == "failure-cleanup":
                    action, operational = self.failure_case, True
                elif lane == "checkout":
                    try:
                        self.build_consumers(self.candidate, source=True)
                    except Exception as error:
                        self.errors.append("distinct Debug/source consumer: " + str(error))
                    action, operational = self.checkout_case, True
                else:
                    action, operational = lambda identifier, checks, lane=lane: self.live_case(lane, identifier, checks), True
                self.lane(lane, action, operational)
            if "reminder-recovery" in self.inputs["selected_additions"]:
                self.check_sources()
                self.lane("reminder-recovery", self.reminder_case, True)
            if "logical-event-evolution" in self.inputs["selected_additions"]:
                self.check_sources()
                self.lane("logical-event-evolution", self.evolution_case, True)
            self.check_sources()
        finally:
            cleanup = self.cleanup()
            if self.pending_restore is not None:
                receipt = self.pending_restore
                receipt["cleanup"] = cleanup
                observed = storage.materialize(receipt, self.storage.context) if hasattr(self, "storage") else receipt
                commands = runtime.validate_commands(observed["commands"])
                rows = {name: value["rows"] for name, value in observed["inventories"].items()}
                receipt["checks"] = runtime.restore_observations(observed, commands, rows)
                receipt["assertions"] = checks_counter(receipt["checks"])
                receipt["execution"], receipt["compatibility"] = runtime.disposition("operational", receipt["assertions"]["failed"] == 0)
                runtime.restore_outcome(receipt, self.storage.context if hasattr(self, "storage") else None)
                path = self.output / "receipts" / (receipt["id"] + ".json")
                write_json(path, self.retain_packet(receipt))
                self.receipts.append(str(path))
            self.save()

    def cleanup(self):
        """Repeated cleanup always visits every owned resource, retaining every failed attempt."""
        attempts = []
        discovery_errors = []
        for name in list(getattr(self, "pending_containers", {})):
            try:
                self.recover_container(name)
            except (Exception, KeyboardInterrupt) as error:
                discovery_errors.append(type(error).__name__)
        label = "hexalith.p1r.invocation=" + self.invocation
        scope = ("tooling-synthetic" if self.fixture["synthetic"] else "operational"
                 if getattr(self, "operational_started", False) else "local-process-control")
        owned = [{"kind": "container", "id": identity, "label": label} for identity in self.containers]
        for name in getattr(self, "pending_containers", {}):
            owned.append({"kind": "container", "id": "container-name:" + name, "label": label})
        for item in self.processes:
            owned.append({"kind": "process", "id": "process:" + str(item["ownership"].root["pid"]) + ":" + str(item["ownership"].root["start_ticks"]), "label": label})
        owned.append({"kind": "scratch", "id": str(self.scratch), "label": label})
        identities = [row["id"] for row in owned]
        for _ in range(2):
            started, errors, remaining = stamp(), list(discovery_errors), []
            for name in list(getattr(self, "pending_containers", {})):
                try:
                    self.recover_container(name)
                except (Exception, KeyboardInterrupt) as error:
                    errors.append(type(error).__name__)
            remaining.extend("container-name:" + name for name in getattr(self, "pending_containers", {}))
            for item in reversed(self.processes):
                try:
                    result = item["ownership"].cleanup() if item["ownership"].remaining() else {"remaining": [], "errors": []}
                    errors.extend(result["errors"])
                    if result["remaining"]:
                        remaining.append("process:" + str(item["ownership"].root["pid"]) + ":" + str(item["ownership"].root["start_ticks"]))
                except (Exception, KeyboardInterrupt) as error:
                    errors.append(type(error).__name__)
            for identity in self.containers:
                try:
                    data = self.run(safe_inspect_argv(identity), cwd=ROOT, check=False)
                    if self.commands[-1]["exit_code"] == 0:
                        row = docker_observations(data)[0]
                        require(row["Id"] == identity and row["Config"]["Labels"].get("hexalith.p1r.invocation") == self.invocation,
                                "container ownership substituted")
                        self.run(["docker", "rm", "-f", identity], cwd=ROOT)
                    else:
                        require(b"No such" in data or b"no such" in data, "container absence is unmeasured")
                except (Exception, KeyboardInterrupt) as error:
                    errors.append(str(error))
                    remaining.append(identity)
            try:
                if self.scratch.exists():
                    shutil.rmtree(self.scratch)
            except (Exception, KeyboardInterrupt) as error:
                errors.append(type(error).__name__)
                remaining.append(str(self.scratch))
            attempts.append({"started_utc": started, "finished_utc": stamp(), "targeted": identities,
                             "removed": [identity for identity in identities if identity not in remaining],
                             "remaining": remaining, "errors": errors})
        shared_after, complete = {}, True
        try:
            # The scratch cwd has been removed; run the read-only discovery from the owning repository.
            names = self.run(["docker", "ps", "-aq"], cwd=ROOT).decode().split()
            rows = docker_observations(self.run(safe_inspect_argv(*names), cwd=ROOT)) if names else []
            shared_after = {row["Id"]: {"image": row["Image"], "running": row["State"]["Running"], "started": row["State"]["StartedAt"]}
                            for row in rows if row["Id"] not in self.containers}
        except (Exception, KeyboardInterrupt):
            complete = False
        shared = {"before": self.shared_before or {}, "after": shared_after, "complete": complete and self.shared_before is not None}
        checks = runtime.checks_from((set(attempts[0]["targeted"]) == set(identities), len(attempts) >= 2,
            all(set(later["targeted"]) >= set(earlier["remaining"]) for earlier, later in zip(attempts, attempts[1:])),
            attempts[-1]["remaining"] == [], all(not attempt["errors"] for attempt in attempts), shared["complete"],
            shared["before"] == shared["after"]), runtime.CLEANUP_CHECKS)
        counter = checks_counter(checks)
        passed = counter["failed"] == 0
        receipt = {"schema": runtime.CLEANUP_SCHEMA if scope != "local-process-control" else "hexalith.p1r.consumer-cleanup.v1",
                   "id": uuid.uuid4().hex, "invocation": self.invocation,
                   "scope": scope, "fixture": self.fixture, "inputs_sha256": self.inputs_sha256,
                   "profile": self.inputs["operational_profile"] if self.inputs else None, "owned": owned,
                   "attempts": attempts, "shared": shared, "checks": checks, "assertions": counter,
                   "execution": runtime.disposition(scope, passed)[0], "compatibility": runtime.disposition(scope, passed)[1]}
        if scope != "local-process-control":
            runtime.cleanup_outcome(receipt)
        path = self.output / "receipts" / (receipt["id"] + ".json")
        write_json(path, self.retain_packet(receipt))
        if scope != "local-process-control":
            self.receipts.append(str(path))
        else:
            self.result["consumer_cleanup"] = str(path)
        try:
            self.check_sources()
        except (Exception, KeyboardInterrupt) as error:
            self.errors.append("final source check: " + str(error))
        self.result["finished_utc"] = stamp()
        self.save()
        return receipt
