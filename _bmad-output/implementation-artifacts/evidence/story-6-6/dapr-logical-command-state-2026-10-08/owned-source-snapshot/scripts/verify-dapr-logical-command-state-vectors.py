#!/usr/bin/env python3
"""Independently recompute dormant logical command-state preimages with struct/hashlib only."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[1]
VECTOR = ROOT / "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-command-state-2026-10-08/vectors.json"


def u(value):
    value = value.encode("utf-8", "strict")
    return struct.pack(">I", len(value)) + value


def b(value):
    return struct.pack(">I", len(value)) + value


def n(value):
    return struct.pack(">q", value)


def optional(value):
    return b"\0" if value is None else b"\1" + value


def h(value):
    return hashlib.sha256(value).digest()


def t(value):
    utc = value.astimezone(datetime.timezone.utc)
    epoch = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    elapsed = utc - epoch
    ticks = (elapsed.days * 86400 + elapsed.seconds) * 10000000 + elapsed.microseconds * 10
    return n(ticks) + struct.pack(">h", int(value.utcoffset().total_seconds() / 60))


def vectors():
    result = []

    def add(name, value):
        result.append({"name": name, "preimageHex": value.hex(), "sha256Hex": h(value).hex()})
        return h(value)

    source = bytes(range(32))
    registry = bytes(range(32, 64))
    binding = bytes(range(64, 96))
    scope = source + u("dapr-actor-logical-v1") + registry + optional(binding)
    command_fields = ["01K00000000000000000000001", "tenant-a", "counter", "aggregate-1", "Fixtures.Increment"]
    payload = b'{"amount":2}'
    command = b"HX-EV-DAPR-COMMAND-ROUTE-1\0\1" + b"".join(u(v) for v in command_fields)
    command += n(len(payload)) + h(payload) + u("correlation-1") + optional(u("cause-1")) + u("user-1")
    extensions = {"\ue000": "bmp", "\U00010000": "astral", "trace": "exact"}
    encoded_extensions = struct.pack(">I", len(extensions))
    encoded_extensions += b"".join(u(k) + u(v) for k, v in sorted(extensions.items(), key=lambda pair: pair[0].encode("utf-8")))
    command_hash = add("command-route", command + optional(encoded_extensions))
    add("command-route-null-extensions", command + optional(None))
    add("command-route-empty-extensions", command + optional(struct.pack(">I", 0)))
    c0 = add("effective-genesis", b"HX-EV-DAPR-EFFECTIVE-GENESIS-1\0\1" + scope)
    c1 = add("effective-step-one", b"HX-EV-DAPR-EFFECTIVE-STEP-1\0\1" + scope + c0 + n(1) + h(b"route-one")
             + u("increment") + struct.pack(">i", 2) + u("json") + h(b'{"delta":13}'))
    c2 = add("effective-step-two", b"HX-EV-DAPR-EFFECTIVE-STEP-1\0\1" + scope + c1 + n(2) + h(b"route-two")
             + u("increment") + struct.pack(">i", 2) + u("json") + h(b'{"delta":7}'))
    state0 = h(b'{"value":0}')
    state2 = h(b'{"value":20}')
    request = h(b"page-request")
    a0 = h(b"logical-genesis")
    a2 = h(b"logical-two")
    response = h(b"pinned-response")
    t0 = add("transcript-genesis", b"HX-EV-DAPR-TRANSCRIPT-GENESIS-1\0\1" + u("tenant-a") + u("operation-1")
             + scope + optional(state0) + optional(command_hash))
    scalars = n(1) + n(1) + request + a0 + a2 + n(1) + n(2) + struct.pack(">i", 2) + response + b"\1"
    scalars += optional(state0) + optional(state2)
    entry = b"HX-EV-DAPR-PAGE-TRANSCRIPT-1\0\1" + scalars + c0 + c2
    add("page-transcript-entry", entry)
    terminal = add("transcript-step", b"HX-EV-DAPR-TRANSCRIPT-STEP-1\0\1" + u("tenant-a") + u("operation-1")
                   + scope + t0 + b(entry))
    issued = datetime.datetime(2026, 10, 8, tzinfo=datetime.timezone.utc)
    expires = issued + datetime.timedelta(minutes=5)
    fields = [u("dapr-actor-logical-v1"), u("tenant-a"), u("counter"), u("aggregate-1"), u("Counter"),
              u("Fixtures.Increment"), u(command_fields[0]), command_hash, source, n(4), n(2), registry, binding,
              u("operation-1"), u("owner-1"), n(1), n(1), n(2), a2, h(b"prefix-claim"), h(b"source-proof"),
              response, state2, c2, terminal, t(issued), t(expires)]
    claim = b"HX-EV-DAPR-COMMAND-STATE-1\0\1" + struct.pack(">H", len(fields))
    claim += b"".join(bytes([tag]) + value for tag, value in enumerate(fields, 1))
    add("completed-command-state-claim", claim)
    proof = b"HX-EV-DAPR-COMMAND-PROOF-1\0\1" + b(claim) + u("logical-key-1") + b(bytes(range(64)))
    add("command-proof-framing", proof)
    add("committed-ledger-two", b"HX-EV-DAPR-REPLAY-LEDGER-2\0\1" + scalars + c0 + c2 + t0 + terminal + optional(h(proof)))
    empty_scalars = n(1) + n(1) + request + a0 + a0 + n(1) + n(0) + struct.pack(">i", 0) + response + b"\1"
    empty_scalars += optional(state0) + optional(state0)
    empty_entry = b"HX-EV-DAPR-PAGE-TRANSCRIPT-1\0\1" + empty_scalars + c0 + c0
    add("empty-page-transcript-entry", empty_entry)
    add("empty-page-transcript-step", b"HX-EV-DAPR-TRANSCRIPT-STEP-1\0\1" + u("tenant-a") + u("operation-1")
        + scope + t0 + b(empty_entry))
    return {"model": "dapr-logical-completed-command-state-v1", "scope": "independent local preimages; no signature or production authority", "vectors": result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    expected = vectors()
    if args.write:
        VECTOR.parent.mkdir(parents=True, exist_ok=True)
        VECTOR.write_text(json.dumps(expected, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    actual = json.loads(VECTOR.read_text(encoding="utf-8"))
    if actual != expected:
        raise SystemExit("logical command-state independent vector mismatch")
    print(json.dumps({"result": "passed", "vectors": len(expected["vectors"]), "activationAuthority": False}))


if __name__ == "__main__":
    main()
