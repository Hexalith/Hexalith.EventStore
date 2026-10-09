#!/usr/bin/env python3
"""Independently frame distinct private replay anchor selections and covered-history seeds."""
import argparse
import hashlib
import json
import pathlib
import struct

MODEL = "dapr-actor-logical-anchored-replay-v1"

def u(text):
    image = text.encode("utf-8", errors="strict")
    if not image or len(image) > 512: raise ValueError("Text limit")
    return struct.pack(">I", len(image)) + image

def selection(covered=1, head=3, target=3, state=bytes([5])*32):
    fields = [u("tenant-a"), u("orders"), u("order-1"), u("r"), bytes([1])*32, bytes([2])*32,
              bytes([3])*32, bytes([4])*32, state, bytes([6])*32, bytes([7])*32, bytes([8])*32,
              struct.pack(">q", covered), struct.pack(">q", head), struct.pack(">q", target), u(MODEL)]
    return b"HX-EV-DAPR-REPLAY-ANCHOR-1\0" + b"\x01" + struct.pack(">H", 16) + b"".join(bytes([i])+value for i,value in enumerate(fields,1))

def vectors():
    rows=[]
    for name,image in [("anchor",selection()),("anchor-state-change",selection(state=bytes([9])*32)),("anchor-zero-tail",selection(3,3,3)),("anchor-max",selection(2**63-1,2**63-1,2**63-1))]:
        rows.append({"name":name,"hex":image.hex(),"bytes":len(image),"sha256":hashlib.sha256(image).hexdigest()})
    digest=hashlib.sha256(selection()).digest()
    for name,separator,covered in [("accumulator-seed",b"HX-EV-DAPR-ANCHOR-ACCUMULATOR-1\0",6),("effective-seed",b"HX-EV-DAPR-ANCHOR-EFFECTIVE-1\0",7),("transcript-seed",b"HX-EV-DAPR-ANCHOR-TRANSCRIPT-1\0",8)]:
        image=separator+b"\x01"+digest+bytes([covered])*32
        rows.append({"name":name,"hex":image.hex(),"bytes":len(image),"sha256":hashlib.sha256(image).hexdigest()})
    return {"schema":"independent-logical-anchor-vectors-v1","authority":False,"vectors":rows}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output",type=pathlib.Path)
    parser.add_argument("--verify",type=pathlib.Path)
    args=parser.parse_args()
    if bool(args.output)==bool(args.verify):parser.error("Select output or verify")
    expected=vectors()
    if args.output:
        args.output.parent.mkdir(parents=True,exist_ok=True)
        args.output.write_text(json.dumps(expected,indent=2)+"\n")
    else:
        if json.loads(args.verify.read_text())!=expected:raise SystemExit("Independent anchor vector mismatch")
        print("7 independent anchor vectors verified")

if __name__=="__main__":main()
