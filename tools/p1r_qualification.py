"""Prepare bounded P1R evidence; this module cannot grant qualification.

Only the fixed local Python process controls execute. Package, actor, database,
container and owner lanes remain unavailable. Linux procfs identities, including
start time, bind cleanup to this invocation rather than to reusable PID numbers.
"""
from __future__ import annotations

import ast
import ctypes
import datetime as dt
import errno
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import signal
import subprocess
import sys
import threading
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
SCENARIOS = (
    "provenance", "legacy-metadata", "metadata-read", "metadata-write",
    "full-replay", "snapshot-tail", "retained-covered", "retained-uncovered",
    "missing-event", "invalid-evidence", "query-wire", "projection-wire",
    "mixed-api", "checkout", "post-upgrade-restore", "pre-upgrade-restore",
    "failure-cleanup",
)
FAMILIES = ("metadata-read", "metadata-write", "invalid-evidence", "query-wire",
            "projection-wire", "mixed-api", "checkout")
ADDITIONS = ("reminder-recovery", "logical-event-evolution")
CONTROLS = ("startup-failure", "success", "timeout", "cancellation",
            "descendants", "repeated-cleanup")
PENDING = ("published-candidate", "capable-published-rollback", "package-provenance",
           "cross-version-runtime", "fresh-database-restore", "container-cleanup",
           "published-assertion-acceptance", "eventstore-owner", "builds-owner",
           "solution-owner", "test-owner", "same-baseline-conformance")
EVIDENCE = "_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation"
VERIFIER = "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/run_verification.py"
SPEC = "_bmad-output/implementation-artifacts/spec-6-1-p1r-remediation.md"
FIXED_INPUTS = (
    SPEC, VERIFIER, "_bmad-output/project-context.md",
    "_bmad-output/planning-artifacts/architecture.md",
    *tuple("_bmad-output/specs/spec-6-1-p1r-remediation/" + name for name in
           ("SPEC.md", "compatibility-matrix.md", "qualification-contract.md", "runtime-evidence.md")),
)
SCHEMA = "hexalith.p1r.preparation.v1"
SOURCE_TOP = frozenset(("src", "tests", "test", "tools", "samples", "deploy", "Props", "Targets", ".config", ".github"))
EXCLUDED = frozenset(("bin", "obj", "node_modules", "__pycache__", ".git"))


class InvalidPacket(ValueError):
    """A safe, bounded refusal reason without input contents."""


def require(condition, reason):
    if not condition:
        raise InvalidPacket(reason)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False).encode()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def stamp():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + ".writing")
    temporary.write_bytes(json.dumps(value, indent=2, sort_keys=True, allow_nan=False).encode() + b"\n")
    temporary.replace(path)


def read_json(path):
    def pairs(items):
        value = {}
        for key, item in items:
            require(key not in value, "duplicate JSON member")
            value[key] = item
        return value
    try:
        value = json.loads(path.read_bytes(), object_pairs_hook=pairs,
                           parse_constant=lambda _: (_ for _ in ()).throw(InvalidPacket("nonfinite JSON number")))
        require(isinstance(value, dict), "JSON root must be an object")
        return value
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise InvalidPacket("missing or unreadable JSON input") from error


def regular(path):
    require(path.is_file() and not path.is_symlink(), "missing or substituted input")
    # A symlink in a parent is also a substitution.
    require(path.absolute() == path.resolve(), "symlink input path")
    return path.read_bytes()


def relative(name):
    require(isinstance(name, str) and bool(name), "invalid packet path")
    value = PurePosixPath(name)
    require(not value.is_absolute() and all(p not in (".", "..") for p in value.parts)
            and value.as_posix() == name, "unsafe packet path")
    return value


def git(root, *arguments):
    result = subprocess.run(["git", "--no-pager", *arguments], cwd=root,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20, check=False)
    require(result.returncode == 0, "source repository observation failed")
    return result.stdout


def relevant(name):
    parts = PurePosixPath(name).parts
    suffix = PurePosixPath(name).suffix.lower()
    source = suffix in (".cs", ".csproj", ".props", ".targets", ".json", ".config", ".ruleset", ".resx",
                        ".py", ".sh", ".yml", ".yaml", ".slnx", ".rsp", ".mjs", ".js", ".ts", ".razor", ".md")
    configuration = parts and parts[-1] in (".editorconfig", ".gitattributes", ".gitmodules", ".gitignore")
    return bool(parts) and not EXCLUDED.intersection(parts) and (source or configuration) and (len(parts) == 1 or parts[0] in SOURCE_TOP)


