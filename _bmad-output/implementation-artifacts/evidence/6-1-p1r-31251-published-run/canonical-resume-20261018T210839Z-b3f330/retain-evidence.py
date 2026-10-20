#!/usr/bin/env python3
"""Retain an indexed byte copy after its existing execution has finished."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import shutil


def stamp():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def sha(path):
    result = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--index", type=Path, required=True)
    args = parser.parse_args()
    if args.out.exists() or args.index.exists():
        parser.error("copy and index must both be new")
    driver = args.source / "canonical-driver-command.json"
    state = json.loads(driver.read_bytes())
    if not state.get("finished_utc") or "exit_code" not in state:
        parser.error("existing canonical driver has not finished")
    args.out.mkdir(parents=True)
    result = {"started_utc": stamp(), "source": str(args.source),
              "copy": str(args.out), "files": [], "errors": [],
              "scope": "Indexed copy; originals retain cwd and physical assembly authority"}
    for path in sorted(args.source.rglob("*")):
        if "__pycache__" in path.parts:
            continue
        relative = path.relative_to(args.source)
        target = args.out / relative
        try:
            if path.is_symlink():
                target.parent.mkdir(parents=True, exist_ok=True)
                target.symlink_to(path.readlink())
                result["files"].append({"path": str(relative), "symlink": str(path.readlink())})
            elif path.is_dir():
                target.mkdir(exist_ok=True)
            elif path.is_file():
                before = sha(path)
                shutil.copyfile(path, target)
                if before != sha(target) or before != sha(path):
                    raise ValueError("source changed or copy differs")
                result["files"].append({"path": str(relative), "bytes": path.stat().st_size,
                                        "sha256": before})
        except Exception as error:
            result["errors"].append({"path": str(relative), "type": type(error).__name__,
                                     "message": str(error)})
    result["finished_utc"] = stamp()
    result["bytes"] = sum(row.get("bytes", 0) for row in result["files"])
    args.index.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({"files": len(result["files"]), "bytes": result["bytes"],
                      "errors": result["errors"]}))
    return 2 if result["errors"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
