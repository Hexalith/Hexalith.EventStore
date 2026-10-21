#!/usr/bin/env python3
"""Observe owned fixture cleanup and safe inotify restoration without changing processes."""
import collections
import datetime
import hashlib
import json
import re
import subprocess
from pathlib import Path

packet=Path(__file__).resolve().parent
baseline=json.loads((packet/'baseline-identities.json').read_text())
work=Path(json.loads((packet/'runtime-acquisition.json').read_text())['workDirectory'])
repository=next(path for path in packet.parents if (path / "Hexalith.EventStore.slnx").exists())
host_assembly=str(repository / "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.dll")
host_project=str(repository / "src/Hexalith.EventStore/Hexalith.EventStore.csproj")
host_executable=str(repository / "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore")
matching_hosts=[]
counts=collections.Counter()
total=0
candidate_processes=[]
for proc in Path('/proc').glob('[0-9]*'):
    try:
        name=(proc/'comm').read_text().strip()
        args=(proc/'cmdline').read_bytes().split(b'\0')
    except OSError:
        continue
    decoded=[argument.decode(errors='replace') for argument in args]
    if host_assembly in decoded or host_project in decoded or (decoded and decoded[0]==host_executable):
        matching_hosts.append({'processId':int(proc.name),'processName':name})
    if args and args[0].decode(errors='replace') in (str(work/'daprd-recovery'),str(work/'daprd-recovery-observed')):
        candidate_processes.append({'processId':int(proc.name),'executable':args[0].decode()})
    for fd in (proc/'fdinfo').glob('*'):
        try:
            with fd.open() as stream:
                count=sum(1 for line in stream if line.startswith('inotify '))
        except OSError:
            continue
        counts[name]+=count
        total+=count
ports=set()
for path in packet.glob('scenarios/*/manifest.json'):
    manifest=json.loads(path.read_text())
    ports.update(value for key,value in manifest.items() if key.endswith('Port') and isinstance(value,int))
    observations=manifest.get('Observations',{})
    ports.update(value for key,value in observations.items() if key.endswith('Port') and isinstance(value,int))
sockets=subprocess.run(['ss','-ltnH'],capture_output=True,text=True)
listening=set()
for line in sockets.stdout.splitlines():
    fields=line.split()
    if len(fields)>3:
        try:listening.add(int(fields[3].rsplit(':',1)[1]))
        except ValueError:pass
containers=subprocess.run(['docker','ps','--format','{{.ID}} {{.Image}} {{.Names}}'],capture_output=True,text=True)
with Path(baseline['installedRuntime']['path']).open('rb') as stream:
    runtime_hash=hashlib.file_digest(stream,'sha256').hexdigest()
result={'observedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'ownedCandidateSidecarsRemaining':candidate_processes,'matchingEventStoreHostProcessesObserved':matching_hosts,'fixturePatternDirectoriesObserved':[str(path) for path in Path('/tmp').glob('eventstore-2-13-*') if path.is_dir() and re.fullmatch('eventstore-2-13-[0-9a-f]{32}',path.name)],'recordedFixturePortsCurrentlyListening':sorted(ports & listening),'socketObservationExitCode':sockets.returncode,'installedRuntimeSha256':runtime_hash,'installedRuntimeUnchanged':runtime_hash==baseline['installedRuntime']['sha256'],'maxUserWatches':int(Path('/proc/sys/fs/inotify/max_user_watches').read_text()),'observedWatchesPerFd':total,'watchesByProcessName':{key:value for key,value in counts.items() if value},'method':'count accessible per-fd inotify watch lines; possible inherited/shared instance overcount','originalMaxUserWatches':1048576,'restorePermittedByObservedUse':total<=1048576,'sharedContainers':{'command':['docker','ps','--format','{{.ID}} {{.Image}} {{.Names}}'],'exitCode':containers.returncode,'output':containers.stdout+containers.stderr},'disposableRuntimeDirectoryRetainedForReproduction':str(work),'sourceDeclaredEmptyDirectoryExists':Path('/tmp/hexalith-eventstore-empty-dapr-resources').exists(),'sourceDeclaredEmptyDirectoryDisposition':'not removed without a retained run-start ownership observation; no unrelated process or persistent sysctl configuration changed'}
output=packet/'closing-observations.json'
assert not output.exists()
output.write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({key:result[key] for key in ['ownedCandidateSidecarsRemaining','fixturePatternDirectoriesObserved','recordedFixturePortsCurrentlyListening','installedRuntimeUnchanged','maxUserWatches','observedWatchesPerFd','restorePermittedByObservedUse']},indent=2))
