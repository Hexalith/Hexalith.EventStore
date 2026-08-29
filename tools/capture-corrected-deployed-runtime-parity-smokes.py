#!/usr/bin/env python3
"""Capture bounded, digest-pinned Production /alive evidence for Story 3.15.

Environmental prerequisite: the linux/arm64 smoke runs under QEMU user-mode emulation on an amd64
host. Register it first with the pinned emulator image

    docker run --privileged --rm \
        tonistiigi/binfmt@sha256:400a4873b838d1b89194d982c45e5fb3cda4593fbfd7e08a02e76b03b21166f0 \
        --install all

The registration is host state, not an input byte this packet can hash, so it is recorded as a
documented precondition rather than bound into the subject.
"""

import argparse
import hashlib
import json
import subprocess
import sys
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path


REPOSITORY = "Hexalith/Hexalith.EventStore"
IMAGE_REPOSITORY = "registry.hexalith.com/eventstore"
INDEX_DIGEST = "sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3"
# The whole-platform wall-clock budget: one monotonic deadline bounds pull, run, port discovery,
# readiness polling and inspection for a single platform. The retained smoke record reports it as
# timeout_seconds, and the verifier requires the recorded per-platform window to fit inside it.
TIMEOUT_SECONDS = 180
# Cleanup runs after that budget is spent, in the failure mode it exists for, so it must not reuse
# the exhausted deadline: doing so made remaining_seconds raise before subprocess.run was ever
# called, leaking the container and its published host port while the record still claimed an
# attempt had timed out. Cleanup gets its own small independent budget.
CLEANUP_TIMEOUT_SECONDS = 30
RERUN_TRIGGER = (
    "Re-run the bounded two-platform Production smoke capture against the pinned index digest, "
    "then reassemble and revalidate the closure packet."
)
# The refusal path must not tell the operator to "re-run the capture", because the guard they just
# hit would refuse that too. Name the flag, or the alternative of capturing into a fresh root.
REFUSAL_RERUN_TRIGGER = (
    "Capture into an empty packet root, or re-run with --force to deliberately overwrite the "
    "retained smoke evidence, then reassemble and revalidate the closure packet."
)
PLATFORMS = (
    ("linux/amd64", "sha256:4d42f969dc5f57e0f9baa927c588346d77c31fd2615793b5d8c12c239585af63"),
    ("linux/arm64", "sha256:ede853318267146a9888574f79e16ea1e51c1f363a35910fe883b5a9d7256f44"),
)


def canonical_bytes(value):
    return (
        json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True)
        + "\n"
    ).encode("utf-8")


def now():
    return datetime.now(timezone.utc).isoformat(timespec="microseconds").replace("+00:00", "Z")


def remaining_seconds(deadline, arguments, budget=None):
    """Return the remaining wall-clock time of one budget, or fail immediately.

    ``budget`` is resolved here rather than as a def-time default, so a test that rebinds
    TIMEOUT_SECONDS on the imported module changes the reported budget too.
    """
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        raise subprocess.TimeoutExpired(arguments, TIMEOUT_SECONDS if budget is None else budget)
    return remaining


def run(deadline, *arguments, check=True, budget=None):
    """Run one command within the remaining portion of a monotonic deadline."""
    return subprocess.run(
        arguments,
        check=check,
        capture_output=True,
        text=True,
        timeout=remaining_seconds(deadline, arguments, budget),
    )


def parse_curl_write_out(value):
    """Parse the exact two-integer curl write-out retained by the smoke contract."""
    fields = value.strip().split()
    if len(fields) != 2 or any(not field.isascii() or not field.isdigit() for field in fields):
        raise RuntimeError(f"curl produced malformed write-out: {value!r}")
    return int(fields[0]), int(fields[1])


