"""Invocation-owned published P1R consumers, measurements, backups and cleanup.

Every command and Boolean observation is retained, including failures. Package
downloads, restore graphs and loaded files are separate from the executor source
checkout and the selected package build provenance. No owner acceptance is made.
"""
from __future__ import annotations

import itertools
import base64
import datetime
import json
import os
from pathlib import Path
import shutil
import signal
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid

import p1r_check_witnesses as witnesses
import p1r_published_qualification as qualification
import p1r_qualification as preparation
import p1r_qualification_runtime as runtime
from p1r_qualification import InvalidPacket, canonical, checks_counter, digest, regular, require, stamp, write_json as _write_json

ROOT = preparation.ROOT
CONSUMERS = ROOT / "tools/p1r-published-consumers"
MECHANISM = "p1r-executed-checks-v1"
CANDIDATE = "3.119.0"
VERSIONS = ("3.110.0", "3.70.1", CANDIDATE)
REDIS = "redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf"
POSTGRES = "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
DAPR_IMAGE = "daprio/dapr@sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59"
TAG_COMMIT = "f463442cca19e4199982a23a08bae4a490767d4a"
BUILDS_COMMIT = "2cf00028bbe563d80d4d12b5fb2054914f14fcb6"
TAG_BUILDS_COMMIT = "468fdbba04e2d9a27d251298875125b57fa6d836"
PROJECTS = ("Host", "Domain", "Probe")
STATUS_RETRYABILITY = (None, False, True)
DOCKER_FORMAT = ('{"Id":{{json .Id}},"Image":{{json .Image}},"Name":{{json .Name}},'
                 '"State":{"Running":{{json .State.Running}},"StartedAt":{{json .State.StartedAt}}},'
                 '"Config":{"Labels":{"hexalith.p1r.invocation":{{json (index .Config.Labels "hexalith.p1r.invocation")}}}}}')
RESERVED_PORTS = set()
PACKAGE_IDS = tuple("Hexalith.EventStore." + name for name in ("Client", "Contracts", "DomainService", "Server", "ServiceDefaults"))
REDIS_DIAGNOSTIC = ("local rows={} for _,key in ipairs(redis.call('KEYS','*')) do "
                    "if redis.call('TYPE',key).ok=='hash' then local data=redis.call('HGET',key,'data') "
                    "if data then table.insert(rows,{key,data}) end end end return cjson.encode(rows)")


def write_json(path, value):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    _write_json(Path(path), value)


def safe_inspect_argv(*identities):
    """Ask Docker for only public preservation identities and this invocation's ownership label."""
    return ["docker", "inspect", "--format", DOCKER_FORMAT, *identities]


def docker_observations(data):
    """Decode the bounded safe-format observation returned once per container."""
    return [json.loads(line) for line in data.decode().splitlines() if line.strip()]


def postgres_value(value):
    """Decode PostgreSQL v1's base64 JSONB string to the original Dapr state bytes."""
    if isinstance(value, str):
        try:
            return base64.b64decode(value, validate=True)
        except (ValueError, base64.binascii.Error):
            pass
    return canonical(value)


def redis_inventory(container, invocation):
    """Read bounded fixture diagnostics only after independently verifying container ownership."""
    def command(*args):
        return subprocess.check_output(["docker", "exec", container, "redis-cli", "--raw", *args])
    observed = docker_observations(subprocess.check_output(safe_inspect_argv(container)))[0]
    require(observed["Id"] == container and observed["Config"]["Labels"].get("hexalith.p1r.invocation") == invocation,
            "unowned provider inventory")
    rows = []
    values = json.loads(command("EVAL", REDIS_DIAGNOSTIC, "0"))
    for key, raw_data in values:
        data = raw_data.encode()
        value = json.loads(data)
        tenant = "tenant-a" if "tenant-a" in key else "tenant-b" if "tenant-b" in key else "infrastructure"
        kind, sequence, floor = "bookkeeping", None, None
        if key.endswith(":metadata"):
            kind, sequence, floor = "metadata", value.get("currentSequence"), value.get("retainedFloor", 1)
        elif ":events:" in key:
            kind, sequence = "event", int(key.rsplit(":", 1)[1])
        elif key.endswith(":snapshot"):
            kind, sequence = "snapshot", value.get("sequenceNumber")
        rows.append({"key": key, "tenant": tenant, "kind": kind, "sha256": digest(data), "sequence": sequence, "floor": floor})
    rows.sort(key=lambda row: row["key"])
    runtime.validate_rows(rows)
    return rows


def postgres_inventory(container, invocation):
    """Read structural fixture state from an exactly owned PostgreSQL v1 container."""
    observed = docker_observations(subprocess.check_output(safe_inspect_argv(container)))[0]
    require(observed["Id"] == container and observed["Config"]["Labels"].get("hexalith.p1r.invocation") == invocation,
            "unowned provider inventory")
    query = "SELECT coalesce(json_agg(json_build_array(key,value) ORDER BY key)::text,'[]') FROM state"
    data = subprocess.check_output(["docker", "exec", container, "psql", "-U", "postgres", "-d", "eventstore",
                                    "-At", "-v", "ON_ERROR_STOP=1", "-c", query])
    rows = []
    for key, value in json.loads(data):
        raw_data = postgres_value(value)
        try:
            value = json.loads(raw_data)
        except (ValueError, UnicodeError):
            value = None
        tenant = "tenant-a" if "tenant-a" in key else "tenant-b" if "tenant-b" in key else "infrastructure"
        kind, sequence, floor = "bookkeeping", None, None
        if key.endswith(":metadata"):
            require(isinstance(value, dict), "invalid persisted metadata content")
            kind, sequence, floor = "metadata", value.get("currentSequence"), value.get("retainedFloor", 1)
        elif ":events:" in key:
            kind, sequence = "event", int(key.rsplit(":", 1)[1])
        elif key.endswith(":snapshot"):
            require(isinstance(value, dict), "invalid persisted snapshot content")
            kind, sequence = "snapshot", value.get("sequenceNumber")
        rows.append({"key": key, "tenant": tenant, "kind": kind, "sha256": digest(raw_data), "sequence": sequence, "floor": floor})
    rows.sort(key=lambda row: row["key"])
    runtime.validate_rows(rows)
    return rows


class Measurements:
    """Record executed checks without failing early or inventing totals."""

    def __init__(self):
        self.checks = []
        self.witnesses = []

    def check(self, identity, passed, witness=None):
        require(type(passed) is bool and not any(row["id"] == identity for row in self.checks),
                "invalid or duplicate executed check")
        witness = witness or witnesses.capture(identity)
        require(witness["kind"] == "probe" or witnesses.evaluate(witness["predicate"]) is passed, "check predicate contradicts observation")
        self.witnesses.append(witness)
        self.checks.append({"id": identity, "passed": passed})

    def import_probe(self, value, prefix, command=None):
        require(value.get("instrumentation") == MECHANISM, "probe instrumentation substituted")
        measured = value["measurement"]
        preparation.validate_counter(measured["assertions"], measured["checks"])
        for check in measured["checks"]:
            self.check(prefix + check["id"], check["passed"],
                       {"id": prefix + check["id"], "kind": "probe", "command": command, "probe_id": check["id"]})

    @property
    def counter(self):
        return checks_counter(self.checks)


def reserve_port():
    # Stay below Linux's ephemeral client range: releasing an ephemeral listener
    # before Docker publishes it otherwise races ordinary outbound connections.
    for _ in range(100):
        port = 10000 + uuid.uuid4().int % 20000
        if port in RESERVED_PORTS:
            continue
        try:
            with socket.socket() as connection:
                connection.bind(("127.0.0.1", port))
            RESERVED_PORTS.add(port)
            return port
        except OSError:
            continue
    raise InvalidPacket("invocation loopback port unavailable")


def validate_execution_inputs(inputs):
    """Refuse a substituted approved tuple before any consumer or operational resource starts."""
    require(qualification.validate_inputs(inputs) == "owner-selected", "synthetic inputs cannot execute published operations")
    candidate, profile = inputs["candidate"], inputs["operational_profile"]
    require(candidate["version"] == CANDIDATE and candidate["tag"] == "v3.119.0"
            and candidate["tag_commit"] == TAG_COMMIT
            and candidate["builds"] == {"version": "4.30.1-20-g2cf0002", "commit": BUILDS_COMMIT}
            and inputs["rollback"] is None and inputs["assertion_instrumentation"]["mechanism"] == MECHANISM
            and [row["version"] for row in inputs.get("comparisons", [])] == list(qualification.COMPARISON_VERSIONS)
            and profile["runtime"] == "dapr" and profile["runtime_version"] == "1.18.2"
            and profile["backend"] == "state.postgresql" and profile["backend_image"] == POSTGRES
            and inputs["selected_additions"] == list(qualification.ADDITIONS), "execution selections substituted")


def semantic_observation(observation, outcome):
    """Normalize only declared semantic outputs and inventory coordinates."""
    result = {"outcome": outcome}
    for name in ("metadata", "primary", "secondary", "actor", "append", "replay"):
        value = observation.get(name)
        if isinstance(value, dict):
            result[name] = {key: value[key] for key in ("sequence", "floor", "accepted", "event_count", "etag", "last_modified") if key in value}
    for name in ("writer", "reader"):
        if name in observation:
            result[name] = {key:value for key,value in observation[name].items() if key not in ("output_sha256",)}
    for name in ("before", "after", "appended_inventory"):
        if observation.get(name) is not None:
            result[name] = [{key:item[key] for key in ("tenant", "kind", "sequence", "floor")}
                            for item in observation[name] if item["kind"] != "bookkeeping"]
    return result


def semantic_delta(package, source):
    return [{"field": key, "package": package.get(key), "source": source.get(key)}
            for key in sorted(set(package) | set(source)) if package.get(key) != source.get(key)]


