"""Archive and remove only this task's completed private review workspace."""
from pathlib import Path
import datetime
import hashlib
import io
import json
import shutil
import tarfile
p=Path(__file__).resolve().parent
private=Path('/tmp/folders-12-2-final-review')
target=p/'final-review-inputs.tar.gz'
if target.exists() or (p/'final-review-inputs-cleanup.json').exists():
    raise SystemExit('Refusing to overwrite evidence')
def sha(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
if not private.is_dir() or private.is_symlink():raise SystemExit('Private workspace ownership/path changed')
files={str(f.relative_to(private)):sha(f) for f in sorted(private.rglob('*')) if f.is_file()}
if any(f.is_symlink() for f in private.rglob('*')):raise SystemExit('Unexpected symlink')
with tarfile.open(target,'x:gz') as archive:
    for name in files:archive.add(private/name,arcname=name,recursive=False)
with tarfile.open(target,'r:gz') as archive:
    members={m.name:hashlib.sha256(archive.extractfile(m).read()).hexdigest() for m in archive if m.isfile()}
if members!=files or any(sha(private/name)!=digest for name,digest in files.items()):raise SystemExit('Archive verification failed')
shutil.rmtree(private)
result={'passed':not private.exists(),'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'private_directory':str(private),'files_sha256':files,'archive':target.name,'archive_sha256':sha(target),'archive_members_verified':members==files,'runtime_cleanup_scope':'Completed private review inputs only; all five frozen source/build inputs remain deliberately retained.'}
with (p/'final-review-inputs-cleanup.json').open('x') as stream:json.dump(result,stream,indent=2);stream.write('\n')
print(json.dumps({'passed':result['passed'],'files':len(files),'archive_sha256':result['archive_sha256']}))
