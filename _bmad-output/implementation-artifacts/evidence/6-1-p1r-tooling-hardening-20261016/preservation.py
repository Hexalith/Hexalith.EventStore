"""Capture tracked starting bytes and verify the narrow tooling-only delta."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
OWNER = HERE.parents[3]
WORKSPACE = OWNER.parents[1]
ALLOWED = {
    "tools/p1r_isolation.py",
    "tools/p1r_isolation_evidence.py",
    "tools/p1r_published_qualification.py",
    "tools/tests/test_p1r_isolation.py",
    "tools/tests/test_p1r_published_executor.py",
    "tools/tests/test_p1r_isolation_hardening.py",
    "tools/tests/p1r_isolation_fixture.py",
    "tools/p1r-published-consumers/README.md",
    ".github/workflows/p1r-qualification.yml",
}


def git(root, *args):
    return subprocess.check_output(["git", "--no-pager", *args], cwd=root,
                                   env=dict(os.environ, GIT_OPTIONAL_LOCKS="0"))


def sha(path):
    if path.is_symlink():
        return "symlink:" + os.readlink(path)
    if not path.is_file():
        return None
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def snapshot(root):
    files = {}
    for row in git(root, "ls-files", "--stage", "-z").split(b"\0"):
        if not row:
            continue
        meta, raw = row.split(b"\t", 1)
        if meta.split()[0] != b"160000":
            name = raw.decode()
            files[name] = sha(root / name)
    index = Path(git(root, "rev-parse", "--path-format=absolute", "--git-path", "index").decode().strip())
    return {"root": str(root), "head": git(root, "rev-parse", "HEAD").decode().strip(),
            "branch": git(root, "branch", "--show-current").decode().strip(),
            "index_sha256": sha(index), "files": files}


def main():
    target = HERE / ("initial-state.json" if sys.argv[1] == "capture" else "final-preservation.json")
    if target.exists():
        raise SystemExit("refusing existing evidence output")
    current = {"projects": snapshot(WORKSPACE), "eventstore": snapshot(OWNER)}
    if sys.argv[1] != "capture":
        before = json.loads((HERE / "initial-state.json").read_bytes())
        changes, violations = {}, []
        for repo, value in before.items():
            actual = current[repo]
            for key in ("head", "branch", "index_sha256"):
                if value[key] != actual[key]:
                    violations.append(f"{repo}:{key}")
            changed = [name for name, digest in value["files"].items() if actual["files"].get(name) != digest]
            changes[repo] = changed
            violations.extend(f"{repo}:{name}" for name in changed if repo != "eventstore" or name not in ALLOWED)
            untracked = [name.decode() for name in git(Path(actual["root"]), "ls-files", "--others",
                                                      "--exclude-standard", "-z").split(b"\0") if name]
            actual["untracked_paths"] = untracked
            bundle_prefix = HERE.relative_to(OWNER).as_posix() + "/"
            spec = "_bmad-output/implementation-artifacts/spec-6-1-p1r-tooling-hardening.md"
            violations.extend(f"{repo}:unexpected-new:{name}" for name in untracked
                              if repo != "eventstore" or not (name in ALLOWED or name == spec
                                                               or name.startswith(bundle_prefix)))
        current = {"passed": not violations, "violations": violations, "changed_tracked_paths": changes,
                   "protected_file_count": sum(len(row["files"]) for row in before.values()),
                   "starting_state_sha256": sha(HERE / "initial-state.json"),
                   "final_state": current}
    target.write_text(json.dumps(current, indent=2, sort_keys=True) + "\n")
    print(json.dumps({"output": str(target), "passed": current.get("passed")}))
    return 0 if current.get("passed", True) else 2


if __name__ == "__main__":
    raise SystemExit(main())
