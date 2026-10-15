"""Recovery receipt contracts and bounded local process controls for 6.1-P1R.

Restore and cleanup checks are recomputed from the receipt's bound commands,
persisted inventories and cleanup attempts: a receipt may retain a failure, but
it can never claim a check outcome its own data contradicts. The replay
observations (``state_before_append``, ``appended_sequence`` and
``state_after_restart``) are executor-reported values; they are compared with
the contract (12/13/13) but cannot be recomputed here. This module executes no
database, container, Dapr or actor backend. Operational
receipts can only come from a separately selected executor and owner-selected
profile; synthetic fixtures keep ``tooling-synthetic`` scope and the local
process controls prove only their own local scope, so neither can satisfy an
operational lane.
"""
from __future__ import annotations

import re
import signal
import subprocess

import p1r_qualification as preparation
from p1r_qualification import canonical, digest, members, require, validate_times

RESTORE_SCHEMA = "hexalith.p1r.restore-receipt.v1"
CLEANUP_SCHEMA = "hexalith.p1r.cleanup-receipt.v1"
RECEIPT_SCOPES = ("tooling-synthetic", "operational")
FLOOR, SNAPSHOT, HEAD, APPEND = 5, 9, 12, 13
ROLES = ("candidate", "rollback")
ROW_KINDS = ("metadata", "event", "snapshot", "bookkeeping")
STEPS = ("create-database", "seed", "backup", "restore", "inventory", "start-writer", "append", "stop-writer",
         "restart", "replay")
OWNED_KINDS = ("container", "process", "database", "scratch")
RESTORE_CHECKS = ("fresh-restored-database", "restored-equals-backup-source", "retained-source-shape",
                  "reconstructed-head-twelve", "appended-thirteen", "retained-floor-preserved",
                  "prior-event-hashes-preserved", "snapshot-preserved", "second-tenant-preserved", "fresh-restart",
                  "restart-reconstructs-thirteen", "owned-cleanup-complete")
CLEANUP_CHECKS = ("first-attempt-targets-every-owned", "repeated-cleanup", "later-attempts-continue",
                  "no-owned-remaining", "no-cleanup-errors", "shared-discovery-complete", "shared-preserved")
RESTORE_KEYS = ("schema", "id", "role", "scope", "fixture", "inputs_sha256", "profile", "packages", "tenants", "commands",
                "databases", "backup", "inventories", "observations", "cleanup", "checks", "assertions", "execution",
                "compatibility")
CLEANUP_KEYS = ("schema", "id", "invocation", "scope", "fixture", "inputs_sha256", "profile", "owned", "attempts",
                "shared", "checks", "assertions", "execution", "compatibility")
COMMAND_KEYS = ("id", "step", "argv", "cwd", "started_utc", "finished_utc", "exit_code", "output_sha256", "database",
                "input_sha256", "input_bytes", "processes")
ROW_KEYS = ("key", "tenant", "kind", "sha256", "sequence", "floor")
PROFILE_KEYS = ("runtime", "runtime_version", "backend", "backend_image", "selected_by", "reference")
HEX32 = re.compile(r"[0-9a-f]{32}")
HEX64 = re.compile(r"[0-9a-f]{64}")
PACKAGE_ID = re.compile(r"[A-Za-z0-9_.-]{1,128}")
IMAGE = re.compile(r"[a-z0-9][a-z0-9._/:-]*@sha256:[0-9a-f]{64}")


def text(value, reason):
    require(isinstance(value, str) and 0 < len(value) <= 512 and value.isprintable(), reason)
    return value


def pattern(value, expression, reason):
    require(isinstance(value, str) and expression.fullmatch(value) is not None, reason)
    return value


def integer(value, reason, minimum=0, optional=False):
    require((optional and value is None) or (type(value) is int and value >= minimum), reason)
    return value


def validate_profile(value, reason="invalid operational profile"):
    members(value, PROFILE_KEYS, reason)
    for key in PROFILE_KEYS:
        text(value[key], reason)
    pattern(value["backend_image"], IMAGE, reason)
    return value


def validate_fixture(value, scope):
    """Synthetic fixtures are exactly the tooling-synthetic scope; nothing else may carry the marker."""
    members(value, ("id", "synthetic"), "invalid fixture identity")
    text(value["id"], "invalid fixture identity")
    require(type(value["synthetic"]) is bool, "invalid fixture identity")
    require(value["synthetic"] == (scope == "tooling-synthetic"), "fixture marker differs from evidence scope")


