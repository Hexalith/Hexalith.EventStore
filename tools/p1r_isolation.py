"""Narrow P1R command runner with a private Docker daemon and Linux namespaces.

Only the controller talks to the explicitly captured outer Docker endpoint.
The command, Python, .NET, Dapr and provider containers share the private daemon's
network/PID namespace. Tool bytes come from immutable images; nothing is installed
in the repository or published. This wrapper cannot grant package qualification.
"""
from __future__ import annotations

import argparse
import base64
from contextlib import contextmanager
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import uuid

import p1r_qualification as preparation
from p1r_qualification import InvalidPacket, digest, regular, require, stamp

ROOT = preparation.ROOT
# Linux/amd64 manifests, rather than mutable tags or multi-platform selection.
SDK_IMAGE = "mcr.microsoft.com/dotnet/sdk@sha256:0eeb52c76e35a5431ca707ad2bc75e38006a05393045d8532ae44c15d9474523"
DAEMON_IMAGE = "docker@sha256:6316d612722fd7fb2bd7edf9fcbb4439d08915cfb2e0e0b28bf16675fc9f3314"
PYTHON_IMAGE = "python@sha256:2986c55feb36e6cae00fa1fefb454283e4b33f35e75ff8bdd123b134130be301"
SOCKET = "unix:///var/lib/docker/p1r-run/docker.sock"
CLIENT_CONFIG = "/var/lib/docker/p1r-client"
LABEL = "hexalith.p1r.isolation"
MARKER = "HEXALITH_P1R_ISOLATION"
OUTER_MARKER = "HEXALITH_P1R_OUTER_DAEMON_ID"
SCHEMA = "hexalith.p1r.isolation.v1"
STARTUP_SECONDS = 30
PROBE_SECONDS = 2
IMAGE_INSPECT = '{"Id":{{json .Id}},"RepoDigests":{{json .RepoDigests}},"Os":{{json .Os}},"Architecture":{{json .Architecture}}}'
IMAGE_PATTERN = r"[^\s]+@sha256:[0-9a-f]{64}"
INSPECT = ('{"Id":{{json .Id}},"Image":{{json .Image}},"Name":{{json .Name}},'
           '"Labels":{{json .Config.Labels}},"State":{{json .State}},'
           '"NetworkMode":{{json .HostConfig.NetworkMode}},"PidMode":{{json .HostConfig.PidMode}},'
           '"Privileged":{{json .HostConfig.Privileged}},"Ports":{{json .HostConfig.PortBindings}},'
           '"Mounts":{{json .Mounts}}}')


def write_json(path, value):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    preparation.write_json(Path(path), value)


class IsolationInterrupted(KeyboardInterrupt):
    """A handled termination request, retained before any resource teardown."""
    def __init__(self, signum):
        self.signum = signum
        super().__init__(signal.Signals(signum).name)


@contextmanager
def signal_handling(ignore=False):
    def interrupted(signum, frame):
        raise IsolationInterrupted(signum)
    previous = {sig: signal.signal(sig, signal.SIG_IGN if ignore else interrupted)
                for sig in (signal.SIGINT, signal.SIGTERM)}
    try:
        yield
    finally:
        for sig, handler in previous.items():
            signal.signal(sig, handler)


def private_environment(environment=None):
    """Refuse endpoint substitution, then drop every ambient Docker client option."""
    source = dict(os.environ if environment is None else environment)
    require(bool(re.fullmatch(r"[0-9a-f]{32}", source.get(MARKER, ""))), "missing private isolation identity")
    require(source.get("DOCKER_HOST") == SOCKET and source.get("DOCKER_CONFIG") == CLIENT_CONFIG,
            "private Docker endpoint substituted")
    require(not source.get("DOCKER_CONTEXT") and not source.get("CONTAINER_HOST"), "ambient Docker endpoint leaked")
    cleaned = {key: value for key, value in source.items()
               if not key.startswith("DOCKER_") and key not in ("CONTAINER_HOST", "CONTAINER_CONNECTION")}
    cleaned.update(DOCKER_HOST=SOCKET, DOCKER_CONFIG=CLIENT_CONFIG)
    return cleaned


def namespaces(pid="self"):
    return {name: os.readlink(f"/proc/{pid}/ns/{name}") for name in ("net", "pid", "mnt")}


