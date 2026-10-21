from pathlib import Path
import subprocess,json,hashlib,datetime,re
root=Path.cwd()
base='ae917d3e4dbab20b8982c5222a13611c3e3faf35'
run=root/'_bmad-output/implementation-artifacts/evidence/folders-12-2-bounded-owner-identity/run-20261021T125151Z'
log=(run/'tests-Server.log').read_text()
line=next(s for s in log.splitlines() if 'Tracked reusable content contains secret material. Only path and line are reported: ' in s)
locations=line.split('Only path and line are reported: ',1)[1].strip().split(', ')
entries=[{'path':s.rsplit(':',1)[0],'line':int(s.rsplit(':',1)[1])} for s in locations]
paths=sorted({e['path'] for e in entries})
raw=subprocess.check_output(['git','ls-tree','-rz',base,'--',*paths])
blobs={}
for item in raw.split(b'\0'):
 if item:
  header,path=item.split(b'\t',1)
  blobs[path.decode()]=header.decode().split()[2]
working=subprocess.check_output(['git','hash-object','--no-filters','--stdin-paths'],input='\n'.join(paths)+'\n',text=True).splitlines()
historical=[{'path':p,'baselineBlob':blobs.get(p),'workingBlob':h,'matchesBaseline':blobs.get(p)==h} for p,h in zip(paths,working)]
code_paths=[
 'src/Hexalith.EventStore.Client/Projections/ReadModelBatchFingerprint.cs',
 'src/Hexalith.EventStore.DomainService/DomainProjectionReplayRecovery.cs',
 'tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionReplayRecoveryTests.cs',
 'tests/Hexalith.EventStore.Server.Tests/Projections/ReplayRecoveryProductionFixture.cs',
 *[str(p.relative_to(root)) for p in sorted((root/'tests/Hexalith.EventStore.Server.Tests/Security').glob('SecretsProtectionTests*.cs'))]]
code=[]
for p in code_paths:
 original=subprocess.check_output(['git','show',base+':'+p])
 current=(root/p).read_bytes()
 code.append({'path':p,'baselineSha256':hashlib.sha256(original).hexdigest(),'workingSha256':hashlib.sha256(current).hexdigest(),'matchesBaseline':original==current})
result={
 'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'baselineCommit':base,'run':str(run.relative_to(root)),
 'historicalLocations':entries,'historicalLocationCount':len(entries),'historicalPathCount':len(paths),
 'historicalPathComparisons':historical,'allHistoricalBytesMatchBaseline':all(e['matchesBaseline'] for e in historical),
 'codeComparisons':code,'allRelevantCodeBytesMatchBaseline':all(e['matchesBaseline'] for e in code),
 'failures':[
 {'test':'Hexalith.EventStore.Server.Tests.Security.SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets','reason':'139 tracked historical evidence path/line findings; no finding points into the changed owner identity source or new untracked packet. Scanner and findings are unchanged from the baseline.','ownerIdentityCallPathInvolved':False},
 {'test':'Hexalith.EventStore.Server.Tests.Projections.ProjectionReplayRecoveryTests.CorruptOriginalManifestOrProviderState_FailsClosedWithoutRepreparation(invalidRecord: False)','reason':'Test setup calls ComputeFromManifest on deliberately malformed JSON {sensitive-manifest at line 268. Unchanged Utf8JsonReader parsing throws JsonReaderException before SaveAsync and before the replay Commit assertion.','ownerIdentityCallPathInvolved':False}],
 'authority':'Read-only comparison; preserves broad gate failures and does not claim an overall qualified packet.'}
out=root/'_bmad-output/implementation-artifacts/evidence/folders-12-2-bounded-owner-identity/diagnostics/broad-server-failure-baseline-audit.json'
out.write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'locations':len(entries),'paths':len(paths),'allHistoricalBytesMatchBaseline':result['allHistoricalBytesMatchBaseline'],'allRelevantCodeBytesMatchBaseline':result['allRelevantCodeBytesMatchBaseline'],'report':str(out)},indent=2))
