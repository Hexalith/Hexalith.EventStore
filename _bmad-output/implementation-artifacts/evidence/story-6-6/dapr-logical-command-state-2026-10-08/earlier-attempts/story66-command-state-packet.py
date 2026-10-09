import gzip,hashlib,json,pathlib,re,shutil
r=pathlib.Path('/home/administrator/projects/hexalith/eventstore');p=r/'_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-command-state-2026-10-08';p.mkdir(exist_ok=True)
qualified={'pre-accounting-release-private':'guards-release-final','post-accounting-release-private':'post-accounting-release3','callback-repair-release-private':'final-release','final-behavioral-release-private':'final-behavioral-release','final-debug-private':'final-debug','established-reconstruction-private':'established-reconstruction3','established-model-private':'established-model','static-checks':'final-static'}
for dest,suffix in qualified.items():
 source=pathlib.Path('/tmp/story66-command-state-'+suffix);target=p/dest;target.mkdir(exist_ok=True)
 for src in source.iterdir():
  if src.is_file() and src.suffix in ['.log','.json','.py']:
   if src.suffix=='.json':
    with gzip.open(target/(src.name+'.gz'),'wb') as f:f.write(src.read_bytes())
   else:shutil.copyfile(src,target/src.name)
 receipt=source/'result.json'
 if receipt.exists():
  x=json.loads(receipt.read_text());summary={k:x[k] for k in ['scope','configuration','dependencyMode','control','controls','controlSummaries','timeoutSecondsPerLane','controlOnly','selectedMutations','rootInputSetUnchanged','excludedExternalPaths','retainedExternalDependencyNames'] if k in x}
  summary['mutations']=[{k:v for k,v in row.items() if k not in ['failureExcerpt']} for row in x['mutations']]
  if 'allRootInputsSha256Before' in x:
   a=x['allRootInputsSha256Before'];b=x['allRootInputsSha256After'];summary['rootChangedPaths']=[k for k in sorted(set(a)|set(b)) if a.get(k)!=b.get(k)]
  summary['dynamicCommandSetsStable']=all(c.get('dynamicLaneInputSetUnchanged',False) and c.get('dynamicImportedHelperSetUnchanged',False) and c.get('dynamicExecutedDllSetUnchanged',False) for c in x.get('commands',[]))
  if 'isolation-wrapper-receipt.json' in [f.name for f in source.iterdir()]:
   w=json.loads((source/'isolation-wrapper-receipt.json').read_text());summary['isolationWrapper']={k:w[k] for k in ['status','selectedEstablishedLanes','selectionMethod','excludedExternalPaths','retainedExternalDependencyNames','rootInputSetUnchanged']};summary['dynamicCommandSetsStable']=all(c.get('dynamicLaneInputSetUnchanged',False) and c.get('dynamicImportedHelperSetUnchanged',False) and c.get('dynamicExecutedDllSetUnchanged',False) for c in w.get('commands',[]))
  summary['successfulControlSummaries']={f.name:re.findall(r'Total:\s*(\d+), Errors:\s*(\d+), Failed:\s*(\d+), Skipped:\s*(\d+), Not Run:\s*(\d+)',f.read_text()) for f in source.glob('*control*tests.log')}
  (target/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
# Keep earlier results with their execution-time byte scope. Never relabel them final-byte controls.
previous=p/'earlier-attempts';previous.mkdir(exist_ok=True)
for source in sorted(pathlib.Path('/tmp').glob('story66-command-state-*')):
 if source.is_dir() and source.name not in ['story66-command-state-'+x for x in qualified.values()] and source.name!='story66-command-state-final-style':
  target=previous/source.name;target.mkdir(exist_ok=True)
  for src in source.iterdir():
   if src.is_file() and src.suffix in ['.json','.log','.py']:
    if src.suffix=='.json':
     with gzip.open(target/(src.name+'.gz'),'wb') as f:f.write(src.read_bytes())
    else:shutil.copyfile(src,target/src.name)
for source in pathlib.Path('/tmp').glob('story66-command-state-*'):
 if source.is_file() and source.suffix in ['.json','.log','.py']:
  shutil.copyfile(source,previous/source.name)
shutil.copyfile('/tmp/story66-established-guard-wrapper.py',p/'established-guard-wrapper.py')
owned=[
'src/Hexalith.EventStore.Client/Aggregates/RegisteredLogicalReplayBinding.cs','src/Hexalith.EventStore.Client/Events/DaprLogicalClaimTrust.cs','src/Hexalith.EventStore.Client/Events/DaprLogicalEncodedClaim.cs',
'src/Hexalith.EventStore.DomainService/BoundedV1DomainResultProducer.cs','src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs','src/Hexalith.EventStore.DomainService/Hexalith.EventStore.DomainService.csproj',
'src/Hexalith.EventStore.Server/Events/DaprReplayOperationOwner.cs','src/Hexalith.EventStore.Server/Events/DaprReplayLedgerCodec.cs','src/Hexalith.EventStore.Server/Events/DaprReplayOperationRecord.cs','src/Hexalith.EventStore.Server/Events/DaprReplayPageLedger.cs','src/Hexalith.EventStore.Server/Events/DaprReplayPreparedTransition.cs',
'scripts/verify-dapr-logical-command-state-guards.py','scripts/verify-dapr-logical-command-state-vectors.py','scripts/verify-dapr-logical-model-guards.py','scripts/verify-dapr-logical-reconstruction-guards.py','.github/workflows/event-evolution-local-guards.yml',
'_bmad-output/implementation-artifacts/story-6-6-dapr-logical-command-state-model.md','_bmad-output/implementation-artifacts/spec-6-6-event-versioning-and-upcasting-implementation.md','tests/Hexalith.EventStore.Server.Tests/Events/DaprLogicalReconstructionFixture.cs']
owned+=json.loads(pathlib.Path('/tmp/story66-command-state-final-style/owned-files.json').read_text());owned=sorted(set(owned))
snap=p/'owned-source-snapshot';snap.mkdir(exist_ok=True)
for filename in owned:
 target=snap/filename;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(r/filename,target)
(p/'owned-file-inventory.json').write_text(json.dumps({'scope':'current command-state slice; preserved unrelated Security/Streams files excluded from ownership','files':{n:hashlib.sha256((r/n).read_bytes()).hexdigest() for n in owned},'frozenIntentSha256':'de1751b9887e48f8302be092797eba2ab5bbb2595958dab3d7325bd380cf548d'},indent=2)+'\n')
print('copied',len(owned),'owned inputs')
