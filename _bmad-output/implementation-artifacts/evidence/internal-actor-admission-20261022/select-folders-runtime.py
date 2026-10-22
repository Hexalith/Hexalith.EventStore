#!/usr/bin/env python3
"""Preflight exact frozen SDK images; --apply selects them in built Debug outputs.

Run only after the Folders source-reference build. Default is read-only.
Every replaced output is preserved by hash in the new receipt directory.
No source, package pin or historical evidence is modified.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--consumer-root", type=Path, required=True)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--receipt", type=Path, help="New directory required with --apply")
    args = parser.parse_args()
    packet = Path(__file__).resolve().parent
    manifest = json.loads((packet / "handoff-inventory.json").read_text())
    inputs = {}
    for row in manifest["files"]:
        path = packet / row["path"]
        if sha(path) != row["sha256"]:
            raise ValueError("Frozen input changed: " + str(path))
        inputs[path.name] = path
    projects = ["src/Hexalith.Folders.Server", "src/Hexalith.Folders.Workers",
                "src/Hexalith.Folders.EventStore", "src/Hexalith.Folders.AppHost",
                "src/Hexalith.Folders.UI", "tests/Hexalith.Folders.AppHost.Tests",
                "tests/Hexalith.Folders.Tests", "tests/tools/Task36FoldObserver",
                "references/Hexalith.Tenants/src/Hexalith.Tenants.Server",
                "references/Hexalith.Tenants/src/Hexalith.Tenants"]
    targets = [args.consumer_root.resolve() / p / "bin/Debug/net10.0" for p in projects]
    missing = [str(p) for p in targets if not p.is_dir()]
    if missing:
        raise ValueError("Required output directories absent: " + json.dumps(missing))
    candidate = json.loads(inputs["Hexalith.EventStore.DomainService.deps.json"].read_text())
    candidate_target = next(iter(candidate["targets"].values()))
    dependency_keys = []
    for prefix in ["Hexalith.Commons.UniqueIds/", "ByteAether.Ulid/"]:
        matches = [key for key in candidate_target if key.startswith(prefix)]
        if len(matches) != 1:
            raise ValueError("Ambiguous qualified dependency: " + prefix)
        dependency_keys += matches
    mandatory = {"Hexalith.EventStore.Contracts.dll", "Hexalith.EventStore.Contracts.pdb",
                 "Hexalith.EventStore.Client.dll", "Hexalith.EventStore.Client.pdb",
                 "Hexalith.EventStore.DomainService.dll", "Hexalith.EventStore.DomainService.pdb",
                 "Hexalith.Commons.UniqueIds.dll", "Hexalith.Commons.UniqueIds.pdb", "ByteAether.Ulid.dll"}
    if not mandatory.issubset(inputs):
        raise ValueError("Frozen dependency graph is incomplete")
    changes, probes = [], []
    for target in targets:
        for name, source in inputs.items():
            if source.suffix not in (".dll", ".pdb"):
                continue
            destination = target / name
            if destination.exists() or name in mandatory:
                changes.append({"source": str(source), "destination": str(destination),
                                "before": sha(destination) if destination.exists() else None,
                                "after": sha(source)})
        for path in target.glob("*.deps.json"):
            if path.name.startswith("Hexalith.EventStore."):
                continue
            document = json.loads(path.read_text())
            target_items = next(iter(document["targets"].values()))
            for key in dependency_keys:
                target_items[key] = candidate_target[key]
                document["libraries"][key] = candidate["libraries"][key]
            probes.append((path, document))
    report = {"apply": args.apply, "manifest_sha256": sha(packet / "handoff-inventory.json"),
              "required_outputs": [str(p) for p in targets], "selected_files": changes,
              "dependency_manifest_updates": [str(p) for p, _ in probes]}
    if args.apply:
        if args.receipt is None:
            parser.error("--apply requires --receipt pointing to a new directory")
        args.receipt.mkdir(parents=True, exist_ok=False)
        archive = args.receipt / "previous-output"
        archive.mkdir()
        def preserve(path):
            if path.exists():
                saved = archive / sha(path)
                if not saved.exists():
                    shutil.copy2(path, saved)
        for row in changes:
            destination = Path(row["destination"])
            preserve(destination)
            shutil.copy2(row["source"], destination)
            if sha(destination) != row["after"]:
                raise ValueError("Selected output drift: " + str(destination))
        manifest_changes = []
        for path, document in probes:
            before = sha(path)
            preserve(path)
            path.write_text(json.dumps(document, indent=2) + "\n")
            manifest_changes.append({"path": str(path), "before": before, "after": sha(path)})
        report["generated_manifest_changes"] = manifest_changes
        (args.receipt / "selection.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
