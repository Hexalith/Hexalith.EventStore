"""Bind the current Story 8.3 candidate to immutable approvals, executed results and dependencies."""
import pathlib,json,hashlib,subprocess,re,xml.etree.ElementTree as ET,datetime,zipfile
root=pathlib.Path(__file__).resolve().parents[5];out=pathlib.Path(__file__).resolve().parent
old=json.loads((out.parent/'verification-2026-10-08-postreview/binding.json').read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def stream(files):
 files=sorted(files,key=lambda x:x['path']);return {'files':files,'count':len(files),'sha256sumStreamSha256':hashlib.sha256(''.join(f"{e['sha256']}  {e['path']}\n" for e in files).encode()).hexdigest()}
def entries(paths):return [{'path':s,'sha256':sha(root/s),'bytes':(root/s).stat().st_size} for s in sorted(set(paths))]
def trx(name, allow_existing_skips=False, preserve_failure=False):
 p=out/name;t=ET.parse(p).getroot();rows=[e.attrib for e in t.iter() if e.tag.endswith('}UnitTestResult')];counts={}
 for e in rows:counts[e['outcome']]=counts.get(e['outcome'],0)+1
 assert rows and (preserve_failure or all(e['outcome']=='Passed' or (allow_existing_skips and e['outcome']=='NotExecuted') for e in rows)),(name,counts)
 return {'path':name,'sha256':sha(p),'count':len(rows),'outcomes':counts,'rows':rows}
compiled=json.loads((out/'authorized-compiled.json').read_text());final=json.loads((out/'verified-source.json').read_text())
before=json.loads((out/'before-authorized-build.json').read_text())
mutable_closure_records={'_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md','_bmad-output/implementation-artifacts/8-3-pdenc-v2-core-cryptographic-engine.md','_bmad-output/implementation-artifacts/sprint-status.yaml'}
def source_map(snapshot):return {e['path']:e['sha256'] for e in snapshot['trackedFiles'] if e['path'] not in mutable_closure_records and not e['path'].startswith(('_bmad/', '_bmad-output/', '.agents/', '.claude/'))}
assert source_map(before)==source_map(compiled),'root source/configuration changed across final build'
live_drift=[{'path':p,'compiledSha256':source_map(compiled).get(p),'currentSha256':source_map(final).get(p)} for p in sorted(source_map(compiled).keys()|source_map(final).keys()) if source_map(compiled).get(p)!=source_map(final).get(p)]
for key in ['core','tests']:assert compiled[key]==final[key],key+' reviewed source changed since compilation'
assert before['submodules']==compiled['submodules']==final['submodules'],'dependency identity changed'
# Executed focused runtime copies must not be substituted by intervening builds or tests.
for e in compiled['buildArtifacts']:
 if '/tests/Hexalith.EventStore.PayloadProtection.Tests/bin/' in '/'+e['path']:
  assert sha(root/e['path'])==e['sha256'],e['path']
frozen=[]
mutable={'.gitattributes','.github/workflows/payload-protection.yml','scripts/ci-local.sh','tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj','tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs','_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md','_bmad-output/implementation-artifacts/8-3-pdenc-v2-core-cryptographic-engine.md'}
for p,h in old['fileHashes'].items():
 if p not in mutable:
  assert sha(root/p)==h,p+' preservation drift';frozen.append({'path':p,'sha256':h})
for e in old['approvedSecurityInventory']['files']:assert sha(root/e['path'])==e['sha256'],e['path']
spec='_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md'
def frozen_block(data):return data[data.index(b'<frozen-after-approval'):data.index(b'</frozen-after-approval>')+len(b'</frozen-after-approval>')]
prior=subprocess.check_output(['git','show',old['head']+':'+spec],cwd=root);assert frozen_block(prior)==frozen_block((root/spec).read_bytes())
core=stream(entries([e['path'] for e in old['production']['files']]));tests=stream(entries([e['path'] for e in old['tests']['files']]+['tests/Hexalith.EventStore.PayloadProtection.Tests/ClosureRegressionTests.cs']))
focused=trx('authorized-focused-results.trx.xml'); invariant=trx('authorized-invariant-results.trx.xml');contracts=trx('authorized-contracts-results.trx.xml')
prototype=[e for e in focused['rows'] if '.InteractionOccurrence' in e['testName']];owned=[e for e in focused['rows'] if e not in prototype];assert len(owned)==324 and len(prototype)==33;assert invariant['count']==1
# Bind every declared vector to at least one executed method, rather than merely counting traits.
vectors={};names=[e['testName'] for e in owned]
for e in tests['files']:
 if not e['path'].endswith('.cs'):continue
 text=(root/e['path']).read_text();class_name=pathlib.Path(e['path']).stem
 for match in re.finditer(r'\[Trait\("Vector", "(V\d+)"\)\](.*?)(?=public\s)',text,re.S):
  remaining=text[match.end():];method=re.match(r'public\s+(?:async\s+)?(?:void|Task|ValueTask)\s+(\w+)\(',remaining)
  assert method,match.group(1)+' missing runnable method'
  prefix='Hexalith.EventStore.PayloadProtection.Tests.'+class_name+'.'+method.group(1)
  rows=[n for n in names if n==prefix or n.startswith(prefix+'(')];assert rows,prefix+' did not execute'
  vectors.setdefault(match.group(1),[]).append({'method':prefix,'executedCases':len(rows)})
expected={f'V{i:03d}' for i in list(range(1,49))+[135,136,138]};assert set(vectors)==expected,(len(vectors),sorted(expected-set(vectors)))
required=['authorized-solution-build','authorized-focused-build','authorized-focused-tests','authorized-invariant-tests','authorized-contracts-tests','authorized-client-tests','authorized-deletion-tests','authorized-normal-pack','authorized-package-validation','authorized-nuget-validation','authorized-capture-remediation','authorized-secrets-tests','final-candidate-artifact-scan','authorized-evaluated-inputs','node-vectors','python-vectors-pinned','final-core-style','final-tests-style','final-whitespace','final-actionlint','final-shell-syntax','authorized-diff-check']
commands=[]
for name in required:
 data=json.loads((out/(name+'.command.json')).read_text());assert data['exitCode']==0,name;assert data['logSha256']==sha(out/(name+'.log')),name+' log changed';commands.append({'name':name,**data})
archives=[]
for p in sorted(pathlib.Path('/tmp/story83-authorized-pack-20261009').glob('*.nupkg')):
 with zipfile.ZipFile(p) as archive:
  nuspec=ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))));repository=next((e.attrib for e in nuspec.iter() if e.tag.split('}')[-1]=='repository'),{})
 archives.append({'path':str(p),'sha256':sha(p),'bytes':p.stat().st_size,'repositoryMetadata':repository})
