#!/usr/bin/env python3
"""Independently verify local canonical-state ledger framing; grants no activation authority."""
import hashlib
import json
from pathlib import Path
import struct


ROOT = Path(__file__).resolve().parents[1]
VECTOR = ROOT / "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/vectors.json"
values = json.loads(VECTOR.read_text())["vectors"]
for name, count, end, final, states in (("canonical-successor", 1, 1, False, True),
                                       ("canonical-empty-final", 0, 0, True, True),
                                       ("source-only-final", 1, 1, True, False)):
    fields = {"pageOrdinal": 1, "generation": 2, "requestHash": bytes(range(32)).hex(),
              "previousAccumulator": bytes(range(32, 64)).hex(), "accumulator": bytes(range(64, 96)).hex(),
              "startSequence": 1, "endSequence": end, "count": count, "responseHash": bytes(range(96, 128)).hex(),
              "isFinal": final, "priorStateHash": hashlib.sha256(b'{"value":0}').hexdigest() if states else None,
              "canonicalStateHash": hashlib.sha256(b'{"value":' + str(end).encode() + b'}').hexdigest() if states else None}
    encoded = b"HX-EV-DAPR-REPLAY-LEDGER-1\0" + bytes([1]) + struct.pack(">qq", 1, 2)
    encoded += b"".join(bytes.fromhex(fields[key]) for key in ("requestHash", "previousAccumulator", "accumulator"))
    encoded += struct.pack(">qqi", 1, end, count) + bytes.fromhex(fields["responseHash"]) + bytes([final])
    for key in ("priorStateHash", "canonicalStateHash"):
        encoded += bytes([fields[key] is not None]) + (bytes.fromhex(fields[key]) if fields[key] else b"")
    assert values[name]["fields"] == fields, name
    assert values[name]["preimageHex"] == encoded.hex(), name
    assert values[name]["sha256"] == hashlib.sha256(encoded).hexdigest(), name
print("3 independent canonical ledger vectors passed")
