"""Explicit runtime selection and exclusive disposable-worker custody (stdlib only)."""
from __future__ import annotations

import json
import os
from pathlib import Path
import socket
import stat

LEGACY_PROFILE = 'deploy/dapr/qualification/runtime-lock.json'
CANDIDATE_PROFILE = 'deploy/dapr/qualification/runtime-lock-1.19.1.json'
LEGACY_INPUTS = 'deploy/dapr/qualification/public-identities/1.18.7-original-inputs'
REDIS_IMAGE = 'redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf'
APPROVED = {
    ('1.18.7', '1.18.2', '1.18.6'): {
        ('dapr/dapr', 'v1.18.7', 'b32a815fa2260983e181b95272ea555fe17d3895'),
        ('dapr/components-contrib', 'v1.18.6', 'eac9e31dd14cd798c9996aae30818b572aa12c59'),
    },
    ('1.19.1', '1.19.1', '1.19.0'): {
        ('dapr/dapr', 'v1.19.1', 'cfb5ee090dda7cfc26145c7703605d831350aa2a'),
        ('dapr/components-contrib', 'v1.19.0', '1d331c389f11baaa1d664022b6b6c2f59811ce11'),
        ('dapr/cli', 'v1.19.1', 'e6263a6e5e6aa63b73ae24ea76a685d074c21d96'),
    },
}


def profile_path(profile: dict) -> str:
    """Retain original packet paths for the original tuple."""
    return LEGACY_PROFILE if profile['runtime']['version'] == '1.18.7' else CANDIDATE_PROFILE


def source_input(profile: dict, root: Path, name: str, expected: str) -> Path:
    """Resolve old input bytes only from the owning checkout, never from a packet."""
    from dapr_broker_qualification import digest, safe_file
    path = safe_file(root, name)
    if digest(path) == expected:
        return path
    if profile_path(profile) == LEGACY_PROFILE and root.resolve() == Path(__file__).resolve().parents[1]:
        retained = safe_file(root, LEGACY_INPUTS + '/' + name)
        if digest(retained) == expected:
            return retained
    return path


class CustodyError(ValueError):
    """The accepted socket or daemon changed; no further mutation is allowed."""


