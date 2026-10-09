#!/usr/bin/env python3
"""Execute the approved immutable published P1R selection in a fresh owned invocation."""
import argparse
import json
from pathlib import Path
import sys

# -I inventory invocations retain their literal command and require an explicit tools path.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from p1r_published_executor import Executor, InvalidPacket, canonical, qualification, redis_inventory


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    observe = commands.add_parser("observe", help="observe all six already approved execution selections")
    observe.add_argument("--out", type=Path, required=True)
    observe.add_argument("--planning-observations", type=Path, required=True)
    observe.add_argument("--selection-reference", required=True)
    execute = commands.add_parser("run", help="run all canonical families and selected additions")
    execute.add_argument("--out", type=Path, required=True)
    execute.add_argument("--inputs", type=Path, required=True)
    inventory = commands.add_parser("inventory", help="read an exactly owned Redis fixture; no runtime application access")
    inventory.add_argument("--container", required=True)
    inventory.add_argument("--invocation", required=True)
    args = parser.parse_args(arguments)
    worker = None
    try:
        if args.command == "inventory":
            sys.stdout.buffer.write(canonical(redis_inventory(args.container, args.invocation)))
            return 0
        if args.command == "observe":
            worker = Executor(args.out)
            worker.observe_inputs(json.loads(args.planning_observations.read_bytes()), args.selection_reference)
            worker.cleanup()
        else:
            inputs, data, scope = qualification.load_document(args.inputs, qualification.validate_inputs)
            qualification.require(scope == "owner-selected", "synthetic selections cannot execute published qualification")
            worker = Executor(args.out, inputs)
            worker.inputs_sha256 = qualification.digest(data)
            worker.result["inputs_sha256"] = worker.inputs_sha256
            worker.save()
            worker.execute()
        print(json.dumps({"out": str(args.out.absolute()), "errors": worker.errors, "p1r_usable": False}, sort_keys=True))
        return 2 if worker.errors else 0
    except (Exception, KeyboardInterrupt) as error:
        if worker is not None:
            worker.errors.append(str(error) if isinstance(error, InvalidPacket) else type(error).__name__)
            try:
                worker.cleanup()
            except (Exception, KeyboardInterrupt) as cleanup_error:
                worker.errors.append("cleanup: " + str(cleanup_error))
                worker.save()
        print(json.dumps({"error": str(error), "p1r_usable": False}), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