def repository_binding(root):
    require(root.is_dir() and root == root.resolve(), "source repository path unavailable")
    head = git(root, "rev-parse", "HEAD").decode().strip()
    require(bool(re.fullmatch(r"[0-9a-f]{40}", head)), "invalid source HEAD")
    committed = {}
    gitlinks = {}
    for row in git(root, "ls-tree", "-rz", "HEAD").split(b"\0"):
        if not row:
            continue
        info, raw_name = row.split(b"\t", 1)
        mode, kind, oid = info.decode().split()
        name = raw_name.decode()
        if kind == "commit":
            gitlinks[name] = oid
        elif relevant(name):
            committed[name] = oid
    names = set(committed)
    indexed = {}
    for row in git(root, "ls-files", "--stage", "-z").split(b"\0"):
        if not row:
            continue
        info, raw_name = row.split(b"\t", 1)
        mode, oid, stage = info.decode().split()
        name = raw_name.decode()
        if relevant(name):
            require(stage == "0" and name not in indexed, "unmerged or duplicate source index input")
            indexed[name] = oid
    names.update(indexed)
    names.update(name.decode() for name in git(root, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0")
                 if name and relevant(name.decode()))
    files = []
    for name in sorted(names):
        data = regular(root / relative(name))
        files.append({"path": name, "sha256": digest(data), "git_blob": committed.get(name),
                      "index_blob": indexed.get(name)})
    require(bool(files), "empty source inventory")
    # Git applies the owning attributes for the canonical tracked diff; physical
    # file hashes separately bind any checkout newline/dirty/untracked bytes.
    diff = git(root, "diff", "HEAD", "--no-ext-diff", "--binary", "--", *sorted(names))
    require(git(root, "rev-parse", "HEAD").decode().strip() == head, "source HEAD changed during binding")
    return {"head": head, "diff_sha256": digest(diff), "files": files, "gitlinks": gitlinks}


def declared_dependencies(root):
    result = subprocess.run(["git", "config", "--file", str(root / ".gitmodules"),
                             "--get-regexp", r"^submodule\..*\.path$"],
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20, check=False)
    require(result.returncode == 0, "root dependency declarations unavailable")
    paths = [row.split(" ", 1)[1] for row in result.stdout.decode().splitlines()]
    require(len(paths) == len(set(paths)), "duplicate root dependency declaration")
    for name in paths:
        relative(name)
    return sorted(paths)


def retained_inputs(root):
    names = set(FIXED_INPUTS)
    index = regular(root / EVIDENCE / "SHA256SUMS")
    names.add(EVIDENCE + "/SHA256SUMS")
    seen = set()
    for line in index.decode().splitlines():
        require(bool(re.fullmatch(r"[0-9a-f]{64}  .+", line)), "invalid retained checksum index")
        expected, name = line.split("  ", 1)
        require(name not in seen, "duplicate retained evidence input")
        seen.add(name)
        data = regular(root / EVIDENCE / relative(name))
        require(digest(data) == expected, "retained source evidence hash mismatch")
        names.add(EVIDENCE + "/" + name)
    require(bool(seen), "empty retained source evidence")
    tree = ast.parse(regular(root / VERIFIER))
    inventory = next((ast.literal_eval(node.value) for node in tree.body
                      if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == "SCENARIOS" for t in node.targets)), None)
    require(inventory == SCENARIOS, "canonical scenario inventory differs")
    return [{"path": name, "sha256": digest(regular(root / name))} for name in sorted(names)]


def source_binding(root=ROOT):
    root = Path(root).absolute()
    main = repository_binding(root)
    declared = declared_dependencies(root)
    require("references/Hexalith.Builds" in declared, "Builds dependency is not root declared")
    dependencies = []
    for name in declared:
        path = root / name
        initialized = (path / ".git").exists()
        dependencies.append({"path": name, "initialized": initialized,
                             "binding": repository_binding(path) if initialized else None})
    require(next(row for row in dependencies if row["path"] == "references/Hexalith.Builds")["initialized"],
            "required Builds input unavailable")
    return {"repository": str(root), "main": main, "dependencies": dependencies,
            "retained_inputs": retained_inputs(root),
            "python": {"path": str(Path(sys.executable).resolve()),
                       "sha256": digest(regular(Path(sys.executable).resolve())),
                       "version": list(sys.version_info[:3])}}


