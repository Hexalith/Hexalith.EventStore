from pathlib import Path
from collections import Counter
import json,hashlib,math,sys
f=Path(sys.argv[1]);b=f.read_bytes();j=json.loads(b);assert j['sourceSnapshot'].startswith('qualification-20261017-');obs=j['observations']
rows=[x['value'] for x in obs if x['kind']=='ProjectionCost' and x['value']['mode']=='current'];assert len(rows)==30
assert Counter((r['head'],r['pageSize'],r['receiptLimit']) for r in rows)==Counter((h,p,R) for h in (1,256,257,9999,10000) for p in (1,16,256) for R in (256,4096))
zero='SourceRanges SourceEventReads SourceDeserializations SourceReturnedEvents SourceLookaheadEvents SourceEventHashes SourceChainExtensions RouteEventHashes WireEvents ProofWriteAttempts MutationAttempts MutationBytes TransactionAttempts HandlerAttempts FoldAttempts BatchExecutions ModelMutations SchedulerScans SchedulerWriteAttempts SchedulerCasAttempts Activations ManifestOperations RetryActivations GuardActivationAttempts WriterActivationAttempts ReconciliationWrites SourceBootstrapAttempts BootstrapStateReads SourceAnchorWriteAttempts BootstrapSaveAttempts ReplayPartUploadAttempts ActivationScans ActivationWriteAttempts'.split()
for r in rows:
 c=r['cost'];h=r['head'];R=r['receiptLimit'];S=min(4194304,16384+80*h+512*min(h,R));assert r['completed']==h and r['routeCount']==1
 assert (c['SourceInspections'],c['ProofReads'],c['MarkerReads'])==(2,1,2)
 assert c['SourceMetadataReads']+c['ProofReads']+c['MarkerReads']+c['MetadataReads']==11
 assert all(c[k]==0 for k in zero);assert c['ProofReadBytes']<=S
 assert sum(c[k] for k in ['SourceMetadataBytes','ProofReadBytes','MarkerReadBytes','MetadataReadBytes'])<=S+114688
 assert c['ProofVisits']<=24*(h+min(h,R))
tails=[x['value'] for x in obs if x['kind']=='ProjectionCost' and x['value']['mode']=='tail'];assert Counter(r['routeCount'] for r in tails)=={1:8,3:2}
for r in tails:
 c=r['cost'];h=r['head'];d=h-r['completed'];R=r['receiptLimit'];Q=math.ceil(d/r['pageSize']);S=min(4194304,16384+80*h+512*min(h,R));routes=r['routeCount'];passes=3 if routes==1 else 7
 assert c['SourceInspections']<=3*routes and c['SourceRanges']<=passes*Q and c['SourceReturnedEvents']<=passes*d
 assert c['SourceLookaheadEvents']<=passes*max(Q-1,0) and c['SourceEventReads']<=passes*d+passes*max(Q-1,0) and c['SourceEventHashes']<=passes*d+passes*max(Q-1,0)
 assert c['SourceMetadataReads']<=routes*(3+6*Q) and c['HandlerAttempts']<=routes*d and c['BatchExecutions']<=routes*d and c['ModelMutations']<=routes*2*d
 assert c['ProofReads']<=routes*8 and c['ProofWriteAttempts']<=routes*4 and c['MarkerReads']==2*c['ProofReads']
 assert c['MetadataReads']<=routes*(96+32*d) and c['MutationAttempts']<=routes*(96+32*d)
 assert c['ProofReadBytes']<=routes*8*S and c['ProofWriteBytes']<=routes*4*S and c['MarkerReadBytes']<=routes*16*4096
 assert c['ProofVisits']<=routes*24*(h+min(h,R)+d)
 assert c['SourceResponseBytes']<=passes*67108864 and c['WireBytes']<=passes*67108864 and c['SourceChainExtensions']<=passes*d and c['RouteEventHashes']<=3*routes*d
alloc=[x['value'] for x in obs if x['kind']=='ProjectionGuardDepthAllocation'];assert len(alloc)==2 and {r['nestedUnknownDepth'] for r in alloc}=={0,96}
for r in alloc:assert r['maxDepth']==1000000 and r['allocatedBytes']<=r['regressionBound']==65536+32*r['bodyBytes']

scheduler=[x['value'] for x in obs if x['kind']=='ProjectionSchedulerCost']
if scheduler:
 assert len(scheduler)==2 and {r['uneven'] for r in scheduler}=={False,True}
 for r in scheduler:
  assert r['shards']==r['reads']==64 and r['items']==4096+(64 if r['uneven'] else 0)
  assert r['bytes']<=16809984+(262144 if r['uneven'] else 0) and r['writeAttempts']==0
mutations=[x['value'] for x in obs if x['kind']=='ProjectionSchedulerMutationCost']
if mutations:
 assert Counter((r['uneven'],r['refused']) for r in mutations)==Counter((u,f) for u in [False,True] for f in [0,4])
 for r in mutations:
  c=r['cost'];n=r['refused'];assert r['occupancy']==(128 if r['uneven'] else 64)
  assert c['SchedulerShardReads']==68+n and c['SchedulerReadBytes']==r['scanBytes']+r['before']*(1+n)+r['leased']+r['updated']+r['leasedAgain']
  assert c['SchedulerWriteAttempts']==4+n and c['SchedulerWriteBytes']==sum(r['attemptedWriteBytes'])==r['leased']*(1+n)+r['updated']+r['leasedAgain']+r['final']
  assert c['SchedulerCasAttempts']==8+n and all(v<=512+4096*r['occupancy'] for v in r['attemptedWriteBytes'])
retries=[x['value'] for x in obs if x['kind']=='ProjectionRetryCost']
if retries:
 assert len(retries)==5
 for r in retries:
  c=r['cost'];assert r['activations']==9 and r['saturatedAttempt']==8
  assert c['RetryActivations']==c['SchedulerScans']==9 and c['SchedulerShardReads']==9*66 and c['SchedulerWriteAttempts']==18 and c['SchedulerReadBytes']>0

print(json.dumps({'gate':j['sourceSnapshot'],'nativeExitCode':j['exitCode'],'vectorSha256':hashlib.sha256(b).hexdigest(),'kinds':dict(Counter(x['kind'] for x in obs)),'independentCurrentRows':len(rows),'independentSingleRouteUniformTailRows':8,'independentSharedThreeRouteRows':2,'finiteDepthAllocationCases':alloc,'result':'pass','scope':'Numerical observation audit only; native gate outcome remains separate. Exact per-route allocations and B/V ceilings remain fixture assertions.'},indent=2))