class ExclusiveDocker:
    """Pins one local socket, daemon and exclusive allocation before any mutation."""
    def __init__(self, command, lease: Path | None, environment=None):
        self.command = command
        self.environment = dict(os.environ if environment is None else environment)
        self.lease_path = lease
        self.endpoint = None
        self.daemon_id = None
        self.socket_identity = None
        self.lease = None

    def raw(self, *arguments, env=None, **kwargs):
        # A command override extends the captured environment once. Docker selectors
        # are removed afterwards, including selectors supplied by the caller.
        merged = {**self.environment, **(env or {})}
        merged = {k: v for k, v in merged.items() if k not in ('DOCKER_HOST', 'DOCKER_CONTEXT', 'DOCKER_TLS_VERIFY', 'DOCKER_CERT_PATH')}
        return self.command(['docker', '--host', self.endpoint, *arguments], env=merged, **kwargs)

    def receipt_owner(self):
        owner = getattr(self.command, '__self__', None)
        return owner if hasattr(owner, 'receipts') else None

    def custody(self):
        return {'exclusive': True, 'endpoint': self.endpoint, 'daemonId': self.daemon_id,
                'socketIdentity': list(self.socket_identity), 'workerId': self.worker_id}


    def capture(self):
        import fcntl
        context, host = self.environment.get('DOCKER_CONTEXT'), self.environment.get('DOCKER_HOST')
        env = {k: v for k, v in self.environment.items() if not k.startswith('DOCKER_')}
        _, output = self.command(['docker', 'context', 'inspect', *([context] if context else [])], env=env)
        selected = json.loads(output)[0]['Endpoints']['docker']['Host']
        if context and host and host != selected:
            raise ValueError('conflicting Docker context/host selection')
        self.endpoint = selected if context or not host else host
        if not self.endpoint.startswith('unix:///') or self.endpoint.startswith('unix:////'):
            raise ValueError('Docker endpoint must be a local Unix socket')
        path = Path(self.endpoint[7:])
        if not path.is_absolute() or not stat.S_ISSOCK(path.stat().st_mode):
            raise ValueError('Docker endpoint must be a canonical local Unix socket')
        path = path.resolve()
        self.endpoint = "unix://" + str(path)
        identity = path.stat()
        self.socket_identity = (identity.st_dev, identity.st_ino)
        _, daemon = self.raw('info', '--format', '{{.ID}}')
        self.daemon_id = daemon.strip()
        if not self.daemon_id:
            raise ValueError('Docker daemon identity is unavailable')
        if self.lease_path is None:
            raise ValueError('exclusive disposable-worker allocation is required before mutation')
        if self.lease_path.is_symlink():
            raise ValueError('exclusive worker lease cannot be a link')
        self.lease = self.lease_path.open('r')
        fcntl.flock(self.lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
        allocation = json.load(self.lease)
        if allocation.get('exclusive') is not True or allocation.get('endpoint') != self.endpoint or allocation.get('daemonId') != self.daemon_id or not allocation.get('workerId'):
            raise ValueError('exclusive worker allocation does not match endpoint/daemon')
        self.worker_id = allocation['workerId']
        lease_stat = os.fstat(self.lease.fileno())
        self.lease_identity = (lease_stat.st_dev, lease_stat.st_ino)
        self.allocation = allocation
        self.verify()
        owner = self.receipt_owner()
        if owner is not None:
            from datetime import datetime, timezone
            now = datetime.now(timezone.utc).isoformat()
            owner.receipts.append({'argv': ['WORKER', 'exclusive-allocation'], 'startedUtc': now,
                'endedUtc': now, 'exitCode': 0, 'output': json.dumps(self.custody(), sort_keys=True),
                'daemonObservation': self.daemon_receipt, 'leaseLocked': True})
            self.admission_receipt = len(owner.receipts) - 1

    def verify(self):
        try:
            lease_stat = self.lease_path.stat()
            if self.lease_path.is_symlink() or (lease_stat.st_dev, lease_stat.st_ino) != self.lease_identity:
                raise CustodyError('exclusive worker allocation file changed')
            self.lease.seek(0)
            if json.load(self.lease) != self.allocation:
                raise CustodyError('exclusive worker allocation changed')
        except (OSError, ValueError) as error:
            raise CustodyError('exclusive worker custody changed: ' + str(error)) from error
        try:
            identity = Path(self.endpoint[7:]).stat()
        except OSError as error:
            raise CustodyError('Docker socket is unavailable; all resource mutation refused') from error
        if (identity.st_dev, identity.st_ino) != self.socket_identity:
            raise CustodyError('Docker socket changed; all resource mutation refused')
        _, output = self.raw('info', '--format', '{{.ID}}')
        if output.strip() != self.daemon_id:
            raise CustodyError('Docker daemon changed; all resource mutation refused')
        owner = self.receipt_owner()
        self.daemon_receipt = len(owner.receipts) - 1 if owner else None
        # Socket identity was observed directly immediately before this info call.
        if owner:
            owner.receipts[-1].update(socketIdentity=list(self.socket_identity))

    def run(self, *arguments, **kwargs):
        self.verify()
        owner = self.receipt_owner()
        before = len(owner.receipts) if owner else 0
        try:
            return self.raw(*arguments, **kwargs)
        finally:
            # Failed operations also retain their actual pinned execution/custody.
            if owner and len(owner.receipts) > before:
                redact = getattr(owner, 'redact', str)
                safe = [redact(str(argument)) for argument in arguments]
                owner.receipts[-1].update(argv=['docker', *safe],
                    executedArgv=['docker', '--host', self.endpoint, *safe],
                    dockerCustody=self.custody(), daemonObservation=self.daemon_receipt,
                    workerAdmission=self.admission_receipt)

    def close(self):
        if self.lease is not None:
            self.lease.close()

    def reacquire(self):
        """Reclaim the same allocation after the supervised child has exited."""
        import fcntl
        self.lease = self.lease_path.open('r')
        fcntl.flock(self.lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
        self.verify()

# Independently verified official identities; a resealed packet cannot substitute them.
CANDIDATE_IDENTITIES = {'binarySha256': 'b1ece5da76674bf6e15126aeb019fa58e444b5b8edf4d0ac59e32df895ae8fd2', 'cliSha256': 'b651f5fcfe0b261ca6b26058dde31927efc8e17b12267d6743bdbeddca09bafd', 'images': {'daprd': 'ghcr.io/dapr/daprd@sha256:9b26f9b3d9fb6fc619a33a3c8dde06daf8cf6b86fccb62d466b7dfbb7d3ca83d', 'operator': 'ghcr.io/dapr/operator@sha256:f7e3dab50f107f1a609a2024fd8d22cc97cd1c201661d52d9ae2ac03207452bf', 'injector': 'ghcr.io/dapr/injector@sha256:27f0e5f60508ffc8b42c591e70847d7041583983cc8239710608d4476a8fabff', 'sentry': 'ghcr.io/dapr/sentry@sha256:f5f057f504d6b428f68e380421613e71a4a1f3cedf1fd66beacaa6fcc866b414', 'placement': 'ghcr.io/dapr/placement@sha256:6dcfbcd606482976c5f4b821d0d0755e88db796bdfcafc395090708ebdba6c66', 'scheduler': 'ghcr.io/dapr/scheduler@sha256:ae0f1e04a0c657cb5a7c67ca4f520cac54c939a01e5810923e9d0b48a63ab739', 'rabbitmq': 'docker.io/library/rabbitmq@sha256:54cb013d76cd1fd6c0a0708f34062ad61c452bb6f016b434c6c33f1a765d2b58', 'kafka': 'docker.io/apache/kafka@sha256:77e3df9054047a88b520d0cc46e16696d3b22022e1d580aeccd2632df6532837'}, 'chart': {'path': 'deploy/dapr/qualification/public-identities/1.19.1/dapr-1.19.1.tgz', 'sha256': '8bada4c1eb566953b30a6c0620a308f1720cf6e0c633dcac7b7c0806615d831b', 'version': '1.19.1', 'appVersion': '1.19.1'}, 'archives': {'daprd': {'url': 'https://github.com/dapr/dapr/releases/download/v1.19.1/daprd_linux_amd64.tar.gz', 'sha256': '93217800d5072cf1735ed89ec8a9cdeedfb9d235895fb9e0d864088bc5d9466c', 'checksumPath': 'deploy/dapr/qualification/public-identities/1.19.1/daprd_linux_amd64.tar.gz.sha256'}, 'dapr': {'url': 'https://github.com/dapr/cli/releases/download/v1.19.1/dapr_linux_amd64.tar.gz', 'sha256': '6e9a339d261e430e5914909cc4443572573c197c174c3e14504c34116831de3b', 'checksumPath': 'deploy/dapr/qualification/public-identities/1.19.1/dapr_linux_amd64.tar.gz.sha256'}}}

CONTROL_BINARIES = {'placement': '09dacec2acfdc6c41ab08e05032c619f6500199f8687b54bb54c22b1ce9734c4', 'scheduler': 'f67e6948556e40ef6cf816296a24ad0608d22863d578987643d697ce3ff11944', 'sentry': '223c5fdac5a5c99304bd79d3dea9d474bc71931c32710f1a471eb30b765e7eb1'}


def require_candidate(path: Path, root: Path) -> dict:
    """Admit loaded bytes before creating output or infrastructure."""
    from dapr_broker_qualification import validate_profile
    profile = json.loads(path.read_text())
    validate_profile(profile, root)
    runtime = profile['runtime']
    if (runtime['version'], runtime['cliVersion'], runtime['componentsVersion']) != ('1.19.1', '1.19.1', '1.19.0'):
        raise ValueError('affected regression lane requires the approved candidate tuple')
    return profile


def run_process_group(argv, *, env=None, timeout=1200, witness=None):
    """Own, stop and reap the test group and escaped, adopted descendants."""
    import ctypes
    import signal
    import subprocess
    import tempfile
    import time
    if ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) != 0:
        raise OSError(ctypes.get_errno(), 'cannot establish child subreaper custody')
    witness = witness if witness is not None else {}
    def snapshot():
        rows = {}
        for path in Path('/proc').glob('[0-9]*/stat'):
            try:
                fields = path.read_text().rsplit(')', 1)[1].split()
                rows[int(path.parent.name)] = (int(fields[1]), int(fields[2]), fields[19])
            except (OSError, ValueError, IndexError): pass
        return rows
    baseline = snapshot()
    owned = {}
    with tempfile.TemporaryFile(mode='w+') as output:
        process = subprocess.Popen(list(map(str, argv)), stdout=output, stderr=subprocess.STDOUT,
                                   env=env, start_new_session=True)
        witness.update(pid=process.pid, group=process.pid, complete=False)
        def observe():
            current = snapshot()
            for pid, (parent, group, created) in current.items():
                if pid == process.pid or (parent == os.getpid() and baseline.get(pid, (None, None, None))[2] != created):
                    owned.setdefault(pid, created)
            changed = True
            while changed:
                changed = False
                for pid, (parent, group, created) in current.items():
                    if pid not in owned and parent in owned and current.get(parent, (None, None, None))[2] == owned[parent]:
                        owned[pid] = created; changed = True
            return {pid: row for pid, row in current.items() if owned.get(pid) == row[2]}
        code = None
        try:
            deadline = time.monotonic() + timeout
            while process.poll() is None:
                observe()
                if time.monotonic() >= deadline:
                    code = 124
                    break
                time.sleep(.02)
            if code is None: code = process.returncode
        finally:
            handlers = {kind: signal.signal(kind, signal.SIG_IGN) for kind in (signal.SIGTERM, signal.SIGINT)}
            try:
                # The subreaper adopts escaped descendants. Baseline native control
                # processes are excluded by their exact pre-launch identities.
                for signum in (signal.SIGTERM, signal.SIGKILL):
                    current = observe()
                    if any(row[1] == process.pid for row in current.values()):
                        try: os.killpg(process.pid, signum)
                        except ProcessLookupError: pass
                    for pid in current:
                        try:
                            descriptor = os.pidfd_open(pid)
                            try:
                                if snapshot().get(pid, (None, None, None))[2] == owned[pid]: signal.pidfd_send_signal(descriptor, signum)
                            finally: os.close(descriptor)
                        except ProcessLookupError: pass
                    deadline = time.monotonic() + 3
                    while time.monotonic() < deadline:
                        process.poll()
                        for pid in list(owned):
                            if pid == process.pid: continue
                            try: os.waitpid(pid, os.WNOHANG)
                            except ChildProcessError: pass
                        if not observe(): break
                        time.sleep(.02)
                    if not observe(): break
                remaining = observe()
                witness.update(complete=not remaining, exitCode=process.poll(),
                               descendants=[{'pid': pid, 'startIdentity': identity} for pid, identity in owned.items() if pid != process.pid],
                               remaining=list(remaining))
            finally:
                for kind, handler in handlers.items(): signal.signal(kind, handler)
        output.seek(0)
        return code, output.read(), witness


def durable_json(path: Path, value):
    """Atomically persist custody before a potentially interrupted create."""
    import tempfile
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, name = tempfile.mkstemp(dir=path.parent, prefix=path.name + '.')
    try:
        with os.fdopen(descriptor, 'w') as stream:
            json.dump(value, stream, indent=2, sort_keys=True)
            stream.write('\n'); stream.flush(); os.fsync(stream.fileno())
        os.replace(name, path)
        descriptor = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
        try: os.fsync(descriptor)
        finally: os.close(descriptor)
    finally:
        if os.path.exists(name): os.unlink(name)


def container_options(arguments):
    """Parse the supported Docker create/run grammar before any creation."""
    values = {'--name', '--cidfile', '--label', '-l', '--publish', '-p', '--env', '-e', '--env-file', '--entrypoint', '--pull', '--network', '--user', '-u', '--workdir', '-w', '--hostname', '-h', '--cap-add', '--cap-drop', '--restart', '--memory', '-m', '--cpus', '--platform', '--health-cmd', '--health-interval', '--health-timeout', '--health-retries', '--stop-timeout', '--volume', '-v', '--mount', '--tmpfs', '--add-host', '--security-opt', '--ulimit', '--log-driver', '--log-opt', '--shm-size'}
    flags = {'-d', '--detach', '--rm', '-i', '--interactive', '-t', '--tty', '--init', '--privileged', '--read-only', '--no-healthcheck', '-it', '-di', '-dit'}
    options = []
    index = 1
    while index < len(arguments):
        argument = str(arguments[index])
        if not argument.startswith('-'):
            return options, argument
        name, equals, value = argument.partition('=')
        if name in values:
            if not equals:
                index += 1
                if index >= len(arguments): raise ValueError('incomplete child container option')
                value = str(arguments[index])
            options.append((name, value))
        elif name not in flags or equals and value not in ('true', 'false'):
            raise ValueError('unsupported child container option; volume/identity custody cannot be established')
        index += 1
    raise ValueError('child container image is absent')


def check_container_volumes(args, invoke, state, owner, *, temporary_volumes=False):
    """Allow bind/tmpfs and already journaled volumes; never implicit volumes."""
    options, image = container_options(args)
    if any(name == '--pull' and value == 'always' for name, value in options) and '@sha256:' not in image:
        raise ValueError('mutable image pull would invalidate inspected volume custody')
    covered = set()
    for name, value in options:
        if name in ('-v', '--volume'):
            fields = value.split(':')
            if len(fields) < 2: raise ValueError('anonymous/implicit child volumes are unsupported')
            source, target = fields[:2]
            kind = 'bind' if source.startswith('/') else 'volume'
        elif name == '--mount':
            fields = {}
            for field in value.split(','):
                key, _, item = field.partition('=')
                if key in fields: raise ValueError('ambiguous child mount')
                fields[key] = item
            kind = fields.get('type', 'volume')
            if sum(name in fields for name in ('source', 'src')) > 1 or sum(name in fields for name in ('destination', 'dst', 'target')) > 1:
                raise ValueError('ambiguous child mount aliases')
            source = fields.get('source', fields.get('src', ''))
            target = fields.get('destination', fields.get('dst', fields.get('target', '')))
            if kind not in ('bind', 'volume', 'tmpfs'): raise ValueError('unsupported child mount type')
        elif name == '--tmpfs':
            covered.add(value.split(':')[0]); continue
        elif name == '--volumes-from':
            raise ValueError('inherited child volumes are unsupported')
        else: continue
        if not target.startswith('/') or kind == 'bind' and not source.startswith('/'):
            raise ValueError('child mount requires exact source and absolute destination')
        if kind == 'volume':
            rows = [row for row in state['resources'] if row['kind'] == 'volume' and row['name'] == source and row.get('id') == source and not row.get('pending') and row.get('owner') == owner]
            if len(rows) != 1: raise ValueError('implicit named/anonymous child volume creation refused')
            observed = invoke(['inspect', '--type', 'volume', source])
            if observed.returncode != 0 or volume_identity(json.loads(observed.stdout)[0]) != rows[0]['volumeIdentity']:
                raise ValueError('owned child volume identity changed')
        covered.add(target)
    observed = invoke(['image', 'inspect', image])
    if observed.returncode != 0: raise ValueError('image-declared volume inventory unavailable before creation')
    configuration = json.loads(observed.stdout)[0]['Config']
    declarations = set(configuration.get('Volumes') or {})
    missing = declarations - covered
    if missing and not temporary_volumes:
        raise ValueError('image-declared implicit child volumes are unsupported')
    for target in sorted(missing):
        # Internal owners use explicit tmpfs for unused image scratch volumes.
        # Their durable data paths are already covered by explicit bind mounts.
        args[1:1] = ['--tmpfs', target]


def owned_docker_operation(args, invoke, state, owner):
    """Admit only measured journal-owned targets for each supported mutation."""
    operation = args[0] if args else ''
    if operation in ('ps', 'info', 'version', '--version', 'inspect', 'images', 'logs', 'port', 'wait') or args[:2] in (['image', 'inspect'], ['network', 'ls'], ['network', 'inspect'], ['volume', 'ls'], ['volume', 'inspect']):
        return invoke(args)
    kind, start = ('container', 1)
    if args[:2] in (['network', 'rm'], ['volume', 'rm']):
        kind, start, operation = args[0], 2, 'rm'
    allowed = {'rm', 'start', 'stop', 'restart', 'kill', 'pause', 'unpause', 'exec', 'cp'}
    if operation not in allowed:
        raise ValueError('unsupported child resource mutation')
    values = {'--time', '-t', '--signal', '-s'} if operation != 'exec' else {'--env', '-e', '--env-file', '--user', '-u', '--workdir', '-w', '--detach-keys'}
    flags = {'--force', '-f', '--volumes', '-v', '-fv', '-vf', '--attach', '-a', '--interactive', '-i'} if operation != 'exec' else {'--detach', '-d', '--interactive', '-i', '--tty', '-t', '-it', '--privileged'}
    targets = []
    index = start
    while index < len(args):
        value = args[index]
        if value.startswith('-'):
            name, equal, _ = value.partition('=')
            if name in values:
                index += 1 if equal else 2
            elif name in flags: index += 1
            else: raise ValueError('unsupported child mutation option')
            continue
        targets.append(value); index += 1
        if operation == 'exec': break  # the remaining argv belongs to the payload
    if operation == 'cp':
        targets = [target.split(':', 1)[0] for target in targets if ':' in target]
    if not targets: raise ValueError('child mutation has no exact owned target')
    for target in targets:
        rows = [row for row in state['resources'] if row['kind'] == kind and target in (row.get('id'), row['name']) and row.get('id') and not row.get('pending') and row.get('owner') == owner]
        if len(rows) != 1: raise ValueError('child mutation target is not a journaled owned identity')
        row = rows[0]
        observed = invoke(['inspect', '--type', kind, row['id']])
        if observed.returncode != 0: raise ValueError('child mutation ownership inspection failed')
        actual = json.loads(observed.stdout)[0]
        labels = actual.get('Config', {}).get('Labels', {}) if kind == 'container' else actual.get('Labels', {})
        if (actual.get('Id', actual.get('Name')) != row['id'] or labels.get('hexalith.qualification') != owner
                or labels.get('hexalith.qualification.resource') != row['resourceToken']
                or kind == 'volume' and volume_identity(actual) != row.get('volumeIdentity')):
            raise ValueError('child mutation target identity/ownership changed')
    return invoke(args)


def child_docker(arguments, invoke, journal: Path, owner: str, *, internal=False, custody=None, supervisor_token=None):
    """Journal resource creation before launch; retain only identity and ownership."""
    import hashlib
    import re
    import secrets
    import fcntl
    from dapr_broker_qualification import write_json
    journal.parent.mkdir(parents=True, exist_ok=True)
    with journal.with_suffix('.lock').open('a+') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        state = json.loads(journal.read_text()) if journal.exists() else {'resources': []}
        args = list(map(str, arguments))
        if args[:1] == ['container']:
            args = args[1:]
            if args[:1] == ['ls']: args[0] = 'ps'
        if custody is not None:
            if state.get('custody', custody) != custody or state.get('owner', owner) != owner:
                raise CustodyError('resource journal custody changed')
            state.update(custody=custody, owner=owner, supervisorToken=supervisor_token)
        kind = 'container' if args[:1] in (['run'], ['create']) else args[0] if args[:2] in (['volume', 'create'], ['network', 'create']) else None
        if kind is None:
            return owned_docker_operation(args, invoke, state, owner) if not internal else invoke(args)
        prefix = 1 if kind == 'container' else 2
        token = secrets.token_hex(10)
        name = None
        if kind == 'container':
            options, _ = container_options(args)
            if any(key == '--cidfile' for key, _ in options) and not internal:
                raise ValueError('child-owned container CID files are allocated by the pinned adapter')
            names = [value for key, value in options if key == '--name']
            if len(names) > 1: raise ValueError('ambiguous child container name')
            if names: name = names[0]
            if name is None:
                name = owner + '-child-' + token
                args[prefix:prefix] = ['--name', name]
            check_container_volumes(args, invoke, state, owner, temporary_volumes=internal)
            supplied = [value for key, value in options if key == '--cidfile']
            if len(supplied) > 1: raise ValueError('ambiguous container CID path')
            cidfile = Path(supplied[0]) if supplied else journal.parent / (token + '.cid')
            if not supplied: args[prefix:prefix] = ['--cidfile', str(cidfile)]
        else:
            values = {'--driver', '-d', '--label', '--opt', '-o', '--scope', '--subnet', '--gateway', '--ip-range', '--ipam-driver', '--ipam-opt', '--aux-address', '--config-from'}
            flags = {'--attachable', '--internal', '--ipv6', '--ingress', '--config-only'}
            positional = []
            index = prefix
            while index < len(args):
                argument = args[index]
                if argument in values:
                    if index + 1 >= len(args): raise ValueError('incomplete child resource option')
                    index += 2
                elif argument in flags or argument.startswith('--') and argument.split('=')[0] in values | flags:
                    index += 1
                elif argument.startswith('-'):
                    raise ValueError('unsupported child creation option; ownership cannot be established')
                else:
                    positional.append(argument); index += 1
            if len(positional) > 1: raise ValueError('ambiguous child resource name')
            name = positional[0] if positional else owner + '-child-' + token
            if not positional: args.append(name)
            cidfile = None
        labels = [value for key, value in options if key in ('--label', '-l')] if kind == 'container' else [args[index + 1] if argument == '--label' and index + 1 < len(args) else argument[8:] for index, argument in enumerate(args) if argument == '--label' or argument.startswith('--label=')]
        for label in labels:
            if label.startswith(('hexalith.qualification=', 'hexalith.qualification.resource=')) and not (internal and label == 'hexalith.qualification=' + owner):
                raise ValueError('child resource cannot override adapter ownership labels')
        if not re.fullmatch('[a-zA-Z0-9][a-zA-Z0-9_.-]*', name):
            raise ValueError('child resource name is not exact and supported')
        # An existing child-supplied name is never adopted or relabeled.
        listing = ['ps', '-aq', '--no-trunc', '--filter', 'name=^/' + re.escape(name) + '$'] if kind == 'container' else [kind, 'ls', '--format', '{{.Name}}']
        present = invoke(listing)
        if present.returncode != 0 or (present.stdout.strip() if kind == 'container' else name in present.stdout.splitlines()):
            raise ValueError('child resource name is already present or absence is unproven')
        args[prefix:prefix] = ['--label', 'hexalith.qualification=' + owner, '--label', 'hexalith.qualification.resource=' + token]
        row = {'kind': kind, 'name': name, 'id': None, 'owner': owner, 'cidfile': str(cidfile) if cidfile else None, 'pending': True, 'resourceToken': token, 'absenceVerified': True}
        state['resources'].append(row)
        durable_json(journal, state)
        try:
            result = invoke(args)
            return result
        finally:
            identity = cidfile.read_text().strip() if cidfile and cidfile.exists() else name
            observed = invoke(['inspect', '--type', kind, identity])
            if observed.returncode == 0:
                value = json.loads(observed.stdout)[0]
                labels = value.get('Config', {}).get('Labels', {}) if kind == 'container' else value.get('Labels', {})
                if labels.get('hexalith.qualification') != owner or labels.get('hexalith.qualification.resource') != token:
                    raise ValueError('child resource ownership cannot be verified')
                if kind != 'volume' and not re.fullmatch('[a-f0-9]{64}', value.get('Id', '')):
                    raise ValueError('child resource has no immutable identity')
                row.update(id=value.get('Id', value.get('Name')), pending=False)
                if kind == 'volume':
                    # Docker volumes expose no UUID. Bind name, CreatedAt, driver,
                    # labels and options; refuse a replacement at cleanup.
                    row['volumeIdentity'] = volume_identity(value)
            durable_json(journal, state)


def volume_identity(value):
    import hashlib
    fields = {key: value.get(key) for key in ('Name', 'CreatedAt', 'Driver', 'Scope', 'Labels', 'Options')}
    if not fields['CreatedAt']:
        raise ValueError('child volume lacks stable creation identity')
    return hashlib.sha256(json.dumps(fields, sort_keys=True).encode()).hexdigest()


def register_private_custody(owner):
    """Register temporary-root ownership during initialization, before admission."""
    root = os.environ.get('QUALIFICATION_SUPERVISOR_ROOT')
    if not root: return None
    root = Path(root)
    state = json.loads((root / 'supervisor.json').read_text())
    token = os.environ.get('QUALIFICATION_SUPERVISOR_TOKEN')
    if state['token'] != token: raise CustodyError('direct lane supervisor token differs')
    journal = root / (owner.run_id + '.json')
    if not journal.exists():
        private = getattr(owner, 'private_parent', None) or owner.runtime
        identity = private.stat()
        durable_json(journal, {'resources': [], 'custody': state['custody'], 'owner': owner.run_id,
            'supervisorToken': token, 'privateDirectory': {'path': str(private.resolve()),
                'device': identity.st_dev, 'inode': identity.st_ino, 'uid': identity.st_uid}})
    return journal


def resource_journal(owner, default: Path):
    """Select the initialized parent-readable journal after child admission."""
    root = os.environ.get('QUALIFICATION_SUPERVISOR_ROOT')
    if not root: return default, None
    state = json.loads((Path(root) / 'supervisor.json').read_text())
    if state['custody'] != owner.docker_owner.custody(): raise CustodyError('direct lane parent/child worker custody differs')
    return register_private_custody(owner), os.environ.get('QUALIFICATION_SUPERVISOR_TOKEN')


def reconcile_private_directory(state):
    """Delete a child temporary root only while its recorded identity is intact."""
    import shutil
    entry = state.get('privateDirectory')
    if entry is None:
        raise ValueError('supervised owner did not retain private-directory custody')
    path = Path(entry['path'])
    try: identity = path.lstat()
    except FileNotFoundError: return {'absent': True}
    if (path.is_symlink() or not stat.S_ISDIR(identity.st_mode)
            or (identity.st_dev, identity.st_ino, identity.st_uid) != (entry['device'], entry['inode'], entry['uid'])
            or identity.st_uid != os.getuid()):
        raise ValueError('child private-directory identity changed')
    shutil.rmtree(path)
    return {'absent': not path.exists()}


def cleanup_child_resources(owner, journal: Path):
    """Remove each independently verified owned child identity; fail closed on loss."""
    from dapr_broker_qualification import write_json
    result = {'resources': [], 'complete': True}
    if not journal.exists(): return result
    state = json.loads(journal.read_text())
    if state.get('custody') is not None and state['custody'] != owner.docker_owner.custody():
        raise CustodyError('child journal worker custody differs from parent')
    custody_valid = True
    for row in reversed(state['resources']):
        outcome = {key: row.get(key) for key in ('kind', 'name', 'id', 'owner')}
        outcome['absent'] = False
        try:
            if not custody_valid: raise CustodyError('custody lost; remaining child resources retained')
            identity = row.get('id')
            if not identity and row.get('cidfile') and Path(row['cidfile']).exists():
                identity = Path(row['cidfile']).read_text().strip()
            if not identity: identity = row['name']
            code, output = owner.docker('inspect', '--type', row['kind'], identity, check=False)
            if code == 0:
                value = json.loads(output)[0]
                labels = value.get('Config', {}).get('Labels', {}) if row['kind'] == 'container' else value.get('Labels', {})
                actual = value.get('Id', value.get('Name'))
                if (row.get('owner') != owner.run_id or labels.get('hexalith.qualification') != owner.run_id
                        or labels.get('hexalith.qualification.resource') != row.get('resourceToken')
                        or not row.get('absenceVerified') or (row.get('id') and actual != row['id'])):
                    raise ValueError('child resource identity/ownership changed')
                if row['kind'] != 'volume' and not __import__('re').fullmatch('[a-f0-9]{64}', actual or ''):
                    raise ValueError('child resource immutable identity unavailable')
                if row['kind'] == 'volume' and (not row.get('volumeIdentity') or volume_identity(value) != row['volumeIdentity']):
                    raise ValueError('child volume identity changed or unresolved')
                outcome['id'] = actual
                remove = ['rm', '-f', actual] if row['kind'] == 'container' else [row['kind'], 'rm', actual]
                owner.docker(*remove)
            unresolved_network = row['kind'] == 'network' and not row.get('id')
            listing = ['ps', '-aq', '--no-trunc'] if row['kind'] == 'container' else ['network', 'ls', '--format', '{{.Name}}'] if unresolved_network else ['network', 'ls', '--format', '{{.ID}}', '--no-trunc'] if row['kind'] == 'network' else ['volume', 'ls', '--format', '{{.Name}}']
            code, output = owner.docker(*listing, check=False)
            if code != 0: raise ValueError('owned resource absence inventory failed')
            # A pending container needs an exact name inventory when no CID was obtained.
            if row['kind'] == 'container' and not row.get('id'):
                _, output = owner.docker('ps', '-aq', '--no-trunc', '--filter', 'name=^/' + row['name'] + '$')
                outcome['absent'] = not output.strip()
            else:
                outcome['absent'] = identity not in output.split()
        except Exception as error:
            outcome['error'] = str(error)
            if isinstance(error, CustodyError): custody_valid = False
        result['resources'].append(outcome)
    result['complete'] = all(row['absent'] for row in result['resources'])
    write_json(owner.output / 'child-resource-cleanup.json', result)
    return result


def retain_compatibility_witnesses(scratch: Path, destination: Path):
    """Project bounded source/process/restore/cleanup witnesses, never private trees."""
    import hashlib
    import re
    from dapr_broker_qualification import write_json
    allowed = re.compile(r'^(executor-inputs|materialized-inputs|focused-summary|cleanup|cleanup-pass-\d+|command-\d+|process-\d+-start|file-fingerprint-\d+)\.json$|^scenarios\.ndjson$')
    manifest = []
    total = 0
    def project(value, key=''):
        if isinstance(value, dict): return {name: project(item, name) for name, item in value.items()}
        if isinstance(value, list): return [project(item, key) for item in value[:2048]]
        if isinstance(value, str):
            if key.lower() in ('executor', 'builds', 'source', 'sourcecommit', 'taggedsource') and re.fullmatch('[a-f0-9]{40}', value): return value
            if ('hash' in key.lower() or key.lower().endswith('sha256')) and re.fullmatch('[a-f0-9]{64}', value): return value
            if key.lower() in ('filename', 'execution', 'compatibility', 'result') and value in ('dotnet', 'docker', 'git', 'passed', 'failed', 'unavailable', 'incompatible', 'compatible', 'Pass', 'Fail'): return value
            return {'sha256': hashlib.sha256(value.encode()).hexdigest(), 'bytes': len(value.encode())}
        return value
    if not scratch.exists(): return manifest
    # Only files directly inside each invocation are eligible. Consumers, SDKs,
    # packages, credential files and provider dumps cannot enter the artifact.
    for invocation in sorted(scratch.iterdir()):
        if not invocation.is_dir() or invocation.is_symlink(): continue
        for path in sorted(invocation.iterdir()):
            if path.is_symlink() or not path.is_file() or not allowed.fullmatch(path.name): continue
            data = path.read_bytes()
            record = {'path': invocation.name + '/' + path.name, 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data), 'retained': False}
            if len(data) <= 262144 and len(manifest) < 2000 and total < 4 * 1024 * 1024:
                try:
                    value = [json.loads(line) for line in data.decode().splitlines()] if path.suffix == '.ndjson' else json.loads(data)
                    safe = project(value)
                    encoded = (json.dumps(safe, indent=2, sort_keys=True) + '\n').encode('utf-8')
                    if len(encoded) <= 262144 and total + len(encoded) <= 4 * 1024 * 1024:
                        target = destination / record['path']
                        target.parent.mkdir(parents=True, exist_ok=True)
                        target.write_bytes(encoded)
                        total += len(encoded)
                        record['retained'] = True
                        record['retainedSha256'] = hashlib.sha256(encoded).hexdigest()
                        record['retainedBytes'] = len(encoded)
                except (ValueError, UnicodeDecodeError):
                    record['reason'] = 'unparseable witness'
            manifest.append(record)
    write_json(destination / 'manifest.json', {'witnesses': manifest[:5000], 'observedFiles': len(manifest), 'retainedBytes': total,
        'projection': 'strings committed by hash except approved source/hash/status fields; private and materialized trees excluded'})
    verify_compatibility_witnesses(destination)
    return manifest


