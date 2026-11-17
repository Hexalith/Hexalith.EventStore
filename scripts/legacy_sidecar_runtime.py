#!/usr/bin/env python3
"""Own a private Dapr 1.18.2 fixture beside the shared CI infrastructure."""

from __future__ import annotations

from contextlib import contextmanager
import json
import os
from pathlib import Path
import secrets
import signal
import socket
import subprocess
import sys
import time
import urllib.parse


RUNTIME_IMAGE = "daprio/dapr@sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59"
REDIS_IMAGE = "redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf"
OWNER_LABEL = "hexalith.eventstore.fixture-owner"


def command(arguments: list[str]) -> str:
    environment = os.environ.copy()
    if arguments[0] == "docker":
        for name in ("DOCKER_CONTEXT", "DOCKER_TLS_VERIFY", "DOCKER_CERT_PATH"):
            environment.pop(name, None)
    logs = arguments[0] == "docker" and arguments[3:4] == ["logs"]
    return subprocess.check_output(arguments, text=True, timeout=10 if logs else 180, env=environment,
                                   stderr=subprocess.STDOUT if logs else None).strip()


def local_docker_environment() -> dict[str, str]:
    """Validate the existing local selection before any fixture mutation."""
    environment = os.environ.copy()
    context, host = environment.get("DOCKER_CONTEXT"), environment.get("DOCKER_HOST")
    if context or not host:
        inspection = environment.copy()
        inspection.pop("DOCKER_CONTEXT", None)
        inspection.pop("DOCKER_HOST", None)
        result = subprocess.run(["docker", "context", "inspect", *([context] if context else [])],
                                check=True, timeout=30, capture_output=True, text=True, env=inspection)
        endpoint = json.loads(result.stdout)[0]["Endpoints"]["docker"]["Host"]
        if context and host and host != endpoint:
            raise ValueError("Conflicting Docker context and host selections refuse fixture preparation")
    else:
        endpoint = host
    address = urllib.parse.urlsplit(endpoint)
    if address.scheme != "unix" or address.netloc or not address.path.startswith("/") or "\n" in endpoint:
        raise ValueError("Product fixtures require a local Unix Docker endpoint")
    for name in ("DOCKER_CONTEXT", "DOCKER_TLS_VERIFY", "DOCKER_CERT_PATH"):
        environment.pop(name, None)
    environment["DOCKER_HOST"] = endpoint
    return environment