def private_proof(outer_id):
    """Measure the real endpoint and dockerd process before permitting a command."""
    require(isinstance(outer_id, str) and bool(outer_id), "outer daemon identity missing")
    environment = private_environment()
    socket_path = Path(SOCKET.removeprefix("unix://"))
    require(socket_path.is_socket() and not Path("/var/run/docker.sock").exists(), "ambient or absent Docker socket")
    daemon = json.loads(subprocess.check_output(["docker", "info", "--format", "{{json .}}"], env=environment))
    require(daemon.get("ID") and daemon["ID"] != outer_id
            and daemon.get("DockerRootDir") == "/var/lib/docker/data", "private daemon identity substituted")
    pid = int(Path("/var/lib/docker/p1r-run/dockerd.pid").read_text())
    process = preparation.process_state(pid)
    require(process is not None and Path(f"/proc/{pid}/comm").read_text().strip() == "dockerd", "dockerd process unavailable")
    own, daemon_namespaces = namespaces(), namespaces(pid)
    require(all(own[name] == daemon_namespaces[name] for name in ("net", "pid")), "command is outside private daemon namespaces")
    executables = {}
    for name, path in (("python", Path(sys.executable).resolve()), ("docker", Path(shutil.which("docker"))),
                       ("dotnet", Path(shutil.which("dotnet")).resolve())):
        executables[name] = {"path": str(path), "sha256": digest(regular(path))}
    docker_version = json.loads(subprocess.check_output(["docker", "version", "--format", "{{json .}}"], env=environment))
    return {"docker_version": docker_version, "daemon": {key: daemon.get(key) for key in ("ID", "ServerVersion", "DockerRootDir", "Driver", "Name")},
            "outer_daemon_id": outer_id, "socket": SOCKET, "process": process,
            "namespaces": own, "daemon_namespaces": daemon_namespaces, "executables": executables,
            "python_version": list(sys.version_info[:3]),
            "dotnet_version": subprocess.check_output(["dotnet", "--version"], env=environment).decode().strip(),
            "glibc": os.confstr("CS_GNU_LIBC_VERSION")}


def inner(output, outer_id, command):
    with signal_handling():
        return _inner(output, outer_id, command)


def _inner(output, outer_id, command):
    """Retain command failures and repeated exact-process cleanup in the private namespace."""
    output = Path(output)
    result = {"schema": SCHEMA, "scope": "isolated-command-verification", "invocation": os.environ.get(MARKER),
              "started_utc": stamp(), "command": command, "exit_code": None, "errors": [], "p1r_usable": False}
    result["supervisor"] = preparation.identity(preparation.process_state(os.getpid()))
    write_json(output / "private-execution.json", result)
    owner, process = preparation.OwnedProcesses(), None
    preparation.subreaper()
    try:
        result["proof"] = private_proof(outer_id)
        original_git = preparation.git
        def diagnosed_git(root, *arguments):
            try:
                return original_git(root, *arguments)
            except InvalidPacket:
                probe = subprocess.run(["git", "--no-pager", *arguments], cwd=root,
                                       stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20)
                result["git_failure"] = {"cwd": str(root), "arguments": arguments, "exit_code": probe.returncode,
                                         "stderr": probe.stderr.decode("utf-8", "replace")}
                raise
        preparation.git = diagnosed_git
        try:
            result["source_binding"] = preparation.source_binding()
        finally:
            preparation.git = original_git
        write_json(output / "private-execution.json", result)
        environment = dict(private_environment(), HEXALITH_P1R_CONTROL_NONCE=owner.nonce,
                           **{OUTER_MARKER: outer_id})
        process = subprocess.Popen(command, env=environment, start_new_session=True)
        owner.attach(process)
        result["root"] = owner.root
        write_json(output / "private-execution.json", result)
        while process.poll() is None:
            owner.discover()
            time.sleep(.1)
        result["root"] = owner.root
        result["exit_code"] = process.returncode
        preparation.check_current_source(result["source_binding"])
    except (Exception, KeyboardInterrupt) as error:
        result["errors"].append(str(error) if isinstance(error, InvalidPacket) else type(error).__name__)
        result["exit_code"] = 130 if isinstance(error, KeyboardInterrupt) else 2
        if isinstance(error, KeyboardInterrupt):
            result["interrupted_signal"] = getattr(error, "signum", signal.SIGINT)
    finally:
        with signal_handling(ignore=True):
            if "interrupted_signal" in result and process is not None and process.poll() is None:
                # Give the exact child its normal Python interruption path so
                # fixture/container cleanup can finish before process teardown.
                preparation.signal_owned(owner.root, signal.SIGINT)
                deadline = time.monotonic() + 40
                while process.poll() is None and time.monotonic() < deadline:
                    owner.discover()
                    time.sleep(.1)
                result["orderly_command_exit"] = process.poll()
            result["cleanup"] = []
            for index in range(2):
                started = stamp()
                attempt = owner.cleanup()
                result["cleanup"].append(dict(attempt, id=index+1, started_utc=started, finished_utc=stamp(),
                                              owned_processes=[dict(item) for item in owner.owned.values()]))
            result["owned_processes"] = list(owner.owned.values())
            if "source_binding" in result:
                try:
                    preparation.check_current_source(result["source_binding"])
                    result["source_checked_after_cleanup"] = True
                except (Exception, KeyboardInterrupt) as error:
                    result["errors"].append(str(error) if isinstance(error, InvalidPacket) else type(error).__name__)
            result["finished_utc"] = stamp()
            result["passed"] = result["exit_code"] == 0 and not result["errors"] and all(
                not row["errors"] and not row["remaining"] for row in result["cleanup"])
            write_json(output / "private-execution.json", result)
    return 0 if result["passed"] else 2