def check_current_source(binding):
    require(binding == source_binding(Path(binding["repository"])), "source/configuration inputs changed")


def checks_counter(checks):
    require(isinstance(checks, list), "invalid assertion observations")
    require(all(isinstance(c, dict) and set(c) == {"id", "passed"}
                and isinstance(c["id"], str) and type(c["passed"]) is bool for c in checks), "invalid assertion observation")
    require(len({c["id"] for c in checks}) == len(checks), "duplicate assertion observation")
    passed = sum(c["passed"] for c in checks)
    return {"attempted": len(checks), "passed": passed, "failed": len(checks) - passed}


def observe(checks, name, value):
    checks.append({"id": name, "passed": bool(value)})


def unavailable(name):
    return {"id": name, "execution": "unavailable", "compatibility": "unverified",
            "assertions": None, "reason": "separately owned qualification evidence unavailable"}


def process_state(pid):
    try:
        data = Path(f"/proc/{pid}/stat").read_text()
    except FileNotFoundError:
        return None
    fields = data[data.rindex(")") + 2:].split()
    return {"pid": pid, "start_ticks": int(fields[19]), "ppid": int(fields[1]),
            "pgid": int(fields[2]), "session": int(fields[3]), "state": fields[0]}


def identity(value):
    return {k: value[k] for k in ("pid", "start_ticks", "pgid", "session")}


def sentinel_state(pid):
    value = process_state(pid)
    return None if value is None else {**identity(value), "running": value["state"] not in ("Z", "X", "x", "T", "t")}


def same_process(value, observed):
    return observed is not None and observed["pid"] == value["pid"] and observed["start_ticks"] == value["start_ticks"]


def signal_owned(value, sig):
    # Bind the signal to a kernel handle before checking the start identity so
    # PID reuse between observation and signalling cannot target another process.
    try:
        descriptor = os.pidfd_open(value["pid"])
    except ProcessLookupError:
        return
    try:
        if same_process(value, process_state(value["pid"])):
            signal.pidfd_send_signal(descriptor, sig)
    finally:
        os.close(descriptor)


def process_inventory():
    require(Path("/proc/self/stat").is_file(), "Linux procfs controls unavailable")
    result = []
    for item in Path("/proc").iterdir():
        if item.name.isdigit():
            value = process_state(int(item.name))
            if value is not None:
                result.append(value)
    return result


class OwnedProcesses:
    """Track a fresh root and children with its private inherited registration."""

    def __init__(self):
        self.process = None
        self.owned = {}
        self.root = None
        self.nonce = uuid.uuid4().hex

    def environment(self):
        return {"PATH": os.defpath, "LANG": "C.UTF-8", "HEXALITH_P1R_CONTROL_NONCE": self.nonce}

    def attach(self, process):
        self.process = process
        row = process_state(process.pid)
        require(row is not None, "launched root identity unavailable")
        self.root = identity(row)
        self.owned[process.pid] = self.root

    def registered(self, row):
        try:
            fields = Path(f"/proc/{row['pid']}/environ").read_bytes().split(b"\0")
        except (FileNotFoundError, ProcessLookupError, PermissionError):
            return False
        token = ("HEXALITH_P1R_CONTROL_NONCE=" + self.nonce).encode()
        return token in fields and same_process(identity(row), process_state(row["pid"]))

    def discover(self):
        rows = process_inventory()
        for row in rows:
            # The registration survives reparenting and detached sessions. A
            # numeric parent/session match alone never grants cleanup ownership.
            if row["pid"] not in self.owned and self.registered(row):
                self.owned[row["pid"]] = identity(row)
            elif row["pid"] in self.owned and same_process(self.owned[row["pid"]], row):
                self.owned[row["pid"]] = identity(row)
        return rows

    def remaining(self):
        values = []
        for item in self.owned.values():
            row = process_state(item["pid"])
            if same_process(item, row):
                values.append(identity(row))
        return values

    def cleanup(self):
        errors = []
        before = []
        try:
            self.discover()
            before = self.remaining()
        except (OSError, ValueError) as error:
            errors.append(type(error).__name__)
        for sig in (signal.SIGTERM, signal.SIGKILL):
            for item in list(self.owned.values()):
                try:
                    signal_owned(item, sig)
                except ProcessLookupError:
                    pass
                except (OSError, ValueError) as error:
                    errors.append(type(error).__name__)
            deadline = time.monotonic() + .5
            while time.monotonic() < deadline:
                if self.process is not None:
                    self.process.poll()
                for pid in self.owned:
                    if self.process is not None and pid == self.process.pid:
                        continue
                    try:
                        os.waitpid(pid, os.WNOHANG)
                    except ChildProcessError:
                        pass
                    except OSError as error:
                        errors.append(type(error).__name__)
                try:
                    if not self.remaining():
                        break
                except (OSError, ValueError) as error:
                    errors.append(type(error).__name__)
                    break
                time.sleep(.01)
        try:
            remaining = self.remaining()
        except (OSError, ValueError) as error:
            errors.append(type(error).__name__)
            remaining = None
        return {"before": before, "remaining": remaining, "errors": errors}


