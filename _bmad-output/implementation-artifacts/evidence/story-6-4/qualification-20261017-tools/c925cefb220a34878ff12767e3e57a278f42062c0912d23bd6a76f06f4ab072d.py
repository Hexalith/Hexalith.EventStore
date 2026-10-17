import collections, datetime, hashlib, json, os, pathlib, re, subprocess, sys, xml.etree.ElementTree as ET
ROOT=pathlib.Path('/home/administrator/projects/hexalith/eventstore')
EVIDENCE=ROOT/'_bmad-output/implementation-artifacts/evidence/story-6-4'
PREFIX='qualification-20261017-'
MODULES=['Hexalith.Tenants','Hexalith.AI.Tools','Hexalith.Commons','Hexalith.Builds','Hexalith.FrontComposer','Hexalith.PolymorphicSerializations','Hexalith.Memories','Hexalith.Platform','Hexalith.McpCli']
PROJECTS=['Hexalith.EventStore.'+p+'.Tests' for p in ('Server','DomainService','Client','Contracts','Server.LiveSidecar')]
ALLOWED={'ProjectionGuardDepthAllocation','ProjectionCost','ProjectionRetryCost','ProjectionSchedulerCost','ProjectionSchedulerMutationCost','ProjectionReplayContext','ProjectionTransitionCapCost','SourceMetadataWriterProfile','ProjectionGuardActivationClosure','ProjectionGuardCatalogColdWarm','ProjectionGuardCurrentMatrix','ProjectionGuardEmptyCost','ProjectionGuardLiveCost','ProjectionGuardNativeAttempts','ProjectionGuardTypedFailure','ProjectionReplayRecoveryAttempt','ProjectionSourceCost'}
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def sha(path):return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()
def identity(path):return {'path':str(pathlib.Path(path).relative_to(ROOT)) if pathlib.Path(path).is_relative_to(ROOT) else str(path),'bytes':pathlib.Path(path).stat().st_size,'sha256':sha(path)}
def command_text(argv,cwd=ROOT):return subprocess.run(argv,cwd=cwd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True).stdout.strip()
def files(base,extensions=None):
 if not base.exists():return []
 found=[]
 for folder,dirs,names in os.walk(base):
  dirs[:]=[d for d in dirs if d not in {'bin','obj','.git','node_modules','references','.agents','.codex','_bmad','_bmad-output','TestResults'}]
  for name in names:
   p=pathlib.Path(folder)/name
   if extensions is None or p.suffix in extensions:found.append(p)
 return found

def source():
 paths=[]
 for base in ['src','tests','samples','scripts','tools','.github','docs']:
  paths+=files(ROOT/base,{'.cs','.csproj','.props','.targets','.json','.slnx','.py','.sh','.yml','.yaml','.md','.ps1','.mjs'})
 for module in MODULES:
  base=ROOT/'references'/module
  paths+=files(base,{'.cs','.csproj','.props','.targets','.json','.slnx'})
 paths += [p for p in ROOT.iterdir() if p.is_file() and (p.suffix in {'.props','.targets','.json','.slnx','.mjs'} or p.name in {'.editorconfig','.gitattributes','.gitmodules','AGENTS.md'})]
 paths += [ROOT/'_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md']
 return {str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths)) if p.is_file()}

def assets():
 paths=[]
 for base in [ROOT/'src',ROOT/'tests',ROOT/'samples']+[ROOT/'references'/m for m in MODULES]:
  if not base.exists():continue
  for folder,dirs,names in os.walk(base):
   dirs[:]=[d for d in dirs if d not in {'bin','.git','node_modules','references','.agents','.codex','_bmad','_bmad-output'}]
   if 'project.assets.json' in names:paths.append(pathlib.Path(folder)/'project.assets.json')
 return {str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))}

def runtime():
 paths=[]
 bases=[ROOT/'tests'/p/'bin'/c/'net10.0' for p in PROJECTS for c in ['Debug','Release']]
 bases += [ROOT/b/'bin'/c/'net10.0' for b in ['src/Hexalith.EventStore','samples/Hexalith.EventStore.Sample'] for c in ['Debug','Release']]
 for base in bases:
  if base.exists():paths += [p for p in base.rglob('*') if p.is_file() and p.suffix in {'.dll','.pdb','.json'}]
 return {str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))}

