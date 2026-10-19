import hashlib,json,re,sys,time
from pathlib import Path
root=Path(__file__).resolve().parents[5]
base=Path(__file__).resolve().parent
source=Path('/tmp/p1r-31251-tagged-source-import-da30571ce90c4296ba51b1d98b3c1775')
sys.path.insert(0,str(root/'tools'))
import p1r_qualification as preparation
import p1r_published_qualification as qualification
target=base/'final-source-freeze-v4.json'
assert not target.exists()
records=json.loads((base/'seven-suites.json').read_bytes())
assert len(records)==7 and all(row['exit_code']==0 and row['ok'] and not row['skips'] for row in records)
retained=[]
for row in records:
    path=Path(row['record'])
    record=json.loads(path.read_bytes())
    log=Path(record['output_log'])
    text=log.read_text()
    assert record['exit_code']==0 and text.rstrip().endswith('OK') and 'skipped=' not in text
    assert int(re.findall(r'^Ran (\d+) tests? in ',text,re.M)[-1])==row['tests']
    retained.append({**row,'record_sha256':preparation.digest(path.read_bytes()),'log_sha256':preparation.digest(log.read_bytes())})
for name in ('python-compilation.json','diff-check.json'):
    record=json.loads((base/name).read_bytes())
    assert record['exit_code']==0
    retained.append({'record':str(base/name),'record_sha256':preparation.digest((base/name).read_bytes()),
                     'log_sha256':preparation.digest(Path(record['output_log']).read_bytes())})
samples=[]
binding=None
for index in range(3):
    start=time.monotonic()
    observed=preparation.source_binding(root,source)
    sha=preparation.digest(preparation.canonical(observed))
    samples.append({'canonical_binding_sha256':sha,'seconds':time.monotonic()-start})
    if binding is None:binding=observed
    else:assert binding==observed
inputs=json.loads((Path('/tmp/p1r-31251-verifier-final-78617511ec9c4208b1267e1cde6e21bf')/base.parent.relative_to(root)/'observation-6508b7c575884cce90582e036e06e6e8/owner-inputs.json').read_bytes())
qualification.validate_candidate_source(binding,inputs)
path=base/'final-source-binding-host-v4.json'
preparation.write_json(path,binding)
result={'scope':'frozen V4 verifier tooling and exact-tag S3 candidate source; host interpreter recorded separately from later private proof',
        'frozen_utc':preparation.stamp(),'verifier_root':str(root),'source_root':str(source),
        'canonical_binding_sha256':samples[0]['canonical_binding_sha256'],
        'binding':{'path':str(path),'sha256':preparation.digest(path.read_bytes())},
        'verification':retained,'total_tests':sum(row['tests'] for row in records),'binding_samples':samples,
        'qualified':False,'p1r_usable':False}
preparation.write_json(target,result)
print(json.dumps(result,indent=2))
