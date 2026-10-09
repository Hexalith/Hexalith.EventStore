#!/usr/bin/env python3
"""Command-line entry point for the 6.1-P1R published qualification harness.

``prepare`` writes a new sealed packet and refuses any existing output path.
Package evidence, owner decisions and lane/recovery receipts are dependent
execution: without owner execution inputs they are refused, never defaulted.
``validate`` is read-only and independently recomputes the evaluation. Exit 0
means a structurally valid packet; it never means qualification or usability.
"""
import argparse
import json
from pathlib import Path
import sys

from p1r_published_qualification import InvalidPacket, create_packet, validate_packet


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    prepare = commands.add_parser("prepare", help="create a new sealed qualification packet")
    prepare.add_argument("--out", type=Path, required=True, help="new output directory (must not exist)")
    prepare.add_argument("--inputs", type=Path, help="owner execution inputs (hexalith.p1r.owner-inputs.v1)")
    prepare.add_argument("--decisions", type=Path, help="owner decisions and same-baseline conformance")
    prepare.add_argument("--candidate-evidence", type=Path, help="isolated Release package evidence for the candidate")
    prepare.add_argument("--rollback-evidence", type=Path, help="isolated Release package evidence for a selected rollback")
    prepare.add_argument("--comparison-evidence", type=Path, action="append", default=[],
                         help="independently verified historical comparison evidence (repeatable; never rollback)")
    prepare.add_argument("--receipt", type=Path, action="append", default=[],
                         help="lane, restore or cleanup receipt from a separately selected executor (repeatable)")
    prepare.add_argument("--process-controls", action="store_true",
                         help="run the bounded local process controls (local scope only)")
    commands.add_parser("validate", help="independently validate an existing packet").add_argument("directory", type=Path)
    args = parser.parse_args(arguments)
    try:
        if args.command == "validate":
            result = validate_packet(args.directory)
        else:
            packet = create_packet(args.out, inputs=args.inputs, decisions=args.decisions,
                                   candidate_evidence=args.candidate_evidence, rollback_evidence=args.rollback_evidence,
                                   receipts=tuple(args.receipt), process_controls=args.process_controls,
                                   comparison_evidence=tuple(args.comparison_evidence))
            if packet["errors"]:
                # The sealed packet retains the refusal; report its exact errors and refusals instead of validating.
                print(json.dumps({"valid": False, "qualified": False, "p1r_usable": False,
                                  "reason": "refused or incomplete invocation", "out": str(args.out.absolute()),
                                  "errors": packet["errors"], "refusals": packet["refusals"]}, sort_keys=True), file=sys.stderr)
                return 2
            result = {"out": str(args.out.absolute()), "errors": packet["errors"], "refusals": packet["refusals"],
                      **{key: packet["evaluation"][key] for key in ("technically_qualified", "decisions_complete", "qualified",
                                                                    "p1r_usable")}}
            validate_packet(args.out)  # The same independent validation must accept the new packet.
        print(json.dumps(result, sort_keys=True))
        return 0
    except Exception as error:  # Keep the documented 0/2 contract; never a traceback or another status.
        reason = str(error) if isinstance(error, InvalidPacket) else type(error).__name__
        print(json.dumps({"valid": False, "qualified": False, "p1r_usable": False, "reason": reason}), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
