#!/usr/bin/env python3
"""Independently audit one fresh 3.125.1 execution; never grant owner decisions."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("verifier-root", "source-root", "execution", "inputs", "decisions", "out"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=False)
    (args.out / "receipts").mkdir()
    sys.path.insert(0, str(args.verifier_root / "tools"))
    import p1r_qualification as preparation
    import p1r_qualification_runtime as runtime
    import p1r_published_qualification as qualification

    result = {"schema": "hexalith.p1r.fresh-independent-audit.v1", "invocation": uuid.uuid4().hex,
              "started_utc": preparation.stamp(), "finished_utc": None, "valid_evidence": False,
              "qualified": False, "p1r_usable": False, "errors": [], "commands": [],
              "packages": {}, "lanes": {}, "recovery": {}}
    script = Path(__file__).resolve()
    result["audit_script"] = {"path": str(script), "sha256": preparation.digest(preparation.regular(script))}

    def save():
        preparation.write_json(args.out / "audit.json", result)

    def command(label, argv):
        started = preparation.stamp()
        process = subprocess.run(list(map(str, argv)), cwd=args.out,
                                 capture_output=True, text=True, timeout=600)
        row = {"label": label, "argv": list(map(str, argv)), "cwd": str(args.out),
               "started_utc": started, "finished_utc": preparation.stamp(),
               "exit_code": process.returncode, "stdout": process.stdout, "stderr": process.stderr}
        result["commands"].append(row)
        save()
        return row

    save()
    try:
        preparation.require(preparation.ROOT == args.verifier_root.resolve(), "audit verifier root substituted")
        selection, raw_inputs, scope = qualification.load_document(args.inputs, qualification.validate_inputs)
        preparation.require(scope == "owner-selected" and selection["candidate"]["version"] == "3.125.1",
                            "audit selection differs from approved candidate")
        decisions, _, _ = qualification.load_document(args.decisions, qualification.validate_decisions)
        preparation.require(decisions["inputs_sha256"] == preparation.digest(raw_inputs), "decision input binding differs")
        result["inputs_sha256"] = preparation.digest(raw_inputs)
        result["decisions"] = decisions
        binding = preparation.source_binding(args.verifier_root, args.source_root)
        qualification.validate_candidate_source(binding, selection)
        preparation.write_json(args.out / "source-binding.json", binding)
        sdk = preparation.regular(args.source_root / "global.json")
        (args.out / "global.json").write_bytes(sdk)
        result["audit_sdk_pin_sha256"] = preparation.digest(sdk)
        execution = qualification.parse_json(preparation.regular(args.execution / "execution.json"))
        source_raw = preparation.regular(args.execution / "executor-source.json")
        preparation.require(execution.get("errors") == [] and execution.get("finished_utc")
                            and execution.get("inputs_sha256") == result["inputs_sha256"],
                            "executor failed, incomplete, or selected different inputs")
        preparation.require(qualification.parse_json(source_raw) == binding
                            and preparation.digest(source_raw) == execution.get("executor_source_sha256"),
                            "independent source closures differ from execution")
        context = {"inputs": {"value": selection, "scope": scope, "sha256": result["inputs_sha256"]},
                   "source_binding": binding, "verified": {}}
        roles = ["candidate", *("comparison-" + version for version in qualification.COMPARISON_VERSIONS)]
        for role in roles:
            preparation.check_current_source(binding)
            row, verified = qualification.execute_package_lane(
                args.out, result["invocation"], role, args.execution / "consumers" / role,
                context["inputs"], qualification.default_verifier)
            preparation.require(verified is not None, "independent package proof failed: " + role)
            qualification.validate_package_receipt(args.out, result["invocation"], role, row["receipts"][0],
                                                   context["inputs"], result["started_utc"], preparation.stamp())
            context["verified"][role] = verified
            result["packages"][role] = row
            save()
        cases = []
        paths = execution.get("receipts")
        preparation.require(isinstance(paths, list) and len(paths) == len(set(paths)), "receipt index missing or duplicate")
        for name in paths:
            path = Path(name)
            preparation.require(path.parent == args.execution / "receipts", "receipt escaped retained execution")
            receipt = qualification.parse_json(preparation.regular(path))
            group, lane, outcome = qualification.receipt_outcome(receipt, context)
            target = result["recovery"] if group == "recovery" else result["lanes"]
            preparation.require(lane not in target, "duplicate audited lane")
            target[lane] = {"path": str(path), "sha256": preparation.digest(preparation.regular(path)), **outcome}
            if group != "recovery":
                cases.append((path, receipt))
            save()
        inventory = qualification.executed_case_inventory(selection)
        preparation.require(set(result["lanes"]) == set(inventory), "incomplete scenario/addition coverage")
        preparation.require(set(result["recovery"]) == {"candidate-restore", "container-cleanup"},
                            "incomplete required recovery/cleanup coverage")
        result["case_count"] = sum(len(receipt["cases"]) for _, receipt in cases)
        preparation.require(result["case_count"] == sum(map(len, inventory.values())), "case count differs from inventory")
        result["lane_assertions"] = {key: sum(receipt["assertions"][key] for _, receipt in cases)
                                     for key in ("attempted", "passed", "failed")}
        result["process_controls"] = runtime.new_process_section()
        runtime.run_process_controls(result["process_controls"], args.out, result["invocation"],
                                     binding["python"]["path"], lambda: preparation.check_current_source(binding), save)
        retained, count = runtime.validate_process_controls(
            args.out, result["process_controls"], result["invocation"], binding["python"]["path"],
            result["started_utc"], preparation.stamp())
        preparation.require(all(row["execution"] == "passed" for row in result["process_controls"]["rows"]),
                            "real process controls failed")
        result["process_control_assertions"] = count
        result["process_control_files"] = sorted(retained)
        cli = args.verifier_root / "tools/p1r-published-qualification.py"
        table = args.out / "successor-support-boundary.json"
        receipt_args = [item for path, _ in cases for item in ("--receipt", str(path))]
        measured = command("measure-boundary", ["python3", cli, "measure-boundary", "--inputs", args.inputs,
                                                "--out", table, *receipt_args])
        preparation.require(measured["exit_code"] == 0, "fresh boundary measurement failed")
        value = qualification.parse_json(preparation.regular(table))
        preparation.require(value["status"] == "measured-pending-disposition" and value["disposition"] is None,
                            "audit must not invent architect disposition")
        packet = args.out / "strict-packet"
        argv = ["python3", cli, "prepare", "--out", packet, "--source-root", args.source_root,
                "--inputs", args.inputs, "--decisions", args.decisions, "--support-boundary", table,
                "--candidate-evidence", args.execution / "consumers/candidate", "--process-controls"]
        for role in roles[1:]:
            argv += ["--comparison-evidence", args.execution / "consumers" / role]
        for path in paths:
            argv += ["--receipt", path]
        prepared = command("strict-prepare", argv)
        validated = command("strict-validate", ["python3", cli, "validate", packet])
        preparation.require(prepared["exit_code"] == validated["exit_code"] == 2
                            and "successor support boundary lacks measured owner disposition" in prepared["stderr"],
                            "strict gate did not retain the expected missing-disposition refusal")
        preparation.check_current_source(binding)
        preparation.require(preparation.digest(preparation.regular(script)) == result["audit_script"]["sha256"],
                            "audit script changed during verification")
        result["source_checked_after_all_commands"] = True
        result["valid_evidence"] = True
    except (Exception, KeyboardInterrupt) as error:
        result["errors"].append({"type": type(error).__name__, "message": str(error)})
    finally:
        result["finished_utc"] = preparation.stamp()
        save()
    print(json.dumps({key: result[key] for key in ("valid_evidence", "errors", "qualified", "p1r_usable")}, sort_keys=True))
    return 0 if result["valid_evidence"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