def interrupt_supervisor(output, invocation):
    """Signal only the registered supervisor, checked again against live procfs."""
    result = json.loads(regular(Path(output) / "private-execution.json"))
    require(result["invocation"] == invocation == os.environ.get(MARKER), "supervisor invocation substituted")
    owner = result["supervisor"]
    process = preparation.process_state(owner["pid"])
    require(preparation.same_process(owner, process), "supervisor process identity unavailable")
    argv = Path(f"/proc/{owner['pid']}/cmdline").read_bytes().split(b"\0")
    require(str(ROOT / "tools/p1r-isolated-runner.py").encode() in argv and b"_inner" in argv
            and str(Path(output)).encode() in argv, "supervisor command substituted")
    environment = Path(f"/proc/{owner['pid']}/environ").read_bytes().split(b"\0")
    require((MARKER + "=" + invocation).encode() in environment, "supervisor ownership substituted")
    preparation.signal_owned(owner, signal.SIGINT)
    return 0


def executor_input(command, repository):
    """Bind the complete published executor grammar and exact physical input bytes."""
    command = list(map(str, command))
    require(len(command) == 7 and command[:3] == ["python3", str(Path(repository)/"tools/p1r-published-executor.py"), "run"]
            and command[3] == "--out" and command[5] == "--inputs", "published executor arguments substituted")
    output, path = Path(command[4]), Path(command[6])
    require(output.is_absolute() and output == output.resolve() and path.is_absolute(), "published executor paths substituted")
    return {"path":str(path), "sha256":digest(regular(path))}


def supported_workspace_path(value):
    """Accept only unambiguous non-root POSIX directory names in bind arguments."""
    value = str(value)
    parts = value.split('/')[1:]
    return (value.startswith('/') and all(part and part not in ('.', '..') for part in parts)
            and not any(ord(character) < 32 or ord(character) == 127 or character == ',' for character in value)
            and not parts[-1].endswith(('.sock', '.socket')))


def validate_workspace(workspace, repository, outer_host):
    """Refuse unsupported mounts and exposure of the controller socket before Docker work."""
    require(supported_workspace_path(workspace), "unsupported workspace mount")
    workspace = Path(workspace)
    require(workspace.is_dir() and workspace == workspace.resolve() and repository.is_relative_to(workspace),
            "workspace mount substituted")
    if workspace != repository:
        parent = preparation.git(repository, "rev-parse", "--show-superproject-working-tree").decode().strip()
        require(parent and workspace == Path(parent), "unsupported workspace mount; select the repository or its superproject")
    socket = Path(outer_host.removeprefix('unix://'))
    require(not socket.is_relative_to(workspace) and not socket.resolve().is_relative_to(workspace),
            "workspace exposes the outer Docker socket")
    # Bind mounts include descendant mounts. Keep this profile to a single filesystem
    # tree rather than exposing an unreviewed nested mount from the controller.
    for line in Path('/proc/self/mountinfo').read_text().splitlines():
        mounted = Path(re.sub(r'\\([0-7]{3})', lambda match: chr(int(match[1], 8)), line.split()[4]))
        require(mounted == workspace or not mounted.is_relative_to(workspace), "unsupported nested workspace mount")
    if socket.exists():
        selected = socket.stat()
        def traversal_error(error):
            raise InvalidPacket('workspace contents unavailable for outer socket isolation') from error
        for directory, _, files in os.walk(workspace, onerror=traversal_error):
            for name in files:
                candidate = Path(directory) / name
                # A symlink pointing outside this sole bind is not exposed there.
                if candidate.is_symlink():
                    continue
                observed = candidate.stat()
                require((observed.st_dev, observed.st_ino) != (selected.st_dev, selected.st_ino),
                        "workspace exposes an alias of the outer Docker socket")
    return workspace


