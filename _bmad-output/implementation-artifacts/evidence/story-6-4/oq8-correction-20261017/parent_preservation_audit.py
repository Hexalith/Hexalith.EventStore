"""Read-only audit of entry preservation and the finite runtime closure."""
import datetime, hashlib, json, pathlib, subprocess
HERE=pathlib.Path(__file__).resolve().parent
ROOT=HERE.parents[4]
WRAPPER='_bmad-output/implementation-artifacts/spec-6-4-projection-cost-and-sequence-guard-implementation.md'
def sha(path): return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()
def main():
 entry=json.loads((HERE/'entry.json').read_text())
 protected=entry['protected']+json.loads((HERE/'parent-test-tool-entry.json').read_text())['files']
 changed=[row['path'] for row in protected if not (ROOT/row['path']).is_file() or sha(ROOT/row['path'])!=row['sha256']]
 assert not changed,changed
 original=(HERE/'wrapper-entry.md').read_bytes()
 assert hashlib.sha256(original).hexdigest()==entry['wrapper']['sha256']
 assert (ROOT/WRAPPER).read_bytes().startswith(original)
 producer=json.loads((HERE/'release-build-baseline.json').read_text())
 candidate=producer['after']['sourceInputs']
 assert all((ROOT/name).is_file() and sha(ROOT/name)==digest for name,digest in candidate.items())
 archives=set()
 for lane in ['release-build-baseline','oq8-baseline','release-live-full']:
  packet=json.loads((HERE/(lane+'.json')).read_text())
  for phase in ['before','after']:
   assert packet[phase]['sourceInputs']==candidate
   retained={**packet[phase]['assets'],**{n:h for n,h in packet[phase]['runtime'].items() if n.endswith('.json')}}
   for name,digest in retained.items():
    assert sha(HERE/'asset-bytes'/digest)==digest,(lane,name)
    archives.add(digest)
 head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
 assert head==entry['head']
 staged=subprocess.run(['git','diff','--cached','--exit-code','--quiet',head],cwd=ROOT).returncode
 assert staged==0
 actual=set(subprocess.check_output(['git','diff','--name-only'],cwd=ROOT,text=True).splitlines())
 assert actual=={WRAPPER},actual
 check=subprocess.run(['git','diff','--check'],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
 assert check.returncode==0,check.stdout
 report={'checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scriptSha256':sha(__file__),'head':head,'entryProtectedFileObjectsUnchanged':len(protected),'allRuntimeFramesUseSameSourceInventory':True,'sourceInputCount':len(candidate),'productionTestDependencyPolicyChanges':[],'wrapperOriginalBytesPreserved':len(original),'wrapperAppendOnly':True,'indexStageMatchesEntryHead':True,'rawIndexBytesUnchanged':sha(ROOT/'.git/index')==entry['indexSha256'],'rawAssetRuntimeJsonObjectsVerified':len(archives),'gitDiffCheck':{'command':['git','diff','--check'],'exitCode':0},'scope':'Entry preservation and local candidate identity only; no inferred authenticated CI/owner assurance or formal acceptance.'}
 target=HERE/'parent-preservation-audit.json';assert not target.exists()
 target.write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps(report))
if __name__=='__main__':main()