def capture_platform(output_root, platform, child_digest):
    immutable_image = f"{IMAGE_REPOSITORY}@{child_digest}"
    container_name = f"hexalith-story315-{platform.split('/')[1]}-{uuid.uuid4().hex[:12]}"
    # started_at is stamped before the deadline opens, so the recorded window encloses the whole
    # platform capture including cleanup. Stamping it after made (ended_at - started_at) always
    # shorter than the budget, which left the verifier's duration guard unfireable by construction
    # for evidence this script produces.
    started_at = now()
    deadline = time.monotonic() + TIMEOUT_SECONDS
    attempts = 0
    http_status = 0
    redirect_count = 0
    exit_code = 1
    cleanup = "failure"
    observed_platform = "unknown/unknown"
    try:
        run(deadline, "docker", "pull", "--platform", platform, immutable_image)
        run(
            deadline,
            "docker",
            "run",
            "--detach",
            "--name",
            container_name,
            "--platform",
            platform,
            "--publish",
            "127.0.0.1::8080",
            "--env",
            "ASPNETCORE_ENVIRONMENT=Production",
            "--env",
            "DOTNET_ENVIRONMENT=Production",
            "--env",
            "ASPNETCORE_URLS=http://+:8080",
            "--env",
            "Authentication__JwtBearer__Issuer=hexalith-container-smoke",
            "--env",
            "Authentication__JwtBearer__Audience=hexalith-eventstore",
            "--env",
            "Authentication__JwtBearer__SigningKey=hexalith-container-smoke-only-key-not-a-secret",
            "--env",
            "Authentication__JwtBearer__AllowInsecureSymmetricKey=true",
            immutable_image,
        )
        port_output = run(deadline, "docker", "port", container_name, "8080/tcp").stdout.strip()
        if ":" not in port_output:
            raise RuntimeError(f"docker port produced no host mapping for {container_name}: {port_output!r}")
        port = port_output.rsplit(":", 1)[1]
        if not port.isascii() or not port.isdigit():
            raise RuntimeError(f"docker port produced an invalid host mapping for {container_name}: {port_output!r}")
        while True:
            curl_budget = remaining_seconds(deadline, ("curl",))
            attempts += 1
            response = run(
                deadline,
                "curl",
                "--silent",
                "--show-error",
                "--output",
                "/dev/null",
                "--write-out",
                "%{http_code} %{num_redirects}",
                "--max-time",
                format(min(5.0, curl_budget), ".6f"),
                f"http://127.0.0.1:{port}/alive",
                check=False,
            )
            http_status, redirect_count = parse_curl_write_out(response.stdout)
            if response.returncode == 0 and http_status == 200 and redirect_count == 0:
                exit_code = 0
                break
            time.sleep(min(2.0, remaining_seconds(deadline, ("readiness-poll",))))
        image_id = run(
            deadline, "docker", "inspect", "--format", "{{.Image}}", container_name
        ).stdout.strip()
        if not image_id:
            raise RuntimeError(f"docker inspect produced no image identity for {container_name}")
        observed_platform = run(
            deadline,
            "docker",
            "image",
            "inspect",
            "--format",
            "{{.Os}}/{{.Architecture}}",
            image_id,
        ).stdout.strip()
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired, RuntimeError, OSError) as error:
        print(f"[corrected-deployed-runtime-parity-smokes] {platform} capture failed: {error}", file=sys.stderr)
    finally:
        try:
            cleanup_deadline = time.monotonic() + CLEANUP_TIMEOUT_SECONDS
            run(
                cleanup_deadline,
                "docker",
                "rm",
                "--force",
                container_name,
                budget=CLEANUP_TIMEOUT_SECONDS,
            )
            cleanup = "pass"
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired, OSError) as error:
            print(
                f"[corrected-deployed-runtime-parity-smokes] {platform} cleanup failed: {error}",
                file=sys.stderr,
            )
    ended_at = now()
    outcome = "pass" if exit_code == 0 and cleanup == "pass" and observed_platform == platform else "failure"
    record = {
        "attempts": attempts,
        "child_digest": child_digest,
        "cleanup": cleanup,
        "ended_at": ended_at,
        "exit_code": exit_code,
        "health_path": "/alive",
        "hosting_environment": "Production",
        "http_status": http_status,
        "observed_runtime_platform": observed_platform,
        "outcome": outcome,
        "platform": platform,
        "readiness_result": "pass" if exit_code == 0 else "failure",
        "redirect_count": redirect_count,
        "schema": "hexalith.eventstore.production-smoke-log.v1",
        "started_at": started_at,
    }
    log_path = output_root / f"smoke-{platform.replace('/', '-')}.log"
    log_bytes = canonical_bytes(record)
    log_path.write_bytes(log_bytes)
    summary = {key: value for key, value in record.items() if key not in ("health_path", "hosting_environment", "schema")}
    summary["log"] = {
        "file": f"smokes/{log_path.name}",
        "sha256": hashlib.sha256(log_bytes).hexdigest(),
        "size": len(log_bytes),
    }
    return summary


