import hashlib,json,subprocess,sys,uuid
from pathlib import Path
root=Path(__file__).resolve().parents[4]
base=Path(__file__).resolve().parent
source=Path('/tmp/p1r-31251-tagged-source-import-da30571ce90c4296ba51b1d98b3c1775')
oldbase=Path('/tmp/p1r-31251-verifier-final-78617511ec9c4208b1267e1cde6e21bf')/base.relative_to(root)
original=Path('/tmp/p1r-31251-verifier-38d60cf41afc4f2d99b4b377c016f95c')/base.relative_to(root)/'outer-shared-before.json'
index=base/'snapshot-smoke-v4-index-v2.json'
assert not index.exists()
sys.path.insert(0,str(root/'tools'))
import p1r_qualification as preparation
freeze=base/'verification-import/final-source-freeze-v4.json'
assert preparation.digest(preparation.canonical(preparation.source_binding(root,source)))==json.loads(freeze.read_bytes())['canonical_binding_sha256']
identity=uuid.uuid4().hex
smoke=base/('snapshot-get-smoke-'+identity)
result={'scope':'fresh bounded real snapshot/GET fixture control','smoke':str(smoke),
        'source_freeze':{'path':str(freeze),'sha256':hashlib.sha256(freeze.read_bytes()).hexdigest()},
        'started_utc':preparation.stamp(),'finished_utc':None,'commands':[],'errors':[]}
def save():preparation.write_json(index,result)
def command(label,argv):
    target=base/(label+'-command-'+identity+'.json')
    code=subprocess.run(['python3',str(base/'verification-import/record-command.py'),str(target),*map(str,argv)],cwd=root).returncode
    result['commands'].append({'label':label,'record':str(target),'exit_code':code})
    save()
    return code
save()
try:
    before=base/'outer-shared-before-snapshot-smoke-v4-v2.json'
    assert command('snapshot-outer-before',['python3',base/'capture-outer-identities.py','--out',before,
        '--before',oldbase/'outer-shared-after-canonical-final.json','--before',original])==0
    code=command('snapshot-smoke',['python3',root/'tools/p1r-isolated-runner.py','--out',smoke.with_name(smoke.name+'-isolation'),
        '--source-root',source,'--','python3',base/'snapshot-get-smoke-31251-v2.py','--verifier-root',root,
        '--source-root',source,'--inputs',base/'smoke-input-copy/owner-inputs.json','--out',smoke])
    assert command('snapshot-outer-after',['python3',base/'capture-outer-identities.py',
        '--out',base/'outer-shared-after-snapshot-smoke-v4-v2.json','--before',before,'--before',original])==0
    assert code==0,'bounded private control retained a nonzero exit'
except Exception as error:
    result['errors'].append({'type':type(error).__name__,'message':str(error)})
finally:
    result['finished_utc']=preparation.stamp()
    save()
print(json.dumps(result,indent=2))
raise SystemExit(2 if result['errors'] else 0)