def subreaper():
    require(sys.platform == "linux", "Linux process controls unavailable")
    # Reap adopted grandchildren ourselves; a container's PID 1 need not reap
    # orphaned zombies. No unrelated child is waited on or terminated.
    libc = ctypes.CDLL(None, use_errno=True)
    require(libc.prctl(36, 1, 0, 0, 0) == 0, "owned descendant reaping unavailable")


def control_command(control, python):
    if control == "startup-failure":
        return ["/nonexistent/hexalith-p1r-owned-startup-control"]
    script = "import time; print('p1r-control-ready', flush=True); "
    if control == "success":
        script += "print('p1r-control-success', flush=True)"
    elif control in ("descendants", "repeated-cleanup"):
        script = ("import subprocess,sys,time; "
                  "p=subprocess.Popen([sys.executable,'-I','-u','-c','import time; time.sleep(30)'],start_new_session=True); "
                  "print('p1r-owned-descendant='+str(p.pid),flush=True); time.sleep(30)")
    else:
        script += "time.sleep(30)"
    return [python, "-I", "-u", "-c", script]


def execute_control(directory, invocation, control, sentinel, python):
    receipt_id = uuid.uuid4().hex
    output_name = f"receipts/{receipt_id}.txt"
    receipt_path = directory / "receipts" / (receipt_id + ".json")
    owner = OwnedProcesses()
    row = {"id": receipt_id, "invocation": invocation, "control": control,
           "argv": control_command(control, python), "cwd": str(directory),
           "started_utc": stamp(), "finished_utc": None, "exit_code": None,
           "launch_error": None, "timed_out": False, "cancelled": False,
           "root_process": None, "owned_processes": [], "cleanup": [], "output": {"path": output_name, "sha256": None},
           "checks": [], "assertions": checks_counter([]), "execution": "failed"}
    write_json(receipt_path, row)  # Survives interruption even before launch.
    timer = None
    try:
        with (directory / output_name).open("xb") as output:
            try:
                process = subprocess.Popen(row["argv"], cwd=directory, stdout=output,
                                           stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL,
                                           env=owner.environment(), start_new_session=True)
                owner.attach(process)
                owner.discover()
                if control == "cancellation":
                    timer = threading.Timer(.2, lambda: os.kill(os.getpid(), signal.SIGINT))
                    timer.start()
                deadline = time.monotonic() + (2 if control == "success" else .4)
                while owner.process.poll() is None:
                    owner.discover()
                    if time.monotonic() >= deadline:
                        row["timed_out"] = True
                        break
                    time.sleep(.01)
            except OSError as error:
                row["launch_error"] = {"type": type(error).__name__, "errno": error.errno}
            except KeyboardInterrupt:
                row["cancelled"] = True
    finally:
        # Cleanup cannot be aborted by a second SIGINT. Restore the caller's
        # handler afterwards, and attempt every exact owned identity twice.
        handler = signal.signal(signal.SIGINT, signal.SIG_IGN)
        try:
            if timer is not None:
                timer.cancel()
                timer.join()
            row["cleanup"].append(owner.cleanup())
            row["cleanup"].append(owner.cleanup())
            row["root_process"] = owner.root
            row["owned_processes"] = list(owner.owned.values())
            row["exit_code"] = owner.process.returncode if owner.process else None
            row["finished_utc"] = stamp()
            row["output"]["sha256"] = digest(regular(directory / output_name))
            checks = row["checks"]
            observe(checks, "expected-process-outcome",
                    (row["launch_error"] is not None and owner.process is None) if control == "startup-failure" else
                    (row["exit_code"] == 0 and not row["cancelled"] and not row["timed_out"]) if control == "success" else
                    row["cancelled"] if control == "cancellation" else row["timed_out"])
            observe(checks, "output-retained", (directory / output_name).is_file())
            observe(checks, "exact-session-ownership", (owner.root is not None or control == "startup-failure")
                    and all(p["pid"] > 0 and p["start_ticks"] > 0 for p in row["owned_processes"]))
            if control in ("descendants", "repeated-cleanup"):
                observe(checks, "detached-descendant-observed", len(row["owned_processes"]) >= 2)
            for index, cleanup in enumerate(row["cleanup"]):
                observe(checks, f"cleanup-{index + 1}-complete", cleanup["remaining"] == [] and not cleanup["errors"])
            observe(checks, "shared-sentinel-preserved", sentinel_state(sentinel["pid"]) == sentinel)
            row["assertions"] = checks_counter(checks)
            row["execution"] = "passed" if row["assertions"]["attempted"] > 0 and row["assertions"]["failed"] == 0 else "failed"
            write_json(receipt_path, row)
        finally:
            signal.signal(signal.SIGINT, handler)
    return {"id": control, "execution": row["execution"], "compatibility": "unverified",
            "assertions": row["assertions"], "receipt": f"receipts/{receipt_id}.json"}