def main():
    # RawDescriptionHelpFormatter keeps the pinned binfmt registration command in the docstring
    # copy-pasteable instead of being rewrapped into one unusable line.
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("packet_root", type=Path)
    parser.add_argument(
        "--force",
        action="store_true",
        help="Overwrite an already populated smokes/ directory instead of refusing.",
    )
    arguments = parser.parse_args()
    output_root = arguments.packet_root / "smokes"
    # Running this against a live packet root used to overwrite the three hash-bound smoke files
    # with failure records, recoverable only through git. Refuse unless the operator opts in. The
    # refusal exits 2, distinct from the exit 1 a genuine smoke failure produces, so a caller can
    # tell "I would have destroyed evidence" from "the runtime did not answer".
    try:
        populated = output_root.is_dir() and any(output_root.iterdir())
    except OSError as error:
        print(
            "[corrected-deployed-runtime-parity-smokes] fail: "
            f"{output_root} could not be inspected: {error}; rerun: {REFUSAL_RERUN_TRIGGER}",
            file=sys.stderr,
        )
        return 2
    if not arguments.force and populated:
        print(
            "[corrected-deployed-runtime-parity-smokes] fail: "
            f"{output_root} already holds retained smoke evidence; pass --force to overwrite; "
            f"rerun: {REFUSAL_RERUN_TRIGGER}",
            file=sys.stderr,
        )
        return 2
    try:
        output_root.mkdir(parents=True, exist_ok=True)
    except (OSError, FileExistsError) as error:
        print(
            "[corrected-deployed-runtime-parity-smokes] fail: "
            f"{output_root} is not a usable output directory: {error}; "
            f"rerun: {REFUSAL_RERUN_TRIGGER}",
            file=sys.stderr,
        )
        return 2
    started_at = now()
    platforms = [capture_platform(output_root, platform, digest) for platform, digest in PLATFORMS]
    ended_at = now()
    result = {
        "ended_at": ended_at,
        "endpoint": "/alive",
        "environment": "Production",
        "exit_code": 0 if all(item["outcome"] == "pass" for item in platforms) else 1,
        "image_repository": IMAGE_REPOSITORY,
        "index_digest": INDEX_DIGEST,
        "platforms": platforms,
        "repository": REPOSITORY,
        "result": "pass" if all(item["outcome"] == "pass" for item in platforms) else "failure",
        "schema": "hexalith.eventstore.production-smoke-results.v1",
        "started_at": started_at,
        "timeout_seconds": TIMEOUT_SECONDS,
    }
    (output_root / "smoke-results.json").write_bytes(canonical_bytes(result))
    if result["exit_code"] != 0:
        print(
            "[corrected-deployed-runtime-parity-smokes] fail: "
            "one or both bounded Production smokes did not pass; "
            f"rerun: {RERUN_TRIGGER}",
            file=sys.stderr,
        )
    print(f"[corrected-deployed-runtime-parity-smokes] {result['result']}")
    return result["exit_code"]


if __name__ == "__main__":
    sys.exit(main())
