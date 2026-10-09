#!/usr/bin/env python3
"""Compute distinct anchored continuation vectors without importing runtime codecs."""
import argparse
import hashlib
import json
import pathlib
import struct

MODEL = 'dapr-actor-logical-anchored-replay-v1'
U = lambda value: struct.pack('>I', len(value.encode('utf-8'))) + value.encode('utf-8')
B = lambda value: struct.pack('>I', len(value)) + value
N = lambda value: struct.pack('>q', value)
I = lambda value: struct.pack('>i', value)
H = lambda value: hashlib.sha256(value.encode()).digest()
SHA = lambda value: hashlib.sha256(value).hexdigest()

def vectors():
    source, registry, selection, state = [H(x) for x in ('source', 'registry', 'selection', 'canonical-state')]
    accumulator, effective, transcript = [H(x) for x in ('anchor-accumulator', 'anchor-effective', 'anchor-transcript')]
    output = []
    def add(name, data, fields):
        output.append({'name': name, 'fields': fields, 'hex': data.hex(), 'sha256': SHA(data), 'bytes': len(data)})
    def prefix(name, start, end, head, target, count, covered):
        ordered_list = H('ordered-list') if count else hashlib.sha256(b'HX-EV-DAPR-PREFIX-LIST-1\0\x01' + source + U('dapr-actor-logical-v1') + struct.pack('>I', 0)).digest()
        fields = {'tenant': 'tenant-a', 'domain': 'counter', 'aggregate': 'aggregate-a', 'type': 'counter',
                  'start': start, 'end': end, 'head': head, 'target': target, 'count': count, 'covered': covered,
                  'list': ordered_list.hex(), 'accumulator': accumulator.hex(), 'registry': registry.hex(),
                  'selection': selection.hex(), 'state': state.hex(), 'source': source.hex(), 'model': MODEL}
        values = [U(fields[x]) for x in ('tenant', 'domain', 'aggregate', 'type')]
        values += [N(x) for x in (start, end, head, target)] + [I(count)]
        values += [bytes.fromhex(fields[x]) for x in ('list', 'accumulator', 'registry', 'selection')] + [N(covered)]
        values += [state, source, U(MODEL)]
        data = b'HX-EV-DAPR-ANCHORED-PREFIX-1\0\x01\x00\x11' + b''.join(bytes([i + 1]) + v for i, v in enumerate(values))
        add(name, data, fields)
    prefix('tail-prefix', 3, 4, 5, 5, 2, 2)
    prefix('zero-tail-prefix', 6, 5, 5, 5, 0, 5)
    prefix('max-zero-tail-prefix', 2**63-1, 2**63-1, 2**63-1, 2**63-1, 0, 2**63-1)
    binding = H('reconstruction')
    common = {'source': source.hex(), 'registry': registry.hex(), 'selection': selection.hex(), 'state': state.hex(),
              'binding': binding.hex(), 'effective': effective.hex(), 'transcript': transcript.hex(), 'accumulator': accumulator.hex()}
    scope = source + U(MODEL) + registry + b'\x01' + binding
    data = b'HX-EV-DAPR-ANCHORED-EFFECTIVE-STEP-1\0\x01' + selection + scope + effective + N(3) + H('route') + U('incremented') + I(2) + U('json') + H('payload')
    add('effective-step', data, common | {'sequence': 3, 'route': H('route').hex(), 'type': 'incremented', 'version': 2, 'format': 'json', 'payload': H('payload').hex()})
    data = b'HX-EV-DAPR-ANCHORED-TRANSCRIPT-GENESIS-1\0\x01' + U('tenant-a') + U('operation-a') + selection + transcript + state
    add('transcript-genesis', data, common | {'tenant': 'tenant-a', 'operation': 'operation-a'})
    entry = b'HX-EV-DAPR-PAGE-TRANSCRIPT-1\0\x01' + N(1) + N(1) + H('request') + accumulator + H('successor-accumulator') + N(3) + N(4) + I(2) + H('response') + b'\x00\x01' + state + b'\x01' + H('successor-state') + effective + H('successor-effective')
    add('page-entry', entry, common | {'request': H('request').hex(), 'response': H('response').hex(), 'nextAccumulator': H('successor-accumulator').hex(), 'nextState': H('successor-state').hex(), 'nextEffective': H('successor-effective').hex()})
    data = b'HX-EV-DAPR-ANCHORED-TRANSCRIPT-STEP-1\0\x01' + U('tenant-a') + U('operation-a') + selection + transcript + B(entry)
    add('transcript-step', data, common | {'tenant': 'tenant-a', 'operation': 'operation-a', 'entry': entry.hex()})
    inner = b'HX-EV-DAPR-REPLAY-LEDGER-2\0\x01' + entry[len(b'HX-EV-DAPR-PAGE-TRANSCRIPT-1\0\x01'):] + transcript + H('successor-transcript') + b'\x00'
    add('ledger', b'HX-EV-DAPR-ANCHORED-LEDGER-1\0\x01' + selection + B(inner), common | {'inner': inner.hex()})
    data = b'HX-EV-DAPR-ANCHORED-PAGE-REQUEST-1\0\x01' + selection + U('tenant-a') + U('operation-a') + U('owner-a') + N(1) + N(1) + U('request-a') + I(2) + source + registry
    add('request', data, common | {'tenant': 'tenant-a', 'operation': 'operation-a', 'owner': 'owner-a', 'request': 'request-a', 'count': 2})
    return {'model': MODEL, 'generator': 'independent Python struct/hashlib; no runtime imports', 'vectors': output}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=pathlib.Path)
    parser.add_argument('--verify', type=pathlib.Path)
    args = parser.parse_args()
    value = vectors()
    if args.verify:
        if json.loads(args.verify.read_text()) != value:
            raise SystemExit('Independent anchored continuation vector mismatch')
        print(str(len(value['vectors'])) + ' independent continuation vectors verified')
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(value, indent=2) + '\n')
    if not args.output and not args.verify:
        print(json.dumps(value, indent=2))

if __name__ == '__main__':
    main()
