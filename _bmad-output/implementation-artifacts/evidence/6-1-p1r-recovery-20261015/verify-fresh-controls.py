"""Fresh focused verification; retain literal commands and independent output bytes."""
from pathlib import Path
import concurrent.futures
import datetime
import hashlib
import json
import os
import shlex
import subprocess
import sys
import time
ROOT = Path('/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore')
OUT = ROOT/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-recovery-20261015'
OLD = ROOT/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014'
assert (OUT/'recovery-result.json').is_file()

def sha(data):
 return hashlib.sha256(data).hexdigest()

def write_json(name, value):
 with (OUT/name).open('x') as stream:
  json.dump(value,stream,indent=2); stream.write('\n')

def run(name, argv, timeout=600, cwd=ROOT):
 start = datetime.datetime.now(datetime.timezone.utc).isoformat()
 began = time.monotonic()
 log = OUT/(name+'.log')
 with log.open('xb') as stream:
  try:
   result = subprocess.run(argv,cwd=cwd,stdout=stream,stderr=subprocess.STDOUT,timeout=timeout)
   code = result.returncode
   timed_out = False
  except subprocess.TimeoutExpired:
   code = None; timed_out = True
 record = {'argv':argv,'command':shlex.join(argv),'cwd':str(cwd),'started_at_utc':start,
   'duration_seconds':round(time.monotonic()-began,3),'exit_code':code,'timed_out':timed_out,
   'log':log.name,'log_bytes':log.stat().st_size,'log_sha256':sha(log.read_bytes()),
   'source_binding':'fresh-source-binding.json','passed':code==0}
 write_json(name+'.json',record)
 print(json.dumps({'name':name,'exit_code':code,'duration_seconds':record['duration_seconds']}),flush=True)
 return record

(OUT/'verify-fresh-controls.py').write_bytes(Path(__file__).read_bytes())
# Copy the exact original compiler evaluator into a new sibling bundle. Its ROOT/OUT
# derivation now points at this new directory; no historical output is touched.
with (OUT/'verify-compiler-enrollment.py').open('xb') as stream:
 stream.write((OLD/'verify-compiler-enrollment.py').read_bytes())
# Bind fresh checks to all active production/test/tool inputs and build configuration.
paths = set()
for directory in ['src','tests','tools']:
 paths.update(p for p in (ROOT/directory).rglob('*') if p.is_file() and not {'bin','obj','__pycache__'}.intersection(p.parts) and p.suffix in ['.cs','.csproj','.props','.targets','.py','.json','.yml','.yaml','.sh'])
paths.update(ROOT/name for name in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','.editorconfig','.gitattributes','.github/workflows/p1r-qualification.yml'])
paths.update(ROOT.parent/'Hexalith.Builds'/name for name in ['Props/Directory.Packages.props','Hexalith.Build.props','Hexalith.Package.props'] if (ROOT.parent/'Hexalith.Builds'/name).is_file())
paths.update(ROOT/entry['path'] for entry in json.loads((OLD/'required-ci-test-dependencies.json').read_text())['files'])
binding = {'captured_at_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
 'owner_head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
 'reviewed_manifest_sha256':sha((OLD/'reviewed-source.json').read_bytes()),'scope':'current checkout source checks; no successor restore or package qualification',
 'files':[{'path':str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p), 'bytes':p.stat().st_size,'sha256':sha(p.read_bytes())} for p in sorted(paths)]}
write_json('fresh-source-binding.json',binding)
records=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
 futures=[]
 for name in ['published_executor','isolation','published_qualification','qualification']:
  futures.append(pool.submit(run,'python-'+name,['python3','-B','-m','unittest','discover','-s','tools/tests','-p','test_p1r_'+name+'.py','-v']))
 for future in concurrent.futures.as_completed(futures):
  records.append(future.result())
write_json('python-verification.json',{'scope':'fresh source/tooling controls only','commands':records,'passed':all(x['passed'] for x in records)})
records.append(run('server-build',['dotnet','build','tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj','--configuration','Debug','-m:1','-p:UseHexalithProjectReferences=true']))
if records[-1]['passed']:
 records.append(run('server-controls',['dotnet','tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll',
 '-class','*EventStreamReaderTests','-class','*SnapshotManagerTests','-class','*AggregateActorManualSnapshotTests','-class','*AggregateActorFoldedSnapshotMigrationTests']))
records.append(run('compiler-controls',['python3','-B',str(OUT/'verify-compiler-enrollment.py')]))
write_json('fresh-verification.json',{'scope':'fresh current checkout source verification; no historical result substitution','commands':records,
 'passed':all(x['passed'] for x in records),'package_qualified':False,'p1r_usable':False,'story_6_1':'blocked'})