def graph():
 result={'head':command_text(['git','rev-parse','HEAD']),'status':command_text(['git','status','--short']),'trackedIndexSha256':hashlib.sha256(subprocess.check_output(['git','ls-files','-s','-z'],cwd=ROOT)).hexdigest(),'modules':{}}
 for m in MODULES:
  base=ROOT/'references'/m
  result['modules'][m]={'head':command_text(['git','rev-parse','HEAD'],base),'status':command_text(['git','status','--short','--untracked-files=no'],base)}
 return result

def differences(before,after):return [p for p in sorted(before.keys()|after.keys()) if before.get(p)!=after.get(p)]
def capture(name,command,kind='test',environment=None,producer=None):
 target=EVIDENCE/(PREFIX+name+'.json');log=EVIDENCE/(PREFIX+name+'.log')
 assert not target.exists() and not log.exists(),('NO_REPEAT',name)
 before={'sourceInputs':source(),'assets':assets(),'runtime':runtime(),'graph':graph()}
 entry={'name':name,'kind':kind,'command':command,'environment':environment or {},'startedAt':now(),'before':before,'producer':producer,'wrapperSha256':sha(__file__)}
 (EVIDENCE/(PREFIX+'active.json')).write_text(json.dumps({'name':name,'command':command,'startedAt':entry['startedAt']},indent=2)+'\n')
 env=os.environ.copy();env.update(environment or {})
 with log.open('w') as output:
  completed=subprocess.run(command,cwd=ROOT,env=env,stdout=output,stderr=subprocess.STDOUT)
 entry.update(endedAt=now(),exitCode=completed.returncode,log=identity(log),after={'sourceInputs':source(),'assets':assets(),'runtime':runtime(),'graph':graph()})
 for category in ('sourceInputs','assets','runtime'):
  entry[category+'Changes']=differences(before[category],entry['after'][category])
 text=log.read_text(errors='replace')
 summaries=list(re.finditer(r'Total: (\d+), Errors: (\d+), Failed: (\d+), Skipped: (\d+), Not Run: (\d+), Time: ([\d.]+)s',text))
 if summaries:
  match=summaries[-1];n=list(map(int,match.groups()[:5]));summary=dict(zip(['selected','errors','failed','skipped','notRun'],n));summary['passed']=n[0]-n[2]-n[3]-n[4];summary['seconds']=float(match.group(6));entry['summary']=summary
 entry['diagnostics']={'warningLines':len(re.findall(r'\bwarning [A-Z][A-Z0-9]+\d+:',text)),'errorLines':len(re.findall(r'\berror [A-Z][A-Z0-9]+\d+:',text)),'buildSummaries':re.findall(r'\d+ Warning\(s\)|\d+ Error\(s\)',text),'failedMethods':re.findall(r'^\s+(Hexalith\.[^\n]+?) \[FAIL\]',text,re.M),'skippedMethods':re.findall(r'^\s+(Hexalith\.[^\n]+?) \[SKIP\]',text,re.M)}
 observations=[]
 for line in text.splitlines():
  match=re.search(r'\[OUTPUT\] (\w+) (\{.*\})$',line)
  if match and match[1] in ALLOWED:
   observations.append({'kind':match[1],'value':json.loads(match[2])})
 if observations:
  vector=EVIDENCE/(PREFIX+name+'-vectors.json');vector.write_text(json.dumps({'sourceSnapshot':PREFIX+name,'exitCode':completed.returncode,'command':command,'observations':observations},indent=2)+'\n');entry['vectors']=identity(vector);entry['observationCounts']=dict(collections.Counter(o['kind'] for o in observations))
 xml=EVIDENCE/(PREFIX+name+'.xml')
 if xml.exists():
  entry['nativeXml']=identity(xml)
  try:
   assembly=ET.parse(xml).getroot().find('assembly');entry['nativeXmlSummary']=assembly.attrib if assembly is not None else {}
  except ET.ParseError as error:entry['nativeXmlParseError']=str(error)
 target.write_text(json.dumps(entry,indent=2)+'\n')
 (EVIDENCE/(PREFIX+'active.json')).unlink()
 print(json.dumps({'name':name,'exitCode':completed.returncode,'summary':entry.get('summary'),'diagnostics':entry['diagnostics'],'sourceChanges':len(entry['sourceInputsChanges']),'assetChanges':len(entry['assetsChanges']),'runtimeChanges':len(entry['runtimeChanges']),'observations':entry.get('observationCounts')}),flush=True)
 return entry

if __name__=='__main__':
 name,kind=sys.argv[1:3];result=capture(name,sys.argv[3:],kind);sys.exit(result['exitCode'])
