#!/usr/bin/env python3
import datetime, hashlib, json, os, pathlib, re, subprocess, sys
packet=pathlib.Path(__file__).resolve().parent
root=next(p for p in packet.parents if (p/'Hexalith.EventStore.slnx').exists())
def digest(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
def ids():
 paths=list((root/'src/Hexalith.EventStore.AppHost/bin/Debug').rglob('*.dll'))+list((root/'src/Hexalith.EventStore/bin/Debug').rglob('*.dll'))+list((root/'tests/Hexalith.EventStore.IntegrationTests/bin/Debug').rglob('*.dll'))
 return {str(p.relative_to(root)):digest(p) for p in sorted(set(paths))}
label,*command=sys.argv[1:]
if not re.fullmatch('[a-z0-9-]+',label) or not command:raise ValueError('label and command required')
receipt=packet/(label+'.json');log=packet/(label+'.log')
if receipt.exists() or log.exists():raise FileExistsError(label)
before=ids();started=datetime.datetime.now(datetime.timezone.utc).isoformat()
result=subprocess.run(command,cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
output=re.sub(r'(?i)([?&]t=|x-otlp-api-key=)[^\s&\"\']+',r'\1[redacted]',result.stdout)
output=re.sub(r'\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b','[redacted-jwt]',output)
log.write_text(output)
data={'command':command,'startedUtc':started,'completedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exitCode':result.returncode,'outputSha256':digest(log),'binaryBefore':before,'binaryAfter':ids(),'recorderSha256':digest(pathlib.Path(__file__))}
receipt.write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps({'label':label,'exitCode':result.returncode,'binariesUnchanged':data['binaryBefore']==data['binaryAfter']}));print(output[-12000:])
sys.exit(result.returncode)