def sdk_arguments(workspace, repository, invocation, daemon, volume):
    """Construct the single permitted SDK container profile."""
    git_paths = [Path(workspace), *(Path(workspace)/name for name in preparation.declared_dependencies(Path(workspace)))]
    git_options = ["--env", "GIT_CONFIG_COUNT=" + str(len(git_paths))]
    for index, path in enumerate(git_paths):
        git_options += ["--env", f"GIT_CONFIG_KEY_{index}=safe.directory", "--env", f"GIT_CONFIG_VALUE_{index}={path}"]
    return ["--network", "container:"+daemon, "--pid", "container:"+daemon, "--cap-add", "SYS_PTRACE",
            "--mount", "type=volume,src="+volume+",dst=/var/lib/docker",
            "--mount", "type=bind,src="+str(workspace)+",dst="+str(workspace),
            "--workdir", str(repository), "--env", MARKER+"="+invocation,
            "--env", "DOCKER_HOST="+SOCKET, "--env", "DOCKER_CONFIG="+CLIENT_CONFIG, *git_options,
            "--env", "PATH=/opt/python/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
            "--env", "LD_LIBRARY_PATH=/opt/python/lib", "--env", "DOTNET_CLI_TELEMETRY_OPTOUT=1", "--entrypoint", "sleep"]


