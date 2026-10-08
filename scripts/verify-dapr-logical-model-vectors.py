#!/usr/bin/env python3
"""Independent exact Python vectors for the selected Dapr logical model."""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def u(value):
    value = value.encode("utf-8", errors="strict")
    return struct.pack(">I", len(value)) + value


def i(value):
    return struct.pack(">i", value)


def n(value):
    return struct.pack(">q", value)


def optional(value, codec=u):
    return b"\0" if value is None else b"\1" + codec(value)


def h(value):
    return hashlib.sha256(value).digest()


def claim(separator, fields):
    return separator.encode("ascii") + b"\0\1" + struct.pack(">H", len(fields)) + b"".join(
        bytes([index]) + value for index, value in enumerate(fields, start=1))


def vectors():
    model = u("dapr-actor-logical-v1")
    fingerprint = h(b"local registry")
    config = h(b"local source")
    timestamp = n(621355968000000000) + struct.pack(">h", 120)
    source = claim("HX-EV-DAPR-SOURCE-1", [
        u("local-app"), u("local-ns"), u("AggregateActor"), u("tenant:d:aggregate"),
        u("tenant"), u("d"), u("aggregate"), u("r"), n(1), n(1), n(1),
        u("tenant:d:aggregate:events:"), u("tenant:d:aggregate:metadata"),
        u("aggregate-identity-decimal-v1"), config, u("eventstore.logical-payload.v1"),
        optional("etag"), timestamp, i(1)])
    source_hash = h(source)
    payload_hash = h(b"{}")
    application_preimage = b"".join(map(u, ["eventstore.logical-payload.v1", "tenant", "d",
        "aggregate", "message", "correlation", "cause", "Legacy.Event", "json", ""]))
    application_preimage += n(1) + i(1) + i(0) + payload_hash
    logical = h(application_preimage)
    extensions = struct.pack(">I", 2) + u("a") + u("1") + u("z") + u("2")
    consumed = b"HX-EV-DAPR-CONSUMED-1\0\1" + b"".join([
        u("message"), u("aggregate"), u("r"), u("tenant"), u("d"), n(1), n(7),
        timestamp, u("correlation"), optional("cause"), optional(""), optional("1.0"),
        u("Legacy.Event"), i(1), u("json"), optional(None), optional(None, i),
        optional(None), b"\1" + extensions, u("json"), logical, source_hash])
    listed = b"HX-EV-DAPR-PREFIX-LIST-1\0\1" + source_hash + model + struct.pack(">I", 1) + n(1) + logical
    genesis = b"HX-EV-DAPR-ACC-GENESIS-1\0\1" + source_hash + model + fingerprint
    step = b"HX-EV-DAPR-ACC-STEP-1\0\1" + source_hash + model + fingerprint + h(genesis) + n(1) + logical
    route = claim("HX-EV-DAPR-ROUTE-1", [logical, u("tenant"), u("d"), u("aggregate"), u("r"),
        n(1), u("message"), u("Legacy.Event"), i(1), optional(None), optional(None, i),
        u("json"), u("evt"), i(1), fingerprint, payload_hash, u("json"), h(consumed), source_hash, model])
    prefix = claim("HX-EV-DAPR-PREFIX-1", [u("tenant"), u("d"), u("aggregate"), u("r"),
        n(1), n(1), n(1), n(1), i(1), h(listed), h(step), fingerprint,
        optional(None), optional(None), optional(None), source_hash, model])
    raw = dict(source=source, application=application_preimage, consumed=consumed, ordered_list=listed,
        genesis=genesis, step=step, route=route, prefix=prefix)
    return {key: {"preimage": value.hex(), "sha256": h(value).hex()} for key, value in raw.items()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    path = Path(__file__).resolve().parents[1] / "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-model-2026-10-08/vectors.json"
    expected = vectors()
    if args.write:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(expected, indent=2) + "\n", encoding="utf-8")
    else:
        actual = json.loads(path.read_text(encoding="utf-8"))
        if actual != expected:
            raise SystemExit("Dapr logical model independent vector mismatch")
    print(json.dumps({"result": "passed", "vectors": len(expected), "scope": "local logical codec only"}))


if __name__ == "__main__":
    main()
