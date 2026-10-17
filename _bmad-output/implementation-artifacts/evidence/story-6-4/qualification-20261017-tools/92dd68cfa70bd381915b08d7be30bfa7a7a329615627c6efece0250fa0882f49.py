from pathlib import Path
from collections import Counter
import hashlib,json,math,sys
p=Path(sys.argv[1]); receipt=int(sys.argv[2]); j=json.loads(p.read_text());obs=j['observations']
allow_missing_replica='--missing-replica-witness' in sys.argv[3:]
missing_witnesses=[]
rows=[o['value'] for o in obs if o['kind']=='ProjectionGuardCurrentMatrix']
zero='SourceRanges SourceEventReads SourceDeserializations SourceReturnedEvents SourceLookaheadEvents SourceEventHashes SourceChainExtensions RouteEventHashes WireEvents ProofWriteAttempts MutationAttempts MutationBytes TransactionAttempts HandlerAttempts FoldAttempts BatchExecutions ModelMutations SchedulerScans SchedulerWriteAttempts SchedulerCasAttempts Activations ManifestOperations RetryActivations GuardActivationAttempts WriterActivationAttempts ReconciliationWrites SourceBootstrapAttempts BootstrapStateReads SourceAnchorWriteAttempts BootstrapSaveAttempts ReplayPartUploadAttempts ActivationScans ActivationWriteAttempts'.split()
assert j['sourceSnapshot'].startswith('qualification-20261017-')
assert Counter((r['head'],r['pageSize'],r['receiptLimit']) for r in rows)==Counter((h,p,receipt) for h in (1,256,257,9999,10000) for p in (1,16,256))
for r in rows:
 c=r['cost'];h=r['head'];s=min(4194304,16384+80*h+512*min(h,receipt))
 assert r['checkpoint']==h and c['SourceInspections']==2 and c['ProofReads']==1 and c['MarkerReads']==2
 assert c['SourceMetadataReads']+c['ProofReads']+c['MarkerReads']+c['MetadataReads']==11
 assert all(c[k]==0 for k in zero)
 assert c['ProofReadBytes']<=s and sum(c[k] for k in ('SourceMetadataBytes','ProofReadBytes','MarkerReadBytes','MetadataReadBytes'))<=s+114688
 assert c['ProofVisits']<=24*(h+min(h,receipt))
 n=r['native'];assert n['primary/caller_actor_http/InspectSourceProofAsync']==2 and n['primary/caller_actor_http/ReadSnapshotAsync']==2
 assert n['primary/sdk_http/actor_state_get']==2 and n['primary/sdk_http/actor_state_transaction']==0
 assert n['primary/caller_actor_http/ReadMeasuredEventsBoundedRangeAsync']==0
costs=[o['value'] for o in obs if o['kind']=='ProjectionGuardLiveCost']
if receipt==256:
 expected=Counter({'ProjectionGuardActivationClosure':1,'ProjectionSourceCost':2,'ProjectionGuardNativeAttempts':9,'ProjectionGuardTypedFailure':1,'ProjectionGuardCurrentMatrix':15,'ProjectionGuardEmptyCost':1,'ProjectionReplayRecoveryAttempt':12,'ProjectionGuardCatalogColdWarm':1,'ProjectionGuardLiveCost':7})
 if allow_missing_replica:
  import xml.etree.ElementTree as ET
  native_xml=p.with_name(p.name.replace('-vectors.json','.xml'))
  test=next(t for t in ET.parse(native_xml).getroot().findall('.//test') if t.get('name','').endswith('SourceGuard_TwoRealReplicasResumeOriginalCommittedEventReceiptsWithoutRepeatingEffects'))
  assert test.get('result')=='Fail' and j['exitCode']!=0
  message=test.findtext('failure/message','');assert 'failed to create watcher' in message and 'no space left on device' in message
  expected['ProjectionGuardNativeAttempts']=8
  missing_witnesses=[{'kind':'ProjectionGuardNativeAttempts','mode':'two_replica_original_receipt_recovery','status':'unproven; replica fixture watcher startup failed','nativeTest':test.get('name'),'nativeXml':str(native_xml),'nativeXmlSha256':hashlib.sha256(native_xml.read_bytes()).hexdigest()}]
 assert Counter(o['kind'] for o in obs)==expected and len(costs)==7
 native_attempts=[o['value'] for o in obs if o['kind']=='ProjectionGuardNativeAttempts']
 assert sum(row.get('mode')=='oversized_range_rejection' for row in native_attempts)==1
 assert sum(row.get('mode')=='two_replica_original_receipt_recovery' for row in native_attempts)==(0 if allow_missing_replica else 1)
 assert Counter((row['head'],row['checkpoint'],row['pageSize']) for row in native_attempts if 'pageSize' in row)==Counter((row['head'],row['checkpoint'],row['pageSize']) for row in costs)
 assert Counter((r['checkpoint'],r['delta'],r['pageSize']) for r in costs)==Counter([(1,0,1),(1,1,1),(1,256,256),(1,257,256),(1,771,256),(9000,771,256),(9999,1,256)])
 for r in costs:
  if r['delta']==0:continue
  c=r['cost'];d=r['delta'];h=r['head'];q=math.ceil(d/r['pageSize']);S=min(4194304,16384+80*h+512*min(h,r['receiptLimit']));limit=64+32+16*d+8*(2*d)
  assert c['SourceInspections']<=3 and c['SourceRanges']<=3*q and c['SourceReturnedEvents']<=3*d
  assert c['SourceLookaheadEvents']<=3*max(q-1,0) and c['SourceEventReads']<=3*d+3*max(q-1,0)
  assert c['SourceEventHashes']<=3*d+3*max(q-1,0) and c['SourceMetadataReads']<=3+6*q
  assert c['HandlerAttempts']<=d and c['BatchExecutions']<=d and c['ModelMutations']<=2*d
  assert c['ProofReads']<=8 and c['ProofWriteAttempts']<=4 and c['MarkerReads']==2*c['ProofReads']
  assert c['MetadataReads']<=limit and c['MutationAttempts']<=limit
  assert c['ProofReadBytes']<=8*S and c['ProofWriteBytes']<=4*S and c['MarkerReadBytes']<=16*4096
  assert c['ProofVisits']<=24*(h+min(h,r['receiptLimit'])+d)
  assert c['SourceResponseBytes']<=3*67108864 and c['WireBytes']<=3*67108864
  assert c['SourceChainExtensions']<=3*d and c['RouteEventHashes']<=3*d
else:assert len(obs)==15 and not costs
print(json.dumps({'sourceSnapshot':j['sourceSnapshot'],'nativeExitCode':j['exitCode'],'vectors':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'observations':dict(Counter(o['kind'] for o in obs)),'currentRowsIndependentlyAudited':len(rows),'nonemptyTailRowsIndependentlyAudited':sum(r['delta']>0 for r in costs),'result':'pass-for-observed-current-and-tail-rows' if missing_witnesses else 'pass','fullVectorWitnessInventoryComplete':not missing_witnesses,'missingWitnesses':missing_witnesses,'nativeGateOutcomeSeparate':True,'limits':'Cumulative counters independently evaluated; exact per-pass B/V assertions remain owned by executable fixtures.'},indent=2))
