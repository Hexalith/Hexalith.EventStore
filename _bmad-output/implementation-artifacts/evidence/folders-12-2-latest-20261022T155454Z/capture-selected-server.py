#!/usr/bin/env python3
"""Capture the actual final Unit process without changing its execution."""
import datetime
import hashlib
import json
import os
from pathlib import Path

packet = Path(__file__).resolve().parent
receipt = json.loads((packet / 'guardfinal-server-full-command.json').read_text())
pid = receipt['pid']
process = Path('/proc') / str(pid)

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def start_ticks():
    stat = (process / 'stat').read_text()
    return stat[stat.rfind(')') + 1:].split()[19]

identity = start_ticks()
argv = [item.decode() for item in (process / 'cmdline').read_bytes().split(b'\0') if item]
if argv != receipt['argv']:
    raise RuntimeError('Selected process argv differs')
raw = (process / 'maps').read_text()
unit = Path(receipt['argv'][1])
stat = unit.stat()
mapped = []
for line in raw.splitlines():
    fields = line.split(maxsplit=5)
    if len(fields) == 6 and fields[5] == str(unit):
        major, minor = [int(value, 16) for value in fields[3].split(':')]
        if (major, minor, int(fields[4])) != (os.major(stat.st_dev), os.minor(stat.st_dev), stat.st_ino):
            raise RuntimeError('Actual Unit mapping identity differs')
        mapped.append(line)
if not mapped:
    raise RuntimeError('Actual Unit mappings missing')
row = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'command_receipt': 'guardfinal-server-full-command.json',
       'pid': pid, 'start_ticks': identity, 'argv': argv, 'exe': str((process / 'exe').resolve()), 'exe_sha256': sha(process / 'exe'),
       'unit': str(unit), 'unit_sha256': sha(unit), 'pdb_sha256': sha(unit.with_suffix('.pdb')),
       'device_major': os.major(stat.st_dev), 'device_minor': os.minor(stat.st_dev), 'inode': stat.st_ino,
       'actual_unit_map_rows': mapped, 'maps': 'selected-final-server-process.maps',
       'scope': 'Actual final Unit process. Strict bounded-worker CLR MVID/OS-map/source proof remains separate.'}
if start_ticks() != identity:
    raise RuntimeError('Selected process identity changed')
with (packet / row['maps']).open('x') as stream:
    stream.write(raw)
row['maps_sha256'] = sha(packet / row['maps'])
with (packet / 'selected-final-server-process-identity.json').open('x') as stream:
    json.dump(row, stream, indent=2)
    stream.write('\n')
print(json.dumps({key: row[key] for key in ('pid', 'start_ticks', 'unit_sha256', 'pdb_sha256')}))
