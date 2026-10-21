#!/usr/bin/env python3
"""Execute the approved immutable published P1R selection in a fresh owned invocation."""
import argparse
import json
import os
from pathlib import Path
import sys
import uuid

# -I inventory invocations retain their literal command and require an explicit tools path.
sys.path.insert(0, str(Path(__file__).resolve().parent))
import p1r_evidence_storage as storage
from p1r_published_executor import (Executor, InvalidPacket, canonical, qualification, postgres_inventory,
                                    redis_inventory, validate_execution_inputs, validate_observation_inputs)


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    observe = commands.add_parser("observe", help="observe all six already approved execution selections")
    observe.add_argument("--out", type=Path)
    observe.add_argument("--planning-observations", type=Path, required=True)
    observe.add_argument("--selection-reference", required=True)
    execute = commands.add_parser("run", help="run all canonical families and selected additions")
    execute.add_argument("--out", type=Path)
    execute.add_argument("--inputs", type=Path, required=True)
    for entry in (observe, execute):
        entry.add_argument("--summary-out", type=Path, help="export one bounded diagnostic attempt summary")
        entry.add_argument("--source-root", type=Path)
        entry.add_argument("--source-builds-root", type=Path)
    inventory = commands.add_parser("inventory", help="read an exactly owned state fixture; no runtime application access")
    inventory.add_argument("--container", required=True)
    inventory.add_argument("--invocation", required=True)
    inventory.add_argument("--backend", choices=("state.postgresql", "state.redis"), default="state.postgresql")
    args = parser.parse_args(arguments)
    worker, isolation, code, failure = None, None, 2, None
    worker_ready, summary_checked = False, False
    def construct(factory, controller, *arguments, **keywords):
        nonlocal worker, isolation, worker_ready
        if args.summary_out is None:
            instance = factory(*arguments, **keywords)
            if not controller:
                worker_ready = True
            return instance
        # Retain the actual instance if initialization fails, as the standalone
        # runner does. Constructor ownership markers decide what may be exported.
        instance = factory.__new__(factory)
        if controller:
            isolation = instance
        else:
            worker = instance
        factory.__init__(instance, *arguments, **keywords)
        if not controller:
            worker_ready = True
        return instance
    def perform():
        nonlocal worker, isolation, code, failure, summary_checked
        try:
            if args.command == "inventory":
                reader = postgres_inventory if args.backend == "state.postgresql" else redis_inventory
                sys.stdout.buffer.write(canonical(reader(args.container, args.invocation)))
                return 0
            # Resolve every path while still in the caller's original working directory.
            args.out = (args.out or storage.default_root() / ("attempt-" + uuid.uuid4().hex) / "packet").absolute()
            if args.summary_out is not None:
                storage.validate_summary_destination(args.out, args.summary_out,
                    isolation=args.out.with_name(args.out.name + "-isolation"))
                summary_checked = True
            args.source_root = args.source_root.absolute() if args.source_root is not None else None
            args.source_builds_root = args.source_builds_root.absolute() if args.source_builds_root is not None else None
            if args.command == "run":
                args.inputs = args.inputs.absolute()
                inputs, data, scope = qualification.load_document(args.inputs, qualification.validate_inputs)
                qualification.require(scope == "owner-selected", "synthetic selections cannot execute published qualification")
                validate_execution_inputs(inputs, args.source_root, args.source_builds_root)
            else:
                args.planning_observations = args.planning_observations.absolute()
                planning_data = qualification.regular(args.planning_observations)
                planning = qualification.parse_json(planning_data)
                validate_observation_inputs(planning, args.source_root, args.source_builds_root)
            if not os.environ.get("HEXALITH_P1R_ISOLATION"):
                from p1r_isolation import IsolatedRunner
                isolation = construct(IsolatedRunner, True, args.out.with_name(args.out.name + "-isolation"),
                                           **({"source_root": args.source_root} if args.source_root is not None else {}),
                                           **({"source_builds_root": args.source_builds_root} if args.source_builds_root is not None else {}))
                forwarded = [args.command, "--out", str(args.out)]
                forwarded += (["--inputs", str(args.inputs)] if args.command == "run" else
                              ["--planning-observations", str(args.planning_observations), "--selection-reference", args.selection_reference])
                if args.source_root is not None:
                    forwarded += ["--source-root", str(args.source_root)]
                if args.source_builds_root is not None:
                    forwarded += ["--source-builds-root", str(args.source_builds_root)]
                code = isolation.run(["python3", str(Path(__file__).resolve()), *forwarded])
                responses = []
                stdout = (storage.command_text(isolation.result["command_stdout_command"], isolation.storage.context)
                          if "command_stdout_command" in isolation.result else isolation.result.get("command_stdout", ""))
                for line in stdout.splitlines():
                    try:
                        value = json.loads(line)
                        if isinstance(value, dict):
                            responses.append(value)
                    except json.JSONDecodeError:
                        pass
                for value in responses or [{"out": str(args.out), "errors": isolation.result["errors"], "p1r_usable": False}]:
                    print(json.dumps(dict(value, isolation_out=str(isolation.output),
                                          isolation_passed=isolation.result.get("passed", code == 0),
                                          isolation_errors=isolation.result["errors"]), sort_keys=True), flush=True)
                return code
            from p1r_isolation import private_proof, OUTER_MARKER
            private_proof(os.environ.get(OUTER_MARKER))
            if args.command == "observe":
                worker = construct(Executor, False, args.out, source_root=args.source_root, source_builds_root=args.source_builds_root,
                                  tooling=planning.get("tooling"), observation=planning,
                                  observation_sha256=qualification.digest(planning_data))
                worker.observe_inputs(planning, args.selection_reference)
                worker.cleanup()
            else:
                worker = construct(Executor, False, args.out, inputs, args.source_root, args.source_builds_root, inputs_sha256=qualification.digest(data))
                worker.inputs_sha256 = qualification.digest(data)
                worker.result["inputs_sha256"] = worker.inputs_sha256
                worker.save()
                worker.execute()
            print(json.dumps({"out": str(args.out.absolute()), "errors": worker.errors, "p1r_usable": False}, sort_keys=True))
            code = 2 if worker.errors else 0
            return code
        except (Exception, KeyboardInterrupt) as error:
            code, failure = (130 if isinstance(error, KeyboardInterrupt) else 2), str(error) if isinstance(error, InvalidPacket) else type(error).__name__
            if worker_ready and worker is not None and isinstance(getattr(worker, "errors", None), list) and isinstance(getattr(worker, "result", None), dict):
                worker.errors.append(str(error) if isinstance(error, InvalidPacket) else type(error).__name__)
                try:
                    worker.cleanup()
                except (Exception, KeyboardInterrupt) as cleanup_error:
                    worker.errors.append("cleanup: " + str(cleanup_error))
                    worker.save()
            print(json.dumps({"error": str(error), "p1r_usable": False}), file=sys.stderr)
            return code

    code = perform()
    if args.command != "inventory" and args.summary_out is not None and summary_checked:
        try:
            # Executor assigns its invocation only after exclusively creating its
            # output. The controller explicitly owns a freshly created parent.
            owned_worker = worker is not None and isinstance(getattr(worker, "invocation", None), str)
            owned_controller = isolation is not None and getattr(isolation, "owns_attempt", False) is True
            if not owned_worker and not owned_controller:
                for checkout in (args.source_root, args.source_builds_root):
                    if checkout is not None:
                        storage.external_path(storage.default_root() / ".summary-artifacts", checkout)
            store = (worker.storage if owned_worker and hasattr(worker, "storage") else
                isolation.storage if owned_controller and hasattr(isolation, "storage") else
                storage.Store(storage.default_root() / ".summary-artifacts", storage.context(uuid.uuid4().hex, qualification.ROOT)))
            packet = worker.output if owned_worker else args.out if owned_controller else store.root / ("absent-" + uuid.uuid4().hex)
            outer = isolation.output if owned_controller else None
            storage.export_attempt(packet, args.summary_out, store, isolation=outer, exit_code=code, error=failure)
        except (Exception, KeyboardInterrupt) as summary_error:
            print(json.dumps({"error": str(summary_error) if isinstance(summary_error, InvalidPacket)
                else type(summary_error).__name__, "summary_exported": False, "p1r_usable": False}), file=sys.stderr)
            code = 2
    return code


if __name__ == "__main__":
    raise SystemExit(main())