def disposition(scope, passed):
    """Execution and compatibility stay separate; tooling scope never establishes compatibility."""
    execution = "passed" if passed else "failed"
    if scope != "operational":
        return execution, "unverified"
    return execution, "compatible" if passed else "incompatible"


def validate_outcome(receipt, observed, scope):
    require(isinstance(receipt["checks"], list) and receipt["checks"] == observed,
            "claimed observations differ from bound evidence")
    preparation.validate_counter(receipt["assertions"], observed)
    execution, compatibility = disposition(scope, all(check["passed"] for check in observed))
    require(receipt["execution"] == execution and receipt["compatibility"] == compatibility,
            "claimed outcome differs from bound evidence")
    return execution, compatibility


def checks_from(values, names):
    return [{"id": name, "passed": bool(value)} for name, value in zip(names, values)]


def validate_header(receipt, keys, schema, reason):
    members(receipt, keys, reason)
    require(receipt["schema"] == schema, "unsupported receipt contract")
    pattern(receipt["id"], HEX32, "invalid receipt identity")
    require(receipt["scope"] in RECEIPT_SCOPES, "unsupported evidence scope")
    validate_fixture(receipt["fixture"], receipt["scope"])
    require(receipt["inputs_sha256"] is None or (isinstance(receipt["inputs_sha256"], str)
                                                 and HEX64.fullmatch(receipt["inputs_sha256"])), "invalid input binding")
    if receipt["profile"] is not None:
        validate_profile(receipt["profile"])


def validate_commands(values):
    require(isinstance(values, list) and values, "missing bound command receipts")
    commands = {}
    previous = 0
    for command in values:
        members(command, COMMAND_KEYS, "command receipt members differ")
        integer(command["id"], "invalid command identity", minimum=1)
        require(command["id"] > previous, "duplicate or reordered command receipt")
        previous = command["id"]
        require(command["step"] in STEPS, "unknown recovery step")
        require(isinstance(command["argv"], list) and command["argv"] and all(isinstance(a, str) and a for a in command["argv"]),
                "missing literal command")
        text(command["cwd"], "missing command working directory")
        validate_times(command)
        require(type(command["exit_code"]) is int, "missing command exit status")
        pattern(command["output_sha256"], HEX64, "missing command output hash")
        for key in ("database", "input_sha256"):
            require(command[key] is None or (isinstance(command[key], str) and HEX64.fullmatch(command[key])),
                    "invalid command binding")
        integer(command["input_bytes"], "invalid command input size", optional=True)
        if command["processes"] is not None:
            preparation.validate_identity_list(command["processes"])
        commands[command["id"]] = command
    return commands


def validate_rows(rows):
    require(isinstance(rows, list), "invalid persisted inventory")
    for row in rows:
        members(row, ROW_KEYS, "invalid persisted inventory row")
        text(row["key"], "invalid persisted inventory row")
        text(row["tenant"], "invalid persisted inventory row")
        require(row["kind"] in ROW_KINDS, "invalid persisted inventory row")
        pattern(row["sha256"], HEX64, "invalid persisted inventory row")
        integer(row["sequence"], "invalid persisted inventory row", minimum=1, optional=True)
        integer(row["floor"], "invalid persisted inventory row", optional=True)
    keys = [row["key"] for row in rows]
    require(keys == sorted(set(keys)), "unordered or duplicate persisted inventory key")
    for tenant in {row["tenant"] for row in rows}:
        events = [row["sequence"] for row in rows if row["tenant"] == tenant and row["kind"] == "event"]
        require(None not in events and len(events) == len(set(events)), "ambiguous retained event stream")
    return rows


def validate_inventory(value, commands, database):
    members(value, ("command", "rows", "sha256"), "invalid persisted inventory")
    rows = validate_rows(value["rows"])
    require(value["sha256"] == digest(canonical(rows)), "persisted inventory hash mismatch")
    command = commands.get(value["command"])
    require(command is not None and command["step"] == "inventory" and command["exit_code"] == 0
            and command["database"] == database and command["output_sha256"] == value["sha256"],
            "persisted inventory lacks its bound state-query receipt")
    return rows


def stream(rows, tenant):
    """Return the tenant's single retained metadata row, snapshot row and event hashes by sequence."""
    owned = [row for row in rows if row["tenant"] == tenant and row["kind"] != "bookkeeping"]
    metadata = [row for row in owned if row["kind"] == "metadata"]
    snapshots = [row for row in owned if row["kind"] == "snapshot"]
    events = {row["sequence"]: row["sha256"] for row in owned if row["kind"] == "event"}
    return (metadata[0] if len(metadata) == 1 else None), (snapshots[0] if len(snapshots) == 1 else None), events