def seal(directory):
    files = sorted(p.relative_to(directory).as_posix() for p in directory.rglob("*") if p.is_file() and p != directory / "SHA256SUMS")
    require(all(not (directory / name).is_symlink() for name in files), "substituted packet file")
    (directory / "SHA256SUMS").write_text("".join(f"{digest(regular(directory / name))}  {name}\n" for name in files))


def create_packet(directory, mode, root=ROOT):
    require(mode in ("prepare", "run"), "unsupported preparation mode")
    directory = Path(directory).absolute()
    require(not directory.exists() and not directory.is_symlink(), "output path already exists")
    require(directory == directory.resolve(), "output parent path is substituted")
    directory.mkdir(parents=True, exist_ok=False)
    (directory / "receipts").mkdir()
    packet = {"schema": SCHEMA, "invocation": uuid.uuid4().hex, "mode": mode,
              "started_utc": stamp(), "finished_utc": None, "source_binding": "source-binding.json",
              "source_binding_sha256": None, "preparation": {"checks": [], "assertions": checks_counter([]), "execution": "failed"},
              "scenarios": [unavailable(name) for name in SCENARIOS], "families": [unavailable(name) for name in FAMILIES],
              "additions": [unavailable(name) for name in ADDITIONS], "process_controls": [unavailable(name) for name in CONTROLS],
              "pending_gates": list(PENDING), "published_candidate": None, "capable_published_rollback": None,
              "qualified": False, "p1r_usable": False, "recovery": "mutation-freeze-and-forward-recovery",
              "errors": [], "process_scope": "local-python-subprocesses-only"}
    if mode == "run":
        packet["sentinel"] = {"identity": None, "before": None, "after_controls": None,
                              "after_release": None, "released": False}
    write_json(directory / "packet.json", packet)
    sentinel_process = None
    sentinel = None
    try:
        binding = source_binding(root)
        write_json(directory / "source-binding.json", binding)
        packet["source_binding_sha256"] = digest(regular(directory / "source-binding.json"))
        for name, condition in (("canonical-seventeen-scenarios", len(SCENARIOS) == 17),
                                ("seven-families", len(FAMILIES) == 7),
                                ("selected-additions", len(ADDITIONS) == 2)):
            observe(packet["preparation"]["checks"], name, condition)
        if mode == "run":
            subreaper()
            sentinel_process = subprocess.Popen([binding["python"]["path"], "-I", "-c", "import time; time.sleep(30)"],
                                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True)
            observed = process_state(sentinel_process.pid)
            packet["sentinel"]["identity"] = identity(observed) if observed is not None else None
            sentinel = sentinel_state(sentinel_process.pid)
            packet["sentinel"]["before"] = sentinel
            require(sentinel is not None and sentinel["running"], "shared sentinel observation unavailable")
            for index, control in enumerate(CONTROLS):
                check_current_source(binding)  # Refuse dependent execution on drift.
                packet["process_controls"][index] = execute_control(directory, packet["invocation"], control, sentinel, binding["python"]["path"])
                write_json(directory / "packet.json", packet)
            packet["sentinel"]["after_controls"] = sentinel_state(sentinel_process.pid)
        check_current_source(binding)
        packet["preparation"]["execution"] = "passed"
    except (InvalidPacket, OSError, ValueError, subprocess.SubprocessError, KeyboardInterrupt) as error:
        packet["errors"].append(str(error) if isinstance(error, InvalidPacket) else type(error).__name__)
    finally:
        if sentinel_process is not None:
            handler = signal.signal(signal.SIGINT, signal.SIG_IGN)
            try:
                sentinel_process.terminate()
                try:
                    sentinel_process.wait(timeout=2)
                except subprocess.TimeoutExpired:
                    sentinel_process.kill()
                    sentinel_process.wait(timeout=2)
                after_release = sentinel_state(sentinel_process.pid)
                packet["sentinel"]["after_release"] = after_release
                original = packet["sentinel"]["identity"]
                packet["sentinel"]["released"] = original is not None and not same_process(original, after_release)
            except (OSError, ValueError, subprocess.SubprocessError) as error:
                packet["errors"].append(type(error).__name__)
            finally:
                signal.signal(signal.SIGINT, handler)
        packet["preparation"]["assertions"] = checks_counter(packet["preparation"]["checks"])
        packet["finished_utc"] = stamp()
        write_json(directory / "packet.json", packet)
        seal(directory)
    return packet


