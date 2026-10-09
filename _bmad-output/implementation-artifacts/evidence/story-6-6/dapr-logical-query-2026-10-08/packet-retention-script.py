import datetime,gzip,hashlib,json,pathlib,re,shutil
r=pathlib.Path('/home/administrator/projects/hexalith/eventstore')
p=r/'_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-query-2026-10-08'
p.mkdir(exist_ok=True)
owned=json.loads(pathlib.Path('/tmp/story66-query-allman-files.json').read_text())
owned+=['src/Hexalith.EventStore.Client/Events/EventRegistryRow.cs','src/Hexalith.EventStore.DomainService/DomainQueryDispatcher.cs','src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs','src/Hexalith.EventStore.Server/Queries/QueryRouter.cs','.github/workflows/event-evolution-local-guards.yml','scripts/verify-dapr-logical-query-guards.py','scripts/verify-dapr-logical-query-vectors.py','_bmad-output/implementation-artifacts/story-6-6-dapr-logical-query-model.md',str((p/'vectors.json').relative_to(r))]
owned=sorted(set(owned))
spec=r/'_bmad-output/implementation-artifacts/spec-6-6-event-versioning-and-upcasting-implementation.md';s=spec.read_text();a=s.index('<frozen-after-approval');b=s.index('</frozen-after-approval>')+len('</frozen-after-approval>');frozen=hashlib.sha256(s[a:b].encode()).hexdigest();assert frozen=='de1751b9887e48f8302be092797eba2ab5bbb2595958dab3d7325bd380cf548d'
for name in owned:
 dst=p/'owned-source-snapshot'/name;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(r/name,dst)
(p/'owned-file-inventory.json').write_text(json.dumps({'scope':'dormant private query prerequisite; unrelated stream/security work excluded from ownership','sealedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'files':{n:hashlib.sha256((r/n).read_bytes()).hexdigest() for n in owned},'frozenIntentSha256':frozen},indent=2)+'\n')
def retain(source,dest):
 source=pathlib.Path(source);dest=p/dest;dest.mkdir(parents=True,exist_ok=True)
 for file in source.glob('*'):
  if not file.is_file():continue
  raw=file.read_bytes()
  if file.suffix=='.json':(dest/(file.name+'.gz')).write_bytes(gzip.compress(raw,mtime=0))
  else:shutil.copyfile(file,dest/file.name)
 result=source/'result.json'
 if result.exists():
  x=json.loads(result.read_text());commands=x.pop('commands',[])
  for name in ('rootInputsBefore','rootInputsAfter','allRootInputsSha256Before','allRootInputsSha256After'):x.pop(name,None)
  x['recordedCommands']=len(commands)
  x['allLaneSetsUnchanged']=all(c['dynamicLaneSetUnchanged'] for c in commands)
  x['allImportedSetsUnchanged']=all(c['dynamicImportedSetUnchanged'] for c in commands)
  x['allExecutedDllSetsUnchanged']=all(c['dynamicDllSetUnchanged'] for c in commands)
  x['exactResultBytesSha256']=hashlib.sha256(result.read_bytes()).hexdigest()
  x['commandMetadata']=[{k:v for k,v in c.items() if not k.endswith(('Before','After'))} for c in commands]
  (dest/'summary.json').write_text(json.dumps(x,indent=2)+'\n')
retain('/tmp/story66-query-final-debug3','final-debug-private')
retain('/tmp/story66-query-final-release','final-release-private')
attempts=[('story66-query-draft-control1','Failed private compilation: omitted external anchored-state dependencies; no runtime verification.'),('story66-query-draft-control2','Failed test compilation: fixture ServiceCollection.Add receiver and CA1822 helper declaration; corrected independently.'),('story66-query-draft-control3','Compiled 0 warnings/errors; 61/63 passed. Two fixture errors: root-provider legacy scope and twice-toggling retained-byte substitution. Historical draft evidence only.'),('story66-query-draft-control4','Failed private compilation: two CS0246 errors for omitted external DeletionActivationComparison.cs. Its exact current file was retained in later copies; no query workaround.'),('story66-query-draft-control5','85 query /16 Client controls passed at pre-semantic-manifest/bulk/cleanup bytes. Stable dynamic input sets; historical draft evidence only.'),('story66-query-current-debug','99 query /16 Client controls passed; 15 compiling mutants killed before metadata/bulk/direct-token cleanup. Stable root/private/helper/DLL sets; historical execution snapshot only.'),('story66-query-final-debug','Failed fixture analyzer gate CA1062 for new point.StartsWith without null validation. Corrected; no runtime qualification.'),('story66-query-final-debug2','109 query /16 Client controls passed before the final direct-store token control and refusal metadata control. Historical execution snapshot only.')]
for source,reason in attempts:
 folder=pathlib.Path('/tmp')/source
 if folder.exists():retain(folder,'earlier-attempts/'+source)
(p/'earlier-attempts/README.md').write_text('# Earlier query attempts\n\nThese retained results keep their execution-time meanings. Failed/older runs are not upgraded to current-byte qualification. Initial helper receipts used narrower governing/helper path sets; current final receipts dynamically re-enumerate lane/helper/DLL sets.\n\n'+''.join('- `'+source+'`: '+reason+'\n' for source,reason in attempts))
static=p/'static-checks';static.mkdir(exist_ok=True)
shutil.copyfile('/tmp/story66-query-final-vector.log',static/'independent-vectors.log')
shutil.copyfile('/tmp/story66-logical-query-first-build.log',static/'earlier-root-debug-build.log')
(static/'earlier-root-debug-build-command.json').write_text(json.dumps({'argv':['dotnet','build','tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj','--configuration','Debug','-p:UseHexalithProjectReferences=true','-m:1','-warnaserror'],'cwd':str(r),'result':'failed; 16 external RecoverableAnchoredState.cs CA1062 errors at that historical attempt','currentWorkspaceQualification':False,'externalSourceChangedLater':True},indent=2)+'\n')
base={}
for name in ('start.json','baseline.json'):
 path=pathlib.Path('/tmp/story66-logical-anchor-aspire-'+name);raw=path.read_bytes();x=json.loads(raw.decode()[raw.decode().index('{'):]);base[name]={'rawBytesSha256':hashlib.sha256(raw).hexdigest()}
 if name=='start.json':base[name]|={k:x[k] for k in ('appHostPath','appHostPid','cliPid')}
 else:base[name]['resources']=[{k:item.get(k) for k in ('displayName','resourceType','state','healthStatus')} for item in x['resources']]
base['scope']='Local Aspire startup/inspection/stop baseline, not production profile, activation or live query qualification; endpoint/token data omitted.'
(static/'aspire-baseline.json').write_text(json.dumps(base,indent=2)+'\n')
shutil.copyfile('/tmp/story66-logical-anchor-aspire-stop.log',static/'aspire-stop.log')
shutil.copyfile('/tmp/story66-query-allman-build.log',static/'allman-helper-build.log')
shutil.copyfile('/tmp/story66-query-allman-files.json',static/'allman-file-inventory.json')
shutil.copyfile(pathlib.Path(__file__),p/'packet-retention-script.py')
print(json.dumps({'ownedFiles':len(owned),'frozenIntentSha256':frozen,'packet':str(p)}))
