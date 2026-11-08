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

from dapr_broker_qualification import (BROKERS, SCENARIOS, PRESSURE_POLICY, PRESSURE_PATTERN, PRESSURE_DEFINITION,
    attributable_denial, pressure_rejection, kafka_group_available, digest, evaluate, missing_binding_absent, rabbitmq_pressure_effective,
    reconcile_publications, validate_profile, write_json)

from dapr_runtime_qualification import CustodyError, ExclusiveDocker, profile_path, source_input, child_docker, cleanup_child_resources, resource_journal, register_private_custody

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
        self.profile_bytes = args.profile.read_bytes()
        self.profile = json.loads(self.profile_bytes)
        # Validate every retained path before copying bytes or creating any
        # output/private directory. A rejected profile has no side effects.
        validate_profile(self.profile, ROOT)
        self.output.mkdir(parents=True, exist_ok=False)
        self.receipts = []
        self.containers = []
        self.processes = []
        self.run_id = "story-3-21-" + secrets.token_hex(8)
        self.private_parent = Path(tempfile.mkdtemp(prefix="story-3-21-private-"))
        self.private_parent.chmod(0o700)
        register_private_custody(self)
        # Container service users need traversal of the mount root. The host
        # parent remains 0700 and is never mounted or retained.
        self.private = self.private_parent / "work"
        self.private.mkdir(mode=0o755)
        self.redactions = []
        self.packet = {"schemaVersion": 2, "profile": profile_path(self.profile), "productionEligible": False, "selectedBroker": None, "brokers": {}}
        self.identities = {"stage": "local-docker-first-stage", "targetProof": False, "preflightPassed": False}
        self.before = {}
        self.context = None
        self.docker_owner = None
        self.cleanup_complete = True
        self.cleanup_errors = []
        self.retained_inputs = self.output / "inputs"
        all_inputs = set(self.profile["inputs"]) | {profile_path(self.profile)}
        all_inputs.update(x["manifest"]["path"] for x in self.profile["images"].values())
        if self.profile["runtime"]["version"] == "1.19.1":
            all_inputs.add(self.profile["chart"]["path"])
            for image in self.profile["images"].values():
                all_inputs.update(child["path"] for child in image.get("children", {}).values())
        for name in all_inputs:
            destination = self.retained_inputs / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            if name == profile_path(self.profile):
                destination.write_bytes(self.profile_bytes)
            else:
                source = source_input(self.profile, ROOT, name, self.profile["inputs"][name]) if name in self.profile["inputs"] else ROOT / name
                shutil.copyfile(source, destination)
        # Current capture code is distinct from the original lock's historical input subject.
        tools = [name.removeprefix('tools/') for name in self.profile['inputs'] if name.startswith('tools/') and name.endswith('.py')] if self.profile['runtime']['version'] == '1.19.1' else ("qualify-dapr-brokers.py", "dapr_broker_qualification.py", "dapr_runtime_qualification.py")
        for name in tools:
            destination = self.output / "execution-tools" / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / "tools" / name, destination)

    def redact(self, text):
        for value in sorted(self.redactions, key=len, reverse=True):
            if value:
                text = text.replace(value, "<redacted-disposable-credential>")
        text = re.sub(r"-----BEGIN [^-]*PRIVATE KEY-----.*?-----END [^-]*PRIVATE KEY-----", "<redacted-key>", text, flags=re.S)
        return text

    def command(self, argv, *, check=True, timeout=60, env=None, retain_transform=None):
        started = utc()
        interrupted = None
        try:
            result = subprocess.run(list(map(str, argv)), stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=timeout, env=env)
            code, output = result.returncode, result.stdout
        except (subprocess.TimeoutExpired, OSError) as error:
            code, output = 124, str(error)
        except KeyboardInterrupt as error:
            code, output, interrupted = 130, "", error
        retained = output
        if retain_transform:
            # Raw unrelated specifications never enter receipts or exception text,
            # including unsuccessful, malformed and interrupted inspections.
            retained = "inspection-sha256:" + hashlib.sha256(output.encode()).hexdigest()
            if code == 0:
                try:
                    retained = retain_transform(output)
                except KeyboardInterrupt as error:
                    code, interrupted = 130, error
                except Exception:
                    code = 65
        self.receipts.append({"argv": [self.redact(str(x)) for x in argv], "startedUtc": started, "endedUtc": utc(), "exitCode": code, "output": self.redact(retained)})
        if interrupted is not None:
            raise interrupted
        if check and code != 0:
            raise RuntimeError(f"command {argv[0]} exited {code}: {self.redact(retained)[-800:]}")
        return code, output

    def request(self, url, data=None, *, token=None, timeout=3, context=None, method=None):
        headers = {"Content-Type": "application/cloudevents+json" if "/publish/" in url else "application/json"}
        if token:
            headers["X-Vault-Token"] = token
        req = urllib.request.Request(url, data=None if data is None else json.dumps(data).encode(), headers=headers, method=method)
        started = utc()
        elapsed_start = time.monotonic()
        try:
            with urllib.request.urlopen(req, timeout=timeout, context=context) as response:
                status, body = response.status, response.read().decode()
        except urllib.error.HTTPError as error:
            status, body = error.code, error.read().decode()
        except (OSError, TimeoutError) as error:
            status, body = 599, self.redact(str(error))
        confidential = token or "/sys/init" in url or "/sys/unseal" in url
        receipt = {"argv": ["HTTP", method or ("POST" if data is not None else "GET"), url], "startedUtc": started, "endedUtc": utc(), "elapsedSeconds": time.monotonic() - elapsed_start, "exitCode": 0 if status < 400 else 1, "httpStatus": status, "output": self.redact(body) if not confidential else "<secret-administration-response-omitted>"}
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
        if self.docker_owner is None:
            raise ValueError("Docker custody is unavailable; resource operations refused")
        if list(argv[:1]) in (['create'], ['run']) or list(argv[:2]) in (['container', 'create'], ['container', 'run'], ['volume', 'create'], ['network', 'create']):
            journal, token = resource_journal(self, self.private / 'owner-resources.json')
            self.resource_journal = journal
            def invoke(arguments):
                code, output = self.docker_owner.run(*arguments, check=False, **{key: value for key, value in kwargs.items() if key != 'check'})
                return subprocess.CompletedProcess(arguments, code, output, '')
            result = child_docker(argv, invoke, journal, self.run_id, internal=True, custody=self.docker_owner.custody(), supervisor_token=token)
            if kwargs.get('check', True) and result.returncode: raise RuntimeError('owned Docker creation failed')
            return result.returncode, result.stdout
        return self.docker_owner.run(*argv, **kwargs)

    def container(self, suffix, image, arguments=(), options=(), env=None):
        name = self.run_id + "-" + suffix
        cidfile = self.private / (suffix + ".cid")
        self.containers.append(cidfile)
        self.docker("run", "-d", "--cidfile", cidfile, "--name", name, "--label", "hexalith.qualification=" + self.run_id,
                    *options, image, *arguments, env=env, timeout=180)
        return cidfile.read_text().strip()

    def process(self, argv, env=None):
        started = utc()
        log = self.private / ("process-" + secrets.token_hex(4) + ".log")
        with log.open("w") as stream:
            process = subprocess.Popen(list(map(str, argv)), stdout=stream, stderr=subprocess.STDOUT, env=env)
        self.processes.append((process, argv, started, log))
        name = Path(argv[0]).name
        if name in self.identities.get('controlPlane', {}):
            self.receipts.append({'argv': [self.redact(str(value)) for value in argv], 'startedUtc': started,
                'endedUtc': utc(), 'exitCode': 0, 'output': '', 'processId': process.pid, 'event': 'native-launch'})
            self.identities['controlPlane'][name]['launchReceipt'] = len(self.receipts) - 1
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
        # Capture every pre-existing container, network and volume specification.
        # Raw credentials stay private; retained receipts contain only hashes.
        result, observations = {}, {}
        for kind, listing in (("container", ("ps", "-aq", "--no-trunc")),
                              ("network", ("network", "ls", "-q", "--no-trunc")),
                              ("volume", ("volume", "ls", "-q"))):
            _, output = self.docker(*listing)
            observations[kind] = {'listingReceipt': len(self.receipts) - 1, 'inspectionReceipt': None}
            ids = output.split()
            if not ids: continue
            def hashed(raw):
                rows = {}
                for value in json.loads(raw):
                    identity = value.get("Id", value["Name"])
                    if kind == "container":
                        specification = {key: value[key] for key in ("Name", "Config", "HostConfig", "Mounts")}
                    else:
                        specification = {key: item for key, item in value.items() if key not in ("Containers", "UsageData", "Status")}
                    rows[kind + ":" + identity] = hashlib.sha256(json.dumps(specification, sort_keys=True).encode()).hexdigest()
                return json.dumps(rows, sort_keys=True)
            _, output = self.docker("inspect", "--type", kind, *ids, retain_transform=hashed)
            rows = json.loads(hashed(output))
            observations[kind]['inspectionReceipt'] = len(self.receipts) - 1
            result.update(rows)
        phase = 'after' if getattr(self, '_inventory_captured', False) else 'before'
        self._inventory_captured = True
        self.receipts.append({'argv': ['WORKSPACE', 'inventory', phase], 'startedUtc': utc(), 'endedUtc': utc(),
            'exitCode': 0, 'output': json.dumps(result, sort_keys=True), 'resourceObservations': observations})
        self.identities.setdefault('workspaceInventories', {})[phase] = len(self.receipts) - 1
        return result

    def extract_control(self, name):
        container = self.run_id + "-" + name
        cidfile = self.private / (name + "-extract.cid")
        self.containers.append(cidfile)
        image = self.profile["images"][name]["reference"]
        self.docker("create", "--cidfile", cidfile, "--name", container, "--label", "hexalith.qualification=" + self.run_id, image, "./" + name)
        creation = next(index for index in reversed(range(len(self.receipts))) if self.receipts[index]['argv'][:2] == ['docker', 'create'])
        binary = self.private / name
        self.docker("cp", cidfile.read_text().strip() + ":/" + name, str(binary))
        extraction = len(self.receipts) - 1
        binary.chmod(0o755)
        if self.profile["runtime"]["version"] == "1.19.1":
            from dapr_runtime_qualification import CONTROL_BINARIES
            if digest(binary) != CONTROL_BINARIES[name]:
                raise ValueError("extracted control-plane executable identity mismatch")
        self.receipts.append({'argv': ['SHA256', str(binary)], 'startedUtc': utc(), 'endedUtc': utc(), 'exitCode': 0, 'output': digest(binary)})
        self.identities.setdefault("controlPlane", {})[name] = {"image": image, "binarySha256": digest(binary),
            'creationReceipt': creation, 'extractionReceipt': extraction, 'hashReceipt': len(self.receipts) - 1}
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
        owner = ExclusiveDocker(self.command, self.args.exclusive_worker)
        self.docker_owner = owner
        owner.capture()
        self.context = owner.endpoint
        self.identities["dockerCustody"] = {"exclusive": True, "endpoint": owner.endpoint,
            "daemonId": owner.daemon_id, "socketIdentity": owner.socket_identity, "workerId": owner.worker_id}
        self.before = self.inventory()
        write_json(self.output / "workspace-before.json", self.before)
        write_json(self.output / 'commands.json', self.receipts)
        write_json(self.output / 'execution-identities.json', self.identities)
        daprd = Path(os.environ.get("QUALIFICATION_DAPRD", str(Path.home() / ".dapr/bin/daprd"))).resolve()
        cli = Path(shutil.which("dapr") or "/missing-dapr")
        if digest(daprd) != self.profile["runtime"]["binarySha256"] or digest(cli) != self.profile["runtime"]["cliSha256"]:
            raise ValueError("daprd/CLI binary hash differs from runtime-lock.json")
        self.daprd = daprd
        self.command([str(daprd), "--version"])
        self.command([str(cli), "--version"])
        self.identities.update({"preflightPassed": True, "daprdSha256": digest(daprd), "cliSha256": digest(cli), "dockerEndpoint": owner.endpoint})
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
        # The image runs as openbao; unused image volumes become root-owned tmpfs.
        # Its supported switch avoids chown there while /data remains our bind.
        container = self.container("openbao", self.profile["openbao"]["reference"],
                       ["server", "-config=/work/bao.hcl"],
                       ["--cap-add=IPC_LOCK", "-e", "SKIP_CHOWN=1", "-p", "127.0.0.1::8200", "-v", f"{self.private}:/work:ro", "-v", f"{data}:/data"])
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
            pattern = PRESSURE_PATTERN if self.profile["runtime"]["version"] == "1.19.1" else "^qualification[.].*backpressure.*"
            self.docker("exec", self.broker_container, "rabbitmqctl", "set_policy", PRESSURE_POLICY, pattern,
                        json.dumps(PRESSURE_DEFINITION), "--apply-to", "queues")
            self.pressure_policy_receipt = len(self.receipts) - 1
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

    def binding_inventory(self):
        if self.broker == "rabbitmq":
            _, output = self.docker("exec", self.broker_container, "rabbitmqctl", "list_bindings",
                                    "source_name", "destination_name", "destination_kind", "routing_key", "--formatter=json")
        else:
            _, output = self.kafka("kafka-topics.sh", "--list")
        return len(self.receipts) - 1, missing_binding_absent(self.broker, output)

    def pressure_state(self):
        """Bound native offset/commit snapshots to each phase; these are diagnostics, not Dapr readback."""
        indexes = []
        if self.broker == "kafka":
            for when in ("earliest", "latest"):
                self.kafka("kafka-get-offsets.sh", "--topic", "qualification.backpressure", "--time", when)
                indexes.append(len(self.receipts) - 1)
            self.kafka("kafka-consumer-groups.sh", "--describe", "--group", "qualification-consumer", check=False)
            indexes.append(len(self.receipts) - 1)
        return indexes

    def reconcile(self, scenario, observation, readback):
        """Use the exact retained readback for all new diagnostic counts and conclusions."""
        if observation.get("diagnosticVersion") != 1 or scenario not in ("secret-rotation", "cancellation-backpressure"):
            return
        if scenario == "secret-rotation":
            request = self.receipts[observation["denialObservation"]["publicationReceipt"]]
            observation["oldGenerationDenied"] = attributable_denial(self.broker, request) and not any(
                row["id"] == observation["staleMessageId"] and row["kind"] == "effect" for row in readback["ledger"])
            observation["newGenerationEffects"] = sum(row["id"] == observation["messageId"] and row["kind"] == "effect" for row in readback["ledger"])
            publications = [{"id": observation["messageId"], "status": observation["newPublishStatus"]},
                            {"id": observation["staleMessageId"], "status": observation["stalePublishStatus"]}]
        else:
            publications = [*observation["publishResults"], {"id": observation["cancelledMessageId"], "status": 599}]
            observation["acknowledgedLoss"] = sum(row["status"] < 300 and not any(
                item["id"] == row["id"] and item["kind"] == "effect" for item in readback["ledger"]) for row in observation["publishResults"])
            observation["cancelledEffects"] = sum(row["id"] == observation["cancelledMessageId"] and row["kind"] == "effect" for row in readback["ledger"])
            rejected = [index for index, row in enumerate(self.receipts) if row.get("input", {}).get("id") in {item["id"] for item in observation["publishResults"]} and pressure_rejection(self.broker, row)]
            observation.update(negativeConfirmRejected=bool(rejected), rejectedConfirmReceipts=rejected)
            publications[-1]["status"] = next(row["httpStatus"] for row in reversed(self.receipts) if row.get("input", {}).get("id") == observation["cancelledMessageId"])
        observation["publicationReconciliation"] = reconcile_publications(publications, readback)
        observation["boundedObservationOnly"] = True

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
                readback = self.readback()
                self.reconcile(scenario, observation, readback)
                self.record(broker, scenario, observation, readback, self.diagnostics())
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
        selected = self.profile["runtime"]["version"] == "1.19.1"
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
                for topic in self.profile["topics"]:
                    if topic != "qualification.missing-binding":
                        self.kafka("kafka-acls.sh", "--add", "--allow-principal", "User:CN=rotated", "--operation", "Read", "--operation", "Write", "--operation", "Describe", "--topic", topic)
                self.kafka("kafka-acls.sh", "--add", "--allow-principal", "User:CN=rotated", "--operation", "Read", "--group", "qualification-consumer")
                self.broker_secret.update({"clientCert": (self.private / "rotated.crt").read_text(), "clientKey": (self.private / "rotated.key").read_text()})
            self.bao("secret/data/dapr/qualification-" + self.broker, {"data": self.broker_secret})
            self.stop(self.consumer)
            self.consumer, self.consumer_url = self.sidecar("qualification-consumer", self.app_port)
            self.publisher, self.publisher_url = self.sidecar("qualification-publisher", self.app_port)
            event_id, new_status = self.publish()
            self.wait(lambda: self.count(event_id) == 1)
            revocations = []
            if self.broker == "rabbitmq":
                self.docker("exec", self.broker_container, "rabbitmqctl", "delete_user", "qualification")
                revocations.append(len(self.receipts) - 1)
            else:
                for topic in self.profile["topics"]:
                    if topic != "qualification.missing-binding":
                        self.kafka("kafka-acls.sh", "--add", "--deny-principal", "User:CN=client", "--operation", "All", "--topic", topic)
                        revocations.append(len(self.receipts) - 1)
            if selected:
                if self.broker == "rabbitmq":
                    self.docker("exec", self.broker_container, "rabbitmqctl", "list_users", "--formatter=json")
                else:
                    self.kafka("kafka-acls.sh", "--list", "--topic", "qualification.events")
                state_receipt = len(self.receipts) - 1
            stale_id, stale_status = self.publish(endpoint=old_endpoint)
            stale_receipt = len(self.receipts) - 1
            # Retain a bounded observation window for delayed effects. Even an
            # explicit denial never proves that every uncertain request is absent.
            if selected:
                deadline = time.monotonic() + 3
                while time.monotonic() < deadline and self.count(stale_id) == 0:
                    time.sleep(.2)
            denied = (attributable_denial(self.broker, self.receipts[stale_receipt]) if selected else 400 <= stale_status < 599) and self.count(stale_id) == 0
            self.stop(old_publisher)
            observation = {"messageId": event_id, "oldGenerationDenied": denied, "newGenerationEffects": self.count(event_id), "controlledRestart": True, "staleMessageId": stale_id, "stalePublishStatus": stale_status}
            if selected:
                observation.update(diagnosticVersion=1, newPublishStatus=new_status,
                    denialObservation={"publicationReceipt": stale_receipt, "revocationReceipts": revocations, "stateReceipt": state_receipt})
            return observation
        if scenario == "cancellation-backpressure":
            self.stop(self.consumer)
            quota_receipt = None
            policy = None
            states = {}
            if selected:
                if self.broker == "rabbitmq":
                    _, output = self.docker("exec", self.broker_container, "rabbitmqctl", "list_queues", "name", "policy", "effective_policy_definition", "--formatter=json")
                    policy = {"policyReceipt": self.pressure_policy_receipt, "queueReceipt": len(self.receipts) - 1}
                    if not rabbitmq_pressure_effective(output):
                        raise RuntimeError("RabbitMQ backpressure policy is not effective on the exact consumer queue")
                else:
                    states["before"] = self.pressure_state()
            if self.broker == "kafka":
                self.kafka("kafka-configs.sh", "--alter", "--entity-type", "clients", "--entity-name", "qualification", "--add-config", "producer_byte_rate=1024")
                quota_receipt = len(self.receipts) - 1
            try:
                results = [self.publish("backpressure", data={"padding": "x" * 8192} if self.broker == "kafka" else None,
                                        timeout=12 if self.broker == "kafka" else 5) for _ in range(8)]
            finally:
                if self.broker == "kafka":
                    self.kafka("kafka-configs.sh", "--alter", "--entity-type", "clients", "--entity-name", "qualification", "--delete-config", "producer_byte_rate")
            if selected and self.broker == "kafka":
                states["afterPublish"] = self.pressure_state()
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
            observation = {"cancelled": status == 599, "cancelledMessageId": cancelled_id, "cancelledEffects": self.count(cancelled_id), "boundedSeconds": bounded, "negativeConfirmRejected": any(400 <= code < 599 for _, code in results), "acknowledgedLoss": loss, "publishResults": [{"id": event_id, "status": code} for event_id, code in results], "quotaReceipt": quota_receipt}
            if selected:
                if self.broker == "kafka":
                    states["afterReadback"] = self.pressure_state()
                observation.update(diagnosticVersion=1, policyObservation=policy, brokerStateReceipts=states,
                    consumerGroupStates={phase: kafka_group_available(self.receipts[indexes[2]]) for phase, indexes in states.items()})
            return observation
        if scenario == "missing-bindings":
            event_id, _ = self.publish()
            self.wait(lambda: self.count(event_id) == 1)
            if selected:
                before_receipt, absent_before = self.binding_inventory()
            missing_id, status = self.publish("missing-binding")
            observation = {"messageId": event_id, "unboundPublishStatus": status, "allowedEffects": self.count(event_id), "bindingAbsent": True, "unboundMessageId": missing_id}
            if selected:
                after_receipt, absent_after = self.binding_inventory()
                observation.update(diagnosticVersion=1, bindingAbsent=absent_before and absent_after,
                    bindingObservation={"beforeReceipt": before_receipt, "afterReceipt": after_receipt})
            return observation
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
        # Cleanup continues across independent failures and always seals its outcome.
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        signal.signal(signal.SIGINT, signal.SIG_IGN)
        for process, argv, started, log in self.processes:
            try:
                self.stop(process)
                self.receipts.append({"argv": [str(x) for x in argv], "startedUtc": started, "endedUtc": utc(), "exitCode": process.returncode, "output": self.redact(log.read_text())})
            except Exception as error:
                self.cleanup_errors.append(str(error))
        remaining, after = [], {}
        if self.context is not None:
            custody_valid = True
            unresolved = set()
            for cidfile in reversed(self.containers):
                if not cidfile.exists():
                    continue
                identity = cidfile.read_text().strip()
                unresolved.add(identity)
                if not custody_valid:
                    continue
                try:
                    if not re.fullmatch(r"[a-f0-9]{64}", identity):
                        raise ValueError("invalid owned container ID")
                    code, output = self.docker("inspect", identity, check=False)
                    if code == 0:
                        owned = json.loads(output)[0]
                        if owned["Id"] != identity or owned["Config"].get("Labels", {}).get("hexalith.qualification") != self.run_id:
                            raise ValueError("refusing cleanup of unowned container ID")
                        self.docker("logs", "--tail", "150", identity, check=False)
                        self.docker("unpause", identity, check=False)
                        self.docker("rm", "-f", "-v", identity)
                    # A failed inspect is not proof of absence. Inventory independently.
                    code, present = self.docker("ps", "-aq", "--no-trunc")
                    if code == 0 and identity not in present.split():
                        unresolved.discard(identity)
                except Exception as error:
                    self.cleanup_errors.append(identity + ": " + str(error))
                    if isinstance(error, CustodyError):
                        custody_valid = False
            if custody_valid:
                try:
                    # Cleanup the private mounted tree only after independent ID cleanup.
                    if self.containers and not unresolved:
                        helper = self.container("private-cleanup", self.profile["openbao"]["reference"],
                            ["-ec", f"chown -R {os.getuid()}:{os.getgid()} /cleanup\nchmod -R u+rwX /cleanup"],
                            ["--network", "none", "--user", "0", "-v", f"{self.private_parent}:/cleanup", "--entrypoint", "/bin/sh"])
                        unresolved.add(helper)
                        self.docker("wait", helper)
                        self.docker("rm", "-v", helper)
                        unresolved.discard(helper)
                    _, output = self.docker("ps", "-aq", "--no-trunc", "--filter", "label=hexalith.qualification=" + self.run_id)
                    unresolved.update(output.split())
                except Exception as error:
                    self.cleanup_errors.append(str(error))
                    for cidfile in self.containers:
                        if cidfile.exists(): unresolved.add(cidfile.read_text().strip())
            remaining = sorted(unresolved)
            try:
                after = self.inventory()
            except Exception as error:
                self.cleanup_errors.append("baseline comparison unavailable: " + str(error))
        remaining = sorted(set(remaining + getattr(self, "child_remaining", [])))
        journal = getattr(self, 'resource_journal', None)
        if journal is not None:
            try:
                reconciled = cleanup_child_resources(self, journal)
                remaining += [row.get('id') or row['name'] for row in reconciled['resources'] if not row['absent']]
                if not reconciled['complete']: self.cleanup_errors.append('owned resource journal reconciliation incomplete')
            except Exception as failure:
                self.cleanup_errors.append('owned resource journal reconciliation: ' + str(failure))
                remaining.append('unresolved-owner-journal')
        if not remaining:
            try:
                shutil.rmtree(self.private_parent)
            except OSError as error:
                self.cleanup_errors.append("private cleanup: " + str(error))
        self.cleanup_complete = not self.cleanup_errors and not remaining and all(after.get(key) == value for key, value in self.before.items())
        self.finalize_receipts(remaining, after)
        if self.docker_owner is not None:
            self.docker_owner.close()

    def finalize_receipts(self, remaining, after):
        write_json(self.output / "workspace-after.json", after)
        if not (self.output / "workspace-before.json").exists():
            write_json(self.output / "workspace-before.json", self.before)
        write_json(self.output / "cleanup.json", {"ownedResourcesRemaining": remaining, "complete": self.cleanup_complete, "errors": self.cleanup_errors, "unrelatedContainersUnchanged": all(after.get(key) == value for key, value in self.before.items()), "ownershipLabel": self.run_id, "privateMaterialRemoved": not self.private_parent.exists()})
        write_json(self.output / "execution-identities.json", self.identities)
        write_json(self.output / "commands.json", self.receipts)
        if getattr(self, "regression", False):
            return
        self.packet["artifacts"] = {str(p.relative_to(self.output)): digest(p) for p in sorted(self.output.rglob("*")) if p.is_file()}
        write_json(self.output / "packet.json", self.packet)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--exclusive-worker", type=Path, default=os.environ.get("QUALIFICATION_EXCLUSIVE_WORKER"), help="exclusive disposable worker allocation JSON; required before Docker mutation")
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
    return 1 if failures or not run.cleanup_complete else 0


if __name__ == "__main__":
    raise SystemExit(main())
