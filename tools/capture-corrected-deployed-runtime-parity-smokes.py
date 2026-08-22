#!/usr/bin/env python3
"""Capture bounded, digest-pinned Production /alive evidence for Story 3.15."""

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
TIMEOUT_SECONDS = 180
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


def run(*arguments, check=True):
    return subprocess.run(arguments, check=check, capture_output=True, text=True, timeout=TIMEOUT_SECONDS)


def capture_platform(output_root, platform, child_digest):
    immutable_image = f"{IMAGE_REPOSITORY}@{child_digest}"
    container_name = f"hexalith-story315-{platform.split('/')[1]}-{uuid.uuid4().hex[:12]}"
    started_at = now()
    attempts = 0
    http_status = 0
    redirect_count = 0
    exit_code = 1
    cleanup = "failure"
    observed_platform = "unknown/unknown"
    try:
        run("docker", "pull", "--platform", platform, immutable_image)
        run(
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
        port_output = run("docker", "port", container_name, "8080/tcp").stdout.strip()
        if ":" not in port_output:
            raise RuntimeError(f"docker port produced no host mapping for {container_name}: {port_output!r}")
        port = port_output.rsplit(":", 1)[1]
        deadline = time.monotonic() + TIMEOUT_SECONDS
        while time.monotonic() < deadline:
            attempts += 1
            response = run(
                "curl",
                "--silent",
                "--show-error",
                "--output",
                "/dev/null",
                "--write-out",
                "%{http_code} %{num_redirects}",
                "--max-time",
                "5",
                f"http://127.0.0.1:{port}/alive",
                check=False,
            )
            if response.returncode == 0:
                http_status, redirect_count = (int(value) for value in response.stdout.split())
                if 200 <= http_status < 300 and redirect_count == 0:
                    exit_code = 0
                    break
            time.sleep(2)
        image_id = run("docker", "inspect", "--format", "{{.Image}}", container_name).stdout.strip()
        observed_platform = run(
            "docker", "image", "inspect", "--format", "{{.Os}}/{{.Architecture}}", image_id
        ).stdout.strip()
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired, RuntimeError, OSError) as error:
        print(f"[corrected-deployed-runtime-parity-smokes] {platform} capture failed: {error}", file=sys.stderr)
    finally:
        cleanup_result = run("docker", "rm", "--force", container_name, check=False)
        cleanup = "pass" if cleanup_result.returncode == 0 else "failure"
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
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("packet_root", type=Path)
    arguments = parser.parse_args()
    output_root = arguments.packet_root / "smokes"
    output_root.mkdir(parents=True, exist_ok=True)
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
    print(f"[corrected-deployed-runtime-parity-smokes] {result['result']}")
    return result["exit_code"]


if __name__ == "__main__":
    sys.exit(main())
