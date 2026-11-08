#!/usr/bin/env python3
"""Child-process Docker adapter bound to the parent worker's exact socket/daemon."""
import json
import os
from pathlib import Path
import stat
import subprocess
import sys


def main():
    endpoint = os.environ['QUALIFICATION_PINNED_ENDPOINT']
    expected = os.environ['QUALIFICATION_PINNED_DAEMON']
    identity = os.environ['QUALIFICATION_PINNED_SOCKET']
    lease = Path(os.environ['QUALIFICATION_PINNED_WORKER'])
    lease_stat = lease.stat()
    allocation = json.loads(lease.read_text())
    if (lease.is_symlink() or f'{lease_stat.st_dev}:{lease_stat.st_ino}' != os.environ['QUALIFICATION_PINNED_LEASE']
            or allocation != json.loads(os.environ['QUALIFICATION_PINNED_ALLOCATION'])):
        raise ValueError('Selected exclusive worker allocation changed; Docker operation refused')
    path = Path(endpoint[7:])
    observed = path.stat()
    if not stat.S_ISSOCK(observed.st_mode) or f'{observed.st_dev}:{observed.st_ino}' != identity:
        raise ValueError('Selected worker socket changed; Docker operation refused')
    real = os.environ['QUALIFICATION_REAL_DOCKER']
    env = {key: value for key, value in os.environ.items() if key not in ('DOCKER_HOST', 'DOCKER_CONTEXT', 'DOCKER_TLS_VERIFY', 'DOCKER_CERT_PATH')}
    daemon = subprocess.check_output([real, '--host', endpoint, 'info', '--format', '{{.ID}}'], env=env, text=True, timeout=30).strip()
    if daemon != expected: raise ValueError('Selected daemon changed; Docker operation refused')
    arguments = sys.argv[1:]
    while arguments and arguments[0].startswith('-') and arguments != ['--version']:
        option = arguments[0]
        if option in ('--host', '-H'):
            if len(arguments) < 3: raise ValueError('Docker endpoint value is absent')
            selected = arguments[1]; arguments = arguments[2:]
        elif option.startswith(('--host=', '-H=')):
            selected = option.split('=', 1)[1]; arguments = arguments[1:]
        elif option.startswith('-H'):
            selected = option[2:]; arguments = arguments[1:]
        else: raise ValueError('Unsupported Docker global endpoint/context option')
        if not selected.startswith('unix:///') or 'unix://' + str(Path(selected[7:]).resolve()) != endpoint:
            raise ValueError('Conflicting Docker endpoint refused')
    if arguments[:2] == ['context', 'inspect']:
        if len(arguments) != 2: raise ValueError('Selected worker cannot change Docker contexts')
        print(json.dumps([{'Name': 'qualification-pinned', 'Endpoints': {'docker': {'Host': endpoint}}}]))
        return 0
    if not arguments or arguments[0].startswith('-') and arguments != ['--version']:
        raise ValueError('Unsupported Docker global options cannot bypass resource journaling')
    if arguments[0] in ('compose', 'build', 'buildx', 'builder', 'system', 'swarm', 'stack', 'service', 'plugin', 'import', 'load', 'commit') or 'prune' in arguments[:2]:
        raise ValueError('Unsupported Docker resource mutation cannot bypass ownership journaling')
    from dapr_runtime_qualification import child_docker
    def invoke(selected):
        lease = Path(os.environ['QUALIFICATION_PINNED_WORKER'])
        lease_stat = lease.stat()
        allocation = json.loads(lease.read_text())
        if (lease.is_symlink() or f'{lease_stat.st_dev}:{lease_stat.st_ino}' != os.environ['QUALIFICATION_PINNED_LEASE']
                or allocation != json.loads(os.environ['QUALIFICATION_PINNED_ALLOCATION'])):
            raise ValueError('Selected exclusive worker allocation changed; Docker operation refused')
        current = path.stat()
        if not stat.S_ISSOCK(current.st_mode) or f'{current.st_dev}:{current.st_ino}' != identity:
            raise ValueError('Selected worker socket changed; Docker operation refused')
        observed = subprocess.check_output([real, '--host', endpoint, 'info', '--format', '{{.ID}}'], env=env, text=True, timeout=30).strip()
        if observed != expected:
            raise ValueError('Selected daemon changed; Docker operation refused')
        return subprocess.run([real, '--host', endpoint, *selected], env=env, timeout=240,
                              text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    result = child_docker(arguments, invoke, Path(os.environ['QUALIFICATION_CHILD_RESOURCES']), os.environ['QUALIFICATION_RESOURCE_OWNER'])
    sys.stdout.write(result.stdout)
    sys.stderr.write(result.stderr)
    return result.returncode


if __name__ == '__main__':
    try: raise SystemExit(main())
    except (Exception, KeyboardInterrupt) as error:
        print('FAIL: ' + str(error), file=sys.stderr)
        raise SystemExit(1)
