#!/usr/bin/env python3
"""Verify this investigation's retained bytes; never infer broker qualification."""
import argparse
import hashlib
import json
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet", type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[5])
    parser.add_argument("--require-qualified", action="store_true")
    args = parser.parse_args()
    index = json.loads((args.packet / "index.json").read_text())
    errors = []
    for relative, expected in index["files"].items():
        path = (args.packet / relative).resolve()
        if not path.is_relative_to(args.packet.resolve()) or not path.is_file() or digest(path) != expected:
            errors.append("retained bytes differ: " + relative)
    for name in ("workspace-preservation.json", "repository-inputs.json"):
        data = json.loads((args.packet / name).read_text())
        bindings = data["files"] if isinstance(data["files"], dict) else {
            x["path"]: x["sha256"] for x in data["files"]}
        for relative, expected in bindings.items():
            path = (args.repo_root / relative).resolve()
            if not path.is_relative_to(args.repo_root.resolve()) or not path.is_file() or digest(path) != expected:
                errors.append("workspace input changed: " + relative)
    for observation in ("cluster-before.json", "cluster-after.json"):
        data = json.loads((args.packet / observation).read_text())
        if data["apiServer"] != "https://192.168.1.30:6443":
            errors.append("wrong cluster endpoint")
        if data["collectorSha256"] != digest(args.packet / "inspect-cluster.py"):
            errors.append("cluster collector binding differs")
        for receipt in data["receipts"]:
            if receipt["exitCode"] != 0 or receipt["argv"][3] not in ("get", "version"):
                errors.append("unsuccessful or unexpected cluster command")
    identities = json.loads((args.packet / "public-identities/identities.json").read_text())
    if identities["collectorSha256"] != digest(args.packet / "capture-identities.py"):
        errors.append("public identity collector binding differs")
    for row in identities["httpReceipts"]:
        if digest(args.packet / "public-identities" / row["file"]) != row["sha256"]:
            errors.append("HTTP bytes differ: " + row["file"])
    for image in identities["images"]:
        if image["immutable"].split("@sha256:")[-1] != digest(args.packet / "public-identities" / image["file"]):
            errors.append("OCI index differs: " + image["tag"])
    for binding in index["planningInputs"]:
        if digest(args.repo_root / binding["path"]) != binding["sha256"]:
            errors.append("planning input changed: " + binding["path"])
    before = json.loads((args.packet / "cluster-before.json").read_text())
    after = json.loads((args.packet / "cluster-after.json").read_text())
    def workload_specs(data):
        return {(x["namespace"], x["kind"], x["name"]): {
            k: v for k, v in x.items() if k not in ("readyReplicas", "readyNumber")}
            for x in data["workloads"]}
    if workload_specs(before) != workload_specs(after):
        errors.append("observed workload identities/specification fields changed")
    print(json.dumps({"byteConsistency": not errors, "errors": errors,
        "brokerExecution": "NOT RUN", "productionEligibility": "FAIL",
        "reason": "Investigation only; live broker evidence and required owner records are absent."}, indent=2))
    raise SystemExit(1 if errors or args.require_qualified else 0)


if __name__ == "__main__":
    main()
