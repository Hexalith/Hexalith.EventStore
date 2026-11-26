#!/usr/bin/env python3
"""Install isolated official CLI/runtime artifacts selected by a validated lock."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import tarfile
import urllib.request
from dapr_runtime_qualification import PLATFORM_1192_ID, load_profile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', type=Path, required=True)
    parser.add_argument('--destination', type=Path)
    parser.add_argument('--validate-only', action='store_true', help='check locked tool inputs without downloading or installing')
    args = parser.parse_args()
    if not args.validate_only and args.destination is None:
        parser.error('--destination is required for installation')
    data, profile = load_profile(args.profile, Path(__file__).resolve().parents[1])
    if args.validate_only:
        runtime = profile['runtime']
        print(json.dumps({'profileSha256': hashlib.sha256(data).hexdigest(),
                          'daprdSha256': runtime['binarySha256'], 'cliSha256': runtime['cliSha256'],
                          'archives': {name: runtime['archives'][name] for name in ('daprd', 'dapr')
                                       if name in runtime.get('archives', {})}}, indent=2))
        return 0
    executables = {}
    for repository, version, binary, field, folder in [
        ('dapr/cli', profile['runtime']['cliVersion'], 'dapr', 'cliSha256', 'cli'),
        ('dapr/dapr', profile['runtime']['version'], 'daprd', 'binarySha256', 'runtime'),
    ]:
        identity = profile['runtime'].get('archives', {}).get(binary)
        url = f'https://github.com/{repository}/releases/download/v{version}/{binary}_linux_amd64.tar.gz'
        if profile.get('profileId') == PLATFORM_1192_ID:
            url = identity['url']
        with urllib.request.urlopen(url, timeout=120) as response:
            data = response.read()
        if identity and hashlib.sha256(data).hexdigest() != identity['sha256']:
            raise ValueError(binary + ': official archive checksum mismatch')
        with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as package:
            members = [m for m in package.getmembers() if Path(m.name).name == binary and m.isfile()]
            if len(members) != 1: raise ValueError('Ambiguous release executable')
            content = package.extractfile(members[0]).read()
        if hashlib.sha256(content).hexdigest() != profile['runtime'][field]:
            raise ValueError(binary + ': immutable executable checksum mismatch')
        executables[args.destination.resolve() / folder / binary] = content
    for destination, content in executables.items():
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(content); destination.chmod(0o755)
    if os.environ.get('GITHUB_PATH'):
        with open(os.environ['GITHUB_PATH'], 'a') as stream: stream.write(str(args.destination.resolve() / 'cli') + '\n')
    if os.environ.get('GITHUB_ENV'):
        with open(os.environ['GITHUB_ENV'], 'a') as stream:
            stream.write('QUALIFICATION_DAPRD=' + str(args.destination.resolve() / 'runtime/daprd') + '\n')
            stream.write('EVENTSTORE_ROUTE_CATALOG_DAPR_CLI=' + str(args.destination.resolve() / 'cli/dapr') + '\n')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
