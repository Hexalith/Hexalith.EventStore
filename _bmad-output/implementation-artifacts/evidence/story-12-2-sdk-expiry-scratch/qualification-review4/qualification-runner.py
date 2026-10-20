import json,hashlib,os,subprocess,time,sys,xml.etree.ElementTree as ET
from pathlib import Path
from datetime import datetime,timezone
root=Path.cwd(); packet=root/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review4'; packet.mkdir(exist_ok=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(name,value): (packet/name).write_text(json.dumps(value,indent=2)+'\n')
def now():return datetime.now(timezone.utc).isoformat()
old=root/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review3'
paths={r['path'] for r in json.load(open(old/'source-freeze.json'))}
for directory in ['src','tests','samples']:
 for p in (root/directory).rglob('*'):
  if p.is_file() and not {'bin','obj','.git','node_modules'}.intersection(p.parts) and p.suffix in ['.cs','.csproj','.props','.targets','.json','.resx','.xml','.yml','.yaml','.razor','.html','.css','.js','.md','.txt','.sh','.ps1','.py']:
   paths.add(str(p.relative_to(root)))
def inventory():return [dict(path=p,bytes=(root/p).stat().st_size,sha256=sha(root/p)) for p in sorted(paths) if (root/p).is_file()]
freeze=inventory();save('source-freeze.json',freeze)
commands=[]
projects=['Hexalith.EventStore.Client.Tests','Hexalith.EventStore.DomainService.Tests','Hexalith.EventStore.Server.LiveSidecar.Tests']
def execute(id,cmd,env=None,testproject=None):
 r={'id':id,'command':cmd,'environment':env or {},'cwd':str(root),'started_utc':now()}; print('START',id,flush=True)
 with open(packet/(id+'.log'),'w') as log:proc=subprocess.run(cmd,cwd=root,env={**os.environ,**(env or {})},stdout=log,stderr=subprocess.STDOUT)
 r.update(exit_code=proc.returncode,finished_utc=now(),log=id+'.log',log_sha256=sha(packet/(id+'.log')))
 if testproject:
  xml=packet/(id+'.xml');a=ET.parse(xml).getroot().find('assembly');r['result']={k:int(a.attrib.get(k,'0')) for k in ['total','passed','failed','skipped','errors','not-run']};assert int(a.attrib.get('total','0'))>0; r['xml']=xml.name;r['xml_sha256']=sha(xml)
  folder=root/'tests'/testproject/'bin/Debug/net10.0'
  binary=[dict(path=str(p.relative_to(root)),sha256=sha(p),bytes=p.stat().st_size) for p in sorted(folder.glob('Hexalith.*')) if p.suffix in ['.dll','.pdb','.xml','.json']];save(id+'-binary-group.json',binary);r['binary_group']=id+'-binary-group.json'
 commands.append(r);save('commands.json',commands);print('END',id,proc.returncode,r.get('result',''),flush=True)
 if proc.returncode:sys.exit(proc.returncode)
for short,project in [('client',projects[0]),('domain',projects[1]),('live',projects[2])]:
 execute(short+'-build',['dotnet','build',f'tests/{project}/{project}.csproj','-c','Debug','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1'])
 if short=='client':
  execute('client-focused',['dotnet',f'tests/{project}/bin/Debug/net10.0/{project}.dll','-class','*ReadModel*','-class','*SharedProjection*','-class','*DaprReadModelBatchTests','-parallel','none','-result-xml',str(packet/'client-focused.xml')],testproject=project)
  execute('client-full',['dotnet',f'tests/{project}/bin/Debug/net10.0/{project}.dll','-parallel','none','-result-xml',str(packet/'client-full.xml')],testproject=project)
 if short=='domain':
  env={'HEXALITH_TENANTS_DOMAIN_ROOT':str(root.parent/'Hexalith.Tenants/src/Hexalith.Tenants')}
  execute('domain-full',['dotnet',f'tests/{project}/bin/Debug/net10.0/{project}.dll','-parallel','none','-result-xml',str(packet/'domain-full.xml')],env,project)
 if short=='live':
  env={'EVENTSTORE_TEST_DAPR_HOT_RELOAD':'false','EVENTSTORE_SCRATCH_PROCESS_EVIDENCE_DIR':str(packet/'process-workers')}
  for id,pattern in [('live-expiry-scratch','*ProjectionExpiryAndScratchRecoveryLiveTests'),('live-read-model-batch','*ReadModelBatchLiveSidecarTests'),('live-shared-epoch','*SharedProjectionEpochLiveSidecarTests')]:
   execute(id,['dotnet',f'tests/{project}/bin/Debug/net10.0/{project}.dll','-class',pattern,'-showliveoutput','-result-xml',str(packet/(id+'.xml'))],env,project)
final=inventory();save('source-final.json',final);save('source-drift.json',{'matches':final==freeze});assert final==freeze
print('ALL QUALIFICATION LANES PASSED',flush=True)
