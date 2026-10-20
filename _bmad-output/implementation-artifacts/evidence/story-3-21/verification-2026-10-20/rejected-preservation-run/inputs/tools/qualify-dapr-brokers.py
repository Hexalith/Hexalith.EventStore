#!/usr/bin/env python3
"""Run Story 3.21 only on owned local resources; never connects to Kubernetes.

Docker is stage one of the retained rollout plan, not target-path proof. The
complete matrix always emits PASS or explicit FAIL; missing execution is FAIL.
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import signal
import socket
import ssl
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone

from dapr_broker_qualification import BROKERS, SCENARIOS, digest, evaluate, validate_profile, write_json

ROOT = Path(__file__).resolve().parents[1]


def utc():
    return datetime.now(timezone.utc).isoformat()


def port():
    with socket.socket() as channel:
        channel.bind(("127.0.0.1", 0))
        return channel.getsockname()[1]


class Run:
    def __init__(self, args):
        self.args = args
        self.output = args.output.resolve()
        self.profile = json.loads(args.profile.read_text())
        # Validate every retained path before copying bytes or creating any
        # output/private directory. A rejected profile has no side effects.
        validate_profile(self.profile, ROOT)
        self.output.mkdir(parents=True, exist_ok=False)
        self.receipts = []
        self.containers = []
        self.processes = []
        self.private_parent = Path(tempfile.mkdtemp(prefix="story-3-21-private-"))
        self.private_parent.chmod(0o700)
        # Container service users need traversal of the mount root. The host
        # parent remains 0700 and is never mounted or retained.
        self.private = self.private_parent / "work"
        self.private.mkdir(mode=0o755)
        self.run_id = "story-3-21-" + secrets.token_hex(8)
        self.redactions = []
        self.packet = {"schemaVersion": 1, "productionEligible": False, "selectedBroker": None, "brokers": {}}
        self.identities = {"stage": "local-docker-first-stage", "targetProof": False, "preflightPassed": False}
        self.before = {}
        self.context = None
        self.retained_inputs = self.output / "inputs"
        all_inputs = set(self.profile["inputs"]) | {"deploy/dapr/qualification/runtime-lock.json"}
        all_inputs.update(x["manifest"]["path"] for x in self.profile["images"].values())
        for name in all_inputs:
            destination = self.retained_inputs / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, destination)

    def redact(self, text):
        for value in sorted(self.redactions, key=len, reverse=True):
            if value:
                text = text.replace(value, "<redacted-disposable-credential>")
        text = re.sub(r"-----BEGIN [^-]*PRIVATE KEY-----.*?-----END [^-]*PRIVATE KEY-----", "<redacted-key>", text, flags=re.S)
        return text

    def command(self, argv, *, check=True, timeout=60, env=None):
        started = utc()
        try:
            result = subprocess.run(list(map(str, argv)), stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=timeout, env=env)
            code, output = result.returncode, result.stdout
        except (subprocess.TimeoutExpired, OSError) as error:
            code, output = 124, str(error)
        self.receipts.append({"argv": [self.redact(str(x)) for x in argv], "startedUtc": started, "endedUtc": utc(), "exitCode": code, "output": self.redact(output)})
        if check and code != 0:
            raise RuntimeError(f"command {argv[0]} exited {code}: {self.redact(output)[-800:]}")
        return code, output

    def request(self, url, data=None, *, token=None, timeout=3, context=None, method=None):
        headers = {"Content-Type": "application/cloudevents+json" if "/publish/" in url else "application/json"}
        if token:
            headers["X-Vault-Token"] = token
        req = urllib.request.Request(url, data=None if data is None else json.dumps(data).encode(), headers=headers, method=method)
        started = utc()
        try:
            with urllib.request.urlopen(req, timeout=timeout, context=context) as response:
                status, body = response.status, response.read().decode()
        except urllib.error.HTTPError as error:
            status, body = error.code, error.read().decode()
        except (OSError, TimeoutError) as error:
            status, body = 599, self.redact(str(error))
        confidential = token or "/sys/init" in url or "/sys/unseal" in url
        receipt = {"argv": ["HTTP", method or ("POST" if data is not None else "GET"), url], "startedUtc": started, "endedUtc": utc(), "exitCode": 0 if status < 400 else 1, "httpStatus": status, "output": self.redact(body) if not confidential else "<secret-administration-response-omitted>"}
        if "/publish/" in url:
            receipt["input"] = data
        self.receipts.append(receipt)
        try:
            payload = json.loads(body)
        except json.JSONDecodeError:
            payload = {"body": body}
        return status, payload

    def wait(self, predicate, timeout=45):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            value = predicate()
            if value:
                return value
            time.sleep(0.2)
        raise TimeoutError("bounded readiness/readback condition not met")

    def docker(self, *argv, **kwargs):
        return self.command(["docker", *argv], **kwargs)

    def container(self, suffix, image, arguments=(), options=(), env=None):
        name = self.run_id + "-" + suffix
        self.containers.append(name)
        self.docker("run", "-d", "--name", name, "--label", "hexalith.qualification=" + self.run_id,
                    *options, image, *arguments, env=env, timeout=180)
        return name

    def process(self, argv, env=None):
        started = utc()
        log = self.private / ("process-" + secrets.token_hex(4) + ".log")
        with log.open("w") as stream:
            process = subprocess.Popen(list(map(str, argv)), stdout=stream, stderr=subprocess.STDOUT, env=env)
        self.processes.append((process, argv, started, log))
        return process

    def stop(self, process):
        if process and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)

    def inventory(self):
        _, output = self.docker("ps", "-aq", "--no-trunc")
        ids = output.split()
        if not ids:
            return {}
        # Retain only specification hashes for unrelated workloads, never their
        # environment values or provider/bootstrap credentials.
        _, output = self.docker("inspect", *ids)
        result = {x["Id"]: hashlib.sha256(json.dumps({"Name": x["Name"], "Config": x["Config"], "HostConfig": x["HostConfig"], "Mounts": x["Mounts"]}, sort_keys=True).encode()).hexdigest() for x in json.loads(output)}
        self.receipts[-1]["output"] = json.dumps(result, sort_keys=True)
        return result

    def extract_control(self, name):
        container = self.run_id + "-" + name
        self.containers.append(container)
        image = self.profile["images"][name]["reference"]
        self.docker("create", "--name", container, "--label", "hexalith.qualification=" + self.run_id, image, "./" + name)
        binary = self.private / name
        self.docker("cp", container + ":/" + name, str(binary))
        binary.chmod(0o755)
        self.identities.setdefault("controlPlane", {})[name] = {"image": image, "binarySha256": digest(binary)}
        return binary

    def published_port(self, container, internal_port):
        _, output = self.docker("inspect", "--format", '{{(index (index .NetworkSettings.Ports "' + str(internal_port) + '/tcp") 0).HostPort}}', container)
        return int(output.strip())

    def broker_ready(self):
        if self.broker == "rabbitmq":
            self.wait(lambda: self.docker("exec", "--user", "rabbitmq", self.broker_container,
                                        "rabbitmq-diagnostics", "-q", "check_running", check=False)[0] == 0,
                      timeout=90)
        else:
            self.wait(lambda: self.kafka("kafka-topics.sh", "--list", check=False)[0] == 0, timeout=90)

    def refresh_forwarder(self):
        target = self.private / (self.broker + "-forward-target")
        temporary = target.with_suffix(".new")
        temporary.write_text(str(self.published_port(self.broker_container, self.broker_internal_port)))
        temporary.replace(target)
        return target

    def restart_broker(self):
        self.docker("restart", self.broker_container, timeout=90)
        receipt = len(self.receipts) - 1
        self.refresh_forwarder()
        self.broker_ready()
        return receipt

    def recover(self):
        """Restore owned hosts after a failed vector without hiding its outcome."""
        self.docker("unpause", self.broker_container, check=False)
        self.refresh_forwarder()
        self.broker_ready()
        self.stop(self.consumer)
        self.stop(self.publisher)
        if self.app.poll() is not None:
            self.start_app()
        self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
        self.publisher, self.publisher_url = self.sidecar("qualification-publisher", self.app_port)

    def prepare(self):
        validate_profile(self.profile, ROOT)
        # A remote Docker daemon can operate production; context names alone are insufficient.
        endpoint = os.environ.get("DOCKER_HOST", "")
        _, raw_context = self.docker("context", "inspect")
        context = json.loads(raw_context)[0]
        endpoint = endpoint or context["Endpoints"]["docker"]["Host"]
        if not endpoint.startswith("unix://") or "192.168.1.30" in endpoint:
            raise ValueError("Docker endpoint must be a local Unix socket; remote/production endpoints are forbidden")
        self.context = context["Name"]
        self.before = self.inventory()
        write_json(self.output / "workspace-before.json", self.before)
        daprd = Path(os.environ.get("QUALIFICATION_DAPRD", str(Path.home() / ".dapr/bin/daprd"))).resolve()
        cli = Path(shutil.which("dapr") or "/missing-dapr")
        if digest(daprd) != self.profile["runtime"]["binarySha256"] or digest(cli) != self.profile["runtime"]["cliSha256"]:
            raise ValueError("daprd/CLI binary hash differs from runtime-lock.json")
        self.daprd = daprd
        self.command([str(daprd), "--version"])
        self.command([str(cli), "--version"])
        self.identities.update({"preflightPassed": True, "daprdSha256": digest(daprd), "cliSha256": digest(cli), "dockerEndpoint": endpoint})
        for name in ["placement", "scheduler", *BROKERS]:
            self.docker("pull", self.profile["images"][name]["reference"], timeout=180)
        self.docker("pull", self.profile["openbao"]["reference"], timeout=180)
        self.certificates()
        self.openbao()
        placement_port, scheduler_port = port(), port()
        self.placement = f"127.0.0.1:{placement_port}"
        self.scheduler = f"127.0.0.1:{scheduler_port}"
        placement_health, scheduler_health = port(), port()
        scheduler_data = self.private / "scheduler-data"
        scheduler_data.mkdir(mode=0o777)
        scheduler_data.chmod(0o777)
        self.process([self.extract_control("placement"), "--port", str(placement_port), "--healthz-port", str(placement_health), "--metrics-port", str(port())])
        self.process([self.extract_control("scheduler"), "--port", str(scheduler_port), "--etcd-data-dir", str(scheduler_data), "--etcd-client-port", str(port()), "--etcd-initial-cluster", "dapr-scheduler-server-0=http://127.0.0.1:" + str(port()), "--healthz-port", str(scheduler_health), "--metrics-port", str(port())])
        self.wait(lambda: self.request(f"http://127.0.0.1:{placement_health}/healthz")[0] in (200, 204))
        self.wait(lambda: self.request(f"http://127.0.0.1:{scheduler_health}/healthz")[0] in (200, 204))

    def certificates(self):
        self.command(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-keyout", self.private / "ca.key", "-out", self.private / "ca.crt", "-days", "2", "-subj", "/CN=story-3-21-disposable-ca", "-addext", "basicConstraints=critical,CA:TRUE", "-addext", "keyUsage=critical,keyCertSign,cRLSign"])
        self.private.joinpath("extensions.cnf").write_text("basicConstraints=critical,CA:FALSE\nkeyUsage=critical,digitalSignature,keyEncipherment\nsubjectAltName=DNS:localhost,IP:127.0.0.1\nextendedKeyUsage=serverAuth,clientAuth\n")
        for name in ("server", "client", "rotated", "admin"):
            self.command(["openssl", "req", "-newkey", "rsa:2048", "-nodes", "-keyout", self.private / f"{name}.key", "-out", self.private / f"{name}.csr", "-subj", "/CN=" + ("localhost" if name == "server" else name)])
            self.command(["openssl", "x509", "-req", "-in", self.private / f"{name}.csr", "-CA", self.private / "ca.crt", "-CAkey", self.private / "ca.key", "-CAcreateserial", "-out", self.private / f"{name}.crt", "-days", "2", "-extfile", self.private / "extensions.cnf"])
            self.redactions.append((self.private / f"{name}.key").read_text())
        self.ssl = ssl.create_default_context(cafile=str(self.private / "ca.crt"))
        for file in self.private.glob("*"):
            file.chmod(0o644)  # private parent remains 0700; container service users can read mounts.

    def bao(self, path, data=None, token=None, method=None):
        status, body = self.request(self.bao_url + "/v1/" + path, data, token=token or self.root_token, context=self.ssl, method=method)
        if status >= 400:
            raise RuntimeError(f"OpenBao administration {path}: HTTP {status}")
        return body

    def openbao(self):
        bao_port = port()
        self.bao_url = f"https://127.0.0.1:{bao_port}"
        config = 'disable_mlock = true\nstorage "file" { path = "/data" }\nlistener "tcp" {\n address = "0.0.0.0:8200"\n tls_cert_file = "/work/server.crt"\n tls_key_file = "/work/server.key"\n}\n'
        (self.private / "bao.hcl").write_text(config)
        data = self.private / "bao-data"
        data.mkdir(mode=0o777)
        data.chmod(0o777)
        container = self.container("openbao", self.profile["openbao"]["reference"],
                       ["server", "-config=/work/bao.hcl"],
                       ["--cap-add=IPC_LOCK", "-p", "127.0.0.1::8200", "-v", f"{self.private}:/work:ro", "-v", f"{data}:/data"])
        self.bao_url = f"https://127.0.0.1:{self.published_port(container, 8200)}"
        self.wait(lambda: self.request(self.bao_url + "/v1/sys/health", context=self.ssl)[0] == 501)
        status, initialized = self.request(self.bao_url + "/v1/sys/init", {"secret_shares": 1, "secret_threshold": 1}, context=self.ssl, method="PUT")
        if status != 200:
            raise RuntimeError("disposable OpenBao initialization failed")
        self.root_token = initialized["root_token"]
        self.redactions.extend([self.root_token, *initialized["keys"], *initialized["keys_base64"]])
        self.bao("sys/unseal", {"key": initialized["keys_base64"][0]})
        self.bao("sys/mounts/secret", {"type": "kv", "options": {"version": "2"}})
        policy = 'path "secret/data/dapr/qualification-*" { capabilities = ["read"] }'
        self.bao("sys/policies/acl/qualification", {"policy": policy})
        response = self.bao("auth/token/create", {"policies": ["qualification"], "no_default_policy": True, "ttl": "1h"})
        self.token = response["auth"]["client_token"]
        self.redactions.append(self.token)
        (self.private / "bao.token").write_text(self.token)
        (self.private / "bao.token").chmod(0o644)

    def start_broker(self, broker):
        self.broker = broker
        self.broker_port = port()
        self.broker_dir = self.private / broker
        self.broker_dir.mkdir()
        self.broker_dir.chmod(0o777)
        self.password = secrets.token_urlsafe(24)
        self.redactions.append(self.password)
        if broker == "rabbitmq":
            self.broker_internal_port = 5671
            config = "listeners.tcp = none\nlisteners.ssl.default = 5671\nssl_options.cacertfile = /work/ca.crt\nssl_options.certfile = /work/server.crt\nssl_options.keyfile = /work/server.key\nssl_options.verify = verify_peer\nssl_options.fail_if_no_peer_cert = true\n"
            (self.private / "rabbitmq.conf").write_text(config)
            env = {**os.environ, "RABBITMQ_DEFAULT_USER": "qualification", "RABBITMQ_DEFAULT_PASS": self.password, "ERL_EPMD_PORT": str(port()), "RABBITMQ_DIST_PORT": str(port())}
            self.broker_container = self.container(broker, self.profile["images"][broker]["reference"], options=["-p", "127.0.0.1::5671", "-v", f"{self.private}:/work:ro", "-v", f"{self.private}/rabbitmq.conf:/etc/rabbitmq/rabbitmq.conf:ro", "-v", f"{self.broker_dir}:/var/lib/rabbitmq", "-e", "RABBITMQ_DEFAULT_USER", "-e", "RABBITMQ_DEFAULT_PASS", "-e", "ERL_EPMD_PORT", "-e", "RABBITMQ_DIST_PORT"], env=env)
            self.broker_ready()
            # Explicit exact-name grants; no unrestricted default application user.
            exact_resources = self.profile["topics"] + ["qualification-consumer-" + topic for topic in self.profile["topics"]]
            grant = "^(" + "|".join(re.escape(name) for name in exact_resources) + ")$"
            self.docker("exec", self.broker_container, "rabbitmqctl", "set_permissions", "qualification", grant, grant, grant)
            self.docker("exec", self.broker_container, "rabbitmqctl", "set_policy", "qualification-bounded", "^qualification[.].*backpressure.*", '{"overflow":"reject-publish","max-length":4}', "--apply-to", "queues")
            self.broker_secret = {"connectionString": f"amqps://qualification:{self.password}@127.0.0.1:{self.broker_port}/%2F"}
        else:
            self.broker_internal_port = self.broker_port
            self.kafka_config()
            command = "/opt/kafka/bin/kafka-storage.sh format --ignore-formatted --cluster-id MkU3OEVBNTcwNTJENDM2Qk --config /work/kafka.properties && exec /opt/kafka/bin/kafka-server-start.sh /work/kafka.properties"
            self.broker_container = self.container(broker, self.profile["images"][broker]["reference"], ["/bin/bash", "-ec", command], ["-p", f"127.0.0.1::{self.broker_internal_port}", "-v", f"{self.private}:/work:ro", "-v", f"{self.broker_dir}:/var/lib/kafka/data"])
            self.broker_ready()
            for topic in self.profile["topics"]:
                if topic != "qualification.missing-binding":
                    self.kafka("kafka-topics.sh", "--create", "--topic", topic, "--partitions", "1", "--replication-factor", "1", "--config", "retention.ms=60000", "--config", "segment.ms=1000")
                    self.kafka("kafka-acls.sh", "--add", "--allow-principal", "User:CN=client", "--operation", "Read", "--operation", "Write", "--operation", "Describe", "--topic", topic)
            self.kafka("kafka-acls.sh", "--add", "--allow-principal", "User:CN=client", "--operation", "Read", "--group", "qualification-consumer")
            self.broker_secret = {"brokers": f"127.0.0.1:{self.broker_port}"}
        target = self.refresh_forwarder()
        proxy = self.process([sys.executable, self.retained_inputs / "tools/dapr_qualification_tcp_proxy.py",
                              "--port", str(self.broker_port), "--target-file", target])
        self.wait(lambda: proxy.poll() is None and self.tcp_ready(self.broker_port))
        self.broker_secret.update({"caCert": (self.private / "ca.crt").read_text(), "clientCert": (self.private / "client.crt").read_text(), "clientKey": (self.private / "client.key").read_text()})
        self.bao("secret/data/dapr/qualification-" + broker, {"data": self.broker_secret})
        self.broker_identity = json.loads(self.docker("inspect", self.broker_container)[1])[0]
        write_json(self.output / broker / "image-identity.json", {"containerId": self.broker_identity["Id"], "imageId": self.broker_identity["Image"], "requestedImage": self.broker_identity["Config"]["Image"], "kind": "disposable-broker-container"})

    @staticmethod
    def tcp_ready(number):
        try:
            with socket.create_connection(("127.0.0.1", number), timeout=1):
                return True
        except OSError:
            return False

    def kafka_config(self):
        def pem(name):
            return (self.private / name).read_text().replace("\n", "\\n")
        controller = port()
        config = {"process.roles": "broker,controller", "node.id": "1", "controller.quorum.voters": f"1@127.0.0.1:{controller}", "controller.listener.names": "CONTROLLER", "listeners": f"SSL://0.0.0.0:{self.broker_port},CONTROLLER://127.0.0.1:{controller}", "advertised.listeners": f"SSL://127.0.0.1:{self.broker_port}", "listener.security.protocol.map": "SSL:SSL,CONTROLLER:SSL", "inter.broker.listener.name": "SSL", "log.dirs": "/var/lib/kafka/data", "offsets.topic.replication.factor": "1", "transaction.state.log.replication.factor": "1", "transaction.state.log.min.isr": "1", "group.initial.rebalance.delay.ms": "0", "auto.create.topics.enable": "false", "ssl.keystore.type": "PEM", "ssl.keystore.key": pem("server.key"), "ssl.keystore.certificate.chain": pem("server.crt"), "ssl.truststore.type": "PEM", "ssl.truststore.certificates": pem("ca.crt"), "ssl.client.auth": "required", "authorizer.class.name": "org.apache.kafka.metadata.authorizer.StandardAuthorizer", "super.users": "User:CN=localhost;User:CN=admin", "allow.everyone.if.no.acl.found": "false", "log.retention.check.interval.ms": "1000"}
        (self.private / "kafka.properties").write_text("\n".join(f"{k}={v}" for k, v in config.items()) + "\n")
        client = {"security.protocol": "SSL", "ssl.keystore.type": "PEM", "ssl.keystore.key": pem("admin.key"), "ssl.keystore.certificate.chain": pem("admin.crt"), "ssl.truststore.type": "PEM", "ssl.truststore.certificates": pem("ca.crt")}
        (self.private / "admin.properties").write_text("\n".join(f"{k}={v}" for k, v in client.items()) + "\n")

    def kafka(self, script, *arguments, check=True):
        return self.docker("exec", self.broker_container, "/opt/kafka/bin/" + script, "--bootstrap-server", f"127.0.0.1:{self.broker_port}", "--command-config", "/work/admin.properties", *arguments, check=check)

    def components(self, role, mutation=None):
        path = self.private / (self.broker + "-" + role + "-" + secrets.token_hex(4))
        path.mkdir()
        source = (self.retained_inputs / "deploy/dapr/qualification" / (self.broker + ".yaml")).read_text()
        if mutation == "missing-secret":
            source = source.replace("name: qualification-" + self.broker + ",", "name: qualification-absent,")
        if self.broker == "kafka" and getattr(self, "kafka_group", None):
            source = source.replace("value: qualification-consumer}", "value: " + self.kafka_group + "}")
        (path / "pubsub.yaml").write_text(source)
        openbao = {"apiVersion": "dapr.io/v1alpha1", "kind": "Component", "metadata": {"name": "openbao"}, "spec": {"type": "secretstores.hashicorp.vault", "version": "v1", "metadata": [{"name": "vaultAddr", "value": self.bao_url}, {"name": "vaultTokenMountPath", "value": str(self.private / "bao.token")}, {"name": "caCert", "value": str(self.private / "ca.crt")}, {"name": "skipVerify", "value": "false"}, {"name": "vaultKVPrefix", "value": "dapr"}, {"name": "vaultValueType", "value": "map"}]}, "scopes": ["qualification-publisher", "qualification-consumer"]}
        if mutation == "tls-negative":
            openbao["spec"]["metadata"] = [{**x, "value": "wrong-host.invalid"} if x["name"] == "tlsServerName" else x for x in openbao["spec"]["metadata"]]
            openbao["spec"]["metadata"].append({"name": "tlsServerName", "value": "wrong-host.invalid"})
        write_json(path / "openbao.yaml", openbao)
        for index, subscription in enumerate((self.retained_inputs / "deploy/dapr/qualification/subscriptions.yaml").read_text().split("\n---\n")):
            (path / f"subscription-{index}.yaml").write_text(subscription)
        config = {"apiVersion": "dapr.io/v1alpha1", "kind": "Configuration", "metadata": {"name": "qualification"}, "spec": {"secrets": {"scopes": [{"storeName": "openbao", "defaultAccess": "deny", "allowedSecrets": ["qualification-" + self.broker]}]}}}
        write_json(path / "config.yaml", config)
        if role == "qualification-consumer":
            write_json(path / "state.yaml", {"apiVersion": "dapr.io/v1alpha1", "kind": "Component", "metadata": {"name": "qualification-state"}, "spec": {"type": "state.sqlite", "version": "v1", "metadata": [{"name": "connectionString", "value": str(self.private / (self.broker + "-effects.sqlite"))}]}, "scopes": ["qualification-consumer"]})
        resiliency = {
            "apiVersion": "dapr.io/v1alpha1", "kind": "Resiliency", "metadata": {"name": "qualification"},
            "spec": {
                "policies": {"retries": {"bounded": {"policy": "constant", "duration": "200ms", "maxRetries": 2}}, "timeouts": {"bounded": "3s"}},
                "targets": {"components": {"pubsub": {"inbound": {"retry": "bounded", "timeout": "bounded"}, "outbound": {"retry": "bounded", "timeout": "bounded"}}}},
            },
        }
        write_json(path / "resiliency.yaml", resiliency)
        retained = self.output / self.broker / "loaded-inputs" / path.name
        shutil.copytree(path, retained)
        return path

    def sidecar(self, role, app_port, mutation=None, ready=True):
        http_port, grpc_port, internal_port = port(), port(), port()
        path = self.components(role, mutation)
        process = self.process([self.daprd, "--app-id", role, "--app-port", str(app_port), "--app-channel-address", "127.0.0.1", "--dapr-http-port", str(http_port), "--dapr-grpc-port", str(grpc_port), "--dapr-internal-grpc-port", str(internal_port), "--metrics-port", str(port()), "--resources-path", path, "--config", path / "config.yaml", "--placement-host-address", self.placement, "--scheduler-host-address", self.scheduler, "--dapr-listen-addresses", "127.0.0.1"], env={**os.environ, "APP_API_TOKEN": self.app_token})
        endpoint = f"http://127.0.0.1:{http_port}"
        if role == "qualification-consumer":
            self.store.joinpath("state-endpoint").write_text(endpoint)
        if ready:
            def healthy():
                if process.poll() is not None:
                    raise RuntimeError("sidecar exited during readiness; inspect retained process log")
                return self.request(endpoint + "/v1.0/healthz")[0] == 204
            self.wait(healthy)
            status, metadata = self.request(endpoint + "/v1.0/metadata")
            if metadata.get("runtimeVersion") != self.profile["runtime"]["version"]:
                raise RuntimeError("loaded runtimeVersion mismatch")
            components = metadata.get("components", [])
            if role != "qualification-forbidden" and not any(x.get("name") == "pubsub" and x.get("type") == "pubsub." + self.broker and x.get("version") == "v1" for x in components):
                raise RuntimeError("loaded pubsub component identity mismatch")
            write_json(self.output / self.broker / "metadata" / (role + "-" + secrets.token_hex(3) + ".json"), metadata)
        return process, endpoint

    def start_app(self):
        self.store.mkdir(exist_ok=True)
        self.app = self.process([sys.executable, self.retained_inputs / "tools/dapr_broker_probe.py", "--port", str(self.app_port), "--store", self.store], env={**os.environ, "APP_API_TOKEN": self.app_token})
        self.wait(lambda: self.request(self.app_url + "/health")[0] == 200)

    def readback(self):
        status, value = self.request(self.app_url + "/readback")
        if status != 200:
            raise RuntimeError("persisted application HTTP readback unavailable")
        value["commandReceipt"] = len(self.receipts) - 1
        return value

    def count(self, event_id, kind="effect"):
        return sum(x["id"] == event_id and x["kind"] == kind for x in self.readback()["ledger"])

    def publish(self, topic="events", event_id=None, data=None, endpoint=None, ttl=None, timeout=5):
        event_id = event_id or secrets.token_hex(12)
        event = {"specversion": "1.0", "id": event_id, "source": "story-3-21", "type": "qualification", "datacontenttype": "application/json", "data": {"messageId": event_id, **(data or {})}}
        suffix = "?metadata.rawPayload=false" + ("&metadata.ttlInSeconds=" + str(ttl) if ttl else "")
        status, _ = self.request((endpoint or self.publisher_url) + "/v1.0/publish/pubsub/qualification." + topic + suffix, event, timeout=timeout)
        return event_id, status

    def diagnostics(self):
        if self.broker == "rabbitmq":
            code, output = self.docker("exec", self.broker_container, "rabbitmqctl", "list_queues", "name", "durable", "type", "messages_ready", "messages_unacknowledged", "--formatter=json", check=False)
        else:
            code, output = self.kafka("kafka-consumer-groups.sh", "--describe", "--all-groups", check=False)
        return {"kind": "broker-administration-diagnostic", "exitCode": code, "output": self.redact(output), "commandReceipt": len(self.receipts) - 1}

    def record(self, broker, scenario, observation, readback=None, diagnostics=None):
        observation.update({"broker": broker, "scenario": scenario})
        path = self.output / broker / scenario
        write_json(path / "observation.json", observation)
        write_json(path / "application-readback.json", readback or {"blockedBy": observation.get("blockedBy", "application evidence unavailable")})
        write_json(path / "broker-diagnostics.json", diagnostics or {"blockedBy": observation.get("blockedBy", "broker diagnostics unavailable")})
        self.packet["brokers"].setdefault(broker, {})[scenario] = {"result": "PASS" if evaluate(scenario, observation) else "FAIL", "observation": str((path / "observation.json").relative_to(self.output)), "applicationReadback": str((path / "application-readback.json").relative_to(self.output)), "brokerDiagnostics": str((path / "broker-diagnostics.json").relative_to(self.output))}
        print(f"{broker}/{scenario}: {self.packet['brokers'][broker][scenario]['result']}" + (" (BLOCKED: " + observation["blockedBy"] + ")" if observation.get("blockedBy") else ""), flush=True)

    def run_broker(self, broker):
        self.start_broker(broker)
        self.app_token = secrets.token_urlsafe(24)
        self.redactions.append(self.app_token)
        self.app_port = port()
        self.app_url = f"http://127.0.0.1:{self.app_port}"
        self.store = self.private / (broker + "-application-ledger")
        self.start_app()
        self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
        self.publisher, self.publisher_url = self.sidecar("qualification-publisher", self.app_port)
        for scenario in SCENARIOS:
            try:
                observation = self.scenario(scenario)
                self.record(broker, scenario, observation, self.readback(), self.diagnostics())
            except (RuntimeError, TimeoutError, OSError, ValueError, KeyError) as error:
                readback, diagnostic = None, None
                try:
                    readback = self.readback()
                    diagnostic = self.diagnostics()
                except (RuntimeError, OSError):
                    pass
                self.record(broker, scenario, {"blockedBy": self.redact(str(error)), "failureClass": "observed-scenario-failure"}, readback, diagnostic)
                self.recover()
        self.stop(self.consumer)
        self.stop(self.publisher)
        self.stop(self.app)

    def scenario(self, scenario):
        if scenario == "crash-redelivery":
            event_id, _ = self.publish(data={"crashOnce": True})
            self.wait(lambda: self.app.poll() is not None)
            code = self.app.returncode
            self.start_app()
            self.stop(self.consumer)
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
            self.wait(lambda: self.count(event_id) == 1)
            return {"messageId": event_id, "receiptCount": self.count(event_id, "receipt"), "effectCount": self.count(event_id), "crashExitCode": code, "sameId": True}
        if scenario == "broker-restart":
            self.stop(self.consumer)
            event_id, status = self.publish()
            before = json.loads(self.docker("inspect", self.broker_container)[1])[0]["Id"]
            restart = self.restart_broker()
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
            self.wait(lambda: self.count(event_id) == 1)
            after = json.loads(self.docker("inspect", self.broker_container)[1])[0]["Id"]
            return {"messageId": event_id, "beforeContainerId": before, "afterContainerId": after, "effectCount": self.count(event_id), "publishedWhileOffline": status < 300, "restartReceipt": restart}
        if scenario == "durable-quarantine":
            blocked = self.store / "quarantine-blocked"
            blocked.touch()
            event_id, _ = self.publish("poison")
            self.wait(lambda: any(row["id"] == event_id and row["route"] == "/quarantine" for row in self.readback()["ledger"]))
            self.stop(self.consumer)
            restart = self.restart_broker()
            blocked.unlink()
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
            survived = False
            try:
                self.wait(lambda: self.count(event_id) == 1, timeout=10)
                survived = True
            except TimeoutError:
                pass
            return {"messageId": event_id, "poisonReceipts": sum(x["kind"] == "receipt" and x["id"] == event_id and x["route"] == "/poison" for x in self.readback()["ledger"]), "quarantineEffects": self.count(event_id), "survivedRestart": survived, "captureBlockedBeforeBrokerRestart": True, "restartReceipt": restart}
        if scenario == "failed-diversion":
            event_id, _ = self.publish("failed-diversion")
            self.wait(lambda: self.count(event_id, "receipt") >= 3)
            before = self.count(event_id, "receipt")
            self.stop(self.consumer)
            restart = self.restart_broker()
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
            recovered = False
            try:
                self.wait(lambda: self.count(event_id, "receipt") > before, timeout=10)
                recovered = True
            except TimeoutError:
                pass
            logs = "\n".join(row[3].read_text() for row in self.processes if "--app-id" in row[1])
            denied = bool(re.search(r"(not allowed|denied|forbidden).*qualification[.]quarantine-denied|qualification[.]quarantine-denied.*(not allowed|denied|forbidden)", logs, re.I))
            return {"messageId": event_id, "denialObserved": denied, "quarantineEffects": self.count(event_id), "sourceRecoveredAfterRestart": recovered, "beforeRestartReceiptCount": before, "restartReceipt": restart}
        if scenario == "retention-expiry":
            if self.broker == "kafka":
                return self.kafka_retention()
            self.stop(self.consumer)
            expired, _ = self.publish("expiry", ttl=1)
            control, _ = self.publish("events")
            started = time.monotonic()
            time.sleep(3)
            diagnostic = self.diagnostics()
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
            self.wait(lambda: self.count(control) == 1)
            return {"messageId": control, "expiredMessageId": expired, "controlEffects": self.count(control), "expiredEffects": self.count(expired), "outageSeconds": time.monotonic() - started, "brokerRetentionObserved": diagnostic["exitCode"] == 0 and self.broker == "rabbitmq", "limitation": "Kafka native log expiry/replay after acknowledgement remains unproven; this vector observes Dapr TTL only." if self.broker == "kafka" else "Bounded quorum-queue backlog/expiry observation only."}
        if scenario == "stable-id-deduplication":
            event_id, _ = self.publish()
            self.publish(event_id=event_id)
            self.wait(lambda: self.count(event_id, "receipt") >= 2)
            return {"messageId": event_id, "receiptCount": self.count(event_id, "receipt"), "effectCount": self.count(event_id), "sameId": True}
        if scenario == "app-topic-scopes":
            event_id, _ = self.publish()
            self.wait(lambda: self.count(event_id) == 1)
            forbidden, endpoint = self.sidecar("qualification-forbidden", self.app_port)
            try:
                forbidden_app_id, forbidden_app = self.publish(endpoint=endpoint)
                forbidden_topic_id, forbidden_topic = self.publish("not-enrolled")
                metadata = self.request(endpoint + "/v1.0/metadata")[1]
                return {"messageId": event_id, "allowedEffects": self.count(event_id), "forbiddenAppStatus": forbidden_app, "forbiddenTopicStatus": forbidden_topic, "forbiddenSubscriptions": len(metadata.get("subscriptions", [])), "forbiddenAppMessageId": forbidden_app_id, "forbiddenTopicMessageId": forbidden_topic_id}
            finally:
                self.stop(forbidden)
        if scenario in ("tls-negative", "missing-secret"):
            event_id, _ = self.publish()
            self.wait(lambda: self.count(event_id) == 1)
            negative, endpoint = self.sidecar("qualification-publisher", self.app_port, mutation=scenario, ready=False)
            try:
                exited = True
                try:
                    negative.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    exited = False
                negative_ready = self.request(endpoint + "/v1.0/healthz")[0] == 204
                code = negative.returncode
                log = next(row[3] for row in self.processes if row[0] is negative).read_text()
                resource_dir = str(negative.args[negative.args.index("--resources-path") + 1])
                return {"messageId": event_id, "allowedEffects": self.count(event_id), "negativeReady": negative_ready, "negativeExitCode": code, "negativeExitedNaturally": exited, "negativeResourceDirectory": resource_dir, "diagnosticMatched": bool(re.search("certificate|failed to get secret|permission denied|not allowed", log, re.I))}
            finally:
                self.stop(negative)
        if scenario == "secret-rotation":
            # Publish new generation, restart both startup-loaded consumers,
            # then revoke the old broker credential and verify denial separately.
            old_publisher, old_endpoint = self.publisher, self.publisher_url
            if self.broker == "rabbitmq":
                self.password = secrets.token_urlsafe(24)
                self.redactions.append(self.password)
                self.docker("exec", self.broker_container, "rabbitmqctl", "add_user", "qualification-v2", self.password)
                exact_resources = self.profile["topics"] + ["qualification-consumer-" + topic for topic in self.profile["topics"]]
                grant = "^(" + "|".join(re.escape(name) for name in exact_resources) + ")$"
                self.docker("exec", self.broker_container, "rabbitmqctl", "set_permissions", "qualification-v2", grant, grant, grant)
                self.broker_secret["connectionString"] = f"amqps://qualification-v2:{self.password}@127.0.0.1:{self.broker_port}/%2F"
            else:
                self.kafka("kafka-acls.sh", "--add", "--allow-principal", "User:CN=rotated", "--operation", "Read", "--operation", "Write", "--operation", "Describe", "--topic", "qualification.", "--resource-pattern-type", "prefixed")
                self.kafka("kafka-acls.sh", "--add", "--allow-principal", "User:CN=rotated", "--operation", "Read", "--group", "qualification-consumer")
                self.broker_secret.update({"clientCert": (self.private / "rotated.crt").read_text(), "clientKey": (self.private / "rotated.key").read_text()})
            self.bao("secret/data/dapr/qualification-" + self.broker, {"data": self.broker_secret})
            self.stop(self.consumer)
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
            self.publisher, self.publisher_url = self.sidecar("qualification-publisher", self.app_port)
            event_id, _ = self.publish()
            self.wait(lambda: self.count(event_id) == 1)
            if self.broker == "rabbitmq":
                self.docker("exec", self.broker_container, "rabbitmqctl", "delete_user", "qualification")
            else:
                self.kafka("kafka-acls.sh", "--add", "--deny-principal", "User:CN=client", "--operation", "All", "--topic", "qualification.", "--resource-pattern-type", "prefixed")
            stale_id, stale_status = self.publish(endpoint=old_endpoint)
            denied = 400 <= stale_status < 599 and self.count(stale_id) == 0
            self.stop(old_publisher)
            return {"messageId": event_id, "oldGenerationDenied": denied, "newGenerationEffects": self.count(event_id), "controlledRestart": True, "staleMessageId": stale_id, "stalePublishStatus": stale_status}
        if scenario == "cancellation-backpressure":
            self.stop(self.consumer)
            quota_receipt = None
            if self.broker == "kafka":
                self.kafka("kafka-configs.sh", "--alter", "--entity-type", "clients", "--entity-name", "qualification", "--add-config", "producer_byte_rate=1024")
                quota_receipt = len(self.receipts) - 1
            try:
                results = [self.publish("backpressure", data={"padding": "x" * 8192} if self.broker == "kafka" else None,
                                        timeout=12 if self.broker == "kafka" else 5) for _ in range(8)]
            finally:
                if self.broker == "kafka":
                    self.kafka("kafka-configs.sh", "--alter", "--entity-type", "clients", "--entity-name", "qualification", "--delete-config", "producer_byte_rate")
            self.docker("pause", self.broker_container)
            started = time.monotonic()
            try:
                cancelled_id, status = self.publish(timeout=1)
                bounded = time.monotonic() - started
            finally:
                self.docker("unpause", self.broker_container)
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
            deadline = time.monotonic() + 10
            while time.monotonic() < deadline and any(code < 300 and self.count(event_id) == 0 for event_id, code in results):
                time.sleep(0.2)
            loss = sum(code < 300 and self.count(event_id) == 0 for event_id, code in results)
            return {"cancelled": status == 599, "cancelledMessageId": cancelled_id, "cancelledEffects": self.count(cancelled_id), "boundedSeconds": bounded, "negativeConfirmRejected": any(400 <= code < 599 for _, code in results), "acknowledgedLoss": loss, "publishResults": [{"id": event_id, "status": code} for event_id, code in results], "quotaReceipt": quota_receipt}
        if scenario == "missing-bindings":
            event_id, _ = self.publish()
            self.wait(lambda: self.count(event_id) == 1)
            missing_id, status = self.publish("missing-binding")
            return {"messageId": event_id, "unboundPublishStatus": status, "allowedEffects": self.count(event_id), "bindingAbsent": True, "unboundMessageId": missing_id}
        raise ValueError(scenario)

    def kafka_retention(self):
        """Observe native retained-log expiry and acknowledged replay through Dapr."""
        self.kafka("kafka-configs.sh", "--alter", "--entity-type", "topics", "--entity-name", "qualification.expiry",
                   "--add-config", "retention.ms=3000,segment.ms=1000,segment.bytes=1048576,file.delete.delay.ms=1000")
        self.stop(self.consumer)
        expired, _ = self.publish("expiry", ttl=1, data={"padding": "x" * 700000})
        # Multiple large records roll closed segments; Kafka does not delete
        # its current active segment solely because retention time elapsed.
        for _ in range(4):
            self.publish("expiry", ttl=1, data={"padding": "x" * 700000})
        control, _ = self.publish("events")
        started = time.monotonic()
        offset_receipts = []

        def earliest_advanced():
            code, output = self.kafka("kafka-get-offsets.sh", "--topic", "qualification.expiry", "--time", "earliest", check=False)
            offset_receipts.append(len(self.receipts) - 1)
            return code == 0 and any(line.startswith("qualification.expiry:0:") and int(line.rsplit(":", 1)[1]) > 0 for line in output.splitlines())

        expired_natively = False
        try:
            self.wait(earliest_advanced, timeout=25)
            expired_natively = True
        except TimeoutError:
            pass
        self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
        self.wait(lambda: self.count(control) == 1)
        receipts_before = self.count(control, "receipt")
        self.stop(self.consumer)
        self.kafka_group = "qualification-replay"
        self.kafka("kafka-acls.sh", "--add", "--allow-principal", "User:CN=client", "--operation", "Read", "--group", self.kafka_group)
        replay = False
        try:
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
            self.wait(lambda: self.count(control, "receipt") > receipts_before, timeout=15)
            replay = True
        except TimeoutError:
            pass
        finally:
            self.stop(self.consumer)
            self.kafka_group = None
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
        return {"messageId": control, "expiredMessageId": expired, "controlEffects": self.count(control),
                "expiredEffects": self.count(expired), "outageSeconds": time.monotonic() - started,
                "brokerRetentionObserved": expired_natively and replay, "nativeExpiryObserved": expired_natively,
                "acknowledgedReplayObserved": replay, "beforeReplayReceiptCount": receipts_before,
                "offsetReceipts": offset_receipts}

    def finish(self):
        for process, argv, started, log in self.processes:
            self.stop(process)
            self.receipts.append({"argv": [str(x) for x in argv], "startedUtc": started, "endedUtc": utc(), "exitCode": process.returncode, "output": self.redact(log.read_text())})
        if self.context is None:
            # Endpoint rejection must not invoke even an owned cleanup helper
            # on an unapproved daemon. No Docker mutation has occurred here.
            shutil.rmtree(self.private_parent)
            self.finalize_receipts("", {})
            return
        for name in reversed(self.containers):
            code, output = self.docker("inspect", name, check=False)
            if code == 0:
                identity = json.loads(output)[0]
                if identity["Config"].get("Labels", {}).get("hexalith.qualification") != self.run_id:
                    raise RuntimeError("refusing cleanup of unowned container " + name)
                self.docker("logs", "--tail", "150", name, check=False)
                self.docker("unpause", name, check=False)
                self.docker("rm", "-f", "-v", name, check=False)
        # Service-owned files can be 0700 under another UID. A one-shot helper
        # sees only this run's private mount; it receives no host socket/network.
        self.docker("run", "--rm", "--name", self.run_id + "-private-cleanup", "--label", "hexalith.qualification=" + self.run_id,
                    "--network", "none", "--user", "0", "-v", f"{self.private_parent}:/cleanup", "--entrypoint", "/bin/sh",
                    self.profile["openbao"]["reference"], "-ec", f"chown -R {os.getuid()}:{os.getgid()} /cleanup\nchmod -R u+rwX /cleanup", check=False)
        shutil.rmtree(self.private_parent)
        _, remaining = self.docker("ps", "-aq", "--filter", "label=hexalith.qualification=" + self.run_id)
        after = self.inventory()
        self.finalize_receipts(remaining, after)

    def finalize_receipts(self, remaining, after):
        write_json(self.output / "workspace-after.json", after)
        if not (self.output / "workspace-before.json").exists():
            write_json(self.output / "workspace-before.json", self.before)
        write_json(self.output / "cleanup.json", {"ownedResourcesRemaining": remaining.split(), "unrelatedContainersUnchanged": all(after.get(key) == value for key, value in self.before.items()), "ownershipLabel": self.run_id, "privateMaterialRemoved": True})
        write_json(self.output / "execution-identities.json", self.identities)
        write_json(self.output / "commands.json", self.receipts)
        self.packet["artifacts"] = {str(p.relative_to(self.output)): digest(p) for p in sorted(self.output.rglob("*")) if p.is_file()}
        write_json(self.output / "packet.json", self.packet)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--brokers", default="rabbitmq,kafka")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
    if args.brokers.split(",") != list(BROKERS):
        parser.error("both candidates rabbitmq,kafka are required; missing requested scenarios cannot pass")
    try:
        run = Run(args)
    except (ValueError, KeyError, TypeError, OSError) as error:
        print(f"FAIL: qualification input rejected: {error}")
        return 1
    blocker = None
    try:
        run.prepare()
        for broker in BROKERS:
            try:
                run.run_broker(broker)
            except (RuntimeError, TimeoutError, OSError, ValueError, KeyError) as error:
                blocker = str(error)
                for scenario in SCENARIOS:
                    if scenario not in run.packet["brokers"].get(broker, {}):
                        run.record(broker, scenario, {"blockedBy": run.redact(blocker), "failureClass": "infrastructure-blocker"})
    except (RuntimeError, TimeoutError, OSError, ValueError, KeyError, KeyboardInterrupt) as error:
        blocker = str(error)
    finally:
        for broker in BROKERS:
            for scenario in SCENARIOS:
                if scenario not in run.packet["brokers"].get(broker, {}):
                    run.record(broker, scenario, {"blockedBy": run.redact(blocker or "not executed"), "failureClass": "infrastructure-blocker"})
        run.finish()
    failures = [f"{b}/{s}" for b, outcomes in run.packet["brokers"].items() for s, row in outcomes.items() if row["result"] != "PASS"]
    print(json.dumps({"packet": str(run.output), "technicalQualification": "FAIL" if failures else "PASS", "failedScenarios": failures, "productionEligibility": "FAIL", "selectedBroker": None}, indent=2))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
