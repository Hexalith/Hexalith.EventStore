#!/usr/bin/env python3
"""Disposable stock-provider diagnostic; not a production receipt implementation.

Requires Docker and cached/pullable pinned images. Uses the actor state HTTP
transaction protocol with a minimal actor host, not the .NET actor SDK. The
negative result is a provider capability check, not full actor qualification.
"""

import argparse
import base64
import hashlib
import http.client
import http.server
import json
import secrets
import socket
import subprocess
import tempfile
import threading
import time
import urllib.error
import urllib.request
from pathlib import Path

DAPR = "daprio/dapr@sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59"
POSTGRES = "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"


def docker(*args):
    return subprocess.check_output(["docker", *args], text=True, stderr=subprocess.PIPE).strip()


def port():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def request(url, method="GET", data=None):
    req = urllib.request.Request(url, data=data, method=method)
    if data is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=10) as response:
        return response.status, dict(response.headers), response.read()


def wait_for(check, seconds=60):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        try:
            return check()
        except (OSError, subprocess.CalledProcessError):
            time.sleep(0.2)
    raise TimeoutError("Owned probe resource did not become ready")


class ActorHost(http.server.BaseHTTPRequestHandler):
    """Only activation/configuration endpoints; state writes use the real sidecar."""

    def log_message(self, *_):
        pass

    def do_GET(self):
        body = json.dumps({"entities": ["ProbeActor"], "actorIdleTimeout": "1h"}).encode() if self.path == "/dapr/config" else b""
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(body)

    def do_PUT(self):
        self.rfile.read(int(self.headers.get("Content-Length", "0")))
        self.send_response(200)
        self.end_headers()

    def do_DELETE(self):
        self.send_response(200)
        self.end_headers()


