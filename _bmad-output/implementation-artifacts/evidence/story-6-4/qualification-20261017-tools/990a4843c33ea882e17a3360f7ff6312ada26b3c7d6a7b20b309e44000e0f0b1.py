from pathlib import Path
from collections import Counter
import hashlib,json,math,sys
p=Path(sys.argv[1]); receipt=int(sys.argv[2]); j=json.loads(p.read_text());obs=j['observations']
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
 assert len(obs)==49 and len(costs)==7
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
print(json.dumps({'sourceSnapshot':j['sourceSnapshot'],'nativeExitCode':j['exitCode'],'vectors':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'observations':dict(Counter(o['kind'] for o in obs)),'currentRowsIndependentlyAudited':len(rows),'nonemptyTailRowsIndependentlyAudited':sum(r['delta']>0 for r in costs),'result':'pass','nativeGateOutcomeSeparate':True,'limits':'Cumulative counters independently evaluated; exact per-pass B/V assertions remain owned by executable fixtures.'},indent=2))
