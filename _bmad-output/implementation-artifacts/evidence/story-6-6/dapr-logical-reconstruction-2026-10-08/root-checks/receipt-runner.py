from pathlib import Path
import datetime, hashlib, json, subprocess, sys
root=Path('/home/administrator/projects/hexalith/eventstore'); out=Path('/tmp/story66-reconstruction-root-final');out.mkdir(exist_ok=True)
files=[p for d in ('src','tests','samples') for p in (root/d).rglob('*') if p.is_file() and not {'bin','obj'}.intersection(p.parts) and p.suffix in ('.cs','.csproj','.props','.targets','.json')]
files += [root/p for p in ('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','Hexalith.EventStore.slnx','.editorconfig','.gitattributes','.github/workflows/event-evolution-local-guards.yml','scripts/verify-dapr-logical-model-guards.py','scripts/verify-dapr-logical-reconstruction-guards.py','scripts/verify-dapr-logical-model-vectors.py','scripts/verify-dapr-logical-reconstruction-vectors.py')]
files += [p for p in (root/'references/Hexalith.Builds').rglob('*') if p.is_file() and '.git' not in p.parts and p.suffix in ('.props','.targets','.json','.config')]
def hashes(paths):return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(paths))}
receipt=out/'commands.json';records=json.loads(receipt.read_text()) if receipt.exists() else []
def run(argv,label):
 before=hashes(files);dlls=list((root/argv[1]).parent.glob('*.dll')) if argv[0]=='dotnet' and argv[1].endswith('.dll') else [];dllbefore=hashes(dlls)
 start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (out/(label+'.log')).open('w') as log:r=subprocess.run(argv,cwd=root,stdout=log,stderr=subprocess.STDOUT,check=False)
 after=hashes(files);dllafter=hashes(dlls)
 record=dict(argv=argv,cwd=str(root),startedUtc=start,endedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),exitCode=r.returncode,log=label+'.log',sourceInputsSha256Before=before,sourceInputsSha256After=after,sourceInputsUnchanged=before==after,executedDllsSha256Before=dllbefore,executedDllsSha256After=dllafter,executedDllsUnchanged=dllbefore==dllafter)
 records.append(record);receipt.write_text(json.dumps(records,indent=2)+'\n');print(label, r.returncode, 'inputs stable',before==after,'DLLs stable',dllbefore==dllafter,flush=True)
 if r.returncode or before!=after or dllbefore!=dllafter:raise SystemExit(1)
mode=sys.argv[1]
if mode=='server':
 run(['dotnet','build','tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj','--configuration','Debug','-p:UseHexalithProjectReferences=true','-m:1','--nologo'],'server-debug-source-build')
 run(['dotnet','tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll',*[a for name in ('Events.DaprLogicalReconstructionTests','Events.DaprLogicalReconstructionCodecTests','Events.DaprLogicalReplayTests','DomainServices.DaprAggregateStateReconstructorTests') for a in ('-class','Hexalith.EventStore.Server.Tests.'+name)]],'server-focused-tests')
if mode=='client':
 run(['dotnet','build','tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj','--configuration','Debug','-p:UseHexalithProjectReferences=true','-m:1','--nologo'],'client-debug-source-build')
 run(['dotnet','tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll',*[a for name in ('EventEvolutionServiceTests','EventRuntimeOptionsBindingTests','DaprLogicalClaimCodecTests') for a in ('-class','Hexalith.EventStore.Client.Tests.Events.'+name)]],'client-affected-tests')
if mode=='release':run(['dotnet','build','Hexalith.EventStore.slnx','--configuration','Release','-p:UseHexalithProjectReferences=false','-warnaserror','-m:1','--nologo'],'solution-release-package-build')
if mode=='checks':
 run(['python3','scripts/verify-dapr-logical-model-vectors.py'],'logical-model-vectors')
 run(['python3','scripts/verify-dapr-logical-reconstruction-vectors.py'],'logical-reconstruction-vectors')
 run(['actionlint','.github/workflows/event-evolution-local-guards.yml'],'workflow-actionlint')
