#!/usr/bin/env python3
"""Command-line entry point for bounded P1R preparation and process evidence."""
import argparse
import json
from pathlib import Path
import sys

from p1r_qualification import InvalidPacket, create_packet, validate_packet


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("prepare", "run"):
        commands.add_parser(name).add_argument("--out", type=Path, required=True)
    commands.add_parser("validate").add_argument("directory", type=Path)
    args = parser.parse_args(arguments)
    try:
        if args.command == "validate":
            result = validate_packet(args.directory)
        else:
            packet = create_packet(args.out, args.command)
            result = {"out": str(args.out.absolute()), "qualified": False, "p1r_usable": False,
                      "preparation_execution": packet["preparation"]["execution"], "errors": packet["errors"]}
            validate_packet(args.out)
        print(json.dumps(result, sort_keys=True))
        return 0  # Valid preparation only; qualification flags always remain false.
    except (InvalidPacket, OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        reason = str(error) if isinstance(error, InvalidPacket) else type(error).__name__
        print(json.dumps({"valid": False, "qualified": False, "p1r_usable": False, "reason": reason}), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