def ordered_rows(rows, expected):
    require(isinstance(rows, list) and all(isinstance(row, dict) for row in rows)
            and [row.get("id") for row in rows] == list(expected),
            "missing, duplicate or reordered required inventory")


def validate_counter(counter, checks=None):
    require(isinstance(counter, dict) and set(counter) == {"attempted", "passed", "failed"}, "unmeasured assertion counter")
    require(all(type(value) is int and value >= 0 for value in counter.values()), "invalid assertion counter")
    require(counter["attempted"] > 0 and counter["passed"] + counter["failed"] == counter["attempted"], "zero or inconsistent assertion counter")
    if checks is not None:
        require(counter == checks_counter(checks), "assertion counter differs from observations")


def validate_times(value):
    try:
        start = dt.datetime.fromisoformat(value["started_utc"])
        end = dt.datetime.fromisoformat(value["finished_utc"])
        require(start.utcoffset() == dt.timedelta(0) and end.utcoffset() == dt.timedelta(0) and end >= start,
                "invalid invocation timestamps")
        return start, end
    except (TypeError, ValueError, KeyError) as error:
        raise InvalidPacket("invalid invocation timestamps") from error


def members(value, names, reason):
    require(isinstance(value, dict) and set(value) == set(names), reason)


def validate_identity(value):
    members(value, ("pid", "start_ticks", "pgid", "session"), "invalid owned process identity")
    require(all(type(item) is int and item > 0 for item in value.values()), "invalid owned process identity")


def validate_identity_list(values):
    require(isinstance(values, list), "invalid process identity observations")
    for value in values:
        validate_identity(value)
    require(len({value["pid"] for value in values}) == len(values), "duplicate owned process identity")


def validate_sentinel_observation(value, original):
    members(value, ("pid", "start_ticks", "pgid", "session", "running"), "missing or invalid sentinel observation")
    validate_identity(identity(value))
    require(identity(value) == original and value["running"] is True, "shared sentinel changed or suspended")


