#!/usr/bin/env python3
"""Independent struct/hashlib logical snapshot/checkpoint/rebase candidate vectors."""
import argparse
import hashlib
import json
import struct
from pathlib import Path

def u(value):
    data = value.encode('utf-8', errors='strict')
    return struct.pack('>I', len(data)) + data

def record(separator, fields):
    return separator.encode('ascii') + b'\0\x01' + struct.pack('>H', len(fields)) + b''.join(bytes([i+1])+x for i,x in enumerate(fields))

def vectors():
    h=bytes(range(32))
    def snapshot(sequence=7, folded=h, generation=1):
        return record('HX-EV-DAPR-SNAPSHOT-WITNESS-1', [u('tenant'),u('d'),u('a'),u('r'),h,struct.pack('>q',sequence),u('tenant:d:a:snapshot:logical-v1'),u('tenant:d:a:snapshot:logical-v1:evolution-witness'),h,folded,h,h,h,u('operation'),struct.pack('>q',generation),h,h,u('plaintext-canonical-v1'),u('dapr-actor-logical-snapshot-v1'),u('state-json')])
    checkpoint=record('HX-EV-DAPR-CHECKPOINT-WITNESS-1',[u('tenant'),u('d'),u('projection'),h,struct.pack('>q',7),h,h,h,u('dapr-actor-logical-checkpoint-v1')])
    rebase=record('HX-EV-DAPR-SNAPSHOT-REBASE-1',[h,hashlib.sha256(snapshot()).digest(),h,bytes(reversed(h)),h,bytes(reversed(h)),h,u('dapr-actor-logical-snapshot-rebase-v1')])
    images={'snapshot':snapshot(),'snapshot-max':snapshot(2**63-1),'snapshot-folded-change':snapshot(folded=bytes(reversed(h))),'snapshot-generation':snapshot(generation=2),'checkpoint':checkpoint,'rebase':rebase}
    return {k:{'hex':v.hex(),'bytes':len(v),'sha256':hashlib.sha256(v).hexdigest()} for k,v in images.items()}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path);parser.add_argument('--verify',type=Path);args=parser.parse_args();result=vectors()
    if args.verify and json.loads(args.verify.read_text())!=result: raise SystemExit('Independent snapshot vectors differ')
    if args.output: args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps({'vectors':len(result),'status':'pass'}))
if __name__=='__main__': main()