class LegacySidecarRuntime:
    """Track exact owned IDs; never rename, remove or reconfigure shared containers."""

    def __init__(self, directory: Path, *, execute=command):
        self.directory = directory
        self.execute = execute
        self.owner = "g6-oq8-" + secrets.token_hex(8)
        self.ids: list[str] = []
        self.cidfiles: list[Path] = []
        self.baseline: dict = {}
        self.endpoint = ""
        self.daemon = ""

    def docker(self, *arguments: str) -> str:
        observed = self.execute(["docker", "--host", self.endpoint, "info", "--format", "{{.ID}}"])
        if observed != self.daemon:
            raise ValueError("Fixture Docker daemon identity changed; mutation refused")
        return self.execute(["docker", "--host", self.endpoint, *arguments])

    def snapshot(self, ids: list[str]) -> dict:
        if not ids:
            return {}
        rows = json.loads(self.docker("inspect", *ids))
        # Retain structural identity only; shared container environment values
        # and other potentially sensitive settings never enter the receipt.
        return {row["Id"]: {"name": row["Name"], "image": row["Image"],
                            "ports": row["HostConfig"]["PortBindings"],
                            "mounts": row["Mounts"], "networks": row["NetworkSettings"]["Networks"]} for row in rows}

    def create(self, kind: str, image: str, internal_port: int | None, *arguments: str, host_port: int = 0) -> tuple[str, int]:
        name = self.owner + "-" + kind
        cidfile = self.directory / (kind + ".cid")
        self.cidfiles.append(cidfile)  # Reconcile even when run fails after creation.
        options = ["run", "-d"] if internal_port else ["create"]
        options += ["--name", name, "--cidfile", str(cidfile), "--label", OWNER_LABEL + "=" + self.owner]
        if internal_port:
            options += ["--publish", f"127.0.0.1:{host_port or ''}:{internal_port}"]
        if kind in ("placement", "scheduler", "extract"):
            options += ["--entrypoint", "./" + ("daprd" if kind == "extract" else kind), "--tmpfs", "/tmp:rw"]
        else:
            options += ["--tmpfs", "/data:rw"]
        try:
            identity = self.docker(*options, image, *arguments).strip()
        finally:
            if cidfile.is_file():
                identity = cidfile.read_text().strip()
                if identity not in self.ids:
                    self.ids.append(identity)
        if identity not in self.ids:
            raise ValueError("Fixture container creation did not record an exact ID")
        if not internal_port:
            return identity, 0
        observed = json.loads(self.docker("inspect", identity))[0]
        binding = observed["NetworkSettings"]["Ports"][f"{internal_port}/tcp"]
        if len(binding) != 1 or binding[0]["HostIp"] != "127.0.0.1":
            raise ValueError("Fixture container must publish one loopback-only port")
        return identity, int(binding[0]["HostPort"])

    def start(self) -> dict[str, str]:
        self.directory.mkdir(parents=True, exist_ok=False)
        # Pin the endpoint before any mutation and refuse remote contexts.
        context, host = os.environ.get("DOCKER_CONTEXT"), os.environ.get("DOCKER_HOST")
        if context or not host:
            rows = json.loads(self.execute(["docker", "context", "inspect", *([context] if context else [])]))
            endpoint = rows[0]["Endpoints"]["docker"]["Host"]
            if context and host and host != endpoint:
                raise ValueError("Conflicting Docker context and host")
        else:
            endpoint = host
        if not endpoint.startswith("unix:///") or "\n" in endpoint:
            raise ValueError("Product fixtures require a local Unix Docker endpoint")
        self.endpoint = endpoint
        self.daemon = self.execute(["docker", "--host", endpoint, "info", "--format", "{{.ID}}"])
        self.baseline = self.snapshot(self.docker("ps", "-aq", "--no-trunc").splitlines())
        for image in (RUNTIME_IMAGE, REDIS_IMAGE):
            self.docker("pull", image)
        # Resolve the immutable image to its content ID once; every container
        # in this fixture then uses those exact local bytes.
        runtime = json.loads(self.docker("image", "inspect", RUNTIME_IMAGE))[0]["Id"]
        extraction, _ = self.create("extract", runtime, None)
        daprd = self.directory / "daprd"
        self.docker("cp", extraction + ":/daprd", str(daprd))
        daprd.chmod(0o755)
        if self.execute([str(daprd), "--version"]) != "1.18.2":
            raise ValueError("Private product fixture must retain daprd 1.18.2")
        redis, redis_port = self.create("redis", REDIS_IMAGE, 6379)
        placement, placement_port = self.create("placement", runtime, 50005, "--port", "50005")
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            scheduler_port = listener.getsockname()[1]
        scheduler, _ = self.create("scheduler", runtime, 50006, "--etcd-data-dir=/tmp/etcd",
                                   f"--override-broadcast-host-port=127.0.0.1:{scheduler_port}",
                                   "--etcd-client-listen-address=0.0.0.0", host_port=scheduler_port)
        deadline = time.monotonic() + 60
        for port in (redis_port, placement_port, scheduler_port):
            while True:
                try:
                    with socket.create_connection(("127.0.0.1", port), timeout=1):
                        break
                except OSError:
                    if time.monotonic() >= deadline:
                        raise TimeoutError("Private product infrastructure did not become ready")
                    time.sleep(0.1)
        # OQ8's existing contract requires names, while diagnostics may use IDs.
        return {"EVENTSTORE_TEST_DAPRD_PATH": str(daprd), "EVENTSTORE_TEST_REDIS_PORT": str(redis_port),
                "EVENTSTORE_TEST_PLACEMENT_PORT": str(placement_port), "EVENTSTORE_TEST_SCHEDULER_PORT": str(scheduler_port),
                "EVENTSTORE_TEST_REDIS_CONTAINER": redis, "EVENTSTORE_TEST_PLACEMENT_CONTAINER": placement,
                "EVENTSTORE_TEST_SCHEDULER_CONTAINER": scheduler,
                "HEXALITH_OQ8_DAPRD_PATH": str(daprd), "HEXALITH_OQ8_CONFIGURATION": "Release",
                "HEXALITH_OQ8_PLACEMENT_CONTAINER": self.owner + "-placement",
                "HEXALITH_OQ8_SCHEDULER_CONTAINER": self.owner + "-scheduler",
                "HEXALITH_OQ8_REDIS_ENDPOINT": f"127.0.0.1:{redis_port}", "HEXALITH_OQ8_NAMESPACE": self.owner}

    def close(self, *, diagnostics: bool = False) -> None:
        failures = []
        for path in self.cidfiles:
            if path.is_file() and path.read_text().strip() not in self.ids:
                self.ids.append(path.read_text().strip())
        for identity in reversed(self.ids):
            try:
                observed = json.loads(self.docker("inspect", identity))[0]
                if observed["Id"] != identity or observed["Config"]["Labels"].get(OWNER_LABEL) != self.owner:
                    raise ValueError("Unrelated container identity; fixture cleanup refused")
                if diagnostics:
                    # Only verified owned IDs are inspected. Native state/logs
                    # are bounded, and diagnostic failure never prevents removal.
                    try:
                        print("Fixture container " + identity + " state: " + json.dumps(observed.get("State", {}))[:4096], file=sys.stderr)
                        print(self.docker("logs", "--tail", "80", identity)[-16384:], file=sys.stderr)
                    except Exception:
                        pass
                self.docker("rm", "-f", identity)
            except Exception as error:
                failures.append(str(error))
        if self.endpoint:
            try:
                if self.snapshot(list(self.baseline)) != self.baseline:
                    failures.append("Shared Docker infrastructure changed during product fixture execution")
            except Exception as error:
                failures.append(str(error))
        if failures:
            raise ValueError("; ".join(failures))


@contextmanager
def legacy_sidecar_runtime(directory: Path):
    fixture = LegacySidecarRuntime(directory)
    previous = {kind: signal.signal(kind, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
                for kind in (signal.SIGTERM, signal.SIGINT)}
    try:
        yield fixture.start()
    finally:
        failed = sys.exc_info()[0] is not None
        for kind in previous:
            signal.signal(kind, signal.SIG_IGN)
        try:
            fixture.close(diagnostics=failed)
        finally:
            for kind, handler in previous.items():
                signal.signal(kind, handler)
