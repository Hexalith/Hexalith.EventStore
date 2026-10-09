#!/usr/bin/env python3
"""Independent unsigned checkpoint selection/range framing; MAX vectors are structural only."""
import argparse
import hashlib
import json
import struct
from pathlib import Path

MODEL = 'dapr-actor-logical-checkpoint-initial-preparation-v1'
MAX = 2**63 - 1


def u(value):
    image = value.encode('utf-8', errors='strict')
    return struct.pack('>I', len(image)) + image


def sha(image):
    return hashlib.sha256(image).digest()


def record(separator, fields):
    return separator.encode('ascii') + b'\0\x01' + struct.pack('>H', len(fields)) + b''.join(bytes([i + 1]) + field for i, field in enumerate(fields))


def vectors():
    result = {}
    hashes = {name: sha(name.encode('ascii')) for name in ('checkpoint', 'requested', 'fold', 'reconstruction', 'registry', 'state', 'root', 'witness')}
    def selection(name, k, head, target):
        fields = {key: value.hex() for key, value in hashes.items()} | {'k': k, 'head': head, 'target': target, 'model': MODEL}
        image = record('HX-EV-DAPR-CHECKPOINT-INITIAL-PREPARE-1', list(hashes.values()) + [struct.pack('>q', n) for n in (k, head, target)] + [u(MODEL)])
        result[name] = {'kind': 'selection', 'fields': fields, 'hex': image.hex(), 'bytes': len(image), 'sha256': sha(image).hex()}
        return sha(image)
    current = selection('current-selection', 2, 2, 2)
    selection('below-head-selection', 2, 3, 2)
    tail = selection('tail-selection', 2, 3, 3)
    capped = selection('bounded-tail-selection', 2, 1000, 1000)
    maximum = selection('max-selection-structural-only', MAX, MAX, MAX)
    last = selection('max-last-tail-selection-structural-only', MAX-1, MAX, MAX)
    def range_row(name, pin, k, head, target, start, end, count, kind):
        fields = {'selection': pin.hex(), 'requested': hashes['requested'].hex(), 'k': k, 'head': head, 'target': target, 'start': start, 'end': end, 'count': count, 'kind': kind, 'model': MODEL}
        image = record('HX-EV-DAPR-CHECKPOINT-RANGE-PREPARE-1', [pin, hashes['requested']] + [struct.pack('>q', n) for n in (k, head, target, start, end)] + [struct.pack('>i', count), bytes([kind]), u(MODEL)])
        result[name] = {'kind': 'range', 'fields': fields, 'hex': image.hex(), 'bytes': len(image), 'sha256': sha(image).hex()}
    range_row('current-zero', current, 2, 2, 2, 3, 2, 0, 0)
    range_row('tail-one', tail, 2, 3, 3, 3, 3, 1, 1)
    range_row('tail-bounded', capped, 2, 1000, 1000, 3, 258, 256, 1)
    range_row('max-zero-structural-only', maximum, MAX, MAX, MAX, MAX, MAX, 0, 0)
    range_row('max-last-tail-structural-only', last, MAX-1, MAX, MAX, MAX, MAX, 1, 1)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--verify', type=Path)
    args = parser.parse_args()
    result = vectors()
    if args.verify and json.loads(args.verify.read_text()) != result:
        raise SystemExit('Independent unsigned checkpoint preparation vectors differ')
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({'vectors': len(result), 'status': 'pass', 'authority': 'none'}))


if __name__ == '__main__':
    main()
