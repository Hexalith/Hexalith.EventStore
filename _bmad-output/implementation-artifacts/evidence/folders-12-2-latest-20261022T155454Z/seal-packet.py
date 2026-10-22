#!/usr/bin/env python3
"""Create one complete, exclusive file-set seal after all packet writes end."""
import hashlib
import json
from pathlib import Path
import stat
p=Path(__file__).resolve().parent
target=p/'SHA256SUMS'
if target.exists():raise SystemExit('Refusing to replace an existing seal')
def files():
    rows=[]
    for f in sorted(p.rglob('*')):
        mode=f.lstat().st_mode
        if stat.S_ISDIR(mode):continue
        if not stat.S_ISREG(mode):raise RuntimeError('Nonregular packet entry: '+str(f))
        rows.append(f)
    return rows
def identity(value):
    return (value.st_dev,value.st_ino,value.st_size,value.st_mtime_ns,value.st_ctime_ns)
entries=files()
rows=[]
for f in entries:
    before=f.stat()
    with f.open('rb') as stream:digest=hashlib.file_digest(stream,'sha256').hexdigest()
    if identity(f.stat())!=identity(before):raise RuntimeError('Packet file changed during sealing: '+str(f))
    name=str(f.relative_to(p))
    if '\n' in name or '\r' in name:raise RuntimeError('Unsafe manifest filename')
    rows.append(digest+'  '+name+'\n')
if files()!=entries:raise RuntimeError('Packet file set changed during sealing')
with target.open('x') as stream:stream.writelines(rows)
print(json.dumps({'sealed_files':len(rows),'seal_sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'packet':str(p),'disposition':json.loads((p/'qualification.json').read_text())['disposition']}))
