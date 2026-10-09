#!/usr/bin/env python3
"""Independent measured logical checkpoint owner framing; vectors grant no operation or backend authority."""
import argparse
import hashlib
import json
import struct
from pathlib import Path


def u(text):
    image = text.encode('utf-8', errors='strict')
    return struct.pack('>I', len(image)) + image


def sha(image):
    return hashlib.sha256(image).digest()


def record(separator, fields):
    return separator.encode('ascii') + b'\0\x01' + struct.pack('>H', len(fields)) + b''.join(bytes([i + 1]) + field for i, field in enumerate(fields))


def vectors():
    h = bytes(range(32))
    namespace = sha(b'HX-EV-DAPR-CHECKPOINT-NAMESPACE-1\0' + b''.join(u(t) for t in ('tenant', 'd', 'aggregate', 'r', 'projection', 'projection:tenant:id', 'projection-store')) + h)
    fold = sha(b'HX-EV-DAPR-PROJECTION-FOLD-1\0' + namespace + h)
    scope = sha(b'HX-EV-DAPR-CHECKPOINT-SCOPE-1\0' + namespace + h + fold)
    version = sha(u('operation') + struct.pack('>q', 1))
    key = 'logical-checkpoint-v1:' + namespace.hex() + ':state:' + version.hex()
    witness_key = 'logical-checkpoint-v1:' + namespace.hex() + ':witness'
    def root(sequence):
        origin = record('HX-EV-DAPR-SNAPSHOT-WITNESS-1', [u('tenant'), u('d'), u('aggregate'), u('r'), h, struct.pack('>q', sequence), u(key), u(witness_key), h, h, h, h, h, u('operation'), struct.pack('>q', 1), h, h, u('plaintext-canonical-v1'), u('dapr-actor-logical-snapshot-v1'), u('test-state-json')])
        return record('HX-EV-DAPR-CHECKPOINT-ROOT-1', [u('tenant'), u('d'), u('projection'), u('projection:tenant:id'), u('projection-store'), h, scope, fold, u(key), struct.pack('>I', len(origin)) + origin])
    images = {'namespace': namespace, 'fold': fold, 'scope': scope, 'version': version, 'root': root(2), 'root-max-encoding-only': root(2**63 - 1)}
    return {name: {'hex': image.hex(), 'bytes': len(image), 'sha256': sha(image).hex()} for name, image in images.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--verify', type=Path)
    args = parser.parse_args()
    result = vectors()
    if args.verify and json.loads(args.verify.read_text()) != result:
        raise SystemExit('Independent checkpoint owner vectors differ')
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({'vectors': len(result), 'status': 'pass'}))


if __name__ == '__main__':
    main()
