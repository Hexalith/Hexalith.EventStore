#!/usr/bin/env python3
"""Retain a value-filtered, read-only inventory of the explicitly named cluster."""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import subprocess


def now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def identity(item):
    m = item["metadata"]
    return {"namespace": m.get("namespace"), "name": m["name"],
            "uid": m["uid"], "generation": m.get("generation")}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("output already exists; choose a new observation path")
    base = ["kubectl", "--context=jpiquot@local", "--request-timeout=20s"]
    receipts = []

    def query(argv):
        started = now()
        result = subprocess.run(base + argv, capture_output=True, text=True, timeout=30)
        receipts.append({"argv": base + argv, "startedUtc": started, "endedUtc": now(),
                         "exitCode": result.returncode,
                         "stderr": result.stderr.strip()})
        result.check_returncode()
        return json.loads(result.stdout)

    # Read only the endpoint from kubeconfig; never serialize user credentials.
    server = subprocess.check_output(base + ["config", "view", "--minify", "-o",
        "jsonpath={.clusters[0].cluster.server}"], text=True).strip()
    if server != "https://192.168.1.30:6443":
        raise SystemExit("refusing unexpected API server: " + server)
    observed = {"startedUtc": now(), "apiServer": server, "context": "jpiquot@local",
                "access": "GET/LIST only; no Secret resources, exec, port-forward or mutations",
                "version": query(["version", "-o", "json"])}
    observed["nodes"] = [{**identity(x), "addresses": x["status"]["addresses"],
        "capacity": x["status"]["capacity"], "allocatable": x["status"]["allocatable"],
        "nodeInfo": x["status"]["nodeInfo"], "conditions": x["status"]["conditions"]}
        for x in query(["get", "nodes", "-o", "json"])["items"]]
    observed["workloads"] = []
    for x in query(["get", "deployments,statefulsets,daemonsets", "-A", "-o", "json"])["items"]:
        spec = x["spec"]["template"]["spec"]
        observed["workloads"].append({**identity(x), "kind": x["kind"],
            "replicas": x["spec"].get("replicas"), "readyReplicas": x["status"].get("readyReplicas"),
            "readyNumber": x["status"].get("numberReady"), "strategy": x["spec"].get("updateStrategy", x["spec"].get("strategy")),
            "helmRelease": {k: v for k, v in x["metadata"].get("annotations", {}).items() if k.startswith("meta.helm.sh/")},
            "daprAnnotations": {k: v for k, v in x["spec"]["template"]["metadata"].get("annotations", {}).items() if k.startswith("dapr.io/")},
            "containers": [{"name": c["name"], "image": c["image"], "resources": c.get("resources", {}),
                "envNames": [e["name"] for e in c.get("env", [])]} for c in spec["containers"]],
            "claims": x["spec"].get("volumeClaimTemplates", []) and [
                {"name": c["metadata"]["name"], "spec": c["spec"]} for c in x["spec"]["volumeClaimTemplates"]]})
    observed["pods"] = [{**identity(x), "node": x["spec"].get("nodeName"), "phase": x["status"]["phase"],
        "containers": [{k: c.get(k) for k in ("name", "image", "imageID", "ready", "restartCount")}
                       for c in x["status"].get("containerStatuses", [])]}
        for x in query(["get", "pods", "-A", "-o", "json"])["items"]]
    observed["storageClasses"] = [{**identity(x), **{k: x.get(k) for k in (
        "provisioner", "reclaimPolicy", "volumeBindingMode", "allowVolumeExpansion")}}
        for x in query(["get", "storageclasses", "-o", "json"])["items"]]
    observed["claims"] = [{**identity(x), "spec": x["spec"],
        "phase": x["status"]["phase"], "capacity": x["status"].get("capacity")}
        for x in query(["get", "pvc", "-A", "-o", "json"])["items"]]
    public_metadata = {"host", "port", "database", "tableName", "metadataTableName", "redisHost",
        "actorStateStore", "keyPrefix", "durable", "deletedWhenUnused", "deliveryMode", "autoAck",
        "publisherConfirm", "prefetchCount", "enableDeadLetter", "publishingScopes", "subscriptionScopes"}
    observed["components"] = []
    for x in query(["get", "components.dapr.io", "-A", "-o", "json"])["items"]:
        observed["components"].append({**identity(x), "type": x["spec"]["type"],
            "version": x["spec"]["version"], "scopes": x.get("scopes", []), "auth": x.get("auth"),
            "metadata": [{"name": m["name"], **({"secretKeyRef": m["secretKeyRef"]} if "secretKeyRef" in m
                else {"value": m.get("value")} if m["name"] in public_metadata else {"value": "[not retained]"})}
                for m in x["spec"].get("metadata", [])]})
    for resource, key in [("services", "services"), ("networkpolicies", "networkPolicies"),
                          ("poddisruptionbudgets", "disruptionBudgets")]:
        observed[key] = [{**identity(x), "spec": x["spec"]}
            for x in query(["get", resource, "-A", "-o", "json"])["items"]]
    for resource, key in [("clusters.postgresql.cnpg.io", "postgresClusters"),
                          ("backups.velero.io", "backups"), ("schedules.velero.io", "backupSchedules")]:
        observed[key] = [{**identity(x), "imageName": x["spec"].get("imageName"),
            "instances": x["spec"].get("instances"), "storage": x["spec"].get("storage"),
            "phase": x.get("status", {}).get("phase"), "lastSuccessfulBackup": x.get("status", {}).get("lastSuccessfulBackup"),
            "completionTimestamp": x.get("status", {}).get("completionTimestamp"),
            "schedule": x["spec"].get("schedule"), "includedNamespaces": x["spec"].get("includedNamespaces", x["spec"].get("template", {}).get("includedNamespaces"))}
            for x in query(["get", resource, "-A", "-o", "json"])["items"]]
    observed["endedUtc"] = now()
    observed["receipts"] = receipts
    observed["collectorSha256"] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(observed, indent=2) + "\n")
    print(json.dumps({"path": str(args.output), "sha256": hashlib.sha256(args.output.read_bytes()).hexdigest(),
        "workloads": len(observed["workloads"]), "components": len(observed["components"]), "commands": len(receipts)}))


if __name__ == "__main__":
    main()