def verify_compatibility_witnesses(destination: Path):
    """Check the actual retained bytes against their separate commitments."""
    import hashlib
    from dapr_broker_qualification import safe_file
    manifest = json.loads((destination / 'manifest.json').read_text())
    total = 0
    retained = set()
    for row in manifest['witnesses']:
        if not row['retained']: continue
        data = safe_file(destination, row['path']).read_bytes()
        if len(data) != row['retainedBytes'] or hashlib.sha256(data).hexdigest() != row['retainedSha256'] or len(data) > 262144:
            raise ValueError('retained compatibility witness commitment mismatch')
        total += len(data); retained.add(row['path'])
    actual = {str(path.relative_to(destination)) for path in destination.rglob('*') if path.is_file() and path.name != 'manifest.json'}
    if total != manifest['retainedBytes'] or total > 4 * 1024 * 1024 or actual != retained:
        raise ValueError('retained compatibility witness inventory/size mismatch')
    return manifest


def sanitize_xunit_report(path: Path):
    """Retain test identities/results while committing arbitrary diagnostics by hash."""
    import hashlib
    import re
    import xml.etree.ElementTree as ET
    if not path.exists(): return
    try:
        report = ET.parse(path)
    except ET.ParseError:
        from dapr_broker_qualification import write_json
        data = path.read_bytes()
        commitment = hashlib.sha256(data).hexdigest()
        write_json(path.with_suffix('.invalid.json'), {'sha256': commitment, 'bytes': len(data), 'result': 'invalid-xunit-report'})
        report = ET.ElementTree(ET.fromstring('<assemblies><diagnostic>diagnostic-sha256:' + commitment + '</diagnostic></assemblies>'))
    for node in report.iter():
        # Keep closed adjudication values and bounded numeric measurements.
        # Identities remain comparable commitments; parameter values and custom
        # diagnostics cannot enter retained attribute values verbatim.
        attributes = {}
        for key, value in node.attrib.items():
            if key == 'result' and value in ('Pass', 'Fail', 'Skip'):
                attributes[key] = value
            elif key in ('time', 'total', 'passed', 'failed', 'skipped', 'not-run', 'errors') and re.fullmatch(r'[0-9]{1,8}(\.[0-9]{1,9})?', value):
                attributes[key] = value
            elif key in ('name', 'type', 'method', 'exception-type', 'date', 'test-framework', 'environment'):
                attributes[key] = value if re.fullmatch('diagnostic-sha256:[a-f0-9]{64}', value) else 'diagnostic-sha256:' + hashlib.sha256(value.encode()).hexdigest()
        node.attrib.clear(); node.attrib.update(attributes)
        for name in ('text', 'tail'):
            value = getattr(node, name)
            if value and value.strip() and not re.fullmatch('diagnostic-sha256:[a-f0-9]{64}', value):
                setattr(node, name, 'diagnostic-sha256:' + hashlib.sha256(value.encode()).hexdigest())
    report.write(path, encoding='utf-8', xml_declaration=True)


def xunit_outcome(path: Path, code: int):
    """Adjudicate actual selected test rows, retaining only bounded diagnostics."""
    import xml.etree.ElementTree as ET
    try:
        tests = list(ET.parse(path).getroot().iter('test'))
        return {'exitCode': code, 'tests': len(tests), 'passed': code == 0 and bool(tests) and all(row.get('result') == 'Pass' for row in tests)}
    finally:
        sanitize_xunit_report(path)