def validate_control_receipt(directory, row, invocation, python, previous_finish, invocation_end, receipts):
    """Validate one executed process-control row and its receipt; return the receipt finish time."""
    require(row.get("compatibility") == "unverified", "process evidence claims runtime compatibility")
    require(set(row) == {"id", "execution", "compatibility", "assertions", "receipt"}, "process result members differ")
    validate_counter(row.get("assertions"))
    name = row.get("receipt")
    relative(name)
    require(name not in receipts, "duplicate invocation receipt")
    receipts.add(name)
    receipt = read_json(directory / name)
    require(set(receipt) == {"id", "invocation", "control", "argv", "cwd", "started_utc", "finished_utc", "exit_code",
                             "launch_error", "timed_out", "cancelled", "root_process", "owned_processes", "cleanup", "output", "checks",
                             "assertions", "execution"}, "receipt members differ")
    require(bool(re.fullmatch(r"[0-9a-f]{32}", receipt["id"])), "invalid receipt identity")
    receipt_start, receipt_end = validate_times(receipt)
    require(previous_finish <= receipt_start <= receipt_end <= invocation_end, "receipt interval outside invocation or control order")
    require(receipt.get("invocation") == invocation and receipt.get("control") == row["id"], "receipt invocation mismatch")
    require(name == "receipts/" + receipt["id"] + ".json" and receipt["argv"] == control_command(row["id"], python),
            "receipt command binding differs")
    require(receipt["cwd"] == str(directory), "receipt working directory differs")
    validate_counter(receipt["assertions"], receipt["checks"])
    expected_checks = ["expected-process-outcome", "output-retained", "exact-session-ownership"]
    if row["id"] in ("descendants", "repeated-cleanup"):
        expected_checks.append("detached-descendant-observed")
    expected_checks.extend(("cleanup-1-complete", "cleanup-2-complete", "shared-sentinel-preserved"))
    require([c["id"] for c in receipt["checks"]] == expected_checks, "executed assertions omitted or invented")
    require(row["assertions"] == receipt["assertions"] and row["execution"] == receipt["execution"], "receipt result differs")
    require(receipt["finished_utc"] is not None and receipt["execution"] == "passed" and receipt["assertions"]["failed"] == 0,
            "process control failed or incomplete")
    require(type(receipt["timed_out"]) is bool and type(receipt["cancelled"]) is bool, "invalid process outcome")
    owned = receipt["owned_processes"]
    validate_identity_list(owned)
    for process in owned:
        current = process_state(process["pid"])
        require(not same_process(process, current), "owned process still remains")
    require(isinstance(receipt["cleanup"], list) and len(receipt["cleanup"]) == 2, "owned cleanup incomplete")
    for cleanup in receipt["cleanup"]:
        members(cleanup, ("before", "remaining", "errors"), "cleanup ownership observation differs")
        validate_identity_list(cleanup["before"])
        validate_identity_list(cleanup["remaining"])
        require(all(value in owned for value in cleanup["before"] + cleanup["remaining"]), "cleanup ownership observation differs")
        require(isinstance(cleanup["errors"], list) and not cleanup["errors"] and cleanup["remaining"] == [], "owned cleanup incomplete")
    members(receipt["output"], ("path", "sha256"), "output receipt binding differs")
    require(receipt["output"]["path"] == "receipts/" + receipt["id"] + ".txt", "output receipt binding differs")
    require(digest(regular(directory / relative(receipt["output"]["path"]))) == receipt["output"]["sha256"], "receipt output hash mismatch")
    if row["id"] == "startup-failure":
        members(receipt["launch_error"], ("type", "errno"), "startup failure observation differs")
        require(receipt["launch_error"] == {"type": "FileNotFoundError", "errno": errno.ENOENT}
                and type(receipt["launch_error"]["errno"]) is int
                and not owned and receipt["root_process"] is None and receipt["exit_code"] is None
                and not receipt["timed_out"] and not receipt["cancelled"]
                and all(not cleanup["before"] for cleanup in receipt["cleanup"]),
                "startup failure observation differs")
    else:
        validate_identity(receipt["root_process"])
        root_process = receipt["root_process"]
        require(root_process in owned and root_process["pid"] == root_process["pgid"] == root_process["session"], "launched root ownership missing")
        require(receipt["launch_error"] is None and type(receipt["exit_code"]) is int, "invalid launched exit status or launch error")
        if row["id"] == "success":
            require(receipt["exit_code"] == 0 and not receipt["cancelled"] and not receipt["timed_out"], "success observation differs")
        else:
            require(receipt["exit_code"] in (-signal.SIGTERM, -signal.SIGKILL), "cleanup exit status differs")
            require(receipt["cancelled"] == (row["id"] == "cancellation") and receipt["timed_out"] == (row["id"] != "cancellation"),
                    "timeout or cancellation observation differs")
            require(receipt["cleanup"][0]["before"] == owned, "first cleanup ownership observation missing")
    if row["id"] in ("descendants", "repeated-cleanup"):
        require(len(receipt["owned_processes"]) >= 2, "descendant observation missing")
    return receipt_end


def validate_sentinel(sentinel):
    """Validate the shared sentinel's unchanged observations and its explicit release."""
    members(sentinel, ("identity", "before", "after_controls", "after_release", "released"), "invalid sentinel receipt")
    validate_identity(sentinel["identity"])
    validate_sentinel_observation(sentinel["before"], sentinel["identity"])
    validate_sentinel_observation(sentinel["after_controls"], sentinel["identity"])
    after_release = sentinel["after_release"]
    if after_release is not None:
        members(after_release, ("pid", "start_ticks", "pgid", "session", "running"), "invalid sentinel release observation")
        validate_identity(identity(after_release))
        require(type(after_release["running"]) is bool, "invalid sentinel release observation")
    require(sentinel["released"] is True and not same_process(sentinel["identity"], after_release)
            and not same_process(sentinel["identity"], process_state(sentinel["identity"]["pid"])), "shared sentinel changed or unreleased")