assert len(archives)==14 and not any('PayloadProtection' in e['path'] for e in archives)
sealed_packages=json.loads((out/'authorized-packages-before-tests.json').read_text())
assert [{'path':e['path'],'sha256':e['sha256'],'bytes':e['bytes']} for e in archives]==sealed_packages,'archives changed across validators/consumer tests'
inputs=json.loads((out/'authorized-evaluated-inputs.log').read_text());input_hashes=[]
for kind,items in inputs['Items'].items():
 for e in items:
  p=pathlib.Path(e.get('FullPath',e['Identity']))
  if not p.is_absolute():p=root/'tests/Hexalith.EventStore.PayloadProtection.Tests'/p
  if p.is_file():input_hashes.append({'itemType':kind,'path':str(p),'sha256':sha(p)})
for s in inputs['Properties']['MSBuildAllProjects'].split(';'):
 p=pathlib.Path(s)
 if p.is_file():input_hashes.append({'itemType':'MSBuildAllProjects','path':s,'sha256':sha(p)})
result={'schemaVersion':1,'story':'8.3','recordedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'approvalStatus':'closure under the existing conditional authorization in AR-20260914-02 after all required checks pass; no new human approval inferred','successorAuthorization':'not granted; EventStore 8.4 remains backlog','g5':'unmet; Parties 8.7 blocked','initialHead':json.loads((out/'before.json').read_text())['head'],'compilationHead':compiled['head'],'currentHead':final['head'],'approvedBaseline':old['approvedBaseline'],'normativeDigest':old['normativeDigest'],'frozenSpecSha256':hashlib.sha256(frozen_block((root/spec).read_bytes())).hexdigest(),'frozenArtifacts':frozen,'approvedSecurityInventory':old['approvedSecurityInventory'],'reviewedCore':core,'reviewedTests':tests,'fullCompiledCore':stream(final['core']),'fullCompiledTests':stream(final['tests']),'fullCompiledSecurity':stream(final['security']),'sourceGraph':{'compiledSnapshot':'reconciled-compiled.json','finalSnapshot':'verified-source.json','coreTestsAndSecurityByteStable':True,'dependencyByteStable':True,'focusedExecutedBinariesUnchanged':True,'concurrentCommits':'external sessions advanced history; this workflow did not stage, commit, push or branch'},'dependencies':{s:{'head':v.get('head'),'status':v.get('status'),'sourceStream':stream(v.get('source',[]))} for s,v in final['submodules'].items()},'focused':focused,'ownedCoreCases':324,'excludedConcurrentPrototypeCases':33,'invariant':invariant,'contracts':contracts,'vectors':vectors,'commands':commands,'packages':{'version':'3.117.2-story83-validation','purpose':'local validation only; not published or approved for release','archives':archives},'evaluatedInputs':input_hashes,'evaluatedProperties':inputs['Properties'],'review':json.loads((out/'review.json').read_text()),'closureBuildRepairs':['IDeletionProtectionOwner.ReconcileBlockedReplacementAsync: null argument guard','DeletionConsumptionActor.ReadBlockedReplacementAsync: null argument guard'],'preservation':'Original 46 approved Security files and frozen approvals/authority/fixtures/solution/14-package manifest unchanged. Concurrent additions preserved and not granted later-story approval.'}
result['approvalStatus']='Story 8.3 closure under preserved conditional AR-20260914-02 authority after required checks passed; user explicitly authorized the separate capture remediation. No retroactive human review of different implementation bytes inferred.'
result['sourceGraph'].update({'compiledSnapshot':'authorized-compiled.json','beforeSnapshot':'before-authorized-build.json','buildBoundaryByteStable':True,'rootSourceAndConfigurationByteStableThroughFinalSnapshot':not live_drift,'changesAfterBuild':live_drift,'selfAuthoredReceiptAttributeChange':any(e['path']=='.gitattributes' for e in live_drift),'lateConcurrentSourcePaths':[e['path'] for e in live_drift if e['path'].startswith(('src/','tests/'))],'coreTestsAndSecurityByteStable':compiled['security']==final['security'],'mutableClosureRecordsExcludedFromComparison':sorted(mutable_closure_records),'identityScope':'Exact compiled/tested snapshot, compared with the final captured working tree; later concurrent edits are disclosed and are not represented as compiled or approved.'})
result['fullCompiledSecurity']=stream(compiled['security'])
result['currentSecurity']=stream(final['security'])
result['rootSourceAndConfiguration']=stream(final['trackedFiles'])
runtime=json.loads((out/'runtime-before-authorized-tests.json').read_text())
for e in runtime:assert sha(root/e['path'])==e['sha256'],e['path']+' changed across final tests'
runtime_paths=set()
for name in ['PayloadProtection','Contracts','Client','Server']:
 runtime_paths.update(str(p.relative_to(root)) for p in (root/'tests'/f'Hexalith.EventStore.{name}.Tests/bin/Release/net10.0').rglob('*') if p.is_file())
assert runtime_paths=={e['path'] for e in runtime},'runtime inventory changed'
result['executedRuntimeFiles']=runtime
result['sourceGraph']['focusedCompilation']='authorized-focused-build explicitly compiles the engine/test project after the broad solution build; the engine remains excluded from the solution. Source/dependency inventories and exact runtime bytes are bound to authorized focused/invariant executions.'
failure=json.loads((out/'closure-secrets-check.command.json').read_text())
assert failure['exitCode']!=0 and failure['logSha256']==sha(out/'closure-secrets-check.log')
result['failedCheckCommand']=failure
consumer=json.loads((out/'authorized-package-consumers.command.json').read_text())
assert consumer['exitCode']==0 and consumer['logSha256']==sha(out/'authorized-package-consumers.log')
result['packageConsumerProof']=consumer
result['postPackEvaluatedInputs']=result.pop('evaluatedInputs')
result['postPackEvaluatedProperties']=result.pop('evaluatedProperties')
result['evaluatedInputsCaveat']='Post-pack evaluated references are diagnostic only; they do not claim to be the earlier compilation inputs. The pre/post source snapshots and compiled artifact inventories identify the build and focused runtime copies separately.'
result['supplementalServerFailure']=trx('server-results.trx.xml',allow_existing_skips=True,preserve_failure=True)
result['historicalSecretsCheckFailure']=trx('closure-secrets-results.trx.xml',preserve_failure=True)
result['repositoryGuardResults']=trx('authorized-secrets-results.trx.xml')
result['finalCandidateArtifactScan']=trx('final-candidate-artifact-scan.trx.xml')
result['currentClient']=trx('authorized-client-results.trx.xml')
result['currentDeletion']=trx('authorized-deletion-results.trx.xml')
result['resolvedHistoricalBlocker']=json.loads((out/'supplemental-scan-blocker.json').read_text())
result['remediationAuthorization']=json.loads((out/'remediation-authorization.json').read_text())
result['captureRemediation']=json.loads((root/'_bmad-output/implementation-artifacts/evidence/story-6-6/otlp-capture-remediation-2026-10-09/disposition.json').read_text())
assert not live_drift,'live code/configuration still differs from authorized tested candidate'
assert compiled['security']==final['security'],'live Security differs from tested candidate'
assert sha(out/'before-authorized-remediation-binding.json')=='2d9f6bb7e4f2006249fe249e081902b7015d3d3802393b62796650aad9eb6bb6','historical binding raw bytes changed'
for key,value in result.items():
 if isinstance(value,dict) and key not in ['focused','invariant'] and 'rows' in value:value.pop('rows')
result['receipts']=[{'path':str(p.relative_to(out)),'sha256':sha(p),'bytes':p.stat().st_size} for p in sorted(out.rglob('*')) if p.is_file() and p.name not in ['binding.json','subject.json','README.md','approval-request.md']]
(out/'binding.json').write_text(json.dumps(result,indent=2)+'\n')
subject={'story':'8.3','decision':'Story 8.3 closed under AR-20260914-02 after successful required verification and authorized capture remediation; no successor or G5 authorization','bindingSha256':sha(out/'binding.json'),'head':final['head'],'reviewedCoreSha256':core['sha256sumStreamSha256'],'reviewedTestsSha256':tests['sha256sumStreamSha256'],'normativeDigest':old['normativeDigest']}
(out/'subject.json').write_text(json.dumps(subject,indent=2)+'\n');print(json.dumps(subject,indent=2));print('Subject SHA-256:',sha(out/'subject.json'))
