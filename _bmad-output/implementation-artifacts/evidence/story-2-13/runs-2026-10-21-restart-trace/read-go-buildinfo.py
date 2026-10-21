#!/usr/bin/env python3
"""Read bounded, value-free Go build identity without installing a Go toolchain."""
import hashlib
import json
from pathlib import Path

binary = Path.home() / '.dapr/bin/daprd'
data = binary.read_bytes()
header = data.index(b'\xff Go buildinf:')
assert data[header + 15] & 2, 'Expected Go inline build-info strings'

def read_string(position):
    size = 0
    shift = 0
    while True:
        value = data[position]
        position += 1
        size |= (value & 127) << shift
        if value < 128:
            break
        shift += 7
        assert shift < 64
    assert size < 1_048_576
    return data[position:position + size], position + size

version, position = read_string(header + 32)
modules, position = read_string(position)
records = modules[16:-16].decode('utf-8').splitlines()
print(json.dumps({
    'binarySha256': hashlib.sha256(data).hexdigest(),
    'goVersion': version.decode('utf-8'),
    'records': [line for line in records if line.startswith((
        'path\t', 'mod\t', 'dep\tgoogle.golang.org/grpc\t', 'build\tvcs.'))],
}, indent=2))
