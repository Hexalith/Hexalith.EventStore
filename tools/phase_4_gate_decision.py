"""Issue a gate decision through fresh authenticated consumption.

The saved document reports this invocation only. Every later consumer must call
the platform verifier again; neither a local PASS nor an earlier decision seals it.
"""

import argparse
import json
import os
from pathlib import Path
import re
import stat
import sys
import tempfile

import phase_4_transition as C

DECISION_SCHEMA = "hexalith.eventstore.phase-4-gate-decision.v1"
GATE = "G-OQ8"
SCOPE = "governing-design-availability"
SCOPES = {GATE: SCOPE, "G-TENANT": "canonical-tenant-boundary", "G-STATUS-ID": "generated-command-status-link"}


def decide(control, run_id, attempt, allow_tenant=False, allow_status_id=False):
    """Accept only this gate/transition and require a freshly authenticated seal."""
    gate = control.request["gate"]
    C.require((gate == GATE or gate == "G-TENANT" and allow_tenant or gate == "G-STATUS-ID" and allow_status_id)
              and control.request["transition"] == "high-risk-gate-pass"
              and control.request["subject_mode"] == "files"
              and not control.request["nfr_ids"],
              "ADAPTER_UNAVAILABLE", "request is outside the supported decision scope")
    C.require(C.positive(run_id) and C.positive(attempt), "INPUT_INVALID",
              "decision requires a positive evaluation run-id and attempt", 2)
    return report(control.consume(run_id, attempt), control.request["gate"])


def report(result, gate=GATE):
    """Retain computed assurance, exact inputs and seal without broadening scope."""
    return {**{key: value for key, value in result.items() if key != "phase"},
            "schema": DECISION_SCHEMA, "gate": gate, "scope": SCOPES[gate]}


class Parser(argparse.ArgumentParser):
    """Keep malformed invocation failures inside the structured diagnostic path."""

    def error(self, message):
        raise C.Refusal("INPUT_INVALID", message, 2)


def write_report(path, result):
    """Replace previous reports atomically and reject symbolic output paths."""
    path = path.absolute()
    C.require(not any(part.is_symlink() for part in (path, *path.parents)),
              "INPUT_INVALID", "diagnostic path cannot contain symbolic links", 2)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent,
                                         prefix=".gate-decision-", delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(json.dumps(result, indent=2, sort_keys=True) + "\n")
        temporary.replace(path)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def main(argv=None):
    parser = Parser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    source = parser.add_mutually_exclusive_group()
    source.add_argument("--request", type=Path)
    source.add_argument("--request-env", action="store_true", help="read PHASE4_REQUEST_JSON")
    parser.add_argument("--allow-tenant", action="store_true", help="enable the dedicated G-TENANT decision")
    parser.add_argument("--allow-status-id", action="store_true", help="enable the dedicated G-STATUS-ID decision")
    parser.add_argument("--run-id")
    parser.add_argument("--attempt")
    parser.add_argument("--result", type=Path)
    arguments = list(sys.argv[1:] if argv is None else argv)
    # Retain the destination even when argparse refuses an incomplete/unknown input.
    destination = None
    for index, argument in enumerate(arguments):
        if argument == "--result" and index + 1 < len(arguments) and not arguments[index + 1].startswith("--"):
            destination = Path(arguments[index + 1])
        elif argument.startswith("--result="):
            destination = Path(argument.split("=", 1)[1])
    control = None
    gate = GATE
    try:
        args = parser.parse_args(arguments)
        destination = args.result
        C.require(destination is not None, "INPUT_INVALID", "--result is required", 2)
        C.require(args.request is not None or args.request_env, "INPUT_INVALID", "request is required", 2)
        if args.request_env:
            content = os.environ.get("PHASE4_REQUEST_JSON", "").encode()
        else:
            path = args.request.absolute()
            C.require(not any(part.is_symlink() for part in (path, *path.parents))
                      and stat.S_ISREG(path.stat().st_mode), "INPUT_INVALID", "request must be a regular file", 2)
            with path.open("rb") as stream:
                content = stream.read(C.MAX_BYTES + 1)
        C.require(len(content) <= C.MAX_BYTES, "INPUT_INVALID", "request too large", 2)
        request = C.parse(content)
        if args.allow_tenant and isinstance(request, dict) and request.get("gate") == "G-TENANT":
            gate = "G-TENANT"
        if args.allow_status_id and request.get("gate") == "G-STATUS-ID":
            gate = "G-STATUS-ID"
        if request.get("transition") in C.M.TRANSITIONS:
            C.require(request["transition"] == "high-risk-gate-pass", "ADAPTER_UNAVAILABLE",
                      "request is outside the supported decision scope")
        request = C.validate_request(request)
        C.require(all(isinstance(value, str) and re.fullmatch(r"[1-9][0-9]{0,18}", value)
                      for value in (args.run_id, args.attempt)),
                  "INPUT_INVALID", "decision requires positive run-id and attempt", 2)
        control = C.Control(args.root.resolve(), request, C.GitHub())
        result = decide(control, int(args.run_id), int(args.attempt), args.allow_tenant, args.allow_status_id)
        code = 0
    except (C.Refusal, OSError, UnicodeError, ValueError, KeyError, TypeError, AttributeError, RecursionError) as error:
        if not isinstance(error, C.Refusal):
            error = C.Refusal("INPUT_INVALID", "malformed or unreadable input: " + type(error).__name__, 2)
        print(error.code + ": " + str(error), file=sys.stderr)
        # Failed invocations carry no effective achieved assurance or seal.
        result = report(control.result("consume", [error.code]) if control else {
            "schema": C.RESULT_SCHEMA, "result": "FAIL", "codes": [error.code], "binding": {},
            "required_level": "none", "achieved_level": "none", "downstream_minimum": "none",
            "approvals": [], "approval_payloads": [], "predecessors": [], "seal": None}, gate)
        result.update(achieved_level="none", downstream_minimum="none", seal=None)
        code = error.exit_code
    try:
        if destination is not None:
            write_report(destination, result)
    except (C.Refusal, OSError, ValueError):
        print("INPUT_INVALID: cannot write diagnostics", file=sys.stderr)
        result.update(result="FAIL", codes=["INPUT_INVALID"], achieved_level="none",
                      downstream_minimum="none", seal=None)
        code = 2
    print(json.dumps(result, sort_keys=True))
    return code
