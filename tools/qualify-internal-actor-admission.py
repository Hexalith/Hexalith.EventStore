#!/usr/bin/env python3
"""Production HTTP/Dapr/Redis regression; owns every process/container it removes.

Build LiveSidecar.Tests in the selected configuration first (Debug by default).
No application persistence is replaced.
The existing OQ8 hosting startup adds only the Counter intent adapter, clock and
boundary counter. Redis reads below are diagnostics, never application writes.
"""
import argparse
import base64
import datetime
import hashlib
import hmac
import json
import os
from pathlib import Path
import secrets
import signal
import shutil
import socket
import subprocess
import tempfile
import threading
import time
import traceback
import urllib.error
import urllib.parse
import urllib.request


def encode(value):
    return base64.urlsafe_b64encode(value).decode().rstrip("=")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


_allocated_ports = set()


def port():
    while True:
        with socket.socket() as sock:
            sock.bind(("127.0.0.1", 0))
            value = sock.getsockname()[1]
        if value not in _allocated_ports:
            _allocated_ports.add(value)
            return value


class Qualification:
    def __init__(self, args):
        self.args = args
        self.root = Path(__file__).resolve().parents[1]
        self.out = Path(args.evidence).resolve()
        self.out.mkdir(parents=True, exist_ok=False)
        self.runtime = Path(tempfile.mkdtemp(prefix="eventstore-actor-admission-"))
        self.run_id = "admission-" + secrets.token_hex(6)
        self.containers = []
        self.processes = []
        self.logs = []
        self.nodes = {}
        self.checks = []
        self.receipts = []
        self.signing_key = encode(secrets.token_bytes(48))
        self.digest_key = secrets.token_bytes(32)
        self.tenant = "tenant-admission"
        self.started = datetime.datetime.now(datetime.timezone.utc).isoformat()
        self.test_output = self.root / "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin" / args.configuration / "net10.0"
        self.sample_output = (Path(args.runtime) / "sample" if args.runtime else
                              self.root / "samples/Hexalith.EventStore.Sample/bin" / args.configuration / "net10.0")
        self.eventstore_output = (Path(args.runtime) / "eventstore" if args.runtime else
                                  self.root / "src/Hexalith.EventStore/bin" / args.configuration / "net10.0")

    def save(self, name, value):
        (self.out / name).write_text(json.dumps(value, indent=2) + "\n")

    def check(self, name, condition, **observed):
        self.checks.append({"name": name, "passed": bool(condition), **observed})
        self.save("checks.json", self.checks)
        if not condition:
            raise AssertionError(name + ": " + json.dumps(observed))

    def run(self, *argv, check=True, timeout=60):
        start = time.monotonic()
        result = subprocess.run(argv, text=True, capture_output=True, timeout=timeout)
        self.receipts.append({"argv": argv, "exit_code": result.returncode,
                              "elapsed_seconds": time.monotonic() - start,
                              "stdout": result.stdout, "stderr": result.stderr})
        self.save("infrastructure-commands.json", self.receipts)
        if check:
            result.check_returncode()
            return result.stdout.strip()
        return result

    def request(self, url, data=None, headers=None, method=None):
        content = None if data is None else json.dumps(data).encode()
        fields = {"Content-Type": "application/json", **(headers or {})}
        req = urllib.request.Request(url, content, fields, method=method)
        try:
            response = urllib.request.urlopen(req, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            body = response.read().decode()
            return response.code, dict(response.headers), body

    def wait(self, url, predicate=lambda status, body: status == 200):
        deadline = time.monotonic() + 60
        last = None
        while time.monotonic() < deadline:
            for process in self.processes:
                if process.poll() is not None and not getattr(process, "owned_stopped", False):
                    raise RuntimeError("Owned child process exited; inspect retained logs")
            try:
                status, _, body = self.request(url)
                last = status, body
                if predicate(status, body):
                    return
            except (OSError, TimeoutError):
                pass
            time.sleep(0.2)
        raise TimeoutError(str((url, last)))

    def launch_container(self, kind, image, internal_port, *command, host_port=None):
        name = self.run_id + "-" + kind
        cidfile = self.runtime / (kind + ".cid")
        argv = ["docker", "run", "--rm", "-d", "--name", name, "--cidfile", str(cidfile),
                "--label", "hexalith.qualification.run-id=" + self.run_id,
                "-p", "127.0.0.1:" + (str(host_port) if host_port else "") + ":" + str(internal_port)]
        if kind in ("placement", "scheduler"):
            argv += ["--entrypoint", "./" + kind]
        index = len(self.containers)
        self.containers.append((name, None))
        try:
            self.run(*argv, image, *command)
        finally:
            if cidfile.exists():
                self.containers[index] = (name, cidfile.read_text().strip())
        identity = self.containers[index][1]
        published = self.run("docker", "port", identity, str(internal_port)).split(":")[-1]
        self.save(kind + "-image.json", json.loads(self.run("docker", "image", "inspect", image)))
        return identity, int(published)

    def launch(self, label, argv, env, cwd):
        out = open(self.out / (label + ".stdout.log"), "w")
        err = open(self.out / (label + ".stderr.log"), "w")
        self.logs += [out, err]
        process = subprocess.Popen(argv, env=env, cwd=cwd, stdout=out, stderr=err)
        self.processes.append(process)
        self.save("processes.json", [{"pid": p.pid, "argv": p.args} for p in self.processes])
        return process

    def setup(self):
        self.redis_id, redis_port = self.launch_container("redis", "redis:7-alpine", 6379)
        _, placement_port = self.launch_container("placement", "daprio/dapr:1.18.7", 50005, "--port", "50005")
        scheduler_port = port()
        self.launch_container("scheduler", "daprio/dapr:1.18.7", 50006,
                              "--etcd-data-dir=/tmp/admission-etcd",
                              "--override-broadcast-host-port=127.0.0.1:" + str(scheduler_port),
                              "--etcd-client-listen-address=0.0.0.0", host_port=scheduler_port)
        components = self.runtime / "components"
        components.mkdir()
        for name, kind, extra in [("statestore", "state.redis", "    - name: actorStateStore\n      value: 'true'\n    - name: keyPrefix\n      value: none\n"), ("pubsub", "pubsub.redis", "")]:
            (components / (name + ".yaml")).write_text(
                "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: " + name +
                "\nspec:\n  type: " + kind + "\n  version: v1\n  metadata:\n    - name: redisHost\n      value: '127.0.0.1:" + str(redis_port) + "'\n    - name: redisPassword\n      value: ''\n" + extra)
        config = self.runtime / "discovery.yaml"
        config.write_text("apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: isolated-admission\nspec:\n  nameResolution:\n    component: sqlite\n    version: v1\n    configuration:\n      connectionString: " + json.dumps(str(self.runtime / "discovery.sqlite")) + "\n")
        self.clock = self.runtime / "clock.txt"
        # .NET UTC ticks, fixed only for admission retention; rate limiters use real time.
        self.clock.write_text(str(621355968000000000 + int(time.time()) * 10000000))
        self.save("infrastructure.json", {"runtime": str(self.runtime), "namespace": self.run_id,
                  "redis_port": redis_port, "placement_port": placement_port, "scheduler_port": scheduler_port,
                  "daprd": str(Path.home() / ".dapr/bin/daprd"),
                  "daprd_sha256": sha(Path.home() / ".dapr/bin/daprd"),
                  "components": {p.name: p.read_text() for p in components.iterdir()}, "discovery": config.read_text()})
        for label, source, dll in [
            ("sample", self.sample_output, "Hexalith.EventStore.Sample.dll"),
            ("eventstore", self.eventstore_output, "Hexalith.EventStore.dll")]:
            dest = self.runtime / label
            shutil.copytree(source, dest)
            if label == "sample":
                # Separate source-reference build graphs can leave stale SDK images
                # in Sample output. Qualify one exact graph across both processes.
                for image in self.eventstore_output.glob("Hexalith.EventStore.*"):
                    if image.suffix in (".dll", ".pdb") and (dest / image.name).exists():
                        shutil.copy2(image, dest / image.name)
            for file in self.test_output.glob("*.dll"):
                if not (dest / file.name).exists():
                    shutil.copy2(file, dest / file.name)
                    if file.with_suffix(".pdb").exists():
                        shutil.copy2(file.with_suffix(".pdb"), dest / file.with_suffix(".pdb").name)
            app_port, http_port, grpc_port, internal_port, metrics_port = [port() for _ in range(5)]
            env = os.environ.copy()
            env.update({"DOTNET_ENVIRONMENT": "Testing", "ASPNETCORE_ENVIRONMENT": "Testing",
                        "ASPNETCORE_URLS": "http://127.0.0.1:" + str(app_port),
                        "DAPR_APP_ID": label, "DAPR_HTTP_PORT": str(http_port), "DAPR_GRPC_PORT": str(grpc_port),
                        "DAPR_HTTP_ENDPOINT": "http://127.0.0.1:" + str(http_port),
                        "DAPR_GRPC_ENDPOINT": "http://127.0.0.1:" + str(grpc_port),
                        "APP_API_TOKEN": secrets.token_hex(32),
                        "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES": "Hexalith.EventStore.Server.LiveSidecar.Tests",
                        "HEXALITH_OQ8_CLOCK_FILE": str(self.clock),
                        "HEXALITH_LIVE_ADMISSION_CATALOG": json.dumps({
                            "RunId": self.run_id, "HostId": label, "AppId": label,
                            "ConsumerAppId": "eventstore", "SampleAppId": "sample",
                            "Tenants": [self.tenant], "ActiveGeneration": "local1", "Readers": []}),
                        "HEXALITH_OQ8_BOUNDARY_COUNTER_FILE": str(self.runtime / (label + "-count.txt")),
                        "DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE": "false",
                        "Authentication__JwtBearer__Issuer": "admission-qualification",
                        "Authentication__JwtBearer__Audience": "hexalith-eventstore",
                        "Authentication__JwtBearer__SigningKey": self.signing_key,
                        "Authentication__JwtBearer__RequireHttpsMetadata": "false",
                        "Authentication__JwtBearer__AllowInsecureSymmetricKey": "true",
                        "Logging__LogLevel__Default": "Warning"})
            if label == "sample":
                env["EventStore__DomainService__AppId"] = "sample"
            else:
                env.update({"EventStore__IdempotencyAdmission__Enabled": "true",
                            "EventStore__IdempotencyAdmission__ActiveDigestKeyVersion": "local1",
                            "EventStore__IdempotencyAdmission__DigestKeySource": "Configuration",
                            "EventStore__IdempotencyAdmission__DigestKeys__local1": base64.b64encode(self.digest_key).decode(),
                            "EventStore__OpenApi__Enabled": "false", "EventStore__SignalR__Enabled": "false",
                            "EventStore__ProjectionDispatch__RetryWorkerInterval": "00:10:00",
                            "EventStore__RateLimiting__PermitLimit": "100",
                            "EventStore__RateLimiting__WindowSeconds": "60",
                            "EventStore__RateLimiting__SegmentsPerWindow": "1",
                            "EventStore__RateLimiting__ConsumerPermitLimit": "100",
                            "EventStore__RateLimiting__ConsumerWindowSeconds": "60",
                            "EventStore__RateLimiting__ConsumerSegmentsPerWindow": "1",
                            "EventStore__RateLimiting__QueueLimit": "0"})
                for field, value in {"AppId": "sample", "MethodName": "process", "TenantId": "*", "Domain": "counter", "Version": "v1"}.items():
                    env["EventStore__DomainServices__Registrations__*|counter|v1__" + field] = value
                for domain in ["tenants", "global-administrators"]:
                    env["EventStore__DomainServices__Registrations__system|" + domain + "|v1__AppId"] = "sample"
            app_argv = ["dotnet", "exec", "--additional-deps", str(self.test_output / "Hexalith.EventStore.Server.LiveSidecar.Tests.deps.json"),
                        "--additionalprobingpath", str(self.test_output), "--additionalprobingpath", str(Path.home() / ".nuget/packages"), str(dest / dll)]
            sidecar_argv = [str(Path.home() / ".dapr/bin/daprd"), "--app-id", label, "--app-port", str(app_port),
                            "--app-protocol", "http", "--app-channel-address", "127.0.0.1",
                            "--dapr-http-port", str(http_port), "--dapr-grpc-port", str(grpc_port),
                            "--dapr-internal-grpc-port", str(internal_port), "--metrics-port", str(metrics_port),
                            "--resources-path", str(components), "--config", str(config), "--log-level", "warn",
                            "--placement-host-address", "127.0.0.1:" + str(placement_port),
                            "--scheduler-host-address", "127.0.0.1:" + str(scheduler_port)]
            self.nodes[label] = {"app_argv": app_argv, "sidecar_argv": sidecar_argv, "env": env, "cwd": dest,
                                 "app_url": env["ASPNETCORE_URLS"], "sidecar_url": env["DAPR_HTTP_ENDPOINT"], "generation": 0}
            self.start_node(label)
        self.save("runtime-inputs.json", {label: {"directory": str(n["cwd"]), "files": {p.name: sha(p) for p in n["cwd"].glob("*") if p.is_file()},
                    "rate_configuration": {k: v for k, v in n["env"].items() if k.startswith("EventStore__RateLimiting__")}} for label, n in self.nodes.items()})

    def start_node(self, label):
        n = self.nodes[label]
        n["generation"] += 1
        suffix = label + "-" + str(n["generation"])
        n["app"] = self.launch(suffix + "-app", n["app_argv"], n["env"], n["cwd"])
        n["sidecar"] = self.launch(suffix + "-sidecar", n["sidecar_argv"], {**os.environ, "APP_API_TOKEN": n["env"]["APP_API_TOKEN"], "NAMESPACE": self.run_id}, n["cwd"])
        self.wait(n["app_url"] + "/alive")
        self.wait(n["sidecar_url"] + "/v1.0/healthz/outbound", lambda s, b: s in (200, 204))
        if label == "eventstore":
            self.wait(n["sidecar_url"] + "/v1.0/metadata", lambda s, b: s == 200 and json.loads(b).get("actorRuntime", {}).get("runtimeStatus") == "RUNNING")
        mapped = {}
        for line in Path("/proc/" + str(n["app"].pid) + "/maps").read_text().splitlines():
            fields = line.split(maxsplit=5)
            if len(fields) == 6 and fields[5].endswith(".dll"):
                path = fields[5]
                mapped[path] = {"dll_sha256": sha(path), "pdb_sha256": sha(Path(path).with_suffix(".pdb")) if Path(path).with_suffix(".pdb").exists() else None}
        self.save(suffix + "-mapped-images.json", {"pid": n["app"].pid, "images": mapped})

    def actor(self, identity, method, data=None):
        url = self.nodes["eventstore"]["sidecar_url"] + "/v1.0/actors/IdempotencyAdmissionActor/" + urllib.parse.quote(identity, safe="") + "/method/" + method
        return self.request(url, data, method="PUT")

    def token(self, tenant=None, subject="qualification-runner"):
        header = encode(b'{"alg":"HS256","typ":"JWT"}')
        body = encode(json.dumps({"sub": subject, "iss": "admission-qualification", "aud": "hexalith-eventstore",
                                 "exp": int(time.time()) + 3600, "tenants": [tenant or self.tenant],
                                 "tenant_id": tenant or self.tenant, "domains": ["counter"], "permissions": ["commands:*"],
                                 "eventstore:surface": "ui"}).encode())
        unsigned = header + "." + body
        return unsigned + "." + encode(hmac.digest(self.signing_key.encode(), unsigned.encode(), "sha256"))

    def submit(self, key, payload=None):
        # Valid ULID-shaped identities; idempotency preserves the first accepted identity.
        alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"
        identity = "01" + "".join(secrets.choice(alphabet) for _ in range(24))
        data = {"messageId": identity, "tenant": self.tenant, "domain": "counter", "aggregateId": "admission-counter",
                "commandType": "IncrementCounter", "payload": payload or {"counterId": "admission-counter"},
                "correlationId": identity, "idempotencyKey": key}
        return self.request(self.nodes["eventstore"]["app_url"] + "/api/v1/commands", data,
                            {"Authorization": "Bearer " + self.token()})

    def snapshot(self, phase):
        keys = self.run("docker", "exec", self.redis_id, "redis-cli", "--scan").splitlines()
        values = {}
        for key in keys:
            if self.run("docker", "exec", self.redis_id, "redis-cli", "TYPE", key) == "hash":
                raw = self.run("docker", "exec", self.redis_id, "redis-cli", "--raw", "HGET", key, "data")
                if raw:
                    try:
                        values[key] = json.loads(raw)
                    except json.JSONDecodeError:
                        values[key] = raw
        self.save(phase + "-persisted.json", values)
        return values

    @staticmethod
    def admission(values, identity):
        return values["eventstore||IdempotencyAdmissionActor||" + identity + "||admission"]

    def assert_admission(self, identity, expected, phase):
        # Snapshot first: each refusal must leave the entire durable record unchanged immediately.
        actual = self.admission(self.snapshot(phase), identity)
        self.check(phase + "_persisted_admission_unchanged", actual == expected, admission=actual)
        status, _, body = self.actor(identity, "InspectAsync")
        inspected = json.loads(body)
        self.check(phase + "_authenticated_inspection", status == 200 and inspected.get("exists") is True
                   and inspected.get("record") == expected, status=status, inspection=inspected)

    def assert_saturated(self, identity, phase):
        node = self.nodes["eventstore"]
        path = "/actors/IdempotencyAdmissionActor/" + urllib.parse.quote(identity, safe="") + "/method/InspectAsync"
        status, headers, body = self.request(node["app_url"] + path, method="PUT",
                                             headers={"dapr-api-token": node["env"]["APP_API_TOKEN"]})
        retry = next((value for key, value in headers.items() if key.lower() == "retry-after"), "")
        self.check(phase + "_actor_a_remains_throttled", status == 429 and retry.isdigit() and int(retry) > 0,
                   status=status, headers=headers, rejection=body)

    def saturate_actor(self, identity, phase):
        statuses = []
        for _ in range(105):
            status, _, body = self.actor(identity, "InspectAsync")
            statuses.append(status)
            if status != 200:
                break
        self.check(phase + "_actual_dapr_actor_saturated", statuses[-1] == 500
                   and json.loads(body).get("errorCode") == "ERR_ACTOR_INVOKE_METHOD" and "(429)" in body,
                   callback_statuses=statuses, rejection=body)
        self.assert_saturated(identity, phase)

    def authority(self, identity, request, expected, phase):
        status, _, body = self.actor(identity, "EvaluateAuthorityAsync", request)
        self.check(phase + "_authority_decision", status == 200 and json.loads(body) == expected,
                   status=status, decision=body, expected=expected)

    def qualify(self):
        a = self.tenant + ":local1:saturated"
        self.saturate_actor(a, "initial")
        b = self.tenant + ":local1:recovery"
        request = {"schemaVersion": 2, "tenantPartition": self.tenant, "digestKeyVersion": "local1", "keyDigest": "recovery",
                   "verificationTag": "fixture-verification", "intentDigest": "fixture-intent", "retentionTier": 1,
                   "executionMessageId": "01J00000000000000000000000", "executionCorrelationId": "recovery-correlation"}
        self.assert_saturated(a, "before_admission_and_recovery")
        status, _, body = self.actor(b, "AdmitAsync", request)
        self.check("other_actor_admission_survives_saturation", status == 200, status=status, body=body)
        admitted = json.loads(body)
        fence = admitted.get("fencingToken", admitted.get("FencingToken"))
        status, _, body = self.actor(b, "BeginAsync", {"fencingToken": fence})
        self.check("begin_persisted", status == 200, status=status, body=body)
        status, _, body = self.actor(b, "MarkRecoveryAsync", {"fencingToken": fence, "state": 4})
        self.check("unknown_outcome_persisted", status == 200, status=status, body=body)
        self.assert_saturated(a, "after_admission_and_recovery")
        before = self.snapshot("before-replacement")
        row = self.admission(before, b)
        self.check("redis_proves_unknown_outcome", row["state"] == 4, state=row)
        key = "private-fixture-command-key"
        self.assert_saturated(a, "before_production_command")
        status, _, body = self.submit(key)
        self.check("production_command_completed_under_other_actor_saturation", status == 202, status=status, body=body)
        self.assert_saturated(a, "after_production_command")
        first = json.loads(body)
        completed = self.snapshot("completed-before-replacement")
        event_rows = {k: v for k, v in completed.items() if ":events:" in k}
        terminal = {k: v for k, v in completed.items() if k.endswith("||admission") and "recovery" not in k and v.get("state") == 5}
        statuses = {k: v for k, v in completed.items() if k.endswith(":status")}
        metadata = {k: v for k, v in completed.items() if k.endswith(":metadata")}
        self.check("persisted_single_event_terminal_admission", len(event_rows) == 1 and len(terminal) == 1,
                   events=event_rows, admissions=terminal, command_status=statuses)
        self.check("persisted_command_completed_and_sequence_one", len(statuses) == 1 and next(iter(statuses.values()))["status"] == 4
                   and len(metadata) == 1 and next(iter(metadata.values()))["currentSequence"] == 1,
                   command_status=statuses, aggregate_metadata=metadata)
        n = self.nodes["eventstore"]
        old_pids = [n["app"].pid, n["sidecar"].pid]
        for process in [n["app"], n["sidecar"]]:
            process.owned_stopped = True
            process.kill()
            process.wait(timeout=10)
        self.start_node("eventstore")
        new_pids = [n["app"].pid, n["sidecar"].pid]
        self.check("os_processes_replaced", set(old_pids).isdisjoint(new_pids), before=old_pids, after=new_pids)
        recovered = self.snapshot("after-replacement")
        self.check("unknown_outcome_survives_process_replacement", self.admission(recovered, b) == row)
        self.saturate_actor(a, "replacement")
        self.assert_admission(b, row, "before_unknown_refusal")
        status, _, body = self.actor(b, "AdmitAsync", request)
        unknown = json.loads(body)
        self.check("unknown_outcome_readback_is_exact", status == 200 and unknown.get("decision") == 4
                   and unknown.get("fencingToken") == fence
                   and unknown.get("executionMessageId") == request["executionMessageId"]
                   and unknown.get("executionCorrelationId") == request["executionCorrelationId"],
                   status=status, decision=unknown)
        authority = {"fencingToken": fence, "digestKeyVersion": "local1",
                     "executionMessageId": request["executionMessageId"],
                     "executionCorrelationId": request["executionCorrelationId"], "purpose": 1}
        self.authority(b, authority, 2, "unknown_execution")
        self.authority(b, {**authority, "purpose": 2}, 1, "unknown_reconciliation")
        self.assert_saturated(a, "before_unknown_refusal")
        status, _, body = self.actor(b, "BeginAsync", {"fencingToken": fence})
        self.check("unknown_outcome_refuses_reexecution", status == 500
                   and json.loads(body).get("errorCode") == "ERR_ACTOR_INVOKE_METHOD", status=status, body=body)
        self.assert_admission(b, row, "after_unknown_refusal")
        self.assert_saturated(a, "after_unknown_refusal")
        self.authority(b, {**authority, "purpose": 2, "fencingToken": fence + 1}, 2, "stale_reconciliation")
        self.assert_saturated(a, "before_stale_refusal")
        status, _, body = self.actor(b, "CompleteAsync", {"fencingToken": fence + 1, "result": {"accepted": True, "correlationId": "stale", "eventCount": 0}})
        self.check("stale_fence_refuses_completion", status == 500
                   and json.loads(body).get("errorCode") == "ERR_ACTOR_INVOKE_METHOD", status=status, body=body)
        self.assert_admission(b, row, "after_stale_refusal")
        self.assert_saturated(a, "after_stale_refusal")
        result = {"accepted": True, "errorMessage": None, "correlationId": "recovery-correlation", "eventCount": 0,
                  "resultPayload": "{\"reconciled\":true}", "backpressureExceeded": False,
                  "backpressurePendingCount": None, "backpressureThreshold": None, "rejectionEventType": None,
                  "failureReason": None, "resultPayloadWithheld": False}
        self.assert_saturated(a, "before_reconciled_completion")
        status, _, body = self.actor(b, "CompleteAsync", {"fencingToken": fence, "result": result})
        self.check("reconciled_completion_after_recovery", status == 200, status=status, body=body)
        self.assert_saturated(a, "after_reconciled_completion")
        status, _, body = self.actor(b, "AdmitAsync", {**request,
            "executionMessageId": "01J11111111111111111111111", "executionCorrelationId": "retry-correlation"})
        recovered_replay = json.loads(body)
        self.check("recovered_admission_readback_is_exact_replay", status == 200 and recovered_replay.get("decision") == 5
                   and recovered_replay.get("fencingToken") == fence and recovered_replay.get("replayResult") == result
                   and recovered_replay.get("executionMessageId") == row["executionMessageId"]
                   and recovered_replay.get("executionCorrelationId") == row["executionCorrelationId"],
                   status=status, replay=recovered_replay)
        status, _, body = self.submit(key)
        replay = json.loads(body)
        # HTTP correlation describes this attempt. The returned messageId and the
        # persisted admission execution identity/result describe the original work.
        self.check("public_command_replays_execution_identity", status == 202 and first["messageId"] == replay["messageId"],
                   status=status, first=first, replay=replay)
        status, _, body = self.submit(key, {"counterId": "changed-intent"})
        self.check("conflicting_intent_refused", status == 409, status=status, body=body)
        final = self.snapshot("final")
        self.check("no_second_event_or_terminal_mutation", {k: v for k, v in final.items() if ":events:" in k} == event_rows and all(final[k] == v for k, v in terminal.items()))
        recovery_terminal = self.admission(final, b)
        self.check("recovered_admission_has_durable_terminal_result", recovery_terminal["state"] == 5
                   and recovery_terminal["fencingToken"] == fence and recovery_terminal["replayResult"] == result
                   and recovery_terminal["executionMessageId"] == row["executionMessageId"]
                   and recovery_terminal["executionCorrelationId"] == row["executionCorrelationId"],
                   admission=recovery_terminal)
        path = "/actors/IdempotencyAdmissionActor/" + b + "/method/InspectAsync"
        for label, headers in [("missing", {}), ("wrong", {"dapr-api-token": "wrong"})]:
            status, _, body = self.request(n["app_url"] + path, headers=headers, method="PUT")
            self.check("actor_channel_" + label + "_denied", status in (401, 403), status=status, body=body)
        status, _, body = self.request(n["app_url"] + "/api/v1/trusted-effects", {}, {"dapr-api-token": n["env"]["APP_API_TOKEN"]})
        self.check("channel_alone_cannot_submit_workload_effect", status in (401, 403), status=status, body=body)
        for lane in ["tenant", "consumer"]:
            statuses = []
            for i in range(101):
                tenant = "quota-tenant" if lane == "tenant" else "quota-" + str(i)
                subject = "quota-subject" if lane == "consumer" else "quota-subject-" + str(i)
                status, headers, body = self.request(n["app_url"] + "/api/v1/commands/status/01J00000000000000000000000",
                                                    headers={"Authorization": "Bearer " + self.token(tenant, subject)})
                statuses.append(status)
                if status != 404:
                    break
            rejection = json.loads(body)
            retry = next((value for key, value in headers.items() if key.lower() == "retry-after"), "")
            self.check("public_" + lane + "_throttling_preserved", statuses == [404] * 100 + [429]
                       and rejection.get("tenantId") == tenant and rejection.get("consumerId") == subject
                       and retry.isdigit() and int(retry) > 0,
                       statuses=statuses, rejection=body, headers=headers)
        count_path = self.runtime / "sample-count.txt"
        self.check("actual_domain_process_called_once", count_path.read_text() == "1", calls=count_path.read_text())
        for label, node in self.nodes.items():
            shutil.copytree(node["cwd"], self.out / "qualified-runtime" / label)

    def cleanup(self):
        outcomes = {"processes": [], "containers": [], "logs": []}
        for process in reversed(self.processes):
            outcome = {"pid": process.pid}
            try:
                if process.poll() is None:
                    process.owned_stopped = True
                    process.terminate()
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait(timeout=5)
                outcome["exit_code"] = process.poll()
            except Exception as error:
                outcome["error"] = repr(error)
            outcome["absent"] = process.poll() is not None
            outcomes["processes"].append(outcome)
        for name, identity in reversed(self.containers):
            outcome = {"name": name, "id": identity}
            try:
                if not name.startswith(self.run_id + "-"):
                    raise RuntimeError("Refusing cleanup of an unowned container name")
                inspected = self.run("docker", "container", "inspect", name, check=False, timeout=10)
                if inspected.returncode == 0:
                    container = json.loads(inspected.stdout)[0]
                    if ((identity is not None and container["Id"] != identity)
                            or container["Config"].get("Labels", {}).get("hexalith.qualification.run-id") != self.run_id):
                        raise RuntimeError("Refusing cleanup of a container with a different ownership identity")
                    removed = self.run("docker", "rm", "-f", container["Id"], check=False, timeout=10)
                    outcome["remove_exit_code"] = removed.returncode
                # --rm may have removed it already. A successful independent inventory proves absence;
                # neither a failed inspect nor a failed rm by itself is accepted as evidence of absence.
                inventory = self.run("docker", "container", "ls", "--all", "--no-trunc",
                                     "--filter", "name=^/" + name + "$", "--format", "{{.ID}}", check=False, timeout=10)
                outcome["absent"] = inventory.returncode == 0 and not inventory.stdout.strip()
                outcome["remaining_ids"] = inventory.stdout.strip()
            except Exception as error:
                outcome["error"] = repr(error)
                outcome["absent"] = False
            outcomes["containers"].append(outcome)
        for log in self.logs:
            outcome = {"path": log.name}
            try:
                log.close()
            except Exception as error:
                outcome["error"] = repr(error)
            outcome["closed"] = log.closed
            outcomes["logs"].append(outcome)
        try:
            shutil.rmtree(self.runtime)
        except FileNotFoundError:
            pass
        except Exception as error:
            outcomes["runtime_error"] = repr(error)
        outcomes["runtime"] = str(self.runtime)
        outcomes["runtime_absent"] = not self.runtime.exists()
        outcomes["passed"] = (all(item["absent"] for item in outcomes["processes"] + outcomes["containers"])
                              and all(item["closed"] for item in outcomes["logs"]) and outcomes["runtime_absent"])
        self.save("cleanup.json", outcomes)
        return outcomes["passed"]


def parse_args(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", required=True)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug",
                        help="Build configuration shared by test, Sample and EventStore outputs (default: Debug)")
    parser.add_argument("--runtime", help="Frozen baseline runtime root containing eventstore/ and sample/")
    parser.add_argument("--owner-pid", type=int, help="Test runner PID; loss of this owner triggers graceful cleanup")
    return parser.parse_args(argv)


def main():
    args = parse_args()
    run = Qualification(args)
    qualification_passed = False
    cleanup_passed = False
    stop_monitor = threading.Event()
    terminating = False

    def terminate(signum, frame):
        nonlocal terminating
        if not terminating:
            terminating = True
            raise InterruptedError("Qualification interrupted; cleaning up owned resources")

    signal.signal(signal.SIGTERM, terminate)
    signal.signal(signal.SIGINT, terminate)

    def monitor_owner():
        while not stop_monitor.wait(0.5):
            if args.owner_pid:
                try:
                    os.kill(args.owner_pid, 0)
                except ProcessLookupError:
                    os.kill(os.getpid(), signal.SIGTERM)
                    return

    threading.Thread(target=monitor_owner, daemon=True).start()
    try:
        run.setup()
        run.qualify()
        qualification_passed = True
    except BaseException:
        (run.out / "failure.txt").write_text(traceback.format_exc())
    finally:
        # Once shutdown starts, repeated cancellation must not interrupt cleanup halfway through.
        terminating = True
        stop_monitor.set()
        try:
            cleanup_passed = run.cleanup()
        except BaseException:
            (run.out / "cleanup-failure.txt").write_text(traceback.format_exc())
        finally:
            run.save("result.json", {"passed": qualification_passed and cleanup_passed,
                                    "qualification_passed": qualification_passed, "cleanup_passed": cleanup_passed,
                                    "started_utc": run.started,
                                    "finished_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                                    "argv": os.sys.argv, "cwd": os.getcwd(), "script_sha256": sha(__file__)})
    return 0 if qualification_passed and cleanup_passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