def domain(rows, tenant):
    return [row for row in rows if row["tenant"] == tenant and row["kind"] != "bookkeeping"]


def restore_observations(receipt, commands, inventories):
    source_db, restored_db = receipt["databases"]["source"], receipt["databases"]["restored"]
    primary, secondary = receipt["tenants"]["primary"], receipt["tenants"]["secondary"]
    observations = receipt["observations"]
    source, restored, appended, restarted = (inventories[name] for name in ("source", "restored", "appended", "restarted"))
    steps = {}
    for command in commands.values():
        steps.setdefault(command["step"], []).append(command)

    created = [c for c in steps.get("create-database", []) if c["database"] == restored_db]
    uses = [c["id"] for c in commands.values() if c["database"] == restored_db and c["step"] != "create-database"]
    fresh = (restored_db != source_db and len(created) == 1 and created[0]["exit_code"] == 0
             and bool(uses) and created[0]["id"] < min(uses))

    source_meta, source_snapshot, source_events = stream(source, primary)
    retained_shape = (source_meta is not None and source_meta["sequence"] == HEAD and source_meta["floor"] == FLOOR
                      and source_snapshot is not None and source_snapshot["sequence"] == SNAPSHOT
                      and sorted(source_events) == list(range(FLOOR, HEAD + 1)) and bool(domain(source, secondary)))

    appends = [c for c in steps.get("append", []) if c["exit_code"] == 0 and c["database"] == restored_db]
    restarts = [c for c in steps.get("restart", []) if c["exit_code"] == 0 and c["processes"]]
    stops = [c["id"] for c in steps.get("stop-writer", []) if c["exit_code"] == 0 and c["database"] == restored_db]
    replays = [c["id"] for c in steps.get("replay", []) if c["exit_code"] == 0 and c["database"] == restored_db]
    append_id = appends[0]["id"] if len(appends) == 1 else None
    restart_id = restarts[-1]["id"] if restarts else None
    appended_meta, appended_snapshot, appended_events = stream(appended, primary)
    restarted_meta, restarted_snapshot, restarted_events = stream(restarted, primary)
    appended_inventory = commands[inventories_command(receipt, "appended")]["id"]
    restarted_inventory = commands[inventories_command(receipt, "restarted")]["id"]
    thirteen = (append_id is not None and append_id < appended_inventory and observations["appended_sequence"] == APPEND
                and appended_meta is not None and appended_meta["sequence"] == APPEND
                and sorted(appended_events) == list(range(FLOOR, APPEND + 1)))
    floor = (appended_meta is not None and appended_meta["floor"] == FLOOR
             and restarted_meta is not None and restarted_meta["floor"] == FLOOR)
    prior = all(appended_events.get(n) == source_events.get(n) == restarted_events.get(n) is not None
                for n in range(FLOOR, HEAD + 1))
    snapshot = (source_snapshot is not None and appended_snapshot is not None and restarted_snapshot is not None
                and source_snapshot["sha256"] == appended_snapshot["sha256"] == restarted_snapshot["sha256"])
    second = (bool(domain(source, secondary))
              and domain(source, secondary) == domain(restored, secondary) == domain(appended, secondary) == domain(restarted, secondary))
    writers = {(p["pid"], p["start_ticks"]) for c in steps.get("start-writer", []) if c["processes"] for p in c["processes"]}
    restarted_processes = {(p["pid"], p["start_ticks"]) for c in restarts for p in c["processes"]}
    # Single writer: the appending writer stops before the fresh restart, and replay follows the restart.
    fresh_restart = (append_id is not None and restart_id is not None and append_id < restart_id < restarted_inventory
                     and appended_inventory < restart_id and bool(writers) and bool(restarted_processes)
                     and not writers & restarted_processes and any(append_id < stop < restart_id for stop in stops))
    reconstructed = (observations["state_after_restart"] == APPEND and restart_id is not None
                     and any(restart_id < replay < restarted_inventory for replay in replays)
                     and domain(restarted, primary) == domain(appended, primary))
    return checks_from((fresh, restored == source, retained_shape, observations["state_before_append"] == HEAD, thirteen,
                        floor, prior, snapshot, second, fresh_restart, reconstructed,
                        receipt["cleanup"]["execution"] == "passed"), RESTORE_CHECKS)


def inventories_command(receipt, name):
    return receipt["inventories"][name]["command"]