class Executor:
    """Own all resources by exact identity and retain partial progress on interruption."""

    def __init__(self, output, inputs=None):
        self.output = Path(output).absolute()
        require(not self.output.exists() and self.output == self.output.resolve(), "output exists or is substituted")
        self.output.mkdir(parents=True)
        self.invocation = uuid.uuid4().hex
        self.scratch = Path(tempfile.mkdtemp(prefix="p1r-published-" + self.invocation + "-"))
        self.inputs = inputs
        self.inputs_sha256 = digest(canonical(inputs)) if inputs else None
        self.commands, self.containers, self.processes, self.active = [], {}, [], []
        self.pending_containers = {}
        preparation.subreaper()
        self.receipts, self.identities, self.evidence = [], {}, {}
        self.configurations = []
        self.witness_sources = {}
        self.shared_before = None
        self.fixture = {"id": "p1r-" + self.invocation, "synthetic": False}
        self.started = stamp()
        self.errors = []
        self.seeds = {}
        self.digest_key = base64.b64encode(os.urandom(32)).decode()
        self.delegation = uuid.uuid4().hex
        self.app_token = uuid.uuid4().hex
        self.workload_key = base64.b64encode(os.urandom(48)).decode()
        self.invocation_argv = [part.decode() for part in regular(Path(f"/proc/{os.getpid()}/cmdline")).split(b"\0") if part]
        self.pending_restore = None
        self.operational_started = False
        self.current_checks = None
        self.result = {"schema": "hexalith.p1r.executor.v1", "invocation": self.invocation, "started_utc": self.started,
                       "finished_utc": None, "executor_source_sha256": None,
                       "inputs_sha256": self.inputs_sha256, "receipts": [], "errors": [], "p1r_usable": False}
        self.save()
        try:
            self.source_binding = preparation.source_binding()
            write_json(self.output / "executor-source.json", self.source_binding)
            self.result["executor_source_sha256"] = digest(regular(self.output / "executor-source.json"))
            self.shared_before = self.shared()
            self.save()
        except BaseException as error:
            self.errors.append("initialization: " + (str(error) if isinstance(error, InvalidPacket) else type(error).__name__))
            self.cleanup()
            raise

    def save(self):
        write_json(self.output / "commands.json", self.commands)
        self.result.update(receipts=self.receipts, errors=self.errors)
        write_json(self.output / "execution.json", self.result)

    def record(self, argv, cwd, started, code, data, retain=True, ownership=None, cleanup=None, timeout=None):
        output = data.decode("utf-8", "replace") if retain else None
        row = {"id": len(self.commands) + 1, "argv": [str(arg) for arg in argv], "cwd": str(cwd),
               "started_utc": started, "finished_utc": max(started, stamp()), "exit_code": code,
               "output_sha256": digest(output.encode() if retain else data), "output": output}
        if timeout is not None:
            row["timeout_seconds"] = timeout
        if ownership is not None:
            row.update(process=ownership.root, owned_processes=list(ownership.owned.values()), cleanup=cleanup or [])
            if ownership.cleanup_diagnostics:
                row["cleanup_error_diagnostics"] = list(ownership.cleanup_diagnostics)
        if "--resources-path" in row["argv"]:
            row["runtime_configuration"] = self.configurations[-1]["id"]
            row["runtime_configuration_sha256"] = digest(canonical(self.configurations[-1]))
        self.commands.append(row)
        self.save()
        return row

    def run(self, argv, cwd=None, env=None, timeout=180, input_bytes=None, retain=True, check=True):
        """Own synchronous trees, including nonce-registered detached descendants."""
        cwd = Path(cwd or self.scratch)
        started, interrupted, code = stamp(), False, 127
        ownership = preparation.OwnedProcesses()
        log = tempfile.TemporaryFile()
        input_file = tempfile.TemporaryFile() if input_bytes is not None else None
        item = None
        if input_file is not None:
            input_file.write(input_bytes)
            input_file.seek(0)
        try:
            process = subprocess.Popen([str(arg) for arg in argv], cwd=cwd,
                env=dict(os.environ if env is None else env, HEXALITH_P1R_CONTROL_NONCE=ownership.nonce),
                stdin=input_file if input_file is not None else subprocess.DEVNULL,
                stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            ownership.attach(process)
            item = {"process": process, "ownership": ownership, "argv": argv, "started": started, "synchronous": True}
            self.processes.append(item)
            deadline = time.monotonic() + timeout
            while process.poll() is None:
                ownership.discover()
                if time.monotonic() >= deadline:
                    code = 124
                    break
                time.sleep(min(.05, max(0, deadline - time.monotonic())))
            else:
                code = process.returncode
        except OSError as error:
            log.write((type(error).__name__ + ": " + str(error)).encode())
        except KeyboardInterrupt:
            code, interrupted = 130, True
        finally:
            handler = signal.signal(signal.SIGINT, signal.SIG_IGN)
            try:
                cleanup = [ownership.cleanup(), ownership.cleanup()] if item else []
                if code == 124:
                    log.write(b"\nexecutor timeout\n")
                if interrupted:
                    log.write(b"\nexecutor interrupted\n")
                log.seek(0)
                data = log.read()
                self.record(argv, cwd, started, code, data, retain, ownership, cleanup, timeout)
            finally:
                log.close()
                if input_file is not None:
                    input_file.close()
                signal.signal(signal.SIGINT, handler)
        if interrupted:
            raise KeyboardInterrupt("owned command interrupted; refusal and cleanup retained")
        require(all(not attempt["remaining"] and not attempt["errors"] for attempt in cleanup), "owned command cleanup failed")
        require(not check or code == 0, "command failed: " + " ".join(str(arg) for arg in argv) + "; exit=" + str(code))
        return data

    def http(self, method, url, value=None, timeout=45):
        body = None if value is None else canonical(value)
        argv = ["HTTP", method, url, "request-sha256=" + digest(body or b"")]
        started = stamp()
        try:
            request = urllib.request.Request(url, data=body, method=method, headers={"Content-Type": "application/json"})
            try:
                with urllib.request.urlopen(request, timeout=timeout) as response:
                    data, status = response.read(), response.status
            except urllib.error.HTTPError as error:
                data, status = error.read(), error.code
            self.record(argv, self.scratch, started, 0 if status < 400 else status, data)
            require(status < 400, "HTTP request refused: " + str(status))
            return json.loads(data) if data else None
        except (OSError, urllib.error.URLError) as error:
            self.record(argv, self.scratch, started, 124, type(error).__name__.encode())
            raise

    def shared(self):
        names = self.run(["docker", "ps", "-aq"]).decode().split()
        if not names:
            return {}
        rows = docker_observations(self.run(safe_inspect_argv(*names)))
        return {row["Id"]: {"image": row["Image"], "running": row["State"]["Running"], "started": row["State"]["StartedAt"]}
                for row in rows if row["Id"] not in self.containers}

    def container(self, role, image, arguments=(), port=6379, start=True, host_port=None, environment=None):
        name = "p1r-" + self.invocation + "-" + role + "-" + uuid.uuid4().hex[:8]
        host_port = host_port or reserve_port()
        cidfile = self.scratch / (name + ".cid")
        argv = ["docker", "create", "--cidfile", str(cidfile), "--name", name, "--label",
                "hexalith.p1r.invocation=" + self.invocation, "-p", f"127.0.0.1:{host_port}:{port}"]
        if image == POSTGRES:
            argv += ["-e", "POSTGRES_PASSWORD", "-e", "POSTGRES_DB=eventstore"]
        argv += [image, *arguments]
        self.pending_containers[name] = {"name": name, "image": image, "role": role, "port": host_port}
        try:
            self.run(argv, env=environment)
        finally:
            identity = self.recover_container(name, cidfile)
        require(identity is not None, "created owned container identity unavailable")
        if start:
            self.run(["docker", "start", identity])
        return identity, host_port

    def recover_container(self, name, cidfile=None):
        """Recover interrupted creation only through exact name AND invocation label."""
        requested = cidfile.read_text().strip() if cidfile is not None and cidfile.is_file() else name
        data = self.run(safe_inspect_argv(requested), cwd=ROOT, check=False)
        if self.commands[-1]["exit_code"] != 0:
            return None
        row = docker_observations(data)[0]
        require(row["Name"] == "/" + name
                and row["Config"]["Labels"].get("hexalith.p1r.invocation") == self.invocation
                and (requested == name or requested == row["Id"]), "container creation ownership substituted")
        identity = row["Id"]
        self.containers[identity] = self.pending_containers.pop(name)
        self.result["owned_containers"] = dict(self.containers)
        self.save()
        return identity

    def launch(self, argv, env):
        log = self.scratch / ("process-" + uuid.uuid4().hex + ".log")
        ownership = preparation.OwnedProcesses()
        environment = dict(env, HEXALITH_P1R_CONTROL_NONCE=ownership.nonce)
        handle = log.open("wb")
        started = stamp()
        process = subprocess.Popen([str(arg) for arg in argv], cwd=self.scratch, env=environment,
                                   stdout=handle, stderr=subprocess.STDOUT, start_new_session=True)
        handle.close()
        ownership.attach(process)
        item = {"process": process, "ownership": ownership, "log": log, "argv": argv, "started": started}
        self.processes.append(item)
        self.active.append(item)
        return item

    def stop(self, item):
        cleanup = item["ownership"].cleanup()
        code = item["process"].poll()
        require(code is not None, "owned process exit status unavailable")
        self.record(item["argv"], self.scratch, item["started"], code, item["log"].read_bytes(),
                    ownership=item["ownership"], cleanup=[cleanup])
        require(not cleanup["remaining"] and not cleanup["errors"], "owned process cleanup failed")

    def stop_nodes(self):
        errors = []
        for item in reversed(self.active):
            try:
                self.stop(item)
            except (Exception, KeyboardInterrupt) as error:
                errors.append(type(error).__name__)
        self.active = [item for item in self.active if item["process"].poll() is None]
        require(not errors, "owned node cleanup failed: " + ",".join(errors))

    def wait(self, url, item):
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            require(item["process"].poll() is None, "owned application exited during startup")
            try:
                return self.http("GET", url, timeout=1)
            except (OSError, urllib.error.URLError, InvalidPacket):
                time.sleep(.15)
        raise TimeoutError("owned endpoint startup timeout")

    def build_consumers(self, version, source=False):
        role = "source" if source else "candidate" if version == CANDIDATE else "comparison-" + version
        root = self.output / "consumers" / role
        shutil.copytree(CONSUMERS, root)
        packages = root / "packages"
        environment = dict(os.environ, CI="true", NUGET_PACKAGES=str(packages), DOTNET_NOLOGO="true",
                           DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE="false", DOTNET_USE_POLLING_FILE_WATCHER="1")
        configuration = "Debug" if source else "Release"
        props = ["-p:EventStoreVersion=" + version, "-p:NuGetAudit=false"]
        if source:
            environment.pop("CI", None)
            props += ["-p:UseCurrentSource=true", "-p:UseHexalithProjectReferences=true", "-p:EventStoreSourceRoot=" + str(ROOT)]
        manifest = {"schema": qualification.EVIDENCE_SCHEMA, "role": role, "fixture": None,
                    "packages_root": "packages", "configuration": configuration, "projects": []}
        for project in PROJECTS:
            csproj = root / project.lower() / (project + ".csproj")
            self.run(["dotnet", "restore", csproj, *props], cwd=root, env=environment, timeout=300)
            self.run(["dotnet", "build", csproj, "--no-restore", "-c", configuration, "-m:1", *props],
                     cwd=root, env=environment, timeout=300)
            output = project.lower() + "/bin/" + configuration + "/net10.0"
            # Physically load all five package assemblies in each actual consumer.
            if project == "Probe":
                observed = json.loads(self.run(["dotnet", root / output / "Probe.dll", "identity"], cwd=root, env=environment))
                identity = observed["observation"]
            else:
                app_port = reserve_port()
                item = self.launch(["dotnet", root / output / (project + ".dll")],
                                   dict(environment, ASPNETCORE_URLS=f"http://127.0.0.1:{app_port}", ASPNETCORE_ENVIRONMENT="Development"))
                try:
                    ready = self.wait(f"http://127.0.0.1:{app_port}/ready", item)
                    identity = ready["identity"] if (source or version == CANDIDATE) and project == "Domain" else self.http("GET", f"http://127.0.0.1:{app_port}/identity")
                finally:
                    self.stop_nodes()
            loaded = "loaded/" + project + ".json"
            write_json(root / loaded, identity)
            manifest["projects"].append({"name": project, "assets": project.lower() + "/obj/project.assets.json",
                                         "lock": project.lower() + "/packages.lock.json", "loaded": loaded, "output": output})
            self.identities.setdefault(version if not source else "source", []).extend(identity["assemblies"])
        write_json(root / "evidence.json", manifest)
        self.evidence[role] = root
        return root

    def observe_inputs(self, planning, reference):
        """Observe the already approved six selections; verify downloads rather than reuse preflight passes."""
        candidate = planning["candidate_proposed"]
        require(candidate["version"] == CANDIDATE and candidate["tag_commit"] == TAG_COMMIT
                and candidate["builds"]["commit"] == BUILDS_COMMIT, "candidate selection substituted")
        tag_builds = preparation.git(ROOT, "ls-tree", "v" + CANDIDATE, "references/Hexalith.Builds").decode().split()
        require(len(tag_builds) == 4 and tag_builds[2] == TAG_BUILDS_COMMIT, "candidate package-build provenance substituted")
        selections = {}
        for version in VERSIONS:
            root = self.build_consumers(version)
            tag_commit = preparation.git(ROOT, "rev-parse", "v" + version + "^{commit}").decode().strip()
            if version == CANDIDATE:
                builds = {key: candidate["builds"][key] for key in ("version", "commit")}
                require(tag_commit == candidate["tag_commit"], "candidate tag substituted")
            else:
                tree = preparation.git(ROOT, "ls-tree", "v" + version, "references/Hexalith.Builds").decode().split()
                require(len(tree) == 4 and tree[:2] == ["160000", "commit"], "comparison Builds provenance missing")
                commit = tree[2]
                builds_root = (Path(self.source_binding["workspace_builds"]["path"]) if "workspace_builds" in self.source_binding
                               else Path(self.source_binding["repository"]) / "references/Hexalith.Builds")
                described = preparation.git(builds_root, "describe", "--tags", "--always", commit).decode().strip()
                builds = {"version": described.removeprefix("v"), "commit": commit}
            selected = []
            for identifier in PACKAGE_IDS:
                lower = identifier.lower()
                base = root / "packages" / lower / version
                archive = base / f"{lower}.{version}.nupkg"
                data = regular(archive)
                nuspec, entries, dlls = qualification.inspect_archive(archive, data)
                require(nuspec["valid"] and nuspec["id"] == identifier and nuspec["version"] == version
                        and entries["signature"] and not entries["synthetic_marker"], "published archive substituted")
                metadata = qualification.parse_json(regular(base / ".nupkg.metadata"))
                row = {"id": identifier, "archive_sha256": digest(data), "content_hash": metadata["contentHash"],
                       "repository_commit": nuspec["repository_commit"]}
                if version == CANDIDATE:
                    expected = next(package for package in candidate["packages"] if package["id"] == identifier)
                    require(all(row[key] == expected[key] for key in row), "candidate archive differs from approved observation")
                self.run(["dotnet", "nuget", "verify", "--all", archive, "--verbosity", "minimal"], cwd=root)
                require(qualification.REPOSITORY_SIGNATURE in self.commands[-1]["output"], "repository signature not verified")
                target = self.output / "archives" / (identifier + "." + version + ".nupkg")
                target.parent.mkdir(exist_ok=True)
                shutil.copyfile(archive, target)
                selected.append(row)
            selections[version] = {"version": version, "tag": "v" + version, "tag_commit": tag_commit,
                                   "builds": builds, "feed": candidate["feed"], "packages": selected}
        inputs = {"schema": qualification.INPUTS_SCHEMA, "fixture": None,
                  "authority": {"owner": "user", "reference": reference, "date": "2026-10-10"},
                  "candidate": selections[CANDIDATE], "rollback": None,
                  "comparisons": [selections[version] for version in qualification.COMPARISON_VERSIONS],
                  "operational_profile": {"runtime": "dapr", "runtime_version": "1.18.2", "backend": "state.postgresql",
                                          "backend_image": POSTGRES, "selected_by": "user", "reference": reference},
                  "assertion_instrumentation": {"mechanism": MECHANISM, "accepted_by": "test-owner", "reference": reference},
                  "selected_additions": list(qualification.ADDITIONS)}
        qualification.validate_inputs(inputs)
        self.inputs = inputs
        self.inputs_sha256 = digest(canonical(inputs))
        write_json(self.output / "owner-inputs.json", inputs)
        self.inputs_sha256 = digest(regular(self.output / "owner-inputs.json"))
        self.result["inputs_sha256"] = self.inputs_sha256
        decisions = {"schema": qualification.DECISIONS_SCHEMA, "fixture": None, "inputs_sha256": self.inputs_sha256,
                     "decisions": [{"role": role, "decision": "pending", "owner": None, "date": None, "reference": None}
                                   for role in qualification.DECISION_ROLES],
                     "conformance": {"status": "pending", "baseline": None, "reference": None}}
        write_json(self.output / "owner-decisions.json", decisions)
        return inputs

    def topology(self):
        require(self.inputs["operational_profile"]["backend_image"] == POSTGRES
                and self.inputs["operational_profile"]["runtime_version"] == "1.18.2", "selected operational profile substituted")
        self.shared_before = self.shared()
        self.placement, self.placement_port = self.container("placement", DAPR_IMAGE, ["./placement", "--port", "50005"], 50005)
        self.scheduler_port = reserve_port()
        self.scheduler, self.scheduler_port = self.container("scheduler", DAPR_IMAGE,
            ["./scheduler", "--port", "50006", "--override-broadcast-host-port", f"127.0.0.1:{self.scheduler_port}",
             "--etcd-data-dir", "/tmp/p1r-etcd", "--etcd-client-listen-address", "0.0.0.0"], 50006, host_port=self.scheduler_port)
        self.pubsub, self.pubsub_port = self.container("pubsub", REDIS)
        self.postgres_password = uuid.uuid4().hex
        self.redis, self.redis_port = self.postgres_container("source")
        self.daprd = self.scratch / "daprd"
        self.run(["docker", "cp", self.placement + ":/daprd", self.daprd])
        self.daprd.chmod(0o700)
        version = self.run([self.daprd, "--version"]).decode()
        require(version.strip() == "1.18.2", "Dapr runtime substituted")
        self.operational_started = True
        write_json(self.output / "runtime-identity.json", {"profile": self.inputs["operational_profile"],
                   "daprd_sha256": digest(regular(self.daprd)), "dapr_version": version.strip(),
                   "images": docker_observations(self.run(safe_inspect_argv(self.placement, self.scheduler, self.pubsub, self.redis)))})

    def postgres_container(self, role, start=True):
        environment = dict(os.environ, POSTGRES_PASSWORD=self.postgres_password)
        identity, port = self.container(role, POSTGRES,
                                        port=5432, start=start, environment=environment)
        if start:
            self.postgres_wait(identity)
        return identity, port

    def postgres_wait(self, identity):
        for _ in range(60):
            self.run(["docker", "exec", identity, "pg_isready", "-h", "127.0.0.1", "-U", "postgres", "-d", "eventstore"], check=False)
            if self.commands[-1]["exit_code"] == 0:
                return
            time.sleep(.5)
        raise TimeoutError("owned PostgreSQL fixture startup timeout")

    def start_nodes(self, version, redis=None, port=None, interval=1000):
        self.stop_nodes()
        resources = self.scratch / "resources"
        resources.mkdir(exist_ok=True)
        redis, port = redis or self.redis, port or self.redis_port
        tracked_state = regular(ROOT / "deploy/dapr/statestore-postgresql.yaml")
        require(b'type: state.postgresql' in tracked_state and b'version: v1' in tracked_state
                and b'{env:POSTGRES_CONNECTION_STRING}' in tracked_state, "tracked PostgreSQL v1 component substituted")
        connection = (f"host=127.0.0.1 port={port} user=postgres password={self.postgres_password} "
                      "dbname=eventstore sslmode=disable connect_timeout=10")
        (resources / "state.yaml").write_bytes(tracked_state.replace(b'{env:POSTGRES_CONNECTION_STRING}', connection.encode()))
        (resources / "pubsub.yaml").write_text(f'apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: pubsub\nspec:\n  type: pubsub.redis\n  version: v1\n  metadata:\n    - name: redisHost\n      value: "127.0.0.1:{self.pubsub_port}"\nscopes:\n  - eventstore\n')
        config = self.scratch / "discovery.yaml"
        config.write_text(f'apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: p1r-private\nspec:\n  features:\n    - name: HotReload\n      enabled: false\n  nameResolution:\n    component: sqlite\n    version: v1\n    configuration:\n      connectionString: "{self.scratch / "discovery.sqlite"}"\n')
        configuration = {
            "id": uuid.uuid4().hex, "version": version, "state_container": redis, "snapshot_interval": interval,
            "state_component_sha256": digest(tracked_state), "backend_image": POSTGRES,
            "runtime_version": self.inputs["operational_profile"]["runtime_version"],
            "observed_utc": stamp(),
            "source_workload_authority": "private Development symmetric JWT; production identity and P2 acceptance pending" if version in ("source", CANDIDATE) else None,
            "dotnet_reload_config_on_change": False, "dotnet_polling_file_watcher": True,
            "files": [{"name": path.name, "path": str(path),
                       "sha256": digest(tracked_state) if path.name == "state.yaml" else digest(regular(path)),
                       "content": tracked_state.decode() if path.name == "state.yaml" else path.read_text(),
                       **({"rendered_sha256": digest(regular(path)), "credential_redacted": True}
                          if path.name == "state.yaml" else {})}
                      for path in (config, resources / "state.yaml", resources / "pubsub.yaml")]}
        write_json(self.output / "configurations" / (configuration["id"] + ".json"), configuration)
        self.configurations.append(configuration)
        role = "source" if version == "source" else "candidate" if version == CANDIDATE else "comparison-" + version
        root = self.evidence[role]
        configuration = "Debug" if version == "source" else "Release"
        for project, appid in (("Domain", "counter"), ("Host", "eventstore")):
            app, http, grpc, internal, metrics, profile = [reserve_port() for _ in range(6)]
            environment = dict(os.environ, CI="true", NUGET_PACKAGES=str(root / "packages"),
                DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE="false", DOTNET_USE_POLLING_FILE_WATCHER="1",
                ASPNETCORE_ENVIRONMENT="Development", ASPNETCORE_URLS=f"http://127.0.0.1:{app}", DAPR_HTTP_PORT=str(http),
                DAPR_GRPC_PORT=str(grpc), NAMESPACE="p1r-" + self.invocation,
                P1R_DIGEST_KEY=self.digest_key, P1R_DELEGATION=self.delegation, APP_API_TOKEN=self.app_token,
                P1R_REMINDER_DUE=getattr(self, "reminder_due", "2030-01-01T00:00:00+00:00"),
                EventStore__Actors__AggregateActorTypeName="AggregateActor", EventStore__Snapshots__DefaultInterval=str(interval),
                EventStore__DomainServices__Registrations__counter__AppId="counter")
            environment.pop("POSTGRES_CONNECTION_STRING", None)
            registration = "EventStore__DomainServices__Registrations__*|counter|v1__"
            environment.update({registration + key: value for key, value in
                                {"AppId": "counter", "MethodName": "process", "TenantId": "*", "Domain": "counter", "Version": "v1"}.items()})
            if version in ("source", CANDIDATE):
                environment.update(Authentication__JwtBearer__Issuer="p1r-" + self.invocation,
                    Authentication__JwtBearer__Audience="counter", Authentication__JwtBearer__SigningKey=self.workload_key,
                    Authentication__Workload__Audience="counter", Authentication__Workload__AllowedCallers__0="eventstore",
                    Authentication__WorkloadIssuer__Workload=appid, EventStore__DomainService__AppId=appid)
            item = self.launch(["dotnet", root / project.lower() / "bin" / configuration / "net10.0" / (project + ".dll")], environment)
            self.wait(f"http://127.0.0.1:{app}/ready", item)
            sidecar = self.launch([self.daprd, "--app-id", appid, "--app-port", str(app), "--app-channel-address", "127.0.0.1",
                "--dapr-http-port", str(http), "--dapr-grpc-port", str(grpc), "--dapr-internal-grpc-port", str(internal),
                "--metrics-port", str(metrics), "--profile-port", str(profile), "--resources-path", resources, "--config", config,
                "--placement-host-address", f"127.0.0.1:{self.placement_port}", "--scheduler-host-address", f"127.0.0.1:{self.scheduler_port}",
                "--log-level", "warn"], environment)
            self.wait(f"http://127.0.0.1:{http}/v1.0/healthz/outbound", sidecar)
            if project == "Host":
                self.host_port, self.sidecar_port = app, http
            else:
                self.domain_port = app
        time.sleep(1)
        return [item["ownership"].root for item in self.active]

    def probe(self, version, arguments, checks, prefix="probe:", timeout=180):
        role = "source" if version == "source" else "candidate" if version == CANDIDATE else "comparison-" + version
        root = self.evidence[role]
        configuration = "Debug" if version == "source" else "Release"
        data = self.run(["dotnet", root / "probe" / "bin" / configuration / "net10.0/Probe.dll", *arguments], check=False, timeout=timeout)
        value = json.loads(data)
        checks.import_probe(value, prefix, self.commands[-1]["id"])
        return value["observation"]

    def redis_command(self, container, *arguments, input_bytes=None):
        require(container in self.containers and self.containers[container]["image"] == REDIS, "unowned Redis diagnostic")
        return self.run(["docker", "exec", *( ["-i"] if input_bytes is not None else [] ), container,
                         "redis-cli", "--raw", *arguments], input_bytes=input_bytes)

    def postgres_query(self, container, query, retain=True):
        require(container in self.containers and self.containers[container]["image"] == POSTGRES,
                "unowned PostgreSQL diagnostic")
        return self.run(["docker", "exec", "-i", container, "psql", "-U", "postgres", "-d", "eventstore",
                         "-At", "-v", "ON_ERROR_STOP=1", "-f", "-"], input_bytes=query.encode(), retain=retain)

    def postgres_state(self, container):
        data = self.postgres_query(container, "SELECT coalesce(json_agg(json_build_array(key,value) ORDER BY key)::text,'[]') FROM state")
        return {key: postgres_value(value) for key, value in json.loads(data)}

    def postgres_audits(self, container):
        data = self.postgres_query(container, "SELECT coalesce(json_agg(json_build_array(key,value) ORDER BY key)::text,'[]') "
                                   "FROM state WHERE key LIKE 'eventstore||p1r-qualification-audit-%'")
        records = []
        for key, value in json.loads(data):
            try:
                record = json.loads(postgres_value(value))
            except (ValueError, UnicodeError):
                record = None
            records.append({"key": key, "record": {name.lower(): item for name, item in record.items()}
                            if isinstance(record, dict) else {}})
        return records

    def postgres_delete(self, container, key):
        encoded = key.encode().hex()
        self.postgres_query(container, f"DELETE FROM state WHERE key=convert_from(decode('{encoded}','hex'),'UTF8');")

    def postgres_write(self, container, key, value):
        encoded_key = key.encode().hex()
        encoded_value = canonical(value).hex()
        self.postgres_query(container, "UPDATE state SET value=convert_from(decode('" + encoded_value
                            + "','hex'),'UTF8')::jsonb WHERE key=convert_from(decode('" + encoded_key + "','hex'),'UTF8');")

    def raw_state(self, container):
        if self.inputs["operational_profile"]["backend"] == "state.postgresql":
            return self.postgres_state(container)
        values = json.loads(self.redis_command(container, "EVAL", REDIS_DIAGNOSTIC, "0"))
        return {key: data.encode() for key, data in values}

    def inventory(self, container=None):
        container = container or self.redis
        return json.loads(self.run([sys.executable, "-I", ROOT / "tools/p1r-published-executor.py", "inventory",
                                   "--container", container, "--invocation", self.invocation,
                                   "--backend", self.inputs["operational_profile"]["backend"]]))

    def mutate(self, kind, variant=None):
        """Bounded diagnostics modify only the stopped writer's invocation-owned test fixture."""
        require(not self.active, "fixture diagnostic refused while a writer is active")
        values = self.raw_state(self.redis)
        for key, data in values.items():
            if "tenant-a" not in key:
                continue
            value = json.loads(data)
            changed = False
            if key.endswith(":metadata") and kind in ("retained", "invalid-floor"):
                value["retainedFloor"] = 5 if kind == "retained" else 0
                changed = True
            if ":events:" in key:
                sequence = int(key.rsplit(":", 1)[1])
                remove = (kind == "retained" and sequence < 5) or (kind == "missing" and sequence == (7 if variant == "interior" else 12))
                if remove:
                    self.postgres_delete(self.redis, key)
                    continue
                if sequence == 7 and kind in ("unreadable", "protected", "unknown-type", "unknown-version"):
                    if kind == "unreadable":
                        value = "unreadable-fixture"
                    else:
                        field, replacement = {"protected": ("serializationFormat", "json+pdenc-v1"),
                                              "unknown-type": ("eventTypeName", "P1R.UnknownEvent"),
                                              "unknown-version": ("metadataVersion", 987)}[kind]
                        value[field] = replacement
                    changed = True
            if key.endswith(":snapshot") and kind == "uncovered":
                if variant == "absent":
                    self.postgres_delete(self.redis, key)
                    continue
                value["sequenceNumber"], value["state"] = 2, {"count": 2}
                changed = True
            if key.endswith(":snapshot") and kind == "snapshot-nine":
                value["sequenceNumber"] = 9
                state = value["state"]
                count_key = "Count" if "Count" in state else "count"
                state[count_key] = 9
                changed = True
            if changed:
                self.postgres_write(self.redis, key, value)

    def actor(self, version, tenant, expected, kind, checks, prefix):
        return self.probe(version, ["actor", f"http://127.0.0.1:{self.sidecar_port}", tenant, "fixture", str(expected), kind], checks, prefix)

    def seed(self, version, interval, checks):
        self.stop_nodes()
        self.postgres_query(self.redis, "DO $$ BEGIN IF to_regclass('public.state') IS NOT NULL THEN TRUNCATE TABLE state; END IF; END $$;")
        self.start_nodes(version, interval=interval)
        for tenant, count in (("tenant-a", 12), ("tenant-b", 3)):
            result = self.probe(version, ["seed", f"http://127.0.0.1:{self.sidecar_port}", tenant, "fixture", str(count)], checks, tenant + ":")
            checks.check(tenant + ":committed-count", result.get("committed") == count)
        self.stop_nodes()
        if interval == 10:
            self.mutate("snapshot-nine")
        rows = self.inventory()
        checks.check("seed-persisted-fifteen-events", len([row for row in rows if row["kind"] == "event"]) == 15)
        return rows

    def live_case(self, lane, identifier, checks):
        parts = identifier.split("-to-")
        writer, tail = parts
        reader, _, variant = tail.partition("-")
        interval = 10 if lane in ("snapshot-tail", "retained-covered", "metadata-write", "retained-uncovered") else 1000
        self.seed(writer, interval, checks)
        if lane in ("retained-covered", "metadata-write", "retained-uncovered"):
            self.mutate("retained")
        if lane == "retained-uncovered":
            self.mutate("uncovered", variant)
        if lane == "missing-event":
            self.mutate("missing", variant)
        before = self.inventory()
        self.start_nodes(reader, interval=interval)
        negative = lane in ("retained-uncovered", "missing-event")
        result = self.actor(reader, "tenant-a", 12, "AssertCounter", checks, "primary:")
        checks.check("primary-expected-hydration", result.get("accepted") is (not negative))
        checks.check("hydration-no-events", result.get("event_count") == 0)
        checks.check("primary-sequence-twelve", result.get("sequence") == 12)
        second = self.actor(reader, "tenant-b", 3, "AssertCounter", checks, "secondary:")
        checks.check("second-tenant-hydrates", second.get("accepted") is True)
        append, replay, appended = None, None, None
        if lane == "metadata-write":
            append = self.actor(reader, "tenant-a", 12, "IncrementCounter", checks, "append:")
            checks.check("append-effect-accepted", append.get("accepted") is True and append.get("event_count") == 1)
        self.stop_nodes()
        if lane == "metadata-write" and append.get("accepted") is True and append.get("event_count") == 1:
            appended = self.inventory()
            self.start_nodes(reader, interval=interval)
            replay = self.actor(reader, "tenant-a", 13, "AssertCounter", checks, "restart:")
            checks.check("supported-append-restart-rehydrates-thirteen", replay.get("accepted") is True and replay.get("sequence") == 13)
            restarted_second = self.actor(reader, "tenant-b", 3, "AssertCounter", checks, "restart-secondary:")
            checks.check("supported-append-restart-second-tenant-hydrates", restarted_second.get("accepted") is True and restarted_second.get("sequence") == 3)
            self.stop_nodes()
        after = self.inventory()
        before_domain = [row for row in before if row["kind"] != "bookkeeping"]
        after_domain = [row for row in after if row["kind"] != "bookkeeping"]
        prior = {row["key"]: row["sha256"] for row in before if row["kind"] in ("event", "snapshot")}
        observed = {row["key"]: row["sha256"] for row in after}
        checks.check("prior-event-and-snapshot-bytes-preserved", all(observed.get(key) == value for key, value in prior.items()))
        checks.check("second-tenant-state-preserved", runtime.domain(before, "tenant-b") == runtime.domain(after, "tenant-b"))
        if lane == "metadata-write":
            metadata, snapshot, events = runtime.stream(after, "tenant-a")
            checks.check("append-thirteen", metadata is not None and metadata["sequence"] == 13)
            checks.check("retained-floor-five-preserved", metadata is not None and metadata["floor"] == 5)
        else:
            checks.check("hydration-domain-inventory-unchanged", before_domain == after_domain)
        metadata, snapshot, events = runtime.stream(after, "tenant-a")
        expected_head = 13 if append is not None and append.get("accepted") is True and append.get("event_count") == 1 else 12
        expected_sequences = list(range(5 if lane in ("retained-covered", "metadata-write", "retained-uncovered") else 1, expected_head + 1))
        if lane == "missing-event":
            expected_sequences.remove(7 if variant == "interior" else 12)
        expected_snapshot = (None if variant == "absent" else 2) if lane == "retained-uncovered" else 9 if interval == 10 else None
        checks.check("persisted-current-sequence-exact", metadata is not None and metadata["sequence"] == expected_head)
        checks.check("persisted-event-sequence-set-exact", sorted(events) == expected_sequences)
        checks.check("persisted-snapshot-coordinate-exact", (None if snapshot is None else snapshot["sequence"]) == expected_snapshot)
        disposition = "compatible" if all(check["passed"] for check in checks.checks) else "incompatible"
        return {"before": before, "after": after, "primary": result, "secondary": second,
                "append": append, "replay": replay, "appended_inventory": appended,
                "expected_coordinates": {"head": expected_head, "events": expected_sequences, "snapshot": expected_snapshot}},             "refusal" if result.get("accepted") is False else "effect", disposition

    def metadata_case(self, lane, identifier, checks):
        version, style, _, floor = identifier.split("-")
        value = {"CurrentSequence": 12, "LastModified": "2026-01-01T00:00:00Z", "ETag": "fixture-etag"}
        if floor != "None":
            value["RetainedFloor"] = int(floor)
        if style == "web":
            value = {name[0].lower() + name[1:]: item for name, item in value.items()}
        path = self.scratch / (uuid.uuid4().hex + ".json")
        path.write_bytes(canonical(value))
        target = self.scratch / (uuid.uuid4().hex + ".json")
        observed = self.probe(version, ["metadata", str(path), str(target), style], checks)
        expected = 1 if floor == "None" else int(floor)
        checks.check("retained-floor-read", observed.get("floor") == expected)
        return {"input_sha256": digest(regular(path)), "output_sha256": digest(regular(target)), "metadata": observed}, "effect", "compatible" if observed.get("floor") == expected else "incompatible"

    def wire_case(self, lane, identifier, checks):
        fields = identifier.split("-")
        writer, reader = fields[:2]
        format_name = "json" if lane == "projection-wire" else fields[2]
        shape = "dual" if lane == "projection-wire" else fields[3]
        if lane == "query-wire":
            value = {"tenantId": "tenant-a", "domain": "counter", "aggregateId": "fixture", "queryType": "FixtureQuery",
                     "payload": "e30=", "correlationId": "fixture-correlation", "userId": "fixture-user", "entityId": "fixture",
                     "isGlobalAdmin": False, "paging": None}
            if shape == "dual":
                value.update(originalActorId="original-fixture", authenticatedWorkloadId="workload-fixture", isDelegated=True,
                             delegationId="delegation-fixture", scopes=["counter.read"], audience=["audience-fixture"],
                             identityAdmissionProof="bounded-wire-fixture-proof")
        else:
            value = {"sequenceNumber": 12,
                     "globalPosition": 987, "eventTypeName": "P1R.Counter.CounterIncremented", "payload": "e30=",
                     "serializationFormat": "json", "timestamp": "2026-01-01T00:00:00Z", "messageId": "fixture-message",
                     "correlationId": "fixture-correlation", "userId": "fixture-user"}
        original, intermediate, target = [self.scratch / (uuid.uuid4().hex + ".wire") for _ in range(3)]
        original.write_bytes(canonical(value))
        kind = "query" if lane == "query-wire" else "projection"
        first = self.probe(writer, ["wire", kind, "json" if format_name == "json" else "to-xml", str(original), str(intermediate)], checks, "writer:")
        if first.get("handling") != "executed":
            return {"writer": first}, "refusal", "incompatible"
        second = self.probe(reader, ["wire", kind, "json" if format_name == "json" else "xml", str(intermediate), str(target)], checks, "reader:")
        if second.get("handling") != "executed":
            return {"writer": first, "reader": second}, "refusal", "incompatible"
        observed = {name[0].lower() + name[1:]: item for name, item in second["fields"].items()}
        for name, expected in value.items():
            actual = observed.get(name)
            if name == "timestamp" and isinstance(actual, str):
                actual = datetime.datetime.fromisoformat(actual.replace("Z", "+00:00")).isoformat()
                expected = datetime.datetime.fromisoformat(expected.replace("Z", "+00:00")).isoformat()
            checks.check("preserved:" + name, actual == expected)
        return {"writer": first, "reader": second}, "effect", "compatible" if all(row["passed"] for row in checks.checks) else "incompatible"

    def case(self, lane, identifier, action, operational):
        checks, start_index, started = Measurements(), len(self.commands), stamp()
        start_configuration = len(getattr(self, "configurations", []))
        interrupted = False
        expected = "refusal" if lane in ("retained-uncovered", "missing-event", "invalid-evidence") else "effect"
        if lane == "mixed-api" and identifier.endswith(("stale-fence", "unauthorized-effect")):
            expected = "refusal"
        if lane == "reminder-recovery" and identifier == "stale-generation-refusal":
            expected = "refusal"
        if lane == "logical-event-evolution" and identifier == "unknown-version-refusal":
            expected = "refusal"
        if lane == "checkout" and identifier in ("retained-uncovered", "missing-event", "invalid-evidence"):
            expected = "refusal"
        observed, outcome, disposition = {}, "error", "compatible"
        try:
            observed, outcome, disposition = action(checks)
        except (Exception, KeyboardInterrupt) as error:
            checks.check("execution-completed", False)
            observed = {"error": type(error).__name__, "reason": str(error)}
            self.errors.append(lane + "/" + identifier + ": " + str(error))
            if isinstance(error, KeyboardInterrupt):
                interrupted = True
                self.errors.append("interrupted")
        finally:
            if operational:
                try:
                    self.stop_nodes()
                except (Exception, KeyboardInterrupt) as error:
                    checks.check("case-cleanup-completed", False)
                    self.errors.append("case cleanup: " + str(error))
        if len(self.commands) == start_index:
            self.run([sys.executable, "-I", "-c", "import sys; print(sys.argv[1]); sys.exit(2)",
                      canonical({"lane": lane, "case": identifier, "observation": observed}).decode()], check=False)
        inventories = observed if isinstance(observed, dict) and "before" in observed and "after" in observed else None
        before = digest(canonical([row for row in inventories["before"] if row["kind"] != "bookkeeping"])) if inventories else digest(canonical(observed))
        after = digest(canonical([row for row in inventories["after"] if row["kind"] != "bookkeeping"])) if inventories else before
        row = {"id": identifier, "operation": "supported", "expected": expected, "outcome": outcome,
               "disposition": disposition, "inventory": {"before_sha256": before, "after_sha256": after},
               "checks": checks.checks, "assertions": checks.counter}
        if observed.get("unsupported") is True:
            row.update(operation="unsupported", expected="refusal", disposition="incompatible")
        if not hasattr(self, "witness_sources"):
            self.witness_sources = {}
        for witness in checks.witnesses:
            if witness["kind"] == "predicate" and witness["source"] not in self.witness_sources:
                data = regular(Path(witness["source"]))
                require(digest(data) == witness["source_sha256"], "predicate source changed during execution")
                self.witness_sources[witness["source"]] = {"path": witness["source"], "sha256": digest(data), "content": data.decode()}
        evidence = {"id": identifier, "commands": self.commands[start_index:], "observations": observed,
                    "configurations": getattr(self, "configurations", [])[start_configuration:],
                    "check_witnesses": checks.witnesses}
        if interrupted:
            write_json(self.output / "interrupted" / (uuid.uuid4().hex + ".json"),
                       {"lane": lane, "case": row, "execution_evidence": evidence,
                        "predicate_sources": list(self.witness_sources.values())})
            raise KeyboardInterrupt("published invocation interrupted; partial case retained")
        return row, evidence

    def lane(self, name, action, operational=False):
        started = stamp()
        cases, evidence = [], []
        for identifier in qualification.executed_case_inventory(self.inputs)[name]:
            row, observed = self.case(name, identifier, lambda checks: action(identifier, checks), operational)
            cases.append(row)
            evidence.append(observed)
        identities = {"configuration": "Release", "packages": [{"id": package["id"], "sha256": package["archive_sha256"]}
                       for selection in [self.inputs["candidate"], *self.inputs["comparisons"]] for package in selection["packages"]],
                       "loaded_assemblies": list({(row["name"], row["sha256"], row["path"]): row
                                                  for version in VERSIONS for row in self.identities[version]}.values())}
        execution_evidence = {"mechanism": MECHANISM, "executor_source_sha256": self.result["executor_source_sha256"],
                              "executor_source": self.source_binding, "cases": evidence,
                              "predicate_sources": [self.witness_sources[path] for path in sorted({
                                  witness["source"] for case in evidence for witness in case["check_witnesses"] if witness["kind"] == "predicate"})]}
        counter = {key: sum(case["assertions"][key] for case in cases) for key in ("attempted", "passed", "failed")}
        passed = (counter["attempted"] > 0 and counter["failed"] == 0
                  and all(case["expected"] == case["outcome"] and (case["outcome"] != "refusal" or
                          case["inventory"]["before_sha256"] == case["inventory"]["after_sha256"]) for case in cases))
        incompatible = any(case["disposition"] == "incompatible" for case in cases)
        receipt = {"schema": qualification.LANE_RECEIPT_SCHEMA, "id": uuid.uuid4().hex, "lane": name,
                   "scope": "operational" if operational else "published-package", "fixture": self.fixture,
                   "inputs_sha256": self.inputs_sha256, "instrumentation": MECHANISM,
                   "profile": self.inputs["operational_profile"] if operational else None,
                   "argv": self.invocation_argv,
                   "cwd": str(ROOT), "started_utc": started, "finished_utc": stamp(), "exit_code": 0,
                   "output_sha256": digest(canonical(execution_evidence)), "execution_evidence": execution_evidence,
                   "identities": identities, "cases": cases, "assertions": counter,
                   "execution": "passed" if passed else "failed",
                   "compatibility": "incompatible" if incompatible else "compatible" if passed else "unverified"}
        qualification.lane_outcome(receipt)
        path = self.output / "receipts" / (receipt["id"] + ".json")
        write_json(path, receipt)
        self.receipts.append(str(path))
        self.save()
        print(json.dumps({"lane": name, "execution": receipt["execution"], "compatibility": receipt["compatibility"],
                          "assertions": receipt["assertions"]}), flush=True)
        return receipt

    def invalid_case(self, identifier, checks):
        version, _, mutation = identifier.partition("-")
        self.seed(version, 1000, checks)
        self.mutate(mutation)
        before = self.inventory()
        self.start_nodes(version)
        observed = self.actor(version, "tenant-a", 12, "AssertCounter", checks, "primary:")
        checks.check("invalid-evidence-refused", observed.get("accepted") is False)
        checks.check("no-domain-events", observed.get("event_count") == 0)
        self.stop_nodes()
        after = self.inventory()
        checks.check("invalid-evidence-domain-unchanged", [row for row in before if row["kind"] != "bookkeeping"]
                     == [row for row in after if row["kind"] != "bookkeeping"])
        checks.check("second-tenant-preserved", runtime.domain(before, "tenant-b") == runtime.domain(after, "tenant-b"))
        return {"before": before, "after": after, "actor": observed}, "refusal" if observed.get("accepted") is False else "effect", "compatible" if observed.get("accepted") is False else "incompatible"

    def provenance_case(self, identifier, checks):
        role = "candidate" if identifier == CANDIDATE else "comparison-" + identifier
        selection = qualification.role_selection(self.inputs, role)
        root = self.evidence[role]
        observed = self.probe(identifier, ["identity"], checks)
        for package in selection["packages"]:
            lower = package["id"].lower()
            archive = root / "packages" / lower / identifier / (lower + "." + identifier + ".nupkg")
            checks.check(package["id"] + ":archive-sha256", digest(regular(archive)) == package["archive_sha256"])
            self.run(["dotnet", "nuget", "verify", "--all", archive, "--verbosity", "minimal"])
            checks.check(package["id"] + ":signature", qualification.REPOSITORY_SIGNATURE in self.commands[-1]["output"])
        return observed, "effect", "compatible"

    def mixed_case(self, identifier, checks):
        if "-client-" in identifier:
            client, host = identifier.removesuffix("-host").split("-client-")
            self.seed(host, 1000, checks)
            before = self.inventory()
            self.start_nodes(host)
            observed = self.actor(client, "tenant-a", 12, "AssertCounter", checks, "mixed:")
            checks.check("mixed-client-hydrates", observed.get("accepted") is True)
            self.stop_nodes()
            after = self.inventory()
            checks.check("mixed-domain-preserved", [row for row in before if row["kind"] != "bookkeeping"]
                         == [row for row in after if row["kind"] != "bookkeeping"])
            return {"before": before, "after": after, "actor": observed}, "effect", "compatible" if observed.get("accepted") else "incompatible"
        version, _, operation = identifier.partition("-")
        inventory = self.probe(version, ["capabilities"], checks, "capability:")
        method = {"fenced-effect": "ProcessFencedCommandAsync", "stale-fence": "ProcessFencedCommandAsync",
                  "trusted-effect": "ProcessTrustedEffectAsync", "unauthorized-effect": "ProcessTrustedEffectAsync",
                  "retained-floor": "GetRetainedFloorAsync"}.get(operation)
        if method and method not in inventory["actor_methods"]:
            self.seed(version, 1000, checks)
            before = self.inventory()
            checks.check("historical-operation-refused-before-dispatch", True)
            after = self.inventory()
            checks.check("unsupported-operation-domain-preserved", before == after)
            return {"unsupported": True, "method": method, "inventory": inventory,
                    "before": before, "after": after}, "refusal", "incompatible"
        if operation == "cursor-scope":
            observed = self.probe(version, ["cursor-scope"], checks, "cursor:")
            unsupported = observed.get("handling") == "unsupported"
            return {"unsupported": unsupported, "cursor": observed}, "refusal" if unsupported else "effect", "incompatible" if unsupported else "compatible"
        self.seed(version, 1000, checks)
        before = self.inventory()
        self.start_nodes(version)
        if operation == "status":
            observed = []
            for retryable in STATUS_RETRYABILITY:
                label = "null" if retryable is None else str(retryable).lower()
                value = {"status": 2, "timestamp": "2026-01-01T00:00:00Z", "aggregateId": "fixture", "eventCount": 1,
                         "rejectionEventType": None, "failureReason": None, "timeoutDuration": None,
                         "messageId": "fixture-status-" + label, "correlationId": "fixture-correlation", "retryable": retryable,
                         "recoveryReasonCode": "fixture-code", "drainAttemptCount": 3, "domain": "counter", "committedEventSequence": 12}
                url = f"http://127.0.0.1:{self.host_port}/status/tenant-a/" + value["messageId"]
                self.http("POST", url, value)
                actual = self.http("GET", url)
                for name in ("messageId", "correlationId", "retryable", "recoveryReasonCode", "drainAttemptCount", "domain", "committedEventSequence"):
                    checks.check("status:" + label + ":" + name, name in actual and actual[name] == value[name])
                observed.append({"retryability_case": label, "input": value, "readback": actual})
            outcome = "effect"
        elif operation == "retained-floor":
            observed = self.probe(version, ["retained-floor", f"http://127.0.0.1:{self.sidecar_port}"], checks, "floor:")
            checks.check("authoritative-floor-one", observed.get("floor") == 1)
            outcome = "effect"
        else:
            observed = self.http("POST", f"http://127.0.0.1:{self.host_port}/qualification/{operation}", {})
            if operation in ("stale-fence", "unauthorized-effect"):
                checks.check("protected-operation-refused", observed.get("accepted") is False)
                checks.check("protected-refusal-preserves-sequence-twelve", observed.get("sequence") == 12)
                if operation == "stale-fence":
                    checks.check("stale-context-refused", observed.get("stale_refused") is True and observed.get("stale_denial") == "stale-or-invalid-fence")
                    checks.check("forged-proof-refused", observed.get("forged_refused") is True and observed.get("forged_denial") == "stale-or-invalid-fence")
                else:
                    checks.check("unauthorized-proof-refused", observed.get("unauthorized_refused") is True and observed.get("denial") == "invalid-gateway-proof")
                checks.check("execution-completed", observed.get("unexpected") is not True)
                outcome = "error" if observed.get("unexpected") is True else "refusal" if observed.get("accepted") is False else "effect"
            else:
                checks.check("non-null-protected-effect-accepted", observed.get("accepted") is True)
                checks.check("protected-effect-persisted-sequence-thirteen", observed.get("sequence") == 13)
                if operation == "trusted-effect":
                    checks.check("trusted-inbox-replays", observed.get("replayed") is True)
                outcome = "effect"
        self.stop_nodes()
        after = self.inventory()
        checks.check("second-tenant-preserved", runtime.domain(before, "tenant-b") == runtime.domain(after, "tenant-b"))
        audits = None
        if version == CANDIDATE and operation in ("trusted-effect", "unauthorized-effect"):
            audits = self.postgres_audits(self.redis)
            expected = {"action": "submission" if operation == "trusted-effect" else "gateway-proof",
                        "tenant": "tenant-a", "workload": "p1r-fixture", "purpose": "published-qualification",
                        "disposition": "authorized" if operation == "trusted-effect" else "denied"}
            matches = [row for row in audits if all(row["record"].get(name) == value for name, value in expected.items())
                       and (isinstance(row["record"].get("effectid"), str) and bool(row["record"]["effectid"])
                            if operation == "trusted-effect" else row["record"].get("effectid") is None)]
            checks.check("candidate-trusted-effect-audit-persisted" if operation == "trusted-effect"
                         else "candidate-unauthorized-effect-audit-persisted", len(matches) == 1)
        if operation in ("stale-fence", "unauthorized-effect"):
            checks.check("refusal-domain-preserved", [row for row in before if row["kind"] != "bookkeeping"]
                         == [row for row in after if row["kind"] != "bookkeeping"])
        return {"before": before, "after": after, "operation": observed,
                **({"audit_records": audits} if audits is not None else {}),
                "authority_scope": "bounded Test fixture; no production identity or P2 acceptance"}, outcome, "compatible" if all(row["passed"] for row in checks.checks) else "incompatible"

    def recovery_command(self, commands, step, command, database=None, processes=None, input_data=None, output_data=None):
        row = {"id": len(commands) + 1, "step": step, **{key: command[key] for key in
               ("argv", "cwd", "started_utc", "finished_utc", "exit_code", "output_sha256")},
               "database": database, "processes": processes, "input_sha256": digest(input_data) if input_data else None,
               "input_bytes": len(input_data) if input_data else None}
        if output_data is not None:
            row["output_sha256"] = digest(output_data)
        commands.append(row)
        return row["id"]

    def postgres_backup(self, container):
        name = "p1r-" + uuid.uuid4().hex + ".dump"
        self.run(["docker", "exec", container, "pg_dump", "-U", "postgres", "-d", "eventstore", "-Fc", "-f", "/tmp/" + name])
        target = self.scratch / name
        self.run(["docker", "cp", container + ":/tmp/" + name, target])
        return target

    def postgres_restore(self, container, backup):
        self.run(["docker", "cp", backup, container + ":/tmp/p1r-restore.dump"])
        self.run(["docker", "exec", container, "pg_restore", "-U", "postgres", "-d", "eventstore",
                  "--clean", "--if-exists", "--no-owner", "/tmp/p1r-restore.dump"])

    def restore_case(self, identifier, checks):
        self.seed(CANDIDATE, 10, checks)
        self.mutate("retained")
        commands, inventories = [], {}
        source_db = digest(self.redis.encode())
        source = self.inventory()
        index = self.recovery_command(commands, "inventory", self.commands[-1], source_db)
        inventories["source"] = {"command": index, "rows": source, "sha256": digest(canonical(source))}
        backup_path = self.postgres_backup(self.redis)
        backup = regular(backup_path)
        backup_id = self.recovery_command(commands, "backup", self.commands[-1], source_db, output_data=backup)
        restored, port = self.postgres_container("restored")
        restored_db = digest(restored.encode())
        self.recovery_command(commands, "create-database", self.commands[-1], restored_db)
        self.postgres_restore(restored, backup_path)
        self.recovery_command(commands, "restore", self.commands[-1], restored_db, input_data=backup)
        self.postgres_wait(restored)
        copied = self.inventory(restored)
        index = self.recovery_command(commands, "inventory", self.commands[-1], restored_db)
        inventories["restored"] = {"command": index, "rows": copied, "sha256": digest(canonical(copied))}
        checks.check("backup-nonempty", bool(backup))
        checks.check("fresh-restore-full-inventory-identical", source == copied)
        processes = self.start_nodes(CANDIDATE, restored, port, interval=1000)
        self.recovery_command(commands, "start-writer", self.commands[-1], restored_db, processes)
        before = self.actor(CANDIDATE, "tenant-a", 12, "AssertCounter", checks, "restored:")
        checks.check("fresh-restore-hydrates-twelve", before.get("accepted") is True)
        append = self.actor(CANDIDATE, "tenant-a", 12, "IncrementCounter", checks, "append:")
        self.recovery_command(commands, "append", self.commands[-1], restored_db)
        checks.check("fresh-restore-appends-thirteen", append.get("accepted") is True and append.get("event_count") == 1)
        appended = self.inventory(restored)
        index = self.recovery_command(commands, "inventory", self.commands[-1], restored_db)
        inventories["appended"] = {"command": index, "rows": appended, "sha256": digest(canonical(appended))}
        self.stop_nodes()
        self.run([sys.executable, "-I", "-c",
                  "import sys,json;sys.path.insert(0,sys.argv[1]);import p1r_qualification as p;"
                  "values=json.loads(sys.argv[2]);sys.exit(1 if any(p.same_process(v,p.process_state(v['pid'])) for v in values) else 0)",
                  str(ROOT / "tools"), canonical(processes).decode()])
        self.recovery_command(commands, "stop-writer", self.commands[-1], restored_db)
        restarted_processes = self.start_nodes(CANDIDATE, restored, port, interval=1000)
        self.recovery_command(commands, "restart", self.commands[-1], restored_db, restarted_processes)
        replay = self.actor(CANDIDATE, "tenant-a", 13, "AssertCounter", checks, "restart:")
        self.recovery_command(commands, "replay", self.commands[-1], restored_db)
        checks.check("fresh-restart-reconstructs-thirteen", replay.get("accepted") is True)
        second = self.actor(CANDIDATE, "tenant-b", 3, "AssertCounter", checks, "secondary:")
        checks.check("restored-second-tenant-hydrates-three", second.get("accepted") is True)
        restarted = self.inventory(restored)
        index = self.recovery_command(commands, "inventory", self.commands[-1], restored_db)
        inventories["restarted"] = {"command": index, "rows": restarted, "sha256": digest(canonical(restarted))}
        self.stop_nodes()
        metadata, snapshot, events = runtime.stream(restarted, "tenant-a")
        checks.check("restored-floor-five", metadata is not None and metadata["floor"] == 5)
        checks.check("restored-head-thirteen", metadata is not None and metadata["sequence"] == 13)
        checks.check("restored-snapshot-nine", snapshot is not None and snapshot["sequence"] == 9)
        checks.check("restore-second-tenant-unchanged", runtime.domain(source, "tenant-b") == runtime.domain(restarted, "tenant-b"))
        self.pending_restore = {"schema": runtime.RESTORE_SCHEMA, "id": uuid.uuid4().hex, "role": "candidate",
            "scope": "operational", "fixture": self.fixture, "inputs_sha256": self.inputs_sha256,
            "profile": self.inputs["operational_profile"], "packages": {package["id"]: package["archive_sha256"]
                for package in self.inputs["candidate"]["packages"]}, "tenants": {"primary": "tenant-a", "secondary": "tenant-b"},
            "commands": commands, "databases": {"source": source_db, "restored": restored_db},
            "backup": {"command": backup_id, "sha256": digest(backup), "bytes": len(backup)}, "inventories": inventories,
            "observations": {"state_before_append": 12 if before.get("accepted") else None,
                "appended_sequence": 13 if append.get("accepted") else None, "state_after_restart": 13 if replay.get("accepted") else None}}
        return {"before": source, "after": restarted, "backup_sha256": digest(backup), "backup_bytes": len(backup),
                "restored_inventory": copied, "appended_inventory": appended}, "effect", "compatible"

    def containment_case(self, identifier, checks):
        self.seed("3.70.1", 1000, checks)
        before = self.inventory()
        backup = self.postgres_backup(self.redis)
        self.start_nodes(CANDIDATE)
        appended = self.actor(CANDIDATE, "tenant-a", 12, "IncrementCounter", checks, "new-write:")
        checks.check("post-backup-write-committed", appended.get("accepted") is True)
        self.stop_nodes()
        advanced = self.inventory()
        restored, port = self.postgres_container("containment")
        self.postgres_restore(restored, backup)
        self.postgres_wait(restored)
        copied = self.inventory(restored)
        checks.check("containment-backup-restored", before == copied)
        checks.check("later-write-absent-from-pre-upgrade-backup", runtime.domain(advanced, "tenant-a") != runtime.domain(copied, "tenant-a"))
        self.start_nodes("3.70.1", restored, port, interval=1000)
        primary = self.actor("3.70.1", "tenant-a", 12, "AssertCounter", checks, "old-primary:")
        secondary = self.actor("3.70.1", "tenant-b", 3, "AssertCounter", checks, "old-secondary:")
        checks.check("old-host-primary-replays-twelve", primary.get("accepted") is True)
        checks.check("old-host-secondary-replays-three", secondary.get("accepted") is True)
        self.stop_nodes()
        replayed = self.inventory(restored)
        metadata, snapshot, events = runtime.stream(replayed, "tenant-a")
        checks.check("old-host-restored-head-twelve", metadata is not None and metadata["sequence"] == 12)
        checks.check("old-host-original-event-hashes-preserved", runtime.domain(before, "tenant-a") == runtime.domain(replayed, "tenant-a"))
        checks.check("old-host-second-tenant-preserved", runtime.domain(before, "tenant-b") == runtime.domain(replayed, "tenant-b"))
        return {"before": before, "after": replayed, "restored_inventory": copied,
                "old_host_hydration": {"primary": primary, "secondary": secondary}, "advanced": advanced, "containment_only": True,
                "rpo_zero": False, "backup_sha256": digest(regular(backup))}, "effect", "incompatible"

    def natural_reminder(self, checks):
        """Observe a completed scheduler effect through read-only actor sequence calls before injection."""
        deadline, observations = time.monotonic() + 30, []
        started_utc = stamp()
        deadline_utc = (datetime.datetime.fromisoformat(started_utc) + datetime.timedelta(seconds=30)).isoformat()
        completed = False
        while time.monotonic() < deadline:
            observation = self.probe(CANDIDATE, ["sequence", f"http://127.0.0.1:{self.sidecar_port}", "tenant-a", "fixture"],
                                     checks, "natural-scheduler:" + str(len(observations)) + ":",
                                     timeout=max(.001, deadline - time.monotonic()))
            observations.append({"observed_utc": stamp(), "sequence": observation.get("sequence"), "command": self.commands[-1]["id"],
                                 "completed_before_deadline": time.monotonic() <= deadline})
            if observation.get("sequence") == 13 and observations[-1]["completed_before_deadline"]:
                completed = True
                break
            time.sleep(.2)
        checks.check("natural-scheduler-effect-completed-before-manual-callback", completed)
        return {"completed": completed, "observations": observations, "manual_callback_sent_before_observation": False,
                "started_utc": started_utc, "deadline_utc": deadline_utc, "budget_seconds": 30}

    def reminder_case(self, identifier, checks):
        self.seed(CANDIDATE, 1000, checks)
        before = self.inventory()
        due = datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(seconds=12)
        self.reminder_due = due.isoformat()
        self.start_nodes(CANDIDATE)
        registration = self.http("POST", f"http://127.0.0.1:{self.host_port}/reminder/register/1", {})
        checks.check("reminder-armed", registration.get("armed") == 1 and registration.get("unresolved") == 0)
        checks.check("initial-convergence-no-direct-submission", registration.get("submitted") == 0)
        natural = None
        if identifier == "stale-generation-refusal":
            time.sleep(max(0, (due - datetime.datetime.now(datetime.timezone.utc)).total_seconds()) + .2)
            natural = self.natural_reminder(checks)
        else:
            before_restart = self.probe(CANDIDATE, ["sequence", f"http://127.0.0.1:{self.sidecar_port}", "tenant-a", "fixture"],
                                        checks, "before-restart:")
            checks.check("scheduled-effect-not-committed-before-restart", before_restart.get("sequence") == 12)
        self.stop_nodes()
        restart_before = self.inventory()
        revision = 2 if identifier == "stale-generation-refusal" else 1
        if revision == 2:
            self.reminder_due = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(seconds=30)).isoformat()
        self.start_nodes(CANDIDATE)
        rearmed = self.http("POST", f"http://127.0.0.1:{self.host_port}/reminder/register/{revision}", {})
        checks.check("reminder-converges-after-restart", rearmed.get("unresolved") == 0 and rearmed.get("quarantined") == 0)
        checks.check("restart-convergence-no-direct-submission", rearmed.get("submitted") == 0)
        if revision == 1:
            time.sleep(max(0, (due - datetime.datetime.now(datetime.timezone.utc)).total_seconds()) + .2)
            natural = self.natural_reminder(checks)
        repeated = self.http("POST", f"http://127.0.0.1:{self.host_port}/reminder/callback/1", {})
        checks.check("restart-or-stale-callback-no-second-effect", repeated.get("sequence") == 13)
        self.stop_nodes()
        after = self.inventory()
        checks.check("original-domain-event-envelope-preserved", all(row in after for row in before if row["kind"] == "event"))
        checks.check("reminder-second-tenant-preserved", runtime.domain(before, "tenant-b") == runtime.domain(after, "tenant-b"))
        if revision == 2:
            checks.check("stale-generation-domain-preserved", runtime.domain(restart_before, "tenant-a") == runtime.domain(after, "tenant-a"))
        return {"before": restart_before if revision == 2 else before, "after": after,
                "due": self.reminder_due, "registration": registration, "rearmed": rearmed,
                "natural_scheduler_effect": natural, "repeated_or_stale": repeated,
                "fixture_authority": "bounded Test authority; P2 acceptance pending"}, "refusal" if revision == 2 else "effect", "compatible"

    def evolution_case(self, identifier, checks):
        self.seed(CANDIDATE, 1000, checks)
        if identifier == "unknown-version-refusal":
            self.mutate("unknown-version")
        before = self.inventory()
        self.start_nodes(CANDIDATE)
        registration = self.http("GET", f"http://127.0.0.1:{self.domain_port}/ready")["evolution_registration"]
        actor = self.actor(CANDIDATE, "tenant-a", 12, "AssertCounter", checks, "logical:")
        expected = identifier != "unknown-version-refusal"
        checks.check("legacy-clr-hydration-disposition", actor.get("accepted") is expected)
        # A bounded V1 FullName serializer and legacy hydration do not exercise the
        # registered logical alias/evolution reader. Keep the selected gap nonpassing.
        checks.check("bounded-v1-clr-serializer-registration-observed", registration.get("bounded_v1_serializer_registered") is True
                     and registration.get("write_alias") == "P1R.Counter.CounterIncremented")
        checks.check("registered-logical-alias-evolution-executed", registration.get("manifest_registered") is True)
        # Independently read actual application envelopes through the Dapr actor-state API.
        readback = []
        for sequence in range(1, 13):
            actor_id = urllib.parse.quote("tenant-a:counter:fixture", safe="")
            key = urllib.parse.quote("tenant-a:counter:fixture:events:" + str(sequence), safe="")
            value = self.http("GET", f"http://127.0.0.1:{self.sidecar_port}/v1.0/actors/AggregateActor/{actor_id}/state/{key}")
            checks.check("logical-envelope-sequence-" + str(sequence), value.get("sequenceNumber") == sequence)
            readback.append({"sequence": sequence, "sha256": digest(canonical(value))})
        self.stop_nodes()
        after = self.inventory()
        checks.check("logical-readback-original-domain-inventory-preserved", [row for row in before if row["kind"] != "bookkeeping"]
                     == [row for row in after if row["kind"] != "bookkeeping"])
        return {"before": before, "after": after, "dapr_application_readback": readback,
                "coverage": "bounded V1 CLR-name serialization, typed legacy hydration and application-envelope readback",
                "domain_registration_observation": registration,
                "registered_logical_alias_evolution": {"executed": False,
                    "reason": "No authoritative manifest pin or registered logical alias/evolution reader is bound by this fixture; V1 CLR-name replay cannot substitute."}}, \
            "effect" if actor.get("accepted") else "refusal", "incompatible"

    def checkout_case(self, identifier, checks):
        if identifier == "current-build":
            observation = self.probe("source", ["identity"], checks, "source:")
            checks.check("separate-debug-source-paths", all("/Debug/" in row["path"] for row in observation["assemblies"]))
            return {"scope": "source-comparison-only", "identity": observation}, "effect", "compatible"
        lane = identifier
        cases = qualification.executed_case_inventory(self.inputs)[lane]
        if lane == "legacy-metadata":
            selected_case = CANDIDATE + "-pascal-floor-None"
            action = lambda version_case, measurements: self.metadata_case(lane, version_case, measurements)
        elif lane in ("query-wire", "projection-wire"):
            selected_case = next(case for case in cases if CANDIDATE in case)
            action = lambda version_case, measurements: self.wire_case(lane, version_case, measurements)
        elif lane == "invalid-evidence":
            selected_case = CANDIDATE + "-unknown-version"
            action = self.invalid_case
        else:
            selected_case = next(case for case in cases if "-to-" + CANDIDATE in case)
            action = lambda version_case, measurements: self.live_case(lane, version_case, measurements)
        paired = {}
        outcomes = {}
        dispositions = {}
        for version, case in (("package", selected_case), ("source", selected_case.replace(CANDIDATE, "source"))):
            measured = Measurements()
            try:
                observation, outcome, disposition = action(case, measured)
                paired[version], outcomes[version], dispositions[version] = observation, outcome, disposition
            finally:
                for check, witness in zip(measured.checks, measured.witnesses):
                    identity = version + ":" + check["id"]
                    checks.check(identity, check["passed"], dict(witness, id=identity))
        package_semantics = semantic_observation(paired["package"], outcomes["package"])
        source_semantics = semantic_observation(paired["source"], outcomes["source"])
        checks.check("source-package-semantics-equivalent", package_semantics == source_semantics)
        comparison = {"selected_version": CANDIDATE, "selected_case": selected_case,
                      "source_case": selected_case.replace(CANDIDATE, "source"),
                      "scope": "same fixture operations, domain outcomes and persisted coordinates; dynamic identities excluded",
                      "package": package_semantics, "source": source_semantics,
                      "delta": semantic_delta(package_semantics, source_semantics)}
        disposition = ("compatible" if all(check["passed"] for check in checks.checks)
                       and all(value == "compatible" for value in dispositions.values()) else "incompatible")
        return {"scope": "source-comparison-only", "comparison": comparison, "paired_observations": paired}, outcomes["source"], disposition

    def failure_case(self, identifier, checks):
        before_shared = self.shared()
        self.seed(CANDIDATE, 1000, checks)
        before = self.inventory()
        if identifier == "startup-failure":
            environment = dict(os.environ, ASPNETCORE_URLS="invalid-owned-url", ASPNETCORE_ENVIRONMENT="Development")
            root = self.evidence["candidate"]
            item = self.launch(["dotnet", root / "host/bin/Release/net10.0/Host.dll"], environment)
            item["process"].wait(timeout=15)
            checks.check("real-consumer-startup-failed", item["process"].returncode != 0)
        else:
            self.start_nodes(CANDIDATE)
            command = [sys.executable, "-I", "-c", "import sys,urllib.request; urllib.request.urlopen(sys.argv[1],timeout=30).read()",
                       f"http://127.0.0.1:{self.host_port}/qualification-hold"]
            if identifier == "cancellation":
                request = self.launch(command, dict(os.environ))
                time.sleep(.3)
                preparation.signal_owned(request["ownership"].root, signal.SIGINT)
                request["process"].wait(timeout=5)
                checks.check("real-operational-request-cancelled", request["process"].returncode != 0)
            else:
                self.run(command, timeout=.3, check=False)
                checks.check("real-operational-request-timed-out", self.commands[-1]["exit_code"] == 124)
            checks.check("operational-request-reached-published-host", any(b"/qualification-hold" in item["log"].read_bytes()
                         for item in self.active if str(item["argv"][-1]).endswith("Host.dll")))
            checks.check("owned-operational-drill-triggered", bool(self.active))
        identities = [item["ownership"].root for item in self.active]
        self.stop_nodes()
        self.stop_nodes()
        checks.check("drill-owned-processes-absent", all(not preparation.same_process(identity, preparation.process_state(identity["pid"])) for identity in identities))
        checks.check("drill-shared-containers-preserved", before_shared == self.shared())
        after = self.inventory()
        checks.check("drill-domain-inventory-preserved", [row for row in before if row["kind"] != "bookkeeping"]
                     == [row for row in after if row["kind"] != "bookkeeping"])
        return {"before": before, "after": after, "owned_processes": identities}, "effect", "compatible"

    def execute(self):
        validate_execution_inputs(self.inputs)
        try:
            for version in VERSIONS:
                self.build_consumers(version)
            self.topology()
            for lane in qualification.SCENARIOS:
                preparation.check_current_source(self.source_binding)
                if lane == "provenance":
                    action, operational = self.provenance_case, False
                elif lane in ("legacy-metadata", "metadata-read"):
                    action, operational = lambda identifier, checks, lane=lane: self.metadata_case(lane, identifier, checks), False
                elif lane in ("query-wire", "projection-wire"):
                    action, operational = lambda identifier, checks, lane=lane: self.wire_case(lane, identifier, checks), False
                elif lane == "invalid-evidence":
                    action, operational = self.invalid_case, True
                elif lane == "mixed-api":
                    action, operational = self.mixed_case, True
                elif lane == "post-upgrade-restore":
                    action, operational = self.restore_case, True
                elif lane == "pre-upgrade-restore":
                    action, operational = self.containment_case, True
                elif lane == "failure-cleanup":
                    action, operational = self.failure_case, True
                elif lane == "checkout":
                    try:
                        self.build_consumers(CANDIDATE, source=True)
                    except Exception as error:
                        self.errors.append("distinct Debug/source consumer: " + str(error))
                    action, operational = self.checkout_case, True
                else:
                    action, operational = lambda identifier, checks, lane=lane: self.live_case(lane, identifier, checks), True
                self.lane(lane, action, operational)
            if "reminder-recovery" in self.inputs["selected_additions"]:
                preparation.check_current_source(self.source_binding)
                self.lane("reminder-recovery", self.reminder_case, True)
            if "logical-event-evolution" in self.inputs["selected_additions"]:
                preparation.check_current_source(self.source_binding)
                self.lane("logical-event-evolution", self.evolution_case, True)
            preparation.check_current_source(self.source_binding)
        finally:
            cleanup = self.cleanup()
            if self.pending_restore is not None:
                receipt = self.pending_restore
                receipt["cleanup"] = cleanup
                commands = runtime.validate_commands(receipt["commands"])
                rows = {name: value["rows"] for name, value in receipt["inventories"].items()}
                receipt["checks"] = runtime.restore_observations(receipt, commands, rows)
                receipt["assertions"] = checks_counter(receipt["checks"])
                receipt["execution"], receipt["compatibility"] = runtime.disposition("operational", receipt["assertions"]["failed"] == 0)
                runtime.restore_outcome(receipt)
                path = self.output / "receipts" / (receipt["id"] + ".json")
                write_json(path, receipt)
                self.receipts.append(str(path))
            self.save()

    def cleanup(self):
        """Repeated cleanup always visits every owned resource, retaining every failed attempt."""
        attempts = []
        discovery_errors = []
        for name in list(getattr(self, "pending_containers", {})):
            try:
                self.recover_container(name)
            except (Exception, KeyboardInterrupt) as error:
                discovery_errors.append(type(error).__name__)
        label = "hexalith.p1r.invocation=" + self.invocation
        scope = ("tooling-synthetic" if self.fixture["synthetic"] else "operational"
                 if getattr(self, "operational_started", False) else "local-process-control")
        owned = [{"kind": "container", "id": identity, "label": label} for identity in self.containers]
        for name in getattr(self, "pending_containers", {}):
            owned.append({"kind": "container", "id": "container-name:" + name, "label": label})
        for item in self.processes:
            owned.append({"kind": "process", "id": "process:" + str(item["ownership"].root["pid"]) + ":" + str(item["ownership"].root["start_ticks"]), "label": label})
        owned.append({"kind": "scratch", "id": str(self.scratch), "label": label})
        identities = [row["id"] for row in owned]
        for _ in range(2):
            started, errors, remaining = stamp(), list(discovery_errors), []
            for name in list(getattr(self, "pending_containers", {})):
                try:
                    self.recover_container(name)
                except (Exception, KeyboardInterrupt) as error:
                    errors.append(type(error).__name__)
            remaining.extend("container-name:" + name for name in getattr(self, "pending_containers", {}))
            for item in reversed(self.processes):
                try:
                    result = item["ownership"].cleanup() if item["ownership"].remaining() else {"remaining": [], "errors": []}
                    errors.extend(result["errors"])
                    if result["remaining"]:
                        remaining.append("process:" + str(item["ownership"].root["pid"]) + ":" + str(item["ownership"].root["start_ticks"]))
                except (Exception, KeyboardInterrupt) as error:
                    errors.append(type(error).__name__)
            for identity in self.containers:
                try:
                    data = self.run(safe_inspect_argv(identity), cwd=ROOT, check=False)
                    if self.commands[-1]["exit_code"] == 0:
                        row = docker_observations(data)[0]
                        require(row["Id"] == identity and row["Config"]["Labels"].get("hexalith.p1r.invocation") == self.invocation,
                                "container ownership substituted")
                        self.run(["docker", "rm", "-f", identity], cwd=ROOT)
                    else:
                        require(b"No such" in data or b"no such" in data, "container absence is unmeasured")
                except (Exception, KeyboardInterrupt) as error:
                    errors.append(str(error))
                    remaining.append(identity)
            try:
                if self.scratch.exists():
                    shutil.rmtree(self.scratch)
            except (Exception, KeyboardInterrupt) as error:
                errors.append(type(error).__name__)
                remaining.append(str(self.scratch))
            attempts.append({"started_utc": started, "finished_utc": stamp(), "targeted": identities,
                             "removed": [identity for identity in identities if identity not in remaining],
                             "remaining": remaining, "errors": errors})
        shared_after, complete = {}, True
        try:
            # The scratch cwd has been removed; run the read-only discovery from the owning repository.
            names = self.run(["docker", "ps", "-aq"], cwd=ROOT).decode().split()
            rows = docker_observations(self.run(safe_inspect_argv(*names), cwd=ROOT)) if names else []
            shared_after = {row["Id"]: {"image": row["Image"], "running": row["State"]["Running"], "started": row["State"]["StartedAt"]}
                            for row in rows if row["Id"] not in self.containers}
        except (Exception, KeyboardInterrupt):
            complete = False
        shared = {"before": self.shared_before or {}, "after": shared_after, "complete": complete and self.shared_before is not None}
        checks = runtime.checks_from((set(attempts[0]["targeted"]) == set(identities), len(attempts) >= 2,
            all(set(later["targeted"]) >= set(earlier["remaining"]) for earlier, later in zip(attempts, attempts[1:])),
            attempts[-1]["remaining"] == [], all(not attempt["errors"] for attempt in attempts), shared["complete"],
            shared["before"] == shared["after"]), runtime.CLEANUP_CHECKS)
        counter = checks_counter(checks)
        passed = counter["failed"] == 0
        receipt = {"schema": runtime.CLEANUP_SCHEMA if scope != "local-process-control" else "hexalith.p1r.consumer-cleanup.v1",
                   "id": uuid.uuid4().hex, "invocation": self.invocation,
                   "scope": scope, "fixture": self.fixture, "inputs_sha256": self.inputs_sha256,
                   "profile": self.inputs["operational_profile"] if self.inputs else None, "owned": owned,
                   "attempts": attempts, "shared": shared, "checks": checks, "assertions": counter,
                   "execution": runtime.disposition(scope, passed)[0], "compatibility": runtime.disposition(scope, passed)[1]}
        if scope != "local-process-control":
            runtime.cleanup_outcome(receipt)
        path = self.output / "receipts" / (receipt["id"] + ".json")
        write_json(path, receipt)
        if scope != "local-process-control":
            self.receipts.append(str(path))
        else:
            self.result["consumer_cleanup"] = str(path)
        self.result["finished_utc"] = stamp()
        self.save()
        return receipt
