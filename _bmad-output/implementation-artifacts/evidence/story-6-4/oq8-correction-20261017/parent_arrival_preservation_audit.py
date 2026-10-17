"""Preserve external arrivals and qualify the retained runtime frame without claiming current source acceptance."""
import datetime,hashlib,json,pathlib,subprocess
HERE=pathlib.Path(__file__).resolve().parent
ROOT=HERE.parents[4]
WRAPPER='_bmad-output/implementation-artifacts/spec-6-4-projection-cost-and-sequence-guard-implementation.md'
def sha(path):return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()
def main():
 entry=json.loads((HERE/'entry.json').read_text())
 protected=entry['protected']+json.loads((HERE/'parent-test-tool-entry.json').read_text())['files']
 head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
 external=set(subprocess.check_output(['git','diff','--name-only',entry['head'],head],cwd=ROOT,text=True).splitlines())
 changed=[]
 for row in protected:
  path=ROOT/row['path']
  if not path.is_file() or sha(path)!=row['sha256']:
   assert row['path'] in external,row['path']
   changed.append({'path':row['path'],'entrySha256':row['sha256'],'currentSha256':sha(path) if path.is_file() else None})
 original=(HERE/'wrapper-entry.md').read_bytes()
 assert hashlib.sha256(original).hexdigest()==entry['wrapper']['sha256']
 assert (ROOT/WRAPPER).read_bytes().startswith(original)
 producer=json.loads((HERE/'release-build-baseline.json').read_text())
 candidate=producer['after']['sourceInputs']
 frames=[];archives=set()
 for lane in ['release-build-baseline','oq8-baseline','release-live-full']:
  packet=json.loads((HERE/(lane+'.json')).read_text())
  assert packet['before']['sourceInputs']==candidate
  if lane!='release-build-baseline':
   assert not packet['assetsChanges'] and not packet['runtimeChanges']
  assert packet['before']['graph']['modules']==packet['after']['graph']['modules']
  for phase in ['before','after']:
   retained={**packet[phase]['assets'],**{n:h for n,h in packet[phase]['runtime'].items() if n.endswith('.json')}}
   for name,digest in retained.items():
    assert sha(HERE/'asset-bytes'/digest)==digest,(lane,name)
    archives.add(digest)
  frames.append({'lane':lane,'beforeHead':packet['before']['graph']['head'],'afterHead':packet['after']['graph']['head'],'sourceInputChanges':packet['sourceInputsChanges'],'assetsChanges':packet['assetsChanges'],'runtimeChanges':packet['runtimeChanges']})
 full=json.loads((HERE/'release-live-full.json').read_text())
 current=full['after']['sourceInputs']
 assert all((ROOT/name).is_file() and sha(ROOT/name)==digest for name,digest in current.items())
 assert set(full['sourceInputsChanges'])<=external
 assert all(n.startswith(('src/','tests/','docs/')) for n in full['sourceInputsChanges'])
 assert subprocess.run(['git','diff','--cached','--exit-code','--quiet',head],cwd=ROOT).returncode==0
 actual=set(subprocess.check_output(['git','diff','--name-only'],cwd=ROOT,text=True).splitlines())
 assert actual=={WRAPPER},actual
 check=subprocess.run(['git','diff','--check'],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
 assert check.returncode==0,check.stdout
 normative=ROOT/'_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md'
 expected=next(row['sha256'] for row in entry['protected'] if row['path']==str(normative.relative_to(ROOT)))
 assert sha(normative)==expected
 report={'checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scriptSha256':sha(__file__),'entryHead':entry['head'],'currentHead':head,'headAdvancedExternally':head!=entry['head'],'entryProtectedFileObjects':len(protected),'entryProtectedUnchangedObjects':len(protected)-len(changed),'changedProtectedObjectsAllInExternalCommit':changed,'externalCommitPaths':sorted(external),'nativeSourceInputChangesDuringFull':full['sourceInputsChanges'],'producerSourceInputCount':len(candidate),'currentSourceInputCount':len(current),'producerSourceMatchesBothExecutionEntries':True,'effectiveAssetRuntimeArchivesVerified':len(archives),'moduleIdentitiesStableDuringFrames':True,'nativeAssetsRuntimeStable':True,'originalWrapperBytesPreserved':len(original),'wrapperAppendOnly':True,'normativeFullFileUnchanged':True,'indexStageMatchesCurrentExternalHead':True,'trackedUncommittedChanges':[WRAPPER],'gitDiffCheckExitCode':0,'frames':frames,'latestSourceQualified':False,'scope':'Producer/runtime-frame preservation only. External committed Story 3.6 source and docs arrivals were preserved; this pass neither authored nor requalified them. P1R/normative/dependency/policy bytes remain unchanged. Original pre-arrival audit drafts are retained unexecuted; no failed native result or gate was relabeled.'}
 target=HERE/'parent-arrival-preservation-audit.json';assert not target.exists()
 target.write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps({'entryProtectedObjects':len(protected),'unchangedProtectedObjects':len(protected)-len(changed),'externallyChangedProtectedObjects':len(changed),'nativeSourceChanges':len(full['sourceInputsChanges']),'entryHead':entry['head'],'currentHead':head,'latestSourceQualified':False,'rawAssetRuntimeJsonObjectsVerified':len(archives)}))
if __name__=='__main__':main()
