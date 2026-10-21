#!/usr/bin/env python3
"""Bind the final disposable runtime to source, patch, toolchain and dependency bytes."""
import datetime
import hashlib
import json
import subprocess
from pathlib import Path

packet=Path(__file__).resolve().parent
acquisition=json.loads((packet/'runtime-acquisition.json').read_text())
work=Path(acquisition['workDirectory'])
source=work/'dapr-b32a815fa2260983e181b95272ea555fe17d3895'
go=work/'go/bin/go'
binary=work/'daprd-recovery-observed'
def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()
commands={}
for label,args in [('version',[str(binary),'--version']),('buildinfo',[str(go),'version','-m',str(binary)]),('toolchain',[str(go),'version'])]:
    result=subprocess.run(args,capture_output=True,text=True)
    commands[label]={'command':args,'exitCode':result.returncode,'output':result.stdout+result.stderr}
identity={'observedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sourceRevision':'b32a815fa2260983e181b95272ea555fe17d3895','candidatePath':str(binary),'candidateSha256':sha(binary),'candidateBytes':binary.stat().st_size,'toolchainSha256':sha(go),'sourceArchiveSha256':acquisition['downloads'][1]['sha256'],'goModSha256':sha(source/'go.mod'),'goSumSha256':sha(source/'go.sum'),'patchSha256':sha(packet/'final-runtime-recovery.patch'),'routerSha256':sha(source/'pkg/actors/router/router.go'),'commands':commands,'installMutation':False,'qualification':'fixture-only local candidate; no production selection'}
output=packet/'final-runtime-identity.json'
assert not output.exists()
output.write_text(json.dumps(identity,indent=2)+'\n')
print(json.dumps({'path':str(binary),'sha256':identity['candidateSha256']}))