def restore_outcome(receipt):
    """Validate a fresh-database restore/append/restart receipt and return its derived outcome."""
    validate_header(receipt, RESTORE_KEYS, RESTORE_SCHEMA, "restore receipt members differ")
    require(receipt["role"] in ROLES, "unknown restore role")
    require(isinstance(receipt["packages"], dict) and all(isinstance(k, str) and PACKAGE_ID.fullmatch(k)
                                                          and isinstance(v, str) and HEX64.fullmatch(v)
                                                          for k, v in receipt["packages"].items()),
            "invalid writer package binding")
    members(receipt["tenants"], ("primary", "secondary"), "invalid tenant binding")
    text(receipt["tenants"]["primary"], "invalid tenant binding")
    text(receipt["tenants"]["secondary"], "invalid tenant binding")
    require(receipt["tenants"]["primary"] != receipt["tenants"]["secondary"], "tenant isolation needs two tenants")
    commands = validate_commands(receipt["commands"])
    members(receipt["databases"], ("source", "restored"), "invalid database binding")
    source_db = pattern(receipt["databases"]["source"], HEX64, "invalid database binding")
    restored_db = pattern(receipt["databases"]["restored"], HEX64, "invalid database binding")
    members(receipt["backup"], ("command", "sha256", "bytes"), "invalid backup binding")
    backup = commands.get(receipt["backup"]["command"])
    require(backup is not None and backup["step"] == "backup" and backup["database"] == source_db and backup["exit_code"] == 0
            and backup["output_sha256"] == receipt["backup"]["sha256"]
            and integer(receipt["backup"]["bytes"], "invalid backup binding", minimum=1),
            "backup hash is not bound to its executed command")
    restores = [c for c in commands.values() if c["step"] == "restore"]
    require(len(restores) == 1 and restores[0]["database"] == restored_db and restores[0]["exit_code"] == 0
            and restores[0]["input_sha256"] == backup["output_sha256"] and restores[0]["input_bytes"] == receipt["backup"]["bytes"]
            and backup["id"] < restores[0]["id"], "restore input differs from the bound backup")
    members(receipt["inventories"], ("source", "restored", "appended", "restarted"), "invalid inventory binding")
    inventories = {"source": validate_inventory(receipt["inventories"]["source"], commands, source_db)}
    for name in ("restored", "appended", "restarted"):
        inventories[name] = validate_inventory(receipt["inventories"][name], commands, restored_db)
    require(restores[0]["id"] < inventories_command(receipt, "restored"), "restored inventory precedes its restore")
    members(receipt["observations"], ("state_before_append", "appended_sequence", "state_after_restart"),
            "invalid replay observations")
    for value in receipt["observations"].values():
        integer(value, "invalid replay observations", optional=True)
    cleanup = cleanup_outcome(receipt["cleanup"])
    require(receipt["cleanup"]["scope"] == receipt["scope"]
            and receipt["cleanup"]["inputs_sha256"] == receipt["inputs_sha256"]
            and receipt["cleanup"]["profile"] == receipt["profile"], "embedded cleanup differs from restore scope")
    execution, compatibility = validate_outcome(receipt, restore_observations(receipt, commands, inventories), receipt["scope"])
    bookkeeping = sum(row["kind"] == "bookkeeping" for rows in inventories.values() for row in rows)
    return {"execution": execution, "compatibility": compatibility, "scope": receipt["scope"],
            "assertions": receipt["assertions"], "bookkeeping_rows": bookkeeping, "cleanup": cleanup}