def run(output):
    output.mkdir(parents=True, exist_ok=True)
    prefix = "es66-spike-" + secrets.token_hex(5)
    owned = []
    server = None
    sidecar = None
    result = {"scope": "local provider diagnostic; no production authority", "dapr_image": DAPR, "postgres_image": POSTGRES}
    try:
        with tempfile.TemporaryDirectory(prefix=prefix) as work:
            work = Path(work)
            work.chmod(0o755)
            app_port, http_port, grpc_port, internal_port = [port() for _ in range(4)]
            server = http.server.ThreadingHTTPServer(("127.0.0.1", app_port), ActorHost)
            threading.Thread(target=server.serve_forever, daemon=True).start()
            password = secrets.token_hex(24)
            env = work / "postgres.env"
            env.write_text(f"POSTGRES_PASSWORD={password}\nPOSTGRES_DB=eventstore\n")
            env.chmod(0o600)

            def start(name, *args):
                name = prefix + "-" + name
                owned.append(name)
                return docker("run", "-d", "--name", name, *args)

            start("postgres", "--env-file", str(env), "-p", "127.0.0.1::5432", POSTGRES)
            db_port = int(docker("port", prefix + "-postgres", "5432/tcp").rsplit(":", 1)[1])
            wait_for(lambda: docker("exec", prefix + "-postgres", "pg_isready", "-U", "postgres", "-d", "eventstore"))
            start("placement", "-p", "127.0.0.1::50005", "--entrypoint", "/placement", DAPR,
                  "--port", "50005", "--enable-metrics=false")
            placement_port = int(docker("port", prefix + "-placement", "50005/tcp").rsplit(":", 1)[1])
            resources = work / "components"
            resources.mkdir()
            # Same stock v1 actor component; isolated local credentials and app scope.
            (resources / "statestore.yaml").write_text(f"""apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.postgresql
  version: v1
  metadata:
  - name: connectionString
    value: "host=127.0.0.1 port={db_port} user=postgres password={password} dbname=eventstore sslmode=disable"
  - name: actorStateStore
    value: "true"
scopes:
- eventstore
""")
            config = work / "config.yaml"
            config.write_text("apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: probe\nspec:\n  features:\n  - name: SchedulerReminders\n    enabled: false\n")
            binary_container = prefix + "-binary"
            owned.append(binary_container)
            docker("create", "--name", binary_container, "--entrypoint", "/daprd", DAPR, "--version")
            binary = work / "daprd"
            docker("cp", binary_container + ":/daprd", str(binary))
            binary.chmod(0o700)
            sidecar = subprocess.Popen([str(binary),
                  "--app-id", "eventstore", "--app-port", str(app_port), "--dapr-http-port", str(http_port),
                  "--dapr-grpc-port", str(grpc_port), "--dapr-internal-grpc-port", str(internal_port),
                  "--placement-host-address", f"127.0.0.1:{placement_port}", "--scheduler-host-address", "",
                  "--resources-path", str(resources), "--config", str(config), "--enable-metrics=false"],
                  stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            root = f"http://127.0.0.1:{http_port}"
            wait_for(lambda: request(root + "/v1.0/healthz"))
            wait_for(lambda: request(root + "/v1.0/actors/ProbeActor/probe/method/activate", "PUT", b"{}"))
            actor = root + "/v1.0/actors/ProbeActor/probe/state"
            payload = b'{ "z": 1, "a": "\\u0061" }'
            encoded = base64.b64encode(payload)
            first = b'{ "generation": 1, "payload": "' + encoded + b'", "type": "Probe" }'
            head = b'{ "generation": 1, "lastSequence": 1 }'

            def transaction(event, head_value, event_key=b"event-1"):
                return b'[{"operation":"upsert","request":{"key":"' + event_key + b'","value":' + event + b'}},{"operation":"upsert","request":{"key":"head","value":' + head_value + b'}}]'

            tx1 = transaction(first, head)
            status, _, _ = request(actor, "POST", tx1)
            assert status == 204
            _, headers1, raw1 = request(actor + "/event-1")
            _, _, head1 = request(actor + "/head")
            assert json.loads(raw1) == json.loads(first)
            assert base64.b64decode(json.loads(raw1)["payload"]) == payload
            assert json.loads(head1)["generation"] == 1
            (output / "submitted-event-1.json").write_bytes(first)
            (output / "readback-event-1.json").write_bytes(raw1)
            (output / "readback-head-1.json").write_bytes(head1)

            # Complete the backend commit, deliberately discard its response before
            # the caller receives it. This is post-commit ACK loss, not an in-flight
            # crash/fencing qualification. No claim is made for rollback ambiguity.
            observed = []

            class DropAck(http.server.BaseHTTPRequestHandler):
                def log_message(self, *_):
                    pass

                def do_POST(self):
                    data = self.rfile.read(int(self.headers["Content-Length"]))
                    observed.append(request(actor, "POST", data)[0])
                    self.close_connection = True
                    self.connection.shutdown(socket.SHUT_RDWR)
                    self.connection.close()

            proxy = http.server.ThreadingHTTPServer(("127.0.0.1", 0), DropAck)
            threading.Thread(target=proxy.serve_forever, daemon=True).start()
            second = b'{"generation":2,"payload":"' + encoded + b'","type":"Probe"}'
            tx2 = transaction(second, b'{"generation":2,"lastSequence":2}', b"event-2")
            try:
                try:
                    request(f"http://127.0.0.1:{proxy.server_port}/", "POST", tx2)
                    raise AssertionError("Caller unexpectedly received an acknowledgment")
                except http.client.RemoteDisconnected:
                    result["caller_lost_acknowledgment"] = True
            finally:
                proxy.shutdown()
                proxy.server_close()
            assert observed == [204]
            _, _, raw2 = request(actor + "/event-2")
            assert json.loads(raw2)["generation"] == 2
            _, _, head2 = request(actor + "/head")
            assert json.loads(head2)["generation"] == 2
            # Diagnostic SQL only. Never add this private-table access to the
            # app-owned metadata adapter or treat xmin as a durable receipt.
            sql = "SELECT key, value::text, xmin::text FROM public.state ORDER BY key"
            snapshot2 = docker("exec", prefix + "-postgres", "psql", "-U", "postgres", "-d", "eventstore", "-At", "-c", sql)
            third = b'{"generation":3,"payload":"' + encoded + b'","type":"Probe"}'
            assert request(actor, "POST", transaction(third, b'{"generation":3,"lastSequence":3}', b"event-3"))[0] == 204
            _, _, raw3 = request(actor + "/event-3")
            assert json.loads(raw3)["generation"] == 3
            snapshot3 = docker("exec", prefix + "-postgres", "psql", "-U", "postgres", "-d", "eventstore", "-At", "-c", sql)
            schema = docker("exec", prefix + "-postgres", "psql", "-U", "postgres", "-d", "eventstore", "-At", "-c",
                            "SELECT table_name,column_name,data_type FROM information_schema.columns WHERE table_schema='public' ORDER BY table_name,ordinal_position")
            (output / "provider-after-generation-2.txt").write_text(snapshot2 + "\n")
            (output / "provider-after-generation-3.txt").write_text(snapshot3 + "\n")
            (output / "provider-schema.txt").write_text(schema + "\n")
            assert "state|value|jsonb" in schema
            head_rows = [line for line in snapshot3.splitlines() if "||head|" in line]
            assert len(head_rows) == 1 and '"generation": 3' in head_rows[0]
            assert '"generation": 1' not in head_rows[0] and '"generation": 2' not in head_rows[0]
            _, _, retained_first = request(actor + "/event-1")
            assert json.loads(retained_first) == json.loads(first)
            result.update({
                "captured_at_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                "repository_head": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
                "probe_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                "runtime_version": docker("run", "--rm", "--entrypoint", "/daprd", DAPR, "--version"),
                "dapr_image_id": docker("image", "inspect", DAPR, "--format", "{{.Id}}"),
                "dapr_repo_digests": json.loads(docker("image", "inspect", DAPR, "--format", "{{json .RepoDigests}}")),
                "postgres_version": docker("exec", prefix + "-postgres", "postgres", "--version"),
                "exact_envelope_bytes_preserved": raw1 == first,
                "embedded_payload_bytes_preserved": True,
                "submitted_sha256": hashlib.sha256(first).hexdigest(),
                "readback_sha256": hashlib.sha256(raw1).hexdigest(),
                "actor_read_etag": headers1.get("ETag", headers1.get("Etag")),
                "post_ack_loss_current_generation_recovered": 2,
                "after_later_write_current_generation": 3,
                "earlier_event_retained": True,
                "historical_head_images_in_current_provider_rows": False,
                "generation_labels": "test-authored application labels; not provider receipts",
                "committed_generation_receipt": "not supplied by stock component; see source/API audit",
                "conclusion": "NO-GO for stock component as exact-byte historical committed-generation authority",
            })
            result["artifact_sha256"] = {
                path.name: hashlib.sha256(path.read_bytes()).hexdigest()
                for path in sorted(output.iterdir()) if path.name != "result.json"
            }
    finally:
        if sidecar is not None:
            sidecar.terminate()
            try:
                sidecar.wait(timeout=10)
            except subprocess.TimeoutExpired:
                sidecar.kill()
                sidecar.wait(timeout=10)
        if server is not None:
            server.shutdown()
            server.server_close()
        for name in reversed(owned):
            try:
                # Logs are intentionally not retained: component metadata may include credentials.
                docker("rm", "-f", "-v", name)
            except subprocess.CalledProcessError:
                pass
        result["owned_containers_remaining"] = docker("ps", "-a", "--filter", "name=" + prefix, "--format", "{{.Names}}")
        (output / "result.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    run(parser.parse_args().output)