class IsolatedRunner:
    """Own only exact outer container/volume identities carrying this invocation label."""

    def __init__(self, output, workspace=None, outer_host=None):
        with signal_handling():
            self.initialize(output, workspace, outer_host)

    def initialize(self, output, workspace=None, outer_host=None):
        self.output = Path(output).absolute()
        require(not self.output.exists() and self.output == self.output.resolve(), "isolation output exists or is substituted")
        self.invocation = uuid.uuid4().hex
        self.scratch = None
        self.environment = {key: value for key, value in os.environ.items()
                            if not key.startswith("DOCKER_") and key not in ("CONTAINER_HOST", "CONTAINER_CONNECTION")}
        self.containers, self.pending, self.volume, self.volume_created = {}, {}, "p1r-isolation-" + self.invocation, False
        self.result = {"schema": SCHEMA, "invocation": self.invocation, "scope": "isolated-command-verification",
                       "started_utc": stamp(), "images": {"sdk": SDK_IMAGE, "daemon": DAEMON_IMAGE, "python": PYTHON_IMAGE},
                       "outer_host": None, "commands": [], "errors": [], "cleanup": [], "p1r_usable": False}
        try:
            self.output.mkdir(parents=True)
            self.scratch = Path(tempfile.mkdtemp(prefix="p1r-isolation-" + self.invocation + "-"))
            self.config = self.scratch / "docker-config"
            self.config.mkdir()
            self.save()
            # Resolve the outer endpoint exactly once. Its context/config is never copied inside.
            self.outer_host = outer_host or subprocess.check_output(
                ["docker", "context", "inspect", "--format", "{{.Endpoints.docker.Host}}"], env=os.environ).decode().strip()
            # Refuse remote/TLS/SSH contexts before provisioning. Never retain endpoint
            # credentials from a rejected URI or copy ambient client configuration.
            require(bool(re.fullmatch(r"unix:///[^\s?#]+", self.outer_host)),
                    "only a local Unix Docker endpoint is supported")
            selected_workspace = workspace or preparation.git(ROOT, "rev-parse", "--show-superproject-working-tree").decode().strip() or ROOT
            self.workspace = validate_workspace(selected_workspace, ROOT, self.outer_host)
            require(self.output.is_relative_to(self.workspace), "isolation output is outside the mounted workspace; private evidence would be lost")
            self.result["outer_host"] = self.outer_host
            self.save()
        except (Exception, KeyboardInterrupt) as error:
            self.result["errors"].append(str(error) if isinstance(error, InvalidPacket) else type(error).__name__)
            self.result["command_exit_code"] = 130 if isinstance(error, KeyboardInterrupt) else 2
            self.cleanup()
            raise

    def save(self):
        self.result["owned_containers"] = self.containers
        self.result["pending_containers"] = self.pending
        self.result["owned_volume"] = self.volume if self.volume_created else None
        self.result["owned_scratch"] = str(self.scratch) if self.scratch is not None else None
        write_json(self.output / "isolation.json", self.result)

    def docker(self, arguments, check=True, timeout=300):
        argv = ["docker", "--host", self.outer_host, "--config", str(self.config), *map(str, arguments)]
        started = stamp()
        interrupted = None
        try:
            completed = subprocess.run(argv, env=self.environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout)
            code, data = completed.returncode, completed.stdout
        except subprocess.TimeoutExpired as error:
            code, data = 124, error.stdout or b""
        except OSError as error:
            code, data = 127, str(error).encode()
        except KeyboardInterrupt as error:
            code, data, interrupted = 130, b"outer command interrupted", error
        row = {"id": len(self.result["commands"])+1, "argv": argv, "started_utc": started, "finished_utc": stamp(),
               "exit_code": code, "output": data.decode("utf-8", "replace")}
        row["output_sha256"] = digest(row["output"].encode())
        try:
            data.decode("utf-8")
        except UnicodeDecodeError:
            self.result["errors"].append("outer Docker output is not UTF-8")
            row.update(raw_output_base64=base64.b64encode(data).decode(), raw_output_sha256=digest(data))
        self.result["commands"].append(row)
        self.save()
        if interrupted is not None:
            raise interrupted
        require(not check or code == 0, "outer Docker command failed: " + str(arguments[0]) + "; exit=" + str(code))
        return code, data

    def inspect(self, identity):
        code, data = self.docker(["inspect", "--format", INSPECT, identity], check=False)
        if code:
            require(b"No such" in data or b"no such" in data, "outer container absence is unmeasured")
            return None
        return json.loads(data)

    def create(self, role, image, arguments, command=()):
        require(bool(re.fullmatch(IMAGE_PATTERN, image)), "runner image is not digest pinned")
        name = "p1r-isolation-" + self.invocation + "-" + role
        cid = self.scratch / (role + ".cid")
        self.pending[name] = {"role": role, "image": image}
        self.save()
        try:
            self.docker(["create", "--cidfile", cid, "--name", name, "--label", LABEL + "=" + self.invocation,
                         *arguments, image, *command])
        finally:
            row = self.inspect(cid.read_text().strip() if cid.is_file() else name)
            if row is not None:
                require(row["Name"] == "/" + name and row["Labels"].get(LABEL) == self.invocation,
                        "created outer container ownership substituted")
                measured = self.result.get("image_measurements", {}).get("python" if role == "python-tools" else role)
                require(measured is None or row.get("Image") == measured["Id"], "created container image ID substituted")
                self.containers[row["Id"]] = self.pending.pop(name)
                self.save()
        require(row is not None, "created outer container identity unavailable")
        return row["Id"]

    def run(self, command):
        with signal_handling():
            return self._run(command)

    def _run(self, command):
        """Execute an explicit command; preserve failure even if later cleanup succeeds."""
        code = 2
        try:
            _, data = self.docker(["info", "--format", "{{.ID}}"])
            outer_id = data.decode().strip()
            require(outer_id, "outer daemon identity missing")
            self.result["outer_daemon_id"] = outer_id
            executable = Path(shutil.which("docker")).resolve()
            _, version = self.docker(["version", "--format", "{{json .}}"])
            self.result["outer_toolchain"] = {"docker": {"path":str(executable), "sha256":digest(regular(executable))},
                                               "version":json.loads(version)}
            self.result["image_measurements"] = {}
            for role, image in (("sdk", SDK_IMAGE), ("daemon", DAEMON_IMAGE), ("python", PYTHON_IMAGE)):
                self.docker(["pull", "--platform", "linux/amd64", image], timeout=600)
                _, data = self.docker(["image", "inspect", "--format", IMAGE_INSPECT, image])
                measured = json.loads(data)
                require(measured.get("Os") == "linux" and measured.get("Architecture") == "amd64"
                        and image in measured.get("RepoDigests", []) and re.fullmatch(r"sha256:[0-9a-f]{64}", measured.get("Id", "")),
                        "pinned image platform, digest or image ID differs")
                self.result["image_measurements"][role] = measured
                self.save()
            # Register the pending volume before creation, including interrupted creation.
            self.volume_created = True
            self.save()
            self.docker(["volume", "create", "--label", LABEL + "=" + self.invocation, self.volume])
            daemon = self.create("daemon", DAEMON_IMAGE,
                ["--privileged", "--network", "bridge", "--mount", "type=volume,src=" + self.volume + ",dst=/var/lib/docker", "--entrypoint", "dockerd"],
                ["--host=" + SOCKET, "--data-root=/var/lib/docker/data", "--exec-root=/var/lib/docker/p1r-exec",
                 "--pidfile=/var/lib/docker/p1r-run/dockerd.pid", "--storage-driver=vfs", "--tls=false"])
            deadline = time.monotonic() + STARTUP_SECONDS
            self.docker(["start", daemon], timeout=STARTUP_SECONDS)
            while True:
                remaining = deadline - time.monotonic()
                require(remaining > 0, "private daemon startup timeout")
                status, _ = self.docker(["exec", daemon, "docker", "--host", SOCKET, "info", "--format", "{{.ID}}"],
                                        check=False, timeout=min(PROBE_SECONDS, remaining))
                require(time.monotonic() <= deadline, "private daemon startup timeout")
                if status == 0:
                    break
                remaining = deadline - time.monotonic()
                require(remaining > 0, "private daemon startup timeout")
                time.sleep(min(.25, remaining))
            python = self.create("python-tools", PYTHON_IMAGE, ["--network", "none", "--entrypoint", "/bin/true"])
            self.docker(["cp", python + ":/usr/local", self.scratch / "python"])
            self.docker(["cp", daemon + ":/usr/local/bin/docker", self.scratch / "docker"])
            runner = self.create("sdk", SDK_IMAGE,
                sdk_arguments(self.workspace, ROOT, self.invocation, daemon, self.volume), ["infinity"])
            self.docker(["cp", self.scratch / "python", runner + ":/opt/python"])
            self.docker(["cp", self.scratch / "docker", runner + ":/usr/local/bin/docker"])
            self.docker(["start", runner])
            observed = self.inspect(runner)
            require(observed["NetworkMode"] == "container:" + daemon and observed["PidMode"] == "container:" + daemon
                    and not observed["Ports"] and not observed["Privileged"]
                    and {row["Destination"] for row in observed["Mounts"]} == {"/var/lib/docker", str(self.workspace)},
                    "runner namespace or mounts substituted")
            self.result["runner_proof"] = observed
            _, measured = self.docker(["exec", runner, "python3", ROOT / "tools/p1r-isolated-runner.py", "_proof", "--outer-id", outer_id])
            self.result["toolchain_proof"] = json.loads(measured)
            if len(command) >= 3 and str(command[1]) == str(ROOT/"tools/p1r-published-executor.py") and command[2] == "run":
                self.result["selected_input"] = executor_input(command, ROOT)
            self.save()
            self.result.update(command_started=True, inner_command=list(map(str, command)), sdk=runner, daemon=daemon)
            self.save()
            code, stdout = self.docker(["exec", runner, "python3", ROOT / "tools/p1r-isolated-runner.py", "_inner",
                                   "--out", self.output, "--outer-id", outer_id, "--", *command], check=False, timeout=86400)
            self.result["command_stdout"] = stdout.decode("utf-8", "replace")
            require((self.output / "private-execution.json").is_file(), "private execution result missing")
            private = json.loads(regular(self.output / "private-execution.json"))
            require(private.get("passed") is True, "private execution verification failed")
        except (Exception, KeyboardInterrupt) as error:
            code = 130 if isinstance(error, KeyboardInterrupt) else 2
            self.result["errors"].append(str(error) if isinstance(error, InvalidPacket) else type(error).__name__)
            if isinstance(error, KeyboardInterrupt):
                self.result["interrupted_signal"] = getattr(error, "signum", signal.SIGINT)
        finally:
            self.result["command_exit_code"] = code
            if self.result.get("command_started") and not self.private_finished():
                self.orderly_interrupt()
            self.cleanup()
        if self.result["passed"] and len(command) >= 3 and str(command[1]) == str(ROOT/"tools/p1r-published-executor.py") and command[2] == "run":
            try:
                import p1r_published_qualification as qualification
                selected = executor_input(command, ROOT)
                require(selected == self.result["selected_input"], "selected input changed during execution")
                selection, _, scope = qualification.load_document(Path(selected["path"]), qualification.validate_inputs)
                qualification.finalize_isolation_receipts(Path(command[command.index("--out")+1]),self.output,selection)
            except (Exception, KeyboardInterrupt) as error:
                self.result["errors"].append(str(error))
                self.result["passed"] = False
                self.save()
        return 0 if self.result["passed"] else 2

    def private_finished(self):
        try:
            value = json.loads(regular(self.output / "private-execution.json"))
            return (value["invocation"] == self.invocation and value.get("finished_utc")
                    and len(value.get("cleanup", [])) >= 2)
        except (OSError, ValueError, KeyError):
            return False

    def orderly_interrupt(self):
        """Wait for final supervisor/process receipts before considering SDK removal."""
        with signal_handling(ignore=True):
            try:
                requested = False
                if not self.private_finished():
                    code, _ = self.docker(["exec", self.result["sdk"], "python3", ROOT / "tools/p1r-isolated-runner.py",
                                          "_interrupt", "--out", self.output, "--invocation", self.invocation], check=False, timeout=10)
                    requested = code == 0
                deadline = time.monotonic() + 45
                while not self.private_finished() and time.monotonic() < deadline:
                    if not requested and (self.output / "private-execution.json").is_file():
                        code, _ = self.docker(["exec", self.result["sdk"], "python3", ROOT / "tools/p1r-isolated-runner.py",
                                              "_interrupt", "--out", self.output, "--invocation", self.invocation], check=False, timeout=5)
                        requested = code == 0
                    time.sleep(.1)
                require(self.private_finished(), "inner supervisor final cleanup is unavailable; resources preserved")
            except (Exception, KeyboardInterrupt) as error:
                self.result["errors"].append(str(error))
            self.save()

    def nested_inventory(self):
        """Observe the whole private daemon; no label grants removal of leftovers."""
        if not self.result.get("command_started"):
            return True, None
        daemon = self.result["daemon"]
        observed = self.inspect(daemon)
        if observed is None:
            require(self.result.get("nested_empty_before_teardown"), "private daemon absence has no prior empty inventory proof")
            return True, {"daemon_absent": True, "prior_empty_inventory": True}
        require(observed["Id"] == daemon and observed["Labels"].get(LABEL) == self.invocation,
                "private daemon ownership substituted")
        code, data = self.docker(["exec", daemon, "docker", "--host", SOCKET, "ps", "-aq", "--no-trunc"], check=False)
        require(code == 0, "private container inventory unavailable; daemon and volume preserved")
        identities = data.decode().split()
        observation = {"daemon": daemon, "containers": identities, "complete": True}
        if not identities:
            self.result["nested_empty_before_teardown"] = True
        return not identities, observation

    def cleanup(self):
        """Retry every exact owned identity, and never select targets by a broad filter."""
        with signal_handling(ignore=True):
            for _ in range(2):
                attempt = {"id": len(self.result["cleanup"])+1, "started_utc": stamp(), "targeted": [], "remaining": [], "errors": [],
                           "absent_containers": [], "absent_volumes": [], "absent_scratch": []}
                for name in list(self.pending):
                    try:
                        row = self.inspect(name)
                        if row is not None:
                            require(row["Name"] == "/" + name and row["Labels"].get(LABEL) == self.invocation, "pending container is unowned")
                            self.containers[row["Id"]] = self.pending.pop(name)
                        else:
                            attempt["remaining"].append(name)
                            attempt["errors"].append("pending container creation remains unresolved: " + name)
                    except (Exception, KeyboardInterrupt) as error:
                        attempt["remaining"].append(name)
                        attempt["errors"].append(str(error))
                nested_safe = True
                try:
                    nested_safe, observation = self.nested_inventory()
                    attempt["nested_inventory"] = observation
                    require(nested_safe, "unexpected private containers remain; daemon and volume preserved")
                except (Exception, KeyboardInterrupt) as error:
                    nested_safe = False
                    attempt["errors"].append(str(error))
                if self.pending:
                    nested_safe = False
                supervisor_finished = not self.result.get("command_started") or self.private_finished()
                if not supervisor_finished:
                    attempt["errors"].append("inner supervisor cleanup not final; SDK, daemon and volume preserved")
                    nested_safe = False
                for identity in reversed(self.containers):
                    attempt["targeted"].append(identity)
                    if (self.containers[identity]["role"] == "daemon" and not nested_safe
                            or self.containers[identity]["role"] == "sdk" and not supervisor_finished):
                        attempt["remaining"].append(identity)
                        continue
                    try:
                        row = self.inspect(identity)
                        if row is not None:
                            require(row["Id"] == identity and row["Labels"].get(LABEL) == self.invocation,
                                    "outer cleanup ownership substituted")
                            if self.containers[identity]["role"] == "daemon":
                                self.docker(["logs", "--tail", "200", identity], check=False)
                            self.docker(["rm", "-f", identity])
                        require(self.inspect(identity) is None and self.inspect(identity) is None,
                                "outer container removal lacks repeated observed absence")
                        attempt["absent_containers"].append(identity)
                    except (Exception, KeyboardInterrupt) as error:
                        attempt["remaining"].append(identity)
                        attempt["errors"].append(str(error))
                if self.volume_created:
                    attempt["targeted"].append("volume:" + self.volume)
                    try:
                        require(nested_safe and not attempt["remaining"], "private volume preserved with unresolved containers")
                        code, data = self.docker(["volume", "inspect", "--format", "{{json .}}", self.volume], check=False)
                        if code == 0:
                            volume = json.loads(data)
                            require(volume["Name"] == self.volume and volume["Labels"].get(LABEL) == self.invocation, "outer volume is unowned")
                            self.docker(["volume", "rm", self.volume])
                        else:
                            require(b"no such volume" in data.lower(), "outer volume absence is unmeasured")
                        for _ in range(2):
                            status, observation = self.docker(["volume", "inspect", "--format", "{{json .}}", self.volume], check=False)
                            require(status != 0 and b"no such volume" in observation.lower(),
                                    "outer volume removal lacks repeated observed absence")
                        attempt["absent_volumes"].append(self.volume)
                    except (Exception, KeyboardInterrupt) as error:
                        attempt["remaining"].append("volume:" + self.volume)
                        attempt["errors"].append(str(error))
                attempt["targeted"].append("scratch:" + str(self.scratch))
                try:
                    require(not self.pending, "scratch retained for unresolved container creation")
                    if self.scratch is not None and self.scratch.exists():
                        shutil.rmtree(self.scratch)
                    require(self.scratch is None or not self.scratch.exists(), "scratch removal did not establish absence")
                    if self.scratch is not None:
                        attempt["absent_scratch"].append(str(self.scratch))
                except (Exception, KeyboardInterrupt) as error:
                    attempt["remaining"].append("scratch:" + str(self.scratch))
                    attempt["errors"].append(type(error).__name__)
                attempt["finished_utc"] = stamp()
                self.result["cleanup"].append(attempt)
                self.save()
            self.result["finished_utc"] = stamp()
            private_path = self.output / "private-execution.json"
            if private_path.is_file():
                self.result["private_execution"] = {"path":str(private_path),"sha256":digest(regular(private_path))}
            self.result["passed"] = self.result.get("command_exit_code") == 0 and not self.result["errors"] and all(
                not row["remaining"] and not row["errors"] for row in self.result["cleanup"])
            self.save()


