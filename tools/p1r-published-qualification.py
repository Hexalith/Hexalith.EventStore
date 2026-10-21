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

import p1r_qualification as preparation
import p1r_published_qualification as qualification

from p1r_published_qualification import (InvalidPacket, create_packet, load_document, measure_successor_support_table,
                                         parse_json, regular, validate_inputs, validate_packet, write_json)


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    prepare = commands.add_parser("prepare", help="create a new sealed qualification packet")
    prepare.add_argument("--out", type=Path, required=True, help="new output directory (must not exist)")
    prepare.add_argument("--inputs", type=Path, help="owner execution inputs (hexalith.p1r.owner-inputs.v1)")
    prepare.add_argument("--source-root", type=Path, help="exact selected 3.125.1 runtime checkout")
    prepare.add_argument("--source-builds-root", type=Path, help="exact release-source Builds checkout")
    prepare.add_argument("--decisions", type=Path, help="owner decisions and same-baseline conformance")
    prepare.add_argument("--candidate-evidence", type=Path, help="isolated Release package evidence for the candidate")
    prepare.add_argument("--rollback-evidence", type=Path, help="isolated Release package evidence for a selected rollback")
    prepare.add_argument("--comparison-evidence", type=Path, action="append", default=[],
                         help="independently verified historical comparison evidence (repeatable; never rollback)")
    prepare.add_argument("--receipt", type=Path, action="append", default=[],
                         help="lane, restore or cleanup receipt from a separately selected executor (repeatable)")
    prepare.add_argument("--support-boundary", type=Path,
                         help="measured, candidate-specific successor case table (required for a successor)")
    prepare.add_argument("--process-controls", action="store_true",
                         help="run the bounded local process controls (local scope only)")
    commands.add_parser("validate", help="independently validate an existing packet").add_argument("directory", type=Path)
    measure = commands.add_parser("measure-boundary", help="draft a successor table from complete new receipts")
    measure.add_argument("--inputs", type=Path, required=True)
    measure.add_argument("--source-root", type=Path)
    measure.add_argument("--source-builds-root", type=Path)
    measure.add_argument("--receipt", type=Path, action="append", required=True)
    measure.add_argument("--out", type=Path, required=True, help="new table file (must not exist)")
    args = parser.parse_args(arguments)
    try:
        if args.command == "validate":
            result = validate_packet(args.directory)
        elif args.command == "measure-boundary":
            if args.out.exists() or args.out.is_symlink():
                raise InvalidPacket("successor table output already exists")
            selection, data, scope = load_document(args.inputs, validate_inputs)
            if scope != "owner-selected":
                raise InvalidPacket("successor measurement lacks owner-selected inputs")
            if args.source_builds_root is not None and "tooling" not in selection:
                raise InvalidPacket("source builds path requires selected tooling pins")
            binding = (preparation.source_binding(preparation.ROOT, selection["tooling"], args.source_root, args.source_builds_root)
                       if "tooling" in selection else preparation.source_binding(preparation.ROOT, args.source_root)
                       if args.source_root is not None else preparation.source_binding())
            preparation.validate_selected_source(selection, binding)
            qualification.validate_candidate_source(binding, selection)
            preparation.check_current_source(binding)
            table = measure_successor_support_table(selection, [parse_json(regular(path)) for path in args.receipt],
                {"source_binding": binding, "inputs": {"value": selection, "sha256": preparation.digest(data)}})
            write_json(args.out, table)
            result = {"out": str(args.out.absolute()), "status": table["status"], "candidate": table["candidate"],
                      "case_count": sum(len(entry["cases"]) for entry in table["lanes"].values()), "p1r_usable": False}
        else:
            packet = create_packet(args.out, inputs=args.inputs, decisions=args.decisions,
                                   candidate_evidence=args.candidate_evidence, rollback_evidence=args.rollback_evidence,
                                   receipts=tuple(args.receipt), process_controls=args.process_controls,
                                   comparison_evidence=tuple(args.comparison_evidence), support_boundary=args.support_boundary,
                                   source_root=args.source_root, source_builds_root=args.source_builds_root)
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
