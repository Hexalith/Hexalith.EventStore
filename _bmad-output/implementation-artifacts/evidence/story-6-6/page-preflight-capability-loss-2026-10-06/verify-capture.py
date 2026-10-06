import hashlib,json
from pathlib import Path
base=Path(__file__).resolve().parent
capture=json.loads((base/'capture.json').read_text())
checked=[]
def verify(value):
    if isinstance(value,dict):
        if {'path','bytes','sha256'} <= value.keys():
            p=Path(value['path']); data=p.read_bytes()
            assert len(data)==value['bytes'], str(p)
            assert hashlib.sha256(data).hexdigest()==value['sha256'], str(p)
            checked.append(str(p))
        else:
            for child in value.values(): verify(child)
    elif isinstance(value,list):
        for child in value: verify(child)
verify(capture)
assert capture['status']=='in-progress' and capture['open_parent_tasks']==8
print(json.dumps({'result':'passed','matched_sha256_records':len(checked),'owned_source_files':len(capture['owned_source_files']),'compiled_artifacts':len(capture['compiled_artifacts']),'scope':'local retained-file consistency; no production or activation authority'},sort_keys=True))
