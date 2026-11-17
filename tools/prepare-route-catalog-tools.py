#!/usr/bin/env python3
"""Prepare the pinned catalog tools only in the caller's disposable workspace."""
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import sys
import tarfile
import urllib.request

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from legacy_sidecar_runtime import local_docker_environment


def main():
    profile = json.loads(Path('deploy/dapr/qualification/runtime-lock.json').read_text())
    environment = local_docker_environment()
    endpoint = environment['DOCKER_HOST']
    for image in (*(profile['images'][name]['reference'] for name in ('sentry', 'placement', 'scheduler')),
                  'redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf'):
        subprocess.run(['docker', '--host', endpoint, 'pull', image], check=True, timeout=300, env=environment)
    tools = (
        ('daprd', 'dapr', profile['runtime']['version'], profile['runtime']['binarySha256']),
        ('dapr', 'cli', profile['runtime']['cliVersion'], profile['runtime']['cliSha256']),
    )
    for executable, repository, version, checksum in tools:
        data = urllib.request.urlopen(f'https://github.com/dapr/{repository}/releases/download/v{version}/{executable}_linux_amd64.tar.gz', timeout=60).read()
        with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as archive:
            member = next(m for m in archive.getmembers() if PurePosixPath(m.name).name == executable and m.isfile())
            binary = archive.extractfile(member).read()
        if hashlib.sha256(binary).hexdigest() != checksum:
            raise ValueError('catalog fixture tool checksum mismatch')
        destination = Path(os.environ['TMPDIR']) / 'route-catalog-tools' / executable
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(binary)
        destination.chmod(0o755)


if __name__ == '__main__':
    main()