def cleanup_outcome(receipt):
    """Validate an owned-resource cleanup receipt; failures and drift are retained as nonpassing."""
    validate_header(receipt, CLEANUP_KEYS, CLEANUP_SCHEMA, "cleanup receipt members differ")
    invocation = pattern(receipt["invocation"], HEX32, "invalid cleanup invocation")
    label = "hexalith.p1r.invocation=" + invocation
    require(isinstance(receipt["owned"], list) and receipt["owned"], "missing owned resource inventory")
    owned = []
    for resource in receipt["owned"]:
        members(resource, ("kind", "id", "label"), "invalid owned resource")
        require(resource["kind"] in OWNED_KINDS, "invalid owned resource")
        owned.append(text(resource["id"], "invalid owned resource"))
        require(resource["label"] == label, "cleanup inventory includes an unowned resource")
    require(len(owned) == len(set(owned)), "duplicate owned resource")
    require(isinstance(receipt["attempts"], list) and receipt["attempts"], "missing cleanup attempts")
    previous = None
    for attempt in receipt["attempts"]:
        members(attempt, ("started_utc", "finished_utc", "targeted", "removed", "remaining", "errors"),
                "invalid cleanup attempt")
        start, end = validate_times(attempt)
        require(previous is None or previous <= start, "cleanup attempts are reordered")
        previous = end
        for key in ("targeted", "removed", "remaining"):
            values = attempt[key]
            require(isinstance(values, list) and len(values) == len(set(values)) and set(values) <= set(owned),
                    "cleanup attempt names an unowned resource")
        require(isinstance(attempt["errors"], list), "invalid cleanup attempt")
        for error in attempt["errors"]:
            text(error, "invalid cleanup attempt")
    members(receipt["shared"], ("before", "after", "complete"), "invalid shared-resource observation")
    for phase in ("before", "after"):
        resources = receipt["shared"][phase]
        require(isinstance(resources, dict) and not set(resources) & set(owned), "invalid shared-resource observation")
        for identity, resource in resources.items():
            text(identity, "invalid shared-resource observation")
            members(resource, ("image", "running", "started"), "invalid shared-resource observation")
            text(resource["image"], "invalid shared-resource observation")
            text(resource["started"], "invalid shared-resource observation")
            require(type(resource["running"]) is bool, "invalid shared-resource observation")
    require(type(receipt["shared"]["complete"]) is bool, "invalid shared-resource observation")
    attempts = receipt["attempts"]
    observed = checks_from((
        set(attempts[0]["targeted"]) == set(owned),
        len(attempts) >= 2,
        all(set(later["targeted"]) >= set(earlier["remaining"]) for earlier, later in zip(attempts, attempts[1:])),
        attempts[-1]["remaining"] == [],
        all(not attempt["errors"] for attempt in attempts),
        receipt["shared"]["complete"] is True,
        receipt["shared"]["before"] == receipt["shared"]["after"],
    ), CLEANUP_CHECKS)
    execution, compatibility = validate_outcome(receipt, observed, receipt["scope"])
    return {"execution": execution, "compatibility": compatibility, "scope": receipt["scope"],
            "assertions": receipt["assertions"]}


def require_operational_profile(selection):
    """Operational execution needs an owner-selected profile; nothing is defaulted."""
    require(selection is not None and selection.get("operational_profile") is not None,
            "operational execution refused: operational profile not selected")
    return selection["operational_profile"]


def new_process_section():
    return {"scope": "local-process-control", "rows": [preparation.unavailable(name) for name in preparation.CONTROLS],
            "sentinel": {"identity": None, "before": None, "after_controls": None, "after_release": None, "released": False}}


def run_process_controls(section, directory, invocation, python, before_each, after_each):
    """Run the fixed local controls with a shared sentinel; ``section`` keeps partial results on interruption."""
    sentinel_process = None
    try:
        preparation.subreaper()
        sentinel_process = subprocess.Popen([python, "-I", "-c", "import time; time.sleep(30)"],
                                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True)
        observed = preparation.process_state(sentinel_process.pid)
        section["sentinel"]["identity"] = preparation.identity(observed) if observed is not None else None
        sentinel = preparation.sentinel_state(sentinel_process.pid)
        section["sentinel"]["before"] = sentinel
        require(sentinel is not None and sentinel["running"], "shared sentinel observation unavailable")
        for index, control in enumerate(preparation.CONTROLS):
            before_each()  # Refuse dependent execution on source drift.
            section["rows"][index] = preparation.execute_control(directory, invocation, control, sentinel, python)
            after_each()
        section["sentinel"]["after_controls"] = preparation.sentinel_state(sentinel_process.pid)
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
                after_release = preparation.sentinel_state(sentinel_process.pid)
                section["sentinel"]["after_release"] = after_release
                original = section["sentinel"]["identity"]
                section["sentinel"]["released"] = original is not None and not preparation.same_process(original, after_release)
            finally:
                signal.signal(signal.SIGINT, handler)


def validate_process_controls(directory, section, invocation, python, invocation_start, invocation_end):
    """Validate local controls; they prove only their own local process scope."""
    members(section, ("scope", "rows", "sentinel"), "process control section differs from contract")
    require(section["scope"] == "local-process-control", "process controls claim a wider scope")
    preparation.ordered_rows(section["rows"], preparation.CONTROLS)
    receipts = set()
    previous = invocation_start
    for row in section["rows"]:
        previous = preparation.validate_control_receipt(directory, row, invocation, python, previous, invocation_end, receipts)
    preparation.validate_sentinel(section["sentinel"])
    outputs = {preparation.read_json(directory / name)["output"]["path"] for name in receipts}
    assertions = sum(row["assertions"]["attempted"] for row in section["rows"])
    return receipts | outputs, assertions

