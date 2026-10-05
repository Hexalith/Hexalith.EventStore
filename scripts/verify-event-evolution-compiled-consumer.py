#!/usr/bin/env python3
"""Compile a prior drain-record consumer once, then run it against the current Server assembly.

This narrow compatibility gate covers the prior twelve-member constructor and
Deconstruct ABI only. It does not qualify the full Story 6.6 package/API matrix.
Each subprocess has a deadline; fixtures and logs remain in the output directory.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import tempfile
import uuid


ROOT = pathlib.Path(__file__).resolve().parents[1]
FIXTURES = ROOT / "tools/event-evolution-compatibility"
ASSEMBLY_NAME = "Hexalith.EventStore.Server.dll"


def sha256(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(command: list[str], workspace: pathlib.Path, log: pathlib.Path, timeout: int = 90) -> subprocess.CompletedProcess:
    result = subprocess.run(command, cwd=workspace, capture_output=True, text=True, timeout=timeout, check=False)
    log.write_text(json.dumps(command) + "\n" + result.stdout + result.stderr, encoding="utf-8")
    return result


def verify(assembly: pathlib.Path, output: pathlib.Path, mutations: bool, configuration: str) -> dict:
    assembly = assembly.resolve(strict=True)
    output.mkdir(parents=True, exist_ok=False)
    workspace = output / "fixture"
    shutil.copytree(FIXTURES, workspace)
    shutil.copy2(ROOT / "global.json", workspace / "global.json")
    consumer_project = workspace / "LegacyConsumer/LegacyConsumer.csproj"
    control = run(["dotnet", "build", str(consumer_project), "--configuration", configuration, "-m:1", "--nologo"],
                  workspace, output / "compile-legacy.log")
    if control.returncode:
        raise RuntimeError("Prior-contract fixture build failed; inspect compile-legacy.log")

    consumer = workspace / f"LegacyConsumer/bin/{configuration}/net10.0/LegacyConsumer.dll"
    replacement = consumer.with_name(ASSEMBLY_NAME)
    compiled_consumer_hash = sha256(consumer)
    legacy_reference_hash = sha256(replacement)
    shutil.copy2(assembly, replacement)
    consumed_server_hash = sha256(replacement)
    current = run(["dotnet", str(consumer)], workspace, output / "current-assembly.log", timeout=20)
    if current.returncode or "legacy-twelve-member-consumer: passed" not in current.stdout:
        raise RuntimeError("Already-compiled consumer failed against current Server; inspect current-assembly.log")
    if sha256(replacement) != consumed_server_hash:
        raise RuntimeError("The replacement Server changed during the already-compiled consumer execution")

    observed_mutations = []
    if mutations:
        reference_source = workspace / "LegacyServer/UnpublishedEventsRecord.cs"
        old_source = reference_source.read_text(encoding="utf-8")
        for name, expected in (("constructor-removed", ".ctor"), ("deconstructor-removed", "Deconstruct")):
            source = old_source.replace("DateTimeOffset? ReminderArmedAt = null);",
                                        "DateTimeOffset? ReminderArmedAt = null, string? CausationId = null)")
            if name == "deconstructor-removed":
                source += """ {
    public UnpublishedEventsRecord(string CorrelationId, long StartSequence, long EndSequence,
        int EventCount, string CommandType, bool IsRejection, DateTimeOffset FailedAt, int RetryCount,
        string? LastFailureReason, string? MessageId, bool DeadLettered, DateTimeOffset? ReminderArmedAt)
        : this(CorrelationId, StartSequence, EndSequence, EventCount, CommandType, IsRejection,
            FailedAt, RetryCount, LastFailureReason, MessageId, DeadLettered, ReminderArmedAt, null) { }
}
"""
            else:
                source += ";\n"
            reference_source.write_text(source, encoding="utf-8")
            mutant = run(["dotnet", "build", str(workspace / "LegacyServer/LegacyServer.csproj"),
                          "--configuration", configuration, "-m:1", "--nologo"], workspace, output / f"{name}-build.log")
            if mutant.returncode:
                raise RuntimeError(f"{name}: negative fixture failed to build")
            shutil.copy2(workspace / f"LegacyServer/bin/{configuration}/net10.0" / ASSEMBLY_NAME, replacement)
            negative = run(["dotnet", str(consumer)], workspace, output / f"{name}.log", timeout=20)
            if negative.returncode != 2 or expected not in negative.stdout:
                raise RuntimeError(f"{name}: already-compiled consumer did not detect the missing {expected}")
            if sha256(consumer) != compiled_consumer_hash:
                raise RuntimeError("The prior consumer was rebuilt during replacement testing")
            observed_mutations.append({"mutation": name, "result": "rejected", "timeout_seconds": 20})
        shutil.copy2(assembly, replacement)

    return {
        "result": "passed",
        "scope": "prior twelve-member UnpublishedEventsRecord ABI only; no Story 6.6 activation",
        "executed_server_sha256": consumed_server_hash,
        "legacy_reference_sha256": legacy_reference_hash,
        "already_compiled_consumer_sha256": compiled_consumer_hash,
        "mutations": observed_mutations,
        "configuration": configuration,
        "output": str(output),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assembly", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path)
    parser.add_argument("--mutations", action="store_true")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    args = parser.parse_args()
    output = args.output or pathlib.Path(tempfile.gettempdir()) / f"event-evolution-compiled-{uuid.uuid4().hex}"
    try:
        result = verify(args.assembly, output.resolve(), args.mutations, args.configuration)
        (output / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(result, sort_keys=True))
        return 0
    except (OSError, RuntimeError, subprocess.TimeoutExpired) as error:
        print(json.dumps({"result": "failed", "detail": str(error), "output": str(output)}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