def validate_packet(directory):
    directory = Path(directory).absolute()
    require(directory.is_dir() and directory == directory.resolve(), "packet directory unavailable or substituted")
    expected_files = {}
    for line in regular(directory / "SHA256SUMS").decode().splitlines():
        require(bool(re.fullmatch(r"[0-9a-f]{64}  .+", line)), "invalid packet checksum index")
        value, name = line.split("  ", 1)
        relative(name)
        require(name not in expected_files and name != "SHA256SUMS", "duplicate packet input")
        expected_files[name] = value
    actual = {p.relative_to(directory).as_posix() for p in directory.rglob("*") if p.is_file() and p != directory / "SHA256SUMS"}
    require(actual == set(expected_files), "omitted or unexpected packet input")
    for name, expected in expected_files.items():
        require(digest(regular(directory / name)) == expected, "packet input hash mismatch")
    packet = read_json(directory / "packet.json")
    require(packet.get("schema") == SCHEMA and packet.get("mode") in ("prepare", "run"), "unsupported packet contract")
    require(bool(re.fullmatch(r"[0-9a-f]{32}", packet.get("invocation", ""))), "invalid invocation identity")
    packet_keys = {"schema", "invocation", "mode", "started_utc", "finished_utc", "source_binding", "source_binding_sha256",
                   "preparation", "scenarios", "families", "additions", "process_controls", "pending_gates", "published_candidate",
                   "capable_published_rollback", "qualified", "p1r_usable", "recovery", "errors", "process_scope"}
    require(set(packet) == packet_keys | ({"sentinel"} if packet["mode"] == "run" else set()), "packet members differ from contract")
    invocation_start, invocation_end = validate_times(packet)
    require(packet.get("qualified") is False and packet.get("p1r_usable") is False, "invented qualification")
    require(packet.get("published_candidate") is None and packet.get("capable_published_rollback") is None,
            "unauthorized package authority")
    require(packet.get("pending_gates") == list(PENDING) and packet.get("recovery") == "mutation-freeze-and-forward-recovery"
            and packet.get("process_scope") == "local-python-subprocesses-only", "qualification boundaries changed")
    require(isinstance(packet["errors"], list) and not packet["errors"], "preparation observation incomplete")
    require(packet.get("source_binding") == "source-binding.json", "unexpected source binding")
    require(digest(regular(directory / "source-binding.json")) == packet.get("source_binding_sha256"), "source binding hash mismatch")
    binding = read_json(directory / "source-binding.json")
    require(binding.get("repository") == str(ROOT), "unauthorized source repository")
    check_current_source(binding)  # Independently reconstructs the required closure.
    preparation = packet["preparation"]
    members(preparation, ("checks", "assertions", "execution"), "preparation members differ")
    validate_counter(preparation["assertions"], preparation["checks"])
    require([c.get("id") for c in preparation["checks"]] == ["canonical-seventeen-scenarios", "seven-families", "selected-additions"],
            "preparation assertions omitted or invented")
    require(preparation["execution"] == "passed" and preparation["assertions"]["failed"] == 0,
            "preparation checks failed")
    for key, inventory in (("scenarios", SCENARIOS), ("families", FAMILIES), ("additions", ADDITIONS)):
        ordered_rows(packet[key], inventory)
        for row in packet[key]:
            require(row == unavailable(row["id"]), "required qualification lane cannot pass in preparation scope")
    ordered_rows(packet["process_controls"], CONTROLS)
    receipts = set()
    previous_finish = invocation_start
    for row in packet["process_controls"]:
        if packet["mode"] == "prepare":
            require(row == unavailable(row["id"]), "unexecuted process control claims evidence")
            continue
        previous_finish = validate_control_receipt(directory, row, packet["invocation"], binding["python"]["path"],
                                                   previous_finish, invocation_end, receipts)
    if packet["mode"] == "run":
        validate_sentinel(packet["sentinel"])
    allowed = {"packet.json", "source-binding.json"} | receipts
    if packet["mode"] == "run":
        allowed.update(read_json(directory / name)["output"]["path"] for name in receipts)
    require(actual == allowed, "unbound or extra receipt input")
    return {"valid": True, "qualified": False, "p1r_usable": False,
            "mode": packet["mode"], "pending_gates": list(PENDING),
            "process_assertions": sum(row["assertions"]["attempted"] for row in packet["process_controls"] if row["assertions"])}
