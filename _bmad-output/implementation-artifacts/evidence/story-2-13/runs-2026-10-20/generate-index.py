from pathlib import Path
import json, hashlib, datetime, re
root=Path('/home/administrator/projects/hexalith/eventstore')
p=root/'_bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-20'
commands=[]
for q in sorted(p.glob('*.json')):
 try:d=json.loads(q.read_text())
 except ValueError:continue
 if isinstance(d,dict) and 'command' in d and 'exitCode' in d:commands.append(q.name)
spec=root/'_bmad-output/implementation-artifacts/spec-2-13-dapr-notification-distribution-qualification.md'
spec_bytes=spec.read_bytes()
def frozen(blob):
 text=blob.decode();return text[text.index('<frozen-after-approval'):text.index('</frozen-after-approval>')+len('</frozen-after-approval>')].encode()
original=(p/'profile-inputs/_bmad-output/implementation-artifacts/spec-2-13-dapr-notification-distribution-qualification.md').read_bytes()
assert frozen(original)==frozen(spec_bytes)
index={
 'schemaVersion':1,'recordedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
 'sourceRevision':json.loads((p/'profile-identity.json').read_text())['sourceRevision'],
 'frozenBaseline':'f2b480b68a8bd5369b3cdf550340e0fadfb2332b',
 'frozenIntentSha256':hashlib.sha256(frozen(spec_bytes)).hexdigest(),
 'workspaceSpecSha256AtPacketClose':hashlib.sha256(spec_bytes).hexdigest(),
 'commands':commands,
 'finalChecks':['final-graph-build.json','post-audit-server-build.json','post-audit-integration-build.json','post-audit-metadata-tests.json','post-audit-redis-baseline.json','retry-distribution-tests.json','final-real-projection.json','final-ordinary-regression.json'],
 'supersededChecks':['replica-restart.json','server-build.json','solution-final-build.json','metadata-unit-tests.json','final-distribution-tests.json','final-metadata-unit-tests.json','final-redis-baseline.json','post-audit-distribution-tests.json'],
 'expectedNonPassingChecks':{'focused-build.json':'cached-package-source-asset-mismatch','post-audit-distribution-tests.json':'environment-local-grpc-port-bind-collision'},
 'reviewDecisions':{'metadataAccounting':'user-directed-serialized-limit','transportOwner':'pending','architecture':'pending','security':'pending','operations':'pending','test':'pending'},
 'rollback':'atomic-host-package-profile-transition-and-rollback-unrun; disposable replica recovery is separate',
 'contractDecision':'User: Enforce the serialized-byte limit in the approved story spec.',
 'disposition':'unresolved-no-transition-or-exception-authority',
 'remainingGates':['dynamic-topics','real-projection-scoped-detail','production-binding-issuer-and-profile','canonical-ui-freshness-and-completion','provider-network-outage-backpressure-retention','conflicting-identity-conformance','atomic-transition-and-rollback','architecture-security-operations-test-transport-owner-disposition'],
 'authority':'local consistency evidence only; no owner disposition, AD-12 CI seal, accepted exception, NFR5 closure, release or production authority',
 'artifacts':{}
}
for relative in index['finalChecks']:
 d=json.loads((p/relative).read_text());assert d['exitCode']==0,relative
for q in sorted(p.rglob('*')):
 if q.is_file() and q.name not in ['index.json','index.sha256']:
  index['artifacts'][str(q.relative_to(p))]=hashlib.sha256(q.read_bytes()).hexdigest()
(p/'index.json').write_text(json.dumps(index,indent=2)+'\n')
(p/'index.sha256').write_text(hashlib.sha256((p/'index.json').read_bytes()).hexdigest()+'  index.json\n')
print('Wrote index for',len(index['artifacts']),'retained files and',len(commands),'command receipts')
