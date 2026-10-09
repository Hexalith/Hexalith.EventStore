#!/usr/bin/env python3
"""Independent struct/hashlib query catalog and complete actor-root known answers."""
import argparse
import hashlib
import json
import struct
from pathlib import Path

def u(value):
    data = value.encode("utf-8", errors="strict")
    return b(data)

def b(value):
    return struct.pack(">I", len(value)) + value

def row(tag, keys, fields):
    return bytes([tag]) + b"".join(u(x) for x in keys) + struct.pack(">H", len(fields)) + b"".join(bytes([i + 1]) + x for i, x in enumerate(fields))

def vectors():
    h = bytes(range(32))
    binding = u("route") + u("space") + u("shared") + u("store") + b(b"backend") + u("")
    bindings = struct.pack(">I", 1) + b(binding)
    q = row(0x5b, ["d", "get-total"], [u("handler"), h, h, u("registration"), h, h, u("endpoint"), u("request"), h, u("response"), h, u("resolver"), h, h, b(bindings)])
    route = row(0x52, ["d", "route"], [u("event"), u("topic"), u("tenant"), u("handler"), h, h, u("mode"), u("contract"), h, struct.pack(">i", 1), b(b"schema"), u("serializer"), h, h, u("readback"), h, h, u("store")])
    declaration = row(0x59, ["d", "route", "space"], [u("item:"), u("shared"), u("store"), b(b"backend"), h])
    dependency = row(0x47, ["d", "dep", "managed"], [u("1.0"), h, u("framework")])
    digest = b"HX-EV-QUERY-ROUTE-1\0" + bytes([1]) + b(q) + struct.pack(">I", 2) + b(route) + b(declaration) + struct.pack(">I", 1) + b(dependency)
    def root(rows, generation=1):
        data = b"HX-EV-DAPR-LOGICAL-QUERY-ROOT-1\0" + struct.pack(">H", 1)
        data += b"".join(u(x) for x in ["dapr-actor-logical-v1", "tenant", "d", "route", "space", "store"])
        data += b(b"backend") + struct.pack(">qI", generation, len(rows))
        for key, type_name, payload, expiry, origin in rows:
            data += u(key) + u(type_name) + (bytes([0]) if payload is None else bytes([2]) + b(payload))
            data += bytes([0]) if expiry is None else bytes([2]) + struct.pack(">q", expiry)
            data += u(origin)
        return data
    def optional(value, encode):
        return bytes([0]) if value is None else bytes([2]) + encode(value)
    def request(full=False):
        data = b"HX-EV-DAPR-LOGICAL-QUERY-REQUEST-1\0" + struct.pack(">H", 1)
        data += b"".join(u(x) for x in ["tenant", "d", "a", "get-total"])
        data += hashlib.sha256(b"{}").digest() + u("c") + u("u")
        data += optional("entity" if full else None, u) + bytes([int(full)])
        data += bytes([0]) if not full else bytes([2]) + optional(10, lambda x: struct.pack(">i", x)) + optional(2, lambda x: struct.pack(">i", x)) + optional("cursor", u)
        data += optional("actor" if full else None, u) + optional("workload" if full else None, u) + bytes([int(full)])
        data += optional(["s1", "s2"] if full else None, lambda xs: struct.pack(">I", len(xs)) + b"".join(u(x) for x in xs))
        data += optional(["aud"] if full else None, lambda xs: struct.pack(">I", len(xs)) + b"".join(u(x) for x in xs))
        data += optional("delegation" if full else None, u) + optional("proof" if full else None, u)
        return data
    images = {"request-basic": request(), "request-full": request(True),"query-row": q, "input-route": route, "input-declaration": declaration, "dependency": dependency, "query-compatibility": digest,
              "root-empty": root([]), "root-present": root([("item:a", "value", b'{"value":2}', None, "op-1")]),
              "root-absent": root([("item:a", "", None, None, "op-2")]),
              "root-ttl": root([("item:a", "value", b'{"value":2}', 638900000000000000, "op-1")]),
              "root-two": root([("item:a", "value", b'{"value":2}', None, "op-1"), ("item:b", "value", b'{"value":3}', None, "op-2")]),
              "root-generation": root([("item:a", "value", b'{"value":2}', None, "op-1")], 2)}
    return {name: {"hex": value.hex(), "bytes": len(value), "sha256": hashlib.sha256(value).hexdigest()} for name, value in images.items()}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path)
    parser.add_argument("--verify", type=Path)
    args = parser.parse_args()
    result = vectors()
    if args.verify and json.loads(args.verify.read_text()) != result:
        raise SystemExit("Independent query vectors differ")
    if args.output:
        args.output.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({"vectors": len(result), "status": "pass"}))

if __name__ == "__main__":
    main()
