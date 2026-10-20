#!/usr/bin/env python3
"""Independently verify a retained, non-authorizing broker packet."""
import argparse
import json
from pathlib import Path
from dapr_broker_qualification import validate_packet


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("packet", type=Path)
    parser.add_argument("--allow-failures", action="store_true", help="verify truthful negative evidence without converting it into a pass")
    parser.add_argument("--require-production-authority", action="store_true")
    args = parser.parse_args()
    try:
        result = validate_packet(args.packet, allow_failures=args.allow_failures, require_authority=args.require_production_authority)
        print(json.dumps(result, indent=2))
        return 0
    except (ValueError, KeyError, TypeError, OSError) as error:
        print(f"FAIL: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