def main(arguments=None):
    arguments = list(sys.argv[1:] if arguments is None else arguments)
    if arguments[:1] == ["_proof"]:
        parser = argparse.ArgumentParser()
        parser.add_argument("--outer-id", required=True)
        args = parser.parse_args(arguments[1:])
        print(json.dumps(private_proof(args.outer_id), sort_keys=True), flush=True)
        return 0
    if arguments[:1] == ["_interrupt"]:
        parser = argparse.ArgumentParser()
        parser.add_argument("--out", type=Path, required=True)
        parser.add_argument("--invocation", required=True)
        args = parser.parse_args(arguments[1:])
        return interrupt_supervisor(args.out, args.invocation)
    internal = arguments[:1] == ["_inner"]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    if internal:
        parser.add_argument("--outer-id", required=True)
        arguments = arguments[1:]
    else:
        parser.add_argument("--outer-host")
        parser.add_argument("--workspace")
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args(arguments)
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    require(command, "explicit verification command required")
    if internal:
        return inner(args.out, args.outer_id, command)
    runner = IsolatedRunner(args.out, args.workspace, args.outer_host)
    code = runner.run(command)
    print(json.dumps({"out": str(runner.output), "passed": runner.result["passed"], "p1r_usable": False}), flush=True)
    return code
